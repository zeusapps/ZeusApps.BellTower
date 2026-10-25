#Requires -Version 7.4
<#
QA 012 independent native driver (installed native execution on disposable synthetic installations/data under
artifacts/qa-012/; stable launcher + payload extracted from package zips; own disposable trays only).
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
    [Parameter(Mandatory)][ValidateSet('churn', 'held', 'negative', 'negative-suspended', 'faults')][string]$Case,
    [ValidateSet('claude', 'codex')][string]$Provider = 'claude',
    [string[]]$Candidates = @('0.1.9001', '0.1.9002', '0.1.9003', '0.1.9004', '0.1.9005', '0.1.9006'),
    [string]$Original = '0.1.9009',
    [int]$ChurnMs = 200,
    [int]$RunTimeoutSeconds = 150,
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot "qa-012-native-$Case-$Provider-results.json" }
$dev = Join-Path $repo 'artifacts/dev-builds'
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$root = Join-Path $repo ("artifacts/qa-012/$Case-$Provider-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
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
        $p.StandardInput.Write('{"session_id":"qa012","cwd":"."}'); $p.StandardInput.Close()
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
public static class Qa012Native {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr h, uint code);
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
        'held' {
            # A statusline process held open: classified NotTray now, so Switch must not wait for the 30 s deadline.
            $held = [Diagnostics.ProcessStartInfo]::new($launcher); $held.UseShellExecute = $false; $held.CreateNoWindow = $true; $held.RedirectStandardInput = $true; $held.RedirectStandardOutput = $true; $held.RedirectStandardError = $true
            foreach ($a in @('statusline', '--provider', $Provider, '--data-dir', $data)) { $held.ArgumentList.Add($a) }
            $hp = [Diagnostics.Process]::Start($held); $ownedPids.Add($hp.Id)
            [void]$hp.StandardOutput.ReadToEndAsync(); [void]$hp.StandardError.ReadToEndAsync()
            Start-Sleep -Seconds 2
            $family = @(Get-CimInstance Win32_Process -Filter "Name='bell-tower.exe'" | Where-Object { $_.ExecutablePath -like "$install*" -and $_.CommandLine -match 'statusline' } | ForEach-Object { [ordered]@{ pid = $_.ProcessId; parent = $_.ParentProcessId; image = $_.ExecutablePath.Substring($install.Length) } })
            foreach ($f in $family) { if ($f.pid -ne $hp.Id) { $ownedPids.Add($f.pid) } }
            $results.heldFamily = $family
            $m = Preflight $Candidates[0]
            $sw = Run 'Switch' $Candidates[0] $m
            $H = [ordered]@{ switchExit = $sw.exit; switchSeconds = $sw.seconds; pipeClosed = $sw.closed; message = (Squash $sw.output); heldStillAlive = (-not $hp.HasExited); trayVersion = (Get-OwnPresenceVersion) }
            $rb = Run 'Rollback' $Candidates[0] $m
            $H.rollbackExit = $rb.exit; $H.rollbackSeconds = $rb.seconds; $H.rollbackTray = (Get-OwnPresenceVersion); $H.recordsLeft = (Get-Records)
            try { $hp.StandardInput.Close() } catch { }
            if (!$hp.WaitForExit(5000)) { $hp.Kill($true) }
            $H.passed = ($H.switchExit -eq 0 -and $H.pipeClosed -and $H.switchSeconds -lt 15 -and $H.heldStillAlive -and $H.rollbackExit -eq 0 -and $H.rollbackTray -eq $Original -and $H.recordsLeft.Count -eq 0)
            $results.held = $H
        }
        { $_ -in 'negative', 'negative-suspended' } {
            # LIVE-001 negative: an installation-image process that stays unclassifiable for the whole deadline.
            #  negative-suspended: the real payload image created CREATE_SUSPENDED (no module loaded: classification fails on the image path).
            #  negative: a long-running stand-in image (a copy of ping.exe named bell-tower.exe under a disposable versions\ folder of this
            #            disposable installation) with an UNKNOWN first verb, fully loaded, so the failure is the unknown-command classification.
            if ($Case -eq 'negative-suspended') {
                $image = Join-Path $install "versions/$Original/bell-tower.exe"
                $cmd = '"' + $image + '" frobnicate-qa012 --data-dir "' + $data + '"'
                $pi = [Qa012Native]::Suspended($image, $cmd)
                $suspended = $pi; $ownedPids.Add($pi.dwProcessId); $heldPid = $pi.dwProcessId
            } else {
                $m = Preflight $Candidates[0]
                $standIn = Join-Path $install 'versions/0.0.1-qa012'
                New-Item -ItemType Directory -Path $standIn | Out-Null
                $image = Join-Path $standIn 'bell-tower.exe'
                Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32/PING.EXE') -Destination $image
                $hs = [Diagnostics.ProcessStartInfo]::new($image); $hs.UseShellExecute = $false; $hs.CreateNoWindow = $true; $hs.RedirectStandardOutput = $true
                foreach ($a in @('-n', '120', '127.0.0.1')) { $hs.ArgumentList.Add($a) }
                $hpn = [Diagnostics.Process]::Start($hs); $ownedPids.Add($hpn.Id); $heldPid = $hpn.Id
                [void]$hpn.StandardOutput.ReadToEndAsync()
            }
            Start-Sleep -Milliseconds 500
            $results.held = [ordered]@{ pid = $heldPid; commandLine = (Get-CimInstance Win32_Process -Filter "ProcessId=$heldPid").CommandLine; image = $image.Substring($install.Length) }
            if ($Case -eq 'negative-suspended') { $m = Preflight $Candidates[0] }
            $pid0 = (Get-Presence).Process.Pid
            $r = Run 'Switch' $Candidates[0] $m
            $p = Get-Presence
            $N = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; seconds = $r.seconds; message = (Squash $r.output); heldStillAlive = [bool](Get-Process -Id $heldPid -ErrorAction SilentlyContinue); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); newTrayPid = ($p -and $p.Process.Pid -ne $pid0); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = (Get-Records) }
            if ($Case -eq 'negative-suspended') { [void][Qa012Native]::TerminateProcess($pi.hProcess, 1); [void][Qa012Native]::CloseHandle($pi.hProcess); [void][Qa012Native]::CloseHandle($pi.hThread); $suspended = $null } else { try { $hpn.Kill($true); [void]$hpn.WaitForExit(5000) } catch { }; Remove-Item -LiteralPath (Join-Path $install 'versions/0.0.1-qa012') -Recurse -Force }
            Start-Sleep -Milliseconds 500
            $retry = Run 'Switch' $Candidates[0] $m
            $N.retryExit = $retry.exit; $N.retryTray = (Get-OwnPresenceVersion); $N.retryState = (Get-State $m)
            $rb = Run 'Rollback' $Candidates[0] $m
            $N.rollbackExit = $rb.exit; $N.rollbackTray = (Get-OwnPresenceVersion); $N.recordsAfter = (Get-Records)
            $N.passed = ($N.exit -ne 0 -and $N.pipeClosed -and $N.message -match 'unclassifiable|safely classified' -and ($Case -eq 'negative-suspended' -or $N.message -match 'unknown Bell Tower command') -and $N.seconds -ge 28 -and $N.trayRunning -and $N.trayVersion -eq $Original -and $N.newTrayPid -and $N.manifestState -eq 'prepared' -and $N.recordsLeft.Count -eq 0 -and $N.retryExit -eq 0 -and $N.rollbackExit -eq 0 -and $N.rollbackTray -eq $Original)
            $results.negative = $N
        }
        'faults' {
            Start-Churn; Start-Sleep -Milliseconds 700
            $cand = @('0.1.9001', '0.1.9002', '0.1.9004', '0.1.9005', '0.1.9006')
            # A: refusal after the Switch stop (regression).
            $m = Preflight $cand[0]; $pid0 = (Get-Presence).Process.Pid
            Set-Flag 'fail-after-switch-stop' $true; $r = Run 'Switch' $cand[0] $m; Set-Flag 'fail-after-switch-stop' $false
            $p = Get-Presence
            $A = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayRunning = [bool]$p; trayVersion = $(if ($p) { $p.Version }); newTrayPid = ($p -and $p.Process.Pid -ne $pid0); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = (Get-Records) }
            $retry = Run 'Switch' $cand[0] $m
            $A.retryExit = $retry.exit; $A.retryClosed = $retry.closed; $A.retryTray = (Get-OwnPresenceVersion); $A.retryState = (Get-State $m)
            $rb = Run 'Rollback' $cand[0] $m
            $A.rollbackExit = $rb.exit; $A.rollbackTray = (Get-OwnPresenceVersion); $A.rollbackState = (Get-State $m); $A.recordsAfter = (Get-Records)
            $A.passed = ($A.exit -ne 0 -and $A.pipeClosed -and $A.trayRunning -and $A.trayVersion -eq $Original -and $A.newTrayPid -and $A.manifestState -eq 'prepared' -and $A.recordsLeft.Count -eq 0 -and $A.retryExit -eq 0 -and $A.retryTray -eq $cand[0] -and $A.rollbackExit -eq 0 -and $A.rollbackTray -eq $Original -and $A.recordsAfter.Count -eq 0)
            $results.A = $A
            # B: refusal plus failed restart before launch (regression; ANALYSIS-552 part: no records kept when nothing launched).
            $m = Preflight $cand[1]
            Set-Flag 'fail-after-switch-stop' $true; Set-Flag "fail-start-$Original" $true
            $r = Run 'Switch' $cand[1] $m
            Set-Flag 'fail-after-switch-stop' $false; Set-Flag "fail-start-$Original" $false
            $B = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayStopped = (-not (Get-Presence)); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = (Get-Records) }
            $retry = Run 'Switch' $cand[1] $m
            $B.retryExit = $retry.exit; $B.retryMessage = (Squash $retry.output)
            $rb = Run 'Rollback' $cand[1] $m
            $B.rollbackExit = $rb.exit; $B.rollbackClosed = $rb.closed; $B.rollbackTray = (Get-OwnPresenceVersion); $B.rollbackState = (Get-State $m); $B.recordsAfter = (Get-Records)
            $B.passed = ($B.exit -ne 0 -and $B.trayStopped -and $B.message -match 'Rollback' -and $B.message -notmatch 'may be running unverified' -and $B.message -match [regex]::Escape($m) -and $B.recordsLeft.Count -eq 0 -and $B.retryExit -ne 0 -and $B.rollbackExit -eq 0 -and $B.rollbackTray -eq $Original -and $B.recordsAfter.Count -eq 0)
            $results.B = $B
            # C: Rollback refusal after the candidate stop (regression).
            $m = Preflight $cand[2]
            $sw = Run 'Switch' $cand[2] $m
            Set-Flag 'fail-after-rollback-stop' $true; $r = Run 'Rollback' $cand[2] $m; Set-Flag 'fail-after-rollback-stop' $false
            $p = Get-Presence
            $C = [ordered]@{ switchExit = $sw.exit; exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); trayAfterRefusal = $(if ($p) { $p.Version }); manifestState = (Get-State $m); selection = (Get-Selection); recordsLeft = (Get-Records) }
            $rb = Run 'Rollback' $cand[2] $m
            $C.retryRollbackExit = $rb.exit; $C.retryRollbackTray = (Get-OwnPresenceVersion); $C.retryState = (Get-State $m); $C.retrySelection = (Get-Selection); $C.recordsAfter = (Get-Records)
            $C.passed = ($sw.exit -eq 0 -and $C.exit -ne 0 -and $C.trayAfterRefusal -eq $cand[2] -and $C.recordsLeft.Count -eq 0 -and $C.retryRollbackExit -eq 0 -and $C.retryRollbackTray -eq $Original -and $C.recordsAfter.Count -eq 0)
            $results.C = $C
            # D (ANALYSIS-552): post-launch restart failure with no candidate payload, then same-manifest Rollback.
            $m = Preflight $cand[3]
            Set-Flag 'fail-after-switch-stop' $true; Set-Flag "fail-after-launch-$Original" $true
            $r = Run 'Switch' $cand[3] $m
            Set-Flag 'fail-after-switch-stop' $false; Set-Flag "fail-after-launch-$Original" $false
            $deadline = [DateTime]::UtcNow.AddSeconds(20); while (-not (Get-Presence) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
            $launched = Get-Presence
            $D = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); messageHasUnverified = ($r.output -match 'may be running unverified'); messageSaysMonitoringStopped = ($r.output -match 'Monitoring is stopped'); trayCameUp = [bool]$launched; trayVersion = $(if ($launched) { $launched.Version }); recordsKept = (Get-Records); manifestState = (Get-State $m); candidatePayloadExists = (Test-Path (Join-Path $install "versions/$($cand[3])")); selection = (Get-Selection) }
            # End that disposable tray (own data directory, own launcher) and wait for it to go.
            try { $controller.RequestExit() } catch { }
            $gone = [DateTime]::UtcNow.AddSeconds(30); while ((Get-Presence) -and [DateTime]::UtcNow -lt $gone) { Start-Sleep -Milliseconds 200 }
            $D.trayEndedBeforeRollback = (-not (Get-Presence))
            $rb = Run 'Rollback' $cand[3] $m
            $D.rollbackExit = $rb.exit; $D.rollbackClosed = $rb.closed; $D.rollbackSeconds = $rb.seconds; $D.rollbackMessage = (Squash $rb.output); $D.rollbackTray = (Get-OwnPresenceVersion); $D.rollbackState = (Get-State $m); $D.recordsAfter = (Get-Records)
            $D.passed = ($D.exit -ne 0 -and $D.pipeClosed -and $D.messageHasUnverified -and -not $D.messageSaysMonitoringStopped -and $D.recordsKept.Count -gt 0 -and -not $D.candidatePayloadExists -and $D.trayEndedBeforeRollback -and $D.rollbackExit -eq 0 -and $D.rollbackTray -eq $Original -and $D.recordsAfter.Count -eq 0)
            $results.D = $D
            # E (ANALYSIS-553): a leftover before-monitoring folder makes the restarted operation terminal.
            $m = Preflight $cand[4]
            $opdir = (Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).operationDirectory
            New-Item -ItemType Directory -Path (Join-Path $opdir 'before-monitoring') | Out-Null
            Set-Flag 'fail-after-switch-stop' $true; $r = Run 'Switch' $cand[4] $m; Set-Flag 'fail-after-switch-stop' $false
            $E = [ordered]@{ exit = $r.exit; pipeClosed = $r.closed; message = (Squash $r.output); messageSaysPreflightAgain = ($r.output -match 'run Preflight again'); trayVersion = (Get-OwnPresenceVersion); manifestState = (Get-State $m); terminalFlag = ((Get-Content -LiteralPath $m -Raw | ConvertFrom-Json).PSObject.Properties.Name -contains 'trayRestartedAfterFailure'); recordsLeft = (Get-Records) }
            $rb = Run 'Rollback' $cand[4] $m
            $E.rollbackExit = $rb.exit; $E.rollbackClosed = $rb.closed; $E.rollbackSeconds = $rb.seconds; $E.rollbackMessage = (Squash $rb.output); $E.rollbackRefusedAtOnce = ($rb.exit -ne 0 -and $rb.seconds -lt 15 -and $rb.output -match 'Nothing to roll back; run Preflight again'); $E.trayAfterRollback = (Get-OwnPresenceVersion); $E.recordsAfter = (Get-Records)
            $retry = Run 'Switch' $cand[4] $m
            $E.switchRetryExit = $retry.exit; $E.switchRetryMessage = (Squash $retry.output)
            $E.passed = ($E.exit -ne 0 -and $E.pipeClosed -and $E.messageSaysPreflightAgain -and $E.terminalFlag -and $E.recordsLeft.Count -eq 0 -and $E.trayVersion -eq $Original -and $E.rollbackRefusedAtOnce -and $E.trayAfterRollback -eq $Original -and $E.recordsAfter.Count -eq 0)
            $results.E = $E
            $results.churn = if (Test-Path $churnCount) { Get-Content $churnCount } else { $null }
        }
    }
} catch { $results.error = $_.ToString() }
finally {
    Stop-Churn
    if ($suspended) { try { [void][Qa012Native]::TerminateProcess($suspended.hProcess, 1) } catch { } }
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
