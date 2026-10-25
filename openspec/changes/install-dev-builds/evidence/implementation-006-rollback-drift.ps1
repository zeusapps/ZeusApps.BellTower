param([string]$Repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference='Stop'
$root=Join-Path $Repo ('artifacts/implementation-006/rollback-drift-'+[Guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'install';$data=Join-Path $root 'data';$workspace=Join-Path $root 'private';$logs=Join-Path $root 'logs'
New-Item -ItemType Directory -Path $root,$install,$data,$logs|Out-Null
$liveRoot=Join-Path $env:LOCALAPPDATA 'Programs/ZeusApps.BellTower'
Copy-Item -LiteralPath (Join-Path $liveRoot 'bell-tower.exe') -Destination (Join-Path $install 'bell-tower.exe')
New-Item -ItemType Directory -Path (Join-Path $install 'versions')|Out-Null
Copy-Item -LiteralPath (Join-Path $liveRoot 'versions/0.1.10') -Destination (Join-Path $install 'versions/0.1.10') -Recurse
New-Item -ItemType Directory -Path (Join-Path $install 'updates')|Out-Null
New-Item -ItemType Directory -Path (Join-Path $data 'updates')|Out-Null
$selection=[ordered]@{version='0.1.10';previous=$null;activation=[Guid]::NewGuid().ToString('N')}
[IO.File]::WriteAllText((Join-Path $install 'current.json'),($selection|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $install 'installation.json'),'{}',[Text.UTF8Encoding]::new($false))
$originalState='{"schemaVersion":1,"sessions":{}}';$originalBackup='{"schemaVersion":1,"sessions":{},"qaOriginalBackup":true}'
[IO.File]::WriteAllText((Join-Path $data 'state.json'),$originalState,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),$originalBackup,[Text.UTF8Encoding]::new($false))
$assembly=Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
foreach($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $assembly $name))}
function Invoke-Installer([string]$Action,[string]$PackagePath,[string]$ManifestPath,[string]$Tag,[string]$BarrierPath){
  $shell=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $shell)){$shell='pwsh.exe'}
  $stdout=Join-Path $logs ($Tag+'.out');$stderr=Join-Path $logs ($Tag+'.err')
  $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $Repo 'scripts/install-dev.ps1'),'-Action',$Action,'-Package',$PackagePath,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-Disposable','-AllowStaleBuild')
  if($ManifestPath){$args+=@('-OperationManifest',$ManifestPath)};if($BarrierPath){$args+=@('-TestBarrierDirectory',$BarrierPath)}
  $p=Start-Process -FilePath $shell -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
  if(!$p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw "QA disposable installer $Tag timed out."}
  $out=if(Test-Path $stdout){Get-Content $stdout -Raw}else{''};$err=if(Test-Path $stderr){Get-Content $stderr -Raw}else{''}
  return [pscustomobject]@{Exit=$p.ExitCode;Output=$out;Error=$err}
}
$launcher=Join-Path $install 'bell-tower.exe';$start=[Diagnostics.ProcessStartInfo]::new($launcher);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;foreach($arg in @('tray','--data-dir',$data,'--silent')){[void]$start.ArgumentList.Add($arg)};$initial=[Diagnostics.Process]::Start($start)
$paths=[BellTower.IO.DataPaths]::new($data);$presence=[BellTower.Updates.UpdateTransaction]::Presence($paths);$limit=[DateTime]::UtcNow.AddSeconds(30);while(!(Test-Path $presence)-and[DateTime]::UtcNow-lt$limit){Start-Sleep -Milliseconds 50};if(!(Test-Path $presence)){throw 'Synthetic original tray did not register.'}
$package=Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9004-win-x64.zip'
$preflight=Invoke-Installer 'Preflight' $package '' 'preflight' '';if($preflight.Exit -ne 0){throw ('Synthetic Preflight failed: '+$preflight.Output+$preflight.Error)};$manifest=($preflight.Output|ConvertFrom-Json).operationManifest
$switch=Invoke-Installer 'Switch' $package $manifest 'switch' '';if($switch.Exit -ne 0){throw ('Synthetic Switch failed: '+$switch.Output+$switch.Error)}
$manifestBefore=Get-Content $manifest -Raw|ConvertFrom-Json;$selectionPath=Join-Path $install 'current.json';$selectedBefore=Get-FileHash $selectionPath -Algorithm SHA256;$backupPath=Join-Path $data 'state.json.bak';$candidateBackup='{"schemaVersion":1,"sessions":{},"qaCandidateBackup":true}';[IO.File]::WriteAllText($backupPath,$candidateBackup,[Text.UTF8Encoding]::new($false));$candidateBackupHash=(Get-FileHash $backupPath -Algorithm SHA256).Hash
# Hold the setup/state lock so Rollback can reach its slow preparation wait before records drift.
$held=[BellTower.IO.DataLock]::new($paths,'setup')
try{
  $rollbackShell=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $rollbackShell)){$rollbackShell='pwsh.exe'}
  $rollbackOut=Join-Path $logs 'rollback-drift.out';$rollbackErr=Join-Path $logs 'rollback-drift.err'
  $rollbackArgs=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $Repo 'scripts/install-dev.ps1'),'-Action','Rollback','-Package',$package,'-OperationManifest',$manifest,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-Disposable','-AllowStaleBuild')
  $rollbackProcess=Start-Process -FilePath $rollbackShell -ArgumentList $rollbackArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $rollbackOut -RedirectStandardError $rollbackErr
  $pending=Join-Path $install 'updates/pending.json';$restart=[BellTower.Updates.UpdateTransaction]::Marker($paths);$deadline=[DateTime]::UtcNow.AddSeconds(10)
  while((!(Test-Path $pending)-or!(Test-Path $restart))-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 20}
  if(!(Test-Path $pending)-or!(Test-Path $restart)){throw 'Rollback did not publish both markers before the isolated lock probe deadline.'}
  $presenceRecord=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096);$trayIdentity=[BellTower.Core.ProcessIdentity]::new($presenceRecord.Process.Pid,$presenceRecord.Process.StartedUtcTicks);$deadline=[DateTime]::UtcNow.AddSeconds(15)
  while([BellTower.IO.ProcessProbe]::Inspect($trayIdentity)-eq[BellTower.IO.ProcessLiveness]::Live-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 20}
  if([BellTower.IO.ProcessProbe]::Inspect($trayIdentity)-ne[BellTower.IO.ProcessLiveness]::Ended){throw 'Rollback had not stopped its exact disposable tray.'}
  $pendingBytesBefore=[IO.File]::ReadAllBytes($pending);$restartBytesBefore=[IO.File]::ReadAllBytes($restart);$marker=Get-Content $restart -Raw|ConvertFrom-Json
  [IO.File]::AppendAllText($restart,"`n");$restartDriftHash=(Get-FileHash $restart -Algorithm SHA256).Hash
  Start-Sleep -Milliseconds 200
}finally{$held.Dispose()}
if(!$rollbackProcess.WaitForExit(30000)){Stop-Process -Id $rollbackProcess.Id -Force;throw 'Rollback child did not finish after releasing the synthetic setup lock.'}
$rollbackOutput=if(Test-Path $rollbackOut){Get-Content $rollbackOut -Raw}else{''};$rollbackError=if(Test-Path $rollbackErr){Get-Content $rollbackErr -Raw}else{''}
$afterManifest=Get-Content $manifest -Raw|ConvertFrom-Json;$afterSelection=Get-FileHash $selectionPath -Algorithm SHA256;$afterBackupHash=(Get-FileHash $backupPath -Algorithm SHA256).Hash
$result=[ordered]@{
  scenario='marker byte drift during normal Rollback after tray stop while disposable setup lock held'
  exit=$rollbackProcess.ExitCode; expectedFailure=($rollbackProcess.ExitCode-ne0)
  refusalMentionsDrift=($rollbackOutput+$rollbackError -match '(?i)changed bytes|drifted')
  selectionUnchanged=($selectedBefore.Hash-eq$afterSelection.Hash); selectionSha256Before=$selectedBefore.Hash; selectionSha256After=$afterSelection.Hash
  selectedVersion=(Get-Content $selectionPath -Raw|ConvertFrom-Json).version
  stateBackupSha256Before=$candidateBackupHash; stateBackupSha256After=$afterBackupHash
  monitoringBackupRestored=($afterBackupHash-ne$candidateBackupHash-and$afterBackupHash-eq(Get-FileHash (Join-Path (Split-Path -Parent $manifest) 'before-monitoring/state.json.bak') -Algorithm SHA256).Hash)
  candidateBackupWasDifferent=($candidateBackupHash-ne(Get-FileHash (Join-Path (Split-Path -Parent $manifest) 'before-monitoring/state.json.bak') -Algorithm SHA256).Hash)
  manifestState=$afterManifest.state
  restartMarkerStillExists=(Test-Path $restart); restartMarkerSha256Before=$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($restartBytesBefore)).ToLowerInvariant()); restartMarkerDriftHash=$restartDriftHash
  pendingMarkerStillExists=(Test-Path $pending); pendingMarkerSha256Before=$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($pendingBytesBefore)).ToLowerInvariant()); pendingMarkerSha256After=(Get-FileHash $pending -Algorithm SHA256).Hash
  trayExited=([BellTower.IO.ProcessProbe]::Inspect($trayIdentity)-eq[BellTower.IO.ProcessLiveness]::Ended)
  limitedLog=($rollbackOutput+$rollbackError -replace '(?s).*?(An operation coordination record changed bytes.*|An operation coordination record drifted.*|The operation .*?)$','$1')
}
$result|ConvertTo-Json|Set-Content (Join-Path $root 'result.json');$result|ConvertTo-Json
