param([string]$CandidatePackage='artifacts/dev-builds/bell-tower-0.1.9005-win-x64.zip')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$artifacts=Join-Path $repo 'artifacts/qa-006'
$root=Join-Path $artifacts ('four-phase-'+[guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'install';$data=Join-Path $root 'data';$private=Join-Path $root 'private';$logs=Join-Path $root 'logs';$assemblies=Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows';$barriers=Join-Path $root 'barriers';$hooks=Join-Path $root 'hooks'
$base=Join-Path $env:LOCALAPPDATA 'Programs/ZeusApps.BellTower';$versions=@('0.1.9001','0.1.9002','0.1.9003','0.1.9004');$candidate='0.1.9005';$original='0.1.10'
New-Item -ItemType Directory -Path $root,$install,$data,$logs,$barriers,(Join-Path $install 'versions'),(Join-Path $install 'updates'),(Join-Path $data 'updates') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $base 'bell-tower.exe') -Destination (Join-Path $install 'bell-tower.exe')
Copy-Item -LiteralPath (Join-Path $base "versions/$original") -Destination (Join-Path $install "versions/$original") -Recurse
[IO.File]::WriteAllText((Join-Path $install 'installation.json'),'{}',[Text.UTF8Encoding]::new($false))
$selection=[ordered]@{version=$original;previous='0.1.9002';activation=[guid]::NewGuid().ToString('N')}
[IO.File]::WriteAllText((Join-Path $install 'current.json'),($selection|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $data 'state.json'),'{"schemaVersion":1,"sessions":{}}',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),' {"schemaVersion":1,"sessions":{},"qa":"original-backup"}',[Text.UTF8Encoding]::new($false))
foreach($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name))}
$packagePaths=@{}
foreach($version in $versions){
    $zip=Join-Path $repo "artifacts/dev-builds/bell-tower-$version-win-x64.zip"
    if(!(Test-Path $zip)){throw "Required seeded dev archive is absent: $version"}
    $stage=Join-Path $root ("stage-$version");Expand-Archive -LiteralPath $zip -DestinationPath $stage
    Copy-Item -LiteralPath (Join-Path $stage "versions/$version") -Destination (Join-Path $install "versions/$version") -Recurse
    $packagePaths[$version]=$zip
}
$fixture=[ordered]@{label='capture-1 current-source fixture';installation=$install;data=$data;version=$original}
[IO.File]::WriteAllText((Join-Path $root 'fixture.json'),($fixture|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$commandsFile=& (Join-Path $PSScriptRoot 'prepare-hooks.ps1') -FixtureFile (Join-Path $root 'fixture.json') -ScenarioRoot $hooks
$commands=Get-Content $commandsFile -Raw|ConvertFrom-Json
$paths=[BellTower.IO.DataPaths]::new($data);$controller=[BellTower.Tray.TrayController]::new($paths);$presence=[BellTower.Updates.UpdateTransaction]::Presence($paths)
$pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'}
$installer=Join-Path $repo 'scripts/install-dev.ps1';$results=[Collections.Generic.List[object]]::new();$initial=$null;$lease=$null
function Start-DisposableTray {
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe'));$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    foreach($arg in @('tray','--data-dir',$data,'--silent')){[void]$start.ArgumentList.Add($arg)}
    [Diagnostics.Process]::Start($start)
}
function Wait-Presence([string]$Version,[int]$Seconds=30){
    $deadline=[DateTime]::UtcNow.AddSeconds($Seconds)
    while([DateTime]::UtcNow-lt$deadline){
        if(Test-Path $presence){try{$r=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096);if($r.Version-eq$Version-and$r.DataDirectory-eq$data-and[BellTower.IO.ProcessProbe]::IsAlive($r.Process)){return $r}}catch{}}
        Start-Sleep -Milliseconds 40
    }
    throw "No exact live disposable $Version tray presence appeared."
}
function Invoke-InstallerPhase([string]$Phase,[string]$Manifest,[string]$BarrierName){
    $barrier=Join-Path $barriers ($Phase+'-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $barrier|Out-Null
    $out=Join-Path $logs ($Phase+'.out');$err=Join-Path $logs ($Phase+'.err')
    $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer,'-Action',$Phase,'-Package',(Join-Path $repo $CandidatePackage),'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$private,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild','-TestBarrierDirectory',$barrier)
    if($Manifest){$args+=@('-OperationManifest',$Manifest)}
    $proc=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    try{
        $entered=Join-Path $barrier ($BarrierName+'.entered');$deadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!(Test-Path $entered)-and!$proc.HasExited-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 30}
        if(!(Test-Path $entered)){throw "$Phase did not enter $BarrierName (exit=$($proc.ExitCode))."}
        $matrix=Join-Path $root "$Phase-hooks.json"
        & (Join-Path $PSScriptRoot 'run-phase-hook-matrix.ps1') -CommandsFile $commandsFile -BarrierDirectory $barrier -Phase $Phase -BarrierName $BarrierName -ResultFile $matrix -MinimumBlockedMilliseconds 3500
        if(!(Get-Content $matrix -Raw|ConvertFrom-Json).complete){throw "$Phase hook matrix failed."}
    }finally{
        $release=Join-Path $barrier ($BarrierName+'.release')
        if(!(Test-Path $release)){[IO.File]::WriteAllText($release,[DateTimeOffset]::UtcNow.ToString('O'),[Text.UTF8Encoding]::new($false))}
    }
    if(!$proc.WaitForExit(90000)){try{$proc.Kill($true);$proc.WaitForExit()}catch{};throw "$Phase installer failed to finish after barrier release."}
    $output=Get-Content $out -Raw -ErrorAction SilentlyContinue;$errorText=Get-Content $err -Raw -ErrorAction SilentlyContinue
    $results.Add([ordered]@{phase=$Phase;exitCode=$proc.ExitCode;output=$output;error=$errorText;matrix=Get-Content $matrix -Raw|ConvertFrom-Json})
    if($proc.ExitCode-ne0){throw "$Phase failed after hook matrix: $output $errorText"}
    return $output
}
function Invoke-RemovalPhase([string]$Phase,[string]$Version){
    $barrier=Join-Path $barriers ($Phase+'-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $barrier|Out-Null
    $ut=[BellTower.Updates.UpdateTransaction].Assembly.GetType('BellTower.Updates.UpdateTransaction')
    $prop=$ut.GetProperty('PayloadRemovalTestCheckpoint',[Reflection.BindingFlags]'Static,NonPublic')
    if(!$prop){throw 'Release build lacks disposable payload-removal checkpoint.'}
    if(!$([System.Management.Automation.PSTypeName]'Qa006FileBarrier').Type){Add-Type -Path (Join-Path $PSScriptRoot 'FileBarrier.cs')}
    $prop.SetValue($null,[Qa006FileBarrier]::Create($install,$Version,$Phase,$barrier,90))
    $worker=[powershell]::Create();$task=$null;$workerError=$null
    $action=@'
param($InstallRoot,$PayloadVersion,$Action)
$i=[BellTower.Distribution.Installation]::new($InstallRoot)
if($Action -eq 'Retire'){$r=[BellTower.Updates.UpdateTransaction]::Retire($i,$PayloadVersion);if($r){throw $r}}else{[BellTower.Updates.UpdateTransaction]::Cleanup($i)}
'@
    $null=$worker.AddScript($action).AddArgument($install).AddArgument($Version).AddArgument($Phase);$task=$worker.BeginInvoke()
    try{
        $entered=Join-Path $barrier ($Phase+'.entered');$deadline=[DateTime]::UtcNow.AddSeconds(20)
        while(!(Test-Path $entered)-and!$task.IsCompleted-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 20}
        if(!(Test-Path $entered)){throw "$Phase did not reach payload-removal checkpoint."}
        $matrix=Join-Path $root "$Phase-hooks.json"
        & (Join-Path $PSScriptRoot 'run-phase-hook-matrix.ps1') -CommandsFile $commandsFile -BarrierDirectory $barrier -Phase $Phase -ResultFile $matrix -MinimumBlockedMilliseconds 3500
        if(!(Get-Content $matrix -Raw|ConvertFrom-Json).complete){throw "$Phase hook matrix failed."}
    }finally{
        $release=Join-Path $barrier ($Phase+'.release')
        if(!(Test-Path $release)){[IO.File]::WriteAllText($release,[DateTimeOffset]::UtcNow.ToString('O'),[Text.UTF8Encoding]::new($false))}
        if($task){$task.AsyncWaitHandle.WaitOne([TimeSpan]::FromSeconds(60))|Out-Null;try{$worker.EndInvoke($task)|Out-Null}catch{$workerError=$_.Exception.Message}}
        $prop.SetValue($null,$null);$worker.Dispose()
    }
    if($workerError){throw "$Phase worker failed: $workerError"}
    $results.Add([ordered]@{phase=$Phase;payloadVersion=$Version;workerCompleted=$task.IsCompleted;payloadRemoved=!(Test-Path (Join-Path $install "versions/$Version"));matrix=Get-Content $matrix -Raw|ConvertFrom-Json})
}
try{
    $initial=Start-DisposableTray;[void](Wait-Presence $original)
    $preOut=& $installer -Action Preflight -Package (Join-Path $repo $CandidatePackage) -InstallationRoot $install -DataDirectory $data -Workspace $private -AssemblyDirectory $assemblies -Disposable -AllowStaleBuild 2>&1|Out-String
    $pre=$preOut|ConvertFrom-Json;if($pre.status-ne'preflight-passed'){throw "Preflight failed: $preOut"};$manifest=$pre.operationManifest
    [void](Invoke-InstallerPhase 'Switch' $manifest 'switch-before-selection');[void](Wait-Presence $candidate)
    [void](Invoke-InstallerPhase 'Rollback' $manifest 'rollback-before-selection');[void](Wait-Presence $original)
    # Successful Rollback legitimately cleans unretained test payloads. Re-seed
    # separate controls after that lifecycle so each later retention reason is isolated.
    foreach($version in $versions){$target=Join-Path $install "versions/$version";if(!(Test-Path $target)){Copy-Item -LiteralPath (Join-Path (Join-Path $root "stage-$version") "versions/$version") -Destination $target -Recurse}}
    # Retention seed: selected 0.1.10, previous 0.1.9002, recovery original 0.1.9003,
    # recovery target 0.1.9002, leased 0.1.9001; 9004 and 9005 are removable controls.
    $retained=[ordered]@{version=$original;previous='0.1.9002';activation=[guid]::NewGuid().ToString('N')}
    [IO.File]::WriteAllText((Join-Path $install 'current.json'),($retained|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
    $recoveryRoot=Join-Path $install 'updates/qa-recovery';New-Item -ItemType Directory -Path $recoveryRoot -Force|Out-Null
    $plan=[ordered]@{id='qa006-retention';token='qa006-retention-token';root=$install;dataDirectory=$data;original='0.1.9003';target='0.1.9002';contractHash=(Get-FileHash (Join-Path $install 'versions/0.1.9002/release.json') -Algorithm SHA256).Hash.ToLowerInvariant();created=[DateTimeOffset]::UtcNow.ToString('O');owner=[ordered]@{pid=$PID;startedUtcTicks=([BellTower.IO.ProcessProbe]::Identify($PID)).StartedUtcTicks};hadTray=$false}
    [IO.File]::WriteAllText((Join-Path $recoveryRoot 'plan.json'),($plan|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
    $stageCandidate=Join-Path $root 'stage-current';Expand-Archive -LiteralPath (Join-Path $repo $CandidatePackage) -DestinationPath $stageCandidate
    if(!(Test-Path (Join-Path $install "versions/$candidate"))){Copy-Item (Join-Path $stageCandidate "versions/$candidate") -Destination (Join-Path $install "versions/$candidate") -Recurse}
    $lease=[BellTower.Distribution.Installation]::new($install).Lease('0.1.9001')
    [void](Invoke-RemovalPhase 'Retire' '0.1.9004')
    [void](Invoke-RemovalPhase 'Cleanup' $candidate)
    $result=[ordered]@{kind='generated provider-shell and NativeAOT helpers; synthetic install/data; own disposable tray';phases=@($results);leaseTarget='0.1.9001';leasedPayloadRetained=(Test-Path (Join-Path $install 'versions/0.1.9001'));previousVersionRetained=(Test-Path (Join-Path $install 'versions/0.1.9002'));recoveryPayloadRetained=(Test-Path (Join-Path $install 'versions/0.1.9003'));selectedOriginalRetained=(Test-Path (Join-Path $install "versions/$original"));retireTargetAbsent=!(Test-Path (Join-Path $install 'versions/0.1.9004'));cleanupTargetAbsent=!(Test-Path (Join-Path $install "versions/$candidate"));fixtureRoot=$root}
    [IO.File]::WriteAllText((Join-Path $root 'four-phase-results.json'),($result|ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false));$result|ConvertTo-Json -Depth 16
}finally{
    if($lease){$lease.Dispose()}
    try{$controller.RequestExit()}catch{}
    $limit=[DateTime]::UtcNow.AddSeconds(15)
    while ((Test-Path $presence) -and ([DateTime]::UtcNow -lt $limit)) { try { $owner=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096);$identity=[BellTower.Core.ProcessIdentity]::new($owner.Process.Pid,$owner.Process.StartedUtcTicks);if([BellTower.IO.ProcessProbe]::Inspect($identity)-eq[BellTower.IO.ProcessLiveness]::Ended){break} } catch {} ; Start-Sleep -Milliseconds 100 }
    if($initial){$initial.Dispose()}
}
