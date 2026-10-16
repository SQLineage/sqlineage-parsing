# Provenance and licensing

Public source migration under review. The repository owner selected Apache License 2.0 for this
public repository. See LICENSE and NOTICE; no new copyright owner identity is asserted.
Original source: https://github.com/cangelosilima/SyncSQL at
bbfb21916c8c9078ef7c4b947f07bd154b112249. That inspected snapshot had no top-level license;
the owner's subsequent Apache-2.0 choice applies to this public repository.

- TSqlParserFactory.cs is adapted from cli/src/SyncSql.Lineage.MsSql/TSqlParserFactory.cs;
  namespace and product-specific documentation references changed.
- SQL Server legacy fallback derives from MsSqlLineageAnalyzer.cs in the same directory.
- Oracle exclusive cache leasing, limits and simulator setup derive from
  cli/src/SyncSql.Lineage.Oracle/OracleLineageAnalyzer.cs. Session API, structured diagnostics
  and parser callbacks are local preparation changes. No lineage visitor, resolver or Core code is included.
- No original copyright header was removed. The grammar remains separately attributed with its
  original LICENSE and NOTICE; third-party licenses and copyright notices remain unchanged.

Package identity/version and this API remain reversible local staging choices.
