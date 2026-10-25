param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $Repo ('artifacts/implementation-006-receipt-' + [Guid]::NewGuid().ToString('N'))
$install = Join-Path $artifacts 'install'; $data = Join-Path $artifacts 'data'; $workspace = Join-Path $artifacts 'private'; $barrier = Join-Path $artifacts 'barriers'
$source = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9003-win-x64'; $package = Join-Path $Repo 'artifacts/dev-builds/bell-tower-0.1.9001-win-x64.zip'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'; $installer = Join-Path $Repo 'scripts/install-dev.ps1'
if (!(Test-Path $source) -or !(Test-Path $package)) { throw 'Required disposable native packages are unavailable.' }
New-Item -ItemType Directory -Path $artifacts,$install,$data,$barrier | Out-Null
New-Item -ItemType File -Path (Join-Path $barrier 'switch-before-selection.release'),(Join-Path $barrier 'hold-candidate-presence-before-receipt') | Out-Null
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $install -Recurse -Force
[IO.File]::WriteAllText((Join-Path $data 'state.json'),'{"schemaVersion":1,"sessions":{}}'); [IO.File]::WriteAllText((Join-Path $data 'state.json.bak'),'{"schemaVersion":1,"sessions":{}}')
foreach ($name in @('BellTower.Distribution.dll','BellTower.Protocol.dll','bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
$paths=[BellTower.IO.DataPaths]::new($data); $controller=[BellTower.Tray.TrayController]::new($paths)
$launcher=Join-Path $install 'bell-tower.exe'; $start=[Diagnostics.ProcessStartInfo]::new($launcher); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
foreach($arg in @('tray','--data-dir',$data,'--silent')){[void]$start.ArgumentList.Add($arg)}; $initial=[Diagnostics.Process]::Start($start)
function Invoke-Installer([string[]]$Arguments) {
    $pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'}
    $out=Join-Path $artifacts ([Guid]::NewGuid().ToString('N')+'.out'); $err=Join-Path $artifacts ([Guid]::NewGuid().ToString('N')+'.err')
    $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer)+$Arguments
    $p=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    if(!$p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw 'Disposable installer exceeded three minutes.'}
    [pscustomobject]@{ExitCode=$p.ExitCode;Output=((Get-Content $out -Raw -ErrorAction SilentlyContinue)+' '+(Get-Content $err -Raw -ErrorAction SilentlyContinue))}
}
try {
    $presence=[BellTower.Updates.UpdateTransaction]::Presence($paths); $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while(!(Test-Path $presence)-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 50}; if(!(Test-Path $presence)){throw 'Initial disposable tray did not publish presence.'}
    $common=@('-Package',$package,'-InstallationRoot',$install,'-DataDirectory',$data,'-Workspace',$workspace,'-AssemblyDirectory',$assemblies,'-Disposable','-AllowStaleBuild')
    $preflight=Invoke-Installer (@('-Action','Preflight')+$common); if($preflight.ExitCode){throw "Preflight failed: $($preflight.Output)"}; $manifest=($preflight.Output|ConvertFrom-Json).operationManifest
    $pwsh=Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if(!(Test-Path $pwsh)){$pwsh='pwsh.exe'}
    $out=Join-Path $artifacts 'interrupted-switch.out'; $err=Join-Path $artifacts 'interrupted-switch.err'; $args=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$installer,'-Action','Switch','-OperationManifest',$manifest,'-TestBarrierDirectory',$barrier)+$common
    $interrupted=Start-Process -FilePath $pwsh -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    $interruptedIdentity=[BellTower.IO.ProcessProbe]::Identify($interrupted.Id); $entered=Join-Path $barrier 'candidate-presence-before-receipt.entered'; $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!(Test-Path $entered)-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Milliseconds 50}
    if(!(Test-Path $entered)){if(!$interrupted.HasExited){$interrupted.Kill();$interrupted.WaitForExit()};throw 'Switch did not reach the presence-before-receipt barrier.'}
    $cim=Get-CimInstance Win32_Process -Filter "ProcessId=$($interrupted.Id)" -Property ProcessId,CommandLine
    if(!$cim -or !$cim.CommandLine.Contains($installer,[StringComparison]::OrdinalIgnoreCase) -or [BellTower.IO.ProcessProbe]::Identify($interrupted.Id) -ne $interruptedIdentity){throw 'Refusing to interrupt a process that is not the exact disposable installer.'}
    $interrupted.Kill(); $interrupted.WaitForExit(10000)|Out-Null
    if([BellTower.IO.ProcessProbe]::Inspect($interruptedIdentity)-ne[BellTower.IO.ProcessLiveness]::Ended){throw 'Disposable installer process did not end after exact interruption.'}
    $saved=Get-Content $manifest -Raw|ConvertFrom-Json; $owner=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096)
    $pending=Join-Path $install 'updates/pending.json'; $restart=[BellTower.Updates.UpdateTransaction]::Marker($paths)
    $retry=Invoke-Installer (@('-Action','Rollback','-OperationManifest',$manifest)+$common)
    $selected=(Get-Content (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).version
    $restored=[BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence,4096)
    $result=[ordered]@{scenario='candidate tray publishes presence, installer ends before candidateTray receipt, same-manifest Rollback';interruptedInstallerPid=$interruptedIdentity.Pid;interruptedInstallerStartedTicks=$interruptedIdentity.StartedUtcTicks;restartLauncher=$saved.restartLauncher;candidateTrayReceiptPresent=($saved.PSObject.Properties.Name -contains 'candidateTray');candidatePresencePid=$owner.Process.Pid;candidatePresenceVersion=$owner.Version;candidatePresenceActivation=$owner.Activation;candidatePresenceDataDirectory=$owner.DataDirectory;retryExit=$retry.ExitCode;retryOutput=$retry.Output;selectedAfterRetry=$selected;restoredTrayVersion=$restored.Version;pendingRemains=(Test-Path $pending);restartRemains=(Test-Path $restart);disposableRoot=$artifacts}
    $result|ConvertTo-Json -Depth 6|Set-Content (Join-Path $artifacts 'result.json'); $result|ConvertTo-Json -Depth 6
    if($retry.ExitCode -ne 0 -or $saved.PSObject.Properties.Name -contains 'candidateTray' -or $selected -ne '0.1.9003' -or $restored.Version -ne '0.1.9003' -or (Test-Path $pending) -or (Test-Path $restart)){throw 'Interrupted candidate receipt recovery did not complete safely.'}
} finally {
    try{$controller.RequestExit()}catch{}
    if($initial -and !$initial.HasExited){try{$initial.WaitForExit(5000)|Out-Null}catch{}}
}
