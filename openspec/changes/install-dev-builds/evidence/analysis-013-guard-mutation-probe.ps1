#Requires -Version 7.4
<#
Analysis 013 read-only probe. Runs the InstallDevFunctionHarness restore scenarios against the reviewed
scripts/install-dev.ps1 and against two scratch mutants:
  - mutant-600-guard1: the Implementation 012 retry condition (state original-stopped only), which reopens ANALYSIS-600.
  - mutant-552-retention: record retention by `restartLauncher` property presence (the Implementation 011 form).
Neither the repository script nor the harness is modified; the mutants and every harness root live under -Scratch.
Guard 2 (the Rollback early refusal) is end-to-end only and is not mutated here.
#>
param(
    [Parameter(Mandatory)][string]$Scratch,
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$harness = Join-Path $repo 'tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1'
$assemblies = Join-Path $repo 'tests/BellTower.Tests/bin/Release/net10.0-windows'
New-Item -ItemType Directory -Path $Scratch -Force | Out-Null
$text = [IO.File]::ReadAllText($installer)

function New-Mutant([string]$Name, [string]$Needle, [string]$Replacement) {
    if (([regex]::Matches($text, [regex]::Escape($Needle))).Count -ne 1) { throw "Mutation anchor for $Name not found exactly once." }
    $path = Join-Path $Scratch ("install-dev.$Name.ps1")
    [IO.File]::WriteAllText($path, $text.Replace($Needle, $Replacement))
    return $path
}
$variants = @(
    @{ name = 'reviewed'; path = $installer },
    @{ name = 'mutant-600-guard1'; path = (New-Mutant 'mutant-600-guard1' "if (`$manifest.state -in @('prepared', 'original-stopped') -and `$untouched) {" "if (`$manifest.state -eq 'original-stopped' -and `$untouched) {") },
    @{ name = 'mutant-552-retention'; path = (New-Mutant 'mutant-552-retention' 'if ($script:restartLaunched) {' 'if ($manifest.PSObject.Properties.Name -contains ''restartLauncher'') {') }
)
$scenarios = @('stopped-tray-prepared-retryable', 'stopped-tray-leftover-launcher-property', 'stopped-tray-restarted', 'stopped-tray-restart-fails',
    'stopped-tray-launched-unverified', 'stopped-tray-partial-snapshot', 'stopped-tray-still-running')
$results = [Collections.Generic.List[object]]::new()
foreach ($variant in $variants) {
    foreach ($scenario in $scenarios) {
        $root = Join-Path $Scratch ($variant.name + '-' + $scenario + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $proc = Start-Process pwsh -ArgumentList @('-NoProfile', '-NonInteractive', '-File', $harness, '-Scenario', $scenario, '-Root', $root, '-AssemblyDirectory', $assemblies, '-Installer', $variant.path) -PassThru -WindowStyle Hidden
        $resultPath = Join-Path $root 'result.json'
        $deadline = [DateTime]::UtcNow.AddSeconds(90)
        while (!(Test-Path -LiteralPath $resultPath) -and !$proc.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
        $entry = [ordered]@{ variant = $variant.name; scenario = $scenario }
        if (Test-Path -LiteralPath $resultPath) {
            $r = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            $failed = @($r.checks.PSObject.Properties | Where-Object { $_.Value -ne $true } | ForEach-Object { $_.Name })
            $entry.allChecksPassed = ($failed.Count -eq 0)
            $entry.failedChecks = $failed
        } else { $entry.allChecksPassed = $null; $entry.failedChecks = @('no result.json') }
        [IO.File]::WriteAllText((Join-Path $root 'exit.release'), 'x')
        if (!$proc.WaitForExit(15000)) { $proc.Kill() }
        $results.Add($entry)
    }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Out -Encoding utf8
$results | Format-Table variant, scenario, allChecksPassed, @{ n = 'failed'; e = { $_.failedChecks -join ',' } } -AutoSize
