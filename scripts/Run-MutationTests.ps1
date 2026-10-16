[CmdletBinding()]
param(
    [ValidateSet('SqlServer', 'Oracle')]
    [string[]]$Project = @('SqlServer', 'Oracle'),
    [string]$ArtifactsPath = "$PSScriptRoot/../TestResults/mutation"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($ArtifactsPath)
$previousCoverageBuild = $env:CoverageBuild
$env:CoverageBuild = 'true'
Push-Location $root
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    Push-Location "$root/tests/Consumption.Tests"
    try {
        foreach ($engine in $Project) {
            # The original scheduled full-project run used break=0; retain that baseline.
            & dotnet stryker --config-file "$root/tests/stryker-config.json" --project "SQLineage.Parsing.$engine.csproj" --output "$output/$engine" --break-at 0 --skip-version-check
            if ($LASTEXITCODE -ne 0) { throw "Mutation run failed for $engine." }
        }
    }
    finally { Pop-Location }
}
finally {
    $env:CoverageBuild = $previousCoverageBuild
    Pop-Location
}
