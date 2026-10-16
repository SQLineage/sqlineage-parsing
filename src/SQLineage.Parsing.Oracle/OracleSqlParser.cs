using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;

namespace SQLineage.Parsing.Oracle;

public sealed record OracleParseDiagnostic(int Line, int Column, string Message, int Offset = -1, bool IsLexerError = false);

/// <summary>Per-call syntax objects. The facade retains none of these after the callback.</summary>
public sealed record OracleParseResult(PlSqlParser Parser, CommonTokenStream Tokens,
    PlSqlParser.Sql_scriptContext Tree, IReadOnlyList<OracleParseDiagnostic> Diagnostics);

/// <summary>Oracle parsing with exclusive, bounded, per-instance prediction-cache reuse.</summary>
public sealed class OracleSqlParser
{
    private PredictionCaches? _idleCache;
    private const int MaxCachedScripts = 16;
    private const int MaxCachedCharacters = 1024 * 1024;
    private const int MaxCachedStates = 2048;
    private const int MaxCachedConfigurations = 250_000;
    private const int MaxCachedContexts = 16_384;

    public TResult Parse<TResult>(string sql, Func<OracleParseResult, TResult> consume)
        => WithSession(session => session.Parse(sql, consume));

    /// <summary>
    /// Leases caches for a synchronous group of parses, including nested dynamic SQL.
    /// Each concurrent lease owns independent caches. Product recursion budgets remain the caller's concern.
    /// A session expires when this callback returns; no trees, token streams or interpreters are cached.
    /// </summary>
    public TResult WithSession<TResult>(Func<Session, TResult> consume)
    {
        ArgumentNullException.ThrowIfNull(consume);
        PredictionCaches cache = Interlocked.Exchange(ref _idleCache, null) ?? new();
        Session session = new(cache);
        bool completed = false;
        try
        {
            TResult result = consume(session);
            completed = true;
            return result;
        }
        finally
        {
            session.Close();
            if (completed && cache.CanReuse())
            {
                Interlocked.CompareExchange(ref _idleCache, cache, null);
            }
        }
    }

    public sealed class Session
    {
        private readonly object _gate = new();
        private PredictionCaches? _cache;
        internal Session(PredictionCaches cache) => _cache = cache;

        public TResult Parse<TResult>(string sql, Func<OracleParseResult, TResult> consume,
            Func<PlSqlParser, PlSqlParser.Sql_scriptContext>? parse = null)
        {
            ArgumentNullException.ThrowIfNull(sql);
            ArgumentNullException.ThrowIfNull(consume);
            lock (_gate)
            {
                PredictionCaches cache = _cache ?? throw new ObjectDisposedException(nameof(Session));
                try
                {
                    cache.Scripts++;
                    cache.Characters += sql.Length;
                    LocalCacheLexer lexer = new(new AntlrInputStream(sql), cache);
                    CollectingErrorListener listener = new();
                    lexer.RemoveErrorListeners();
                    lexer.AddErrorListener(listener);
                    CommonTokenStream tokens = new(lexer);
                    LocalCacheParser parser = new(tokens, cache);
                    parser.RemoveErrorListeners();
                    parser.AddErrorListener(listener);
                    PlSqlParser.Sql_scriptContext tree = parse is null ? parser.sql_script() : parse(parser);
                    return consume(new(parser, tokens, tree, listener.Errors.AsReadOnly()));
                }
                catch
                {
                    cache.Failed = true;
                    throw;
                }
            }
        }

        internal void Close()
        {
            lock (_gate) { _cache = null; }
        }
    }

    private sealed class CollectingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public List<OracleParseDiagnostic> Errors { get; } = [];
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => Errors.Add(new(line, charPositionInLine + 1, msg, offendingSymbol.StartIndex));
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => Errors.Add(new(line, charPositionInLine + 1, msg, IsLexerError: true));
    }

    internal sealed class PredictionCaches
    {
        public DFA[]? Lexer { get; set; }
        public DFA[]? Parser { get; set; }
        public PredictionContextCache LexerContexts { get; } = new();
        public PredictionContextCache ParserContexts { get; } = new();
        public int Scripts { get; set; }
        public long Characters { get; set; }
        public bool Failed { get; set; }

        public bool CanReuse()
        {
            if (Failed || Scripts >= MaxCachedScripts || Characters >= MaxCachedCharacters
                || LexerContexts.Count + ParserContexts.Count > MaxCachedContexts) { return false; }
            int states = 0;
            long configurations = 0;
            foreach (DFA decision in (Lexer ?? []).Concat(Parser ?? []))
            {
                states += decision.states.Count;
                if (states > MaxCachedStates) { return false; }
                foreach (DFAState state in decision.states.Keys)
                {
                    configurations += state.configSet.Count;
                    if (configurations > MaxCachedConfigurations) { return false; }
                }
            }
            return true;
        }
    }

    private sealed class LocalCacheLexer : PlSqlLexer
    {
        public LocalCacheLexer(ICharStream input, PredictionCaches cache) : base(input)
            => Interpreter = new LexerATNSimulator(this, Atn, cache.Lexer ??= CreateDecisionCache(Atn), cache.LexerContexts);
    }
    private sealed class LocalCacheParser : PlSqlParser
    {
        public LocalCacheParser(ITokenStream input, PredictionCaches cache) : base(input)
            => Interpreter = new ParserATNSimulator(this, Atn, cache.Parser ??= CreateDecisionCache(Atn), cache.ParserContexts);
    }
    private static DFA[] CreateDecisionCache(ATN atn)
    {
        DFA[] decisions = new DFA[atn.NumberOfDecisions];
        for (int i = 0; i < decisions.Length; i++) { decisions[i] = new DFA(atn.GetDecisionState(i), i); }
        return decisions;
    }
}
