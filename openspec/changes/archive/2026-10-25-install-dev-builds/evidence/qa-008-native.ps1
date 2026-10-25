#Requires -Version 7.4
<#
QA 007 independent native disposable matrix for install-dev.ps1. Evidence type: installed native execution on synthetic
disposable installations/data under artifacts/qa-008/ (stable launcher + payload copied from package zips; own tray).
Children write to files only. Interrupts end only the exact installer process this script started.
#>
param(
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string[]]$Cases = @('lifecycle'),
    [string]$Candidate = '0.1.9006', [string]$Original = '0.1.9003', [string]$RetireControl = '0.1.9004',
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
$Cases = @($Cases | ForEach-Object { $_ -split ',' })
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot ('qa-008-native-' + ($Cases -join '+') + '-results.json') }
$dev = Join-Path $Repo 'artifacts/dev-builds'
$package = Join-Path $dev "bell-tower-$Candidate-win-x64.zip"
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $Repo 'scripts/install-dev.ps1'
$qaRoot = Join-Path $Repo 'artifacts/qa-008'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
function Hash([string]$Path) { if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() } return $null }
function Free-GB { [Math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2) }

# Clean source of the "original" installation: extracted from the original package zip (not an unpacked build folder).
$source = Join-Path $qaRoot "source-$Original"
if (!(Test-Path $source)) { New-Item -ItemType Directory -Path $source | Out-Null; Expand-Archive -LiteralPath (Join-Path $dev "bell-tower-$Original-win-x64.zip") -DestinationPath $source }

