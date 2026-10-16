# CI migration and SonarQube readiness

Source inventory: `cangelosilima/SyncSQL` at `bbfb21916c8c9078ef7c4b947f07bd154b112249`. No source repository files or settings were changed. All checks build local artifacts; release, deployment, registry publication, badge-branch writes and automatic PR comments are absent.

| Original check | Destination and retained behavior |
| --- | --- |
| CLI build/test and local tool consumption | Private product: Linux, Windows and macOS; exact package installation into an isolated tool directory. Parsers: independent NuGet consumers. Grammar: source hashes, pack/license verification and .NET 8 consumer. |
| NuGet vulnerabilities | Every destination: direct and transitive audit at `low`; NU1900–NU1905 fail, including unavailable audit data. PR dependency review retains `low`. |
| Recommended .NET analyzers and formatting | Product and handwritten parser code; original source formatting rules. Vendor-generated grammar remains byte-identical and excluded from style/mutation. |
| CLI coverage | Product and extracted parser code: 100% lines, branches, methods and fully covered methods; raw counts must agree. Missing reports or unavailable method metrics fail. ReportGenerator stays 5.5.10. |
| PR mutation | Product Core 90%; selected CLI command/progress files 55%. Original mutation scope and timeout retained. |
| Full mutation | Manual/weekly product seven-project matrix; parser SqlServer/Oracle matrix. Original full-run break=0 is informational, not a new 90% claim; initial test failure fails. Stryker stays 5.0.0. |
| Performance | Product: original eight workload and two retention budgets, unchanged; regression/failure handling self-test retained. |
| PowerShell | Parse, real Windows PowerShell 5.1 parse, and PSScriptAnalyzer 5.1/7.4 compatible syntax checks. No database publication is executed. |
| Packaging guards | Gateway visibility guards, release/tag idempotency and authorization/error tests run with mocks; Windows manifest/hash fixtures; local Windows self-contained build/archive/Chocolatey pack. |
| Live heterogeneous lineage | Private product hosted Linux with SQL Server + Oracle containers; runtime-generated disposable password, extraction and assertion reports, cleanup. Local Docker daemon is unavailable on Desktop-Victor. |
| CodeQL | Retained as a separate scan; SARIF artifacts, no upload or new write scopes. Private repository preflight requires an existing visible Code Security entitlement; unavailable entitlement is a failure, not a skipped success. Sonar is not asserted to be equivalent to CodeQL. |
| Workflow lint and aggregate | Original actionlint 1.7.7 pinned Docker digest; strict all-green aggregates reject failed/skipped dependencies. Sonar and security have their own failing statuses and must also be required during future branch-protection setup. No branch-protection setting was changed. |

## SonarQube: prepared, not configured or validated

The user confirmed no SonarQube account/instance exists. No code was submitted to SonarSource or another Sonar server; no account, project, token, secret or access grant was created. `SonarQube quality gate` deliberately fails with an explicit configuration error until an approved `.sonar/target.json` exists. The example has `approved=false` and blank service, URL, keys, gate and scanner version. CI references a future `SONAR_TOKEN` secret without creating or reading its value.

The prepared .NET scanner integration checks that the project already exists and that its assigned gate matches the approved gate, uses an explicitly selected compatible scanner version, builds and executes tests between begin/end, imports OpenCover and TRX, then waits up to 600 seconds for processing and the gate. Failure or timeout fails CI. It does not equate successful submission with a passing gate. The scanner changes compiler analysis settings, so strict audit/build/style/tests remain in independent jobs. Generated grammar and checked-out dependency artifacts are excluded from analysis; the grammar repository has no handwritten production C# and its meaningful Sonar language/scope must be agreed before enabling analysis.

Sonar consumes coverage reports; it does not run the tests or replace Stryker, benchmark budgets, package/install checks, grammar byte/license checks, actionlint or live database tests. Existing 100% line/branch/method/full-method gates remain independent. No default Sonar gate is claimed to preserve those thresholds. Sonar conditions, quality profiles and new-code definition have not been selected or changed.

Required setup decisions: Server or Cloud; approved HTTPS destination and whether private source/results may be transmitted there; existing project keys and Cloud organization if applicable; edition/plan and supported PR/branch analysis; approved existing quality gate/profile/conditions and new-code definition; compatible exact scanner version and runner connectivity; a separately authorized mechanism for a project-scoped analysis credential. Do not put token values in configuration or source control. Scanner 12.0 was released 2026-10-15 and requires Server >=2025.1 LTA or Community Build >=25.1, so compatibility must be selected after the service is known.

Cloud Free supports at most 50k private LOC / five members and main plus PRs targeting main, but does not support custom quality gates/profiles. Team supports custom gates; OSS is public-only. Server branch/PR integration begins with Developer Edition. Free is therefore not assumed to centralize this repository's custom thresholds. The private production C# nonblank-line estimate is 10,516 across 103 tracked production C# files (11,848 physical lines; comments included, tests/benchmarks/build outputs/public parsers/vendor grammar excluded); this is not Sonar's billable LOC and does not establish eligibility for the entire analysis scope.

Official references checked 2026-10-16:

- [Scanner begin/build/end and changed compiler settings](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/scanners/dotnet/using)
- [Supported .NET coverage imports](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/test-coverage/dotnet-test-coverage)
- [Gate wait, timeout, authentication and destination parameters](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-parameters/parameters-not-settable-in-ui)
- [Scanner versions and compatibility](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/scanners/dotnet/installing)
- [Server GitHub PR/branch edition support](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/ci-integration/github-actions)
- [Cloud subscription plans](https://docs.sonarsource.com/sonarqube-cloud/administering-sonarcloud/managing-subscription/subscription-plans)

## Explicitly unavailable or outside migrated scope

- `REPORTGENERATOR_LICENSE` is absent from the private repository's secret-name inventory. CI will report actual method-metric availability; the raw-count gate fails if metrics are missing. No license was copied or configured. Local coverage results do not prove GitHub runner licensing.
- Private GitHub Code Security/dependency-review availability is unconfirmed (`security_and_analysis` returned null). No subscription, setting or token scope was enabled.
- Optional proprietary `dg4msql` execution requires an already provisioned oracle-gateway runner and licensed gateway image/media plus registry access. That infrastructure was not supplied or provisioned. The ordinary live-container lineage job does not validate dg4msql execution.
- Exact public NuGet/Chocolatey/WinGet catalog installation cannot be validated before separately authorized publication. Local package/install and mocked retry tests do not claim public catalog readiness. Windows manifest identifiers are local packaging fixtures and have not been reserved or submitted.
- Site npm audit, frontend lint/typecheck/80% coverage and 100% reachability mutation belong to the original frontend, which has not been migrated into these three repositories. They are unported scope, not passing checks.
- Scheduled/manual full mutation cannot be triggered through a new workflow until it is available on the default branch; no merge was performed. Its configuration is reviewable, and no completed full-run score is claimed.

CI run results and actual measured counts are recorded in the task migration map / PR evidence. Missing setup and failing baseline measurements are blockers, never waivers.
