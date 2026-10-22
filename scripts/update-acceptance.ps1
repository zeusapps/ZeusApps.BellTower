param([string]$BaselineArchive)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $version = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if (!$BaselineArchive) {
        # Initial bootstrap baseline: distinct compiled version from this source, not a claim about the legacy 0.1.7 release.
        ./scripts/package.ps1 -FixtureVersion 0.1.7 -OutputDirectory artifacts/update-acceptance/packages
        $BaselineArchive = 'artifacts/update-acceptance/packages/bell-tower-0.1.7-win-x64.zip'
        Write-Output 'Baseline provenance: current source compiled with fixture version 0.1.7; first versioned installation protocol.'
    }
    ./scripts/package.ps1
    dotnet run --project tools/BellTower.Smoke -c Release --no-build -- $BaselineArchive "artifacts/releases/bell-tower-$version-win-x64.zip"
    if ($LASTEXITCODE -ne 0) { throw 'Packaged updater acceptance failed.' }
} finally { Pop-Location }
