param(
    [Parameter(Mandatory)][string]$CommandsFile,
    [Parameter(Mandatory)][string]$ResultFile,
    [ValidateSet('LegacyClaude', 'LegacyCodex', 'LegacyMixed', 'NativeMixed')][string]$Trial,
    [string]$BarrierDirectory,
    [string]$BarrierPhase,
    [string]$ClaudeShell = 'C:/Program Files/Git/bin/bash.exe',
    [ValidateRange(500, 10000)][int]$BudgetMilliseconds = 3000
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$target = [IO.Path]::GetFullPath((Split-Path -Parent $ResultFile))
if (!$target.StartsWith((Join-Path $artifacts 'qa-006') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Hook results must stay in artifacts/qa-006-*.'
}
if (Test-Path -LiteralPath $ResultFile) { throw 'Use a new result file for every trial.' }
$commands = Get-Content -LiteralPath $CommandsFile -Raw | ConvertFrom-Json
if (!(Test-Path -LiteralPath $ClaudeShell -PathType Leaf)) { throw 'Git Bash is unavailable; Claude checks cannot be claimed.' }
$barrierEntered = $null
$barrierReleased = $null
if ($BarrierDirectory) {
    if ([string]::IsNullOrWhiteSpace($BarrierPhase)) { throw '-BarrierPhase is required with -BarrierDirectory.' }
    $barrierEntered = Join-Path $BarrierDirectory ($BarrierPhase + '.entered')
    $barrierReleased = Join-Path $BarrierDirectory ($BarrierPhase + '.release')
    if (!(Test-Path -LiteralPath $barrierEntered -PathType Leaf) -or (Test-Path -LiteralPath $barrierReleased)) {
        throw 'Run the trial only while the requested maintenance phase is entered and unreleased.'
    }
}
$selection = switch ($Trial) {
    'LegacyClaude' { @(@{ Provider='claude'; Channel='Legacy'; Index=1 }) }
    'LegacyCodex' { @(@{ Provider='codex'; Channel='Legacy'; Index=1 }) }
    'LegacyMixed' { @(@{ Provider='claude'; Channel='Legacy'; Index=1 }, @{ Provider='codex'; Channel='Legacy'; Index=1 }) }
    'NativeMixed' { @(@{ Provider='claude'; Channel='Native'; Index=1 }, @{ Provider='codex'; Channel='Native'; Index=1 }) }
}
function Start-GeneratedHook([string]$Provider, [string]$Channel, [int]$Index) {
    $command = $commands.commands.($Provider + $Channel)
    if ([string]::IsNullOrWhiteSpace($command)) { throw "Missing generated $Provider $Channel command." }
    if ($Provider -eq 'claude') {
        $info = [Diagnostics.ProcessStartInfo]::new($ClaudeShell)
        [void]$info.ArgumentList.Add('-c')
        [void]$info.ArgumentList.Add($command)
    } else {
        $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32/cmd.exe'))
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
    $info.Environment['BELLTOWER_ATTACHMENT'] = "qa006-$Trial-$Provider-$Index"
    $info.Environment['BELLTOWER_DEDICATED'] = '1'
    $started = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($info)
    if ($null -eq $process) { throw "QA-owned $Provider hook shell did not start." }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $input = [ordered]@{
        session_id = "qa006-$Trial-$Provider-$Index"
        hook_event_name = 'Stop'
        cwd = (Join-Path $commands.data 'qa006-synthetic-workspace')
        last_assistant_message = "synthetic $Trial $Provider"
    } | ConvertTo-Json -Compress
    $process.StandardInput.Write($input)
    $process.StandardInput.Close()
    return [pscustomobject]@{ Provider=$Provider; Channel=$Channel; Process=$process; Stdout=$stdout; Stderr=$stderr; Started=$started }
}
$capturePath = Join-Path $commands.data 'captures-v1'
$inboxPath = Join-Path $commands.data 'inbox'
$before = [ordered]@{
    captures = if (Test-Path -LiteralPath $capturePath) { @(Get-ChildItem -LiteralPath $capturePath -Filter '*.json' -File).Count } else { 0 }
    inbox = if (Test-Path -LiteralPath $inboxPath) { @(Get-ChildItem -LiteralPath $inboxPath -Filter '*.json' -File).Count } else { 0 }
}
$handles = [Collections.Generic.List[object]]::new()
$results = [Collections.Generic.List[object]]::new()
$failure = $null
try {
    foreach ($scenario in $selection) { $handles.Add((Start-GeneratedHook $scenario.Provider $scenario.Channel $scenario.Index)) }
    while (($handles | Where-Object { !$_.Process.HasExited -and $_.Started.ElapsedMilliseconds -lt $BudgetMilliseconds }).Count -gt 0) { Start-Sleep -Milliseconds 10 }
    foreach ($handle in $handles) {
        $elapsed = [int]$handle.Started.ElapsedMilliseconds
        if (!$handle.Process.HasExited) {
            # Terminate only the shell/process tree launched by this evidence harness.
            $handle.Process.Kill($true)
            $handle.Process.WaitForExit()
            $results.Add([ordered]@{ provider=$handle.Provider; channel=$handle.Channel; passed=$false; timeout=$true; elapsedMilliseconds=$elapsed; exitCode=$null; neutralOutput=$false; silentNativeIngress=$false })
            continue
        }
        $stdout = $handle.Stdout.GetAwaiter().GetResult()
        $stderr = $handle.Stderr.GetAwaiter().GetResult()
        $neutral = $stdout.Trim() -eq '{}'
        $silent = $handle.Channel -ne 'Native' -or $stderr.Length -eq 0
        $withinBudget = $elapsed -le $BudgetMilliseconds
        $results.Add([ordered]@{ provider=$handle.Provider; channel=$handle.Channel; passed=($withinBudget -and $handle.Process.ExitCode -eq 0 -and $neutral -and $silent); timeout=(!$withinBudget); elapsedMilliseconds=$elapsed; exitCode=$handle.Process.ExitCode; neutralOutput=$neutral; silentNativeIngress=$silent })
    }
} catch { $failure = $_.Exception.Message }
finally {
    foreach ($handle in $handles) {
        if ($null -ne $handle -and $null -ne $handle.Process) {
            if (!$handle.Process.HasExited) { $handle.Process.Kill($true); $handle.Process.WaitForExit() }
            $handle.Process.Dispose()
        }
    }
}
$after = [ordered]@{
    captures = if (Test-Path -LiteralPath $capturePath) { @(Get-ChildItem -LiteralPath $capturePath -Filter '*.json' -File).Count } else { 0 }
    inbox = if (Test-Path -LiteralPath $inboxPath) { @(Get-ChildItem -LiteralPath $inboxPath -Filter '*.json' -File).Count } else { 0 }
}
$barrierStillActive = if ($barrierEntered) { (Test-Path -LiteralPath $barrierEntered) -and !(Test-Path -LiteralPath $barrierReleased) } else { $false }
$summary = [ordered]@{
    trial=$Trial; hookBudgetMilliseconds=$BudgetMilliseconds; providers=@('Claude Code (Git Bash)','Codex (cmd.exe)')
    barrierStillActiveAfterHooks=$barrierStillActive; before=$before; after=$after; durableCaptureDelta=($after.captures-$before.captures); durableInboxDelta=($after.inbox-$before.inbox)
    hooks=@($results); harnessError=$failure
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
[IO.File]::WriteAllText($ResultFile, ($summary | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output $ResultFile
if ($failure -or @($results | Where-Object { !$_.passed }).Count -gt 0 -or ($BarrierDirectory -and !$barrierStillActive)) { exit 1 }
