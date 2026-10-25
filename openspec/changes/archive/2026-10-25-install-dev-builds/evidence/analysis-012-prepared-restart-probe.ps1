#Requires -Version 7.4
<#
Analysis 012 read-only probe. Copies the test harness to -Scratch, appends one scenario that calls the reviewed
Restore-StoppedTrayAfterFailure with the manifest still in state 'prepared' (the state a Switch keeps when the failure is
the 'Verified tray did not exit' timeout and the tray then ends before Restore reads the owner), and reports whether the
terminal flag `trayRestartedAfterFailure` is saved while the state stays 'prepared'. The repository script and harness
are not modified; the scenario stubs Read-Owner (no owner) and Start-SelectedTray (verified restart) like the existing
stopped-tray-* scenarios.
#>
param(
    [Parameter(Mandatory)][string]$Scratch,
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$assemblies = Join-Path $repo 'tests/BellTower.Tests/bin/Release/net10.0-windows'
New-Item -ItemType Directory -Path $Scratch -Force | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $repo 'tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1'))
$scenario = @'
    '^a012-prepared-restart$' {
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'prepared'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) {
            return [ordered]@{ pid = 5151; ticks = 9; version = $Version; executable = 'C:\fixture\bell-tower.exe'; activation = $script:coordinationPlan.Id; dataDirectory = $DataDirectory }
        }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('Verified tray did not exit; no provider or runtime was signalled.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $saved = Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json
        $result.savedState = [string]$saved.state
        $result.savedTerminalFlag = ($saved.PSObject.Properties.Name -contains 'trayRestartedAfterFailure')
        $result.saysCannotRetry = ($errorText -like '*cannot be retried; run Preflight again*')
        $result.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
    }
    default { throw "Unknown scenario $Scenario." }
'@
$anchor = '    default { throw "Unknown scenario $Scenario." }'
if (([regex]::Matches($source, [regex]::Escape($anchor))).Count -ne 1) { throw 'Harness anchor not found exactly once.' }
$harness = Join-Path $Scratch 'InstallDevFunctionHarness.a012.ps1'
[IO.File]::WriteAllText($harness, $source.Replace($anchor, $scenario.TrimEnd()))
$root = Join-Path $Scratch ('prepared-restart-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$proc = Start-Process pwsh -ArgumentList @('-NoProfile', '-NonInteractive', '-File', $harness, '-Scenario', 'a012-prepared-restart', '-Root', $root, '-AssemblyDirectory', $assemblies, '-Installer', $installer) -PassThru -WindowStyle Hidden
$resultPath = Join-Path $root 'result.json'
$deadline = [DateTime]::UtcNow.AddSeconds(90)
while (!(Test-Path -LiteralPath $resultPath) -and !$proc.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
$r = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $root 'exit.release'), 'x')
if (!$proc.WaitForExit(15000)) { $proc.Kill() }
$summary = [ordered]@{ scenario = 'a012-prepared-restart'; savedState = $r.savedState; savedTerminalFlag = $r.savedTerminalFlag; saysCannotRetry = $r.saysCannotRetry; recordsRemoved = $r.recordsRemoved; error = $r.error }
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Out -Encoding utf8
$summary | ConvertTo-Json -Depth 4
