#Requires -Version 7.4
<#
Implementation 012 native fault matrix for LIVE-002 and ANALYSIS-552/553 (post-launch restart failure, then same-manifest Rollback) (refusal after the tray stop, automatic restart, recovery text). Synthetic installation and data under
artifacts/implementation-012/ (stable launcher + payload extracted from the original package zip; own tray).
A churn process spawns `<disposable install>\bell-tower.exe statusline --provider claude --data-dir <disposable data>` about
every 200 ms during Switch and Rollback. The installer runs with redirected stdout/stderr pipes that are read to the end,
so a tray that inherited them would hang the read (LIVE-003). Only processes this script started are ever stopped.
#>
param(
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string[]]$Candidates = @('0.1.9001', '0.1.9002', '0.1.9004', '0.1.9005'),
    [string]$Original = '0.1.9003',
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot 'implementation-012-native-faults-results.json' }
$dev = Join-Path $Repo 'artifacts/dev-builds'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $Repo 'scripts/install-dev.ps1'
$root = Join-Path $Repo ('artifacts/implementation-012/native-' + [Guid]::NewGuid().ToString('N').Substring(0, 10))
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $workspace = Join-Path $root 'private'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
$barrier = Join-Path $root 'barriers'
New-Item -ItemType Directory -Path $root, $install, $data, $barrier | Out-Null
foreach ($phase in @('switch-before-selection', 'rollback-before-selection')) { New-Item -ItemType File -Path (Join-Path $barrier ($phase + '.release')) | Out-Null }
Expand-Archive -LiteralPath (Join-Path $dev "bell-tower-$Original-win-x64.zip") -DestinationPath $install
[IO.File]::WriteAllText((Join-Path $data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')
$paths = [BellTower.IO.DataPaths]::new($data)
$controller = [BellTower.Tray.TrayController]::new($paths)
$presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)

function Get-Presence {
    if (!(Test-Path -LiteralPath $presence)) { return $null }
    try { $p = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence, 4096) } catch { return $null }
    if ([BellTower.IO.ProcessProbe]::Inspect($p.Process) -ne [BellTower.IO.ProcessLiveness]::Live) { return $null }
    return $p
}
function Wait-Presence([string]$Version) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) { $p = Get-Presence; if ($p -and $p.Version -eq $Version) { return $p }; Start-Sleep -Milliseconds 100 }
    throw "No live $Version tray."
}
# Runs the installer with captured pipes; returns when the pipes close (a leaked tray handle would time out here).
function Invoke-Captured([string[]]$Arguments) {
    $s = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $s.UseShellExecute = $false; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true; $s.CreateNoWindow = $true
    foreach ($a in @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer) + $Arguments) { $s.ArgumentList.Add($a) }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $p = [Diagnostics.Process]::Start($s)
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    $closed = [Threading.Tasks.Task]::WaitAll(@($out, $err), 180000)
    if (!$closed) { try { $p.Kill($true) } catch { } }
    else { [void]$p.WaitForExit(30000) }
    return [pscustomobject]@{ closed = $closed; seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 1); exit = $(if ($p.HasExited) { $p.ExitCode } else { $null }); output = $(if ($closed) { $out.Result + $err.Result } else { '' }) }
}
$common = @('-InstallationRoot', $install, '-DataDirectory', $data, '-Workspace', $workspace, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild', '-TestBarrierDirectory', $barrier)
function Set-Fault([string]$Name, [bool]$On) { $f = Join-Path $barrier $Name; if ($On) { New-Item -ItemType File -Path $f -Force | Out-Null } elseif (Test-Path $f) { Remove-Item $f } }
function Get-State([string]$Manifest) { (Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).state }
function Run([string]$Action, [string]$Candidate, [string]$Manifest) {
    $a = @('-Action', $Action, '-Package', (Join-Path $dev "bell-tower-$Candidate-win-x64.zip")) + $common
    if ($Manifest) { $a += @('-OperationManifest', $Manifest) }
    Invoke-Captured $a
}
function Preflight([string]$Candidate) { $r = Run 'Preflight' $Candidate $null; if ($r.exit -ne 0) { throw $r.output }; ($r.output | ConvertFrom-Json).operationManifest }
$results = [ordered]@{}
$initial = [Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe')); $initial.UseShellExecute = $false; $initial.CreateNoWindow = $true
foreach ($a in @('tray', '--data-dir', $data, '--silent')) { $initial.ArgumentList.Add($a) }
([Diagnostics.Process]::Start($initial)).Dispose()
try {
    [void](Wait-Presence $Original)
    # A: refusal after the Switch tray stop -> original tray restarted, state retryable, same-manifest Switch works.
    $m = Preflight $Candidates[0]
    $pid0 = (Get-Presence).Process.Pid
    Set-Fault 'fail-after-switch-stop' $true
    $r = Run 'Switch' $Candidates[0] $m
    Set-Fault 'fail-after-switch-stop' $false
    $p = Get-Presence
    $retry = Run 'Switch' $Candidates[0] $m
    $results.A = [ordered]@{ exit = $r.exit; message = ($r.output -replace '\s+', ' ').Trim(); trayRunningAfterRefusal = [bool]$p; trayVersion = $p.Version; newTray = ($p.Process.Pid -ne $pid0); recordsLeft = @(Get-ChildItem (Join-Path $install 'updates') -Filter pending.json -ErrorAction SilentlyContinue).Count; retryExit = $retry.exit; retryState = (Get-State $m); retryTray = (Get-Presence).Version }
    $rb = Run 'Rollback' $Candidates[0] $m
    $results.A.rollbackExit = $rb.exit; $results.A.rollbackTray = (Get-Presence).Version
    # B: refusal after the stop AND the automatic restart fails -> message says how to recover; Rollback recovers.
    $m = Preflight $Candidates[1]
    Set-Fault 'fail-after-switch-stop' $true; Set-Fault ('fail-start-' + $Original) $true
    $r = Run 'Switch' $Candidates[1] $m
    Set-Fault 'fail-after-switch-stop' $false; Set-Fault ('fail-start-' + $Original) $false
    $stopped = -not (Get-Presence)
    $retry = Run 'Switch' $Candidates[1] $m
    $rb = Run 'Rollback' $Candidates[1] $m
    $results.B = [ordered]@{ exit = $r.exit; message = ($r.output -replace '\s+', ' ').Trim(); trayStopped = $stopped; manifestState = 'see retryMessage'; retryExit = $retry.exit; retryMessage = ($retry.output -replace '\s+', ' ').Trim(); rollbackExit = $rb.exit; rollbackStatus = $(try { ($rb.output | ConvertFrom-Json).status } catch { $rb.output }); rollbackTray = (Get-Presence).Version }
    # C: Rollback refuses after the candidate tray stop -> candidate restarted; same-manifest Rollback then finishes.
    $m = Preflight $Candidates[2]
    $sw = Run 'Switch' $Candidates[2] $m
    Set-Fault 'fail-after-rollback-stop' $true
    $r = Run 'Rollback' $Candidates[2] $m
    Set-Fault 'fail-after-rollback-stop' $false
    $p = Get-Presence
    $results.C = [ordered]@{ switchExit = $sw.exit; exit = $r.exit; message = ($r.output -replace '\s+', ' ').Trim(); trayAfterRefusal = $p.Version; selection = (Get-Content (Join-Path $install 'current.json') -Raw | ConvertFrom-Json).Version }
    $rb = Run 'Rollback' $Candidates[2] $m
    $results.C.retryRollbackExit = $rb.exit; $results.C.retryRollbackTray = (Get-Presence).Version
    # D: restart launched but refused after launch (no candidate payload yet) -> records kept and "may be running unverified";
    # the unverified disposable tray is ended by this script, then a same-manifest Rollback must finish with no records.
    $m = Preflight $Candidates[3]
    Set-Fault 'fail-after-switch-stop' $true; Set-Fault ('fail-after-launch-' + $Original) $true
    $r = Run 'Switch' $Candidates[3] $m
    Set-Fault 'fail-after-switch-stop' $false; Set-Fault ('fail-after-launch-' + $Original) $false
    # The launched tray keeps starting after the installer exits (that is why the message says it may be running).
    $wait = [DateTime]::UtcNow.AddSeconds(30); do { $left = Get-Presence; if (!$left) { Start-Sleep -Milliseconds 200 } } while (!$left -and [DateTime]::UtcNow -lt $wait)
    $recordsKept = (Test-Path (Join-Path $install 'updates/pending.json'))
    $payloadAbsent = !(Test-Path (Join-Path $install ('versions/' + $Candidates[3])))
    if ($left) { Stop-Process -Id $left.Process.Pid -Force; $end = [DateTime]::UtcNow.AddSeconds(20); while ((Get-Presence) -and [DateTime]::UtcNow -lt $end) { Start-Sleep -Milliseconds 100 } }
    $rb = Run 'Rollback' $Candidates[3] $m
    $results.D = [ordered]@{ exit = $r.exit; message = ($r.output -replace '\s+', ' ').Trim(); trayLaunchedUnverified = [bool]$left; recordsKept = $recordsKept; candidatePayloadAbsent = $payloadAbsent; rollbackExit = $rb.exit; rollbackOutput = ($rb.output -replace '\s+', ' ').Trim(); recordsAfterRollback = @(Get-ChildItem (Join-Path $install 'updates') -Filter pending.json -ErrorAction SilentlyContinue).Count; rollbackTray = (Get-Presence).Version }
} finally { try { $controller.RequestExit() } catch { } }
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultFile
$results | ConvertTo-Json -Depth 6
