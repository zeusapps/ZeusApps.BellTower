#Requires -Version 7.4
# QA 007 published 0.1.8 compatibility trial (capture 0, no fast path). Evidence type: installed native, actual published
# 0.1.8 launcher/payload (SHA verified at fixture preparation), disposable data; hook commands built with current-source
# HookCommandBuilder in legacy form. Kept separate from capture-1 source fixtures.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$root = Join-Path $repo 'artifacts/qa-007/published-018-fixture'
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $private = Join-Path $root 'private'; $logs = Join-Path $root 'logs'
$commandsFile = Join-Path $repo 'artifacts/qa-007/published-018-hooks/commands.json'
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
$package = Join-Path $repo 'artifacts/dev-builds/bell-tower-0.1.9006-win-x64.zip'
$installer = Join-Path $repo 'scripts/install-dev.ps1'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
$paths = [BellTower.IO.DataPaths]::new($data); $controller = [BellTower.Tray.TrayController]::new($paths); $presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)
$result = [ordered]@{ kind = 'installed native, actual published 0.1.8 runtime, capture 0 / legacy-only; synthetic disposable data'; steps = [ordered]@{} }
function Run-Trials([string]$Label, [string]$BarrierDir, [string]$BarrierName) {
    $rows = @()
    foreach ($t in @('LegacyClaude', 'LegacyCodex', 'LegacyMixed')) {
        $f = Join-Path $root ("hook-$Label-$t-" + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.json')
        $splat = @{ CommandsFile = $commandsFile; ResultFile = $f; Trial = $t }
        if ($BarrierDir) { $splat.BarrierDirectory = $BarrierDir; $splat.BarrierPhase = $BarrierName }
        try { & (Join-Path $PSScriptRoot "qa-007-hook-trial.ps1") @splat | Out-Null } catch { $script:hookErr = $_.ToString() }
        $r = if (Test-Path $f) { Get-Content $f -Raw | ConvertFrom-Json } else { $null }
        $rows += [ordered]@{ trial = $t; err = $script:hookErr; hookPass = [bool]($r -and @($r.hooks | Where-Object { !$_.passed }).Count -eq 0 -and !$r.harnessError); maxMs = $(if ($r) { ($r.hooks | Measure-Object elapsedMilliseconds -Maximum).Maximum } else { $null })
            captureDelta = $r.durableCaptureDelta; inboxDelta = $r.durableInboxDelta; barrierHeld = $r.barrierStillActiveAfterHooks; neutral = [bool]($r -and @($r.hooks | Where-Object { !$_.neutralOutput }).Count -eq 0) }
    }
    return $rows
}
$initial = $null
try {
    $s = [Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe')); $s.UseShellExecute = $false; $s.CreateNoWindow = $true
    foreach ($a in @('tray', '--data-dir', $data, '--silent')) { [void]$s.ArgumentList.Add($a) }
    $initial = [Diagnostics.Process]::Start($s)
    $d = [DateTime]::UtcNow.AddSeconds(40)
    while (!(Test-Path $presence) -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 100 }
    $p = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence, 4096)
    $result.steps.tray = [ordered]@{ version = $p.Version; capture = $p.Capture; pid = $p.Process.Pid }
    $result.steps.idleHooks = Run-Trials 'idle' $null $null
    $selBefore = (Get-FileHash (Join-Path $install 'current.json')).Hash
    $out = Join-Path $logs 'preflight.out'; $err = Join-Path $logs 'preflight.err'
    $a = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-Action', 'Preflight', '-Package', $package, '-InstallationRoot', $install, '-DataDirectory', $data, '-Workspace', $private, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild')
    $pp = Start-Process $pwsh -ArgumentList $a -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err; [void]$pp.WaitForExit(120000)
    $txt = (Get-Content $out -Raw) + (Get-Content $err -Raw)
    $result.steps.preflight = [ordered]@{ exitCode = $pp.ExitCode; text = ($txt -replace '\x1b\[[0-9;]*m', '' -replace '\s+', ' ').Trim(); selectionUnchanged = ($selBefore -eq (Get-FileHash (Join-Path $install 'current.json')).Hash) }
    if ($pp.ExitCode -eq 0) {
        $manifest = ($txt | ConvertFrom-Json).operationManifest
        $b = Join-Path $root 'barrier'; New-Item -ItemType Directory -Path $b -Force | Out-Null
        $out2 = Join-Path $logs 'switch.out'; $err2 = Join-Path $logs 'switch.err'
        $a2 = $a.Clone(); $a2[7] = 'Switch'; $a2 += @('-OperationManifest', $manifest, '-TestBarrierDirectory', $b)
        $sp = Start-Process $pwsh -ArgumentList $a2 -PassThru -WindowStyle Hidden -RedirectStandardOutput $out2 -RedirectStandardError $err2
        $d = [DateTime]::UtcNow.AddSeconds(90); while (!(Test-Path (Join-Path $b 'switch-before-selection.entered')) -and !$sp.HasExited -and [DateTime]::UtcNow -lt $d) { Start-Sleep -Milliseconds 50 }
        if (Test-Path (Join-Path $b 'switch-before-selection.entered')) {
            $t = [Diagnostics.Stopwatch]::StartNew()
            $result.steps.heldSwitchHooks = Run-Trials 'switch' $b 'switch-before-selection'
            $result.steps.heldSwitchMs = $t.ElapsedMilliseconds
            [IO.File]::WriteAllText((Join-Path $b 'switch-before-selection.release'), 'x')
        }
        [void]$sp.WaitForExit(120000)
        $result.steps.switch = [ordered]@{ exitCode = $sp.ExitCode; text = (((Get-Content $out2 -Raw) + (Get-Content $err2 -Raw)) -replace '\x1b\[[0-9;]*m', '' -replace '\s+', ' ').Trim() }
        $result.steps.selectionAfterSwitch = (Get-Content (Join-Path $install 'current.json') -Raw | ConvertFrom-Json).version
    }
} catch { $result.error = $_.ToString() }
finally {
    try { $controller.RequestExit() } catch { }
    Start-Sleep -Seconds 3
    $left = @(Get-CimInstance Win32_Process -Property ProcessId, ExecutablePath, CommandLine | Where-Object { ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($root, [StringComparison]::OrdinalIgnoreCase)) })
    foreach ($i in $left) { Stop-Process -Id $i.ProcessId -Force -ErrorAction SilentlyContinue }
    $result.cleanup = [ordered]@{ leftoversStopped = @($left | ForEach-Object { $_.ProcessId }) }
}
$f = Join-Path $PSScriptRoot 'qa-007-published-018-results.json'
[IO.File]::WriteAllText($f, ($result | ConvertTo-Json -Depth 8))
$result | ConvertTo-Json -Depth 8
