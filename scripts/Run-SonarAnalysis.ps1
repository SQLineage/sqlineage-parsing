[CmdletBinding()]
param(
    [string]$ConfigurationPath = "$PSScriptRoot/../.sonar/target.json",
    [switch]$ValidateConfigurationOnly
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ConfigurationPath)) {
    throw 'SonarQube is unconfigured: an explicitly approved .sonar/target.json is required. No analysis was submitted.'
}
$target = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
if ($target.approved -ne $true) { throw 'SonarQube destination has not been approved. No analysis was submitted.' }
if ($target.service -notin @('server', 'cloud')) { throw 'Select the approved SonarQube service: server or cloud.' }
$hostUri = $null
if (-not [Uri]::TryCreate($target.hostUrl, [UriKind]::Absolute, [ref]$hostUri) -or
    $hostUri.Scheme -ne 'https' -or $hostUri.UserInfo -or $hostUri.Query -or $hostUri.Fragment) {
    throw 'An approved HTTPS SonarQube URL without embedded credentials is required.'
}
foreach ($name in @('projectKey', 'qualityGateName', 'scannerVersion', 'solution')) {
    if ([string]::IsNullOrWhiteSpace($target.$name)) { throw "Missing approved SonarQube setting: $name." }
}
if ($target.scannerVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Pin an exact compatible scanner version.' }
if ($target.service -eq 'cloud' -and [string]::IsNullOrWhiteSpace($target.organization)) { throw 'SonarQube Cloud organization is required.' }
if ($env:GITHUB_EVENT_NAME -eq 'pull_request' -and $target.pullRequestAnalysisSupported -ne $true) {
    throw 'The selected SonarQube edition/plan has not been confirmed to support this pull request analysis.'
}
if ($ValidateConfigurationOnly) { Write-Host 'SonarQube destination configuration validated offline; no analysis submitted.'; return }
if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) { throw 'SONAR_TOKEN is unavailable. No analysis was submitted.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = [IO.Path]::GetFullPath((Join-Path $root $target.solution))
if (-not $solution.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $solution)) { throw 'The approved analysis solution must exist inside this repository.' }
# Read existing project and gate; never let scanner submission provision a project implicitly.
$headers = @{ Authorization = 'Bearer ' + $env:SONAR_TOKEN }
$base = $target.hostUrl.TrimEnd('/')
$key = [Uri]::EscapeDataString($target.projectKey)
$organization = if ($target.service -eq 'cloud') { '&organization=' + [Uri]::EscapeDataString($target.organization) } else { '' }
$null = Invoke-RestMethod -Uri "$base/api/components/show?component=$key$organization" -Headers $headers
$gate = Invoke-RestMethod -Uri "$base/api/qualitygates/get_by_project?project=$key$organization" -Headers $headers
if ($gate.qualityGate.name -cne $target.qualityGateName) { throw 'The existing project quality gate differs from the approved gate.' }
$tools = Join-Path $root 'artifacts/sonar-tools'
function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Sonar build or test command failed ($LASTEXITCODE)." }
}
Push-Location $root
try {
    Invoke-DotNet tool install dotnet-sonarscanner --version $target.scannerVersion --tool-path $tools
    $scanner = Join-Path $tools $(if ($IsWindows) { 'dotnet-sonarscanner.exe' } else { 'dotnet-sonarscanner' })
    $begin = @('begin', "/k:$($target.projectKey)", "/d:sonar.host.url=$base",
        "/d:sonar.token=$env:SONAR_TOKEN", '/d:sonar.qualitygate.wait=true', '/d:sonar.qualitygate.timeout=600',
        '/d:sonar.cs.opencover.reportsPaths=TestResults/sonar/**/coverage.opencover.xml',
        '/d:sonar.cs.vstest.reportsPaths=TestResults/sonar/**/*.trx',
        '/d:sonar.exclusions=artifacts/**,**/bin/**,**/obj/**,**/Generated/**')
    if ($target.service -eq 'cloud') { $begin += "/o:$($target.organization)" }
    # .NET scanner does not read SONAR_TOKEN directly; never print arguments or enable verbose logging.
    & $scanner @begin
    if ($LASTEXITCODE -ne 0) { throw 'SonarScanner begin failed.' }
    $properties = @()
    if ($target.sourceCoverageBuild -eq $true) { $properties += '-p:CoverageBuild=true' }
    Invoke-DotNet build $solution -c Release --no-incremental --disable-build-servers @properties
    if ($target.coverageSettings) {
        Invoke-DotNet test $solution -c Release --no-build @properties --collect 'XPlat Code Coverage' --settings $target.coverageSettings --results-directory TestResults/sonar --logger trx
        if (@(Get-ChildItem TestResults/sonar -Recurse -Filter coverage.opencover.xml).Count -eq 0) { throw 'No OpenCover coverage report was produced.' }
    }
    & $scanner end "/d:sonar.token=$env:SONAR_TOKEN"
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube analysis or quality gate failed, timed out, or could not complete.' }
}
finally { Pop-Location }
