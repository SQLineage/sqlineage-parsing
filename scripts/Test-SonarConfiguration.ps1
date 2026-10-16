$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Run-SonarAnalysis.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('sqlineage-sonar-guard-' + [Guid]::NewGuid().ToString('N') + '.json')
$previousEvent = $env:GITHUB_EVENT_NAME
$checks = 0
function Assert-Rejected([hashtable]$Target, [string]$Expected) {
    $Target | ConvertTo-Json | Set-Content -LiteralPath $fixture
    try { & $runner -ConfigurationPath $fixture -ValidateConfigurationOnly }
    catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        $script:checks++
        return
    }
    throw "Unsafe Sonar configuration was accepted: $Expected"
}
try {
    try { & $runner -ConfigurationPath $fixture -ValidateConfigurationOnly; throw 'Missing configuration was accepted.' }
    catch { if ($_.Exception.Message -notlike '*unconfigured*') { throw }; $checks++ }
    Assert-Rejected @{ approved = $false } 'not been approved'
    Assert-Rejected @{ approved = $true; service = 'unknown' } 'approved SonarQube service'
    Assert-Rejected @{ approved = $true; service = 'server'; hostUrl = 'http://example.invalid' } 'approved HTTPS'
    Assert-Rejected @{ approved = $true; service = 'server'; hostUrl = 'https://user@example.invalid' } 'approved HTTPS'
    Assert-Rejected @{ approved = $true; service = 'server'; hostUrl = 'https://example.invalid' } 'projectKey'
    $valid = @{ approved = $true; service = 'server'; hostUrl = 'https://example.invalid'; projectKey = 'fixture'; qualityGateName = 'fixture'; scannerVersion = '12.0.0'; solution = 'fixture.slnx'; pullRequestAnalysisSupported = $false }
    $env:GITHUB_EVENT_NAME = 'pull_request'
    Assert-Rejected $valid 'not been confirmed to support'
    $valid.pullRequestAnalysisSupported = $true
    $valid | ConvertTo-Json | Set-Content -LiteralPath $fixture
    & $runner -ConfigurationPath $fixture -ValidateConfigurationOnly
    $checks++
    Write-Host "Sonar configuration guards passed: $checks offline cases; no network or scanner invocation."
}
finally {
    $env:GITHUB_EVENT_NAME = $previousEvent
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
}
