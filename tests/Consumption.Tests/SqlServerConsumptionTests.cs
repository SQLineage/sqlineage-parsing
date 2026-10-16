using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLineage.Parsing.SqlServer;

public sealed class SqlServerConsumptionTests
{
    [Fact]
    public void NewestGrammarHandlesModernSyntax()
    {
        var result = SqlServerParser.Parse("CREATE OR ALTER PROCEDURE dbo.p AS SELECT JSON_VALUE(payload, '$.id') FROM dbo.t;");
        Assert.Empty(result.Diagnostics);
        Assert.False(result.UsedLegacyGrammar);
        Assert.IsType<TSqlScript>(result.Fragment);
        Type newest = typeof(TSqlParser).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("TSql", StringComparison.Ordinal) && t.Name.EndsWith("Parser", StringComparison.Ordinal))
            .Select(t => (Type: t, Version: int.TryParse(t.Name.AsSpan(4, t.Name.Length - 10), out int n) ? n : 0))
            .MaxBy(t => t.Version).Type;
        Assert.Equal(newest, TSqlParserFactory.GetParser().GetType());
    }

    [Theory]
    [InlineData("RAISERROR @errno @errmsg")]
    [InlineData("RAISERROR 50001 'legacy error'")]
    public void LegacyRetryRequiresOptInAndWholeScriptSuccess(string statement)
    {
        string sql = $"CREATE PROCEDURE dbo.p AS BEGIN SELECT id FROM dbo.before_error; {statement}; SELECT id FROM dbo.after_error; END";
        Assert.NotEmpty(SqlServerParser.Parse(sql).Diagnostics);
        var legacy = SqlServerParser.Parse(sql, allowLegacySyntax: true);
        Assert.Empty(legacy.Diagnostics);
        Assert.True(legacy.UsedLegacyGrammar);
        Assert.Contains("after_error", legacy.Fragment.ScriptTokenStream.Select(t => t.Text));
    }

    [Fact]
    public void FailedLegacyRetryKeepsModernPartialAstAndPositions()
    {
        const string sql = "SELECT JSON_VALUE(payload, '$.id') FROM dbo.Modern;\nSELECT (";
        var newest = SqlServerParser.Parse(sql);
        var retry = SqlServerParser.Parse(sql, allowLegacySyntax: true);
        Assert.False(retry.UsedLegacyGrammar);
        Assert.NotEmpty(retry.Diagnostics);
        Assert.Equal(newest.Diagnostics.Select(e => (e.Number, e.Offset, e.Line, e.Column)),
            retry.Diagnostics.Select(e => (e.Number, e.Offset, e.Line, e.Column)));
        Assert.Contains(retry.Diagnostics, e => e.Line == 2);
        Assert.Contains("Modern", retry.Fragment.ScriptTokenStream.Select(t => t.Text));
    }

    [Fact]
    public void PackageHasNoProductDependency()
    {
        foreach (var assembly in new[] { typeof(SqlServerParser).Assembly, typeof(SQLineage.Parsing.Oracle.OracleSqlParser).Assembly })
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), name =>
                name.Name!.Contains("Core", StringComparison.Ordinal) && name.Name.StartsWith("SyncSql", StringComparison.Ordinal));
            Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.Contains("LineageAnalyzer", StringComparison.Ordinal));
        }
    }
}
