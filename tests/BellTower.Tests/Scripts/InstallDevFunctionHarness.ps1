#Requires -Version 7.4
<#
Test-only extracted-function harness for scripts/install-dev.ps1 (InstallDevScriptTests).
It defines the installer's top-level functions without running its program, then runs one named scenario in this
still-running host. Scenarios replace only the installer's acquisition seams (Open-*), Read-Owner, the tray controller
and Assert-OperationBoundary to inject faults. After the scenario it checks on this same thread that no data lock or
launch gate is still owned and that no record guard is open, writes result.json, and stays alive until the test has
also acquired every resource from its own process.
#>
param(
    [Parameter(Mandatory)][string]$Scenario,
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$AssemblyDirectory,
    [Parameter(Mandatory)][string]$Installer
)
$ErrorActionPreference = 'Stop'
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $AssemblyDirectory $name)) }
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($Installer, [ref]$null, [ref]$parseErrors)
if ($parseErrors) { throw ('Installer does not parse: ' + ($parseErrors -join '; ')) }
foreach ($definition in @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] })) {
    . ([scriptblock]::Create($definition.Extent.Text))
}

# ---- disposable fixture -------------------------------------------------------------------------------------------
$InstallationRoot = Join-Path $Root 'install'
$DataDirectory = Join-Path $Root 'data'
$Disposable = $false
$TestBarrierDirectory = $null
$NoTray = $false
New-Item -ItemType Directory -Path $InstallationRoot, $DataDirectory, (Join-Path $InstallationRoot 'updates') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $InstallationRoot 'bell-tower.exe'), 'launcher fixture')
[BellTower.Distribution.DistributionJson]::Write((Join-Path $InstallationRoot 'installation.json'), [BellTower.Distribution.InstallationManifest]::new())
[BellTower.Distribution.DistributionJson]::Write((Join-Path $InstallationRoot 'current.json'), [BellTower.Distribution.VersionSelection]::new('0.1.8', $null, $null))
$installation = [BellTower.Distribution.Installation]::new($InstallationRoot)
$paths = [BellTower.IO.DataPaths]::new($DataDirectory)
$script:pendingPath = Join-Path $InstallationRoot 'updates/pending.json'
$script:restartPath = [BellTower.Updates.UpdateTransaction]::Marker($paths)
$OperationManifest = Join-Path $Root 'operation.json'
$script:selectionMutated = $false
$script:monitoringMutationStarted = $false
$script:warnings = [Collections.Generic.List[string]]::new()
function Write-Warning([string]$Message) { $script:warnings.Add($Message) }

function New-FixtureRecords([TimeSpan]$Age = [TimeSpan]::Zero) {
    $script:coordinationPlan = [BellTower.Updates.UpdatePlan]::new([Guid]::NewGuid().ToString('N'), ('a' * 64), $InstallationRoot, $DataDirectory,
        '0.1.8', '0.1.9001', ('b' * 64), [DateTimeOffset]::UtcNow - $Age, [BellTower.IO.ProcessProbe]::Identify($PID), $false)
    $script:pendingHash = New-ExclusiveRecord $script:pendingPath $script:coordinationPlan
    $script:restartHash = New-ExclusiveRecord $script:restartPath $script:coordinationPlan
    $script:manifest = [pscustomobject]@{ state = 'restored-monitoring-candidate-pointer'; coordinationPhase = 'Rollback'; coordinationPhaseTarget = '0.1.8'; operationDirectory = (Join-Path $Root 'operation') }
    Save-CoordinationExpectation
}
function Get-LaunchGateName {
    $bytes = [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($InstallationRoot).ToUpperInvariant() + 'launch')
    return 'Local\BellTower-install-' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}