function New-Case([string]$Name) {
    $root = Join-Path $qaRoot ("native-$Name-" + [Guid]::NewGuid().ToString('N').Substring(0, 10))
    $c = [ordered]@{ name = $Name; root = $root; install = (Join-Path $root 'install'); data = (Join-Path $root 'data'); workspace = (Join-Path $root 'private'); barriers = (Join-Path $root 'barriers'); logs = (Join-Path $root 'logs') }
    New-Item -ItemType Directory -Path $root, $c.install, $c.data, $c.barriers, $c.logs | Out-Null
    Get-ChildItem -LiteralPath $source | Copy-Item -Destination $c.install -Recurse -Force
    [IO.File]::WriteAllText((Join-Path $c.data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
    [IO.File]::WriteAllText((Join-Path $c.data 'state.json.bak'), '{"schemaVersion":1,"sessions":{},"qa":"original-backup"}')
    $c.paths = [BellTower.IO.DataPaths]::new($c.data)
    $c.controller = [BellTower.Tray.TrayController]::new($c.paths)
    $c.presence = [BellTower.Updates.UpdateTransaction]::Presence($c.paths)
    $c.selection = Join-Path $c.install 'current.json'
    $c.pending = Join-Path $c.install 'updates/pending.json'
    $c.restart = [BellTower.Updates.UpdateTransaction]::Marker($c.paths)
    return $c
}
function Wait-Presence($C, [string]$Version, [int]$Seconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $p = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($C.presence, 4096)
            if ($p.Version -eq $Version -and [BellTower.IO.ProcessProbe]::Inspect($p.Process) -eq [BellTower.IO.ProcessLiveness]::Live) { return $p }
        } catch { }
        Start-Sleep -Milliseconds 50
    }
    throw "No live disposable $Version tray presence in $($C.name)."
}
function Get-LivePresence($C) {
    if (!(Test-Path -LiteralPath $C.presence)) { return $null }
    try { $p = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($C.presence, 4096) } catch { return $null }
    if ([BellTower.IO.ProcessProbe]::Inspect($p.Process) -ne [BellTower.IO.ProcessLiveness]::Live) { return $null }
    return $p
}
function Start-InitialTray($C) {
    $s = [Diagnostics.ProcessStartInfo]::new((Join-Path $C.install 'bell-tower.exe')); $s.UseShellExecute = $false; $s.CreateNoWindow = $true
    foreach ($a in @('tray', '--data-dir', $C.data, '--silent')) { [void]$s.ArgumentList.Add($a) }
    ([Diagnostics.Process]::Start($s)).Dispose()
    return (Wait-Presence $C $Original)
}
function Start-Installer($C, [string]$Action, [string]$Manifest, [string]$Barrier, [string[]]$Extra = @()) {
    $id = $Action + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $a = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-Action', $Action, '-Package', $package,
        '-InstallationRoot', $C.install, '-DataDirectory', $C.data, '-Workspace', $C.workspace, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild')
    if ($Manifest) { $a += @('-OperationManifest', $Manifest) }
    if ($Barrier) { $a += @('-TestBarrierDirectory', $Barrier) }
    $a += $Extra
    $out = Join-Path $C.logs ($id + '.out'); $err = Join-Path $C.logs ($id + '.err')
    $p = Start-Process -FilePath $pwsh -ArgumentList $a -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    return [pscustomobject]@{ process = $p; identity = [BellTower.IO.ProcessProbe]::Identify($p.Id); out = $out; err = $err }
}
function Stop-ExactInstaller($R) {
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($R.process.Id)" -Property ProcessId, CommandLine
    if (!$cim -or !$cim.CommandLine.Contains($installer, [StringComparison]::OrdinalIgnoreCase) -or [BellTower.IO.ProcessProbe]::Identify($R.process.Id) -ne $R.identity) { throw 'Refusing to interrupt a process that is not the exact disposable installer.' }
    $R.process.Kill(); [void]$R.process.WaitForExit(10000)
    if ([BellTower.IO.ProcessProbe]::Inspect($R.identity) -ne [BellTower.IO.ProcessLiveness]::Ended) { throw 'Installer did not end after exact interruption.' }
}
function Complete-Installer($R) {
    if (!$R.process.WaitForExit(180000)) { Stop-ExactInstaller $R; throw 'Installer exceeded three minutes.' }
    $t = ((Get-Content -LiteralPath $R.out -Raw -ErrorAction SilentlyContinue) + ' ' + (Get-Content -LiteralPath $R.err -Raw -ErrorAction SilentlyContinue))
    return [pscustomobject]@{ exitCode = $R.process.ExitCode; output = ($t -replace '\x1b\[[0-9;]*m', '' -replace '\s*\r?\n\s*\|?\s*', ' ').Trim() }
}
function Invoke-Installer($C, [string]$Action, [string]$Manifest, [string]$Barrier, [string[]]$Extra = @()) { Complete-Installer (Start-Installer $C $Action $Manifest $Barrier $Extra) }
function Wait-Barrier($R, [string]$Barrier, [string]$Phase) {
    $e = Join-Path $Barrier ($Phase + '.entered'); $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while (!(Test-Path $e) -and !$R.process.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 30 }
    if (!(Test-Path $e)) { $d = if ($R.process.HasExited) { Complete-Installer $R } else { Stop-ExactInstaller $R; $null }; throw "Installer did not reach $Phase. $($d.output)" }
}
function New-Barrier($C, [string[]]$Files) {
    $d = Join-Path $C.barriers ([Guid]::NewGuid().ToString('N').Substring(0, 8)); New-Item -ItemType Directory -Path $d | Out-Null
    foreach ($f in $Files) { [IO.File]::WriteAllText((Join-Path $d $f), 'probe') }
    return $d
}
function Release-Barrier([string]$B, [string]$Phase) { [IO.File]::WriteAllText((Join-Path $B ($Phase + '.release')), 'probe') }
function Read-Manifest([string]$P) { Get-Content -LiteralPath $P -Raw | ConvertFrom-Json }
function Get-Preflight($C) {
    $before = [ordered]@{ selection = (Hash $C.selection); launcher = (Hash (Join-Path $C.install 'bell-tower.exe')); pending = (Test-Path $C.pending); restart = (Test-Path $C.restart) }
    $r = Invoke-Installer $C 'Preflight' $null $null
    if ($r.exitCode) { throw "Preflight failed: $($r.output)" }
    $after = [ordered]@{ selection = (Hash $C.selection); launcher = (Hash (Join-Path $C.install 'bell-tower.exe')); pending = (Test-Path $C.pending); restart = (Test-Path $C.restart) }
    if (($before | ConvertTo-Json) -ne ($after | ConvertTo-Json)) { throw 'Preflight mutated the installation.' }
    return ($r.output | ConvertFrom-Json).operationManifest
}
function Set-PresenceActivation($C, [string]$A) {
    $cur = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($C.presence, 4096)
    [BellTower.Distribution.DistributionJson]::Write($C.presence, [BellTower.Updates.TrayPresence]::new($cur.Process, $cur.Version, $cur.DataDirectory, $A, $cur.Capture))
}
function Get-Fingerprint($C, [string]$M) { [ordered]@{ selection = (Hash $C.selection); pending = (Hash $C.pending); restart = (Hash $C.restart); manifest = (Hash $M); exitRequest = (Test-Path (Join-Path $C.data 'exit.request')) } }
function Get-AsideStamp([string]$M) {
    $op = Split-Path -Parent $M
    (Get-ChildItem -LiteralPath $op -Directory -Filter 'dev-monitoring-aside-*' | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File } | Sort-Object FullName | ForEach-Object { $_.FullName + '=' + (Hash $_.FullName) + '@' + $_.LastWriteTimeUtc.Ticks }) -join '|'
}
function Assert-RolledBack($C, [string]$M, $R, [string]$OriginalSelection) {
    if ($R.exitCode) { return [ordered]@{ exitZero = $false; output = $R.output } }
    $s = Read-Manifest $M; $p = Wait-Presence $C $Original
    [ordered]@{ exitZero = $true; selectionOriginal = ((Hash $C.selection) -eq $OriginalSelection); trayOriginal = ($p.Version -eq $Original); trayActivationIsOperation = ($p.Activation -eq $s.coordinationId)
        recordsRemoved = (!(Test-Path $C.pending) -and !(Test-Path $C.restart)); noIntent = ($s.PSObject.Properties.Name -notcontains 'rollbackPointerTransition'); state = ($s.state -eq 'running-original') }
}
function Test-AllTrue($H) { @($H.Values | Where-Object { $_ -ne $true }).Count -eq 0 }
function Close-Case($C) {
    $r = [ordered]@{ trayExitRequested = $false; trayEnded = $true; leftovers = @() }
    $p = Get-LivePresence $C
    if ($p) {
        try { $C.controller.RequestExit(); $r.trayExitRequested = $true } catch { }
        $d = [DateTime]::UtcNow.AddSeconds(20)
        while ([BellTower.IO.ProcessProbe]::Inspect($p.Process) -eq [BellTower.IO.ProcessLiveness]::Live -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 100 }
        $r.trayEnded = ([BellTower.IO.ProcessProbe]::Inspect($p.Process) -ne [BellTower.IO.ProcessLiveness]::Live)
    }
    Start-Sleep -Milliseconds 500
    $scan = { @(Get-CimInstance Win32_Process -Property ProcessId, ExecutablePath, CommandLine | Where-Object { ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($C.root, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($C.root, [StringComparison]::OrdinalIgnoreCase)) }) }
    foreach ($i in (& $scan)) { $r.leftovers += [ordered]@{ pid = $i.ProcessId; executable = $i.ExecutablePath }; try { Stop-Process -Id $i.ProcessId -Force } catch { } }
    Start-Sleep -Milliseconds 300
    $r.remainingAfterCleanup = (& $scan).Count
    return $r
}
function Drift-File([string]$Path, [bool]$Unknown) {
    if ($Unknown) { $n = [System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($Path)); $n['qaUnknown'] = [System.Text.Json.Nodes.JsonValue]::Create('drift'); [IO.File]::WriteAllText($Path, $n.ToJsonString()) }
    else { [IO.File]::AppendAllText($Path, "`n") }
}

