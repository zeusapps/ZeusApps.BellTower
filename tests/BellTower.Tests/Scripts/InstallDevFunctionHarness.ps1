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
$script:stoppedTray = $null
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
    '^tray-classification-(ended|reused|unreadable-live|transient|churn)$' {
        # A process that exits during the drain is skipped when proven ended; a live one that cannot be classified yet is
        # re-scanned and refuses only when the same PID and start identity stays unclassifiable until the deadline.
        $case = $Matches[1]
        $script:trayDrainSeconds = 2
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
        $script:cimCalls = 0
        $script:cimCreated = $created
        function New-FakeProcess([DateTime]$When) { [pscustomobject]@{ ProcessId = $targetPid; CreationDate = $When; ExecutablePath = (Join-Path $versions '0.1.9001/bell-tower.exe'); CommandLine = '' } }
        function Get-CimInstance {
            $script:cimCalls++
            # transient: unclassifiable for three scans, then gone. churn: a different start identity on every scan, then gone.
            if ($case -eq 'transient' -and $script:cimCalls -gt 3) { return @() }
            if ($case -eq 'churn' -and $script:cimCalls -gt 4) { return @() }
            $when = if ($case -eq 'churn') { $script:cimCreated.AddTicks($script:cimCalls) } else { $script:cimCreated }
            return (New-FakeProcess $when)
        }
        $fake = New-FakeProcess $created
        try { Wait-InstallationTrayStartsDrained } catch { $errorText = $_.ToString() }
        $checks.pidProvenEndedMatchesCase = ((Test-TrayProcessEnded $fake) -eq ($case -in @('ended', 'reused')))
        $checks.refusedOnlyWhenStuck = (($null -ne $errorText) -eq ($case -eq 'unreadable-live'))
        $checks.rescannedTransient = ($case -notin @('transient', 'churn') -or $script:cimCalls -gt 3)
    }
    '^stopped-tray-(restarted|restart-fails)$' {
        # After a refusal that follows the tray stop (selection and monitoring untouched), the stopped tray is restarted through
        # Start-SelectedTray and the owned records are cleaned; a failed restart says plainly how to recover.
        $fails = $Matches[1] -eq 'restart-fails'
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'original-stopped'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) {
            $script:startedVersion = $Version
            if ($fails) { throw 'injected restart failure' }
            return [ordered]@{ pid = 5151; ticks = 9; version = $Version; executable = 'C:\fixture\bell-tower.exe'; activation = $script:coordinationPlan.Id; dataDirectory = $DataDirectory }
        }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('A Bell Tower process could not be safely classified.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $saved = if (Test-Path -LiteralPath $OperationManifest) { Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json } else { $null }
        $checks.restartedStoppedVersion = ($script:startedVersion -eq '0.1.8')
        $checks.errorKeepsPrimaryCause = ($errorText -like 'A Bell Tower process could not be safely classified.*')
        if ($fails) {
            $checks.saysTrayStopped = ($errorText -like '*Monitoring is stopped*' -and $errorText -like '*-Action Rollback -OperationManifest*')
            $checks.stateStaysStopped = ($manifest.state -eq 'original-stopped')
            # Nothing was launched: records are removed so Rollback needs no dead-operation recovery.
            $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
        } else {
            $checks.saysRestarted = ($errorText -like '*was restarted (pid 5151)*' -and $errorText -like '*can be retried*')
            $checks.stateRetryable = ($manifest.state -eq 'prepared' -and $manifest.originalTray.pid -eq 5151 -and $manifest.preflightTray.pid -eq 4242 -and $saved.state -eq 'prepared')
            $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
        }
    }
    '^stopped-tray-(still-running|foreign-owner)$' {
        # The tray did not exit, or another tray owns monitoring: nothing was launched, so the run's own records are removed
        # (after the exact byte and tuple check) and the explanatory note is kept (ANALYSIS-551).
        $foreign = $Matches[1] -eq 'foreign-owner'
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'original-stopped'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        $live = [ordered]@{ pid = $(if ($foreign) { 6262 } else { 4242 }); ticks = $(if ($foreign) { 11 } else { 7 }); version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        function Read-Owner { return $live }
        function Describe-Owner($Owner) { return 'pid ' + $Owner.pid }
        $script:startCalls = 0
        function Start-SelectedTray([string]$Version) { $script:startCalls++; throw 'must not launch' }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('Verified tray did not exit.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $checks.noLaunch = ($script:startCalls -eq 0)
        $checks.errorKeepsPrimaryCause = ($errorText -like 'Verified tray did not exit.*')
        $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
        $checks.stateUnchanged = ($manifest.state -eq 'original-stopped')
    }
    '^stopped-tray-launched-unverified$' {
        # The restart reached Process.Start but could not be verified: a tray may be running, so the records stay and the
        # message must not claim monitoring is stopped (ANALYSIS-552).
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'original-stopped'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) {
            $script:restartLaunched = $true
            $manifest | Add-Member -NotePropertyName restartLauncher -NotePropertyValue ([ordered]@{ pid = 1; ticks = 1; version = $Version; activation = $script:coordinationPlan.Id }) -Force
            throw 'injected refusal after launch'
        }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('A Bell Tower process could not be safely classified.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $checks.saysMayBeRunning = ($errorText -like '*A tray may be running unverified*' -and $errorText -notlike '*Monitoring is stopped*' -and $errorText -like '*-Action Rollback -OperationManifest*')
        $checks.recordsKept = ((Test-Path -LiteralPath $script:pendingPath) -and (Test-Path -LiteralPath $script:restartPath))
        $checks.stateStaysStopped = ($manifest.state -eq 'original-stopped')
    }
    '^stopped-tray-partial-snapshot$' {
        # A snapshot folder left by a failed Backup-Monitoring makes the restart non-retryable, and the outcome is terminal (ANALYSIS-553).
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:\fixture\bell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'original-stopped'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        New-Item -ItemType Directory -Path (Join-Path $manifest.operationDirectory 'before-monitoring') -Force | Out-Null
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) {
            return [ordered]@{ pid = 5151; ticks = 9; version = $Version; executable = 'C:\fixture\bell-tower.exe'; activation = $script:coordinationPlan.Id; dataDirectory = $DataDirectory }
        }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('Snapshot failed.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $saved = Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json
        $checks.notRetryable = ($manifest.state -eq 'original-stopped' -and $errorText -notlike '*can be retried*')
        $checks.terminalFlagSaved = ($manifest.trayRestartedAfterFailure -eq $true -and $saved.trayRestartedAfterFailure -eq $true)
        $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
    }
    '^stopped-tray-prepared-retryable$' {
        # ANALYSIS-600: an untouched manifest still in state prepared stays retryable after a verified restart; no terminal flag is saved.
        $Action = 'Switch'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $original = [ordered]@{ pid = 4242; ticks = 7; version = '0.1.8'; executable = 'C:ixtureell-tower.exe'; activation = $null; dataDirectory = $DataDirectory }
        $script:stoppedTray = $original
        $manifest = [pscustomobject]@{ state = 'prepared'; candidateVersion = '0.1.9001'; originalTray = $original; operationDirectory = (Join-Path $Root 'operation') }
        $script:manifest = $manifest
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) {
            return [ordered]@{ pid = 5151; ticks = 9; version = $Version; executable = 'C:ixtureell-tower.exe'; activation = $script:coordinationPlan.Id; dataDirectory = $DataDirectory }
        }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('A Bell Tower process could not be safely classified.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $saved = Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json
        $checks.stillRetryable = ($manifest.state -eq 'prepared' -and $saved.state -eq 'prepared' -and $errorText -like '*can be retried*')
        $checks.noTerminalFlag = ($manifest.PSObject.Properties.Name -notcontains 'trayRestartedAfterFailure' -and $saved.PSObject.Properties.Name -notcontains 'trayRestartedAfterFailure')
        $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
    }
    '^stopped-tray-leftover-launcher-property$' {
        # ANALYSIS-552 point 2: a launcher receipt left in the manifest by an earlier attempt must not keep this run's records
        # when this run failed before Process.Start; retention follows whether this call launched.
        $Action = 'Rollback'
        $candidate = [pscustomobject]@{ Version = '0.1.9001' }
        $stoppedCandidate = [ordered]@{ pid = 4343; ticks = 8; version = '0.1.9001'; executable = 'C:ixtureell-tower.exe'; activation = 'old-activation'; dataDirectory = $DataDirectory }
        $script:stoppedTray = $stoppedCandidate
        $manifest = [pscustomobject]@{ state = 'restored-monitoring-snapshot'; candidateVersion = '0.1.9001'; originalTray = $stoppedCandidate; operationDirectory = (Join-Path $Root 'operation')
            restartLauncher = [ordered]@{ pid = 1; ticks = 1; version = '0.1.9001'; activation = 'old-activation' } }
        $script:manifest = $manifest
        function Read-Owner { return $null }
        function Start-SelectedTray([string]$Version) { throw 'injected failure before launch' }
        try { Restore-StoppedTrayAfterFailure ([Management.Automation.ErrorRecord]::new([Exception]::new('Rollback refused.'), 'x', 'NotSpecified', $null)) } catch { $errorText = $_.ToString() }
        $checks.launchedFlagClear = (-not $script:restartLaunched)
        $checks.saysStopped = ($errorText -like '*Monitoring is stopped*' -and $errorText -notlike '*may be running unverified*')
        $checks.recordsRemoved = (!(Test-Path -LiteralPath $script:pendingPath) -and !(Test-Path -LiteralPath $script:restartPath))
    }
    '^recovery-contract-without-candidate-payload$' {
        # Switch computed its plan contract hash from the prepared payload; recovery must not need the installed candidate copy (ANALYSIS-552).
        $prepared = Join-Path $Root 'prepared'
        New-Item -ItemType Directory -Path $prepared -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $prepared 'release.json'), '{"prepared":true}')
        $manifest = [pscustomobject]@{ preparedPayload = $prepared; candidateVersion = '0.1.9001'; originalVersion = '0.1.8' }
        $script:manifest = $manifest
        $checks.candidatePayloadAbsent = !(Test-Path -LiteralPath (Join-Path $InstallationRoot 'versions/0.1.9001'))
        $checks.switchUsesPreparedPayload = ((Get-RecoveryExpectedContractHash 'Switch' '0.1.9001') -eq (Hash (Join-Path $prepared 'release.json')))
        $checks.unknownPhaseHasNoExpectation = ((Get-RecoveryExpectedContractHash '' '') -eq '')
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
