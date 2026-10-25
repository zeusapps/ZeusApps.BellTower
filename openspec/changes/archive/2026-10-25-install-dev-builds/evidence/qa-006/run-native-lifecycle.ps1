param(
    [string]$CandidatePackage='artifacts/dev-builds/bell-tower-0.1.9005-win-x64.zip',
    [string]$RetirePackage='artifacts/dev-builds/bell-tower-0.1.9004-win-x64.zip'
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$artifacts=Join-Path $repo 'artifacts/qa-006'
$root=Join-Path $artifacts ('native-lifecycle-'+[guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'install';$data=Join-Path $root 'data';$workspace=Join-Path $root 'private';$logs=Join-Path $root 'logs';$assemblies=Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$base=Join-Path $env:LOCALAPPDATA 'Programs/ZeusApps.BellTower'
$original='0.1.10';$candidate='0.1.9005';$retire='0.1.9004'
New-Item -ItemType Directory -Path $root,$install,$data,$logs,(Join-Path $install 'versions'),(Join-Path $install 'updates'),(Join-Path $data 'updates') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $base 'bell-tower.exe') -Destination (Join-Path $install 'bell-tower.exe')
Copy-Item -LiteralPath (Join-Path $base "versions/$original") -Destination (Join-Path $install "versions/$original") -Recurse
$selection=[ordered]@{version=$original;previous=$null;activation=[guid]::NewGuid().ToString('N')}
[IO.File]::WriteAllText((Join-Path $install 'current.json'),($selection|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $install 'installation.json'),'{}',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $data 'state.json'),'{"schemaVersion":1,"sessions":{}}',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),' {"schemaVersion":1,"sessions":{},"qa":"original-backup"}',[Text.UTF8Encoding]::new($false))
foreach($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name))}
$paths=[BellTower.IO.DataPaths]::new($data);$controller=[BellTower.Tray.TrayController]::new($paths)
$presence=[BellTower.Updates.UpdateTransaction]::Presence($paths)
$pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'}
function Invoke-Installer([string]$Action,[string]$Manifest,[string]$Tag,[string]$Version=''){
    $out=Join-Path $logs ($Tag+'.out');$err=Join-Path $logs ($Tag+'.err')
    $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $repo 'scripts/install-dev.ps1'),'-Action',$Action,'-Package',(Join-Path $repo $CandidatePackage),'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    if($Manifest){$args+=@('-OperationManifest',$Manifest)};if($Version){$args+=@('-Version',$Version)}
    $proc=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    if(!$proc.WaitForExit(180000)){Stop-Process -Id $proc.Id -Force;throw "QA-owned installer $Tag timed out."}
    [pscustomobject]@{exitCode=$proc.ExitCode;stdout=(Get-Content $out -Raw -ErrorAction SilentlyContinue);stderr=(Get-Content $err -Raw -ErrorAction SilentlyContinue)}
}
$initial=$null;$result=$null
try{
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe'));$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    foreach($arg in @('tray','--data-dir',$data,'--silent')){[void]$start.ArgumentList.Add($arg)}
    $initial=[Diagnostics.Process]::Start($start)
    $deadline=[DateTime]::UtcNow.AddSeconds(30);while(!(Test-Path $presence)-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 50}
    if(!(Test-Path $presence)){throw 'Initial original tray failed to publish disposable presence.'}
    $beforeLauncher=(Get-FileHash (Join-Path $install 'bell-tower.exe') -Algorithm SHA256).Hash
    $pre=Invoke-Installer 'Preflight' '' 'preflight'
    if($pre.exitCode-ne0){throw ('Preflight failed: '+$pre.stdout+$pre.stderr)}
    $manifest=($pre.stdout|ConvertFrom-Json).operationManifest
    $sw=Invoke-Installer 'Switch' $manifest 'switch'
    if($sw.exitCode-ne0){throw ('Switch failed: '+$sw.stdout+$sw.stderr)}
    $selectedAfterSwitch=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version
    $swOutput=$sw.stdout|ConvertFrom-Json
    if($selectedAfterSwitch-ne$candidate){throw 'Switch did not select current candidate version.'}
    $rb=Invoke-Installer 'Rollback' $manifest 'rollback'
    if($rb.exitCode-ne0){throw ('Rollback failed: '+$rb.stdout+$rb.stderr)}
    $selectedAfterRollback=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version
    if($selectedAfterRollback-ne$original){throw 'Rollback did not restore original selection.'}
    $staging=Join-Path $root 'retire-package';Expand-Archive -LiteralPath (Join-Path $repo $RetirePackage) -DestinationPath $staging
    $retireSource=Join-Path $staging "versions/$retire";$retireTarget=Join-Path $install "versions/$retire"
    if(!(Test-Path (Join-Path $retireSource 'release.json'))){throw 'Retire control package lacks dev contract.'}
    Copy-Item -LiteralPath $retireSource -Destination $retireTarget -Recurse
    $ret=Invoke-Installer 'Retire' '' 'retire' $retire
    if($ret.exitCode-ne0){throw ('Explicit Retire failed: '+$ret.stdout+$ret.stderr)}
    $preflightStatus=($pre.stdout|ConvertFrom-Json).status
    $result=[ordered]@{kind='installed native, synthetic disposable install/data';originalVersion=$original;candidateVersion=$candidate;retiredVersion=$retire;initialTrayPublished=$true;preflight=$preflightStatus;switchExit=$sw.exitCode;selectedAfterSwitch=$selectedAfterSwitch;switchTrayRestarted=$swOutput.trayRestarted;rollbackExit=$rb.exitCode;selectedAfterRollback=$selectedAfterRollback;retireExit=$ret.exitCode;retiredPayloadAbsent=!(Test-Path $retireTarget);stableLauncherUnchanged=((Get-FileHash (Join-Path $install 'bell-tower.exe') -Algorithm SHA256).Hash-eq$beforeLauncher);candidateRetained=(Test-Path (Join-Path $install "versions/$candidate"));originalRetained=(Test-Path (Join-Path $install "versions/$original"));pendingAbsent=!(Test-Path (Join-Path $install 'updates/pending.json'));restartAbsent=!(Test-Path ([BellTower.Updates.UpdateTransaction]::Marker($paths)));fixtureRoot=$root}
    [IO.File]::WriteAllText((Join-Path $root 'lifecycle-results.json'),($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $result|ConvertTo-Json -Depth 8
}finally{
    try{$controller.RequestExit()}catch{}
    $limit=[DateTime]::UtcNow.AddSeconds(15)
    while ((Test-Path $presence) -and ([DateTime]::UtcNow -lt $limit)) { try { $owner=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096);$identity=[BellTower.Core.ProcessIdentity]::new($owner.Process.Pid,$owner.Process.StartedUtcTicks);if([BellTower.IO.ProcessProbe]::Inspect($identity)-eq[BellTower.IO.ProcessLiveness]::Ended){break} } catch {} ; Start-Sleep -Milliseconds 100 }
    if($initial){$initial.Dispose()}
}
