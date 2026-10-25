# Analysis 009: read-only probe of the WMI (cimwin32) -> CIM DateTime -> ToUniversalTime pipeline for historical
# timestamps on either side of a DST change. Compares CIM_DataFile.LastModified with the filesystem's UTC write time
# for a few repository files. Records only month, DST flag and tick differences; mutates nothing.
param([string]$Root, [string]$Output)
$ErrorActionPreference = 'Stop'
$zone = [TimeZoneInfo]::Local
$files = Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "'" } |
    Group-Object { $zone.IsDaylightSavingTime($_.LastWriteTimeUtc) } |
    ForEach-Object { $_.Group | Sort-Object LastWriteTimeUtc | Select-Object -First 3 }
$rows = foreach ($file in $files) {
    $escaped = $file.FullName.Replace('\', '\\')
    $cim = Get-CimInstance -ClassName CIM_DataFile -Filter "Name='$escaped'" -Property LastModified -OperationTimeoutSec 20
    if ($null -eq $cim) { continue }
    $fsUtc = $file.LastWriteTimeUtc.Ticks
    $cimUtc = ([DateTime]$cim.LastModified).ToUniversalTime().Ticks
    [ordered]@{
        writeMonthUtc = $file.LastWriteTimeUtc.ToString('yyyy-MM'); writtenDuringDst = $zone.IsDaylightSavingTime($file.LastWriteTimeUtc)
        cimKind = ([DateTime]$cim.LastModified).Kind.ToString(); diffTicks = ($cimUtc - $fsUtc); diffHours = [Math]::Round(($cimUtc - $fsUtc) / 36000000000.0, 3)
    }
}
$result = [ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); timeZone = $zone.Id; nowIsDst = $zone.IsDaylightSavingTime([DateTime]::UtcNow); rows = @($rows) }
$json = $result | ConvertTo-Json -Depth 5
if ($Output) { [IO.File]::WriteAllText($Output, $json) }
$json
