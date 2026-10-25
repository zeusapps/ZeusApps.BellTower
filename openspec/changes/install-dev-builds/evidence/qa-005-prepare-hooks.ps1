param(
    [Parameter(Mandatory)][string]$FixtureFile,
    [Parameter(Mandatory)][string]$ScenarioRoot
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$fixture = Get-Content -LiteralPath $FixtureFile -Raw | ConvertFrom-Json
$ScenarioRoot = [IO.Path]::GetFullPath($ScenarioRoot)
if (!$ScenarioRoot.StartsWith((Join-Path $artifacts 'qa-005') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Hook homes and command results must stay in artifacts/qa-005-*.'
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
$paths.Ensure()
$launcher = Join-Path $install 'bell-tower.exe'
$setup = [BellTower.Setup.IntegrationSetup]::new($paths, $launcher, $homeRoot)
$setup.Plan('both').Apply($paths)
$selectedVersion = [BellTower.Distribution.Installation]::new($install).Selection().Version
$sourceExe = Join-Path (Join-Path (Join-Path $install 'versions') $selectedVersion) 'bell-tower.exe'
$provision = [BellTower.Setup.IngressProvision]::Plan($launcher, $paths)
if ($null -eq $provision) { throw 'Capture-compatible baseline did not produce a NativeAOT ingress provision plan.' }
$provision.Apply($paths)
$legacy = [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths)
$native = [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths, $provision.Target)
$result = [ordered]@{
    fixtureLabel = $fixture.label
    installation = $install
    data = $data
    home = $homeRoot
    selectedVersion = $selectedVersion
    ingress = $provision.Target
    ingressSha256 = $provision.Hash
    commands = [ordered]@{
        claudeLegacy = $legacy.Build('event', 'claude')
        codexLegacy = $legacy.Build('event', 'codex')
        claudeNative = $native.Build('event', 'claude')
        codexNative = $native.Build('event', 'codex')
    }
    providerHomes = @((Join-Path $homeRoot '.claude/settings.json'), (Join-Path $homeRoot '.codex/hooks.json'))
}
New-Item -ItemType Directory -Path $ScenarioRoot -Force | Out-Null
$resultPath = Join-Path $ScenarioRoot 'commands.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output $resultPath
