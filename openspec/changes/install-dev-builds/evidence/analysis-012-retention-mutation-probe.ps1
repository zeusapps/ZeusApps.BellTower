#Requires -Version 7.4
<#
Analysis 012 read-only probe. Runs the existing InstallDevFunctionHarness restore scenarios against the reviewed
scripts/install-dev.ps1 and against a scratch copy whose record-retention test is reverted to the Implementation 011
form (property presence of `restartLauncher` instead of `$script:restartLaunched`). Neither the repository script nor
the harness is modified; the mutant and every harness root live under -Scratch. Answers: does the current suite
distinguish "this call reached Process.Start" from "a restartLauncher property exists"?
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
$needle = 'if ($script:restartLaunched) {'
if (([regex]::Matches($text, [regex]::Escape($needle))).Count -ne 1) { throw 'Mutation anchor not found exactly once.' }
$mutant = Join-Path $Scratch 'install-dev.mutant-011-retention.ps1'
[IO.File]::WriteAllText($mutant, $text.Replace($needle, 'if ($manifest.PSObject.Properties.Name -contains ''restartLauncher'') {'))

$scenarios = @('stopped-tray-restart-fails', 'stopped-tray-launched-unverified', 'stopped-tray-restarted', 'stopped-tray-still-running')
$results = [Collections.Generic.List[object]]::new()
foreach ($variant in @(@{ name = 'reviewed'; path = $installer }, @{ name = 'mutant-011-retention'; path = $mutant })) {
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
            $entry.recordsPresentAfter = @($r.records | Where-Object { Test-Path -LiteralPath $_ }).Count
        } else { $entry.allChecksPassed = $null; $entry.failedChecks = @('no result.json') }
        [IO.File]::WriteAllText((Join-Path $root 'exit.release'), 'x')
        if (!$proc.WaitForExit(15000)) { $proc.Kill() }
        $results.Add($entry)
    }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Out -Encoding utf8
$results | Format-Table variant, scenario, allChecksPassed, recordsPresentAfter, @{ n = 'failed'; e = { $_.failedChecks -join ',' } } -AutoSize
