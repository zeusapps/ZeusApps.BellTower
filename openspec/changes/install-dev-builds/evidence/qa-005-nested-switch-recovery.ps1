param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $Repo ('artifacts/implementation-005-native-restart-' + [Guid]::NewGuid().ToString('N'))
$install = Join-Path $artifacts 'install'
$data = Join-Path $artifacts 'data'
$workspace = Join-Path $artifacts 'private'
$barrier = Join-Path $artifacts 'barriers'
$source = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64'
$package = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9001-win-x64.zip'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installerScript = Join-Path $Repo 'scripts/install-dev.ps1'
function Invoke-Installer([string[]]$Arguments) {
    $pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'
    if (!(Test-Path -LiteralPath $pwsh)) { $pwsh = 'pwsh.exe' }
    $stdoutPath = Join-Path $artifacts ('installer-' + [Guid]::NewGuid().ToString('N') + '.stdout.txt')
    $stderrPath = Join-Path $artifacts ('installer-' + [Guid]::NewGuid().ToString('N') + '.stderr.txt')
    $startArguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installerScript) + $Arguments
    $process = Start-Process -FilePath $pwsh -ArgumentList $startArguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    if (!$process.WaitForExit(180000)) { Stop-Process -Id $process.Id -Force; throw 'Disposable installer process exceeded three minutes.' }
    $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { '' }
    $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
    return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout + $stderr }
}
if (!(Test-Path -LiteralPath $source -PathType Container) -or !(Test-Path -LiteralPath $package -PathType Leaf)) { throw 'Required disposable packages 0.1.9003 and 0.1.9001 are missing.' }
foreach ($path in @($artifacts, $install, $data, $workspace)) {
    if (Test-Path -LiteralPath $path) { throw "Refusing to reuse existing smoke path: $path" }
}
New-Item -ItemType Directory -Path $artifacts | Out-Null
New-Item -ItemType Directory -Path $install, $data | Out-Null
New-Item -ItemType Directory -Path $barrier | Out-Null
foreach ($phase in @('switch-before-selection', 'rollback-before-selection')) { New-Item -ItemType File -Path (Join-Path $barrier ($phase + '.release')) | Out-Null }
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $install -Recurse -Force
Set-Content -LiteralPath (Join-Path $data 'state.json') -Value '{"schemaVersion":1,"sessions":{}}' -NoNewline
Set-Content -LiteralPath (Join-Path $data 'state.json.bak') -Value '{"schemaVersion":1,"sessions":{}}' -NoNewline
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
$paths = [BellTower.IO.DataPaths]::new($data)
$controller = [BellTower.Tray.TrayController]::new($paths)
$launcher = Join-Path $install 'bell-tower.exe'
$start = [Diagnostics.ProcessStartInfo]::new($launcher)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
foreach ($argument in @('tray', '--data-dir', $data, '--silent')) { $start.ArgumentList.Add($argument) }
$initial = [Diagnostics.Process]::Start($start)
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)
    while ([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath $presence -PathType Leaf)) { Start-Sleep -Milliseconds 100 }
    if (!(Test-Path -LiteralPath $presence -PathType Leaf)) { throw 'Initial disposable tray did not write presence.' }
    $preflight = Invoke-Installer @('-Action','Preflight','-Package',$package,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    if ($preflight.ExitCode -ne 0) { throw "Preflight failed: $($preflight.Output)" }
    $manifest = ($preflight.Output | ConvertFrom-Json).operationManifest
    New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9001') | Out-Null
    $failedSwitch = Invoke-Installer @('-Action','Switch','-Package',$package,'-OperationManifest',$manifest,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    if ($failedSwitch.ExitCode -eq 0 -or !$failedSwitch.Output.Contains('restart of version 0.1.9001 refused')) { throw "Switch restart failure injection did not trigger: $($failedSwitch.Output)" }
    Remove-Item -LiteralPath (Join-Path $barrier 'fail-start-0.1.9001')
    New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9003') | Out-Null
    $failedRecovery=Invoke-Installer @('-Action','Rollback','-Package',$package,'-OperationManifest',$manifest,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    $phaseManifest=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
    Remove-Item -LiteralPath (Join-Path $barrier 'fail-start-0.1.9003')
    $retry=Invoke-Installer @('-Action','Rollback','-Package',$package,'-OperationManifest',$manifest,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    [ordered]@{scenario='failed Switch candidate start, failed original restart during same-manifest recovery, retry';failedRecoveryExit=$failedRecovery.ExitCode;phase=$phaseManifest.coordinationPhase;phaseTarget=$phaseManifest.coordinationPhaseTarget;manifestState=$phaseManifest.state;selectedAfter=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version;retryExit=$retry.ExitCode;retryOutput=$retry.Output;pendingMarkerRemains=(Test-Path (Join-Path $install 'updates/pending.json'));restartMarkerRemains=(Test-Path ([BellTower.Updates.UpdateTransaction]::Marker($paths)));disposableRoot=$artifacts}|ConvertTo-Json -Depth 7
    return    $package2 = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9002-win-x64.zip'
    $preflight2 = Invoke-Installer @('-Action','Preflight','-Package',$package2,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    if ($preflight2.ExitCode -ne 0) { throw "Second Preflight failed: $($preflight2.Output)" }
    $manifest2 = ($preflight2.Output | ConvertFrom-Json).operationManifest
    $switch = Invoke-Installer @('-Action','Switch','-Package',$package2,'-OperationManifest',$manifest2,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    if ($switch.ExitCode -ne 0) { throw "Second Switch failed: $($switch.Output)" }
    $switchResult = $switch.Output | ConvertFrom-Json
    $switchedOwner = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence, 4096)
    if ($switchedOwner.Version -ne '0.1.9002' -or $switchedOwner.Activation -eq $null) { throw 'Candidate restart did not publish the expected version and activation.' }
    New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9003') | Out-Null
    $failedRollback = Invoke-Installer @('-Action','Rollback','-Package',$package2,'-OperationManifest',$manifest2,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    if ($failedRollback.ExitCode -eq 0 -or !$failedRollback.Output.Contains('restart of version 0.1.9003 refused')) { throw "Rollback restart failure injection did not trigger: $($failedRollback.Output)" }
    Remove-Item -LiteralPath (Join-Path $barrier 'fail-start-0.1.9003')
    $rollback = Invoke-Installer @('-Action','Rollback','-Package',$package2,'-OperationManifest',$manifest2,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    if ($rollback.ExitCode -ne 0) { throw "Rollback recovery failed: $($rollback.Output)" }
    $restoredOwner = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence, 4096)
    if ($restoredOwner.Version -ne '0.1.9003' -or $restoredOwner.Activation -eq $null) { throw 'Original-version restart recovery did not publish the expected version and activation.' }
    $controller.RequestExit()
    $endedIdentity = [BellTower.Core.ProcessIdentity]::new($restoredOwner.Process.Pid, $restoredOwner.Process.StartedUtcTicks)
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline -and [BellTower.IO.ProcessProbe]::Inspect($endedIdentity) -eq [BellTower.IO.ProcessLiveness]::Live) { Start-Sleep -Milliseconds 100 }
    if ([BellTower.IO.ProcessProbe]::Inspect($endedIdentity) -ne [BellTower.IO.ProcessLiveness]::Ended) { throw 'Disposable tray did not exit after the verified shutdown request.' }
    $stalePresenceRemains = Test-Path -LiteralPath $presence
    $retired = $null
    if (Test-Path -LiteralPath (Join-Path $install 'versions/0.1.9001')) {
        $retired = Invoke-Installer @('-Action','Retire','-Version','0.1.9001','-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable')
        if ($retired.ExitCode -ne 0) { throw "Candidate Retire failed: $($retired.Output)" }
    }
    $stoppedPreflight = Invoke-Installer @('-Action','Preflight','-Package',$package,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    if ($stoppedPreflight.ExitCode -ne 0) { throw "Stopped-tray Preflight failed: $($stoppedPreflight.Output)" }
    $stoppedPreflight = $stoppedPreflight.Output | ConvertFrom-Json
    if ($stoppedPreflight.status -ne 'preflight-passed' -or $stoppedPreflight.trayRunning) { throw 'Preflight did not classify the ended tray presence as stopped.' }
    [ordered]@{ preflight = 'passed'; failedSwitchRecovery = $switchRecoveryResult; switch = $switchResult; failedRollbackRestartRecovered = ($rollback.Output | ConvertFrom-Json); switchedTray = $switchedOwner; restoredTray = $restoredOwner; stalePresenceRemains = $stalePresenceRemains; stoppedPreflight = $stoppedPreflight; retiredCandidate = if ($retired) { $retired.Output | ConvertFrom-Json } else { 'automatic cleanup removed 0.1.9001' }; disposableRoot = $artifacts } | ConvertTo-Json -Depth 10
} finally {
    try { $controller.RequestExit() } catch { }
    if ($initial -and !$initial.HasExited) { try { $initial.WaitForExit(10000) | Out-Null } catch { } }
}

