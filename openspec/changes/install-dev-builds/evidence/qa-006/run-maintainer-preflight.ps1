$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$evidence=$PSScriptRoot
$install=Join-Path $env:LOCALAPPDATA 'Programs/ZeusApps.BellTower'
$data=Join-Path $env:LOCALAPPDATA 'ZeusApps.BellTower'
$package=Join-Path $repo 'artifacts/dev-builds/bell-tower-0.1.9005-win-x64.zip'
$workspace=Join-Path (Join-Path $repo 'artifacts/qa-006') ('maintainer-preflight-'+[guid]::NewGuid().ToString('N'))
$before=Join-Path $evidence 'maintainer-before.json';$after=Join-Path $evidence 'maintainer-after.json'
& (Join-Path $evidence 'maintainer-snapshot.ps1') -InstallationRoot $install -DataDirectory $data -OutputPath $before
$raw=& (Join-Path $repo 'scripts/install-dev.ps1') -Action Preflight -Package $package -InstallationRoot $install -DataDirectory $data -Workspace $workspace -AssemblyDirectory (Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows') 2>&1 | Out-String
$preflight=$raw|ConvertFrom-Json
if ($preflight.status -ne 'preflight-passed') { throw "Read-only Preflight did not pass: $($preflight.status)" }
& (Join-Path $evidence 'maintainer-snapshot.ps1') -InstallationRoot $install -DataDirectory $data -OutputPath $after
$b=Get-Content -LiteralPath $before -Raw|ConvertFrom-Json;$a=Get-Content -LiteralPath $after -Raw|ConvertFrom-Json
$payloadsSame=(ConvertTo-Json $b.payloads -Depth 12 -Compress) -ceq (ConvertTo-Json $a.payloads -Depth 12 -Compress)
$trayRecordSame=($b.trayRecordProcess.pid -eq $a.trayRecordProcess.pid) -and ($b.trayRecordProcess.startedUtcTicks -eq $a.trayRecordProcess.startedUtcTicks)
$trayProcessSame=($b.liveTray.pid -eq $a.liveTray.pid) -and ($b.liveTray.creationUtcTicks -eq $a.liveTray.creationUtcTicks) -and ($b.liveTray.image -ceq $a.liveTray.image)
$result=[ordered]@{
    status=$preflight.status
    selectedBefore=$b.selectedVersion;selectedAfter=$a.selectedVersion
    selectionHashUnchanged=($b.selectionSha256 -ceq $a.selectionSha256)
    launcherHashUnchanged=($b.launcherSha256 -ceq $a.launcherSha256)
    payloadHashCount=@($b.payloads|ForEach-Object{$_.files.Count}|Measure-Object -Sum).Sum
    allPayloadHashesUnchanged=$payloadsSame
    trayRecordIdentityUnchanged=$trayRecordSame
    trayLiveIdentityUnchanged=$trayProcessSame
    trayPid=$b.trayRecordProcess.pid
    trayStartedUtcTicks=$b.trayRecordProcess.startedUtcTicks
}
[IO.File]::WriteAllText((Join-Path $evidence 'maintainer-preflight-result.json'),($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Compress
if (!$result.selectionHashUnchanged -or !$result.launcherHashUnchanged -or !$result.allPayloadHashesUnchanged -or !$result.trayRecordIdentityUnchanged -or !$result.trayLiveIdentityUnchanged) { exit 1 }