function Test-ThreadOwnsMutex([string]$Name) {
    # ReleaseMutex succeeds only for the owning thread; an absent object cannot be owned at all.
    $mutex = $null
    if (![Threading.Mutex]::TryOpenExisting($Name, [ref]$mutex)) { return $false }
    try {
        $mutex.ReleaseMutex()
        # It was owned: take that ownership back so the resource's own Dispose still balances it.
        [void]$mutex.WaitOne(0)
        return $true
    } catch [ApplicationException] { return $false }
    finally { $mutex.Dispose() }
}
function Test-RecordUnguarded([string]$Path) {
    try { $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $stream.Dispose(); return $true }
    catch [IO.IOException] { return $false }
}
function Get-HeldResources {
    $held = [Collections.Generic.List[string]]::new()
    foreach ($area in @('setup', 'state', 'presentation')) { if (Test-ThreadOwnsMutex $paths.LockName($area)) { $held.Add($area) } }
    if (Test-ThreadOwnsMutex (Get-LaunchGateName)) { $held.Add('launch-gate') }
    foreach ($path in @($script:pendingPath, $script:restartPath)) { if ((Test-Path -LiteralPath $path) -and !(Test-RecordUnguarded $path)) { $held.Add('guard:' + [IO.Path]::GetFileName($path)) } }
    return $held.ToArray()
}
function New-FailingDisposable($Inner, [string]$Label) {
    # Releases the real resource, then reports a disposal failure, so later disposals must still be attempted.
    $wrapper = [pscustomobject]@{ Inner = $Inner; Label = $Label }
    $wrapper | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $this.Inner.Dispose(); throw ('injected disposal failure for ' + $this.Label) }
    $wrapper | Add-Member -MemberType ScriptProperty -Name Length -Value { $this.Inner.Length }
    return $wrapper
}
$script:boundaryCalls = 0
$script:boundaryObservations = [Collections.Generic.List[object]]::new()
function Assert-OperationBoundary([string]$Boundary) {
    # Records what is held whenever renewal would run; the real renewal/drain is not needed for these scenarios.
    $script:boundaryCalls++
    $script:boundaryObservations.Add([ordered]@{ call = $script:boundaryCalls; held = @(Get-HeldResources) })
    if ($script:onBoundary) { & $script:onBoundary $script:boundaryCalls }
}
$script:onBoundary = $null

