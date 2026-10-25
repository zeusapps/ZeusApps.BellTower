#Requires -Version 7.4
<#
QA 011 independent native driver (installed native execution on disposable synthetic installations/data under
artifacts/qa-011/; stable launcher + payload extracted from package zips; own disposable trays only).
Cases: churn (Switch->Rollback cycles with a statusline churn), faults (LIVE-002), negative (LIVE-001 held unclassifiable
process), foreign (foreign tray start during a held Switch). Every installer run goes through captured pipes read to the
end with a hard timeout (LIVE-003). Only processes this script started are ever stopped.
#>
param(
    [Parameter(Mandatory)][ValidateSet('churn', 'faults', 'negative', 'foreign')][string]$Case,
    [ValidateSet('claude', 'codex')][string]$Provider = 'claude',
    [string[]]$Candidates = @('0.1.9001', '0.1.9002', '0.1.9003', '0.1.9004', '0.1.9005', '0.1.9006', '0.1.9007', '0.1.9001'),
    [string]$Original = '0.1.9008',
    [int]$ChurnMs = 200,
    [int]$RunTimeoutSeconds = 150,
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot "qa-011-native-$Case-$Provider-results.json" }
$dev = Join-Path $repo 'artifacts/dev-builds'
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$root = Join-Path $repo ("artifacts/qa-011/$Case-$Provider-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $workspace = Join-Path $root 'private'; $barrier = Join-Path $root 'barriers'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
New-Item -ItemType Directory -Path $root, $install, $data, $barrier | Out-Null
# The disposable barriers wait unconditionally when a barrier directory is given; pre-release them (foreign case holds Switch).
foreach ($phase in @('switch-before-selection', 'rollback-before-selection')) { if ($Case -eq 'foreign' -and $phase -eq 'switch-before-selection') { continue }; New-Item -ItemType File -Path (Join-Path $barrier ($phase + '.release')) | Out-Null }
Expand-Archive -LiteralPath (Join-Path $dev "bell-tower-$Original-win-x64.zip") -DestinationPath $install
[IO.File]::WriteAllText((Join-Path $data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')
$paths = [BellTower.IO.DataPaths]::new($data)
$controller = [BellTower.Tray.TrayController]::new($paths)
$presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)
$launcher = Join-Path $install 'bell-tower.exe'
$ownedPids = [Collections.Generic.List[int]]::new()

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
function Invoke-Captured([string[]]$Arguments) {
    $s = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $s.UseShellExecute = $false; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true; $s.CreateNoWindow = $true
    foreach ($a in @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer) + $Arguments) { $s.ArgumentList.Add($a) }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $p = [Diagnostics.Process]::Start($s); $ownedPids.Add($p.Id)
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    $closed = [Threading.Tasks.Task]::WaitAll(@($out, $err), $RunTimeoutSeconds * 1000)
    if (!$closed) { try { $p.Kill($true) } catch { } } else { [void]$p.WaitForExit(30000) }
    [pscustomobject]@{ closed = $closed; seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 1); exit = $(if ($p.HasExited) { $p.ExitCode } else { $null }); output = $(if ($closed) { $out.Result + $err.Result } else { '' }) }
}
$common = @('-InstallationRoot', $install, '-DataDirectory', $data, '-Workspace', $workspace, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild', '-TestBarrierDirectory', $barrier)
function Run([string]$Action, [string]$Candidate, [string]$Manifest) {
    $a = @('-Action', $Action, '-Package', (Join-Path $dev "bell-tower-$Candidate-win-x64.zip")) + $common
    if ($Manifest) { $a += @('-OperationManifest', $Manifest) }
    Invoke-Captured $a
}
function Preflight([string]$Candidate) { $r = Run 'Preflight' $Candidate $null; if ($r.exit -ne 0 -or !$r.closed) { throw "Preflight failed: $($r.output)" }; ($r.output | ConvertFrom-Json).operationManifest }
function Get-State([string]$Manifest) { (Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).state }
function Set-Flag([string]$Name, [bool]$On) { $f = Join-Path $barrier $Name; if ($On) { New-Item -ItemType File -Path $f -Force | Out-Null } elseif (Test-Path $f) { Remove-Item $f } }
function Squash([string]$Text) { ($Text -replace '\s+', ' ').Trim() }
function Get-Selection { (Get-Content (Join-Path $install 'current.json') -Raw | ConvertFrom-Json).Version }

# Churn: about every ChurnMs ms spawn `<install>\bell-tower.exe statusline --provider <p> --data-dir <data>` like the agent status line does.
$churnScript = Join-Path $root 'churn.ps1'
$churnCount = Join-Path $root 'churn.count'
[IO.File]::WriteAllText($churnScript, @'
param([string]$Exe, [string]$Data, [string]$Provider, [int]$Ms, [string]$CountFile)
$n = 0; $max = 0.0
while ($true) {
    $s = [Diagnostics.ProcessStartInfo]::new($Exe)
    $s.UseShellExecute = $false; $s.CreateNoWindow = $true; $s.RedirectStandardInput = $true; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true
    foreach ($a in @('statusline', '--provider', $Provider, '--data-dir', $Data)) { $s.ArgumentList.Add($a) }
    try {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $p = [Diagnostics.Process]::Start($s)
        $p.StandardInput.Write('{"session_id":"qa011","cwd":"."}'); $p.StandardInput.Close()
        [void]$p.StandardOutput.ReadToEndAsync(); [void]$p.StandardError.ReadToEndAsync()
        [void]$p.WaitForExit(5000)
        $n++; if ($clock.Elapsed.TotalMilliseconds -gt $max) { $max = $clock.Elapsed.TotalMilliseconds }
        [IO.File]::WriteAllText($CountFile, "$n $([int]$max)")
    } catch { }
    Start-Sleep -Milliseconds $Ms
}
'@)
$churn = $null
function Start-Churn {
    $script:churn = Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile', '-File', $churnScript, '-Exe', $launcher, '-Data', $data, '-Provider', $Provider, '-Ms', $ChurnMs, '-CountFile', $churnCount) -PassThru -WindowStyle Hidden
    $ownedPids.Add($script:churn.Id)
}
function Stop-Churn { if ($script:churn -and !$script:churn.HasExited) { try { $script:churn.Kill($true) } catch { } } }

$initial = [Diagnostics.ProcessStartInfo]::new($launcher); $initial.UseShellExecute = $false; $initial.CreateNoWindow = $true
foreach ($a in @('tray', '--data-dir', $data, '--silent')) { $initial.ArgumentList.Add($a) }
([Diagnostics.Process]::Start($initial)).Dispose()
$results = [ordered]@{ case = $Case; provider = $Provider; root = $root; original = $Original; startedUtc = [DateTime]::UtcNow.ToString('o') }
try {
    [void](Wait-Presence $Original)
    switch ($Case) {
        'churn' {
            Start-Churn
            Start-Sleep -Milliseconds 700
            $cycles = [Collections.Generic.List[object]]::new()
            $n = 0
            foreach ($candidate in $Candidates) {
                $n++
                $c = [ordered]@{ cycle = $n; candidate = $candidate }
                $m = Preflight $candidate
                $pid0 = (Get-Presence).Process.Pid
                $sw = Run 'Switch' $candidate $m
                $p = Get-Presence
                $c.switch = [ordered]@{ exit = $sw.exit; pipeClosed = $sw.closed; seconds = $sw.seconds; message = (Squash $sw.output); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); state = (Get-State $m) }
                $rb = $null
                if ($sw.exit -eq 0 -and $sw.closed -and $p -and $p.Version -eq $candidate) {
                    $rb = Run 'Rollback' $candidate $m
                    $p2 = Get-Presence
                    $c.rollback = [ordered]@{ exit = $rb.exit; pipeClosed = $rb.closed; seconds = $rb.seconds; message = (Squash $rb.output); trayRunning = [bool]$p2; trayVersion = $(if ($p2) { $p2.Version }); state = (Get-State $m); selection = (Get-Selection) }
                    $c.passed = ($rb.exit -eq 0 -and $rb.closed -and $p2 -and $p2.Version -eq $Original -and $p2.Process.Pid -ne $pid0)
                } else { $c.passed = $false }
                $c.trayRunningEnd = [bool](Get-Presence)
                $cycles.Add($c)
                if (!$c.passed) { break }
            }
            $results.cycles = $cycles.ToArray(); $results.passedCycles = @($cycles | ? { $_.passed }).Count; $results.totalCycles = $cycles.Count
            $results.churn = if (Test-Path $churnCount) { Get-Content $churnCount } else { $null }
            $results.churnAlive = [bool]($churn -and !$churn.HasExited)
        }
        'faults' {
            Start-Churn; Start-Sleep -Milliseconds 700
            $cand = @('0.1.9001', '0.1.9002', '0.1.9004')
            # A: refusal after the Switch stop.
            $m = Preflight $cand[0]; $pid0 = (Get-Presence).Process.Pid
            Set-Flag 'fail-after-switch-stop' $true; $r = Run 'Switch' $cand[0] $m; Set-Flag 'fail-after-switch-stop' $false
            $p = Get-Presence
            $A = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); newTrayPid = ($p -and $p.Process.Pid -ne $pid0); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = @(Get-ChildItem (Join-Path $install 'updates') -Filter *.json -ErrorAction SilentlyContinue | % Name) }
            $retry = Run 'Switch' $cand[0] $m
            $A.retryExit = $retry.exit; $A.retryClosed = $retry.closed; $A.retryTray = (Get-Presence).Version; $A.retryState = (Get-State $m)
            $rb = Run 'Rollback' $cand[0] $m
            $A.rollbackExit = $rb.exit; $A.rollbackTray = (Get-Presence).Version; $A.rollbackState = (Get-State $m)
            $A.passed = ($A.exit -ne 0 -and $A.pipeClosed -and $A.trayRunning -and $A.trayVersion -eq $Original -and $A.newTrayPid -and $A.manifestState -eq 'prepared' -and $A.retryExit -eq 0 -and $A.retryTray -eq $cand[0] -and $A.rollbackExit -eq 0 -and $A.rollbackTray -eq $Original)
            $results.A = $A
            # B: refusal plus failed restart.
            $m = Preflight $cand[1]
            Set-Flag 'fail-after-switch-stop' $true; Set-Flag "fail-start-$Original" $true
            $r = Run 'Switch' $cand[1] $m
            Set-Flag 'fail-after-switch-stop' $false; Set-Flag "fail-start-$Original" $false
            $B = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayStopped = (-not (Get-Presence)); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = @(Get-ChildItem (Join-Path $install 'updates') -Filter *.json -ErrorAction SilentlyContinue | % Name) }
            $retry = Run 'Switch' $cand[1] $m
            $B.retryExit = $retry.exit; $B.retryMessage = (Squash $retry.output)
            $rb = Run 'Rollback' $cand[1] $m
            $B.rollbackExit = $rb.exit; $B.rollbackClosed = $rb.closed; $B.rollbackTray = $(if (Get-Presence) { (Get-Presence).Version }); $B.rollbackState = (Get-State $m)
            $B.passed = ($B.exit -ne 0 -and $B.trayStopped -and $B.message -match 'Rollback' -and $B.message -match [regex]::Escape($m) -and $B.retryExit -ne 0 -and $B.rollbackExit -eq 0 -and $B.rollbackTray -eq $Original)
            $results.B = $B
            # C: Rollback refusal after the candidate stop.
            $m = Preflight $cand[2]
            $sw = Run 'Switch' $cand[2] $m
            Set-Flag 'fail-after-rollback-stop' $true; $r = Run 'Rollback' $cand[2] $m; Set-Flag 'fail-after-rollback-stop' $false
            $p = Get-Presence
            $C = [ordered]@{ switchExit = $sw.exit; exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayAfterRefusal = $(if ($p) { $p.Version }); manifestState = (Get-State $m); selection = (Get-Selection) }
            $rb = Run 'Rollback' $cand[2] $m
            $C.retryRollbackExit = $rb.exit; $C.retryRollbackTray = (Get-Presence).Version; $C.retryState = (Get-State $m); $C.retrySelection = (Get-Selection)
            $C.passed = ($sw.exit -eq 0 -and $C.exit -ne 0 -and $C.trayAfterRefusal -eq $cand[2] -and $C.retryRollbackExit -eq 0 -and $C.retryRollbackTray -eq $Original)
            $results.C = $C
            $results.churn = if (Test-Path $churnCount) { Get-Content $churnCount } else { $null }
        }
        'negative' {
            # LIVE-001 negative: a live process in the installation image that stays unclassifiable for the whole deadline.
            $held = [Diagnostics.ProcessStartInfo]::new($launcher); $held.UseShellExecute = $false; $held.CreateNoWindow = $true; $held.RedirectStandardInput = $true; $held.RedirectStandardOutput = $true; $held.RedirectStandardError = $true
            foreach ($a in @('statusline', '--provider', $Provider, '--data-dir', $data)) { $held.ArgumentList.Add($a) }
            $hp = [Diagnostics.Process]::Start($held); $ownedPids.Add($hp.Id)
            [void]$hp.StandardOutput.ReadToEndAsync(); [void]$hp.StandardError.ReadToEndAsync()   # stdin stays open: process waits
            Start-Sleep -Seconds 2
            $family = @(Get-CimInstance Win32_Process -Filter "Name='bell-tower.exe'" | ? { $_.ExecutablePath -like "$install*" -and $_.CommandLine -match 'statusline' } | % { [ordered]@{ pid = $_.ProcessId; parent = $_.ParentProcessId; image = $_.ExecutablePath.Substring($install.Length) } })
            foreach ($f in $family) { if ($f.pid -ne $hp.Id) { $ownedPids.Add($f.pid) } }
            $results.heldFamily = $family
            $m = Preflight $Candidates[0]; $pid0 = (Get-Presence).Process.Pid
            $r = Run 'Switch' $Candidates[0] $m
            $p = Get-Presence
            $N = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; seconds = $r.seconds; message = (Squash $r.output); heldStillAlive = (-not $hp.HasExited); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); newTrayPid = ($p -and $p.Process.Pid -ne $pid0); manifestState = (Get-State $m); selection = (Get-Selection) }
            # Release the held process (own PID tree), retry the same manifest.
            try { $hp.StandardInput.Close() } catch { }
            if (!$hp.WaitForExit(5000)) { $hp.Kill($true) }
            Start-Sleep -Milliseconds 500
            $retry = Run 'Switch' $Candidates[0] $m
            $N.retryExit = $retry.exit; $N.retryTray = $(if (Get-Presence) { (Get-Presence).Version }); $N.retryState = (Get-State $m)
            $rb = Run 'Rollback' $Candidates[0] $m
            $N.rollbackExit = $rb.exit; $N.rollbackTray = $(if (Get-Presence) { (Get-Presence).Version })
            $N.passed = ($N.exit -ne 0 -and $N.pipeClosed -and $N.message -match 'unclassifiable|safely classified' -and $N.seconds -ge 28 -and $N.trayRunning -and $N.trayVersion -eq $Original -and $N.manifestState -eq 'prepared' -and $N.retryExit -eq 0 -and $N.rollbackExit -eq 0 -and $N.rollbackTray -eq $Original)
            $results.negative = $N
        }
        'foreign' {
            # Foreign tray start for this data directory while the Switch holds its reservation (after the original tray stop).
            $m = Preflight $Candidates[0]
            $a = @('-Action', 'Switch', '-Package', (Join-Path $dev "bell-tower-$($Candidates[0])-win-x64.zip"), '-OperationManifest', $m) + $common
            $s = [Diagnostics.ProcessStartInfo]::new($pwsh); $s.UseShellExecute = $false; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true; $s.CreateNoWindow = $true
            foreach ($x in @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer) + $a) { $s.ArgumentList.Add($x) }
            $sp = [Diagnostics.Process]::Start($s); $ownedPids.Add($sp.Id)
            $out = $sp.StandardOutput.ReadToEndAsync(); $err = $sp.StandardError.ReadToEndAsync()
            $entered = Join-Path $barrier 'switch-before-selection.entered'
            $deadline = [DateTime]::UtcNow.AddSeconds(90); while (!(Test-Path $entered) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
            $FR = [ordered]@{ barrierEntered = (Test-Path $entered) }
            $fs = [Diagnostics.ProcessStartInfo]::new($launcher); $fs.UseShellExecute = $false; $fs.CreateNoWindow = $true
            foreach ($x in @('tray', '--data-dir', $data, '--silent')) { $fs.ArgumentList.Add($x) }
            $fp = [Diagnostics.Process]::Start($fs); $ownedPids.Add($fp.Id)
            $exited = $fp.WaitForExit(20000)
            $FR.foreignExitedWithinBarrier = $exited; $FR.foreignExitCode = $(if ($exited) { $fp.ExitCode }); $FR.presenceDuringBarrier = [bool](Get-Presence)
            if (!$exited) { try { $fp.Kill($true) } catch { } }
            New-Item -ItemType File -Path (Join-Path $barrier 'switch-before-selection.release') -Force | Out-Null
            $closed = [Threading.Tasks.Task]::WaitAll(@($out, $err), 120000)
            if (!$closed) { try { $sp.Kill($true) } catch { } } else { [void]$sp.WaitForExit(30000) }
            $FR.switchExit = $(if ($sp.HasExited) { $sp.ExitCode }); $FR.switchPipeClosed = $closed; $FR.trayAfter = $(if (Get-Presence) { (Get-Presence).Version }); $FR.trayOwnerActivation = $(if (Get-Presence) { (Get-Presence).Activation }); $FR.state = (Get-State $m)
            $rb = Run 'Rollback' $Candidates[0] $m; $FR.rollbackExit = $rb.exit; $FR.rollbackTray = $(if (Get-Presence) { (Get-Presence).Version })
            $FR.passed = ($FR.barrierEntered -and $FR.foreignExitedWithinBarrier -and -not $FR.presenceDuringBarrier -and $FR.switchExit -eq 0 -and $FR.trayAfter -eq $Candidates[0] -and $FR.rollbackExit -eq 0 -and $FR.rollbackTray -eq $Original)
            $results.foreign = $FR
        }
    }
} catch { $results.error = $_.ToString() }
finally {
    Stop-Churn
    try { $controller.RequestExit() } catch { }
    Start-Sleep -Seconds 2
    # Sweep: anything still running from this disposable root (own images only).
    $left = @(Get-CimInstance Win32_Process | ? { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })
    foreach ($l in $left) { try { Stop-Process -Id $l.ProcessId -Force } catch { } }
    $results.sweptProcesses = @($left | % { [ordered]@{ pid = $_.ProcessId; image = $_.ExecutablePath.Substring($root.Length) } })
    $results.endedUtc = [DateTime]::UtcNow.ToString('o')
}
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultFile
$results | ConvertTo-Json -Depth 8
