param(
    [Parameter(Mandatory)][string]$CommandsFile,
    [Parameter(Mandatory)][string]$ResultFile,
    [string]$BarrierDirectory,
    [string]$BarrierPhase,
    [ValidateSet('Legacy', 'Native', 'Both')][string]$Channel = 'Both',
    [switch]$HoldLaunchGate,
    [string]$ClaudeShell = 'C:/Program Files/Git/bin/bash.exe',
    [ValidateRange(500, 10000)][int]$BudgetMilliseconds = 3000
)
$ErrorActionPreference = 'Stop'
$commands = Get-Content -LiteralPath $CommandsFile -Raw | ConvertFrom-Json
$target = [IO.Path]::GetFullPath((Split-Path -Parent $ResultFile))
$artifacts = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..\artifacts'))
if (!$target.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Results must stay under artifacts/qa-004-*.'
}
if (Test-Path -LiteralPath $ResultFile) { throw "Use a new result file: $ResultFile" }
New-Item -ItemType Directory -Path $target -Force | Out-Null
if ($BarrierDirectory) {
    $entered = Join-Path $BarrierDirectory ($BarrierPhase + '.entered')
    $released = Join-Path $BarrierDirectory ($BarrierPhase + '.release')
    if (!(Test-Path -LiteralPath $entered -PathType Leaf) -or (Test-Path -LiteralPath $released)) {
        throw 'Run this check only while the requested disposable maintenance barrier is entered and unreleased.'
    }
}
if (!(Test-Path -LiteralPath $ClaudeShell -PathType Leaf)) { throw 'Git Bash was not found at -ClaudeShell; Claude shell coverage is not run.' }

function Start-Hook([string]$Provider, [string]$Channel, [int]$Index) {
    $command = $commands.commands.($Provider + $Channel)
    if ([string]::IsNullOrWhiteSpace($command)) { throw "Missing generated command for $Provider$Channel." }
    if ($Provider -eq 'claude') {
        $info = [Diagnostics.ProcessStartInfo]::new($ClaudeShell)
        $info.ArgumentList.Add('-c')
        $info.ArgumentList.Add($command)
    } else {
        $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32/cmd.exe'))
        # Match the provider's actual Windows raw /C argument construction.
        $info.Arguments = '/C "' + $command + '"'
    }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
    $info.WorkingDirectory = $commands.home
    foreach ($key in @($info.Environment.Keys)) {
        if ($key.StartsWith('BELLTOWER_', [StringComparison]::OrdinalIgnoreCase)) { [void]$info.Environment.Remove($key) }
    }
    $info.Environment['BELLTOWER_ATTACHMENT'] = "qa004-$Provider-$Index"
    $info.Environment['BELLTOWER_DEDICATED'] = '1'
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $started = [Diagnostics.Stopwatch]::StartNew()
    $input = [ordered]@{
        session_id = "qa004-$Provider-$Index"
        hook_event_name = 'Stop'
        cwd = (Join-Path $commands.data 'fixture-directory')
        last_assistant_message = 'qa-004-synthetic-message'
    } | ConvertTo-Json -Compress
    $process.StandardInput.Write($input)
    $process.StandardInput.Close()
    if ($null -eq $process) { throw "QA-owned $Provider $Channel shell did not start." }
    return [pscustomobject]@{ Provider = $Provider; Channel = $Channel; Index = $Index; Process = $process; Stdout = $stdout; Stderr = $stderr; Started = $started }
}

