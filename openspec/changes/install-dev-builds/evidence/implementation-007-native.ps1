#Requires -Version 7.4
<#
Implementation 007 native disposable probes for install-dev.ps1 (ANALYSIS-200/201). Every case uses its own installation,
data directory and private workspace under artifacts/, a copied stable launcher plus 0.1.9003 payload, and the marked
0.1.9001 dev package. Children write to files (never pipes) so a tray child cannot hold a pipe open. Interruptions end only
the exact installer process this script started (PID, start time and command line verified). Each case ends its own trays
and records every process it had to clean up.
#>
param(
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string[]]$Cases = @('lifecycle', 'restored-monitoring-retry', 'pointer-intent-saved', 'pointer-written', 'pointer-phase-saved',
        'live-activation-before-receipt', 'activation-before-stop', 'hooks-during-maintenance'),
    [string]$ResultFile = (Join-Path $PSScriptRoot 'implementation-007-native-results.json')
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64'
$package = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9001-win-x64.zip'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $Repo 'scripts/install-dev.ps1'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
$originalVersion = '0.1.9003'; $candidateVersion = '0.1.9001'
if (!(Test-Path $source) -or !(Test-Path $package)) { throw 'Required disposable native packages are unavailable.' }
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
function Hash([string]$Path) { if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() } return $null }

