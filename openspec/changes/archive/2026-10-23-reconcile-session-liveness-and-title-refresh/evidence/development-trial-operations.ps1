#Requires -Version 7.4
param(
    [ValidateSet('Preflight','Switch','Rollback')][string]$Action = 'Preflight',
    [Parameter(Mandatory=$true)][string]$InstallationRoot,
    [Parameter(Mandatory=$true)][string]$DataDirectory,
    [Parameter(Mandatory=$true)][string]$CandidateArchive,
    [Parameter(Mandatory=$true)][string]$CandidateSha256,
    [Parameter(Mandatory=$true)][string]$BackupWorkspace,
    [string]$OperationManifest,
    [string]$OriginalVersion = '0.1.8',
    [string]$CandidateVersion = '0.1.9',
    [switch]$InjectReadinessFailure
)
$ErrorActionPreference = 'Stop'
$helperLayout = $PSScriptRoot.Replace('\','/').TrimEnd('/')
if ($helperLayout -match '/openspec/changes/archive/\d{4}-\d{2}-\d{2}-reconcile-session-liveness-and-title-refresh/evidence$') {
    $rootRelative = '../../../../..'
} elseif ($helperLayout.EndsWith('/openspec/changes/reconcile-session-liveness-and-title-refresh/evidence',[StringComparison]::OrdinalIgnoreCase)) {
    $rootRelative = '../../../..'
} else {
    throw 'Unsupported recovery helper layout; use the reviewed active or dated-archive path.'
}
# A mirrored private recovery-tool is its own root. Never search above it for newer API DLLs.
$repo = (Resolve-Path (Join-Path $PSScriptRoot $rootRelative)).Path
$bin = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
foreach ($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) }
function Full([string]$Path) { [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path)) }
function Same([string]$Left,[string]$Right) { return (Full $Left).Equals((Full $Right),[StringComparison]::OrdinalIgnoreCase) }
function Within([string]$Parent,[string]$Child) { return (Full $Child).StartsWith((Full $Parent)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) }
function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save-Json([string]$Path,$Value) { [BellTower.IO.AtomicFile]::Write($Path,($Value | ConvertTo-Json -Depth 30),$false) }
function Wait-Until([scriptblock]$Ready,[string]$Failure) {
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $deadline) { if (& $Ready) { return }; Start-Sleep -Milliseconds 100 }
    throw $Failure
}
function Assert-PrivateWorkspace {
    if (!(Test-Path -LiteralPath $BackupWorkspace -PathType Container)) { throw 'Create the private backup workspace before preflight.' }
    $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')
    $acl=Get-Acl -LiteralPath $BackupWorkspace
    foreach ($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and $rule.IdentityReference.Value -notin $allowed) {
            throw 'Backup workspace allows another identity; restrict its ACL before copying monitoring snapshots.'
        }
    }
}
function Read-Owner {
    if (!$controller.IsRunning()) { return $null }
    $presencePath=[BellTower.Updates.UpdateTransaction]::Presence($paths)
    if (!(Test-Path -LiteralPath $presencePath -PathType Leaf)) { throw 'Running tray has no verifiable presence record.' }
    $presence=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presencePath,4096)
    if (!(Same $presence.DataDirectory $DataDirectory) -or [BellTower.IO.ProcessProbe]::Identify($presence.Process.Pid) -ne $presence.Process) { throw 'Tray presence ownership cannot be verified.' }
    $process=[Diagnostics.Process]::GetProcessById($presence.Process.Pid)
    try { $executable=Full $process.MainModule.FileName } finally { $process.Dispose() }
    $expected=Join-Path ($installation.Payload($presence.Version)) 'bell-tower.exe'
    if (!(Same $executable $expected)) { throw 'Tray executable does not match its declared installation/version.' }
    return [ordered]@{pid=$presence.Process.Pid;ticks=$presence.Process.StartedUtcTicks;version=$presence.Version;executable=$executable}
}
function Assert-SameOwner($Expected,$Actual) {
    if ($null -eq $Expected -and $null -eq $Actual) { return }
    if ($null -eq $Expected -or $null -eq $Actual -or $Expected.pid -ne $Actual.pid -or $Expected.ticks -ne $Actual.ticks -or !(Same $Expected.executable $Actual.executable)) { throw 'Monitoring owner changed since preflight; stop and inspect.' }
}
function Stop-VerifiedTray($Expected) {
    Assert-SameOwner $Expected (Read-Owner)
    if ($null -ne $Expected) {
        $controller.RequestExit()
        Wait-Until { !$controller.IsRunning() } 'Verified tray did not exit; no provider or runtime was signalled.'
    }
}
function Start-SelectedTray([string]$Version) {
    $start=[Diagnostics.ProcessStartInfo]::new($installation.Launcher)
    $start.UseShellExecute=$true
    $start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory=$InstallationRoot
    foreach ($argument in @('tray','--data-dir',$DataDirectory)) { $start.ArgumentList.Add($argument) }
    $child=[Diagnostics.Process]::Start($start)
    if ($Version -eq $CandidateVersion) {
        $identity=[BellTower.IO.ProcessProbe]::Identify($child.Id)
        $manifest | Add-Member -NotePropertyName candidateLauncher -NotePropertyValue ([ordered]@{pid=$identity.Pid;ticks=$identity.StartedUtcTicks}) -Force
        Save-Json $OperationManifest $manifest
    }
    $child.Dispose()
    Wait-Until { try { $owner=Read-Owner; return ($null -ne $owner -and $owner.version -eq $Version) } catch { return $false } } 'Selected tray did not confirm ownership; leave selection/data intact and inspect the operation manifest.'
    if ($Version -eq $CandidateVersion) { Assert-OwnedCandidate (Read-Owner) }
    if ($Version -eq $CandidateVersion -and $InjectReadinessFailure) { throw 'Injected post-commit readiness failure; use reviewed rollback recovery.' }
    return (Read-Owner)
}
function Assert-OwnedCandidate($Owner) {
    if ($null -eq $Owner) { return }
    if ($Owner.version -ne $CandidateVersion) { throw 'A different tray now owns monitoring.' }
    if ($manifest.PSObject.Properties.Name -contains 'candidateTray') { Assert-SameOwner $manifest.candidateTray $Owner; return }
    if ($manifest.PSObject.Properties.Name -notcontains 'candidateLauncher') { throw 'Candidate launch ownership was not recorded.' }
    $launcher=[BellTower.IO.ProcessProbe]::Identify($manifest.candidateLauncher.pid)
    $parents=[BellTower.IO.ProcessProbe]::Ancestors($Owner.pid)
    if ($null -eq $launcher -or $launcher.StartedUtcTicks -ne $manifest.candidateLauncher.ticks -or $manifest.candidateLauncher.pid -notin @($parents.Item1)) { throw 'Candidate tray is not a child of this operation launcher.' }
}
function Backup-Monitoring([string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -ErrorAction Stop | Out-Null
    foreach ($name in @('state.json','state.json.bak')) {
        $source=Join-Path $DataDirectory $name
        if (Test-Path -LiteralPath $source -PathType Leaf) { Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $name) }
    }
    Copy-Item -LiteralPath $installation.SelectionPath -Destination (Join-Path $Destination 'current.json')
    return ,@(Get-ChildItem -LiteralPath $Destination -File | ForEach-Object { [ordered]@{name=$_.Name;sha256=(Hash $_.FullName)} })
}
function Assert-Snapshot([string]$Directory,$Files) {
    if (!(Within $manifest.operationDirectory $Directory)) { throw 'Snapshot path is outside this operation.' }
    foreach ($file in $Files) {
        if ($file.name -notin @('state.json','state.json.bak','current.json') -or (Hash (Join-Path $Directory $file.name)) -ne $file.sha256) { throw 'Private monitoring snapshot differs from its recorded hash.' }
    }
    if ('state.json' -notin @($Files.name) -or 'current.json' -notin @($Files.name)) { throw 'Monitoring snapshot is incomplete.' }
}
function Restore-SnapshotFile([string]$Source,[string]$Target) {
    if (!(Same $Target (Join-Path $DataDirectory 'state.json')) -and !(Same $Target (Join-Path $DataDirectory 'state.json.bak')) -and !(Same $Target $installation.SelectionPath)) { throw 'Unexpected monitoring restore target.' }
    [BellTower.Distribution.Installation]::EnsurePlainPath($Target)
    $temporary=$Target+'.'+[Guid]::NewGuid().ToString('N')+'.trialtmp'
    try {
        [IO.File]::Copy($Source,$temporary,$false)
        [IO.File]::Move($temporary,$Target,$true)
        if ((Hash $Source) -ne (Hash $Target)) { throw 'Restored monitoring bytes differ from the private snapshot.' }
    } finally { if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary } }
}
function Assert-OriginalBytes($Manifest) {
    if ((Hash $installation.Launcher) -ne $Manifest.originalLauncherSha256) { throw 'Original stable launcher changed.' }
    foreach ($file in $Manifest.originalPayloadFiles) {
        if ((Hash (Join-Path ($installation.Payload($OriginalVersion)) $file.path)) -ne $file.sha256) { throw 'An original payload file changed; inspect privately.' }
    }
}
function Assert-NoFastCapture {
    if ((Test-Path -LiteralPath (Join-Path $DataDirectory 'capture-protocol.json')) -or
        ((Test-Path -LiteralPath (Join-Path $DataDirectory 'captures-v1')) -and @(Get-ChildItem -LiteralPath (Join-Path $DataDirectory 'captures-v1') -File -Filter '*.json').Count -gt 0)) {
        throw 'Fast capture is enabled or pending; this legacy rollback recipe is not applicable.'
    }
}
$InstallationRoot=Full $InstallationRoot
$DataDirectory=Full $DataDirectory
$BackupWorkspace=Full $BackupWorkspace
$CandidateArchive=Full $CandidateArchive
foreach ($root in @($InstallationRoot,$DataDirectory,$BackupWorkspace)) { [BellTower.Distribution.Installation]::EnsurePlainPath($root) }
if ((Same $InstallationRoot $DataDirectory) -or (Within $InstallationRoot $DataDirectory) -or (Within $DataDirectory $InstallationRoot) -or
    (Same $BackupWorkspace $DataDirectory) -or (Within $DataDirectory $BackupWorkspace) -or (Within $BackupWorkspace $DataDirectory) -or
    (Same $BackupWorkspace $InstallationRoot) -or (Within $InstallationRoot $BackupWorkspace) -or (Within $BackupWorkspace $InstallationRoot)) { throw 'Installation, data and private backup workspace must be separate.' }
