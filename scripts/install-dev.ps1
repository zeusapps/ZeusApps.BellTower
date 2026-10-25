#Requires -Version 7.4
<#
.SYNOPSIS
Installs, rolls back and retires local Bell Tower development builds on a versioned installation.

.DESCRIPTION
Actions (see docs/development.md, "Development builds"):
  Preflight  Verify a dev package and its compatibility; write a private operation record. Changes nothing else.
  Switch     Snapshot selection and monitoring state, add the dev payload, stop only the verified tray, select the payload, restart the tray from the stable launcher.
  Rollback   Restore the recorded previous selection and the monitoring snapshot, restart the tray.
  Retire     Remove a dev payload that is neither selected, the recorded previous version, nor leased.

It never stops agent sessions, never changes the stable launcher or existing payload bytes, and never modifies provider configuration.
Defaults point at the real per-user installation and data directory: run Preflight there, but only Switch, Rollback or Retire with the maintainer's explicit authorization.
#>
param(
    [ValidateSet('Preflight', 'Switch', 'Rollback', 'Retire')][string]$Action = 'Preflight',
    # Dev package zip. Default: the newest bell-tower-*-win-x64.zip under artifacts/dev-builds/.
    [string]$Package,
    [string]$InstallationRoot = (Join-Path $env:LOCALAPPDATA 'Programs\ZeusApps.BellTower'),
    [string]$DataDirectory = (Join-Path $env:LOCALAPPDATA 'ZeusApps.BellTower'),
    # Private workspace for snapshots and operation records (created with a restricted ACL when absent). Default: artifacts/dev-builds/operations.
    [string]$Workspace,
    # Switch/Rollback: the operation.json that Preflight printed.
    [string]$OperationManifest,
    # Retire: the payload version to remove.
    [string]$Version,
    # Proceed although the package commit differs from the repository HEAD or sources are newer than the Release build.
    [switch]$AllowStaleBuild,
    # Directory holding BellTower.Distribution.dll, BellTower.Protocol.dll and bell-tower.dll. Default: the repository Release build.
    [string]$AssemblyDirectory,
    # Disposable test installations only (everything must live under the repository artifacts/ folder).
    [switch]$Disposable,
    [switch]$NoTray,
    [switch]$SkipProbe,
    # Deterministic slow-phase barrier for disposable-only hook delivery tests.
    [string]$TestBarrierDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$defaultAssemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$assemblyDirectory = if ($AssemblyDirectory) { $AssemblyDirectory } else { $defaultAssemblies }
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) {
    $path = Join-Path $assemblyDirectory $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing $path. Run 'dotnet build -c Release' first." }
    [void][Reflection.Assembly]::LoadFrom($path)
}

function Full([string]$Path) { [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path)) }
function Same([string]$Left, [string]$Right) { return (Full $Left).Equals((Full $Right), [StringComparison]::OrdinalIgnoreCase) }
function Within([string]$Parent, [string]$Child) { return (Full $Child).StartsWith((Full $Parent) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) }
function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save-Json([string]$Path, $Value) { [BellTower.IO.AtomicFile]::Write($Path, ($Value | ConvertTo-Json -Depth 30), $false) }
function Wait-Until([scriptblock]$Ready, [string]$Failure) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) { if (& $Ready) { return }; Start-Sleep -Milliseconds 100 }
    throw $Failure
}
function Wait-TestBarrier([string]$Phase) {
    if (!$TestBarrierDirectory) { return }
    [IO.File]::WriteAllText((Join-Path $TestBarrierDirectory ($Phase + '.entered')), [DateTime]::UtcNow.ToString('o'))
    Wait-Until { Test-Path -LiteralPath (Join-Path $TestBarrierDirectory ($Phase + '.release')) } "Timed out waiting for disposable test barrier '$Phase'."
}
function Wait-OptionalTestBarrier([string]$Phase) {
    if ($Disposable -and $TestBarrierDirectory -and (Test-Path -LiteralPath (Join-Path $TestBarrierDirectory ('hold-' + $Phase)))) { Wait-TestBarrier $Phase }
}
function Test-DevVersion([string]$Text) { return [BellTower.Distribution.ReleaseContract]::IsDevVersion($Text) }

