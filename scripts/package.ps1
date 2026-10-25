param(
    [string]$OutputDirectory = 'artifacts/releases', [switch]$Offline, [string]$Version, [string]$FixtureVersion,
    [switch]$Dev, [string]$DevVersion,
    # Installation whose versions/ folder is consulted when choosing a free dev number (default: the standard per-user installation). Read-only.
    [string]$TargetInstallation = (Join-Path $env:LOCALAPPDATA 'Programs\ZeusApps.BellTower'),
    # Optional explicit vswhere.exe; by default it is found next to the Visual Studio Installer or on PATH.
    [string]$VsWherePath,
    # Print the version, output folder and build metadata without building (used by tests and for previews).
    [switch]$PlanOnly,
    # Locate and validate the native toolchain, then stop.
    [switch]$CheckToolchain
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$devPatchStart = 9000

function Test-DevVersion([string]$Text) { return $Text -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(\d+)$' -and [int64]$Matches[3] -ge $devPatchStart }

function Find-NativeToolchain {
    # NativeAOT linking calls vswhere.exe, which is not on PATH in a normal shell. Find it ourselves; the caller prepends its folder for child processes only.
    $candidates = @()
    if ($VsWherePath) { $candidates += $VsWherePath }
    elseif (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe') }
    if (!$VsWherePath) {
        $onPath = Get-Command vswhere.exe -ErrorAction SilentlyContinue
        if ($onPath) { $candidates += $onPath.Source }
    }
    $vswhere = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    if (!$vswhere) { throw 'vswhere.exe was not found. Install the Visual Studio Build Tools with the "Desktop development with C++" workload (MSVC x64/x86 build tools) and a Windows SDK; NativeAOT cannot link without them and no managed ingress is packaged.' }
    $instance = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or !$instance) { throw 'No Visual Studio instance has the MSVC x64/x86 build tools component (Microsoft.VisualStudio.Component.VC.Tools.x86.x64). Install the Build Tools "Desktop development with C++" workload; no managed ingress is packaged.' }
    $kits = $null
    foreach ($key in @('HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots')) {
        $value = (Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue).KitsRoot10
        if ($value) { $kits = $value; break }
    }
    if (!$kits -or !(Get-ChildItem -Path (Join-Path $kits 'Lib\*\um\x64\kernel32.lib') -ErrorAction SilentlyContinue)) { throw 'The Windows SDK (um/x64 libraries) was not found. Install a Windows 10/11 SDK; NativeAOT cannot link without it and no managed ingress is packaged.' }
    return [pscustomobject]@{ VsWhere = $vswhere; Instance = ($instance | Select-Object -First 1); WindowsSdk = $kits }
}

function Get-TakenDevPatches([string]$Major, [string]$Minor, [string[]]$Folders) {
    $taken = [System.Collections.Generic.HashSet[int64]]::new()
    foreach ($folder in $Folders) {
        if (!(Test-Path -LiteralPath $folder -PathType Container)) { continue }
        foreach ($entry in Get-ChildItem -LiteralPath $folder) {
            $name = $entry.Name -replace '^bell-tower-', '' -replace '-win-x64.*$', ''
            if ($name -match "^$Major\.$Minor\.(\d+)$" -and [int64]$Matches[1] -ge $devPatchStart) { [void]$taken.Add([int64]$Matches[1]) }
        }
    }
    return ,$taken
}

Push-Location $projectRoot
$savedPath = $env:PATH
try {
    $projectVersion = ([xml](Get-Content -LiteralPath Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if ($Dev -and ($FixtureVersion -or $Version)) { throw '-Dev cannot be combined with -Version or -FixtureVersion.' }
    if ($DevVersion -and !$Dev) { throw '-DevVersion needs -Dev.' }
    if ($Version -and (Test-DevVersion $Version)) { throw "Version $Version is in the reserved development range (PATCH >= $devPatchStart); stable packages cannot use it. Use -Dev for development builds." }
    if ($Version -and $Version -cne $projectVersion) { throw 'Tag/application version mismatch.' }
    $version = $projectVersion
    $devBuildsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/dev-builds'))
    if ($Dev) {
        if (!$PSBoundParameters.ContainsKey('OutputDirectory')) { $OutputDirectory = 'artifacts/dev-builds' }
        $requested = [System.IO.Path]::GetFullPath($OutputDirectory)
        if ($requested -ne $devBuildsRoot -and !$requested.StartsWith($devBuildsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Development packages must stay under artifacts/dev-builds/.' }
    }
    $releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
    if ($FixtureVersion) {
        $fixtureRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/update-acceptance')) + [System.IO.Path]::DirectorySeparatorChar
        if (!$releaseRoot.StartsWith($fixtureRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture packages must stay under artifacts/update-acceptance/.' }
        $version = $FixtureVersion
    } elseif (!$Dev -and (Test-DevVersion $version)) {
        throw "Version $version is in the reserved development range (PATCH >= $devPatchStart); stable packages cannot use it. Use -Dev for development builds."
    }
    if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected MAJOR.MINOR.PATCH.' }
    $build = $null
    $informationalVersion = $version
    if ($Dev) {
        $baseVersion = $projectVersion
        if ($baseVersion -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -or (Test-DevVersion $baseVersion)) { throw 'Directory.Build.props must hold a stable MAJOR.MINOR.PATCH base version.' }
        $major = $Matches[1]; $minor = $Matches[2]
        $installationVersions = Join-Path $TargetInstallation 'versions'
        $taken = Get-TakenDevPatches $major $minor @($releaseRoot, $installationVersions)
        if ($DevVersion) {
            if (!(Test-DevVersion $DevVersion) -or $DevVersion -notmatch "^$major\.$minor\.\d+$") { throw "-DevVersion must be $major.$minor.PATCH with PATCH >= $devPatchStart." }
            if ($taken.Contains([int64]($DevVersion.Split('.')[2]))) { throw "Development version $DevVersion already exists in $releaseRoot or $installationVersions; choose another number." }
            $version = $DevVersion
        } else {
            $highest = if ($taken.Count -gt 0) { ($taken | Measure-Object -Maximum).Maximum } else { $devPatchStart }
            $version = "$major.$minor.$($highest + 1)"
        }
        $commit = (git rev-parse --short=7 HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{7,40}$') { throw 'Could not determine the source commit.' }
        # Only files that feed the build make a tree dirty; untracked scratch files elsewhere (for example OpenSpec notes) do not.
        $inputs = @('src', 'tests', 'scripts', 'vscode', 'docs', 'README.md', 'Directory.Build.props', 'ZeusApps.BellTower.slnx') | Where-Object { Test-Path -LiteralPath $_ }
        $dirty = [bool]((git status --porcelain --untracked-files=normal -- @inputs) -join '')
        $build = [ordered]@{ channel = 'dev'; commit = $commit; dirty = $dirty; builtAt = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'); baseVersion = $baseVersion }
        $informationalVersion = "$version-dev+$commit" + $(if ($dirty) { '.dirty' } else { '' })
    }
    if ($PlanOnly) {
        [ordered]@{ version = $version; outputDirectory = $releaseRoot; informationalVersion = $informationalVersion; build = $build } | ConvertTo-Json -Depth 5
        return
    }
    $toolchain = Find-NativeToolchain
    if ($CheckToolchain) { $toolchain | ConvertTo-Json; return }
    $env:PATH = (Split-Path $toolchain.VsWhere -Parent) + [System.IO.Path]::PathSeparator + $env:PATH
    $extension = Get-Content -LiteralPath vscode/package.json -Raw | ConvertFrom-Json
    if (!$FixtureVersion -and !$Dev -and $extension.version -cne $version) { throw 'Extension/application version mismatch.' }
    $installationRoot = Join-Path $releaseRoot "bell-tower-$version-win-x64"
    $publishRoot = Join-Path $installationRoot "versions/$version"
    $restoreOptions = @('--locked-mode', '-r', 'win-x64', '-p:SelfContained=true')
    if ($Offline) { $restoreOptions += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
    $versionOptions = @("-p:Version=$version", "-p:InformationalVersion=$informationalVersion")
    dotnet restore src/BellTower/BellTower.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet restore src/BellTower.Launcher/BellTower.Launcher.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Launcher locked restore failed.' }
    dotnet restore src/BellTower.Ingress/BellTower.Ingress.csproj @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Ingress locked restore failed.' }
    dotnet publish src/BellTower/BellTower.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true @versionOptions -o $publishRoot --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Stop a running copy in the target directory before packaging.' }
    $executable = Join-Path $publishRoot 'bell-tower.exe'
    dotnet publish src/BellTower.Ingress/BellTower.Ingress.csproj -c Release -r win-x64 --self-contained true @versionOptions -o (Join-Path $installationRoot 'ingress-publish') --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'NativeAOT ingress publish failed; install the Windows C++ toolchain. No managed fallback is packaged.' }
    Copy-Item -LiteralPath (Join-Path $installationRoot 'ingress-publish/bell-tower-ingress.exe') -Destination $publishRoot
    $probe = & (Join-Path $publishRoot 'bell-tower-ingress.exe') --protocol-info
    if ($LASTEXITCODE -ne 0 -or $probe.Trim() -cne '{"protocol":1,"minimumConsumer":1,"maximumConsumer":1}') { throw 'Ingress protocol probe failed.' }
    dotnet publish src/BellTower.Launcher/BellTower.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true @versionOptions -o $installationRoot --no-restore
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
    $contract = [ordered]@{ version = $version; runtime = 'win-x64'; selfContained = $true; layout = 1; bootstrap = 1; state = 1; runtimeRecords = 1; shell = 1; bridge = 1; capture = 1; attachmentHealth = 1; files = $inventory }
    if ($build) { $contract['build'] = $build }
    $contract | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $publishRoot 'release.json') -Encoding utf8
    @{ layout = 1; bootstrap = 1 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installationRoot 'installation.json') -Encoding utf8
    @{ version = $version } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installationRoot 'current.json') -Encoding utf8
    $launcher = Join-Path $installationRoot 'bell-tower.exe'
    $reported = (& $launcher --version)
    if ($LASTEXITCODE -ne 0) { throw 'Packaged launcher/application version mismatch.' }
    $reported = ($reported | Out-String).Trim()
    # Stable builds print exactly the version; dev builds print "VERSION dev COMMIT ...".
    if ($Dev) { if (!$reported.StartsWith("$version dev ", [System.StringComparison]::Ordinal)) { throw 'Packaged launcher/application version mismatch (expected a dev label).' } }
    elseif ($reported -cne $version) { throw 'Packaged launcher/application version mismatch.' }
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
} finally { $env:PATH = $savedPath; Pop-Location }
