using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using SQLineage.Parsing.Oracle;

public sealed class OracleConsumptionTests
{
    [Theory]
    [InlineData("CREATE OR REPLACE EDITIONABLE SYNONYM reporting.current_orders FOR app.orders;")]
    [InlineData("CREATE NONEDITIONABLE PUBLIC SYNONYM current_orders FOR app.orders@sales.world;")]
    [InlineData("CREATE OR REPLACE PACKAGE BODY app.pkg AS PROCEDURE run IS BEGIN INSERT INTO app.target(id) SELECT o.id FROM app.orders o; END; END;")]
    [InlineData("BEGIN EXECUTE IMMEDIATE 'SELECT o.id FROM app.orders o'; END;")]
    public void GrammarFeaturesParseWithFullContextAndNormalRecovery(string sql)
    {
        new OracleSqlParser().Parse(sql, result =>
        {
            Assert.Empty(result.Diagnostics);
            Assert.Equal(PredictionMode.LL, result.Parser.Interpreter.PredictionMode);
            Assert.IsType<DefaultErrorStrategy>(result.Parser.ErrorHandler);
            Assert.Equal("", typeof(PlSqlParser).Namespace ?? "");
            Assert.NotNull(result.Tree);
            return 0;
        });
    }

    [Fact]
    public void MalformedInputReportsPositionAndKeepsPartialTree()
    {
        new OracleSqlParser().Parse("SELECT o.id FROM app.orders o WHERE ;\nSELECT c.id FROM app.customers c;", result =>
        {
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, error => { Assert.True(error.Line > 0); Assert.True(error.Column > 0); });
            Assert.Contains("customers", result.Tree.GetText(), StringComparison.OrdinalIgnoreCase);
            return 0;
        });
    }

    [Fact]
    public void NestedSessionParsesShareLeaseAndExpiredSessionCannotRetainCache()
    {
        OracleSqlParser parser = new();
        OracleSqlParser.Session? saved = null;
        parser.WithSession(session =>
        {
            saved = session;
            return session.Parse("SELECT id FROM app.orders;", outer =>
                session.Parse("SELECT id FROM app.customers;", inner =>
                {
                    Assert.Same(outer.Parser.Interpreter.decisionToDFA, inner.Parser.Interpreter.decisionToDFA);
                    Assert.Empty(inner.Diagnostics);
                    return 0;
                }));
        });
        Assert.Throws<ObjectDisposedException>(() => saved!.Parse("SELECT 1 FROM dual;", _ => 0));
    }

    [Fact]
    public async Task ConcurrentLeasesUseIndependentCaches()
    {
        OracleSqlParser parser = new();
        using CountdownEvent entered = new(2);
        using ManualResetEventSlim release = new();
        ConcurrentBag<object> caches = [];
        Task<int>[] calls = Enumerable.Range(0, 2).Select(i => Task.Factory.StartNew(
            () => parser.Parse($"SELECT id FROM app.orders{i};", result =>
            {
                caches.Add(result.Parser.Interpreter.decisionToDFA);
                entered.Signal();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15)));
                Assert.Empty(result.Diagnostics);
                return 0;
            }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        bool overlap;
        try { overlap = entered.Wait(TimeSpan.FromSeconds(15)); }
        finally { release.Set(); }
        await Task.WhenAll(calls);
        Assert.True(overlap);
        Assert.Equal(2, caches.Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    [Fact]
    public void WarmCacheDoesNotRetainInputTreeParserOrTokens()
    {
        OracleSqlParser parser = new();
        WeakReference[] objects = ParseAndObserve(parser);
        Collect();
        Assert.All(objects, value => Assert.False(value.IsAlive));
        GC.KeepAlive(parser);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LifetimeAndFailureDiscardCache(bool fail)
    {
        OracleSqlParser parser = new();
        List<WeakReference> caches = [];
        for (int i = 0; i < 16; i++)
        {
            if (fail) { Assert.Throws<InvalidOperationException>(() => ObserveCache(parser, caches, fail)); }
            else { ObserveCache(parser, caches, fail); }
        }
        Collect();
        Assert.All(caches, value => Assert.False(value.IsAlive));
        GC.KeepAlive(parser);
    }

    [Fact]
    public void LargeInputDiscardsCache()
    {
        OracleSqlParser parser = new();
        WeakReference cache = parser.Parse("SELECT id FROM app.orders; --" + new string('x', 1024 * 1024),
            result => new WeakReference(result.Parser.Interpreter.decisionToDFA));
        Collect();
        Assert.False(cache.IsAlive);
        GC.KeepAlive(parser);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OversizedStateOrConfigurationTablesAreNotRetained(bool manyStates)
    {
        OracleSqlParser parser = new();
        WeakReference cache = InflateTable(parser, manyStates);
        Collect();
        Assert.False(cache.IsAlive);
        Assert.Equal(0, parser.Parse("SELECT id FROM app.customers;", result => result.Diagnostics.Count));
        GC.KeepAlive(parser);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference InflateTable(OracleSqlParser parser, bool manyStates)
        => parser.Parse("SELECT id FROM app.orders;", result =>
        {
            WeakReference cache = new(result.Parser.Interpreter.decisionToDFA);
            DFA decision = new(new BasicBlockStartState(), 0);
            if (manyStates)
            {
                for (int i = 0; i <= 2048; i++)
                {
                    DistinctState state = new();
                    decision.states.Add(state, state);
                }
            }
            else
            {
                DistinctState state = new();
                for (int i = 0; i <= 250_000; i++) { state.configSet.configs.Add(null!); }
                decision.states.Add(state, state);
            }
            result.Parser.Interpreter.decisionToDFA[0] = decision;
            return cache;
        });

    private sealed class DistinctState() : DFAState(new ATNConfigSet())
    {
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ParseAndObserve(OracleSqlParser parser)
        => parser.Parse("SELECT id FROM app.orders;", result => new[]
        {
            new WeakReference(result.Parser), new WeakReference(result.Tree), new WeakReference(result.Tokens),
            new WeakReference(result.Tokens.TokenSource), new WeakReference(result.Parser.Interpreter)
        });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ObserveCache(OracleSqlParser parser, List<WeakReference> caches, bool fail)
        => parser.Parse("SELECT id FROM app.orders;", result =>
        {
            caches.Add(new WeakReference(result.Parser.Interpreter.decisionToDFA));
            if (fail) { throw new InvalidOperationException("Simulated consumer failure"); }
            return 0;
        });

    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
}
