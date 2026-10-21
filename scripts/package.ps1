param([string]$OutputDirectory = 'artifacts/releases', [switch]$Offline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $version = ([xml](Get-Content -LiteralPath Directory.Build.props -Raw)).Project.PropertyGroup.Version
    $releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
    $publishRoot = Join-Path $releaseRoot "bell-tower-$version-win-x64"
    $restoreOptions = @('--locked-mode', '-r', 'win-x64', '-p:SelfContained=true')
    if ($Offline) { $restoreOptions += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
    dotnet restore src/BellTower/BellTower.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet publish src/BellTower/BellTower.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishRoot --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Stop a running copy in the target directory before packaging.' }
    $executable = Join-Path $publishRoot 'bell-tower.exe'
    if ((& $executable --version).Trim() -cne $version) { throw 'Packaged application version mismatch.' }
    New-Item -ItemType Directory -Path (Join-Path $publishRoot 'helpers') -Force | Out-Null
    Copy-Item -LiteralPath scripts/Invoke-BellTower.ps1 -Destination (Join-Path $publishRoot 'helpers')
    Copy-Item -LiteralPath README.md -Destination $publishRoot
    Copy-Item -LiteralPath docs -Destination $publishRoot -Recurse -Force
    @{ version = $version; runtime = 'win-x64'; selfContained = $true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishRoot 'release.json') -Encoding utf8
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
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $vsix) { Remove-Item -LiteralPath $vsix }
    $zip = [System.IO.Compression.ZipFile]::Open($vsix, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($source in @('package.json', 'extension.js', 'service.js', 'focus.js')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $projectRoot "vscode/$source"), "extension/$source") | Out-Null
        }
        foreach ($entry in @(@{ Name = 'extension.vsixmanifest'; Text = $manifest }, @{ Name = '[Content_Types].xml'; Text = $types })) {
            $stream = $zip.CreateEntry($entry.Name).Open()
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
            try { $writer.Write($entry.Text) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    $archive = Join-Path $releaseRoot "bell-tower-$version-win-x64.zip"
    $owned = @('bell-tower.exe', 'release.json', 'README.md', "bell-tower-focus-$version.vsix", 'docs', 'helpers') | ForEach-Object { Join-Path $publishRoot $_ }
    Compress-Archive -LiteralPath $owned -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ascii
    Write-Output "Packaged $archive"
} finally { Pop-Location }
