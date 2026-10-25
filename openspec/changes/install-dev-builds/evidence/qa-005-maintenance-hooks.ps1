param([string]$Repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference='Stop'
$root=Join-Path $Repo ('artifacts/qa-005/maintenance-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $root|Out-Null
$baselineData=Join-Path $root 'baseline-data';New-Item -ItemType Directory -Path $baselineData|Out-Null;[IO.File]::WriteAllText((Join-Path $baselineData 'state.json'),'{"schemaVersion":1,"sessions":{}}');[IO.File]::WriteAllText((Join-Path $baselineData 'state.json.bak'),'{"schemaVersion":1,"sessions":{}}')
$fixtureFile=Join-Path $root 'fixture.json';$fixtureRoot=Join-Path $root 'fixture';& (Join-Path $PSScriptRoot 'qa-005-fixture.ps1') -BaselineInstallation (Join-Path $env:LOCALAPPDATA 'Programs/ZeusApps.BellTower') -BaselineData $baselineData -FixtureRoot $fixtureRoot|Out-Null
$fixture=Get-Content $fixtureFile -Raw -ErrorAction SilentlyContinue;if(!$fixture){$fixturePath=Join-Path $fixtureRoot 'fixture.json';$fixture=Get-Content $fixturePath -Raw};$fixture=$fixture|ConvertFrom-Json;$install=$fixture.installation;$data=$fixture.data
$stage=Join-Path $root 'stage';Expand-Archive -LiteralPath (Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9004-win-x64.zip') -DestinationPath $stage
$staleVersion='0.1.9004';$stale=Join-Path $install ('versions/'+$staleVersion);if(Test-Path $stale){throw 'Chosen stale dev payload unexpectedly already exists in disposable clone.'};Copy-Item -LiteralPath (Join-Path $stage ('versions/'+$staleVersion)) -Destination $stale -Recurse
$slowFile=Join-Path $stale 'qa-slow-hash.fixture';[IO.File]::WriteAllText($slowFile,'disposable only');$contractPath=Join-Path $stale 'release.json';$contract=Get-Content $contractPath -Raw|ConvertFrom-Json;Add-Member -InputObject $contract.files -MemberType NoteProperty -Name 'qa-slow-hash.fixture' -Value ((Get-FileHash $slowFile -Algorithm SHA256).Hash.ToLowerInvariant()) -Force;[IO.File]::WriteAllText($contractPath,($contract|ConvertTo-Json -Depth 32),[Text.UTF8Encoding]::new($false))
$assembly=Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows';foreach($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $assembly $name))}
$fixtureJsonPath=Join-Path $fixtureRoot 'fixture.json';$commandsPath=& (Join-Path $PSScriptRoot 'qa-005-prepare-hooks.ps1') -FixtureFile $fixtureJsonPath -ScenarioRoot (Join-Path $root 'hooks')|Select-Object -Last 1
$commands=Get-Content $commandsPath -Raw|ConvertFrom-Json
$testAssembly=[BellTower.Updates.UpdateTransaction].Assembly;$updateType=$testAssembly.GetType('BellTower.Updates.UpdateTransaction');$prop=$updateType.GetProperty('PayloadRemovalTestCheckpoint',[Reflection.BindingFlags]'Static,NonPublic');if(!$prop){throw 'QA test checkpoint unavailable in current Release assembly.'}
$results=[Collections.Generic.List[object]]::new()
function Get-Counts { $capt=Join-Path $data 'captures-v1';$inbox=Join-Path $data 'inbox';return [ordered]@{captures=if(Test-Path $capt){@(Get-ChildItem $capt -Filter '*.json' -File -Recurse).Count}else{0};inbox=if(Test-Path $inbox){@(Get-ChildItem $inbox -Filter '*.json' -File -Recurse).Count}else{0}} }
function Invoke-Phase([string]$Phase,[string]$Version,[scriptblock]$Action){
  $barrier=Join-Path $root ($Phase+'-barrier');New-Item -ItemType Directory -Path $barrier|Out-Null;[IO.File]::WriteAllText((Join-Path $barrier ($Phase+'.entered')),'entered')
  $entered=[Threading.ManualResetEventSlim]::new($false);$release=[Threading.ManualResetEventSlim]::new($false)
  $callback=[Action[string,string]]{param($candidateRoot,$candidateVersion) if($candidateRoot.Equals($install,[StringComparison]::OrdinalIgnoreCase)-and$candidateVersion-eq$Version){$entered.Set();if(!$release.Wait([TimeSpan]::FromSeconds(60))){throw 'QA release deadline expired.'}}}
  $prop.SetValue($null,$callback);$timer=[Diagnostics.Stopwatch]::StartNew();$worker=[powershell]::Create();$null=$worker.AddScript($Action.ToString()).AddArgument($install).AddArgument($Version);$task=$worker.BeginInvoke()
  try{
    if(!$entered.Wait([TimeSpan]::FromSeconds(10))){throw "$Phase did not enter payload removal verification."}
    $before=Get-Counts
    foreach($trial in @('LegacyClaude','LegacyCodex','LegacyMixed','NativeMixed')){
      $resultPath=Join-Path $root ($Phase+'-'+$trial+'.json');if(Test-Path $resultPath){throw 'Trial result exists.'}
      $runner=Join-Path $Repo 'openspec/changes/install-dev-builds/evidence/qa-005-hook-trial.ps1'
      $trialOutput=& $runner -CommandsFile $commandsPath -ResultFile $resultPath -Trial $trial -BarrierDirectory $barrier -BarrierPhase $Phase -BudgetMilliseconds 3000
      $trialResult=Get-Content $resultPath -Raw|ConvertFrom-Json
      $results.Add([ordered]@{phase=$Phase;trial=$trial;hookResults=@($trialResult.hooks);barrierStillActive=[bool]$trialResult.barrierStillActiveAfterHooks;durableCaptures=$trialResult.durableCaptureDelta;durableInbox=$trialResult.durableInboxDelta})
      if($LASTEXITCODE-ne0-or$trialResult.failed-ne0-or!$trialResult.barrierStillActiveAfterHooks){throw "$Phase/$trial hook check failed while maintenance was blocked."}
    }
    if($timer.Elapsed.TotalSeconds-lt3){Start-Sleep -Milliseconds ([int](3000-$timer.ElapsedMilliseconds+50))}
    if($task.IsCompleted){throw "$Phase returned before the release signal."}
    $release.Set();$task.WaitHandle.WaitOne([TimeSpan]::FromSeconds(60))|Out-Null;if(!$task.IsCompleted){throw "$Phase did not finish after releasing the barrier."};$worker.EndInvoke($task)|Out-Null;$timer.Stop()
    return [ordered]@{phase=$Phase;durationMilliseconds=$timer.ElapsedMilliseconds;before=$before;after=(Get-Counts);maintenanceCompleted=$true;payloadRemoved=!(Test-Path (Join-Path $install ('versions/'+$Version)))}
  }finally{$release.Set();$prop.SetValue($null,$null);if($task -and !$task.IsCompleted){try{$task.WaitHandle.WaitOne([TimeSpan]::FromSeconds(60))|Out-Null;$worker.EndInvoke($task)|Out-Null}catch{}};if($worker){$worker.Dispose()};$entered.Dispose();$release.Dispose()}
}
$installation=[BellTower.Distribution.Installation]::new($install)
$retire=Invoke-Phase 'Retire' $staleVersion {param($InstallRoot,$PayloadVersion) $localInstallation=[BellTower.Distribution.Installation]::new($InstallRoot);[void][BellTower.Updates.UpdateTransaction]::Retire($localInstallation,$PayloadVersion)}
$sourceOld=Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64.zip';$stageOld=Join-Path $root 'stage-old';Expand-Archive -LiteralPath $sourceOld -DestinationPath $stageOld;$cleanupVersion='0.1.9003';$cleanupTarget=Join-Path $install ('versions/'+$cleanupVersion);if(Test-Path $cleanupTarget){throw 'Cleanup target unexpectedly exists in disposable clone.'};Copy-Item -LiteralPath (Join-Path $stageOld ('versions/'+$cleanupVersion)) -Destination $cleanupTarget -Recurse
$cleanup=Invoke-Phase 'Cleanup' $cleanupVersion {param($InstallRoot,$PayloadVersion) $localInstallation=[BellTower.Distribution.Installation]::new($InstallRoot);[BellTower.Updates.UpdateTransaction]::Cleanup($localInstallation)}
[ordered]@{mode='generated shell hooks and installed native helper on synthetic data; production settings were not read';providers=@('Claude Code','Codex');phases=@($retire,$cleanup);trials=$results;fixtureRoot=$root}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $root 'maintenance-results.json')
Get-Content -LiteralPath (Join-Path $root 'maintenance-results.json')
