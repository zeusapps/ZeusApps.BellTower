param([Parameter(Mandatory)][string]$InstallationRoot,[Parameter(Mandatory)][string]$DataDirectory,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$install=[IO.Path]::GetFullPath($InstallationRoot);$data=[IO.Path]::GetFullPath($DataDirectory)
$selectionPath=Join-Path $install 'current.json';$launcher=Join-Path $install 'bell-tower.exe';$trayPath=Join-Path $data 'updates/tray.json'
$selection=Get-Content -LiteralPath $selectionPath -Raw|ConvertFrom-Json
$tray=Get-Content -LiteralPath $trayPath -Raw|ConvertFrom-Json
$pidValue=[int]$tray.process.pid;$ticks=[long]$tray.process.startedUtcTicks
$process=Get-CimInstance Win32_Process -Filter "ProcessId=$pidValue"
if ($null -eq $process) { throw 'Recorded maintainer tray process is absent; do not treat unknown presence as a matching owner.' }
$processStart=([datetime]$process.CreationDate).ToUniversalTime().Ticks
$payloads=@(Get-ChildItem -LiteralPath (Join-Path $install 'versions') -Directory | Sort-Object Name | ForEach-Object {
    $versionPath=$_.FullName
    $files=@(Get-ChildItem -LiteralPath $versionPath -File -Recurse | Sort-Object FullName | ForEach-Object { [ordered]@{relative=$_.FullName.Substring($versionPath.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
    [ordered]@{version=$_.Name;files=$files}
})
$result=[ordered]@{
    selectedVersion=[string]$selection.version
    selectionSha256=(Get-FileHash -LiteralPath $selectionPath -Algorithm SHA256).Hash.ToLowerInvariant()
    launcherSha256=(Get-FileHash -LiteralPath $launcher -Algorithm SHA256).Hash.ToLowerInvariant()
    payloads=$payloads
    trayRecordSha256=(Get-FileHash -LiteralPath $trayPath -Algorithm SHA256).Hash.ToLowerInvariant()
    trayRecordProcess=[ordered]@{pid=$pidValue;startedUtcTicks=$ticks}
    liveTray=[ordered]@{pid=$pidValue;creationUtcTicks=$processStart;image=(Split-Path -Leaf $process.ExecutablePath)}
    capturedUtc=[DateTimeOffset]::UtcNow.ToString('O')
}
[IO.File]::WriteAllText($OutputPath,($result|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
Write-Output $OutputPath
