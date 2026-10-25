param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$source = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64'; $package = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9001-win-x64.zip'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'; $installer = Join-Path $Repo 'scripts/install-dev.ps1'
if (!(Test-Path $source) -or !(Test-Path $package)) { throw 'Required disposable native packages are unavailable.' }
foreach ($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
function Invoke-Installer([string]$Artifacts,[string[]]$Arguments) {
    $pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'}
    $out=Join-Path $Artifacts ([Guid]::NewGuid().ToString('N')+'.out');$err=Join-Path $Artifacts ([Guid]::NewGuid().ToString('N')+'.err')
    $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer)+$Arguments
    $p=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    if(!$p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw 'Disposable installer exceeded three minutes.'}
    [pscustomobject]@{ExitCode=$p.ExitCode;Output=((Get-Content $out -Raw -ErrorAction SilentlyContinue)+' '+(Get-Content $err -Raw -ErrorAction SilentlyContinue))}
}
function Invoke-TransitionScenario([string]$Stage,[bool]$Drift=$false) {
    $root=Join-Path $Repo ('artifacts/implementation-006-transition-'+$Stage+'-'+[Guid]::NewGuid().ToString('N'));$install=Join-Path $root 'install';$data=Join-Path $root 'data';$workspace=Join-Path $root 'private';$barrier=Join-Path $root 'barrier'
    New-Item -ItemType Directory -Path $root,$install,$data,$barrier|Out-Null
    New-Item -ItemType File -Path (Join-Path $barrier 'switch-before-selection.release'),(Join-Path $barrier 'rollback-before-selection.release'),(Join-Path $barrier ('hold-'+$Stage))|Out-Null
    Get-ChildItem $source|Copy-Item -Destination $install -Recurse -Force
    [IO.File]::WriteAllText((Join-Path $data 'state.json'),'{"schemaVersion":1,"sessions":{}}');[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),'{"schemaVersion":1,"sessions":{}}')
    $paths=[BellTower.IO.DataPaths]::new($data);$controller=[BellTower.Tray.TrayController]::new($paths);$launcher=Join-Path $install 'bell-tower.exe'
    $start=[Diagnostics.ProcessStartInfo]::new($launcher);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;foreach($arg in @('tray','--data-dir',$data,'--silent')){[void]$start.ArgumentList.Add($arg)};$initial=[Diagnostics.Process]::Start($start)
    try {
        $presence=[BellTower.Updates.UpdateTransaction]::Presence($paths);$deadline=[DateTime]::UtcNow.AddSeconds(30);while(!(Test-Path $presence)-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 50};if(!(Test-Path $presence)){throw 'Initial disposable tray did not publish presence.'}
        $common=@('-Package',$package,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
        $preflight=Invoke-Installer $root (@('-Action','Preflight')+$common);if($preflight.ExitCode){throw "Preflight failed: $($preflight.Output)"};$manifest=($preflight.Output|ConvertFrom-Json).operationManifest
        New-Item -ItemType File -Path (Join-Path $barrier 'fail-start-0.1.9001')|Out-Null
        $switch=Invoke-Installer $root (@('-Action','Switch','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common);if(!$switch.ExitCode-or!$switch.Output.Contains('restart of version 0.1.9001 refused')){throw 'Candidate restart failure was not injected.'}
        Remove-Item (Join-Path $barrier 'fail-start-0.1.9001')
        $pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe';if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'};$out=Join-Path $root 'transition.out';$err=Join-Path $root 'transition.err'
        $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer,'-Action','Rollback','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common
        $proc=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err;$identity=[BellTower.IO.ProcessProbe]::Identify($proc.Id)
        $entered=Join-Path $barrier ($Stage+'.entered');$deadline=[DateTime]::UtcNow.AddSeconds(90);while(!(Test-Path $entered)-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 50}
        if(!(Test-Path $entered)){if(!$proc.HasExited){$proc.Kill();$proc.WaitForExit()};throw "Recovery did not reach $Stage."}
        $cim=Get-CimInstance Win32_Process -Filter "ProcessId=$($proc.Id)" -Property ProcessId,CommandLine;if(!$cim.CommandLine.Contains($installer,[StringComparison]::OrdinalIgnoreCase)-or[BellTower.IO.ProcessProbe]::Identify($proc.Id)-ne$identity){throw 'Refusing to stop a process other than the exact disposable installer.'}
        $proc.Kill();$proc.WaitForExit(10000)|Out-Null
        $pending=Join-Path $install 'updates/pending.json';$restart=[BellTower.Updates.UpdateTransaction]::Marker($paths)
        $planPending=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($pending,16384);$planRestart=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.UpdatePlan]($restart,16384)
        $pendingHash=(Get-FileHash $pending -Algorithm SHA256).Hash;$restartHash=(Get-FileHash $restart -Algorithm SHA256).Hash
        $driftHash=$null
        if($Drift){
            switch($Stage){
                'transition-before-first-write' { [IO.File]::AppendAllText($restart,"`n") }
                'transition-between-records' { $foreign=Get-Content $restart -Raw|ConvertFrom-Json; $foreign|Add-Member -NotePropertyName foreignField -NotePropertyValue 'unowned' -Force; [IO.File]::WriteAllText($restart,($foreign|ConvertTo-Json -Depth 20 -Compress)) }
                'transition-pair-verified' { [IO.File]::AppendAllText($pending,"`n") }
            }
            $driftPath=if($Stage -eq 'transition-pair-verified'){$pending}else{$restart};$driftHash=(Get-FileHash $driftPath -Algorithm SHA256).Hash
        }
        $retry=Invoke-Installer $root (@('-Action','Rollback','-OperationManifest',$manifest)+$common)
        $currentPending=Test-Path $pending;$currentRestart=Test-Path $restart;$selected=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version
        $driftPreserved=$false;if($Drift){$driftPreserved=(Get-FileHash $driftPath -Algorithm SHA256).Hash -eq $driftHash}
        $result=[ordered]@{stage=$Stage;foreignDriftInjected=$Drift;interruptedInstallerPid=$identity.Pid;pendingTargetBeforeRetry=$planPending.Target;restartTargetBeforeRetry=$planRestart.Target;pendingHashBeforeRetry=$pendingHash;restartHashBeforeRetry=$restartHash;retryExit=$retry.ExitCode;retryOutput=$retry.Output;selectedAfterRetry=$selected;pendingRemains=$currentPending;restartRemains=$currentRestart;foreignDriftPreserved=$driftPreserved;disposableRoot=$root}
        $result|ConvertTo-Json -Depth 5|Set-Content (Join-Path $root 'result.json')
        if($Drift){if(!$retry.ExitCode-or$selected-ne'0.1.9001'-or!$currentPending-or!$currentRestart-or!$driftPreserved){throw "Foreign drift at $Stage was not refused and preserved."}}
        elseif($retry.ExitCode-or$selected-ne'0.1.9003'-or$currentPending-or$currentRestart){throw "Recovery after $Stage did not complete."}
        return $result
    } finally { try{$controller.RequestExit()}catch{};if($initial-and!$initial.HasExited){try{$initial.WaitForExit(5000)|Out-Null}catch{}} }
}
$results=@(
    Invoke-TransitionScenario 'transition-before-first-write'
    Invoke-TransitionScenario 'transition-between-records'
    Invoke-TransitionScenario 'transition-pair-verified'
    Invoke-TransitionScenario 'transition-before-first-write' $true
    Invoke-TransitionScenario 'transition-between-records' $true
    Invoke-TransitionScenario 'transition-pair-verified' $true
)
$results|ConvertTo-Json -Depth 6
