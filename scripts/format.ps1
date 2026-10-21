param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $formatArguments = @('format', 'ZeusApps.BellTower.slnx', '--no-restore', '--verbosity', 'minimal')
    if ($Verify) {
        $formatArguments += '--verify-no-changes'
    }
    dotnet @formatArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Formatting check failed. Run scripts/format.ps1 to apply the configured style.'
    }
} finally {
    Pop-Location
}
