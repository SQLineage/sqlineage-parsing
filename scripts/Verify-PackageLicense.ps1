param([string]$FeedPath = (Join-Path $PSScriptRoot '../artifacts/feed'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Split-Path -Parent $PSScriptRoot
$packages = @(Get-ChildItem -LiteralPath $FeedPath -Filter 'SQLineage.Parsing.*.nupkg')
if ($packages.Count -ne 2) { throw 'Expected SQL Server and Oracle parser packages.' }
foreach ($package in $packages) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $specEntry = @($zip.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($specEntry.Count -ne 1) { throw 'Expected exactly one package manifest.' }
        $reader = [System.IO.StreamReader]::new($specEntry[0].Open())
        try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($spec.package.metadata.license.type -ne 'expression' -or
            $spec.package.metadata.license.InnerText -ne 'Apache-2.0') { throw 'Package license must be Apache-2.0.' }
        foreach ($name in @('LICENSE','NOTICE')) {
            $entry = $zip.GetEntry($name)
            if ($null -eq $entry) { throw "Missing required attribution file: $name" }
            $stream = $entry.Open()
            try { $actualHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash } finally { $stream.Dispose() }
            $expectedHash = (Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash
            if ($actualHash -ne $expectedHash) { throw "Package attribution changed: $name" }
        }
        Write-Output "Verified Apache-2.0 metadata and LICENSE/NOTICE bytes: $($package.Name)"
    }
    finally { $zip.Dispose() }
}
