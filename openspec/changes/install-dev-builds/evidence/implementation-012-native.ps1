#Requires -Version 7.4
<#
Implementation 012 native disposable churn run (ANALYSIS-550/552/553 on top of LIVE-001/002/003). Synthetic installation and data under
artifacts/implementation-012/ (stable launcher + payload extracted from the original package zip; own tray).
A churn process spawns `<disposable install>\bell-tower.exe statusline --provider claude --data-dir <disposable data>` about
every 200 ms during Switch and Rollback. The installer runs with redirected stdout/stderr pipes that are read to the end,
so a tray that inherited them would hang the read (LIVE-003). Only processes this script started are ever stopped.
#>
param(
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string[]]$Candidates = @('0.1.9001', '0.1.9002', '0.1.9004', '0.1.9005', '0.1.9006', '0.1.9007'),
    [string]$Original = '0.1.9003',
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot 'implementation-012-native-results.json' }
$dev = Join-Path $Repo 'artifacts/dev-builds'
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
$installer = Join-Path $Repo 'scripts/install-dev.ps1'
$root = Join-Path $Repo ('artifacts/implementation-012/native-' + [Guid]::NewGuid().ToString('N').Substring(0, 10))
$install = Join-Path $root 'install'; $data = Join-Path $root 'data'; $workspace = Join-Path $root 'private'
$pwsh = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'; if (!(Test-Path $pwsh)) { $pwsh = 'pwsh.exe' }
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
New-Item -ItemType Directory -Path $root, $install, $data | Out-Null
Expand-Archive -LiteralPath (Join-Path $dev "bell-tower-$Original-win-x64.zip") -DestinationPath $install
[IO.File]::WriteAllText((Join-Path $data 'state.json'), '{"schemaVersion":1,"sessions":{}}')
[IO.File]::WriteAllText((Join-Path $data 'state.json.bak'), '{"schemaVersion":1,"sessions":{}}')
$paths = [BellTower.IO.DataPaths]::new($data)
$controller = [BellTower.Tray.TrayController]::new($paths)
$presence = [BellTower.Updates.UpdateTransaction]::Presence($paths)

function Get-Presence {
    if (!(Test-Path -LiteralPath $presence)) { return $null }
    try { $p = [BellTower.Distribution.DistributionJson]::Read[BellTower.Updates.TrayPresence]($presence, 4096) } catch { return $null }
    if ([BellTower.IO.ProcessProbe]::Inspect($p.Process) -ne [BellTower.IO.ProcessLiveness]::Live) { return $null }
    return $p
}
function Wait-Presence([string]$Version) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) { $p = Get-Presence; if ($p -and $p.Version -eq $Version) { return $p }; Start-Sleep -Milliseconds 100 }
    throw "No live $Version tray."
}
# Runs the installer with captured pipes; returns when the pipes close (a leaked tray handle would time out here).
function Invoke-Captured([string[]]$Arguments) {
    $s = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $s.UseShellExecute = $false; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true; $s.CreateNoWindow = $true
    foreach ($a in @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installer) + $Arguments) { $s.ArgumentList.Add($a) }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $p = [Diagnostics.Process]::Start($s)
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    $closed = [Threading.Tasks.Task]::WaitAll(@($out, $err), 180000)
    if (!$closed) { try { $p.Kill($true) } catch { } }
    else { [void]$p.WaitForExit(30000) }
    return [pscustomobject]@{ closed = $closed; seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 1); exit = $(if ($p.HasExited) { $p.ExitCode } else { $null }); output = $(if ($closed) { $out.Result + $err.Result } else { '' }) }
}
$common = @('-InstallationRoot', $install, '-DataDirectory', $data, '-Workspace', $workspace, '-AssemblyDirectory', $assemblies, '-Disposable', '-AllowStaleBuild')

