param([string]$GrammarPath = (Join-Path $PSScriptRoot '../../sql-grammar-plsql'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$feed = Join-Path $root 'artifacts/feed'
$cache = Join-Path $root ('artifacts/packages-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $feed -Force | Out-Null
function Invoke-DotNet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet command failed ($LASTEXITCODE)." } }
Invoke-DotNet pack (Join-Path $GrammarPath 'src/SQLineage.Grammar.PlSql/SQLineage.Grammar.PlSql.csproj') -c Release -o $feed
& (Join-Path $GrammarPath 'scripts/Verify-PackageLicense.ps1') -FeedPath $feed
foreach ($engine in @('SqlServer','Oracle')) {
    Invoke-DotNet pack (Join-Path $root "src/SQLineage.Parsing.$engine/SQLineage.Parsing.$engine.csproj") -c Release -o $feed "-p:RestorePackagesPath=$cache"
}
& (Join-Path $PSScriptRoot 'Verify-PackageLicense.ps1') -FeedPath $feed
Invoke-DotNet test (Join-Path $root 'tests/Consumption.Tests/Consumption.Tests.csproj') -c Release "-p:RestorePackagesPath=$cache" --logger trx --results-directory (Join-Path $root 'artifacts/test-results')
