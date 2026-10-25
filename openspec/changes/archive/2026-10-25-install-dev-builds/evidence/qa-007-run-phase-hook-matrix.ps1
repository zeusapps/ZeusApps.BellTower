param(
    [Parameter(Mandatory)][string]$CommandsFile,
    [Parameter(Mandatory)][string]$BarrierDirectory,
    [Parameter(Mandatory)][ValidateSet('Switch','Rollback','Cleanup','Retire')][string]$Phase,
    [string]$BarrierName,
    [Parameter(Mandatory)][string]$ResultFile,
    [ValidateRange(3500,60000)][int]$MinimumBlockedMilliseconds = 3500,
    [ValidateRange(500,10000)][int]$HookBudgetMilliseconds = 3000,
    [string]$ClaudeShell = 'C:/Program Files/Git/bin/bash.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
$resultParent = [IO.Path]::GetFullPath((Split-Path -Parent $ResultFile))
if (!$resultParent.StartsWith((Join-Path $artifacts 'qa-007') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Results must stay under artifacts/qa-007/.' }
if (Test-Path -LiteralPath $ResultFile) { throw 'Use a fresh result file; failed samples are never overwritten.' }
$name=if($BarrierName){$BarrierName}else{$Phase}
$entered = Join-Path $BarrierDirectory ($name + '.entered')
$release = Join-Path $BarrierDirectory ($name + '.release')
if (!(Test-Path -LiteralPath $entered -PathType Leaf) -or (Test-Path -LiteralPath $release)) { throw "Expected unreleased $Phase barrier." }
$commands = Get-Content -LiteralPath $CommandsFile -Raw | ConvertFrom-Json
$runner = Join-Path $PSScriptRoot 'qa-007-hook-trial.ps1'
$timer = [Diagnostics.Stopwatch]::StartNew()
$trials = [Collections.Generic.List[object]]::new()
$primaryError = $null
try {
    foreach ($trial in @('LegacyClaude','LegacyCodex','LegacyMixed','NativeMixed')) {
        $trialFile = Join-Path $resultParent ("$Phase-$trial-" + [guid]::NewGuid().ToString('N') + '.json')
        $output = & $runner -CommandsFile $CommandsFile -ResultFile $trialFile -Trial $trial -BarrierDirectory $BarrierDirectory -BarrierPhase $name -ClaudeShell $ClaudeShell -BudgetMilliseconds $HookBudgetMilliseconds
        $trialResult = Get-Content -LiteralPath $trialFile -Raw | ConvertFrom-Json
        $hookPass=(@($trialResult.hooks).Count -gt 0) -and (@($trialResult.hooks | Where-Object { !$_.passed }).Count -eq 0) -and !$trialResult.harnessError
        $trials.Add([ordered]@{trial=$trial;hookPass=$hookPass;barrierStillHeld=[bool]$trialResult.barrierStillActiveAfterHooks;hooks=@($trialResult.hooks);durableCaptureDelta=$trialResult.durableCaptureDelta;durableInboxDelta=$trialResult.durableInboxDelta;resultFile=[IO.Path]::GetFileName($trialFile)})
    }
    $remaining = $MinimumBlockedMilliseconds - $timer.ElapsedMilliseconds
    if ($remaining -gt 0) { Start-Sleep -Milliseconds $remaining }
    if (!(Test-Path -LiteralPath $entered -PathType Leaf) -or (Test-Path -LiteralPath $release)) { throw 'Maintenance barrier ceased to be held before the trial matrix finished.' }
} catch { $primaryError = $_.Exception.Message }
finally {
    $remaining = $MinimumBlockedMilliseconds - $timer.ElapsedMilliseconds
    if ($remaining -gt 0) { Start-Sleep -Milliseconds $remaining }
    # This runner owns only the explicit disposable phase barrier supplied to it.
    [IO.File]::WriteAllText($release, [DateTimeOffset]::UtcNow.ToString('O'), [Text.UTF8Encoding]::new($false))
}
$timer.Stop()
$result = [ordered]@{
    mode='generated hooks and NativeAOT helper with synthetic provider homes/data'
    phase=$Phase
    minimumBlockedMilliseconds=$MinimumBlockedMilliseconds
    barrierElapsedMilliseconds=$timer.ElapsedMilliseconds
    hookBudgetMilliseconds=$HookBudgetMilliseconds
    trials=@($trials)
    primaryError=$primaryError
    complete=($null -eq $primaryError -and $trials.Count -eq 4 -and @($trials | Where-Object { !$_.hookPass -or !$_.barrierStillHeld }).Count -eq 0 -and $timer.ElapsedMilliseconds -ge $MinimumBlockedMilliseconds)
}
[IO.File]::WriteAllText($ResultFile, ($result | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
Get-Content -LiteralPath $ResultFile
if (!$result.complete) { exit 1 }
