# Analysis 009: read-only probe of the CIM CreationDate vs Process.StartTime correlation that
# Get-InstallationTrayStarts and Test-TrayProcessEnded rely on (10-tick bound). Records aggregate
# counts only: no process names, paths or command lines. Mutates nothing.
param([string]$Output)
$ErrorActionPreference = 'Stop'
$enumeratedAt = [DateTime]::UtcNow.Ticks
$items = @(Get-CimInstance -ClassName Win32_Process -Property ProcessId,CreationDate -OperationTimeoutSec 10)
$diffs = [Collections.Generic.List[long]]::new()
$kinds = @{}
$accessDenied = 0; $gone = 0; $noDate = 0; $startedAfterEnumeration = 0; $other = 0
foreach ($item in $items) {
    if ($null -eq $item.CreationDate) { $noDate++; continue }
    $kind = ([DateTime]$item.CreationDate).Kind.ToString(); $kinds[$kind] = 1 + [int]$kinds[$kind]
    $process = $null
    try {
        try { $process = [Diagnostics.Process]::GetProcessById([int]$item.ProcessId) } catch [ArgumentException] { $gone++; continue }
        $live = $process.StartTime.ToUniversalTime().Ticks
        if ($live -gt $enumeratedAt) { $startedAfterEnumeration++ }
        $diffs.Add([Math]::Abs($live - ([DateTime]$item.CreationDate).ToUniversalTime().Ticks))
    } catch [ComponentModel.Win32Exception] { $accessDenied++ }
    catch { $other++ }
    finally { if ($process) { $process.Dispose() } }
}
# Access-denied behaviour of the helper's calls on a protected process (System, PID 4), when present.
$protected = [ordered]@{ pid = 4 }
try {
    $p = [Diagnostics.Process]::GetProcessById(4)
    $protected.getProcessById = 'ok'
    try { $p.Refresh(); $protected.hasExited = [string]$p.HasExited } catch { $protected.hasExited = $_.Exception.GetType().FullName }
    try { [void]$p.StartTime; $protected.startTime = 'ok' } catch { $protected.startTime = $_.Exception.GetType().FullName }
    $p.Dispose()
} catch { $protected.getProcessById = $_.Exception.GetType().FullName }
$sorted = @($diffs | Sort-Object)
$result = [ordered]@{
    utc = [DateTime]::UtcNow.ToString('o'); timeZone = [TimeZoneInfo]::Local.Id; psVersion = $PSVersionTable.PSVersion.ToString()
    lastBootUtc = (Get-CimInstance Win32_OperatingSystem -Property LastBootUpTime).LastBootUpTime.ToUniversalTime().ToString('o')
    cimProcesses = $items.Count; compared = $sorted.Count; goneDuringProbe = $gone; accessDenied = $accessDenied; noCreationDate = $noDate; otherErrors = $other
    creationDateKinds = $kinds; startedAfterEnumeration = $startedAfterEnumeration
    diffTicks = [ordered]@{
        max = if ($sorted.Count) { $sorted[-1] } else { $null }
        within10 = @($sorted | Where-Object { $_ -le 10 }).Count
        over10 = @($sorted | Where-Object { $_ -gt 10 }).Count
        overOneSecond = @($sorted | Where-Object { $_ -gt 10000000 }).Count
    }
    protectedProcess = $protected
}
$json = $result | ConvertTo-Json -Depth 5
if ($Output) { [IO.File]::WriteAllText($Output, $json) }
$json
