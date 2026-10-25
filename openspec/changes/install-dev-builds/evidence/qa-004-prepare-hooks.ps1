param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][string]$ScenarioRoot
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repository 'artifacts'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$ScenarioRoot = [IO.Path]::GetFullPath($ScenarioRoot)
if (!$ScenarioRoot.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'QA data must stay in a new artifacts/qa-004-* scenario root.'
}
if (Test-Path -LiteralPath $ScenarioRoot) { throw "Use a new scenario directory: $ScenarioRoot" }
if (!(Test-Path -LiteralPath (Join-Path $PackageRoot 'installation.json'))) { throw 'PackageRoot must be the extracted package layout, not the zip file.' }
$assemblyRoot = Join-Path $repository 'src/BellTower/bin/Release/net10.0-windows'
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblyRoot $name))
}
$fixtureHome = Join-Path $ScenarioRoot 'home'
$data = Join-Path $ScenarioRoot "Δ O'Brien/data"
$paths = [BellTower.IO.DataPaths]::new($data)
$paths.Ensure()
$integration = [BellTower.Setup.IntegrationSetup]::new($paths, (Join-Path $PackageRoot 'bell-tower.exe'), $fixtureHome)
$integration.Plan('both').Apply($paths)
$provision = [BellTower.Setup.IngressProvision]::Plan((Join-Path $PackageRoot 'bell-tower.exe'), $paths)
if ($null -eq $provision) { throw 'Versioned package did not produce an ingress plan.' }
$provision.Apply($paths)
$launcher = Join-Path $PackageRoot 'bell-tower.exe'
$legacy = [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths)
$fast = [BellTower.Setup.HookCommandBuilder]::new($launcher, $paths, $provision.Target)
$result = [ordered]@{
    packageRoot = $PackageRoot
    packageVersion = [BellTower.Distribution.Installation]::new($PackageRoot).Selection().Version
    home = $fixtureHome
    data = $data
    ingress = $provision.Target
    ingressSha256 = $provision.Hash
    commands = [ordered]@{
        claudeLegacy = $legacy.Build('event', 'claude')
        codexLegacy = $legacy.Build('event', 'codex')
        claudeNative = $fast.Build('event', 'claude')
        codexNative = $fast.Build('event', 'codex')
    }
    providerHomes = @((Join-Path $fixtureHome '.claude/settings.json'), (Join-Path $fixtureHome '.codex/hooks.json'))
}
$resultPath = Join-Path $ScenarioRoot 'commands.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 8))
Write-Output $resultPath
