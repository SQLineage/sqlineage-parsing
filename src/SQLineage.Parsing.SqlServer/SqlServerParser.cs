using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLineage.Parsing.SqlServer;

/// <summary>A parsed syntax tree and diagnostics, without lineage or catalog resolution.</summary>
public sealed record SqlServerParseResult(TSqlFragment Fragment, IReadOnlyList<ParseError> Diagnostics, bool UsedLegacyGrammar);

/// <summary>Parses SQL using the newest available ScriptDom grammar.</summary>
public static class SqlServerParser
{
    /// <summary>
    /// Legacy retry is opt-in: it is accepted only when the whole input parses without errors.
    /// A failed retry preserves the newest grammar's partial tree and diagnostics.
    /// Leave disabled for the existing linter behavior.
    /// </summary>
    public static SqlServerParseResult Parse(string sql, bool allowLegacySyntax = false)
    {
        ArgumentNullException.ThrowIfNull(sql);
        using StringReader reader = new(sql);
        TSqlFragment fragment = TSqlParserFactory.GetParser().Parse(reader, out IList<ParseError> errors);
        if (allowLegacySyntax && errors.Count > 0)
        {
            using StringReader legacyReader = new(sql);
            TSqlFragment legacy = new TSql80Parser(true).Parse(legacyReader, out IList<ParseError> legacyErrors);
            if (legacyErrors.Count == 0)
            {
                return new(legacy, Array.AsReadOnly(legacyErrors.ToArray()), true);
            }
        }
        return new(fragment, Array.AsReadOnly(errors.ToArray()), false);
    }
}
