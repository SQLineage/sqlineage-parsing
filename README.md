# SQLineage parsing

Independently consumable .NET 10 parsing facades: `SQLineage.Parsing.SqlServer` over ScriptDom
180.102.0 and `SQLineage.Parsing.Oracle` over ANTLR 4.13.1 and `SQLineage.Grammar.PlSql`.
Product lineage visitors, Core, extraction and metadata resolution remain in the private product.

This migration is under review. Version `0.0.0-local.1` is built into a local feed; no package,
release or automatic publication is configured. This repository is licensed under
[Apache License 2.0](LICENSE). See [NOTICE](NOTICE) and [provenance](PROVENANCE.md) for attribution.
Dependencies retain their own licenses. Packages include LICENSE/NOTICE and declare `Apache-2.0`.

Clone [the grammar repository](https://github.com/SQLineage/sql-grammar-plsql) alongside this one
and check out its migration PR branch while review is pending. Validate with:

```powershell
./scripts/Validate.ps1 -GrammarPath ../sql-grammar-plsql
```

This builds local packages and tests NuGet consumers using a fresh cache with audit enabled.
CI pins the exact grammar commit and requires no private-repository credentials.

## SQL Server

`SqlServerParser.Parse(sql)` returns the newest ScriptDom AST and diagnostics.
Set `allowLegacySyntax: true` to accept TSql80 only if the whole input parses cleanly; a failed retry
preserves the newest partial AST and diagnostics. Leave false for existing lint semantics.
This retry supports older SQL Server syntax; it is SQL language support, not a SyncSQL API alias.
`TSqlParserFactory` selects the highest available version and supports explicit parser creation.

## Oracle

`new OracleSqlParser().Parse(sql, result => result.Diagnostics.Count)` consumes a parse synchronously.
Use `WithSession(session => ...)` for nested parses sharing one exclusive cache lease.
Sessions expire on callback return; concurrent leases own independent caches. Callers own syntax
objects they deliberately retain. Only bounded DFA/context caches are retained by the facade,
never trees, tokens, interpreters or inputs. Limits remain 16 scripts, 1 MiB input, 2048 states,
250000 configurations and 16384 contexts. They bound retention, not work within one complex parse.
ANTLR LL prediction and normal recovery are preserved. Diagnostics have one-based positions,
parser offsets and lexer/parser origin. Session.Parse accepts an optional entry delegate for
grammar configuration/inspection. Callbacks must stay synchronous; product recursion budgets
remain the caller's responsibility.
