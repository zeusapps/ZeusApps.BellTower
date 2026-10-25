$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repository 'artifacts'
$trial = Join-Path $artifacts 'qa-010-owner-race'
if (Test-Path -LiteralPath $trial) { throw "Use a new disposable root; existing: $trial" }
$installationRoot = Join-Path $trial 'install'
$versionsRoot = Join-Path $installationRoot 'versions'
$dataRoot = Join-Path $trial 'data'
$workspace = Join-Path $trial 'workspace'
$barriers = Join-Path $trial 'barriers'
New-Item -ItemType Directory -Path $versionsRoot, $dataRoot, $barriers, (Join-Path $dataRoot 'updates') | Out-Null

$fixtureRoot = Join-Path $artifacts 'dev-cycle/20261024T200826Z/install'
$stablePayload = Join-Path $fixtureRoot 'versions/0.1.8'
if (!(Test-Path -LiteralPath $stablePayload -PathType Container)) { throw 'The archived disposable 0.1.8 fixture is unavailable.' }
Copy-Item -LiteralPath $stablePayload -Destination (Join-Path $versionsRoot '0.1.8') -Recurse
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'bell-tower.exe') -Destination (Join-Path $installationRoot 'bell-tower.exe')
[IO.File]::WriteAllText((Join-Path $installationRoot 'installation.json'), '{"layout":1,"bootstrap":1}')
[IO.File]::WriteAllText((Join-Path $installationRoot 'current.json'), '{"version":"0.1.8"}')
[IO.File]::WriteAllText((Join-Path $dataRoot 'state.json'), '{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $dataRoot 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')

$script = Join-Path $repository 'scripts/install-dev.ps1'
$package = Join-Path $artifacts 'dev-builds/bell-tower-0.1.9002-win-x64.zip'
if (!(Test-Path -LiteralPath $package -PathType Leaf)) { throw 'The QA dev package 0.1.9002 is unavailable.' }
function Invoke-Script([string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh.exe -CommandType Application).Source)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.ArgumentList.Add('-NoProfile')
    $info.ArgumentList.Add('-File')
    $info.ArgumentList.Add($script)
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    if (!$process.WaitForExit(120000)) { $process.Kill($true); throw 'install-dev.ps1 exceeded the isolated QA deadline.' }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    return [ordered]@{ exitCode = $process.ExitCode; stdout = $stdout; stderr = $stderr }
}
function Wait-Path([string]$Path) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return }
        Start-Sleep -Milliseconds 50
    }
    throw "Timed out waiting for disposable barrier: $Path"
}

$common = @('-Package', $package, '-InstallationRoot', $installationRoot, '-DataDirectory', $dataRoot,
    '-Workspace', $workspace, '-Disposable', '-NoTray', '-SkipProbe', '-TestBarrierDirectory', $barriers)
$preflight = Invoke-Script (@('-Action', 'Preflight') + $common)
if ($preflight.exitCode -ne 0) { throw "Preflight failed: $($preflight.stderr) $($preflight.stdout)" }
$operation = ($preflight.stdout | ConvertFrom-Json).operationManifest
$switchInfo = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh.exe -CommandType Application).Source)
$switchInfo.UseShellExecute = $false
$switchInfo.CreateNoWindow = $true
$switchInfo.RedirectStandardOutput = $true
$switchInfo.RedirectStandardError = $true
foreach ($argument in (@('-NoProfile', '-File', $script, '-Action', 'Switch', '-OperationManifest', $operation) + $common)) {
    $switchInfo.ArgumentList.Add($argument)
}
$switch = [Diagnostics.Process]::Start($switchInfo)
$tray = $null
try {
    Wait-Path (Join-Path $barriers 'switch-before-selection.entered')
    $trayInfo = [Diagnostics.ProcessStartInfo]::new((Join-Path $installationRoot 'bell-tower.exe'))
    $trayInfo.UseShellExecute = $false
    $trayInfo.CreateNoWindow = $true
    $trayInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($argument in @('tray', '--data-dir', $dataRoot, '--silent')) { $trayInfo.ArgumentList.Add($argument) }
    $tray = [Diagnostics.Process]::Start($trayInfo)
    $presencePath = Join-Path $dataRoot 'updates/tray.json'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath $presencePath -PathType Leaf) -and !$tray.HasExited) { Start-Sleep -Milliseconds 100 }
    $trayExitedBeforeRelease = $tray.HasExited
    $trayExitCodeBeforeRelease = if ($tray.HasExited) { $tray.ExitCode } else { $null }
    $newOwner = if (Test-Path -LiteralPath $presencePath -PathType Leaf) { Get-Content -LiteralPath $presencePath -Raw | ConvertFrom-Json } else { $null }
    [IO.File]::WriteAllText((Join-Path $barriers 'switch-before-selection.release'), 'continue')
    if (!$switch.WaitForExit(120000)) { $switch.Kill($true); throw 'Switch exceeded the isolated QA deadline.' }
    $switchOutput = $switch.StandardOutput.ReadToEnd()
    $switchError = $switch.StandardError.ReadToEnd()
    $selected = (Get-Content -LiteralPath (Join-Path $installationRoot 'current.json') -Raw | ConvertFrom-Json).version
    [ordered]@{
        action = 'Switch';
        fixtureVersion = '0.1.8';
        candidateVersion = '0.1.9002';
        trayStartedDuringBlockedPhase = $true; trayExitedBeforeRelease = $trayExitedBeforeRelease; trayExitCodeBeforeRelease = $trayExitCodeBeforeRelease; trayPresenceBeforeRelease = ($null -ne $newOwner); trayStillRunningAfterSwitch = (!$tray.HasExited); presenceAfterSwitch = (Test-Path -LiteralPath $presencePath);
        trayVersion = $(if ($newOwner) { $newOwner.version } else { $null });
        trayProcessId = $(if ($newOwner) { $newOwner.process.pid } else { $null });
        switchExitCode = $switch.ExitCode;
        switchStdout = $switchOutput;
        switchStderr = $switchError;
        selectedVersionAfterRelease = $selected;
        sourceIdentity = 'QA 010; baseline b74a12b, identity in qa-010-identity-before.txt'
    } | ConvertTo-Json -Depth 8
}
finally {
    if (Test-Path -LiteralPath (Join-Path $barriers 'switch-before-selection.entered')) {
        [IO.File]::WriteAllText((Join-Path $barriers 'switch-before-selection.release'), 'continue')
    }
    if ($null -ne $tray -and !$tray.HasExited) {
        try {
            $exitInfo = [Diagnostics.ProcessStartInfo]::new((Join-Path $installationRoot 'bell-tower.exe'))
            $exitInfo.UseShellExecute = $false
            $exitInfo.CreateNoWindow = $true
            foreach ($argument in @('exit', '--data-dir', $dataRoot)) { $exitInfo.ArgumentList.Add($argument) }
            $ownedExit = [Diagnostics.Process]::Start($exitInfo)
            if (!$ownedExit.WaitForExit(10000)) { $ownedExit.Kill($true) }
            if (!$tray.WaitForExit(15000)) { $tray.Kill($true) }
        } catch { if (!$tray.HasExited) { $tray.Kill($true) } }
    }
    if (!$switch.HasExited) { [IO.File]::WriteAllText((Join-Path $barriers 'switch-before-selection.release'), 'continue'); $switch.Kill($true) }
}
