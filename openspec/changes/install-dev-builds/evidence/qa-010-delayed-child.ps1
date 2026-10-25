# QA 010: delayed old-order tray start (simulator passes CanStartTray before any Switch marker exists, pauses before the
# named instance mutex) racing a real Switch on a disposable installation. Evidence type: installed native (script) + simulated old-order tray.
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repository 'artifacts'
$trial = Join-Path $artifacts 'qa-010-delayed'
if (Test-Path -LiteralPath $trial) { throw "Use a new disposable root; existing: $trial" }
$installationRoot = Join-Path $trial 'install'; $versionsRoot = Join-Path $installationRoot 'versions'
$dataRoot = Join-Path $trial 'data'; $workspace = Join-Path $trial 'workspace'; $control = Join-Path $trial 'control'
New-Item -ItemType Directory -Path $versionsRoot, $dataRoot, $control, (Join-Path $dataRoot 'updates') | Out-Null
$fixtureRoot = Join-Path $artifacts 'dev-cycle/20261024T200826Z/install'
$payload = Join-Path $versionsRoot '0.1.8'
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'versions/0.1.8') -Destination $payload -Recurse
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'bell-tower.exe') -Destination (Join-Path $installationRoot 'bell-tower.exe')
[IO.File]::WriteAllText((Join-Path $installationRoot 'installation.json'), '{"layout":1,"bootstrap":1}')
[IO.File]::WriteAllText((Join-Path $installationRoot 'current.json'), '{"version":"0.1.8"}')
[IO.File]::WriteAllText((Join-Path $dataRoot 'state.json'), '{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $dataRoot 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')
# Swap in the simulator as the payload image and re-seal the contract hash.
$helperSource = Join-Path $artifacts 'qa-010-helper-publish/bell-tower.exe'  # single-file publish of evidence/qa-005-delayed-child (unchanged source)
$helperImage = Join-Path $payload 'bell-tower.exe'
Copy-Item -LiteralPath $helperSource -Destination $helperImage -Force
$contractPath = Join-Path $payload 'release.json'
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$contract.files.'bell-tower.exe' = (Get-FileHash -LiteralPath $helperImage -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($contractPath, ($contract | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))

$script = Join-Path $repository 'scripts/install-dev.ps1'
$package = Join-Path $artifacts 'dev-builds/bell-tower-0.1.9002-win-x64.zip'
function New-Info([string]$File) {
    $i = [Diagnostics.ProcessStartInfo]::new($File); $i.UseShellExecute = $false; $i.CreateNoWindow = $true
    $i.RedirectStandardOutput = $true; $i.RedirectStandardError = $true; return $i
}
$pwsh = (Get-Command pwsh.exe -CommandType Application).Source
$common = @('-Package', $package, '-InstallationRoot', $installationRoot, '-DataDirectory', $dataRoot, '-Workspace', $workspace, '-Disposable', '-NoTray', '-SkipProbe')
function Start-Script([string[]]$Arguments) {
    $i = New-Info $pwsh
    foreach ($a in (@('-NoProfile', '-File', $script) + $Arguments)) { $i.ArgumentList.Add($a) }
    return [Diagnostics.Process]::Start($i)
}
$child = $null; $childIdentity = $null; $switch = $null
try {
    $pre = Start-Script (@('-Action', 'Preflight') + $common)
    if (!$pre.WaitForExit(120000)) { $pre.Kill($true); throw 'Preflight timeout' }
    $preOut = $pre.StandardOutput.ReadToEnd()
    if ($pre.ExitCode -ne 0) { throw "Preflight failed: $preOut $($pre.StandardError.ReadToEnd())" }
    $operation = ($preOut | ConvertFrom-Json).operationManifest

    # Old-order child: launched through the stable launcher after Preflight, before any Switch marker exists.
    $ci = New-Info (Join-Path $installationRoot 'bell-tower.exe')
    $ci.RedirectStandardOutput = $false; $ci.RedirectStandardError = $false
    $ci.Environment['QA005_DELAYED_TRAY_CONTROL'] = $control
    foreach ($a in @('tray', '--data-dir', $dataRoot, '--silent')) { $ci.ArgumentList.Add($a) }
    $launcher = [Diagnostics.Process]::Start($ci); $launcher.Dispose()
    $entered = Join-Path $control 'can-start-passed.json'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (!(Test-Path $entered) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (!(Test-Path $entered)) { throw 'Simulator never passed CanStartTray.' }
    $p = Get-Content $entered -Raw | ConvertFrom-Json
    $live = Get-Process -Id $p.pid
    $childIdentity = [ordered]@{ pid = $p.pid; startTicks = $live.StartTime.ToUniversalTime().Ticks; image = $live.MainModule.FileName }
    if ($childIdentity.image -ne $helperImage) { throw "Child image mismatch: $($childIdentity.image)" }

    $switch = Start-Script (@('-Action', 'Switch', '-OperationManifest', $operation) + $common)
    $paused = $switch.WaitForExit(20000)
    $phase1 = [ordered]@{ switchExitedWhileChildPaused = $paused; childAliveAtThatPoint = !$live.HasExited }
    if ($paused) { $phase1.exitCode = $switch.ExitCode; $phase1.stdout = $switch.StandardOutput.ReadToEnd(); $phase1.stderr = $switch.StandardError.ReadToEnd() }
    $phase1.selectedWhilePaused = (Get-Content (Join-Path $installationRoot 'current.json') -Raw | ConvertFrom-Json).version

    [IO.File]::WriteAllText((Join-Path $control 'release-before-instance-mutex'), 'go')
    $d2 = [DateTime]::UtcNow.AddSeconds(15)
    $mutexResult = Join-Path $control 'mutex-result.json'
    while (!(Test-Path $mutexResult) -and [DateTime]::UtcNow -lt $d2) { Start-Sleep -Milliseconds 50 }
    $phase2 = [ordered]@{ mutexResult = $(if (Test-Path $mutexResult) { Get-Content $mutexResult -Raw | ConvertFrom-Json } else { $null }) }
    if (!$paused) {
        if (!$switch.WaitForExit(60000)) { $switch.Kill($true); $phase2.switchTimeoutKilled = $true }
        else { $phase2.exitCode = $switch.ExitCode; $phase2.stdout = $switch.StandardOutput.ReadToEnd(); $phase2.stderr = $switch.StandardError.ReadToEnd() }
    }
    $phase2.selectedAfter = (Get-Content (Join-Path $installationRoot 'current.json') -Raw | ConvertFrom-Json).version
    $phase2.foreignOwnerPresence = Test-Path (Join-Path $dataRoot 'updates/fixture-tray-owner.json')
    $stillThere = $null -ne (Get-Process -Id $childIdentity.pid -ErrorAction SilentlyContinue)
    $phase2.childStillRunning = $stillThere
    [ordered]@{ child = $childIdentity; phase1_beforeRelease = $phase1; phase2_afterRelease = $phase2 } | ConvertTo-Json -Depth 8
}
finally {
    if ($switch -and !$switch.HasExited) { $switch.Kill($true) }
    if ($childIdentity) {
        $g = Get-Process -Id $childIdentity.pid -ErrorAction SilentlyContinue
        if ($g -and $g.StartTime.ToUniversalTime().Ticks -eq $childIdentity.startTicks) { $g.Kill(); [void]$g.WaitForExit(10000) }
    }
}