function New-PrivateWorkspace([string]$Path) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')) {
        $identity = [Security.Principal.SecurityIdentifier]::new($sid)
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Assert-PrivateWorkspace {
    if (!(Test-Path -LiteralPath $Workspace -PathType Container)) { throw 'The private workspace does not exist.' }
    $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    $acl = Get-Acl -LiteralPath $Workspace
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and $rule.IdentityReference.Value -notin $allowed) {
            throw 'The workspace allows another identity; restrict its ACL to the current user, SYSTEM and Administrators before copying monitoring snapshots.'
        }
    }
}
function Read-Owner {
    $presencePath = [BellTower.Updates.UpdateTransaction]::Presence($paths)
    $mutexExists = $controller.IsRunning()
    if (!(Test-Path -LiteralPath $presencePath -PathType Leaf)) {
        if (!$script:trayReservation -and $mutexExists) { throw 'Tray-instance object exists without verifiable presence; owner is unknown.' }
        return $null
    }
    $presence = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presencePath, 4096)
    $liveness = [BellTower.IO.ProcessProbe]::Inspect($presence.Process)
    if ($liveness -eq [BellTower.IO.ProcessLiveness]::Unverified) { throw 'Tray presence process identity is live or inaccessible but cannot be verified.' }
    if ($liveness -eq [BellTower.IO.ProcessLiveness]::Ended) {
        # A stale record is harmless only when no tray object remains. During our reservation,
        # object existence is our own fence and the process drain checks for other starters.
        if (!$script:trayReservation -and $mutexExists) {
            throw 'Recorded tray identity ended or its PID was reused while a tray-instance object exists; owner is unknown.'
        }
        return $null
    }
    if (!$mutexExists -and !$script:trayReservation) { throw 'Tray presence is live but its tray-instance object is absent; owner is inconsistent.' }
    if (!(Same $presence.DataDirectory $DataDirectory)) { throw 'Tray presence belongs to a different data directory.' }
    if ([BellTower.IO.ProcessProbe]::Identify($presence.Process.Pid) -ne $presence.Process) { throw 'Tray presence ownership changed during inspection.' }
    $process = [Diagnostics.Process]::GetProcessById($presence.Process.Pid)
    try { $executable = Full $process.MainModule.FileName } finally { $process.Dispose() }
    $expected = Join-Path ($installation.Payload($presence.Version)) 'bell-tower.exe'
    if (!(Same $executable $expected)) { throw 'Tray executable does not match its declared installation/version.' }
    if ([BellTower.IO.ProcessProbe]::Identify($presence.Process.Pid) -ne $presence.Process) { throw 'Tray process exited or changed identity during image inspection.' }
    return [ordered]@{ pid = $presence.Process.Pid; ticks = $presence.Process.StartedUtcTicks; version = $presence.Version; executable = $executable; activation = $presence.Activation; dataDirectory = $presence.DataDirectory }
}
function Describe-Owner($Owner) {
    if ($null -eq $Owner) { return 'no tray running' }
    return "pid $($Owner.pid), started (UTC ticks) $($Owner.ticks), $($Owner.executable)"
}
function Assert-SameOwner($Expected, $Actual) {
    if ($null -eq $Expected -and $null -eq $Actual) { return }
    if ($null -eq $Expected -or $null -eq $Actual -or $Expected.pid -ne $Actual.pid -or $Expected.ticks -ne $Actual.ticks -or !(Same $Expected.executable $Actual.executable)) { throw ('Monitoring owner changed since preflight; stop and inspect. Expected: ' + (Describe-Owner $Expected) + '. Actual: ' + (Describe-Owner $Actual) + '.') }
}
function Stop-VerifiedTray($Expected, [scriptblock]$Revalidate) {
    # Re-read the owner immediately before any signal; the optional check repeats the caller's full ownership rule.
    $live = Read-Owner
    Assert-SameOwner $Expected $live
    if ($null -ne $Expected -and $Revalidate) { & $Revalidate $live }
    if ($null -ne $Expected -and !$NoTray) {
        $controller.RequestExit()
        Wait-Until { [BellTower.IO.ProcessProbe]::Identify([int]$Expected.pid) -ne [BellTower.Core.ProcessIdentity]::new([int]$Expected.pid, [long]$Expected.ticks) } 'Verified tray did not exit; no provider or runtime was signalled.'
    }
}
function New-ExclusiveRecord([string]$Path, $Value) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 20 -Compress))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    return (Hash $Path)
}
function ConvertTo-UpdatePlan($Value) {
    $owner = [BellTower.Core.ProcessIdentity]::new([int]$Value.Owner.Pid, [long]$Value.Owner.StartedUtcTicks)
    return [BellTower.Updates.UpdatePlan]::new([string]$Value.Id, [string]$Value.Token, [string]$Value.Root, [string]$Value.DataDirectory,
        [string]$Value.Original, [string]$Value.Target, [string]$Value.ContractHash, [DateTimeOffset]$Value.Created, $owner, [bool]$Value.HadTray)
}
function Get-PlanHash($Plan) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Plan | ConvertTo-Json -Depth 20 -Compress))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Assert-OwnedRecordPair {
    if (!$script:coordinationPlan -or !(Test-Path -LiteralPath $script:pendingPath -PathType Leaf) -or !(Test-Path -LiteralPath $script:restartPath -PathType Leaf)) {
        throw 'An operation coordination record is missing; preserving any remaining record.'
    }
    if ((Hash $script:pendingPath) -ne $script:pendingHash -or (Hash $script:restartPath) -ne $script:restartHash) {
        throw 'An operation coordination record changed bytes; preserving both records.'
    }
    foreach ($path in @($script:pendingPath, $script:restartPath)) {
        $current = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($path, 16384)
        if (!$current.Equals($script:coordinationPlan)) { throw 'An operation coordination record changed its owner or operation tuple; preserving both records.' }
    }
}
function Save-CoordinationExpectation {
    $script:manifest | Add-Member -NotePropertyName coordinationPlan -NotePropertyValue $script:coordinationPlan -Force
    $script:manifest | Add-Member -NotePropertyName coordinationPendingHash -NotePropertyValue $script:pendingHash -Force
    $script:manifest | Add-Member -NotePropertyName coordinationRestartHash -NotePropertyValue $script:restartHash -Force
    Save-Json $OperationManifest $script:manifest
}
function Replace-OwnedRecordPair($NewPlan, [string]$NewPhase = $script:manifest.coordinationPhase, [string]$NewPhaseTarget = $script:manifest.coordinationPhaseTarget) {
    Assert-OwnedRecordPair
    $newHash = Get-PlanHash $NewPlan
    $json = $NewPlan | ConvertTo-Json -Depth 20 -Compress
    $intent = [ordered]@{
        oldPlan = $script:coordinationPlan; oldPendingHash = $script:pendingHash; oldRestartHash = $script:restartHash
        newPlan = $NewPlan; newJson = $json; newPendingHash = $newHash; newRestartHash = $newHash
        oldPhase = [string]$script:manifest.coordinationPhase; oldPhaseTarget = [string]$script:manifest.coordinationPhaseTarget
        newPhase = $NewPhase; newPhaseTarget = $NewPhaseTarget
    }
    $script:manifest | Add-Member -NotePropertyName coordinationTransition -NotePropertyValue $intent -Force
    Save-Json $OperationManifest $script:manifest
    if ($NewPhase -eq 'Rollback') { Wait-OptionalTestBarrier 'transition-before-first-write' }
    $recordIndex = 0
    foreach ($path in @($script:pendingPath, $script:restartPath)) {
        $expectedHash = if ($path -eq $script:pendingPath) { $script:pendingHash } else { $script:restartHash }
        if ((Hash $path) -ne $expectedHash) { throw 'An operation coordination record drifted during renewal; preserving both records.' }
        $temporary = $path + '.' + $script:coordinationPlan.Id + '.tmp'
        [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $path, $true)
        $recordIndex++
        if ($recordIndex -eq 1 -and $NewPhase -eq 'Rollback') { Wait-OptionalTestBarrier 'transition-between-records' }
    }
    $script:coordinationPlan = $NewPlan
    $script:pendingHash = Hash $script:pendingPath
    $script:restartHash = Hash $script:restartPath
    if ($script:pendingHash -ne $newHash -or $script:restartHash -ne $newHash) { throw 'Renewed operation records failed byte verification; preserving them for recovery.' }
    if ($NewPhase -eq 'Rollback') { Wait-OptionalTestBarrier 'transition-pair-verified' }
    Move-PointerIntentPublication $intent.oldPlan $intent.oldPendingHash $intent.oldRestartHash
    $script:manifest.PSObject.Properties.Remove('coordinationTransition')
    $script:manifest | Add-Member -NotePropertyName coordinationPhase -NotePropertyValue $NewPhase -Force
    $script:manifest | Add-Member -NotePropertyName coordinationPhaseTarget -NotePropertyValue $NewPhaseTarget -Force
    Save-CoordinationExpectation
}
function Resolve-InterruptedRecordTransition($Pending, $Restart) {
    if ($script:manifest.PSObject.Properties.Name -notcontains 'coordinationTransition') { return $null }
    $intent = $script:manifest.coordinationTransition
    try {
        $oldPlan = ConvertTo-UpdatePlan $intent.oldPlan
        $newPlan = ConvertTo-UpdatePlan $intent.newPlan
        $oldPendingHash = [string]$intent.oldPendingHash
        $oldRestartHash = [string]$intent.oldRestartHash
        $newPendingHash = [string]$intent.newPendingHash
        $newRestartHash = [string]$intent.newRestartHash
        $newJson = [string]$intent.newJson
        $oldPhase = [string]$intent.oldPhase
        $oldPhaseTarget = [string]$intent.oldPhaseTarget
        $newPhase = [string]$intent.newPhase
        $newPhaseTarget = [string]$intent.newPhaseTarget
    } catch { throw 'The saved coordination transition is incomplete; preserving both records.' }
    $oldBound = $oldPlan.Target -eq $oldPhaseTarget -and $oldPhase -in @('Switch', 'Rollback') -and
        $script:manifest.coordinationPhase -eq $oldPhase -and $script:manifest.coordinationPhaseTarget -eq $oldPhaseTarget -and
        $script:manifest.coordinationPendingHash -eq $oldPendingHash -and $script:manifest.coordinationRestartHash -eq $oldRestartHash
    $jsonPlan = ConvertTo-UpdatePlan ($newJson | ConvertFrom-Json)
    $jsonHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.UTF8Encoding]::new($false).GetBytes($newJson))).ToLowerInvariant()
    $newBound = $newPlan.Target -eq $newPhaseTarget -and $newPhase -in @('Switch', 'Rollback') -and
        $oldPlan.Id -eq $newPlan.Id -and $oldPlan.Token -eq $newPlan.Token -and (Same $oldPlan.Root $newPlan.Root) -and (Same $oldPlan.DataDirectory $newPlan.DataDirectory) -and $oldPlan.Original -eq $newPlan.Original
    if (!$oldBound -or !$newBound -or !$newPlan.Equals($jsonPlan) -or $newPendingHash -ne $newRestartHash -or $newPendingHash -ne $jsonHash) { throw 'The saved coordination transition is not bound to its original and target phases; preserving both records.' }
    foreach ($plan in @($oldPlan, $newPlan)) {
        if ([BellTower.IO.ProcessProbe]::Inspect($plan.Owner) -ne [BellTower.IO.ProcessLiveness]::Ended) { throw 'A transition owner is live or unverifiable; preserving both records.' }
    }
    $actual = @(
        @{ plan = $Pending; hash = (Hash $script:pendingPath) },
        @{ plan = $Restart; hash = (Hash $script:restartPath) }
    )
    $oldPending = $actual[0].hash -eq $oldPendingHash -and $actual[0].plan.Equals($oldPlan)
    $oldRestart = $actual[1].hash -eq $oldRestartHash -and $actual[1].plan.Equals($oldPlan)
    $newPending = $actual[0].hash -eq $newPendingHash -and $actual[0].plan.Equals($newPlan)
    $newRestart = $actual[1].hash -eq $newRestartHash -and $actual[1].plan.Equals($newPlan)
    if (!(($oldPending -and $oldRestart) -or ($newPending -and $oldRestart) -or ($newPending -and $newRestart))) { throw 'The pending/restart pair is not one of the exact recorded transition publication states; preserving both records.' }
    $json = $newJson
    if (!$newPending) {
        $temporary = $script:pendingPath + '.' + $newPlan.Id + '.recover.tmp'
        [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $script:pendingPath, $true)
    }
    if (!$newRestart) {
        $temporary = $script:restartPath + '.' + $newPlan.Id + '.recover.tmp'
        [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $script:restartPath, $true)
    }
    if ((Hash $script:pendingPath) -ne $newPendingHash -or (Hash $script:restartPath) -ne $newRestartHash) { throw 'Reconciled coordination records differ from the saved transition bytes.' }
    $script:manifest.PSObject.Properties.Remove('coordinationTransition')
    $script:manifest | Add-Member -NotePropertyName coordinationPhase -NotePropertyValue $newPhase -Force
    $script:manifest | Add-Member -NotePropertyName coordinationPhaseTarget -NotePropertyValue $newPhaseTarget -Force
    $script:coordinationPlan = $newPlan
    $script:pendingHash = $newPendingHash
    $script:restartHash = $newRestartHash
    Move-PointerIntentPublication $oldPlan $oldPendingHash $oldRestartHash
    Save-CoordinationExpectation
    return [ordered]@{ plan = $newPlan; pendingHash = $newPendingHash; restartHash = $newRestartHash; phase = $newPhase }
}
function Set-OwnedRecordsFresh {
    $oldPlan = $script:coordinationPlan
    $newPlan = [BellTower.Updates.UpdatePlan]::new($oldPlan.Id, $oldPlan.Token, $oldPlan.Root, $oldPlan.DataDirectory,
        $oldPlan.Original, $oldPlan.Target, $oldPlan.ContractHash, [DateTimeOffset]::UtcNow, $oldPlan.Owner, $oldPlan.HadTray)
    Replace-OwnedRecordPair $newPlan
}
function Assert-OperationBoundary([string]$Boundary) {
    Assert-OwnedRecordPair
    Set-OwnedRecordsFresh
    Wait-InstallationTrayStartsDrained
    Assert-OwnedRecordPair
    $remaining = [DateTimeOffset]::UtcNow - $script:coordinationPlan.Created
    if ($remaining -gt [TimeSpan]::FromMinutes(8)) {
        Set-OwnedRecordsFresh
        Wait-InstallationTrayStartsDrained
        Assert-OwnedRecordPair
    }
}
# Acquisition seams: each returns one disposable resource. The extracted-function test harness replaces them to inject faults.
function Open-DataLock([string]$Area) { return [BellTower.IO.DataLock]::TryAcquire($paths, $Area) }
function Open-BlockingDataLock([string]$Area) { return [BellTower.IO.DataLock]::new($paths, $Area) }
function Open-RecordGuard([string]$Path) { return [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read) }
function Open-LaunchGate { return [BellTower.Distribution.InstallationGate]::new($InstallationRoot) }
function Close-Resources($Guards, $Locks) {
    # Attempts every disposal even after one fails: guards in reverse, then locks in reverse acquisition order.
    $failures = [Collections.Generic.List[object]]::new()
    foreach ($group in @(, @($Guards)) + @(, @($Locks))) {
        $items = @($group | Where-Object { $null -ne $_ })
        [array]::Reverse($items)
        foreach ($item in $items) { try { $item.Dispose() } catch { $failures.Add($_) } }
    }
    return , $failures.ToArray()
}
function Complete-ResourceScope($Failures, $Primary) {
    # The acquisition or validation error stays primary; cleanup failures are reported without replacing it.
    $Failures = @($Failures | Where-Object { $null -ne $_ })
    if ($null -ne $Primary) {
        foreach ($failure in $Failures) { Write-Warning ('Releasing an operation resource also failed: ' + $failure) }
        throw $Primary
    }
    if ($Failures.Count) {
        foreach ($failure in @($Failures | Select-Object -Skip 1)) { Write-Warning ('Releasing an operation resource also failed: ' + $failure) }
        throw $Failures[0]
    }
}
function Describe-OperationProgress {
    $state = if ($script:manifest) { [string]$script:manifest.state } else { '' }
    if ($script:selectionMutated) { return "the installation selection was changed (operation state $state); the operation records were kept for a same-manifest Rollback" }
    if ($script:monitoringMutationStarted) {
        if ($state -eq 'restored-monitoring-candidate-pointer') { return 'monitoring state was already restored from the original snapshot while the candidate selection is still active; the operation records were kept so a same-manifest Rollback finishes the pointer step' }
        return "monitoring restoration has started (operation state $state) while the candidate selection is still active; the operation records were kept for a same-manifest Rollback"
    }
    return 'selection and monitoring state were left intact'
}
function Close-MonitoringCommit($Commit) {
    if ($null -eq $Commit) { return , @() }
    return (Close-Resources -Guards $Commit.guards -Locks $Commit.locks)
}
function Enter-MonitoringCommit([string]$Boundary) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        # Renewal, CIM drain, and all retry delays happen before any data lock is held.
        Assert-OperationBoundary $Boundary
        $locks = [Collections.Generic.List[object]]::new()
        $guards = [Collections.Generic.List[object]]::new()
        $busy = $false
        $transferred = $false
        $primary = $null
        $failures = @()
        # Every acquisition is inside this ownership scope: only a complete validated bundle transfers to the caller.
        try {
            foreach ($area in @('setup', 'state', 'presentation')) {
                $lock = Open-DataLock $area
                if ($null -eq $lock) { $busy = $true; break }
                $locks.Add($lock)
            }
            if (!$busy) {
                foreach ($path in @($script:pendingPath, $script:restartPath)) { $guards.Add((Open-RecordGuard $path)) }
                foreach ($guard in $guards) { if ($guard.Length -gt 16384) { throw 'An operation coordination record exceeds the bounded metadata size.' } }
                Assert-OwnedRecordPair
                if (([DateTimeOffset]::UtcNow - $script:coordinationPlan.Created) -gt [TimeSpan]::FromMinutes(8)) { $busy = $true }
                else {
                    $transferred = $true
                    return [ordered]@{ locks = $locks.ToArray(); guards = $guards.ToArray() }
                }
            }
        } catch { $primary = $_ }
        finally { if (!$transferred) { $failures = Close-Resources -Guards $guards -Locks $locks } }
        Complete-ResourceScope $failures $primary
        # Busy or stale: everything was released above; delay and renew outside all locks.
        Start-Sleep -Milliseconds 100
    }
    throw ('Bell Tower state remained busy during rollback; ' + (Describe-OperationProgress) + '.')
}
function Invoke-GuardedPointerStep([string]$Boundary, [string]$ExpectedSelectionHash, [string]$SelectionDrift, [scriptblock]$Step) {
    # Renewal and the process drain stay outside the launch gate. After the potentially blocking gate wait, the exact
    # record bytes, tuples and freshness are revalidated under read-only sharing guards, which are held through the
    # short pointer step and its manifest publication. Stale records release the gate and guards before renewal.
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        Assert-OperationBoundary $Boundary
        Wait-OptionalTestBarrier ($Boundary + '-before-gate')
        $gateHeld = [Collections.Generic.List[object]]::new()
        $guards = [Collections.Generic.List[object]]::new()
        $primary = $null
        $failures = @()
        $done = $false
        try {
            $gateHeld.Add((Open-LaunchGate))
            foreach ($path in @($script:pendingPath, $script:restartPath)) { $guards.Add((Open-RecordGuard $path)) }
            foreach ($guard in $guards) { if ($guard.Length -gt 16384) { throw 'An operation coordination record exceeds the bounded metadata size.' } }
            Assert-OwnedRecordPair
            if (([DateTimeOffset]::UtcNow - $script:coordinationPlan.Created) -le [TimeSpan]::FromMinutes(8)) {
                $selectionHash = Hash $installation.SelectionPath
                if ($selectionHash -ne $ExpectedSelectionHash) { throw ($SelectionDrift + ' Expected sha256 ' + $ExpectedSelectionHash + ', actual ' + $selectionHash + '.') }
                & $Step
                $done = $true
            }
        } catch { $primary = $_ }
        finally { $failures = Close-Resources -Guards $guards -Locks $gateHeld }
        Complete-ResourceScope $failures $primary
        if ($done) { return }
    }
    throw ('Operation coordination records could not be kept fresh at the selection boundary; ' + (Describe-OperationProgress) + '.')
}
function Test-TrayProcessEnded($Item) {
    # Proof that the process CIM described has ended: the PID no longer exists, the process reports exit after a refresh,
    # or the PID now belongs to a process with a different start time (PID reuse). Any doubt means not proven.
    $process = $null
    try {
        try { $process = [Diagnostics.Process]::GetProcessById([int]$Item.ProcessId) }
        catch [ArgumentException] { return $true }
        $process.Refresh()
        if ($process.HasExited) { return $true }
        if ($null -eq $Item.CreationDate) { return $false }
        $identity = [BellTower.IO.ProcessProbe]::Identify([int]$Item.ProcessId)
        if ($null -eq $identity) { return $false }
        $cimTicks = ([DateTime]$Item.CreationDate).ToUniversalTime().Ticks
        return ([Math]::Abs([long]$identity.StartedUtcTicks - [long]$cimTicks) -gt 10)
    } catch { return $false }
    finally { if ($process) { $process.Dispose() } }
}
function Get-InstallationTrayStarts {
    # Keep command lines in memory only: they can contain private data and are never emitted.
    $items = @(Get-CimInstance -ClassName Win32_Process -Filter "Name='bell-tower.exe'" -Property ProcessId,CreationDate,ExecutablePath,CommandLine -OperationTimeoutSec 5 -ErrorAction Stop)
    $found = [Collections.Generic.List[object]]::new()
    foreach ($item in $items) {
        $liveProcess = $null
        try {
            try { $liveProcess = [Diagnostics.Process]::GetProcessById([int]$item.ProcessId) }
            catch [ArgumentException] { continue }
            try {
                if ($liveProcess.HasExited) { continue }
                $identity = [BellTower.IO.ProcessProbe]::Identify([int]$item.ProcessId)
                if ($null -eq $identity) { throw 'identity unavailable' }
                $liveExecutable = Full $liveProcess.MainModule.FileName
                if ($item.ExecutablePath -and !(Same $liveExecutable (Full $item.ExecutablePath))) { throw 'CIM and live image differ' }
                if (!(Within (Join-Path $InstallationRoot 'versions') $liveExecutable)) { continue }
                if ($null -eq $item.CreationDate) { throw 'creation time unavailable' }
                $cimTicks = ([DateTime]$item.CreationDate).ToUniversalTime().Ticks
                # CIM's microsecond timestamp differed by 9 ticks in a live comparison; retain only that 1-microsecond correlation bound.
                if ([Math]::Abs([long]$identity.StartedUtcTicks - [long]$cimTicks) -gt 10) { throw 'CIM start identity differs' }
                if (!$item.CommandLine) { throw 'command line unavailable' }
                $classification = [BellTower.IO.TrayInvocationCommand]::Classify([string]$item.CommandLine, $liveExecutable, $DataDirectory)
                if ($classification -ne [BellTower.IO.TrayInvocationKind]::ThisDataDirectory) { continue }
                if ([BellTower.IO.ProcessProbe]::Identify($identity.Pid) -ne $identity) { throw 'identity changed' }
                $liveProcess.Refresh()
                if ($liveProcess.HasExited -or !(Same (Full $liveProcess.MainModule.FileName) $liveExecutable)) { throw 'live image changed' }
                $found.Add([ordered]@{ pid = $identity.Pid; ticks = $identity.StartedUtcTicks })
            }
            finally { $liveProcess.Dispose() }
        } catch {
            # Processes legitimately exit during the drain. Skip one only when it is proven to have ended; refuse otherwise.
            if (Test-TrayProcessEnded $item) { continue }
            throw ('A Bell Tower process could not be safely classified (PID ' + [int]$item.ProcessId + '); refusing before selection mutation.')
        }
    }
    return $found.ToArray()
}
function Wait-InstallationTrayStartsDrained {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $starts = @(Get-InstallationTrayStarts)
        if ($starts.Count -eq 0) { return }
        foreach ($start in $starts) {
            if ($null -ne $script:expectedStoppedTray -and $start.pid -eq $script:expectedStoppedTray.pid -and $start.ticks -eq $script:expectedStoppedTray.ticks) { continue }
            # Under the named-object reservation, old payloads see createdNew=false and exit.
            if ([BellTower.IO.ProcessProbe]::Identify([int]$start.pid) -ne [BellTower.Core.ProcessIdentity]::new([int]$start.pid, [long]$start.ticks)) { continue }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $remaining = @(Get-InstallationTrayStarts)
    if ($remaining.Count) { throw ('A tray startup did not drain under the reservation; ' + (Describe-OperationProgress) + '. PIDs: ' + (($remaining | ForEach-Object { $_.pid }) -join ', ')) }
}
function Remove-OwnedCoordinationRecords {
    Assert-OwnedRecordPair
    Remove-Item -LiteralPath $script:pendingPath
    Remove-Item -LiteralPath $script:restartPath
}
function Start-SelectedTray([string]$Version) {
    if ($TestBarrierDirectory -and (Test-Path -LiteralPath (Join-Path $TestBarrierDirectory ('fail-start-' + $Version)))) {
        throw "Disposable test injection: restart of version $Version refused before launch."
    }
    $start = [Diagnostics.ProcessStartInfo]::new($installation.Launcher)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory = $InstallationRoot
    $start.Environment['BELLTOWER_UPDATE_ACTIVATION'] = $script:coordinationPlan.Id
    foreach ($argument in @('tray', '--data-dir', $DataDirectory)) { $start.ArgumentList.Add($argument) }
    if ($Disposable) { $start.ArgumentList.Add('--silent') }
    $child = [Diagnostics.Process]::Start($start)
    $identity = [BellTower.IO.ProcessProbe]::Identify($child.Id)
    if ($null -eq $identity) { throw 'Could not verify the operation-owned launcher process identity.' }
    $manifest | Add-Member -NotePropertyName restartLauncher -NotePropertyValue ([ordered]@{ pid = $identity.Pid; ticks = $identity.StartedUtcTicks; version = $Version; activation = $script:coordinationPlan.Id }) -Force
    Save-Json $OperationManifest $manifest
    $child.Dispose()
    Wait-Until { try { $owner = Read-Owner; return ($null -ne $owner -and $owner.version -eq $Version) } catch { return $false } } 'Selected tray did not confirm ownership; the selection and data were left intact. Inspect the operation record.'
    $owner = Read-Owner
    Assert-OwnedRestart $owner $Version
    return $owner
}
function Assert-OwnedRestart($Owner, [string]$Version) {
    if ($null -eq $Owner) { throw 'The operation-owned tray restart has no verifiable presence record.' }
    if ($Owner.version -ne $Version -or !(Same $Owner.dataDirectory $DataDirectory) -or $Owner.activation -ne $script:coordinationPlan.Id) {
        throw 'The restarted tray does not match the selected version, data directory and operation activation.'
    }
    if ($manifest.PSObject.Properties.Name -notcontains 'restartLauncher') { throw 'The operation-owned launcher identity was not recorded.' }
    $launcherIdentity = [BellTower.Core.ProcessIdentity]::new([int]$manifest.restartLauncher.pid, [long]$manifest.restartLauncher.ticks)
    if ([BellTower.IO.ProcessProbe]::Inspect($launcherIdentity) -ne [BellTower.IO.ProcessLiveness]::Live) { throw 'The operation-owned launcher exited or changed identity before restart verification.' }
    $parents = [BellTower.IO.ProcessProbe]::Ancestors($Owner.pid)
    # PowerShell surfaces C# ValueTuple members by their runtime names, Item1/Item2.
    $parentPids = @($parents | ForEach-Object { $_.Item1 })
    if ($launcherIdentity.Pid -notin $parentPids) { throw ('The restarted tray is not descended from this operation-owned launcher; launcher PID ' + $launcherIdentity.Pid + ', observed ancestor PIDs ' + ($parentPids -join ',') + '.') }
    if ([BellTower.IO.ProcessProbe]::Identify($Owner.pid) -ne [BellTower.Core.ProcessIdentity]::new([int]$Owner.pid, [long]$Owner.ticks)) { throw 'The restarted tray process identity changed during verification.' }
}
function Get-ExpectedCandidateActivation {
    # Switch records the activation it launches the candidate with; Rollback may later mint its own coordination ID.
    if ($manifest.PSObject.Properties.Name -contains 'candidateActivation' -and $manifest.candidateActivation) { return [string]$manifest.candidateActivation }
    if ($manifest.PSObject.Properties.Name -contains 'candidateTray' -and $manifest.candidateTray.activation) { return [string]$manifest.candidateTray.activation }
    throw 'The candidate activation was not recorded; the running tray cannot be proven operation-owned.'
}
function Assert-OwnedCandidate($Owner) {
    if ($null -eq $Owner) { return }
    if ($Owner.version -ne $manifest.candidateVersion) { throw 'A different tray now owns monitoring.' }
    if (!(Same $Owner.dataDirectory $DataDirectory)) { throw 'The running candidate tray belongs to a different data directory.' }
    # The live presence activation must be this operation's candidate activation; a valid lineage never compensates.
    $expectedActivation = Get-ExpectedCandidateActivation
    if ([string]$Owner.activation -ne $expectedActivation) { throw ('The live candidate tray activation differs from this operation (expected ' + $expectedActivation + ', live ' + $Owner.activation + '); refusing before any signal.') }
    if ($manifest.PSObject.Properties.Name -contains 'candidateTray') {
        if ([string]$manifest.candidateTray.activation -ne $expectedActivation) { throw 'The saved candidate tray receipt is bound to a different activation.' }
        Assert-SameOwner $manifest.candidateTray $Owner
        return
    }
    if ($manifest.PSObject.Properties.Name -notcontains 'restartLauncher') { throw 'Candidate launch ownership was not recorded.' }
    $receipt = $manifest.restartLauncher
    if ($receipt.version -ne $manifest.candidateVersion -or [string]$receipt.activation -ne $expectedActivation -or [string]$receipt.activation -ne [string]$Owner.activation) { throw 'Candidate launcher receipt is bound to a different version or activation.' }
    $expectedLauncher = [BellTower.Core.ProcessIdentity]::new([int]$receipt.pid, [long]$receipt.ticks)
    $launcher = [BellTower.IO.ProcessProbe]::Identify($expectedLauncher.Pid)
    if ($null -eq $launcher -or $launcher -ne $expectedLauncher) { throw 'Candidate launcher exited or changed identity.' }
    $launcherProcess = [Diagnostics.Process]::GetProcessById($expectedLauncher.Pid)
    try { if (!(Same (Full $launcherProcess.MainModule.FileName) $installation.Launcher)) { throw 'Candidate launcher image differs from the stable installation launcher.' } }
    finally { $launcherProcess.Dispose() }
    if ([BellTower.IO.ProcessProbe]::Identify($Owner.pid) -ne [BellTower.Core.ProcessIdentity]::new([int]$Owner.pid, [long]$Owner.ticks)) { throw 'Candidate tray exited or changed identity before lineage verification.' }
    $parents = [BellTower.IO.ProcessProbe]::Ancestors($Owner.pid)
    $parentPids = @($parents | ForEach-Object { $_.Item1 })
    if ($expectedLauncher.Pid -notin $parentPids) { throw 'Candidate tray is not a child of this operation launcher.' }
    if ([BellTower.IO.ProcessProbe]::Identify($expectedLauncher.Pid) -ne $expectedLauncher -or [BellTower.IO.ProcessProbe]::Identify($Owner.pid) -ne [BellTower.Core.ProcessIdentity]::new([int]$Owner.pid, [long]$Owner.ticks)) { throw 'Candidate launcher or tray changed identity during lineage verification.' }
}
function Backup-Monitoring([string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -ErrorAction Stop | Out-Null
    foreach ($name in @('state.json', 'state.json.bak')) {
        $source = Join-Path $DataDirectory $name
        if (Test-Path -LiteralPath $source -PathType Leaf) { Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $name) }
    }
    Copy-Item -LiteralPath $installation.SelectionPath -Destination (Join-Path $Destination 'current.json')
    return , @(Get-ChildItem -LiteralPath $Destination -File | ForEach-Object { [ordered]@{ name = $_.Name; sha256 = (Hash $_.FullName) } })
}
function Assert-Snapshot([string]$Directory, $Files) {
    if (!(Within $manifest.operationDirectory $Directory)) { throw 'Snapshot path is outside this operation.' }
    foreach ($file in $Files) {
        if ($file.name -notin @('state.json', 'state.json.bak', 'current.json') -or (Hash (Join-Path $Directory $file.name)) -ne $file.sha256) { throw 'Private monitoring snapshot differs from its recorded hash.' }
    }
    if ('state.json' -notin @($Files.name) -or 'current.json' -notin @($Files.name)) { throw 'Monitoring snapshot is incomplete.' }
}
function Restore-SnapshotFile([string]$Source, [string]$Target) {
    if (!(Same $Target (Join-Path $DataDirectory 'state.json')) -and !(Same $Target (Join-Path $DataDirectory 'state.json.bak')) -and !(Same $Target $installation.SelectionPath)) { throw 'Unexpected monitoring restore target.' }
    [BellTower.Distribution.Installation]::EnsurePlainPath($Target)
    $temporary = $Target + '.' + [Guid]::NewGuid().ToString('N') + '.devtmp'
    try {
        [IO.File]::Copy($Source, $temporary, $false)
        [IO.File]::Move($temporary, $Target, $true)
        if ((Hash $Source) -ne (Hash $Target)) { throw 'Restored monitoring bytes differ from the private snapshot.' }
    } finally { if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary } }
}
function Assert-OriginalBytes($Manifest) {
    $launcherHash = Hash $installation.Launcher
    if ($launcherHash -ne $Manifest.originalLauncherSha256) { throw ('The stable launcher changed. Expected sha256 ' + $Manifest.originalLauncherSha256 + ', actual ' + $launcherHash + '.') }
    foreach ($file in $Manifest.originalPayloadFiles) {
        $actual = Hash (Join-Path ($installation.Payload($Manifest.originalVersion)) $file.path)
        if ($actual -ne $file.sha256) { throw ('An original payload file changed; inspect privately. File: versions/' + $Manifest.originalVersion + '/' + $file.path.Replace('\', '/') + '. Expected sha256 ' + $file.sha256 + ', actual ' + $actual + '.') }
    }
}
function Save-SwitchMonitoringSnapshot {
    # Every lock acquisition is inside the cleanup scope, so a later acquisition failure still releases earlier locks.
    $locks = [Collections.Generic.List[object]]::new()
    $primary = $null
    $failures = @()
    try {
        foreach ($area in @('setup', 'state', 'presentation')) { $locks.Add((Open-BlockingDataLock $area)) }
        $before = Join-Path $manifest.operationDirectory 'before-monitoring'
        $snapshotFiles = Backup-Monitoring $before
        $manifest | Add-Member -NotePropertyName originalMonitoringFiles -NotePropertyValue $snapshotFiles -Force
        Save-Json $OperationManifest $manifest
        Assert-Snapshot $before $manifest.originalMonitoringFiles
        Assert-Compatible $candidate $before "the candidate $($candidate.Version)"
    } catch { $primary = $_ }
    finally { $failures = Close-Resources -Guards @() -Locks $locks }
    Complete-ResourceScope $failures $primary
}
function Get-SelectionFields([string]$Path) {
    $selection = [BellTower.Distribution.DistributionJson]::Read[BellTower.Distribution.VersionSelection]($Path, 16384)
    return [ordered]@{ version = [string]$selection.Version; previous = [string]$selection.Previous; activation = [string]$selection.Activation }
}
function Get-RollbackPointerBinding {
    # The exact source (proposed dev pointer) and destination (verified original snapshot) of the Rollback pointer step.
    if ($manifest.state -ne 'restored-monitoring-candidate-pointer') { throw ('Monitoring restoration is not durably complete (operation state ' + $manifest.state + '); the pointer step is refused.') }
    # Only the two small selection files are read here, so this is safe under the launch gate. Get-RollbackRoute
    # verifies the complete monitoring snapshot before any gate is taken.
    $before = Join-Path $manifest.operationDirectory 'before-monitoring'
    if (!(Within $manifest.operationDirectory $before)) { throw 'Snapshot path is outside this operation.' }
    $snapshotSelection = Join-Path $before 'current.json'
    $snapshotHash = Hash $snapshotSelection
    $recorded = @($manifest.originalMonitoringFiles | Where-Object { $_.name -eq 'current.json' })
    if ($recorded.Count -ne 1 -or $recorded[0].sha256 -ne $snapshotHash -or $snapshotHash -ne $manifest.originalSelectionSha256) { throw 'The original selection snapshot does not match the preflight selection bytes; rollback refused.' }
    $proposed = Join-Path $manifest.operationDirectory 'proposed-current.json'
    if ($manifest.PSObject.Properties.Name -notcontains 'devSelectionSha256' -or !(Test-Path -LiteralPath $proposed -PathType Leaf) -or (Hash $proposed) -ne $manifest.devSelectionSha256) { throw 'The recorded dev selection proposal differs from its hash; rollback refused.' }
    $source = Get-SelectionFields $proposed
    $destination = Get-SelectionFields $snapshotSelection
    if ($source.version -ne $manifest.candidateVersion -or $source.previous -ne $manifest.originalVersion -or $destination.version -ne $manifest.originalVersion) { throw 'The pointer selections are not bound to this operation; rollback refused.' }
    return [ordered]@{
        operationId = [string]$manifest.coordinationId; operationToken = [string]$manifest.coordinationToken
        installationRoot = $InstallationRoot; dataDirectory = $DataDirectory; phase = 'Rollback'; target = [string]$manifest.originalVersion
        sourceSelectionSha256 = [string]$manifest.devSelectionSha256; sourceSelection = $source
        destinationSelectionSha256 = [string]$manifest.originalSelectionSha256; destinationSelection = $destination
        snapshotSelectionSha256 = $snapshotHash
    }
}
function Assert-RollbackPointerIntent($Intent) {
    # Validates an outstanding write-ahead pointer intent against the committed manifest expectation, before any takeover.
    $binding = Get-RollbackPointerBinding
    try { $intentPlan = ConvertTo-UpdatePlan $Intent.plan; $committedPlan = ConvertTo-UpdatePlan $manifest.coordinationPlan }
    catch { throw 'The recorded pointer transition intent is incomplete; rollback refused and records preserved.' }
    $sameSelection = { param($Left, $Right) $null -ne $Left -and [string]$Left.version -eq $Right.version -and [string]$Left.previous -eq $Right.previous -and [string]$Left.activation -eq $Right.activation }
    $checks = [ordered]@{
        operation = ([string]$Intent.operationId -eq $binding.operationId -and [string]$Intent.operationToken -eq $binding.operationToken -and $intentPlan.Id -eq $binding.operationId -and $intentPlan.Token -eq $binding.operationToken)
        paths = ((Same ([string]$Intent.installationRoot) $InstallationRoot) -and (Same ([string]$Intent.dataDirectory) $DataDirectory))
        phase = ([string]$Intent.phase -eq 'Rollback' -and [string]$Intent.target -eq $binding.target -and $intentPlan.Target -eq $binding.target -and [string]$manifest.coordinationPhase -eq 'Rollback')
        source = ([string]$Intent.sourceSelectionSha256 -eq $binding.sourceSelectionSha256 -and (& $sameSelection $Intent.sourceSelection $binding.sourceSelection))
        destination = ([string]$Intent.destinationSelectionSha256 -eq $binding.destinationSelectionSha256 -and (& $sameSelection $Intent.destinationSelection $binding.destinationSelection))
        snapshot = ([string]$Intent.snapshotSelectionSha256 -eq $binding.snapshotSelectionSha256 -and $binding.snapshotSelectionSha256 -eq $binding.destinationSelectionSha256)
        publication = ($intentPlan.Equals($committedPlan) -and [string]$Intent.pendingHash -eq [string]$manifest.coordinationPendingHash -and [string]$Intent.restartHash -eq [string]$manifest.coordinationRestartHash)
        owner = ([BellTower.IO.ProcessProbe]::Inspect($intentPlan.Owner) -eq [BellTower.IO.ProcessLiveness]::Ended -or ($null -ne $script:coordinationPlan -and $intentPlan.Equals($script:coordinationPlan)))
    }
    $failed = @($checks.GetEnumerator() | Where-Object { !$_.Value } | ForEach-Object { $_.Key })
    if ($failed.Count) { throw ('The recorded pointer transition intent is not bound to this operation (failed=' + ($failed -join ',') + '); rollback refused and records preserved.') }
}
function Move-PointerIntentPublication($OldPlan, [string]$OldPendingHash, [string]$OldRestartHash) {
    # A validated pointer intent follows its own record renewal or takeover in the same manifest publication.
    if ($script:manifest.PSObject.Properties.Name -notcontains 'rollbackPointerTransition') { return }
    $intent = $script:manifest.rollbackPointerTransition
    if (!(ConvertTo-UpdatePlan $intent.plan).Equals((ConvertTo-UpdatePlan $OldPlan)) -or [string]$intent.pendingHash -ne $OldPendingHash -or [string]$intent.restartHash -ne $OldRestartHash) {
        throw 'The pointer transition intent is not bound to the committed coordination records; preserving them.'
    }
    $intent.plan = $script:coordinationPlan
    $intent.pendingHash = $script:pendingHash
    $intent.restartHash = $script:restartHash
}
function Save-OriginalPointerPhase {
    # One atomic manifest publication commits the original-pointer phase and retires the write-ahead intent.
    $script:manifest.PSObject.Properties.Remove('rollbackPointerTransition')
    $script:manifest.state = 'restored-monitoring-snapshot'
    Save-Json $OperationManifest $script:manifest
}
function Complete-RollbackPointer {
    # Shared by freshly restored monitoring and every retry that left the candidate pointer selected.
    Invoke-GuardedPointerStep 'rollback-selection' $manifest.devSelectionSha256 'The dev selection changed before rollback (current.json).' {
        $intent = Get-RollbackPointerBinding
        if ($manifest.PSObject.Properties.Name -contains 'rollbackPointerTransition') { Assert-RollbackPointerIntent $manifest.rollbackPointerTransition }
        $intent.plan = $script:coordinationPlan
        $intent.pendingHash = $script:pendingHash
        $intent.restartHash = $script:restartHash
        $manifest | Add-Member -NotePropertyName rollbackPointerTransition -NotePropertyValue $intent -Force
        Save-Json $OperationManifest $manifest
        Wait-OptionalTestBarrier 'rollback-pointer-intent-saved'
        $script:selectionMutated = $true
        Restore-SnapshotFile (Join-Path (Join-Path $manifest.operationDirectory 'before-monitoring') 'current.json') $installation.SelectionPath
        if ((Hash $installation.SelectionPath) -ne $manifest.originalSelectionSha256) { throw 'Restored selection differs from the original recorded bytes.' }
        Wait-OptionalTestBarrier 'rollback-pointer-written'
        Save-OriginalPointerPhase
    }
    Wait-OptionalTestBarrier 'rollback-pointer-phase-saved'
}
function Get-RollbackRoute {
    # Classifies the exact phase/selection/intent combination before any record takeover or mutation.
    $selectionHash = Hash $installation.SelectionPath
    $state = [string]$manifest.state
    $hasDev = $manifest.PSObject.Properties.Name -contains 'devSelectionSha256'
    if ($state -eq 'restored-monitoring-candidate-pointer') { Assert-Snapshot (Join-Path $manifest.operationDirectory 'before-monitoring') $manifest.originalMonitoringFiles }
    if ($manifest.PSObject.Properties.Name -contains 'rollbackPointerTransition') {
        $intent = $manifest.rollbackPointerTransition
        Assert-RollbackPointerIntent $intent
        if ($selectionHash -eq [string]$intent.sourceSelectionSha256) { return 'pointer-intent-source' }
        if ($selectionHash -eq [string]$intent.destinationSelectionSha256) { return 'pointer-intent-destination' }
        throw ('The selection matches neither side of the recorded pointer transition; rollback refused. Expected sha256 ' + $intent.sourceSelectionSha256 + ' or ' + $intent.destinationSelectionSha256 + ', actual ' + $selectionHash + '.')
    }
    if ($selectionHash -eq $manifest.originalSelectionSha256 -and $state -in @('prepared', 'original-stopped', 'ready-to-select')) { return 'never-selected' }
    if ($selectionHash -eq $manifest.originalSelectionSha256 -and $state -in @('restored-monitoring-snapshot', 'running-original')) { return 'original-retry' }
    if ($hasDev -and $selectionHash -eq $manifest.devSelectionSha256 -and $state -eq 'restored-monitoring-candidate-pointer') { return 'pointer-only' }
    if ($state -eq 'restored-monitoring-candidate-pointer') { throw ('Monitoring was restored but the selection is not the recorded dev pointer and no pointer transition was recorded; rollback refused. Expected sha256 ' + $manifest.devSelectionSha256 + ', actual ' + $selectionHash + '.') }
    if (!$hasDev -or $state -notin @('ready-to-select', 'selected-candidate', 'running-candidate', 'restoring-monitoring-snapshot')) { throw ('The operation state is ' + $state + ', not a switched dev selection; rollback refused.') }
    if ($selectionHash -ne $manifest.devSelectionSha256) { throw ('The dev selection changed since the switch (current.json); rollback refused. Expected sha256 ' + $manifest.devSelectionSha256 + ', actual ' + $selectionHash + '.') }
    return 'restore-monitoring'
}
function Test-PendingCaptures {
    $folder = Join-Path $DataDirectory 'captures-v1'
    return (Test-Path -LiteralPath (Join-Path $DataDirectory 'capture-protocol.json')) -or ((Test-Path -LiteralPath $folder) -and @(Get-ChildItem -LiteralPath $folder -File -Filter '*.json').Count -gt 0)
}
# Compatibility comes from the contract of the version that will read the data, never from fixed numbers.
function Assert-Compatible($Contract, [string]$Directory, [string]$Name) {
    [BellTower.Distribution.CaptureCompatibility]::Ensure($Directory, $Contract.Capture, $Contract.AttachmentHealth)
    if ($Contract.Capture -eq 0 -and (Test-PendingCaptures)) { throw "Fast capture is enabled or pending, and $Name cannot read it; this operation is not applicable." }
}

# ---- common setup ---------------------------------------------------------------------------------------------------------------------
$InstallationRoot = Full $InstallationRoot
$DataDirectory = Full $DataDirectory
if (!$Workspace) { $Workspace = Join-Path $repo 'artifacts/dev-builds/operations' }
$Workspace = Full $Workspace
$artifacts = Join-Path $repo 'artifacts'
if (($NoTray -or $SkipProbe) -and !$Disposable) { throw '-NoTray and -SkipProbe are only for disposable test installations; add -Disposable.' }
if ($Disposable) {
    $rootsToValidate = @($InstallationRoot, $DataDirectory, $Workspace)
    if ($TestBarrierDirectory) { $rootsToValidate += $TestBarrierDirectory }
    foreach ($root in $rootsToValidate) { if (!(Within $artifacts $root)) { throw "-Disposable requires every path to be under $artifacts." } }
}
elseif ($TestBarrierDirectory) { throw '-TestBarrierDirectory is only for disposable test installations; add -Disposable.' }
foreach ($root in @($InstallationRoot, $DataDirectory, $Workspace)) { [BellTower.Distribution.Installation]::EnsurePlainPath($root) }
if ((Same $InstallationRoot $DataDirectory) -or (Within $InstallationRoot $DataDirectory) -or (Within $DataDirectory $InstallationRoot) -or
    (Same $Workspace $DataDirectory) -or (Within $DataDirectory $Workspace) -or (Within $Workspace $DataDirectory) -or
    (Same $Workspace $InstallationRoot) -or (Within $InstallationRoot $Workspace) -or (Within $Workspace $InstallationRoot)) { throw 'Installation, data and the private workspace must be separate.' }
if (!(Test-Path -LiteralPath $Workspace)) { New-PrivateWorkspace $Workspace }
Assert-PrivateWorkspace
$installation = [BellTower.Distribution.Installation]::new($InstallationRoot)
$paths = [BellTower.IO.DataPaths]::new($DataDirectory)
$controller = [BellTower.Tray.TrayController]::new($paths)
$script:trayReservation = $null
$script:selectionMutated = $false
$script:coordinationPlan = $null
$script:pendingPath = Join-Path $InstallationRoot 'updates/pending.json'
$script:restartPath = [BellTower.Updates.UpdateTransaction]::Marker($paths)

# ---- Retire ---------------------------------------------------------------------------------------------------------------------------
if ($Action -eq 'Retire') {
    if (!$Version) { throw 'Retire needs -Version.' }
    [void][BellTower.Distribution.ReleaseContract]::Parse($Version)
    $target = Join-Path (Join-Path $InstallationRoot 'versions') $Version
    if (!(Test-Path -LiteralPath $target -PathType Container)) { throw "Payload $Version does not exist." }
    $files = @(Get-ChildItem -LiteralPath $target -File -Recurse -Force | ForEach-Object { [IO.Path]::GetRelativePath($target, $_.FullName).Replace('\', '/') })
    $selection = $installation.Selection()
    # One rule shared with the tray's automatic cleanup (selected, previous, recovery-retained, valid contract, no lease, only owned files, hashes);
    # Retire adds only the requirement that the payload carries the dev marker.
    $reason = [BellTower.Updates.UpdateTransaction]::Retire($installation, $Version)
    if ($reason) { throw "Refused: $reason" }
    $record = Join-Path $Workspace ('retire-' + $Version + '-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '.json')
    Save-Json $record ([ordered]@{ action = 'retire'; installationRoot = $InstallationRoot; version = $Version; selected = $selection.Version; previous = $selection.Previous; files = $files; at = [DateTime]::UtcNow.ToString('o') })
    [ordered]@{ status = 'retired'; version = $Version; selectedVersion = $installation.Selection().Version; record = $record } | ConvertTo-Json
    return
}
# ---- Preflight ------------------------------------------------------------------------------------------------------------------------
if ($Action -eq 'Preflight') {
    if (!$Package) {
        $newest = Get-ChildItem -LiteralPath (Join-Path $repo 'artifacts/dev-builds') -Filter 'bell-tower-*-win-x64.zip' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if (!$newest) { throw 'No dev package found under artifacts/dev-builds/. Run scripts/package.ps1 -Dev first.' }
        $Package = $newest.FullName
    }
    $Package = Full $Package
    $descriptorPath = $Package.Substring(0, $Package.Length - 4) + '.release.json'
    $offer = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.ReleaseOffer]($descriptorPath, 1048576)
    $offer.Validate()
    $candidate = $offer.Contract
    if ((Hash $Package) -ne $offer.ArchiveHash.ToLowerInvariant()) { throw 'The package differs from its descriptor hash.' }
    if (!(Test-DevVersion $candidate.Version) -or $null -eq $candidate.Build -or $candidate.Build.Channel -ne 'dev') { throw "Refused: $($candidate.Version) is not a marked dev build (reserved range PATCH >= 9000 plus build metadata). Build it with scripts/package.ps1 -Dev." }
    # Stale-build guard: this script runs the repository Release assemblies, so they must belong to the package's source.
    if (!$AllowStaleBuild) {
        $head = (git -C $repo rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or !$head.StartsWith($candidate.Build.Commit, [StringComparison]::OrdinalIgnoreCase)) { throw "Stale build: the package was built from $($candidate.Build.Commit), but the repository HEAD is $($head.Substring(0, 7)). Rebuild the package, or pass -AllowStaleBuild." }
        if (!$AssemblyDirectory) {
            $dll = (Get-Item -LiteralPath (Join-Path $assemblyDirectory 'bell-tower.dll')).LastWriteTimeUtc
            $newer = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -File -Include '*.cs', '*.csproj' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.LastWriteTimeUtc -gt $dll } | Select-Object -First 1
            if ($newer) { throw "Stale build: $($newer.FullName) is newer than the Release assemblies. Run 'dotnet build -c Release', or pass -AllowStaleBuild." }
        }
    }
    $selection = $installation.Selection()
    $OriginalVersion = $selection.Version
    $original = $installation.Contract($OriginalVersion)
    if ($candidate.Version -eq $OriginalVersion) { throw 'The candidate is already selected.' }
    if (Test-Path -LiteralPath (Join-Path $InstallationRoot ('versions/' + $candidate.Version))) { throw 'Candidate target already exists; do not overwrite it. Build a new dev number.' }
    if (!(Test-Path -LiteralPath $paths.State -PathType Leaf)) { throw 'Existing monitoring state is required (state.json).' }
    Assert-Compatible $candidate $DataDirectory "the candidate $($candidate.Version)"
    Assert-Compatible $original $DataDirectory "the current version $OriginalVersion"
    $operation = Join-Path $Workspace ('bell-tower-dev-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $operation | Out-Null
    $prepared = [BellTower.Updates.ReleasePackage]::Extract($Package, (Join-Path $operation 'prepared-package'), $offer)
    if (!$SkipProbe) { [BellTower.Updates.ReleasePackage]::Probe($prepared, $candidate.Version) }
    $originalPayload = $installation.Payload($OriginalVersion)
    $files = @(Get-ChildItem -LiteralPath $originalPayload -File -Recurse | ForEach-Object { [ordered]@{ path = [IO.Path]::GetRelativePath($originalPayload, $_.FullName); sha256 = (Hash $_.FullName) } })
    $manifest = [ordered]@{
        installationRoot = $InstallationRoot; dataDirectory = $DataDirectory; workspace = $Workspace; operationDirectory = $operation
        originalVersion = $OriginalVersion; candidateVersion = $candidate.Version; package = $Package; packageSha256 = $offer.ArchiveHash.ToLowerInvariant(); preparedPayload = $prepared
        originalSelectionSha256 = (Hash $installation.SelectionPath); originalLauncherSha256 = (Hash $installation.Launcher); originalPayloadFiles = $files
        originalTray = (Read-Owner); noTray = [bool]$NoTray; state = 'prepared'
    }
    $OperationManifest = Join-Path $operation 'operation.json'
    Save-Json $OperationManifest $manifest
    [ordered]@{
        status = 'preflight-passed'; operationManifest = $OperationManifest; originalVersion = $OriginalVersion; candidateVersion = $candidate.Version
        display = [BellTower.Distribution.VersionDisplay]::Format($candidate.Version, $candidate.Build); originalFiles = $files.Count
        trayRunning = ($null -ne $manifest.originalTray); mutation = 'none; only the private operation workspace was written'
    } | ConvertTo-Json
    return
}

# ---- Switch / Rollback ----------------------------------------------------------------------------------------------------------------
if (!$OperationManifest -or !(Within $Workspace $OperationManifest)) { throw 'Use the operation.json that Preflight wrote inside the private workspace (-OperationManifest).' }
$OperationManifest = Full $OperationManifest
$manifest = Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json
$script:manifest = $manifest
foreach ($pair in @(@($manifest.installationRoot, $InstallationRoot), @($manifest.dataDirectory, $DataDirectory), @($manifest.workspace, $Workspace))) { if (!(Same $pair[0] $pair[1])) { throw 'Operation paths differ from preflight.' } }
if (!(Within $Workspace $manifest.operationDirectory) -or !(Within $manifest.operationDirectory $manifest.preparedPayload)) { throw 'Operation record points outside the private workspace.' }
if (!(Test-Path -LiteralPath $manifest.package -PathType Leaf) -or (Hash $manifest.package) -ne $manifest.packageSha256) { throw 'The package differs from the reviewed hash.' }
if ([bool]$NoTray -ne [bool]$manifest.noTray) { throw 'Operation tray mode differs from preflight (-NoTray).' }
$offer = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.ReleaseOffer]($manifest.package.Substring(0, $manifest.package.Length - 4) + '.release.json', 1048576)
$candidate = $offer.Contract
if ($candidate.Version -ne $manifest.candidateVersion -or $offer.ArchiveHash.ToLowerInvariant() -ne $manifest.packageSha256) { throw 'Package descriptor differs from preflight.' }
[BellTower.Distribution.Installation]::EnsurePlainPath($manifest.preparedPayload)
Assert-OriginalBytes $manifest
$original = $installation.Contract($manifest.originalVersion)
$gate = [BellTower.Distribution.InstallationGate]::new($InstallationRoot, 'update')
$reservationReleased = $false
$script:monitoringMutationStarted = $false
try {
    # Serialize with update staging/activation and preserve any active or interrupted recovery plan.
    $recovering = $false
    $existingPlan = $null
    $recoveryPhase = $null
    $rollbackRoute = $null
    if ($Action -eq 'Rollback') {
        # Classify and validate any write-ahead pointer intent against the committed expectation before renewal or takeover.
        if ($manifest.PSObject.Properties.Name -contains 'rollbackPointerTransition' -and
            (!(Test-Path -LiteralPath $script:pendingPath -PathType Leaf) -or !(Test-Path -LiteralPath $script:restartPath -PathType Leaf))) {
            throw 'A pointer transition intent is outstanding but its coordination records are missing; rollback refused.'
        }
        $rollbackRoute = Get-RollbackRoute
        if ($rollbackRoute -in @('pointer-only', 'pointer-intent-source', 'pointer-intent-destination', 'restore-monitoring') -and
            ($rollbackRoute -ne 'restore-monitoring' -or $manifest.state -eq 'restoring-monitoring-snapshot')) {
            # Monitoring was at least partly restored; failure cleanup keeps the records for a same-manifest retry.
            $script:monitoringMutationStarted = $true
        }
    }
    if (Test-Path -LiteralPath $script:pendingPath) {
        if ($Action -ne 'Rollback' -or $manifest.PSObject.Properties.Name -notcontains 'coordinationId' -or
            $manifest.PSObject.Properties.Name -notcontains 'coordinationToken' -or !(Test-Path -LiteralPath $script:restartPath)) {
            [BellTower.Updates.UpdateTransaction]::EnsureNoCoordinator($installation)
            throw 'A pending operation is missing its matching restart record; preserving it.'
        }
        $existingPlan = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($script:pendingPath, 16384)
        $existingRestart = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($script:restartPath, 16384)
        $transition = Resolve-InterruptedRecordTransition $existingPlan $existingRestart
        if ($null -ne $transition) {
            $existingPlan = $transition.plan
            $existingRestart = $transition.plan
        }
        $recoveryPhase = if ($manifest.PSObject.Properties.Name -contains 'coordinationPhase') { [string]$manifest.coordinationPhase }
            elseif ($existingPlan.Target -eq $manifest.candidateVersion) { 'Switch' } elseif ($existingPlan.Target -eq $manifest.originalVersion) { 'Rollback' } else { '' }
        $phaseTarget = if ($recoveryPhase -eq 'Switch') { [string]$manifest.candidateVersion } elseif ($recoveryPhase -eq 'Rollback') { [string]$manifest.originalVersion } else { '' }
        $expectedTargetContract = if ($phaseTarget) { Hash (Join-Path ($installation.Payload($phaseTarget)) 'release.json') } else { '' }
        $expectedHadTray = ($null -ne $manifest.originalTray -and !$NoTray)
        $ownerState = [BellTower.IO.ProcessProbe]::Inspect($existingPlan.Owner)
        $hasRecordedPlan = $manifest.PSObject.Properties.Name -contains 'coordinationPlan'
        $contractMatches = [string]::Equals([string]$existingPlan.ContractHash, [string]$expectedTargetContract, [StringComparison]::OrdinalIgnoreCase)
        if (!$contractMatches -and !$hasRecordedPlan) { $contractMatches = [string]::IsNullOrEmpty([string]$existingPlan.ContractHash) }
        $tupleChecks = [ordered]@{
            id = ($existingPlan.Id -eq $manifest.coordinationId)
            token = ($existingPlan.Token -eq $manifest.coordinationToken)
            root = (Same $existingPlan.Root $InstallationRoot)
            data = (Same $existingPlan.DataDirectory $DataDirectory)
            original = ($existingPlan.Original -eq $manifest.originalVersion)
            target = ($existingPlan.Target -eq $phaseTarget)
            contract = $contractMatches
            hadTray = ($existingPlan.HadTray -eq $expectedHadTray)
            restart = $existingRestart.Equals($existingPlan)
            created = (([DateTimeOffset]$existingPlan.Created) -le [DateTimeOffset]::UtcNow)
        }
        $exactTuple = @($tupleChecks.Values | Where-Object { !$_ }).Count -eq 0
        if (!$exactTuple -or $ownerState -ne [BellTower.IO.ProcessLiveness]::Ended) { throw ('The complete pending/restart recovery tuple or ended-owner proof differs (failed=' + (@($tupleChecks.GetEnumerator() | Where-Object { !$_.Value } | ForEach-Object { $_.Key }) -join ',') + ', phase=' + $recoveryPhase + ', target=' + $phaseTarget + ', contract=' + $existingPlan.ContractHash + '/' + $expectedTargetContract + ', owner=' + $ownerState + '); preserving both records.') }
        $pendingHashNow = Hash $script:pendingPath
        $restartHashNow = Hash $script:restartPath
        if ($manifest.PSObject.Properties.Name -contains 'coordinationPlan') {
            $recordedPlan = ConvertTo-UpdatePlan $manifest.coordinationPlan
            if (!$recordedPlan.Equals($existingPlan) -or $manifest.coordinationPendingHash -ne $pendingHashNow -or $manifest.coordinationRestartHash -ne $restartHashNow) {
                throw 'A recovery marker changed bytes or differs from the complete manifest tuple; preserving both records.'
            }
        } else {
            $expectedProperties = @('Id','Token','Root','DataDirectory','Original','Target','ContractHash','Created','Owner','HadTray')
            foreach ($path in @($script:pendingPath, $script:restartPath)) {
                $rawPlan = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                if (@($rawPlan.PSObject.Properties.Name).Count -ne $expectedProperties.Count -or @($rawPlan.PSObject.Properties.Name | Where-Object { $_ -notin $expectedProperties }).Count) {
                    throw 'A legacy recovery marker contains unknown fields; preserving both records.'
                }
            }
            if ($pendingHashNow -ne $restartHashNow) { throw 'Legacy recovery marker bytes differ; preserving both records.' }
        }
        [BellTower.Updates.UpdateTransaction]::EnsureNoCoordinator($installation, $existingPlan)
        $recovering = $true
    } else {
        [BellTower.Updates.UpdateTransaction]::EnsureNoCoordinator($installation)
        if (Test-Path -LiteralPath $script:restartPath) { throw 'A restart coordination record already exists; inspect it before starting another operation.' }
    }
    $expectedAtStart = $manifest.originalTray
    if ($Action -eq 'Rollback') {
        $expectedAtStart = Read-Owner
        if ((Hash $installation.SelectionPath) -ne $manifest.originalSelectionSha256) { Assert-OwnedCandidate $expectedAtStart }
    }
    Assert-SameOwner $expectedAtStart (Read-Owner)
    if ($rollbackRoute -in @('pointer-only', 'pointer-intent-source', 'pointer-intent-destination') -and $null -ne $expectedAtStart) {
        throw ('A tray is running although the candidate was already stopped and monitoring restored; refusing before any signal. Owner: ' + (Describe-Owner $expectedAtStart) + '.')
    }
    $trayMutexName = $paths.LockName('tray-instance')
    $createdNew = $false
    $script:trayReservation = [Threading.Mutex]::new($false, $trayMutexName, [ref]$createdNew)
    if (!$createdNew) {
        $actualOwner = Read-Owner
        if ($null -eq $expectedAtStart -or $null -eq $actualOwner -or $actualOwner.pid -ne $expectedAtStart.pid -or $actualOwner.ticks -ne $expectedAtStart.ticks) {
            throw 'The tray-instance object already exists without the verified preflight owner; refusing before mutation.'
        }
    }
    $phaseTarget = if ($Action -eq 'Switch') { $candidate.Version } else { $manifest.originalVersion }
    $contractPath = if ($Action -eq 'Switch') { Join-Path $manifest.preparedPayload 'release.json' } else { Join-Path ($installation.Payload($phaseTarget)) 'release.json' }
    $targetContractHash = Hash $contractPath
    if ($recovering) {
        $script:coordinationPlan = $existingPlan
        $script:pendingHash = $pendingHashNow
        $script:restartHash = $restartHashNow
        $manifest | Add-Member -NotePropertyName coordinationPlan -NotePropertyValue $existingPlan -Force
        $manifest | Add-Member -NotePropertyName coordinationPendingHash -NotePropertyValue $pendingHashNow -Force
        $manifest | Add-Member -NotePropertyName coordinationRestartHash -NotePropertyValue $restartHashNow -Force
        $manifest | Add-Member -NotePropertyName coordinationPhase -NotePropertyValue $recoveryPhase -Force
        $manifest | Add-Member -NotePropertyName coordinationPhaseTarget -NotePropertyValue $existingPlan.Target -Force
        Save-Json $OperationManifest $manifest
        $nextPlan = [BellTower.Updates.UpdatePlan]::new($existingPlan.Id, $existingPlan.Token, $InstallationRoot, $DataDirectory,
            $manifest.originalVersion, $phaseTarget, $targetContractHash, [DateTimeOffset]::UtcNow, [BellTower.IO.ProcessProbe]::Identify($PID), $expectedHadTray)
        Replace-OwnedRecordPair $nextPlan $Action $phaseTarget
    } else {
        $activationId = [Guid]::NewGuid().ToString('N')
        $script:coordinationPlan = [BellTower.Updates.UpdatePlan]::new($activationId, [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)),
            $InstallationRoot, $DataDirectory, $manifest.originalVersion, $phaseTarget, $targetContractHash, [DateTimeOffset]::UtcNow,
            [BellTower.IO.ProcessProbe]::Identify($PID), ($null -ne $manifest.originalTray -and !$NoTray))
        $script:pendingHash = Get-PlanHash $script:coordinationPlan
        $script:restartHash = $script:pendingHash
        $manifest | Add-Member -NotePropertyName coordinationId -NotePropertyValue $script:coordinationPlan.Id -Force
        $manifest | Add-Member -NotePropertyName coordinationToken -NotePropertyValue $script:coordinationPlan.Token -Force
        $manifest | Add-Member -NotePropertyName coordinationPhase -NotePropertyValue $Action -Force
        $manifest | Add-Member -NotePropertyName coordinationPhaseTarget -NotePropertyValue $phaseTarget -Force
        Save-CoordinationExpectation
        if ((New-ExclusiveRecord $script:pendingPath $script:coordinationPlan) -ne $script:pendingHash) { throw 'Pending marker bytes differ from the manifest expectation.' }
        try {
            if ((New-ExclusiveRecord $script:restartPath $script:coordinationPlan) -ne $script:restartHash) { throw 'Restart marker bytes differ from the manifest expectation.' }
        } catch {
            if ((Test-Path -LiteralPath $script:pendingPath) -and (Hash $script:pendingPath) -eq $script:pendingHash) { Remove-Item -LiteralPath $script:pendingPath }
            throw
        }
    }
    $activationId = $script:coordinationPlan.Id
    $manifest | Add-Member -NotePropertyName coordinationId -NotePropertyValue $activationId -Force
    $script:expectedStoppedTray = $expectedAtStart
    if ($Action -eq 'Switch') {
        if ($manifest.state -ne 'prepared') { throw ('The operation was already applied (state ' + $manifest.state + ').') }
        # Bind the candidate tray to this Switch activation; a later Rollback mints its own coordination ID.
        $manifest | Add-Member -NotePropertyName candidateActivation -NotePropertyValue $activationId -Force
        $selectionHash = Hash $installation.SelectionPath
        if ($selectionHash -ne $manifest.originalSelectionSha256) { throw ('The original selection changed since preflight (current.json). Expected sha256 ' + $manifest.originalSelectionSha256 + ', actual ' + $selectionHash + '.') }
        Assert-Compatible $candidate $DataDirectory "the candidate $($candidate.Version)"
        Stop-VerifiedTray $manifest.originalTray
        Wait-InstallationTrayStartsDrained
        $manifest.state = 'original-stopped'
        Save-Json $OperationManifest $manifest
        Save-SwitchMonitoringSnapshot
        # Copying and hashing a payload may be slow; the update gate serializes installers while
        # launcher-based hooks remain free to resolve and start the currently selected payload.
        $target = Join-Path $InstallationRoot ('versions/' + $candidate.Version)
        [BellTower.Distribution.Installation]::EnsurePlainPath($target)
        if (!(Within (Join-Path $InstallationRoot 'versions') $target) -or (Test-Path -LiteralPath $target)) { throw 'Candidate target is unsafe or already exists.' }
        [BellTower.Distribution.Installation]::VerifyFiles($manifest.preparedPayload, $candidate)
        Copy-Item -LiteralPath $manifest.preparedPayload -Destination $target -Recurse
        [BellTower.Distribution.Installation]::VerifyFiles($target, $candidate)
        Wait-TestBarrier 'switch-before-selection'
        $selection = [BellTower.Distribution.VersionSelection]::new($candidate.Version, $manifest.originalVersion, [Guid]::NewGuid().ToString('N'))
        $planned = Join-Path $manifest.operationDirectory 'proposed-current.json'
        [BellTower.Distribution.DistributionJson]::Write($planned, $selection)
        $manifest | Add-Member -NotePropertyName devSelectionSha256 -NotePropertyValue (Hash $planned) -Force
        $manifest.state = 'ready-to-select'
        Save-Json $OperationManifest $manifest
        Assert-OriginalBytes $manifest
        # Final renewal and drain after the slow phase, then the guarded post-gate check immediately before the pointer write.
        Invoke-GuardedPointerStep 'switch-selection' $manifest.originalSelectionSha256 'The original selection changed before activation (current.json).' {
            if ((Hash $planned) -ne $manifest.devSelectionSha256) { throw 'The proposed dev selection differs from its recorded hash.' }
            $script:selectionMutated = $true
            Restore-SnapshotFile $planned $installation.SelectionPath
            if ((Hash $installation.SelectionPath) -ne $manifest.devSelectionSha256) { throw 'The written selection differs from the recorded dev selection bytes.' }
        }
        $manifest.state = 'selected-candidate'
        Save-Json $OperationManifest $manifest
    } else {
        # Re-classify after takeover: the route must be the one validated before any record or state change.
        $route = Get-RollbackRoute
        if ($route -ne $rollbackRoute) { throw ('The rollback route changed during recovery (' + $rollbackRoute + ' -> ' + $route + '); rollback refused.') }
        switch ($route) {
            'never-selected' {
                Assert-OperationBoundary 'rollback-state-restore'
                Assert-Compatible $original $DataDirectory "the original $($manifest.originalVersion)"
                $originalOwner = Read-Owner
                if ($null -ne $originalOwner) { Assert-SameOwner $manifest.originalTray $originalOwner }
                $manifest.state = 'restored-monitoring-snapshot'
                Save-Json $OperationManifest $manifest
            }
            'original-retry' {
                # A retry after the original-pointer phase finishes restart and cleanup without restoring the snapshot twice.
                $script:selectionMutated = $true
                Assert-OperationBoundary 'rollback-retry'
            }
            'pointer-intent-destination' {
                # The original pointer was committed before its phase publication: verify it under the same guards and
                # commit only the phase. Neither monitoring nor the pointer is rewritten.
                $script:selectionMutated = $true
                Invoke-GuardedPointerStep 'rollback-pointer-commit' $manifest.originalSelectionSha256 'The restored original selection changed before its phase was committed (current.json).' {
                    Assert-RollbackPointerIntent $manifest.rollbackPointerTransition
                    Save-OriginalPointerPhase
                }
            }
            { $_ -in @('pointer-only', 'pointer-intent-source') } {
                # Monitoring restoration is durably complete; finish only the guarded pointer step.
                Complete-RollbackPointer
            }
            'restore-monitoring' {
                $candidateOwner = Read-Owner
                Assert-OwnedCandidate $candidateOwner
                Wait-OptionalTestBarrier 'candidate-before-stop'
                Stop-VerifiedTray $candidateOwner { param($Live) Assert-OwnedCandidate $Live }
                $commit = Enter-MonitoringCommit 'rollback-monitoring-restore'
                $primary = $null
                $failures = @()
                try {
                    if ($manifest.PSObject.Properties.Name -notcontains 'savedDevMonitoring') {
                        $aside = Join-Path $manifest.operationDirectory ('dev-monitoring-aside-' + [Guid]::NewGuid().ToString('N'))
                        $devSnapshotFiles = Backup-Monitoring $aside
                        $manifest | Add-Member -NotePropertyName savedDevMonitoring -NotePropertyValue ([ordered]@{ directory = $aside; files = $devSnapshotFiles }) -Force
                        Save-Json $OperationManifest $manifest
                    }
                    $before = Join-Path $manifest.operationDirectory 'before-monitoring'
                    Assert-Snapshot $before $manifest.originalMonitoringFiles
                    Assert-Compatible $original $before "the original $($manifest.originalVersion)"
                    $manifest.state = 'restoring-monitoring-snapshot'
                    $script:monitoringMutationStarted = $true
                    Save-Json $OperationManifest $manifest
                    foreach ($name in @('state.json', 'state.json.bak')) {
                        $source = Join-Path $before $name
                        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { $source = Join-Path $before 'state.json' }
                        Restore-SnapshotFile $source (Join-Path $DataDirectory $name)
                    }
                    Assert-Compatible $original $DataDirectory "the original $($manifest.originalVersion)"
                    $manifest.state = 'restored-monitoring-candidate-pointer'
                    Save-Json $OperationManifest $manifest
                } catch { $primary = $_ }
                finally { $failures = Close-MonitoringCommit $commit }
                Complete-ResourceScope $failures $primary
                Wait-TestBarrier 'rollback-before-selection'
                # The same pointer completion as every restored-monitoring retry.
                Complete-RollbackPointer
            }
            default { throw ('Unknown rollback route ' + $route + '.') }
        }
    }
    Assert-OriginalBytes $manifest
    Assert-OperationBoundary 'reservation-release'
    $gate.Dispose()
    $gate = $null
    $script:trayReservation.Dispose()
    $script:trayReservation = $null
    $reservationReleased = $true
} finally {
    if ($script:trayReservation) { $script:trayReservation.Dispose(); $script:trayReservation = $null }
    if ($gate) { $gate.Dispose() }
    # Failure path only: a successful run keeps its records through the tray restart and the final ownership check.
    if (!$reservationReleased -and !$script:selectionMutated -and !$script:monitoringMutationStarted -and $script:coordinationPlan) { Remove-OwnedCoordinationRecords }
}
$owner = $null
if ($Action -eq 'Switch') {
    if ($null -ne $manifest.originalTray -and !$NoTray) {
        $owner = Start-SelectedTray $candidate.Version
        if ($TestBarrierDirectory -and (Test-Path -LiteralPath (Join-Path $TestBarrierDirectory 'hold-candidate-presence-before-receipt'))) {
            Wait-TestBarrier 'candidate-presence-before-receipt'
        }
        $manifest | Add-Member -NotePropertyName candidateTray -NotePropertyValue $owner -Force
    }
    $manifest.state = 'running-candidate'
} else {
    if ($null -ne $manifest.originalTray -and !$NoTray) { $owner = Start-SelectedTray $manifest.originalVersion }
    $manifest.state = 'running-original'
}
Assert-OriginalBytes $manifest
Save-Json $OperationManifest $manifest
foreach ($path in @($script:pendingPath, $script:restartPath)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Operation coordination record disappeared; preserving recovery state.' }
    $record = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($path, 16384)
    if ($record.Id -ne $script:coordinationPlan.Id -or $record.Token -ne $script:coordinationPlan.Token) { throw 'Operation coordination ownership changed; preserving recovery state.' }
}
Remove-OwnedCoordinationRecords
[ordered]@{
    status = $manifest.state; operationManifest = $OperationManifest; selectedVersion = $installation.Selection().Version
    trayPid = if ($owner) { $owner.pid } else { $null }; trayRestarted = [bool]$owner; originalBytesPreserved = $true
    restoredFiles = if ($Action -eq 'Rollback') { 'state.json, state.json.bak, current.json only' } else { 'none' }; configurationAndRuntimeFiles = 'untouched'
} | ConvertTo-Json
