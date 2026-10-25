#Requires -Version 7.4
<#
QA 012 independent native driver (installed native execution on disposable synthetic installations/data under
artifacts/qa-013/; stable launcher + payload extracted from package zips; own disposable trays only).
Cases:
  churn     Switch->Rollback cycles with a Claude- or Codex-flavored `statusline` churn (~200 ms), then direct drain durations
            (the real Wait-InstallationTrayStartsDrained, extracted from scripts/install-dev.ps1 by AST) under the same churn.
  held      a `statusline` process held open on stdin (now classified NotTray): Switch must not wait for the deadline.
  negative  LIVE-001 negative: an installation-image process with an UNKNOWN verb, created suspended so it stays alive
            (CreateProcess CREATE_SUSPENDED); Switch must refuse at the deadline and restart the original tray.
  faults    LIVE-002 regression (A/B/C) plus D (post-launch failure -> unverified message -> same-manifest Rollback, no records)
            and E (terminal trayRestartedAfterFailure -> Preflight-again hint -> Rollback refuses at once).
Every installer run uses captured pipes read to the end with a hard timeout (LIVE-003). Only processes this script started
are ever stopped, by exact PID (or by image path under its own disposable root).
#>
param(
    [Parameter(Mandatory)][ValidateSet('churn', 'cases', 'timeout')][string]$Case,
    [ValidateSet('claude', 'codex')][string]$Provider = 'claude',
    [string[]]$Candidates = @('0.1.9001', '0.1.9002', '0.1.9003', '0.1.9004', '0.1.9005', '0.1.9006'),
    [string]$Original = '0.1.9009',
    [int]$ChurnMs = 200,
    [int]$RunTimeoutSeconds = 150,
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot "qa-013-native-$Case-$Provider-results.json" }
$dev = Join-Path $repo 'artifacts/dev-builds'
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$root = Join-Path $repo ("artifacts/qa-013/$Case-$Provider-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $workspace = Join-Path $root 'private'; $barrier = Join-Path $root 'barriers'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
New-Item -ItemType Directory -Path $root, $install, $data, $barrier | Out-Null
foreach ($phase in @('switch-before-selection', 'rollback-before-selection')) { New-Item -ItemType File -Path (Join-Path $barrier ($phase + '.release')) | Out-Null }
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
function Get-Records { @(Get-ChildItem (Join-Path $install 'updates') -Filter *.json -ErrorAction SilentlyContinue | ForEach-Object Name) }
function Get-OwnPresenceVersion { $p = Get-Presence; if ($p) { $p.Version } }

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
        $p.StandardInput.Write('{"session_id":"qa013","cwd":"."}'); $p.StandardInput.Close()
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

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class Qa013Native {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("ntdll.dll")] public static extern int NtSuspendProcess(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    public static IntPtr Open(int pid) { return OpenProcess(0x800, false, pid); }
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static PROCESS_INFORMATION Suspended(string app, string cmd) {
        var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(typeof(STARTUPINFO)); PROCESS_INFORMATION pi;
        if (!CreateProcessW(app, cmd, IntPtr.Zero, IntPtr.Zero, false, 0x4 | 0x08000000, IntPtr.Zero, null, ref si, out pi)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return pi;
    }
}
'@

$initial = [Diagnostics.ProcessStartInfo]::new($launcher); $initial.UseShellExecute = $false; $initial.CreateNoWindow = $true
foreach ($a in @('tray', '--data-dir', $data, '--silent')) { $initial.ArgumentList.Add($a) }
([Diagnostics.Process]::Start($initial)).Dispose()
$results = [ordered]@{ case = $Case; provider = $Provider; root = $root; original = $Original; startedUtc = [DateTime]::UtcNow.ToString('o') }
$suspended = $null
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
                $c.switch = [ordered]@{ exit = $sw.exit; pipeClosed = $sw.closed; seconds = $sw.seconds; message = (Squash $sw.output); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); state = (Get-State $m); recordsLeft = (Get-Records) }
                if ($sw.exit -eq 0 -and $sw.closed -and $p -and $p.Version -eq $candidate) {
                    $rb = Run 'Rollback' $candidate $m
                    $p2 = Get-Presence
                    $c.rollback = [ordered]@{ exit = $rb.exit; pipeClosed = $rb.closed; seconds = $rb.seconds; message = (Squash $rb.output); trayRunning = [bool]$p2; trayVersion = $(if ($p2) { $p2.Version }); state = (Get-State $m); selection = (Get-Selection); recordsLeft = (Get-Records) }
                    $c.passed = ($rb.exit -eq 0 -and $rb.closed -and $p2 -and $p2.Version -eq $Original -and $p2.Process.Pid -ne $pid0 -and (Get-Records).Count -eq 0)
                } else { $c.passed = $false }
                $cycles.Add($c)
                if (!$c.passed) { break }
            }
            $results.cycles = $cycles.ToArray(); $results.passedCycles = @($cycles | Where-Object { $_.passed }).Count; $results.totalCycles = $cycles.Count
            $results.churn = if (Test-Path $churnCount) { Get-Content $churnCount } else { $null }
            $results.churnAlive = [bool]($churn -and !$churn.HasExited)
            # Direct drain durations under the same churn with no tray running (the real function, extracted by AST).
            try { $controller.RequestExit() } catch { }
            $gone = [DateTime]::UtcNow.AddSeconds(20); while ((Get-Presence) -and [DateTime]::UtcNow -lt $gone) { Start-Sleep -Milliseconds 100 }
            $ast = [Management.Automation.Language.Parser]::ParseFile($installer, [ref]$null, [ref]$null)
            foreach ($definition in @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] })) { . ([scriptblock]::Create($definition.Extent.Text)) }
            $InstallationRoot = $install; $DataDirectory = $data; $script:expectedStoppedTray = $null
            $drains = [Collections.Generic.List[object]]::new()
            1..8 | ForEach-Object {
                $clock = [Diagnostics.Stopwatch]::StartNew(); $failure = $null
                try { Wait-InstallationTrayStartsDrained } catch { $failure = $_.Exception.Message }
                $drains.Add([ordered]@{ seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2); refused = $failure })
                Start-Sleep -Milliseconds 300
            }
            $results.directDrains = $drains.ToArray()
            $results.maxDrainSeconds = ($drains | ForEach-Object { $_.seconds } | Measure-Object -Maximum).Maximum
            $results.churnAfterDrains = if (Test-Path $churnCount) { Get-Content $churnCount } else { $null }
        }
        'cases' {
            $cand = @('0.1.9001', '0.1.9002')
            # B: a completed Switch (dev selected) whose manifest carries a stale trayRestartedAfterFailure flag must still roll back.
            $m = Preflight $cand[0]
            $sw = Run 'Switch' $cand[0] $m
            $B = [ordered]@{ switchExit = $sw.exit; switchTray = (Get-OwnPresenceVersion); stateAfterSwitch = (Get-State $m); selectionAfterSwitch = (Get-Selection) }
            $json = Get-Content -LiteralPath $m -Raw | ConvertFrom-Json
            $json | Add-Member -NotePropertyName trayRestartedAfterFailure -NotePropertyValue $true -Force
            [IO.File]::WriteAllText($m, ($json | ConvertTo-Json -Depth 20 -Compress), [Text.UTF8Encoding]::new($false))
            $B.flagPlanted = ((Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).PSObject.Properties.Name -contains 'trayRestartedAfterFailure')
            $rb = Run 'Rollback' $cand[0] $m
            $B.rollbackExit = $rb.exit; $B.rollbackClosed = $rb.closed; $B.rollbackSeconds = $rb.seconds; $B.rollbackMessage = (Squash $rb.output)
            $B.rollbackTray = (Get-OwnPresenceVersion); $B.stateAfterRollback = (Get-State $m); $B.selectionAfterRollback = (Get-Selection); $B.recordsAfter = (Get-Records)
            $B.passed = ($sw.exit -eq 0 -and $B.switchTray -eq $cand[0] -and $B.stateAfterSwitch -ne 'prepared' -and $B.flagPlanted -and $rb.exit -eq 0 -and $rb.closed -and $B.stateAfterRollback -eq 'running-original' -and $B.rollbackTray -eq $Original -and $B.selectionAfterRollback -eq $Original -and $B.recordsAfter.Count -eq 0 -and $rb.output -notmatch 'Nothing to roll back')
            $results.B = $B
            # C: never-selected terminal case (fail after the Switch stop with a leftover before-monitoring folder) refuses Rollback at once.
            $m = Preflight $cand[1]
            $opdir = (Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).operationDirectory
            New-Item -ItemType Directory -Path (Join-Path $opdir 'before-monitoring') | Out-Null
            Set-Flag 'fail-after-switch-stop' $true; $r = Run 'Switch' $cand[1] $m; Set-Flag 'fail-after-switch-stop' $false
            $C = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayVersion = (Get-OwnPresenceVersion); manifestState = (Get-State $m); terminalFlag = ((Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).PSObject.Properties.Name -contains 'trayRestartedAfterFailure'); selection = (Get-Selection); recordsLeft = (Get-Records) }
            $rb = Run 'Rollback' $cand[1] $m
            $C.rollbackExit = $rb.exit; $C.rollbackSeconds = $rb.seconds; $C.rollbackMessage = (Squash $rb.output); $C.rollbackRefusedAtOnce = ($rb.exit -ne 0 -and $rb.seconds -lt 15 -and $rb.output -match 'Nothing to roll back; run Preflight again'); $C.trayAfterRollback = (Get-OwnPresenceVersion); $C.recordsAfter = (Get-Records)
            $C.passed = ($C.exit -ne 0 -and $C.pipeClosed -and $C.terminalFlag -and $C.trayVersion -eq $Original -and $C.recordsLeft.Count -eq 0 -and $C.rollbackRefusedAtOnce -and $C.trayAfterRollback -eq $Original -and $C.recordsAfter.Count -eq 0)
            $results.C = $C
        }
        'timeout' {
            # A: drive the 'Verified tray did not exit' timeout natively. The disposable tray is suspended (NtSuspendProcess) so it cannot exit;
            # a watcher kills it at (first coordination record + delta) so the tray is gone only after the 30 s wait gave up and before
            # Restore's Read-Owner. Bisect delta over attempts. Outcomes: early (stop wait succeeded), hit (restarted, state prepared), late (live owner).
            $cand = @('0.1.9001', '0.1.9002', '0.1.9004', '0.1.9005', '0.1.9006', '0.1.9007', '0.1.9008', '0.1.9003')
            $lo = 29.0; $hi = 33.0; $attempts = [Collections.Generic.List[object]]::new(); $hit = $null
            $updates = Join-Path $install 'updates'
            for ($i = 0; $i -lt 8 -and !$hit; $i++) {
                $delta = ($lo + $hi) / 2
                $c = [ordered]@{ attempt = $i + 1; candidate = $cand[$i]; delta = [Math]::Round($delta, 3) }
                $m = Preflight $cand[$i]
                $trayBefore = Get-Presence; $pid0 = $trayBefore.Process.Pid
                $h = [Qa013Native]::Open($pid0)
                [void][Qa013Native]::NtSuspendProcess($h)
                $s = [Diagnostics.ProcessStartInfo]::new($pwsh)
                $s.UseShellExecute = $false; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true; $s.CreateNoWindow = $true
                foreach ($a in (@('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-Action', 'Switch', '-Package', (Join-Path $dev "bell-tower-$($cand[$i])-win-x64.zip"), '-OperationManifest', $m) + $common)) { $s.ArgumentList.Add($a) }
                $clock = [Diagnostics.Stopwatch]::StartNew()
                $ip = [Diagnostics.Process]::Start($s); $ownedPids.Add($ip.Id)
                $out = $ip.StandardOutput.ReadToEndAsync(); $err = $ip.StandardError.ReadToEndAsync()
                $t0 = $null
                while ($clock.Elapsed.TotalSeconds -lt 60 -and !$ip.HasExited) {
                    if (@(Get-ChildItem -LiteralPath $updates -Filter *.json -ErrorAction SilentlyContinue).Count -gt 0) { $t0 = $clock.Elapsed.TotalSeconds; break }
                    [Threading.Thread]::Sleep(2)
                }
                $c.recordAtSeconds = $t0
                if ($t0) {
                    $target = $t0 + $delta
                    while ($clock.Elapsed.TotalSeconds -lt $target -and !$ip.HasExited) { [Threading.Thread]::Sleep(1) }
                    $c.killedAtSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 3)
                    try { Stop-Process -Id $pid0 -Force } catch { }
                }
                $closed = [Threading.Tasks.Task]::WaitAll(@($out, $err), 150000)
                if ($closed) { [void]$ip.WaitForExit(30000) } else { try { $ip.Kill($true) } catch { } }
                [void][Qa013Native]::CloseHandle($h)
                $msg = if ($closed) { Squash ($out.Result + $err.Result) } else { '' }
                $c.exit = $(if ($ip.HasExited) { $ip.ExitCode } else { $null }); $c.pipeClosed = $closed; $c.seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 1); $c.message = $msg
                $c.state = (Get-State $m); $c.selection = (Get-Selection); $c.recordsLeft = (Get-Records)
                $c.terminalFlag = ((Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).PSObject.Properties.Name -contains 'trayRestartedAfterFailure')
                Start-Sleep -Milliseconds 1500
                $pn = Get-Presence; $c.trayVersion = $(if ($pn) { $pn.Version }); $c.newTrayPid = [bool]($pn -and $pn.Process.Pid -ne $pid0)
                if ($c.exit -eq 0) { $c.outcome = 'early'; $lo = $delta }
                elseif ($msg -match 'still running') { $c.outcome = 'late'; $hi = $delta }
                elseif ($msg -match 'was restarted') { $c.outcome = 'hit' }
                else { $c.outcome = 'other' }
                if ($c.outcome -eq 'hit') {
                    $hit = $c; $hitManifest = $m; $hitCand = $cand[$i]
                } else {
                    if ($c.outcome -eq 'early') { $rb = Run 'Rollback' $cand[$i] $m; $c.recoveryRollbackExit = $rb.exit }
                    if (-not (Get-Presence)) {
                        $ini = [Diagnostics.ProcessStartInfo]::new($launcher); $ini.UseShellExecute = $false; $ini.CreateNoWindow = $true
                        foreach ($a in @('tray', '--data-dir', $data, '--silent')) { $ini.ArgumentList.Add($a) }
                        ([Diagnostics.Process]::Start($ini)).Dispose()
                        try { [void](Wait-Presence $Original) } catch { $c.restartError = $_.ToString() }
                    }
                }
                $attempts.Add($c)
            }
            $results.attempts = $attempts.ToArray()
            if ($hit) {
                $A = [ordered]@{ attempt = $hit.attempt; hitMessage = $hit.message; hitState = $hit.state; terminalFlagSaved = $hit.terminalFlag; recordsLeft = $hit.recordsLeft; trayVersion = $hit.trayVersion; newTrayPid = $hit.newTrayPid }
                $retry = Run 'Switch' $hitCand $hitManifest
                $A.retryExit = $retry.exit; $A.retryTray = (Get-OwnPresenceVersion); $A.retryState = (Get-State $hitManifest); $A.retryMessage = (Squash $retry.output)
                $rb = Run 'Rollback' $hitCand $hitManifest
                $A.rollbackExit = $rb.exit; $A.rollbackMessage = (Squash $rb.output); $A.rollbackTray = (Get-OwnPresenceVersion); $A.rollbackState = (Get-State $hitManifest); $A.selectionAfterRollback = (Get-Selection); $A.recordsAfter = (Get-Records)
                $A.passed = ($hit.exit -ne 0 -and $hit.state -eq 'prepared' -and -not $hit.terminalFlag -and $hit.recordsLeft.Count -eq 0 -and $hit.newTrayPid -and $hit.message -match 'can be retried' -and $retry.exit -eq 0 -and $A.retryTray -eq $hitCand -and $rb.exit -eq 0 -and $A.rollbackTray -eq $Original -and $A.rollbackState -eq 'running-original' -and $A.recordsAfter.Count -eq 0)
                $results.A = $A
            } else { $results.A = [ordered]@{ passed = $false; note = 'timeout trigger not hit natively within the attempt budget' } }
        }
    }
} catch { $results.error = $_.ToString() }
finally {
    Stop-Churn
    if ($suspended) { try { [void][Qa013Native]::TerminateProcess($suspended.hProcess, 1) } catch { } }
    try { $controller.RequestExit() } catch { }
    Start-Sleep -Seconds 2
    # Sweep: anything still running from this disposable root (own images only).
    $left = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })
    foreach ($l in $left) { try { Stop-Process -Id $l.ProcessId -Force } catch { } }
    $results.sweptProcesses = @($left | ForEach-Object { [ordered]@{ pid = $_.ProcessId; image = $_.ExecutablePath.Substring($root.Length) } })
    $results.ownedPids = $ownedPids.ToArray()
    $results.endedUtc = [DateTime]::UtcNow.ToString('o')
}
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultFile
$results | ConvertTo-Json -Depth 8