$results = [Collections.Generic.List[object]]::new()
foreach ($name in $Cases) {
    $free = Free-GB
    if ($free -lt 1.8) { $results.Add([ordered]@{ case = $name; passed = $false; notPerformed = "free memory $free GB < 1.8 GB" }); Write-Host "$name not performed (memory $free)"; continue }
    $c = New-Case $name
    $rec = [ordered]@{ case = $name; root = $c.root; passed = $false; freeGBAtStart = $free }
    $t0 = [DateTime]::UtcNow
    try {
        $initial = Start-InitialTray $c
        $rec.initialTray = [ordered]@{ pid = $initial.Process.Pid; version = $initial.Version }
        $origSel = Hash $c.selection
        $manifest = Get-Preflight $c
        $rec.preflightReadOnly = $true
        switch -Regex ($name) {
            '^lifecycle$' {
                $sw = Invoke-Installer $c 'Switch' $manifest $null
                if ($sw.exitCode) { throw "Switch failed: $($sw.output)" }
                $cand = Wait-Presence $c $Candidate; $s = Read-Manifest $manifest
                $rec.switch = [ordered]@{ selected = ([BellTower.Distribution.Installation]::new($c.install)).Selection().Version; trayVersion = $cand.Version; candidateActivationBound = ($cand.Activation -eq $s.candidateActivation); previous = ([BellTower.Distribution.Installation]::new($c.install)).Selection().Previous }
                $rb = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.rollback = Assert-RolledBack $c $manifest $rb $origSel
                # Explicit Retire from the package zip. The restarted tray's own cleanup may retire the candidate first (observed, not asserted).
                $cp = Join-Path $c.install "versions/$Candidate"
                $rec.candidateRetainedAfterRollback = (Test-Path $cp)
                $stage = Join-Path $c.root 'retire-package'
                Expand-Archive -LiteralPath (Join-Path $dev "bell-tower-$RetireControl-win-x64.zip") -DestinationPath $stage
                Copy-Item -LiteralPath (Join-Path $stage "versions/$RetireControl") -Destination (Join-Path $c.install "versions/$RetireControl") -Recurse
                $ret = Invoke-Installer $c 'Retire' $null $null @('-Version', $RetireControl)
                $rec.retire = [ordered]@{ version = $RetireControl; exitCode = $ret.exitCode; output = $ret.output; payloadRemoved = !(Test-Path (Join-Path $c.install "versions/$RetireControl")); originalKept = (Test-Path (Join-Path $c.install "versions/$Original"))
                    selectionUnchanged = ((Hash $c.selection) -eq $origSel); launcherUnchanged = ((Hash (Join-Path $c.install 'bell-tower.exe')) -eq (Hash (Join-Path $source 'bell-tower.exe'))) }
                # Refusals: selected payload and an unmarked/unknown name must be refused.
                $refSel = Invoke-Installer $c 'Retire' $null $null @('-Version', $Original)
                $rec.retireSelectedRefused = [ordered]@{ refused = ($refSel.exitCode -ne 0); selectedStillThere = (Test-Path (Join-Path $c.install "versions/$Original")); output = $refSel.output }
                $rec.passed = ($rec.switch.selected -eq $Candidate -and $rec.switch.candidateActivationBound -and (Test-AllTrue $rec.rollback) -and $ret.exitCode -eq 0 -and $rec.retire.payloadRemoved -and $rec.retire.originalKept -and $rec.retire.selectionUnchanged -and $rec.retire.launcherUnchanged -and $rec.retireSelectedRefused.refused -and $rec.retireSelectedRefused.selectedStillThere)
            }
            '^(restored-monitoring-retry|pointer-intent-saved|pointer-written|pointer-phase-saved)$' {
                $sw = Invoke-Installer $c 'Switch' $manifest $null
                if ($sw.exitCode) { throw "Switch failed: $($sw.output)" }
                [void](Wait-Presence $c $Candidate)
                $devSel = Hash $c.selection
                $phase = switch ($name) { 'restored-monitoring-retry' { 'rollback-before-selection' } 'pointer-intent-saved' { 'rollback-pointer-intent-saved' } 'pointer-written' { 'rollback-pointer-written' } 'pointer-phase-saved' { 'rollback-pointer-phase-saved' } }
                $files = if ($phase -eq 'rollback-before-selection') { @() } else { @('rollback-before-selection.release', "hold-$phase") }
                $b = New-Barrier $c $files
                $run = Start-Installer $c 'Rollback' $manifest $b
                Wait-Barrier $run $b $phase
                Stop-ExactInstaller $run
                $s = Read-Manifest $manifest
                $rec.interrupted = [ordered]@{ phase = $phase; state = $s.state; intent = ($s.PSObject.Properties.Name -contains 'rollbackPointerTransition')
                    pointer = $(if ((Hash $c.selection) -eq $devSel) { 'dev' } elseif ((Hash $c.selection) -eq $origSel) { 'original' } else { 'other' }); candidateStopped = ($null -eq (Get-LivePresence $c)) }
                $aside = Get-AsideStamp $manifest
                $retry = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.retry = Assert-RolledBack $c $manifest $retry $origSel
                $rec.retry.devSnapshotNotSavedAgain = ((Get-AsideStamp $manifest) -eq $aside)
                $exp = switch ($name) { 'restored-monitoring-retry' { @('restored-monitoring-candidate-pointer', $false, 'dev') } 'pointer-intent-saved' { @('restored-monitoring-candidate-pointer', $true, 'dev') } 'pointer-written' { @('restored-monitoring-candidate-pointer', $true, 'original') } 'pointer-phase-saved' { @('restored-monitoring-snapshot', $false, 'original') } }
                $rec.expectedInterruptedState = $exp
                $rec.passed = ($rec.interrupted.state -eq $exp[0] -and $rec.interrupted.intent -eq $exp[1] -and $rec.interrupted.pointer -eq $exp[2] -and $rec.interrupted.candidateStopped -and (Test-AllTrue $rec.retry))
            }
            '^live-activation-before-receipt$' {
                $b = New-Barrier $c @('switch-before-selection.release', 'hold-candidate-presence-before-receipt')
                $run = Start-Installer $c 'Switch' $manifest $b
                Wait-Barrier $run $b 'candidate-presence-before-receipt'
                Stop-ExactInstaller $run
                $cand = Wait-Presence $c $Candidate; $s = Read-Manifest $manifest
                $rec.interrupted = [ordered]@{ candidateTrayReceiptAbsent = ($s.PSObject.Properties.Name -notcontains 'candidateTray'); receiptActivationMatchesLive = ($s.restartLauncher.activation -eq $cand.Activation); candidatePid = $cand.Process.Pid }
                $presenceBytes = [IO.File]::ReadAllBytes($c.presence); $manifestBytes = [IO.File]::ReadAllBytes($manifest)
                $neg = [ordered]@{}
                Set-PresenceActivation $c ([Guid]::NewGuid().ToString('N'))
                $fp = Get-Fingerprint $c $manifest
                $x = Invoke-Installer $c 'Rollback' $manifest $null
                $neg.liveActivation = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match 'live candidate tray activation differs'); unchanged = ((Get-Fingerprint $c $manifest | ConvertTo-Json) -eq ($fp | ConvertTo-Json)); trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($cand.Process) -eq [BellTower.IO.ProcessLiveness]::Live); output = $x.output }
                [IO.File]::WriteAllBytes($c.presence, $presenceBytes)
                foreach ($d in @('receiptActivation', 'receiptPid')) {
                    $e = [System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($manifest))
                    if ($d -eq 'receiptActivation') { $e['restartLauncher']['activation'] = [System.Text.Json.Nodes.JsonValue]::Create([Guid]::NewGuid().ToString('N')) }
                    else { $e['restartLauncher']['pid'] = [System.Text.Json.Nodes.JsonValue]::Create([int]$e['restartLauncher']['pid'].GetValue[int]() + 7) }
                    [IO.File]::WriteAllText($manifest, $e.ToJsonString())
                    $fp = Get-Fingerprint $c $manifest
                    $x = Invoke-Installer $c 'Rollback' $manifest $null
                    $msg = if ($d -eq 'receiptActivation') { 'receipt is bound to a different version or activation' } else { 'Candidate launcher exited or changed identity' }
                    $neg[$d] = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match [regex]::Escape($msg)); unchanged = ((Get-Fingerprint $c $manifest | ConvertTo-Json) -eq ($fp | ConvertTo-Json)); trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($cand.Process) -eq [BellTower.IO.ProcessLiveness]::Live); output = $x.output }
                    [IO.File]::WriteAllBytes($manifest, $manifestBytes)
                }
                $rec.negatives = $neg
                $retry = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.retry = Assert-RolledBack $c $manifest $retry $origSel
                $rec.passed = ($rec.interrupted.candidateTrayReceiptAbsent -and $rec.interrupted.receiptActivationMatchesLive -and @($neg.Values | Where-Object { !$_.refused -or !$_.message -or !$_.unchanged -or !$_.trayAlive }).Count -eq 0 -and (Test-AllTrue $rec.retry))
            }
            '^activation-before-stop$' {
                $sw = Invoke-Installer $c 'Switch' $manifest $null
                if ($sw.exitCode) { throw "Switch failed: $($sw.output)" }
                $cand = Wait-Presence $c $Candidate
                $presenceBytes = [IO.File]::ReadAllBytes($c.presence); $devSel = Hash $c.selection
                $b = New-Barrier $c @('rollback-before-selection.release', 'hold-candidate-before-stop')
                $run = Start-Installer $c 'Rollback' $manifest $b
                Wait-Barrier $run $b 'candidate-before-stop'
                Set-PresenceActivation $c ([Guid]::NewGuid().ToString('N'))
                Release-Barrier $b 'candidate-before-stop'
                $x = Complete-Installer $run; $s = Read-Manifest $manifest
                $rec.refusal = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match 'live candidate tray activation differs'); noExitRequest = !(Test-Path (Join-Path $c.data 'exit.request')); trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($cand.Process) -eq [BellTower.IO.ProcessLiveness]::Live); selectionUnchanged = ((Hash $c.selection) -eq $devSel); state = $s.state; output = $x.output }
                [IO.File]::WriteAllBytes($c.presence, $presenceBytes)
                $retry = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.retry = Assert-RolledBack $c $manifest $retry $origSel
                $rec.passed = ($rec.refusal.refused -and $rec.refusal.message -and $rec.refusal.noExitRequest -and $rec.refusal.trayAlive -and $rec.refusal.selectionUnchanged -and $rec.refusal.state -eq 'running-candidate' -and (Test-AllTrue $rec.retry))
            }
            '^postgate-switch-(restart|pending)$' {
                $kind = $Matches[1]; $target = if ($kind -eq 'restart') { $c.restart } else { $c.pending }
                $b = New-Barrier $c @('switch-before-selection.release', 'hold-switch-selection-before-gate')
                $run = Start-Installer $c 'Switch' $manifest $b
                Wait-Barrier $run $b 'switch-selection-before-gate'
                $gate = [BellTower.Distribution.InstallationGate]::new($c.install)
                try { Release-Barrier $b 'switch-selection-before-gate'; Start-Sleep -Milliseconds 1200; $exactBytes = [IO.File]::ReadAllBytes($target); Drift-File $target ($kind -eq 'pending'); $drifted = [IO.File]::ReadAllBytes($target) } finally { $gate.Dispose() }
                $x = Complete-Installer $run; $s = Read-Manifest $manifest; $orig = Get-LivePresence $c
                $rec.result = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match 'operation coordination record changed'); selectionUnchanged = ((Hash $c.selection) -eq $origSel); driftedKept = ([Linq.Enumerable]::SequenceEqual([byte[]]$drifted, [byte[]][IO.File]::ReadAllBytes($target)))
                    recordsRemain = ((Test-Path $c.pending) -and (Test-Path $c.restart)); state = $s.state; originalTrayStoppedByThisPoint = ($null -eq $orig); output = $x.output }
                $rec.passed = ($rec.result.refused -and $rec.result.message -and $rec.result.selectionUnchanged -and $rec.result.driftedKept -and $rec.result.recordsRemain -and $rec.result.state -eq 'ready-to-select')
                # Informational follow-up: undo only the drift, then Rollback of the same manifest recovers the original tray.
                [IO.File]::WriteAllBytes($target, $exactBytes)
                $fr = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.followUpRollback = [ordered]@{ exitCode = $fr.exitCode; output = $fr.output }
                if ($fr.exitCode -eq 0) { $rec.followUpRollback.check = Assert-RolledBack $c $manifest $fr $origSel }
            }
            '^rollback-never-selected$' {
                $b = New-Barrier $c @()
                $run = Start-Installer $c 'Switch' $manifest $b
                Wait-Barrier $run $b 'switch-before-selection'
                Stop-ExactInstaller $run
                $s = Read-Manifest $manifest
                $rec.interrupted = [ordered]@{ state = $s.state; selectionOriginal = ((Hash $c.selection) -eq $origSel); trayStopped = ($null -eq (Get-LivePresence $c)); recordsPresent = ((Test-Path $c.pending) -and (Test-Path $c.restart)) }
                $rb = Invoke-Installer $c 'Rollback' $manifest $null
                $s2 = Read-Manifest $manifest; $live = Get-LivePresence $c
                $rec.rollback = [ordered]@{ exitCode = $rb.exitCode; output = $rb.output; manifestState = $s2.state; originalTrayRunning = [bool]($live -and $live.Version -eq $Original); recordsRemoved = (!(Test-Path $c.pending) -and !(Test-Path $c.restart)); selectionOriginal = ((Hash $c.selection) -eq $origSel) }
                $rec.passed = ($rb.exitCode -eq 0 -and $rec.rollback.manifestState -eq 'running-original' -and $rec.rollback.originalTrayRunning -and $rec.rollback.recordsRemoved -and $rec.rollback.selectionOriginal)
            }
            '^marker-negatives$' {
                # Switch interrupted before the pointer write: both coordination records exist, original tray stopped, selection original.
                $b = New-Barrier $c @()
                $run = Start-Installer $c 'Switch' $manifest $b
                Wait-Barrier $run $b 'switch-before-selection'
                Stop-ExactInstaller $run
                $pBytes = [IO.File]::ReadAllBytes($c.pending); $rBytes = [IO.File]::ReadAllBytes($c.restart); $mBytes = [IO.File]::ReadAllBytes($manifest)
                $rec.ownerFields = @(([System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($c.pending)))['Owner'].AsObject() | ForEach-Object { $_.Key })
                $rec.recordFields = [ordered]@{ pending = @(([System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($c.pending))).AsObject() | ForEach-Object { $_.Key }); restart = @(([System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($c.restart))).AsObject() | ForEach-Object { $_.Key }) }
                $liveForeign = $PID
                $tampers = @(
                    @{ n = 'pending-token'; f = 'pending'; k = 'Token'; v = 'forged' }, @{ n = 'pending-id'; f = 'pending'; k = 'Id'; v = 'forged' },
                    @{ n = 'restart-token'; f = 'restart'; k = 'Token'; v = 'forged' }, @{ n = 'restart-id'; f = 'restart'; k = 'Id'; v = 'forged' },
                    @{ n = 'pending-unknown-field'; f = 'pending'; k = 'qaUnknown'; v = 'x' }, @{ n = 'restart-unknown-field'; f = 'restart'; k = 'qaUnknown'; v = 'x' },
                    @{ n = 'pending-trailing-byte'; f = 'pending'; bytes = $true }, @{ n = 'restart-trailing-byte'; f = 'restart'; bytes = $true },
                    @{ n = 'restart-missing'; f = 'restart'; delete = $true }, @{ n = 'pending-missing'; f = 'pending'; delete = $true },
                    @{ n = 'pending-version'; f = 'pending'; k = 'Target'; v = '9.9.9' }, @{ n = 'restart-original'; f = 'restart'; k = 'Original'; v = [Guid]::NewGuid().ToString('N') },
                    @{ n = 'pending-owner-live-foreign'; f = 'pending'; owner = $true }, @{ n = 'restart-owner-live-foreign'; f = 'restart'; owner = $true },
                    @{ n = 'pending-expired'; f = 'pending'; k = 'Created'; v = ([DateTimeOffset]::UtcNow.AddMinutes(-30).ToString('o')) }, @{ n = 'restart-expired'; f = 'restart'; k = 'Created'; v = ([DateTimeOffset]::UtcNow.AddMinutes(-30).ToString('o')) }
                )
                $rows = [Collections.Generic.List[object]]::new()
                foreach ($t in $tampers) {
                    $path = if ($t.f -eq 'pending') { $c.pending } else { $c.restart }
                    if ($t.delete) { Remove-Item -LiteralPath $path }
                    elseif ($t.bytes) { [IO.File]::AppendAllText($path, "`n") }
                    else {
                        $n = [System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($path))
                        if ($t.owner) { $ident = [BellTower.IO.ProcessProbe]::Identify($liveForeign); foreach ($ok in @($n['Owner'].AsObject() | ForEach-Object { $_.Key })) { if ($ok -ieq 'pid') { $n['Owner'][$ok] = [System.Text.Json.Nodes.JsonValue]::Create([int]$ident.Pid) } elseif ($ok -ieq 'startedUtcTicks') { $n['Owner'][$ok] = [System.Text.Json.Nodes.JsonValue]::Create([long]$ident.StartedUtcTicks) } } }
                        else { $n[$t.k] = [System.Text.Json.Nodes.JsonValue]::Create($t.v) }
                        [IO.File]::WriteAllText($path, $n.ToJsonString())
                    }
                    $tp = Hash $c.pending; $tr = Hash $c.restart; $ts = Hash $c.selection; $tm = Hash $manifest
                    $x = Invoke-Installer $c 'Rollback' $manifest $null
                    $rows.Add([ordered]@{ tamper = $t.n; refused = ($x.exitCode -ne 0); pendingUnchanged = ((Hash $c.pending) -eq $tp); restartUnchanged = ((Hash $c.restart) -eq $tr); selectionUnchanged = ((Hash $c.selection) -eq $ts); manifestUnchanged = ((Hash $manifest) -eq $tm); noTrayStarted = ($null -eq (Get-LivePresence $c)); message = (($x.output -replace '^.*?\| ', '') | Select-Object -First 1).Substring(0, [Math]::Min(220, $x.output.Length)) })
                    [IO.File]::WriteAllBytes($c.pending, $pBytes); [IO.File]::WriteAllBytes($c.restart, $rBytes)
                    if ((Hash $manifest) -ne (Hash $manifest) -or $rows[-1].refused -eq $false) { [IO.File]::WriteAllBytes($manifest, $mBytes); break }
                }
                $rec.tampers = $rows.ToArray()
                # Positive control: exact records recover.
                $rb = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.control = [ordered]@{ exitCode = $rb.exitCode; output = $rb.output; originalTrayRunning = [bool](Get-LivePresence $c) }
                $rec.passed = (@($rows | Where-Object { !$_.refused -or !$_.selectionUnchanged -or !$_.noTrayStarted }).Count -eq 0)
            }
            '^relative-root-process$' {
                # A foreign tray invocation with a relative data directory under the installation must make Switch refuse before any mutation.
                $helperData = Join-Path $c.root 'helper-data'; New-Item -ItemType Directory -Path $helperData | Out-Null
                [IO.File]::WriteAllText((Join-Path $helperData 'state.json'), '{"schemaVersion":1,"sessions":{}}')
                $exe = Join-Path $c.install "versions/$Original/bell-tower.exe"
                $s = [Diagnostics.ProcessStartInfo]::new($exe); $s.UseShellExecute = $false; $s.CreateNoWindow = $true; $s.WorkingDirectory = $c.root
                foreach ($a in @('tray', '--data-dir', 'helper-data', '--silent')) { [void]$s.ArgumentList.Add($a) }
                $helper = [Diagnostics.Process]::Start($s); Start-Sleep -Seconds 3
                $rec.helper = [ordered]@{ pid = $helper.Id; alive = !$helper.HasExited }
                try {
                    $fp = Get-Fingerprint $c $manifest
                    $x = Invoke-Installer $c 'Switch' $manifest $null
                    $rec.switch = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match 'could not be safely classified|relative|Refus'); unchanged = ((Get-Fingerprint $c $manifest | ConvertTo-Json) -eq ($fp | ConvertTo-Json)); originalTrayAlive = [bool](Get-LivePresence $c); helperAlive = !$helper.HasExited; output = $x.output }
                    $rec.passed = ($rec.switch.refused -and $rec.switch.unchanged -and $rec.switch.originalTrayAlive -and $rec.switch.helperAlive)
                } finally {
                    if (!$helper.HasExited) { $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($helper.Id)" -Property CommandLine; if ($cim.CommandLine -like '*helper-data*' -and $cim.CommandLine -like "*$($c.root)*" -or $cim.CommandLine -like '*helper-data*') { $helper.Kill(); [void]$helper.WaitForExit(10000) } }
                    $rec.helperEnded = $helper.HasExited
                }
            }
            '^postgate-rollback$' {
                $sw = Invoke-Installer $c 'Switch' $manifest $null
                if ($sw.exitCode) { throw "Switch failed: $($sw.output)" }
                [void](Wait-Presence $c $Candidate)
                $devSel = Hash $c.selection
                $origState = [IO.File]::ReadAllBytes((Join-Path $c.data 'state.json'))
                [IO.File]::WriteAllText((Join-Path $c.data 'state.json'), '{"schemaVersion":1,"sessions":{"dev":{}}}')
                $b = New-Barrier $c @('rollback-before-selection.release', 'hold-rollback-selection-before-gate')
                $run = Start-Installer $c 'Rollback' $manifest $b
                Wait-Barrier $run $b 'rollback-selection-before-gate'
                $gate = [BellTower.Distribution.InstallationGate]::new($c.install)
                try { Release-Barrier $b 'rollback-selection-before-gate'; Start-Sleep -Milliseconds 1200; $restartBytes = [IO.File]::ReadAllBytes($c.restart); Drift-File $c.restart $false } finally { $gate.Dispose() }
                $x = Complete-Installer $run; $s = Read-Manifest $manifest
                $rec.refusal = [ordered]@{ refused = ($x.exitCode -ne 0); message = ($x.output -match 'operation coordination record changed'); selectionStillDev = ((Hash $c.selection) -eq $devSel); monitoringRestored = ([Linq.Enumerable]::SequenceEqual([byte[]]$origState, [byte[]][IO.File]::ReadAllBytes((Join-Path $c.data 'state.json')))); state = $s.state; noIntent = ($s.PSObject.Properties.Name -notcontains 'rollbackPointerTransition'); output = $x.output }
                [IO.File]::WriteAllBytes($c.restart, $restartBytes)
                $mon = Get-AsideStamp $manifest
                $retry = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.retry = Assert-RolledBack $c $manifest $retry $origSel
                $rec.retry.monitoringNotRestoredAgain = ((Get-AsideStamp $manifest) -eq $mon)
                $rec.passed = ($rec.refusal.refused -and $rec.refusal.message -and $rec.refusal.selectionStillDev -and $rec.refusal.monitoringRestored -and $rec.refusal.state -eq 'restored-monitoring-candidate-pointer' -and $rec.refusal.noIntent -and (Test-AllTrue $rec.retry))
            }
            '^qa250-lockwait-drift$' {
                $sw = Invoke-Installer $c 'Switch' $manifest $null
                if ($sw.exitCode) { throw "Switch failed: $($sw.output)" }
                [void](Wait-Presence $c $Candidate)
                $devSel = Hash $c.selection
                $bak = Join-Path $c.data 'state.json.bak'
                [IO.File]::WriteAllText($bak, '{"schemaVersion":1,"sessions":{},"qa":"candidate-backup"}')
                $candBak = Hash $bak
                $origBak = Hash (Join-Path (Split-Path -Parent $manifest) 'before-monitoring/state.json.bak')
                $held = [BellTower.IO.DataLock]::new($c.paths, 'setup')
                try {
                    $run = Start-Installer $c 'Rollback' $manifest $null
                    $d = [DateTime]::UtcNow.AddSeconds(30)
                    while ((!(Test-Path $c.pending) -or !(Test-Path $c.restart) -or (Hash $c.pending) -eq $null) -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 20 }
                    $cp = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($c.presence, 4096)
                    $d = [DateTime]::UtcNow.AddSeconds(20)
                    while ([BellTower.IO.ProcessProbe]::Inspect($cp.Process) -eq [BellTower.IO.ProcessLiveness]::Live -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 20 }
                    $rec.trayStoppedBeforeDrift = ([BellTower.IO.ProcessProbe]::Inspect($cp.Process) -eq [BellTower.IO.ProcessLiveness]::Ended)
                    $restartOrig = [IO.File]::ReadAllBytes($c.restart); [IO.File]::AppendAllText($c.restart, "`n"); Start-Sleep -Milliseconds 300
                } finally { $held.Dispose() }
                $x = Complete-Installer $run; $s = Read-Manifest $manifest
                $rec.refusal = [ordered]@{ refused = ($x.exitCode -ne 0); mentionsDrift = ($x.output -match '(?i)changed bytes|drifted|changed'); selectionStillDev = ((Hash $c.selection) -eq $devSel); candidateBackupKept = ((Hash $bak) -eq $candBak); monitoringNotRestored = ((Hash $bak) -ne $origBak); state = $s.state; markerKept = (Test-Path $c.restart); output = $x.output }
                $rec.passed = ($rec.trayStoppedBeforeDrift -and $rec.refusal.refused -and $rec.refusal.selectionStillDev -and $rec.refusal.candidateBackupKept -and $rec.refusal.monitoringNotRestored -and $rec.refusal.markerKept)
                # Follow-up (informational): exact bytes back, then the same manifest retries.
                [IO.File]::WriteAllBytes($c.restart, $restartOrig)
                $retry = Invoke-Installer $c 'Rollback' $manifest $null
                $rec.followUpRetry = Assert-RolledBack $c $manifest $retry $origSel
            }
            '^qa251-nested-restart-failure$' {
                $b = New-Barrier $c @("fail-start-$Candidate", "switch-before-selection.release", "rollback-before-selection.release")
                $sw = Invoke-Installer $c 'Switch' $manifest $b
                $rec.switchFailed = [ordered]@{ exitCode = $sw.exitCode; refusedRestart = $sw.output.Contains("restart of version $Candidate refused") }
                Remove-Item (Join-Path $b "fail-start-$Candidate"); New-Item -ItemType File (Join-Path $b "fail-start-$Original") | Out-Null
                $rcv = Invoke-Installer $c 'Rollback' $manifest $b
                $s = Read-Manifest $manifest
                $rec.recoveryFailed = [ordered]@{ exitCode = $rcv.exitCode; refusedRestart = $rcv.output.Contains("restart of version $Original refused"); phase = $s.coordinationPhase; phaseTarget = $s.coordinationPhaseTarget; state = $s.state; selected = ([BellTower.Distribution.Installation]::new($c.install)).Selection().Version; recordsRemain = ((Test-Path $c.pending) -and (Test-Path $c.restart)) }
                Remove-Item (Join-Path $b "fail-start-$Original")
                $retry = Invoke-Installer $c 'Rollback' $manifest $b
                $rec.retry = Assert-RolledBack $c $manifest $retry $origSel
                $rec.passed = ($rec.switchFailed.exitCode -ne 0 -and $rec.recoveryFailed.exitCode -ne 0 -and $rec.recoveryFailed.refusedRestart -and $rec.recoveryFailed.phaseTarget -eq $Original -and (Test-AllTrue $rec.retry))
            }
        }
    } catch { $rec.error = $_.ToString() }
    finally { $rec.seconds = [Math]::Round(([DateTime]::UtcNow - $t0).TotalSeconds, 1); $rec.cleanup = Close-Case $c }
    $results.Add($rec)
    Write-Host ("{0}: passed={1} {2}" -f $name, $rec.passed, $rec.error)
}
$summary = [ordered]@{ generatedUtc = [DateTime]::UtcNow.ToString('o'); evidenceType = 'installed native, synthetic disposable installation and data'; package = $package; packageSha256 = (Hash $package); installer = (Hash $installer); cases = $results.ToArray()
    allPassed = (@($results | Where-Object { !$_.passed }).Count -eq 0); remainingProcesses = (@($results | ForEach-Object { $_.cleanup.remainingAfterCleanup }) | Measure-Object -Sum).Sum }
[IO.File]::WriteAllText($ResultFile, ($summary | ConvertTo-Json -Depth 12))
[pscustomobject]@{ allPassed = $summary.allPassed; remainingProcesses = $summary.remainingProcesses } | ConvertTo-Json -Compress
