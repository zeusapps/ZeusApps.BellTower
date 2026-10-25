param(
    [Parameter(Mandatory)][string]$FixtureFile,
    [Parameter(Mandatory)][string]$ScenarioRoot,
    [switch]$LegacyOnly0_1_8
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$fixture = Get-Content -LiteralPath $FixtureFile -Raw | ConvertFrom-Json
$ScenarioRoot = [IO.Path]::GetFullPath($ScenarioRoot)
if (!$ScenarioRoot.StartsWith((Join-Path $artifacts 'qa-006') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Hook homes and command results must stay in artifacts/qa-006-*.'
}
if (Test-Path -LiteralPath $ScenarioRoot) { throw 'Use a new, empty scenario root.' }
$install = [IO.Path]::GetFullPath($fixture.installation)
$data = [IO.Path]::GetFullPath($fixture.data)
if (!(Test-Path -LiteralPath (Join-Path $install 'current.json')) -or !(Test-Path -LiteralPath (Join-Path $data 'state.json'))) {
    throw 'Fixture installation or data root is incomplete.'
}
$assemblyRoot = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblyRoot $name))
}
$homeRoot = Join-Path $ScenarioRoot 'fake-home'
$paths = [BellTower.IO.DataPaths]::new($data)
$launcher = Join-Path $install 'bell-tower.exe'
$provision = $null
if (!$LegacyOnly0_1_8) {
    $setup = [BellTower.Setup.IntegrationSetup]::new($paths, $launcher, $homeRoot)
    $setup.Plan('both').Apply($paths)
} else {
    [IO.Directory]::CreateDirectory($data) | Out-Null
}
$selectedVersion = [BellTower.Distribution.Installation]::new($install).Selection().Version
$captureZero = $selectedVersion -eq '0.1.8'
if ($LegacyOnly0_1_8 -and !$captureZero) { throw '-LegacyOnly0_1_8 requires selected release 0.1.8.' }
if ($captureZero -and !$LegacyOnly0_1_8) { throw 'Release 0.1.8 must use the separate capture-zero legacy-only fixture.' }
$sourceExe = Join-Path (Join-Path (Join-Path $install 'versions') $selectedVersion) 'bell-tower.exe'
$legacy = [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths)
$provision = if ($captureZero) { $null } else { [BellTower.Setup.IngressProvision]::Plan($launcher, $paths) }
if (!$captureZero -and $null -eq $provision) { throw 'Capture-compatible baseline did not produce a NativeAOT ingress provision plan.' }
if ($provision) { $provision.Apply($paths) }
$native = if ($provision) { [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths, $provision.Target) } else { $null }
$result = [ordered]@{
    fixtureLabel = $fixture.label
    installation = $install
    data = $data
    home = $homeRoot
    selectedVersion = $selectedVersion
    ingress = if ($provision) { $provision.Target } else { $null }
    ingressSha256 = if ($provision) { $provision.Hash } else { $null }
    commands = [ordered]@{
        claudeLegacy = $legacy.Build('event', 'claude')
        codexLegacy = $legacy.Build('event', 'codex')
        claudeNative = if ($native) { $native.Build('event', 'claude') } else { $null }
        codexNative = if ($native) { $native.Build('event', 'codex') } else { $null }
    }
    providerHomes = @((Join-Path $homeRoot '.claude/settings.json'), (Join-Path $homeRoot '.codex/hooks.json'))
}
New-Item -ItemType Directory -Path $ScenarioRoot -Force | Out-Null
$resultPath = Join-Path $ScenarioRoot 'commands.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output $resultPath
