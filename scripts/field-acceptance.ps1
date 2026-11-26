# Installed field acceptance harness (task 2.1 foundation). Retains the published baseline release, builds the fixture-version
# candidate, builds the harness and runs a scenario under disposable roots. Nothing here installs into, or reads settings from, the real
# installation, data directory, ~/.claude, ~/.codex or the running tray; see docs/distribution-validation.md.
# Exit codes: 0 every check passed; 1 a check or the isolation comparison failed; 2 another run is active; 3 no check failed but unfinished
# parity items remain ("PASSED WITH UNFINISHED ITEMS": open release work, not acceptance; the items are listed in the summary);
# 130 interrupted. Exit codes 1, 2 and 130 are returned with `exit` after the message is printed, so they reach `pwsh -File` callers (a
# `throw` would turn every one of them into 1); other setup failures (download, package or harness build) still throw and exit 1.
param(
    [string]$Scenario = 'upgrade-without-setup',
    [string]$BaselineTag = 'v0.1.9',
    [string]$Repository = 'zeusapps/ZeusApps.BellTower',
    [string]$FixtureVersion = '0.1.11',
    [string]$BaselineArchive,
    [string]$CandidateArchive,
    [string]$OutputDirectory = 'artifacts/field-acceptance/runs',
    # Optional committable summary (sanitized: repository, profile, temp, user and host names become placeholders).
    [string]$EvidenceOut,
    [switch]$RebuildCandidate,
    [switch]$KeepSandbox
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    function Resolve-Local([string]$Path) { return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $Path)) }
    $baselineVersion = $BaselineTag.TrimStart('v')
    $baselineDirectory = Resolve-Local 'artifacts/field-acceptance/baseline'
    $baselineName = "bell-tower-$baselineVersion-win-x64"
    if (!$BaselineArchive) { $BaselineArchive = Join-Path $baselineDirectory "$baselineName.zip" }
    $BaselineArchive = [System.IO.Path]::GetFullPath($BaselineArchive)
    $provenance = Join-Path (Split-Path $BaselineArchive -Parent) 'provenance.json'

    # 0. One harness run at a time (the harness holds this named mutex for its whole run). Refuse before the build below rewrites binaries a
    #    running harness is using; the harness itself enforces the same lock authoritatively.
    $active = $null
    if ([System.Threading.Mutex]::TryOpenExisting('Local\BellTower.FieldAcceptance', [ref]$active)) {
        $active.Dispose()
        [Console]::Error.WriteLine('Another field acceptance run is active on this machine; wait for it to finish (only one run can use the preflight sweep and the production comparison).')
        $global:LASTEXITCODE = 2
        exit 2
    }

    # 1. The historical baseline is the archive GitHub published for the tag, never a rebuild of that version from this source.
    if (!(Test-Path -LiteralPath $BaselineArchive)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $BaselineArchive -Parent) | Out-Null
        gh release download $BaselineTag --repo $Repository --pattern "$baselineName.*" --dir (Split-Path $BaselineArchive -Parent) --clobber
        if ($LASTEXITCODE -ne 0) { throw "gh release download $BaselineTag failed; the published archive is required." }
    }
    $digest = $null
    $release = gh release view $BaselineTag --repo $Repository --json assets 2>$null | ConvertFrom-Json
    if ($release) { $digest = ($release.assets | Where-Object { $_.name -eq (Split-Path $BaselineArchive -Leaf) }).digest }
    [ordered]@{
        tag = $BaselineTag
        source = "gh release download $BaselineTag --repo $Repository --pattern '$baselineName.*' (public release assets, not rebuilt from source)"
        githubAssetDigest = $digest
        note = 'Archive, .sha256 and release descriptor are the files published with the release.'
    } | ConvertTo-Json | Set-Content -LiteralPath $provenance -Encoding utf8

    # 2. The fixture-version candidate (never the release candidate: the declarations stay at the published version until task 4.4).
    $packageDirectory = Resolve-Local 'artifacts/update-acceptance/packages'
    if (!$CandidateArchive) { $CandidateArchive = Join-Path $packageDirectory "bell-tower-$FixtureVersion-win-x64.zip" }
    $CandidateArchive = [System.IO.Path]::GetFullPath($CandidateArchive)
    $buildCommand = "scripts/package.ps1 -FixtureVersion $FixtureVersion -OutputDirectory artifacts/update-acceptance/packages"
    if ($RebuildCandidate -or !(Test-Path -LiteralPath $CandidateArchive)) {
        & (Join-Path $PSScriptRoot 'package.ps1') -FixtureVersion $FixtureVersion -OutputDirectory $packageDirectory
    }

    # 3. The harness itself.
    dotnet build tools/BellTower.FieldAcceptance -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Harness build failed.' }
    $harness = Resolve-Local 'tools/BellTower.FieldAcceptance/bin/Release/net10.0-windows/BellTower.FieldAcceptance.dll'

    # 4. Run.
    $runDirectory = Join-Path (Resolve-Local $OutputDirectory) (Get-Date -Format 'yyyyMMdd-HHmmss')
    $harnessArguments = @($harness, '--scenario', $Scenario, '--baseline', $BaselineArchive, '--candidate', $CandidateArchive, '--run-dir', $runDirectory,
        '--baseline-provenance', $provenance, '--candidate-label', 'fixture-version candidate', '--candidate-build-command', $buildCommand,
        '--command-text', "scripts/field-acceptance.ps1 -Scenario $Scenario -FixtureVersion $FixtureVersion")
    if ($EvidenceOut) { $harnessArguments += @('--evidence-out', [System.IO.Path]::GetFullPath((Join-Path $projectRoot $EvidenceOut))) }
    if ($KeepSandbox) { $harnessArguments += '--keep-sandbox' }
    dotnet @harnessArguments
    $code = $LASTEXITCODE
    Write-Output "Result JSON:  $(Join-Path $runDirectory 'result.json')"
    Write-Output "Summary:      $(Join-Path $runDirectory 'summary.md')"
    if ($code -eq 3) {
        Write-Warning 'Field acceptance passed with unfinished parity items (exit 3): the listed items are open release work, not acceptance.'
        $global:LASTEXITCODE = 3
        exit 3
    }
    if ($code -ne 0) {
        [Console]::Error.WriteLine("Field acceptance failed (exit $code).")
        $global:LASTEXITCODE = $code
        exit $code
    }
} finally { Pop-Location }
