# Provenance and pending license decision

Public source migration under review. No redistribution license is selected or granted for these facades.
Original source: https://github.com/cangelosilima/SyncSQL at
bbfb21916c8c9078ef7c4b947f07bd154b112249. No top-level source license was detected.

- TSqlParserFactory.cs is adapted from cli/src/SyncSql.Lineage.MsSql/TSqlParserFactory.cs;
  namespace and product-specific documentation references changed.
- SQL Server legacy fallback derives from MsSqlLineageAnalyzer.cs in the same directory.
- Oracle exclusive cache leasing, limits and simulator setup derive from
  cli/src/SyncSql.Lineage.Oracle/OracleLineageAnalyzer.cs. Session API, structured diagnostics
  and parser callbacks are local preparation changes. No lineage visitor, resolver or Core code is included.
- No original copyright header was removed. The grammar remains separately attributed with its
  original LICENSE and NOTICE; its license does not establish licensing for these facades.

Package identity/version and this API remain reversible local staging choices.
