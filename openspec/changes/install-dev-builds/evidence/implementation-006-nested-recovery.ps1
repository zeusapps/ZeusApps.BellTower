param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $Repo ('artifacts/implementation-006-nested-' + [Guid]::NewGuid().ToString('N'))
$install = Join-Path $artifacts 'install'; $data = Join-Path $artifacts 'data'; $workspace = Join-Path $artifacts 'private'; $barrier = Join-Path $artifacts 'barriers'
$source = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64'
$package = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9001-win-x64.zip'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $Repo 'scripts/install-dev.ps1'
if (!(Test-Path $source) -or !(Test-Path $package)) { throw 'Required disposable native packages are unavailable.' }
New-Item -ItemType Directory -Path $artifacts,$install,$data,$barrier | Out-Null
New-Item -ItemType File -Path (Join-Path $barrier 'switch-before-selection.release'),(Join-Path $barrier 'rollback-before-selection.release') | Out-Null
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $install -Recurse -Force
[IO.File]::WriteAllText((Join-Path $data 'state.json'),'{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),'{"schemaVersion":1,"sessions":{}}')
foreach ($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
$paths = [BellTower.IO.DataPaths]::new($data); $controller = [BellTower.Tray.TrayController]::new($paths)
$launcher = Join-Path $install 'bell-tower.exe'; $start = [Diagnostics.ProcessStartInfo]::new($launcher); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
foreach ($arg in @('tray','--data-dir',$data,'--silent')) { [void]$start.ArgumentList.Add($arg) }
$initial = [Diagnostics.Process]::Start($start)
function Invoke-Installer([string[]]$Arguments) {
    $pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh='pwsh.exe' }
    $out=Join-Path $artifacts ([Guid]::NewGuid().ToString('N')+'.out'); $err=Join-Path $artifacts ([Guid]::NewGuid().ToString('N')+'.err')
    $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer)+$Arguments
    $p=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    if (!$p.WaitForExit(180000)) { Stop-Process -Id $p.Id -Force; throw 'Disposable installer exceeded three minutes.' }
    [pscustomobject]@{ ExitCode=$p.ExitCode; Output=((Get-Content $out -Raw -ErrorAction SilentlyContinue)+' '+(Get-Content $err -Raw -ErrorAction SilentlyContinue)) }
}
try {
    $presence=[BellTower.Updates.UpdateTransaction]::Presence($paths); $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while (!(Test-Path $presence) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (!(Test-Path $presence)) { throw 'Initial disposable tray did not publish presence.' }
    $common=@('-Package',$package,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    $preflight=Invoke-Installer (@('-Action','Preflight')+$common); if ($preflight.ExitCode) { throw "Preflight failed: $($preflight.Output)" }
    $manifest=($preflight.Output|ConvertFrom-Json).operationManifest
    New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9001')|Out-Null
    $switch=Invoke-Installer (@('-Action','Switch','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common)
    if (!$switch.ExitCode -or !$switch.Output.Contains('restart of version 0.1.9001 refused')) { throw "Expected candidate restart failure did not occur: $($switch.Output)" }
    Remove-Item (Join-Path $barrier 'fail-start-0.1.9001')
    New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9003')|Out-Null
    $recovery=Invoke-Installer (@('-Action','Rollback','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common)
    if (!$recovery.ExitCode -or !$recovery.Output.Contains('restart of version 0.1.9003 refused')) { throw "Expected original restart failure did not occur during recovery: $($recovery.Output)" }
    $afterFailure=Get-Content $manifest -Raw|ConvertFrom-Json
    $pending=Join-Path $install 'updates/pending.json'; $restart=[BellTower.Updates.UpdateTransaction]::Marker($paths)
    $targetBefore=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version
    Remove-Item (Join-Path $barrier 'fail-start-0.1.9003')
    $retry=Invoke-Installer (@('-Action','Rollback','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common)
    $final=Get-Content $manifest -Raw|ConvertFrom-Json
    $result=[ordered]@{scenario='candidate restart failure -> recovery original restart failure -> same-manifest retry';recoveryExit=$recovery.ExitCode;phaseAfterRecovery=$afterFailure.coordinationPhase;phaseTargetAfterRecovery=$afterFailure.coordinationPhaseTarget;stateAfterRecovery=$afterFailure.state;selectedBeforeRetry=$targetBefore;retryExit=$retry.ExitCode;retryOutput=$retry.Output;selectedAfterRetry=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version;phaseAfterRetry=$final.coordinationPhase;pendingRemains=(Test-Path $pending);restartRemains=(Test-Path $restart);disposableRoot=$artifacts}
    $result|ConvertTo-Json -Depth 6|Set-Content (Join-Path $artifacts 'result.json'); $result|ConvertTo-Json -Depth 6
    if ($retry.ExitCode -ne 0 -or $targetBefore -ne '0.1.9003' -or !$afterFailure.coordinationPhase.Equals('Rollback') -or $afterFailure.coordinationPhaseTarget -ne '0.1.9003' -or (Test-Path $pending) -or (Test-Path $restart)) { throw 'Nested same-manifest recovery did not finish coherently.' }
} finally {
    try { $controller.RequestExit() } catch { }
    if ($initial -and !$initial.HasExited) { try { $initial.WaitForExit(5000)|Out-Null } catch { } }
}
