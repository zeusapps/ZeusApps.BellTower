param(
    [Parameter(Mandatory)][string]$BaselineInstallation,
    [Parameter(Mandatory)][string]$BaselineData,
    [Parameter(Mandatory)][string]$FixtureRoot,
    [string]$DelayedTrayExe,
    [switch]$UseDelayedTray
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$BaselineInstallation = [IO.Path]::GetFullPath($BaselineInstallation)
$BaselineData = [IO.Path]::GetFullPath($BaselineData)
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
if (!$FixtureRoot.StartsWith((Join-Path $artifacts 'qa-007') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'QA007 fixture roots must be new descendants of artifacts/qa-007-*.'
}
if (Test-Path -LiteralPath $FixtureRoot) { throw 'Use a new disposable fixture root for every scenario.' }
$baselineSelection = Get-Content -LiteralPath (Join-Path $BaselineInstallation 'current.json') -Raw | ConvertFrom-Json
$version = [string]$baselineSelection.version
$sourcePayload = Join-Path (Join-Path $BaselineInstallation 'versions') $version
if (!(Test-Path -LiteralPath (Join-Path $sourcePayload 'release.json'))) { throw 'Baseline selected payload contract is missing.' }
if (!(Test-Path -LiteralPath (Join-Path $BaselineData 'state.json'))) { throw 'Baseline data must have state.json.' }
$install = Join-Path $FixtureRoot 'install'
$data = Join-Path $FixtureRoot 'data'
$workspace = Join-Path $FixtureRoot 'workspace'
$control = Join-Path $FixtureRoot 'delayed-child-control'
New-Item -ItemType Directory -Path $FixtureRoot | Out-Null
Copy-Item -LiteralPath $BaselineInstallation -Destination $install -Recurse
Copy-Item -LiteralPath $BaselineData -Destination $data -Recurse
New-Item -ItemType Directory -Path $workspace,$control | Out-Null
if ($UseDelayedTray) {
    if (!$DelayedTrayExe -or !(Test-Path -LiteralPath $DelayedTrayExe -PathType Leaf)) { throw 'UseDelayedTray requires the QA005 self-hosted apphost.' }
    $payload = Join-Path (Join-Path $install 'versions') $version
    $target = Join-Path $payload 'bell-tower.exe'
    Copy-Item -LiteralPath $DelayedTrayExe -Destination $target
    $contractPath = Join-Path $payload 'release.json'
    $contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
    $contract.files.'bell-tower.exe' = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($contractPath, ($contract | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
}
$result = [ordered]@{
    label = if ($version -eq '0.1.8') { 'released-0.1.8 legacy-only compatibility fixture' } else { 'current-source-version capture-compatible disposable fixture' }
    installation = $install
    data = $data
    workspace = $workspace
    control = $control
    version = $version
    stableLauncherSha256 = (Get-FileHash -LiteralPath (Join-Path $install 'bell-tower.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    helperInjected = [bool]$UseDelayedTray
    helperImage = if ($UseDelayedTray) { Join-Path (Join-Path (Join-Path $install 'versions') $version) 'bell-tower.exe' } else { $null }
    stateSha256 = (Get-FileHash -LiteralPath (Join-Path $data 'state.json') -Algorithm SHA256).Hash.ToLowerInvariant()
}
$resultPath = Join-Path $FixtureRoot 'fixture.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output $resultPath