$churnScript = Join-Path $root 'churn.ps1'
[IO.File]::WriteAllText($churnScript, @'
param([string]$Exe, [string]$Data)
while ($true) {
    $s = [Diagnostics.ProcessStartInfo]::new($Exe)
    $s.UseShellExecute = $false; $s.CreateNoWindow = $true; $s.RedirectStandardInput = $true; $s.RedirectStandardOutput = $true; $s.RedirectStandardError = $true
    foreach ($a in @('statusline', '--provider', 'claude', '--data-dir', $Data)) { $s.ArgumentList.Add($a) }
    try { $p = [Diagnostics.Process]::Start($s); $p.StandardInput.Close(); [void]$p.StandardOutput.ReadToEndAsync(); [void]$p.StandardError.ReadToEndAsync() } catch { }
    Start-Sleep -Milliseconds 200
}
'@)
$cycles = [Collections.Generic.List[object]]::new()
$initial = [Diagnostics.ProcessStartInfo]::new((Join-Path $install 'bell-tower.exe')); $initial.UseShellExecute = $false; $initial.CreateNoWindow = $true
foreach ($a in @('tray', '--data-dir', $data, '--silent')) { $initial.ArgumentList.Add($a) }
([Diagnostics.Process]::Start($initial)).Dispose()
$churn = $null
try {
    [void](Wait-Presence $Original)
    $churn = Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile', '-File', $churnScript, '-Exe', (Join-Path $install 'bell-tower.exe'), '-Data', $data) -PassThru -WindowStyle Hidden
    foreach ($candidate in $Candidates) {
        $package = Join-Path $dev "bell-tower-$candidate-win-x64.zip"
        $cycle = [ordered]@{ candidate = $candidate }
        $pre = Invoke-Captured (@('-Action', 'Preflight', '-Package', $package) + $common)
        if ($pre.exit -ne 0) { throw "Preflight $candidate failed: $($pre.output)" }
        $manifest = ($pre.output | ConvertFrom-Json).operationManifest
        $switch = Invoke-Captured (@('-Action', 'Switch', '-Package', $package, '-OperationManifest', $manifest) + $common)
        $cycle.switch = [ordered]@{ exit = $switch.exit; pipeClosed = $switch.closed; seconds = $switch.seconds; tail = $switch.output.Trim() }
        $p = Get-Presence
        $cycle.switchTrayRunning = [bool]$p
        $cycle.switchTrayVersion = if ($p) { $p.Version } else { $null }
        if ($switch.exit -ne 0 -or !$switch.closed) { $cycle.failed = 'switch'; $cycles.Add($cycle); if (!$p) { $cycle.trayStopped = $true }; break }
        if ($p.Version -ne $candidate) { $cycle.failed = 'candidate tray version'; $cycles.Add($cycle); break }
        $roll = Invoke-Captured (@('-Action', 'Rollback', '-Package', $package, '-OperationManifest', $manifest) + $common)
        $cycle.rollback = [ordered]@{ exit = $roll.exit; pipeClosed = $roll.closed; seconds = $roll.seconds; tail = $roll.output.Trim() }
        $p = Get-Presence
        $cycle.rollbackTrayVersion = if ($p) { $p.Version } else { $null }
        $cycle.passed = ($roll.exit -eq 0 -and $roll.closed -and $p -and $p.Version -eq $Original)
        $cycles.Add($cycle)
        if (!$cycle.passed) { break }
    }

    # Direct drain durations under the same churn, with no tray running: the real Wait-InstallationTrayStartsDrained.
    try { $controller.RequestExit() } catch { }
    $gone = [DateTime]::UtcNow.AddSeconds(20); while ((Get-Presence) -and [DateTime]::UtcNow -lt $gone) { Start-Sleep -Milliseconds 100 }
    $ast = [Management.Automation.Language.Parser]::ParseFile($installer, [ref]$null, [ref]$null)
    foreach ($definition in @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] })) { . ([scriptblock]::Create($definition.Extent.Text)) }
    $InstallationRoot = $install; $DataDirectory = $data; $script:expectedStoppedTray = $null
    $drains = [Collections.Generic.List[object]]::new()
    1..5 | ForEach-Object {
        $clock = [Diagnostics.Stopwatch]::StartNew(); $failure = $null
        try { Wait-InstallationTrayStartsDrained } catch { $failure = $_.Exception.Message }
        $drains.Add([ordered]@{ seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2); refused = $failure })
        Start-Sleep -Milliseconds 300
    }
    $drainSeconds = $drains.ToArray()
} finally {
    if ($churn -and !$churn.HasExited) { try { $churn.Kill($true) } catch { } }
    try { $controller.RequestExit() } catch { }
}
$summary = [ordered]@{ root = $root; cycles = $cycles.ToArray(); drains = $drainSeconds; passedCycles = @($cycles | Where-Object { $_.passed }).Count; totalCycles = $cycles.Count }
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultFile
$summary | ConvertTo-Json -Depth 8