# ---- scenarios ----------------------------------------------------------------------------------------------------
$checks = [ordered]@{}
$errorText = $null
$result = [ordered]@{}
New-FixtureRecords
$manifest = $script:manifest
switch -Regex ($Scenario) {
    '^commit-lock-throws-(state|presentation)$' {
        $failArea = $Matches[1]
        function Open-DataLock([string]$Area) { if ($Area -eq $failArea) { throw ('injected acquisition failure for ' + $Area) }; return [BellTower.IO.DataLock]::TryAcquire($paths, $Area) }
        try { [void](Enter-MonitoringCommit 'test') } catch { $errorText = $_.ToString() }
    }
    '^commit-guard-throws$' {
        function Open-RecordGuard([string]$Path) { if ($Path -eq $script:restartPath) { throw 'injected guard open failure' }; return [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read) }
        try { [void](Enter-MonitoringCommit 'test') } catch { $errorText = $_.ToString() }
    }
    '^commit-validation-fails$' {
        [IO.File]::AppendAllText($script:restartPath, ' ')
        try { [void](Enter-MonitoringCommit 'test') } catch { $errorText = $_.ToString() }
    }
    '^commit-dispose-fails$' {
        # A complete bundle fails validation; the middle lock and the first guard fail to dispose.
        [IO.File]::AppendAllText($script:restartPath, ' ')
        function Open-DataLock([string]$Area) { $lock = [BellTower.IO.DataLock]::TryAcquire($paths, $Area); if ($Area -eq 'state') { return (New-FailingDisposable $lock $Area) }; return $lock }
        function Open-RecordGuard([string]$Path) { $guard = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read); if ($Path -eq $script:pendingPath) { return (New-FailingDisposable $guard 'pending guard') }; return $guard }
        try { [void](Enter-MonitoringCommit 'test') } catch { $errorText = $_.ToString() }
        $checks.cleanupFailuresReported = (@($script:warnings | Where-Object { $_ -like '*injected disposal failure for state*' }).Count -eq 1 -and @($script:warnings | Where-Object { $_ -like '*injected disposal failure for pending guard*' }).Count -eq 1)
    }
    '^commit-dispose-fails-only$' {
        # Stale records make a complete bundle retry; the only failure is releasing the middle lock, which surfaces.
        Remove-Item -LiteralPath $script:pendingPath, $script:restartPath
        New-FixtureRecords ([TimeSpan]::FromMinutes(9))
        $manifest = $script:manifest
        function Open-DataLock([string]$Area) { $lock = [BellTower.IO.DataLock]::TryAcquire($paths, $Area); if ($Area -eq 'state') { return (New-FailingDisposable $lock $Area) }; return $lock }
        try { [void](Enter-MonitoringCommit 'test') } catch { $errorText = $_.ToString() }
    }
    '^commit-busy-retry$' {
        $script:stateAttempts = 0
        function Open-DataLock([string]$Area) {
            if ($Area -eq 'state') { $script:stateAttempts++; if ($script:stateAttempts -eq 1) { return $null } }
            return [BellTower.IO.DataLock]::TryAcquire($paths, $Area)
        }
        $commit = Enter-MonitoringCommit 'test'
        $checks.bundleComplete = (@($commit.locks).Count -eq 3 -and @($commit.guards).Count -eq 2)
        $checks.heldWhileCommitted = ((@(Get-HeldResources) -join ',') -eq 'setup,state,presentation,guard:pending.json,guard:restart.json')
        Complete-ResourceScope (Close-MonitoringCommit $commit) $null
        $checks.retriedOnce = ($script:boundaryCalls -eq 2 -and $script:stateAttempts -eq 2)
    }
    '^commit-stale-retry$' {
        Remove-Item -LiteralPath $script:pendingPath, $script:restartPath
        New-FixtureRecords ([TimeSpan]::FromMinutes(9))
        $manifest = $script:manifest
        $script:onBoundary = { param($Call) if ($Call -eq 2) { Set-OwnedRecordsFresh } }
        $commit = Enter-MonitoringCommit 'test'
        Complete-ResourceScope (Close-MonitoringCommit $commit) $null
        $checks.renewedOnce = ($script:boundaryCalls -eq 2)
    }
    '^switch-snapshot-lock-throws$' {
        function Open-BlockingDataLock([string]$Area) { if ($Area -eq 'presentation') { throw 'injected acquisition failure for presentation' }; return [BellTower.IO.DataLock]::new($paths, $Area) }
        try { Save-SwitchMonitoringSnapshot } catch { $errorText = $_.ToString() }
    }
    '^switch-snapshot-backup-fails$' {
        # All three locks are held when the snapshot copy fails (its directory already exists).
        New-Item -ItemType Directory -Path (Join-Path $script:manifest.operationDirectory 'before-monitoring') -Force | Out-Null
        try { Save-SwitchMonitoringSnapshot } catch { $errorText = $_.ToString() }
    }
    '^pointer-stale-retry$' {
        Remove-Item -LiteralPath $script:pendingPath, $script:restartPath
        New-FixtureRecords ([TimeSpan]::FromMinutes(9))
        $manifest = $script:manifest
        $script:onBoundary = { param($Call) if ($Call -eq 2) { Set-OwnedRecordsFresh } }
        $script:steps = 0
        Invoke-GuardedPointerStep 'test' (Hash $installation.SelectionPath) 'Selection drift.' { $script:steps++; $script:heldInStep = (@(Get-HeldResources) -join ',') }
        $checks.stepRanOnce = ($script:steps -eq 1)
        $checks.renewedOutsideGate = ($script:boundaryCalls -eq 2)
        $checks.gateAndGuardsHeldInStep = ($script:heldInStep -eq 'launch-gate,guard:pending.json,guard:restart.json')
    }
    '^pointer-mutation-denied$' {
        $script:denied = [ordered]@{}
        $before = @{ pending = (Hash $script:pendingPath); restart = (Hash $script:restartPath) }
        Invoke-GuardedPointerStep 'test' (Hash $installation.SelectionPath) 'Selection drift.' {
            foreach ($path in @($script:pendingPath, $script:restartPath)) {
                $label = [IO.Path]::GetFileNameWithoutExtension($path)
                try { $s = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite); $s.Dispose(); $script:denied[$label + '-write'] = $false } catch { $script:denied[$label + '-write'] = $true }
                try { [IO.File]::Delete($path); $script:denied[$label + '-delete'] = $false } catch { $script:denied[$label + '-delete'] = $true }
                $temporary = $path + '.replace.tmp'
                [IO.File]::WriteAllText($temporary, '{}')
                try { [IO.File]::Move($temporary, $path, $true); $script:denied[$label + '-replace'] = $false } catch { $script:denied[$label + '-replace'] = $true }
                finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
            }
        }
        foreach ($entry in $script:denied.GetEnumerator()) { $checks[$entry.Key + 'Denied'] = $entry.Value }
        $checks.recordsUnchanged = ((Hash $script:pendingPath) -eq $before.pending -and (Hash $script:restartPath) -eq $before.restart)
    }
    '^pointer-drift-after-boundary$' {
        # Drift lands after the outside boundary (while the installer would be waiting for the launch gate).
        $selection = Hash $installation.SelectionPath
        $script:onBoundary = { param($Call) [IO.File]::AppendAllText($script:restartPath, ' ') }
        $script:steps = 0
        try { Invoke-GuardedPointerStep 'test' $selection 'Selection drift.' { $script:steps++ } } catch { $errorText = $_.ToString() }
        $checks.stepNotRun = ($script:steps -eq 0)
        $checks.selectionUnchanged = ((Hash $installation.SelectionPath) -eq $selection)
        $checks.driftPreserved = ([IO.File]::ReadAllText($script:restartPath).EndsWith(' '))
    }
    '^pointer-guard-throws$' {
        function Open-RecordGuard([string]$Path) { if ($Path -eq $script:restartPath) { throw 'injected guard open failure' }; return [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read) }
        $script:steps = 0
        try { Invoke-GuardedPointerStep 'test' (Hash $installation.SelectionPath) 'Selection drift.' { $script:steps++ } } catch { $errorText = $_.ToString() }
        $checks.stepNotRun = ($script:steps -eq 0)
    }
    '^pointer-dispose-fails$' {
        # The step fails; the launch gate and a guard then fail to dispose. The step error stays primary.
        function Open-LaunchGate { return (New-FailingDisposable ([BellTower.Distribution.InstallationGate]::new($InstallationRoot)) 'launch gate') }
        function Open-RecordGuard([string]$Path) { $guard = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read); if ($Path -eq $script:restartPath) { return (New-FailingDisposable $guard 'restart guard') }; return $guard }
        try { Invoke-GuardedPointerStep 'test' (Hash $installation.SelectionPath) 'Selection drift.' { throw 'injected pointer step failure' } } catch { $errorText = $_.ToString() }
        $checks.cleanupFailuresReported = (@($script:warnings | Where-Object { $_ -like '*launch gate*' }).Count -eq 1 -and @($script:warnings | Where-Object { $_ -like '*restart guard*' }).Count -eq 1)
    }
    '^candidate-(stop-activation-changes|stop-control)$' {
        $changes = $Matches[1] -eq 'stop-activation-changes'
        $owner = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.9001'; executable = 'C:\fixture\bell-tower.exe'; activation = 'expected-activation'; dataDirectory = $DataDirectory }
        $manifest = [pscustomobject]@{ candidateVersion = '0.1.9001'; candidateActivation = 'expected-activation'; candidateTray = [pscustomobject]$owner; coordinationId = 'later-rollback-id' }
        $script:readCount = 0
        function Read-Owner {
            $script:readCount++
            $live = [ordered]@{}
            foreach ($key in $owner.Keys) { $live[$key] = $owner[$key] }
            if ($changes -and $script:readCount -gt 1) { $live.activation = 'foreign-activation' }
            return $live
        }
        $script:exitRequests = 0
        $controller = [pscustomobject]@{}
        $controller | Add-Member -MemberType ScriptMethod -Name RequestExit -Value { $script:exitRequests++ }
        $first = Read-Owner
        Assert-OwnedCandidate $first
        try { Stop-VerifiedTray $first { param($Live) Assert-OwnedCandidate $Live } } catch { $errorText = $_.ToString() }
        $checks.ownerReReadBeforeSignal = ($script:readCount -eq 2)
        $checks.signalMatchesOwnership = ($script:exitRequests -eq $(if ($changes) { 0 } else { 1 }))
    }
    '^candidate-(live-activation|receipt-activation|data-directory|unrecorded-activation)$' {
        $case = $Matches[1]
        $owner = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.9001'; executable = 'C:\fixture\bell-tower.exe'; activation = 'expected-activation'; dataDirectory = $DataDirectory }
        $receipt = [pscustomobject]@{ pid = 4241; ticks = 6; version = '0.1.9001'; activation = 'expected-activation' }
        $manifest = [pscustomobject]@{ candidateVersion = '0.1.9001'; candidateActivation = 'expected-activation'; restartLauncher = $receipt; coordinationId = 'expected-activation' }
        switch ($case) {
            'live-activation' { $owner.activation = 'foreign-activation' }
            'receipt-activation' { $receipt.activation = 'foreign-activation' }
            'data-directory' { $owner.dataDirectory = (Join-Path $Root 'other-data') }
            'unrecorded-activation' { $manifest.PSObject.Properties.Remove('candidateActivation') }
        }
        try { Assert-OwnedCandidate $owner } catch { $errorText = $_.ToString() }
    }
    '^tray-classification-(ended|reused|unreadable-live)$' {
        # A process that exits during the drain is skipped only when proven ended; a live unclassifiable one still refuses.
        $case = $Matches[1]
        $self = [BellTower.IO.ProcessProbe]::Identify($PID)
        $versions = Join-Path $InstallationRoot 'versions'
        $created = [DateTime]::new($self.StartedUtcTicks, [DateTimeKind]::Utc)
        if ($case -eq 'reused') { $created = $created.AddHours(-1) }
        $targetPid = $PID
        if ($case -eq 'ended') {
            $child = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList '-NoProfile', '-Command', 'exit 0' -PassThru -WindowStyle Hidden
            $child.WaitForExit()
            $targetPid = $child.Id
            $child.Dispose()
        }
        $fake = [pscustomobject]@{ ProcessId = $targetPid; CreationDate = $created; ExecutablePath = (Join-Path $versions '0.1.9001/bell-tower.exe'); CommandLine = 'bell-tower.exe' }
        function Get-CimInstance { return $fake }
        $starts = $null
        try { $starts = @(Get-InstallationTrayStarts) } catch { $errorText = $_.ToString() }
        $checks.pidProvenEndedMatchesCase = ((Test-TrayProcessEnded $fake) -eq ($case -ne 'unreadable-live'))
        $checks.nothingClassified = ($null -eq $starts -or $starts.Count -eq 0)
        $checks.refusedOnlyForLiveProcess = (($null -ne $errorText) -eq ($case -eq 'unreadable-live'))
    }
    default { throw "Unknown scenario $Scenario." }
}

$held = @(Get-HeldResources)
$checks.nothingHeldOnThisThread = ($held.Count -eq 0)
$result.scenario = $Scenario
$result.error = $errorText
$result.held = $held
$result.warnings = $script:warnings.ToArray()
$result.boundaryObservations = $script:boundaryObservations.ToArray()
$result.checks = $checks
$result.launchGate = Get-LaunchGateName
$result.lockNames = [ordered]@{ setup = $paths.LockName('setup'); state = $paths.LockName('state'); presentation = $paths.LockName('presentation') }
$result.records = @($script:pendingPath, $script:restartPath)
[BellTower.IO.AtomicFile]::Write((Join-Path $Root 'result.json'), ($result | ConvertTo-Json -Depth 8), $false)
# Stay alive (so nothing is released by process exit) until the test has acquired every resource from its own process.
$deadline = [DateTime]::UtcNow.AddSeconds(60)
while (!(Test-Path -LiteralPath (Join-Path $Root 'exit.release')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
