#Requires -Version 7.4
# QA 007 teardown failure / timeout-join check for the QA-owned hook workers: one hook assertion failure (impossible 500 ms budget)
# while Switch is held at a barrier; verifies timed-out hook shell trees are killed and joined, the barrier is released in finally,
# the installer finishes, and no QA-owned process survives. Evidence type: installed native on synthetic disposable data.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$root = Join-Path $repo ('artifacts/qa-007/teardown-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $private = Join-Path $root 'private'; $barrier = Join-Path $root 'barrier'
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'
New-Item -ItemType Directory -Path $root, $install, $data, $barrier | Out-Null
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
Get-ChildItem (Join-Path $repo 'artifacts/qa-007/source-0.1.9003') | Copy-Item -Destination $install -Recurse -Force
[IO.File]::WriteAllText((Join-Path $data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
$fixture = [ordered]@{ label = 'qa007 teardown'; installation = $install; data = $data; version = '0.1.9003' }
[IO.File]::WriteAllText((Join-Path $root 'fixture.json'), ($fixture | ConvertTo-Json))
$commands = & (Join-Path $PSScriptRoot 'qa-007-prepare-hooks.ps1') -FixtureFile (Join-Path $root 'fixture.json') -ScenarioRoot (Join-Path $root 'hooks')
$paths = [BellTower.IO.DataPaths]::new($data); $controller = [BellTower.Tray.TrayController]::new($paths); $presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)
$package = Join-Path $repo 'artifacts/dev-builds/bell-tower-0.1.9006-win-x64.zip'
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$res = [ordered]@{}
$s = [Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe')); $s.UseShellExecute = $false; $s.CreateNoWindow = $true
foreach ($a in @('tray', '--data-dir', $data, '--silent')) { [void]$s.ArgumentList.Add($a) }
[void][Diagnostics.Process]::Start($s)
try {
    $d = [DateTime]::UtcNow.AddSeconds(40); while (!(Test-Path $presence) -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 100 }
    $base = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-Package', $package, '-InstallationRoot', $install, '-DataDirectory', $data, '-Workspace', $private, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild')
    $pre = Start-Process $pwsh -ArgumentList ($base + @('-Action', 'Preflight')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root 'pre.out') -RedirectStandardError (Join-Path $root 'pre.err'); [void]$pre.WaitForExit(120000)
    $manifest = ((Get-Content (Join-Path $root 'pre.out') -Raw) | ConvertFrom-Json).operationManifest
    $sw = Start-Process $pwsh -ArgumentList ($base + @('-Action', 'Switch', '-OperationManifest', $manifest, '-TestBarrierDirectory', $barrier)) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root 'sw.out') -RedirectStandardError (Join-Path $root 'sw.err')
    $entered = Join-Path $barrier 'switch-before-selection.entered'; $release = Join-Path $barrier 'switch-before-selection.release'
    $d = [DateTime]::UtcNow.AddSeconds(90); while (!(Test-Path $entered) -and !$sw.HasExited -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 30 }
    $before = @(Get-Process -Name powershell, bash, cmd -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $trialFile = Join-Path $root 'trial.json'
    try {
        # Impossible 500 ms budget for legacy hooks: the harness must report failure, kill its own shell trees and keep the barrier held.
        & (Join-Path $PSScriptRoot 'qa-007-hook-trial.ps1') -CommandsFile $commands -ResultFile $trialFile -Trial LegacyMixed -BarrierDirectory $barrier -BarrierPhase 'switch-before-selection' -BudgetMilliseconds 500 | Out-Null
        $res.trialExit = $LASTEXITCODE
    } catch { $res.trialThrew = $_.ToString() }
    $t = Get-Content $trialFile -Raw | ConvertFrom-Json
    $res.trial = [ordered]@{ hooks = @($t.hooks | ForEach-Object { [ordered]@{ provider = $_.provider; passed = $_.passed; timeout = $_.timeout; elapsed = $_.elapsedMilliseconds } }); barrierStillActive = $t.barrierStillActiveAfterHooks; harnessError = $t.harnessError }
    Start-Sleep -Milliseconds 500
    $after = @(Get-Process -Name powershell, bash, cmd -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $res.newShellProcessesAfterTimeout = @($after | Where-Object { $_ -notin $before })
    $res.installerStillHeld = !$sw.HasExited
    [IO.File]::WriteAllText($release, 'x')
    $res.installerFinishedAfterRelease = $sw.WaitForExit(120000)
    $res.installerExit = $sw.ExitCode
} finally {
    if (!(Test-Path (Join-Path $barrier 'switch-before-selection.release'))) { [IO.File]::WriteAllText((Join-Path $barrier 'switch-before-selection.release'), 'x') }
    try { $controller.RequestExit() } catch { }
    Start-Sleep -Seconds 4
    $left = @(Get-CimInstance Win32_Process -Property ProcessId, ExecutablePath, CommandLine | Where-Object { ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($root, [StringComparison]::OrdinalIgnoreCase)) })
    foreach ($i in $left) { Stop-Process -Id $i.ProcessId -Force -ErrorAction SilentlyContinue }
    $res.leftoversStopped = @($left | ForEach-Object { $_.ProcessId })
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'qa-007-teardown-results.json'), ($res | ConvertTo-Json -Depth 8))
$res | ConvertTo-Json -Depth 8