$runs = @(
    @{ Provider = 'claude'; Channel = 'Legacy'; Index = 1 },
    @{ Provider = 'codex'; Channel = 'Legacy'; Index = 1 },
    @{ Provider = 'claude'; Channel = 'Legacy'; Index = 2 },
    @{ Provider = 'codex'; Channel = 'Legacy'; Index = 2 },
    @{ Provider = 'claude'; Channel = 'Native'; Index = 3 },
    @{ Provider = 'codex'; Channel = 'Native'; Index = 3 },
    @{ Provider = 'claude'; Channel = 'Native'; Index = 4 },
    @{ Provider = 'codex'; Channel = 'Native'; Index = 4 }
)
if ($Channel -ne 'Both') { $runs = @($runs | Where-Object Channel -eq $Channel) }
$handles = @()
$before = [ordered]@{
    captures = if (Test-Path -LiteralPath (Join-Path $commands.data 'captures-v1')) { @(Get-ChildItem -LiteralPath (Join-Path $commands.data 'captures-v1') -Filter '*.json' -File).Count } else { 0 }
    inbox = if (Test-Path -LiteralPath (Join-Path $commands.data 'inbox')) { @(Get-ChildItem -LiteralPath (Join-Path $commands.data 'inbox') -Filter '*.json' -File).Count } else { 0 }
}
$gate = $null
$results = @()
try {
    if ($HoldLaunchGate) {
        $assemblyRoot = Join-Path $commands.packageRoot 'versions'
        $selectedVersion = $commands.packageVersion
        $payloadAssemblies = Join-Path $assemblyRoot ($selectedVersion + '/bell-tower.exe')
        $repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
        $releaseAssemblies = Join-Path $repository 'src/BellTower/bin/Release/net10.0-windows'
        foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) {
            [void][Reflection.Assembly]::LoadFrom((Join-Path $releaseAssemblies $name))
        }
        $gate = [BellTower.Distribution.InstallationGate]::new($commands.packageRoot)
    }
    foreach ($run in $runs) { $handles += Start-Hook $run.Provider $run.Channel $run.Index }
    foreach ($handle in $handles) { if ($null -eq $handle.Process) { throw "Captured handle missing for $($handle.Provider) $($handle.Channel)." } }
    $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds($BudgetMilliseconds)
    while ($handles.Process.Where({ !$_.HasExited }).Count -gt 0 -and [DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 20
    }
    foreach ($handle in $handles) {
        $elapsed = [int]$handle.Started.ElapsedMilliseconds
        if (!$handle.Process.HasExited) {
            # Terminate only this QA-owned provider shell/launcher process tree.
            $handle.Process.Kill($true)
            $results += [ordered]@{ provider = $handle.Provider; channel = $handle.Channel; index = $handle.Index; passed = $false; elapsedMilliseconds = $elapsed; exitCode = $null; timeout = $true; stdout = ''; stderr = 'exceeded the generated hook budget' }
            continue
        }
        $stdout = $handle.Stdout.GetAwaiter().GetResult()
        $stderr = $handle.Stderr.GetAwaiter().GetResult()
        $neutral = $stdout.Trim() -eq '{}'
        $silent = $handle.Channel -ne 'Native' -or $stderr.Length -eq 0
        $results += [ordered]@{ provider = $handle.Provider; channel = $handle.Channel; index = $handle.Index; passed = ($handle.Process.ExitCode -eq 0 -and $neutral -and $silent); elapsedMilliseconds = $elapsed; exitCode = $handle.Process.ExitCode; timeout = $false; neutralOutput = $neutral; silentNativeIngress = $silent; stdout = $stdout; stderr = $stderr }
    }
    $after = [ordered]@{
        captures = if (Test-Path -LiteralPath (Join-Path $commands.data 'captures-v1')) { @(Get-ChildItem -LiteralPath (Join-Path $commands.data 'captures-v1') -Filter '*.json' -File).Count } else { 0 }
        inbox = if (Test-Path -LiteralPath (Join-Path $commands.data 'inbox')) { @(Get-ChildItem -LiteralPath (Join-Path $commands.data 'inbox') -Filter '*.json' -File).Count } else { 0 }
    }
    $gateStillHeld = $null -ne $gate
    if ($BarrierDirectory) {
        if (Test-Path -LiteralPath $released) { throw 'Maintenance phase was released before hooks were measured.' }
        $barrierStillActive = Test-Path -LiteralPath $entered
    } else { $barrierStillActive = $false }
    $summary = [ordered]@{
        mode = if ($BarrierDirectory) { 'generated shell commands during blocked disposable maintenance phase' } else { 'generated shell commands during explicit launch-gate hold or idle fixture' }
        providers = @('Claude Code (Git Bash)', 'Codex (cmd.exe)')
        scenario = if ($BarrierPhase) { $BarrierPhase } else { 'gate-hold' }
        hookBudgetMilliseconds = $BudgetMilliseconds
        barrierStillActiveAfterHooks = $barrierStillActive
        launchGateHeldForBaselineControl = $gateStillHeld
        channel = $Channel
        before = $before
        after = $after
        completed = @($results | Where-Object passed).Count
        failed = @($results | Where-Object { !$_.passed }).Count
        hooks = $results
    }
    [IO.File]::WriteAllText($ResultFile, ($summary | ConvertTo-Json -Depth 8))
    Write-Output $ResultFile
    if ($summary.failed -gt 0) { exit 1 }
    if ($BarrierDirectory -and !$barrierStillActive) { exit 1 }
}
finally {
    foreach ($handle in $handles) {
        if ($null -eq $handle -or $null -eq $handle.Process) { continue }
        if (!$handle.Process.HasExited) { $handle.Process.Kill($true) }
        $handle.Process.Dispose()
    }
    if ($null -ne $gate) { $gate.Dispose() }
    if ($BarrierDirectory -and (Test-Path -LiteralPath $released)) {
        # Caller owns barrier release; this script only observes it.
    }
}