if ($InjectReadinessFailure -and !(Within (Join-Path $repo 'artifacts') $DataDirectory)) { throw 'Failure injection is restricted to disposable artifact data.' }
Assert-PrivateWorkspace
if ((Hash $CandidateArchive) -ne $CandidateSha256.ToLowerInvariant()) { throw 'Candidate archive differs from the reviewed hash.' }
$installation=[BellTower.Distribution.Installation]::new($InstallationRoot)
$paths=[BellTower.IO.DataPaths]::new($DataDirectory)
$controller=[BellTower.Tray.TrayController]::new($paths)
if (!(Test-Path -LiteralPath $paths.State -PathType Leaf)) { throw 'Existing monitoring state is required for this trial recipe.' }
$offer=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.ReleaseOffer]($CandidateArchive.Substring(0,$CandidateArchive.Length-4)+'.release.json',1048576)
if ($offer.Version -ne $CandidateVersion -or $offer.Contract.AttachmentHealth -ne 1 -or $offer.ArchiveHash -ne $CandidateSha256.ToLowerInvariant()) { throw 'Candidate descriptor/version/capability does not match the reviewed package.' }
if ($Action -eq 'Preflight') {
    if ($installation.Selection().Version -ne $OriginalVersion) { throw 'Original version is not selected.' }
    Assert-NoFastCapture
    [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($DataDirectory,0)
    if (Test-Path -LiteralPath (Join-Path $InstallationRoot ('versions/'+$CandidateVersion))) { throw 'Candidate target already exists; do not overwrite it.' }
    $operation=Join-Path $BackupWorkspace ('bell-tower-trial-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $operation | Out-Null
    $prepared=[BellTower.Updates.ReleasePackage]::Extract($CandidateArchive,(Join-Path $operation 'prepared-package'),$offer)
    [BellTower.Updates.ReleasePackage]::Probe($prepared,$CandidateVersion)
    $files=@(Get-ChildItem -LiteralPath ($installation.Payload($OriginalVersion)) -File -Recurse | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($installation.Payload($OriginalVersion),$_.FullName);sha256=(Hash $_.FullName)} })
    $manifest=[ordered]@{installationRoot=$InstallationRoot;dataDirectory=$DataDirectory;backupWorkspace=$BackupWorkspace;operationDirectory=$operation;
        originalVersion=$OriginalVersion;candidateVersion=$CandidateVersion;candidateArchive=$CandidateArchive;candidateSha256=$CandidateSha256.ToLowerInvariant();preparedPayload=$prepared;
        originalSelectionSha256=(Hash $installation.SelectionPath);originalLauncherSha256=(Hash $installation.Launcher);originalPayloadFiles=$files;originalTray=(Read-Owner);state='prepared'}
    $OperationManifest=Join-Path $operation 'operation.json'
    Save-Json $OperationManifest $manifest
    [ordered]@{status='preflight-passed';operationManifest=$OperationManifest;originalFiles=$files.Count;candidateVersion=$CandidateVersion;productionMutation='none; only private preparation workspace written'} | ConvertTo-Json
    return
}
if (!$OperationManifest -or !(Within $BackupWorkspace $OperationManifest)) { throw 'Use the reviewed preflight manifest inside the private backup workspace.' }
$manifest=Get-Content -LiteralPath $OperationManifest -Raw | ConvertFrom-Json
foreach ($pair in @(@($manifest.installationRoot,$InstallationRoot),@($manifest.dataDirectory,$DataDirectory),@($manifest.backupWorkspace,$BackupWorkspace),@($manifest.candidateArchive,$CandidateArchive))) { if (!(Same $pair[0] $pair[1])) { throw 'Operation paths differ from preflight.' } }
if ($manifest.originalVersion -ne $OriginalVersion -or $manifest.candidateVersion -ne $CandidateVersion -or $manifest.candidateSha256 -ne $CandidateSha256.ToLowerInvariant() -or !(Within $BackupWorkspace $manifest.operationDirectory)) { throw 'Operation identity differs from preflight.' }
if (!(Within $manifest.operationDirectory $manifest.preparedPayload)) { throw 'Prepared payload escaped its operation workspace.' }
[BellTower.Distribution.Installation]::EnsurePlainPath($manifest.preparedPayload)
Assert-OriginalBytes $manifest
$gate=[BellTower.Distribution.InstallationGate]::new($InstallationRoot)
try {
    if ($Action -eq 'Switch') {
        if ($manifest.state -ne 'prepared' -or (Hash $installation.SelectionPath) -ne $manifest.originalSelectionSha256) { throw 'Original selection changed or operation already applied.' }
        Assert-NoFastCapture
        [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($DataDirectory,0)
        Stop-VerifiedTray $manifest.originalTray
        $manifest.state='original-stopped'
        Save-Json $OperationManifest $manifest
        $setupGate=[BellTower.IO.DataLock]::new($paths,'setup')
        $stateGate=[BellTower.IO.DataLock]::new($paths,'state')
        $presentationGate=[BellTower.IO.DataLock]::new($paths,'presentation')
        try {
            $before=Join-Path $manifest.operationDirectory 'before-monitoring'
            $snapshotFiles=Backup-Monitoring $before
            $manifest | Add-Member -NotePropertyName originalMonitoringFiles -NotePropertyValue $snapshotFiles -Force
            Save-Json $OperationManifest $manifest
            Assert-Snapshot $before $manifest.originalMonitoringFiles
            Assert-NoFastCapture
            [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($before,0)
            $target=Join-Path $InstallationRoot ('versions/'+$CandidateVersion)
            [BellTower.Distribution.Installation]::EnsurePlainPath($target)
            if (!(Within (Join-Path $InstallationRoot 'versions') $target) -or (Test-Path -LiteralPath $target)) { throw 'Candidate target is unsafe or already exists.' }
            [BellTower.Distribution.Installation]::VerifyFiles($manifest.preparedPayload,$offer.Contract)
            Copy-Item -LiteralPath $manifest.preparedPayload -Destination $target -Recurse
            [BellTower.Distribution.Installation]::VerifyFiles($target,$offer.Contract)
            $activation=[Guid]::NewGuid().ToString('N')
            $selection=[BellTower.Distribution.VersionSelection]::new($CandidateVersion,$OriginalVersion,$activation)
            $planned=Join-Path $manifest.operationDirectory 'proposed-current.json'
            [BellTower.Distribution.DistributionJson]::Write($planned,$selection)
            $manifest | Add-Member -NotePropertyName trialSelectionSha256 -NotePropertyValue (Hash $planned) -Force
            $manifest.state='ready-to-select'
            Save-Json $OperationManifest $manifest
            [BellTower.Distribution.DistributionJson]::Write($installation.SelectionPath,$selection)
            $manifest.state='selected-candidate'
            Save-Json $OperationManifest $manifest
        } finally { $presentationGate.Dispose();$stateGate.Dispose();$setupGate.Dispose() }
    } else {
        $currentSelectionHash=Hash $installation.SelectionPath
        if ($currentSelectionHash -eq $manifest.originalSelectionSha256 -and $manifest.state -in @('prepared','original-stopped','ready-to-select')) {
            [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($DataDirectory,0)
            $originalOwner=Read-Owner
            if ($null -ne $originalOwner) { Assert-SameOwner $manifest.originalTray $originalOwner }
            $manifest.state='restored-monitoring-snapshot'
            Save-Json $OperationManifest $manifest
        } else {
        if ($manifest.PSObject.Properties.Name -notcontains 'trialSelectionSha256' -or $currentSelectionHash -ne $manifest.trialSelectionSha256 -or
            $manifest.state -notin @('ready-to-select','selected-candidate','running-candidate')) { throw 'Trial selection/state changed; rollback refused.' }
        Assert-NoFastCapture
        $candidateOwner=Read-Owner
        Assert-OwnedCandidate $candidateOwner
        Stop-VerifiedTray $candidateOwner
        $setupGate=[BellTower.IO.DataLock]::new($paths,'setup')
        $stateGate=[BellTower.IO.DataLock]::new($paths,'state')
        $presentationGate=[BellTower.IO.DataLock]::new($paths,'presentation')
        try {
            $aside=Join-Path $manifest.operationDirectory ('trial-monitoring-aside-'+[Guid]::NewGuid().ToString('N'))
            $trialSnapshotFiles=Backup-Monitoring $aside
            $manifest | Add-Member -NotePropertyName savedTrialMonitoring -NotePropertyValue ([ordered]@{directory=$aside;files=$trialSnapshotFiles}) -Force
            Save-Json $OperationManifest $manifest
            $before=Join-Path $manifest.operationDirectory 'before-monitoring'
            Assert-Snapshot $before $manifest.originalMonitoringFiles
            Assert-NoFastCapture
            [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($before,0)
            foreach ($name in @('state.json','state.json.bak')) {
                $source=Join-Path $before $name
                if (!(Test-Path -LiteralPath $source -PathType Leaf)) { $source=Join-Path $before 'state.json' }
                Restore-SnapshotFile $source (Join-Path $DataDirectory $name)
            }
            [BellTower.Distribution.AttachmentHealthCompatibility]::Ensure($DataDirectory,0)
            Restore-SnapshotFile (Join-Path $before 'current.json') $installation.SelectionPath
            if ((Hash $installation.SelectionPath) -ne $manifest.originalSelectionSha256) { throw 'Restored selection differs from the original reviewed bytes.' }
            $manifest.state='restored-monitoring-snapshot'
            Save-Json $OperationManifest $manifest
        } finally { $presentationGate.Dispose();$stateGate.Dispose();$setupGate.Dispose() }
        }
    }
    Assert-OriginalBytes $manifest
} finally { $gate.Dispose() }
if ($Action -eq 'Switch') {
    $owner=Start-SelectedTray $CandidateVersion
    $manifest | Add-Member -NotePropertyName candidateTray -NotePropertyValue $owner -Force
    $manifest.state='running-candidate'
} else {
    $owner=Start-SelectedTray $OriginalVersion
    $manifest.state='running-original'
}
Assert-OriginalBytes $manifest
Save-Json $OperationManifest $manifest
[ordered]@{status=$manifest.state;operationManifest=$OperationManifest;selectedVersion=$installation.Selection().Version;trayPid=$owner.pid;originalBytesPreserved=$true;restoredFiles=if($Action -eq 'Rollback'){'state.json,state.json.bak,current.json only'}else{'none'};configurationAndRuntimeFiles='untouched'} | ConvertTo-Json