function New-Case([string]$Name) {
    $root = Join-Path $Repo ('artifacts/implementation-007-native-' + $Name + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
    $case = [ordered]@{ name = $Name; root = $root; install = (Join-Path $root 'install'); data = (Join-Path $root 'data'); workspace = (Join-Path $root 'private'); barriers = (Join-Path $root 'barriers'); logs = (Join-Path $root 'logs') }
    New-Item -ItemType Directory -Path $root, $case.install, $case.data, $case.barriers, $case.logs | Out-Null
    Get-ChildItem -LiteralPath $source | Copy-Item -Destination $case.install -Recurse -Force
    [IO.File]::WriteAllText((Join-Path $case.data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
    [IO.File]::WriteAllText((Join-Path $case.data 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')
    $case.paths = [BellTower.IO.DataPaths]::new($case.data)
    $case.controller = [BellTower.Tray.TrayController]::new($case.paths)
    $case.presence = [BellTower.Updates.UpdateTransaction]::Presence($case.paths)
    $case.selection = Join-Path $case.install 'current.json'
    $case.pending = Join-Path $case.install 'updates/pending.json'
    $case.restart = [BellTower.Updates.UpdateTransaction]::Marker($case.paths)
    return $case
}
function Start-InitialTray($Case) {
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $Case.install 'bell-tower.exe')); $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    foreach ($arg in @('tray', '--data-dir', $Case.data, '--silent')) { [void]$start.ArgumentList.Add($arg) }
    ([Diagnostics.Process]::Start($start)).Dispose()
    return (Wait-Presence $Case $originalVersion)
}
function Wait-Presence($Case, [string]$Version, [int]$Seconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $presence = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($Case.presence, 4096)
            if ($presence.Version -eq $Version -and [BellTower.IO.ProcessProbe]::Inspect($presence.Process) -eq [BellTower.IO.ProcessLiveness]::Live) { return $presence }
        } catch { }
        Start-Sleep -Milliseconds 50
    }
    throw "No live disposable $Version tray presence appeared in $($Case.name)."
}
function Get-LivePresence($Case) {
    if (!(Test-Path -LiteralPath $Case.presence)) { return $null }
    $presence = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($Case.presence, 4096)
    if ([BellTower.IO.ProcessProbe]::Inspect($presence.Process) -ne [BellTower.IO.ProcessLiveness]::Live) { return $null }
    return $presence
}
function Start-Installer($Case, [string]$Action, [string]$Manifest, [string]$Barrier, [string[]]$Extra = @()) {
    $id = $Action + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-Action', $Action, '-Package', $package,
        '-InstallationRoot', $Case.install, '-DataDirectory', $Case.data, '-Workspace', $Case.workspace, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild')
    if ($Manifest) { $arguments += @('-OperationManifest', $Manifest) }
    if ($Barrier) { $arguments += @('-TestBarrierDirectory', $Barrier) }
    $arguments += $Extra
    $out = Join-Path $Case.logs ($id + '.out'); $err = Join-Path $Case.logs ($id + '.err')
    $process = Start-Process -FilePath $pwsh -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    return [pscustomobject]@{ process = $process; identity = [BellTower.IO.ProcessProbe]::Identify($process.Id); out = $out; err = $err }
}
function Complete-Installer($Run) {
    if (!$Run.process.WaitForExit(180000)) { Stop-ExactInstaller $Run; throw 'Disposable installer exceeded three minutes.' }
    $text = ((Get-Content -LiteralPath $Run.out -Raw -ErrorAction SilentlyContinue) + ' ' + (Get-Content -LiteralPath $Run.err -Raw -ErrorAction SilentlyContinue))
    return [pscustomobject]@{ exitCode = $Run.process.ExitCode; output = ($text -replace '\x1b\[[0-9;]*m', '' -replace '\s*\r?\n\s*\|?\s*', ' ').Trim() }
}
function Invoke-Installer($Case, [string]$Action, [string]$Manifest, [string]$Barrier, [string[]]$Extra = @()) { return (Complete-Installer (Start-Installer $Case $Action $Manifest $Barrier $Extra)) }
function Stop-ExactInstaller($Run) {
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($Run.process.Id)" -Property ProcessId, CommandLine
    if (!$cim -or !$cim.CommandLine.Contains($installer, [StringComparison]::OrdinalIgnoreCase) -or [BellTower.IO.ProcessProbe]::Identify($Run.process.Id) -ne $Run.identity) {
        throw 'Refusing to interrupt a process that is not the exact disposable installer.'
    }
    $Run.process.Kill(); [void]$Run.process.WaitForExit(10000)
    if ([BellTower.IO.ProcessProbe]::Inspect($Run.identity) -ne [BellTower.IO.ProcessLiveness]::Ended) { throw 'Disposable installer did not end after exact interruption.' }
}
function Wait-Barrier($Run, [string]$Barrier, [string]$Phase) {
    $entered = Join-Path $Barrier ($Phase + '.entered'); $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while (!(Test-Path $entered) -and !$Run.process.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 30 }
    if (!(Test-Path $entered)) { $done = if ($Run.process.HasExited) { Complete-Installer $Run } else { Stop-ExactInstaller $Run; $null }; throw "Installer did not reach $Phase. $($done.output)" }
}
function New-Barrier($Case, [string[]]$Files) {
    $directory = Join-Path $Case.barriers ([Guid]::NewGuid().ToString('N').Substring(0, 8)); New-Item -ItemType Directory -Path $directory | Out-Null
    foreach ($file in $Files) { [IO.File]::WriteAllText((Join-Path $directory $file), 'probe') }
    return $directory
}
function Release-Barrier([string]$Barrier, [string]$Phase) { [IO.File]::WriteAllText((Join-Path $Barrier ($Phase + '.release')), 'probe') }
function Read-Manifest([string]$Path) { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) }
function Get-Preflight($Case) {
    $result = Invoke-Installer $Case 'Preflight' $null $null
    if ($result.exitCode) { throw "Preflight failed: $($result.output)" }
    return ($result.output | ConvertFrom-Json).operationManifest
}
function Set-PresenceActivation($Case, [string]$Activation) {
    $current = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($Case.presence, 4096)
    [BellTower.Distribution.DistributionJson]::Write($Case.presence, [BellTower.Updates.TrayPresence]::new($current.Process, $current.Version, $current.DataDirectory, $Activation, $current.Capture))
}
function Get-Fingerprint($Case, [string]$Manifest) {
    return [ordered]@{ selection = (Hash $Case.selection); pending = (Hash $Case.pending); restart = (Hash $Case.restart); manifest = (Hash $Manifest); exitRequest = (Test-Path (Join-Path $Case.data 'exit.request')) }
}
function Get-AsideStamp([string]$Manifest) {
    $operation = Split-Path -Parent $Manifest
    return ((Get-ChildItem -LiteralPath $operation -Directory -Filter 'dev-monitoring-aside-*' | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File } |
        Sort-Object FullName | ForEach-Object { $_.FullName + '=' + (Hash $_.FullName) + '@' + $_.LastWriteTimeUtc.Ticks }) -join '|')
}
function Assert-RolledBack($Case, [string]$Manifest, $Result, [string]$OriginalSelection) {
    if ($Result.exitCode) { throw "Rollback failed: $($Result.output)" }
    $saved = Read-Manifest $Manifest
    $presence = Wait-Presence $Case $originalVersion
    $checks = [ordered]@{
        exitZero = ($Result.exitCode -eq 0); selectionOriginal = ((Hash $Case.selection) -eq $OriginalSelection)
        trayOriginal = ($presence.Version -eq $originalVersion); trayActivationIsOperation = ($presence.Activation -eq $saved.coordinationId)
        launcherReceiptOriginal = ($saved.restartLauncher.version -eq $originalVersion); recordsRemoved = (!(Test-Path $Case.pending) -and !(Test-Path $Case.restart))
        noIntent = ($saved.PSObject.Properties.Name -notcontains 'rollbackPointerTransition'); state = ($saved.state -eq 'running-original')
    }
    return $checks
}
function Close-Case($Case) {
    # End this case's trays through its own disposable data directory, then report (and end) any process still under its root.
    $cleanup = [ordered]@{ trayExitRequested = $false; trayEnded = $true; leftovers = @() }
    $presence = Get-LivePresence $Case
    if ($presence -and $Case.controller.IsRunning()) {
        $Case.controller.RequestExit(); $cleanup.trayExitRequested = $true
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        while ([BellTower.IO.ProcessProbe]::Inspect($presence.Process) -eq [BellTower.IO.ProcessLiveness]::Live -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
        $cleanup.trayEnded = ([BellTower.IO.ProcessProbe]::Inspect($presence.Process) -ne [BellTower.IO.ProcessLiveness]::Live)
    }
    Start-Sleep -Milliseconds 500
    $leftovers = @(Get-CimInstance Win32_Process -Property ProcessId, ExecutablePath, CommandLine | Where-Object {
        ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($Case.root, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($Case.root, [StringComparison]::OrdinalIgnoreCase)) })
    foreach ($item in $leftovers) {
        $cleanup.leftovers += [ordered]@{ pid = $item.ProcessId; executable = $item.ExecutablePath }
        try { Stop-Process -Id $item.ProcessId -Force } catch { }
    }
    $cleanup.remainingAfterCleanup = @(Get-CimInstance Win32_Process -Property ProcessId, ExecutablePath, CommandLine | Where-Object {
        ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($Case.root, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($Case.root, [StringComparison]::OrdinalIgnoreCase)) }).Count
    return $cleanup
}

# ---- hook timing (generated legacy launcher and native ingress commands, file-redirected) ------------------------------------
function Start-Hook($Case, [string]$Kind, [string]$Provider, [string]$Session) {
    $launcher = Join-Path $Case.install 'bell-tower.exe'
    $selected = ([BellTower.Distribution.Installation]::new($Case.install)).Selection().Version
    $ingress = Join-Path $Case.install ("versions/$selected/bell-tower-ingress.exe")
    $builder = if ($Kind -eq 'native') { [BellTower.Setup.HookCommandBuilder]::new($launcher, $Case.paths, $ingress) } else { [BellTower.Setup.HookCommandBuilder]::new($launcher, $Case.paths) }
    $command = $builder.Build('event', $Provider)
    $id = "$Kind-$Provider-$Session"
    $stdin = Join-Path $Case.logs ($id + '.in'); $out = Join-Path $Case.logs ($id + '.out'); $err = Join-Path $Case.logs ($id + '.err')
    [IO.File]::WriteAllText($stdin, (@{ session_id = $Session; hook_event_name = 'Stop'; cwd = "D:/maintenance/$Provider" } | ConvertTo-Json -Compress))
    # Each command line is passed verbatim as one string, the way the provider shells receive the generated command.
    if ($Kind -eq 'legacy') { $file = 'powershell.exe'; $arguments = '-NoProfile -NonInteractive -EncodedCommand ' + $command.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)[-1] }
    elseif ($Provider -eq 'claude') { $file = 'C:\Program Files\Git\bin\bash.exe'; $arguments = '-c "' + $command.Replace('"', '\"') + '"' }
    else { $file = 'cmd.exe'; $arguments = '/d /s /c "' + $command + '"' }
    $started = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $file -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardInput $stdin -RedirectStandardOutput $out -RedirectStandardError $err
    return [pscustomobject]@{ id = $id; process = $process; watch = $started; out = $out; err = $err }
}
function Complete-Hook($Hook) {
    $exited = $Hook.process.WaitForExit(3000)
    $observed = $Hook.watch.Elapsed.TotalMilliseconds
    if (!$exited) { try { $Hook.process.Kill($true) } catch { } }
    # Whole-command time from process creation to exit; the observed time also includes this script's launch overhead.
    $whole = if ($exited) { ($Hook.process.ExitTime - $Hook.process.StartTime).TotalMilliseconds } else { $null }
    return [ordered]@{ hook = $Hook.id; exited = $exited; exitCode = $(if ($exited) { $Hook.process.ExitCode } else { $null }); milliseconds = $(if ($null -ne $whole) { [Math]::Round($whole, 1) } else { 3000 }); observedMilliseconds = [Math]::Round($observed, 1)
        stdout = (Get-Content -LiteralPath $Hook.out -Raw -ErrorAction SilentlyContinue); stderr = (Get-Content -LiteralPath $Hook.err -Raw -ErrorAction SilentlyContinue) }
}
function Invoke-HookMatrix($Case, [string]$Phase) {
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($kind in @('legacy', 'native')) {
        $rows.Add((Complete-Hook (Start-Hook $Case $kind 'claude' "$Phase-claude-one")))
        $rows.Add((Complete-Hook (Start-Hook $Case $kind 'codex' "$Phase-codex-one")))
        $pair = @((Start-Hook $Case $kind 'claude' "$Phase-claude-mixed"), (Start-Hook $Case $kind 'codex' "$Phase-codex-mixed"))
        foreach ($hook in $pair) { $rows.Add((Complete-Hook $hook)) }
    }
    return $rows.ToArray()
}

