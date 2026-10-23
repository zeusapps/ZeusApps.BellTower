param([string]$OutputDirectory = 'artifacts/releases', [switch]$Offline, [string]$Version, [string]$FixtureVersion)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $projectVersion = ([xml](Get-Content -LiteralPath Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if ($Version -and $Version -cne $projectVersion) { throw 'Tag/application version mismatch.' }
    $version = $projectVersion
    $releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
    if ($FixtureVersion) {
        $fixtureRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/update-acceptance')) + [System.IO.Path]::DirectorySeparatorChar
        if (!$releaseRoot.StartsWith($fixtureRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture packages must stay under artifacts/update-acceptance/.' }
        $version = $FixtureVersion
    }
    if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected MAJOR.MINOR.PATCH.' }
    $extension = Get-Content -LiteralPath vscode/package.json -Raw | ConvertFrom-Json
    if (!$FixtureVersion -and $extension.version -cne $version) { throw 'Extension/application version mismatch.' }
    $installationRoot = Join-Path $releaseRoot "bell-tower-$version-win-x64"
    $publishRoot = Join-Path $installationRoot "versions/$version"
    $restoreOptions = @('--locked-mode', '-r', 'win-x64', '-p:SelfContained=true')
    if ($Offline) { $restoreOptions += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
    dotnet restore src/BellTower/BellTower.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet restore src/BellTower.Launcher/BellTower.Launcher.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Launcher locked restore failed.' }
    dotnet restore src/BellTower.Ingress/BellTower.Ingress.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Ingress locked restore failed.' }
    dotnet publish src/BellTower/BellTower.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true "-p:Version=$version" -o $publishRoot --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Stop a running copy in the target directory before packaging.' }
    $executable = Join-Path $publishRoot 'bell-tower.exe'
    dotnet publish src/BellTower.Ingress/BellTower.Ingress.csproj -c Release -r win-x64 --self-contained true "-p:Version=$version" -o (Join-Path $installationRoot 'ingress-publish') --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'NativeAOT ingress publish failed; install the Windows C++ toolchain. No managed fallback is packaged.' }
    Copy-Item -LiteralPath (Join-Path $installationRoot 'ingress-publish/bell-tower-ingress.exe') -Destination $publishRoot
    $probe = & (Join-Path $publishRoot 'bell-tower-ingress.exe') --protocol-info
    if ($LASTEXITCODE -ne 0 -or $probe.Trim() -cne '{"protocol":1,"minimumConsumer":1,"maximumConsumer":1}') { throw 'Ingress protocol probe failed.' }
    dotnet publish src/BellTower.Launcher/BellTower.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true "-p:Version=$version" -o $installationRoot --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed.' }
    New-Item -ItemType Directory -Path (Join-Path $publishRoot 'helpers') -Force | Out-Null
    Copy-Item -LiteralPath scripts/Invoke-BellTower.ps1 -Destination (Join-Path $publishRoot 'helpers')
    Copy-Item -LiteralPath README.md -Destination $publishRoot
    Copy-Item -LiteralPath docs -Destination $publishRoot -Recurse -Force
    $vsix = Join-Path $publishRoot "bell-tower-focus-$version.vsix"
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata><Identity Language="en-US" Id="bell-tower" Version="$version" Publisher="zeusapps" />
  <DisplayName>Bell Tower Focus</DisplayName><Description xml:space="preserve">Local terminal registration and focusing.</Description>
  <Properties><Property Id="Microsoft.VisualStudio.Code.Engine" Value="^1.90.0" /></Properties></Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code" /></Installation><Dependencies />
  <Assets><Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" /></Assets>
</PackageManifest>
"@
    $types = '<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="json" ContentType="application/json" /><Default Extension="js" ContentType="application/javascript" /><Default Extension="vsixmanifest" ContentType="text/xml" /></Types>'
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $vsix) { Remove-Item -LiteralPath $vsix }
    $zip = [System.IO.Compression.ZipFile]::Open($vsix, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($source in @('extension.js', 'service.js', 'focus.js')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $projectRoot "vscode/$source"), "extension/$source") | Out-Null
        }
        $extension.version = $version
        foreach ($entry in @(@{ Name = 'extension/package.json'; Text = ($extension | ConvertTo-Json -Depth 20) }, @{ Name = 'extension.vsixmanifest'; Text = $manifest }, @{ Name = '[Content_Types].xml'; Text = $types })) {
            $stream = $zip.CreateEntry($entry.Name).Open()
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
            try { $writer.Write($entry.Text) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    $inventory = [ordered]@{}
    $ownedNames = @('bell-tower.exe', 'bell-tower-ingress.exe', 'README.md', "bell-tower-focus-$version.vsix", 'helpers/Invoke-BellTower.ps1')
    $ownedNames += Get-ChildItem -LiteralPath docs -File -Recurse | ForEach-Object { 'docs/' + [System.IO.Path]::GetRelativePath((Join-Path $projectRoot 'docs'), $_.FullName).Replace('\', '/') }
    foreach ($name in $ownedNames) { $inventory[$name] = (Get-FileHash -LiteralPath (Join-Path $publishRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant() }
    $contract = [ordered]@{ version = $version; runtime = 'win-x64'; selfContained = $true; layout = 1; bootstrap = 1; state = 1; runtimeRecords = 1; shell = 1; bridge = 1; capture = 1; files = $inventory }
    $contract | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $publishRoot 'release.json') -Encoding utf8
    @{ layout = 1; bootstrap = 1 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installationRoot 'installation.json') -Encoding utf8
    @{ version = $version } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installationRoot 'current.json') -Encoding utf8
    $launcher = Join-Path $installationRoot 'bell-tower.exe'
    if ((& $launcher --version).Trim() -cne $version -or $LASTEXITCODE -ne 0) { throw 'Packaged launcher/application version mismatch.' }
    $archive = Join-Path $releaseRoot "bell-tower-$version-win-x64.zip"
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
    $releaseZip = [System.IO.Compression.ZipFile]::Open($archive, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @('bell-tower.exe', 'installation.json', 'current.json')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($releaseZip, (Join-Path $installationRoot $name), $name) | Out-Null
        }
        foreach ($name in @($ownedNames) + @('release.json')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($releaseZip, (Join-Path $publishRoot $name), "versions/$version/$name") | Out-Null
        }
    } finally { $releaseZip.Dispose() }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ascii
    @{ contract = $contract; archiveHash = $hash } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath ($archive.Substring(0, $archive.Length - 4) + '.release.json') -Encoding utf8
    Write-Output "Packaged $archive"
} finally { Pop-Location }
