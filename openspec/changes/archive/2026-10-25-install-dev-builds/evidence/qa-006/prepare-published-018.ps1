param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$DataSeed,
    [Parameter(Mandatory)][string]$FixtureRoot
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
if (!$FixtureRoot.StartsWith((Join-Path $artifacts 'qa-006') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'The published package fixture must stay under artifacts/qa-006/.' }
if (Test-Path -LiteralPath $FixtureRoot) { throw 'Use a new empty fixture path; the released archive is never overlaid on a capture-1 fixture.' }
$archive = Join-Path $PublishedDirectory 'bell-tower-0.1.8-win-x64.zip'
$sidecar = $archive + '.sha256'
$descriptorPath = Join-Path $PublishedDirectory 'bell-tower-0.1.8-win-x64.release.json'
foreach ($path in @($archive,$sidecar,$descriptorPath)) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing published release input: $path" } }
$expected = ((Get-Content -LiteralPath $sidecar -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$descriptor = Get-Content -LiteralPath $descriptorPath -Raw | ConvertFrom-Json
if ($expected -ne $actual -or $descriptor.archiveHash.ToLowerInvariant() -ne $actual -or $descriptor.contract.version -ne '0.1.8') { throw 'Published 0.1.8 archive hash/descriptor mismatch; refusing capture-zero fixture.' }
if (!(Test-Path -LiteralPath (Join-Path $DataSeed 'state.json') -PathType Leaf)) { throw 'A synthetic or otherwise explicitly approved disposable data seed is required.' }
$install = Join-Path $FixtureRoot 'install'
$data = Join-Path $FixtureRoot 'data'
New-Item -ItemType Directory -Path $install,$data -Force | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $install
Copy-Item -LiteralPath $DataSeed -Destination $data -Recurse
if ((Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw | ConvertFrom-Json).version -ne '0.1.8') { throw 'Extracted package is not selected at released 0.1.8.' }
foreach ($relative in $descriptor.contract.files.PSObject.Properties.Name) {
    $path = Join-Path (Join-Path $install 'versions/0.1.8') $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $descriptor.contract.files.$relative.ToLowerInvariant()) { throw "Published payload member hash mismatch: $relative" }
}
$result = [ordered]@{fixtureKind='actual published release archive';releaseVersion='0.1.8';captureGeneration=0;legacyOnly=$true;archiveSha256=$actual;descriptorArchiveHash=$descriptor.archiveHash;installation=$install;data=$data;warning='Do not run capture-1 provisioning in this fixture. Generate legacy commands with the extracted 0.1.8 executable after stable-target go.'}
[IO.File]::WriteAllText((Join-Path $FixtureRoot 'fixture.json'),($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $FixtureRoot 'fixture.json')