# ---- cases --------------------------------------------------------------------------------------------------------------------
$results = [Collections.Generic.List[object]]::new()
foreach ($name in $Cases) {
    $case = New-Case $name
    $record = [ordered]@{ case = $name; root = $case.root; passed = $false }
    $started = [DateTime]::UtcNow
    try {
        $initial = Start-InitialTray $case
        $record.initialTray = [ordered]@{ pid = $initial.Process.Pid; version = $initial.Version }
        $originalSelection = Hash $case.selection
        $manifest = Get-Preflight $case
        switch ($name) {
            'lifecycle' {
                $switch = Invoke-Installer $case 'Switch' $manifest $null
                if ($switch.exitCode) { throw "Switch failed: $($switch.output)" }
                $candidate = Wait-Presence $case $candidateVersion
                $saved = Read-Manifest $manifest
                $record.switch = [ordered]@{ selected = ([BellTower.Distribution.Installation]::new($case.install)).Selection().Version; trayVersion = $candidate.Version; candidateActivationBound = ($candidate.Activation -eq $saved.candidateActivation) }
                $rollback = Invoke-Installer $case 'Rollback' $manifest $null
                $record.rollback = Assert-RolledBack $case $manifest $rollback $originalSelection
                # The restarted tray's startup cleanup (same shared retention rule, next pass 15 minutes later) may retire the
                # candidate itself; wait for that pass, then explicitly Retire a separate idle marked dev payload.
                $candidatePayload = Join-Path $case.install "versions/$candidateVersion"
                $deadline = [DateTime]::UtcNow.AddSeconds(60)
                while ((Test-Path $candidatePayload) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
                $retireVersion = $candidateVersion
                $candidateRetiredByTrayCleanup = !(Test-Path $candidatePayload)
                if ($candidateRetiredByTrayCleanup) {
                    $retireVersion = '0.1.9002'
                    # Take the payload from the package archive: the unpacked build folder also holds files outside the
                    # contract (e.g. .pdb), which the shared owned-files rule correctly refuses to retire.
                    $staging = Join-Path $case.root 'retire-package'
                    Expand-Archive -LiteralPath (Join-Path $Repo "artifacts/dev-builds/bell-tower-$retireVersion-win-x64.zip") -DestinationPath $staging
                    Copy-Item -LiteralPath (Join-Path $staging "versions/$retireVersion") -Destination (Join-Path $case.install "versions/$retireVersion") -Recurse
                }
                $retire = Invoke-Installer $case 'Retire' $null $null @('-Version', $retireVersion)
                $record.retire = [ordered]@{ version = $retireVersion; candidateRetiredByTrayCleanup = $candidateRetiredByTrayCleanup; exitCode = $retire.exitCode; output = $retire.output
                    payloadRemoved = !(Test-Path (Join-Path $case.install "versions/$retireVersion")); originalKept = (Test-Path (Join-Path $case.install "versions/$originalVersion"))
                    selectionUnchanged = ((Hash $case.selection) -eq $originalSelection); launcherUnchanged = ((Hash (Join-Path $case.install 'bell-tower.exe')) -eq (Hash (Join-Path $source 'bell-tower.exe'))) }
                $record.passed = ($record.switch.selected -eq $candidateVersion -and $record.switch.candidateActivationBound -and @($record.rollback.Values | Where-Object { !$_ }).Count -eq 0 -and $retire.exitCode -eq 0 -and $record.retire.payloadRemoved -and $record.retire.originalKept -and $record.retire.selectionUnchanged -and $record.retire.launcherUnchanged)
            }
            { $_ -in @('restored-monitoring-retry', 'pointer-intent-saved', 'pointer-written', 'pointer-phase-saved') } {
                $switch = Invoke-Installer $case 'Switch' $manifest $null
                if ($switch.exitCode) { throw "Switch failed: $($switch.output)" }
                [void](Wait-Presence $case $candidateVersion)
                $devSelection = Hash $case.selection
                $phase = switch ($name) { 'restored-monitoring-retry' { 'rollback-before-selection' } 'pointer-intent-saved' { 'rollback-pointer-intent-saved' } 'pointer-written' { 'rollback-pointer-written' } 'pointer-phase-saved' { 'rollback-pointer-phase-saved' } }
                $files = if ($phase -eq 'rollback-before-selection') { @() } else { @('rollback-before-selection.release', "hold-$phase") }
                $barrier = New-Barrier $case $files
                $run = Start-Installer $case 'Rollback' $manifest $barrier
                Wait-Barrier $run $barrier $phase
                Stop-ExactInstaller $run
                $saved = Read-Manifest $manifest
                $record.interrupted = [ordered]@{ phase = $phase; state = $saved.state; intent = ($saved.PSObject.Properties.Name -contains 'rollbackPointerTransition')
                    pointer = $(if ((Hash $case.selection) -eq $devSelection) { 'dev' } elseif ((Hash $case.selection) -eq $originalSelection) { 'original' } else { 'other' }); candidateStopped = ($null -eq (Get-LivePresence $case)) }
                $aside = Get-AsideStamp $manifest
                $retry = Invoke-Installer $case 'Rollback' $manifest $null
                $record.retry = Assert-RolledBack $case $manifest $retry $originalSelection
                $record.retry.devSnapshotNotSavedAgain = ((Get-AsideStamp $manifest) -eq $aside)
                $expected = switch ($name) {
                    'restored-monitoring-retry' { @('restored-monitoring-candidate-pointer', $false, 'dev') }
                    'pointer-intent-saved' { @('restored-monitoring-candidate-pointer', $true, 'dev') }
                    'pointer-written' { @('restored-monitoring-candidate-pointer', $true, 'original') }
                    'pointer-phase-saved' { @('restored-monitoring-snapshot', $false, 'original') }
                }
                $record.passed = ($record.interrupted.state -eq $expected[0] -and $record.interrupted.intent -eq $expected[1] -and $record.interrupted.pointer -eq $expected[2] -and $record.interrupted.candidateStopped -and
                    @($record.retry.Values | Where-Object { !$_ }).Count -eq 0)
            }
            'live-activation-before-receipt' {
                $barrier = New-Barrier $case @('switch-before-selection.release', 'hold-candidate-presence-before-receipt')
                $run = Start-Installer $case 'Switch' $manifest $barrier
                Wait-Barrier $run $barrier 'candidate-presence-before-receipt'
                Stop-ExactInstaller $run
                $candidate = Wait-Presence $case $candidateVersion
                $saved = Read-Manifest $manifest
                $record.interrupted = [ordered]@{ candidateTrayReceipt = ($saved.PSObject.Properties.Name -contains 'candidateTray'); receiptActivationMatches = ($saved.restartLauncher.activation -eq $candidate.Activation); candidatePid = $candidate.Process.Pid }
                $presenceBytes = [IO.File]::ReadAllBytes($case.presence)
                $manifestBytes = [IO.File]::ReadAllBytes($manifest)
                $negatives = [ordered]@{}
                # 1. Only the live presence activation changes; receipt, PID/start, image and lineage stay valid.
                Set-PresenceActivation $case ([Guid]::NewGuid().ToString('N'))
                $before = Get-Fingerprint $case $manifest
                $refused = Invoke-Installer $case 'Rollback' $manifest $null
                $negatives.liveActivation = [ordered]@{ refused = ($refused.exitCode -ne 0); message = ($refused.output -match 'live candidate tray activation differs'); unchanged = ((Get-Fingerprint $case $manifest | ConvertTo-Json) -eq ($before | ConvertTo-Json)); trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($candidate.Process) -eq [BellTower.IO.ProcessLiveness]::Live); output = $refused.output }
                [IO.File]::WriteAllBytes($case.presence, $presenceBytes)
                # 2. Receipt activation and 3. receipt launcher PID drift, with the exact live activation.
                foreach ($drift in @('receiptActivation', 'receiptPid')) {
                    # JsonNode keeps every other value byte-for-byte (no date re-serialization).
                    $edited = [System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($manifest))
                    if ($drift -eq 'receiptActivation') { $edited['restartLauncher']['activation'] = [System.Text.Json.Nodes.JsonValue]::Create([Guid]::NewGuid().ToString('N')) }
                    else { $edited['restartLauncher']['pid'] = [System.Text.Json.Nodes.JsonValue]::Create([int]$edited['restartLauncher']['pid'].GetValue[int]() + 7) }
                    [IO.File]::WriteAllText($manifest, $edited.ToJsonString())
                    $before = Get-Fingerprint $case $manifest
                    $refused = Invoke-Installer $case 'Rollback' $manifest $null
                    $expectedMessage = if ($drift -eq 'receiptActivation') { 'receipt is bound to a different version or activation' } else { 'Candidate launcher exited or changed identity' }
                    $negatives[$drift] = [ordered]@{ refused = ($refused.exitCode -ne 0); message = ($refused.output -match [regex]::Escape($expectedMessage)); unchanged = ((Get-Fingerprint $case $manifest | ConvertTo-Json) -eq ($before | ConvertTo-Json)); trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($candidate.Process) -eq [BellTower.IO.ProcessLiveness]::Live); output = $refused.output }
                    [IO.File]::WriteAllBytes($manifest, $manifestBytes)
                }
                $record.negatives = $negatives
                # Positive control: exact activation and receipt recover through the same manifest.
                $retry = Invoke-Installer $case 'Rollback' $manifest $null
                $record.retry = Assert-RolledBack $case $manifest $retry $originalSelection
                $record.passed = (!$record.interrupted.candidateTrayReceipt -and $record.interrupted.receiptActivationMatches -and
                    @($negatives.Values | Where-Object { !$_.refused -or !$_.message -or !$_.unchanged -or !$_.trayAlive }).Count -eq 0 -and @($record.retry.Values | Where-Object { !$_ }).Count -eq 0)
            }
            'activation-before-stop' {
                $switch = Invoke-Installer $case 'Switch' $manifest $null
                if ($switch.exitCode) { throw "Switch failed: $($switch.output)" }
                $candidate = Wait-Presence $case $candidateVersion
                $presenceBytes = [IO.File]::ReadAllBytes($case.presence)
                $devSelection = Hash $case.selection
                $barrier = New-Barrier $case @('rollback-before-selection.release', 'hold-candidate-before-stop')
                $run = Start-Installer $case 'Rollback' $manifest $barrier
                Wait-Barrier $run $barrier 'candidate-before-stop'
                # The first ownership validation passed; change only the live activation before the stop is attempted.
                Set-PresenceActivation $case ([Guid]::NewGuid().ToString('N'))
                Release-Barrier $barrier 'candidate-before-stop'
                $refused = Complete-Installer $run
                $saved = Read-Manifest $manifest
                $record.refusal = [ordered]@{ refused = ($refused.exitCode -ne 0); message = ($refused.output -match 'live candidate tray activation differs'); noExitRequest = !(Test-Path (Join-Path $case.data 'exit.request'))
                    trayAlive = ([BellTower.IO.ProcessProbe]::Inspect($candidate.Process) -eq [BellTower.IO.ProcessLiveness]::Live); selectionUnchanged = ((Hash $case.selection) -eq $devSelection); state = $saved.state; output = $refused.output }
                [IO.File]::WriteAllBytes($case.presence, $presenceBytes)
                $retry = Invoke-Installer $case 'Rollback' $manifest $null
                $record.retry = Assert-RolledBack $case $manifest $retry $originalSelection
                $record.passed = ($record.refusal.refused -and $record.refusal.message -and $record.refusal.noExitRequest -and $record.refusal.trayAlive -and $record.refusal.selectionUnchanged -and
                    $record.refusal.state -eq 'running-candidate' -and @($record.retry.Values | Where-Object { !$_ }).Count -eq 0)
            }
            'hooks-during-maintenance' {
                # Hooks run while the installer is held at each slow or pre-gate maintenance phase (all outside the launch gate).
                $matrix = [ordered]@{}
                $barrier = New-Barrier $case @('hold-switch-selection-before-gate')
                $run = Start-Installer $case 'Switch' $manifest $barrier
                foreach ($phase in @('switch-before-selection', 'switch-selection-before-gate')) {
                    Wait-Barrier $run $barrier $phase
                    $held = [Diagnostics.Stopwatch]::StartNew()
                    $matrix[$phase] = Invoke-HookMatrix $case $phase
                    while ($held.ElapsedMilliseconds -lt 3500) { Start-Sleep -Milliseconds 50 }
                    $matrix[$phase + '-heldMilliseconds'] = $held.ElapsedMilliseconds
                    if ($run.process.HasExited) { throw "Switch exited while held at $phase." }
                    Release-Barrier $barrier $phase
                }
                $switch = Complete-Installer $run
                if ($switch.exitCode) { throw "Switch failed: $($switch.output)" }
                [void](Wait-Presence $case $candidateVersion)
                $barrier = New-Barrier $case @('hold-rollback-selection-before-gate')
                $run = Start-Installer $case 'Rollback' $manifest $barrier
                foreach ($phase in @('rollback-before-selection', 'rollback-selection-before-gate')) {
                    Wait-Barrier $run $barrier $phase
                    $held = [Diagnostics.Stopwatch]::StartNew()
                    $matrix[$phase] = Invoke-HookMatrix $case $phase
                    while ($held.ElapsedMilliseconds -lt 3500) { Start-Sleep -Milliseconds 50 }
                    $matrix[$phase + '-heldMilliseconds'] = $held.ElapsedMilliseconds
                    if ($run.process.HasExited) { throw "Rollback exited while held at $phase." }
                    Release-Barrier $barrier $phase
                }
                $rollback = Complete-Installer $run
                $record.rollback = Assert-RolledBack $case $manifest $rollback $originalSelection
                $record.hooks = $matrix
                $rows = @($matrix.Values | Where-Object { $_ -is [array] } | ForEach-Object { $_ })
                $record.hookSummary = [ordered]@{ count = $rows.Count; allExitedZero = (@($rows | Where-Object { !$_.exited -or $_.exitCode -ne 0 }).Count -eq 0)
                    maxMilliseconds = ($rows | Measure-Object -Property milliseconds -Maximum).Maximum; anyStdout = (@($rows | Where-Object { $_.stdout }).Count); anyStderr = (@($rows | Where-Object { $_.stderr }).Count) }
                $record.passed = ($rows.Count -eq 32 -and $record.hookSummary.allExitedZero -and $record.hookSummary.maxMilliseconds -lt 3000 -and @($record.rollback.Values | Where-Object { !$_ }).Count -eq 0)
            }
        }
    } catch {
        $record.error = $_.ToString()
    } finally {
        $record.seconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 1)
        $record.cleanup = Close-Case $case
    }
    $results.Add($record)
    Write-Host ("{0}: passed={1} {2}" -f $name, $record.passed, $record.error)
}
$summary = [ordered]@{ generatedUtc = [DateTime]::UtcNow.ToString('o'); package = $package; source = $source; installer = (Hash $installer); cases = $results.ToArray()
    allPassed = (@($results | Where-Object { !$_.passed }).Count -eq 0); remainingProcesses = (@($results | ForEach-Object { $_.cleanup.remainingAfterCleanup }) | Measure-Object -Sum).Sum }
[IO.File]::WriteAllText($ResultFile, ($summary | ConvertTo-Json -Depth 12))
[pscustomobject]@{ allPassed = $summary.allPassed; remainingProcesses = $summary.remainingProcesses } | ConvertTo-Json -Compress
