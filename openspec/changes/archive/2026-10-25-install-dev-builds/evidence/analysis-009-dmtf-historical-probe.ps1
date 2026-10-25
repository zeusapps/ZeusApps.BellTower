# Analysis 009: read-only check of how a historical WMI datetime (Win32_OperatingSystem.InstallDate) is encoded
# and converted by CIM, against the registry's UTC InstallDate (Unix seconds). Mutates nothing.
param([string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Management
$raw = [string]([Management.ManagementObjectSearcher]::new('SELECT InstallDate FROM Win32_OperatingSystem').Get() | Select-Object -First 1)['InstallDate']
$cim = (Get-CimInstance Win32_OperatingSystem -Property InstallDate).InstallDate
$seconds = [long](Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name InstallDate)
$truthUtc = [DateTimeOffset]::FromUnixTimeSeconds($seconds).UtcDateTime
$zone = [TimeZoneInfo]::Local
$result = [ordered]@{
    timeZone = $zone.Id
    installDuringDst = $zone.IsDaylightSavingTime($truthUtc)
    rawDmtfOffset = if ($raw.Length -ge 25) { $raw.Substring(21) } else { $raw }
    rawWallMinusTruthHours = [Math]::Round(([DateTime]::ParseExact($raw.Substring(0, 14), 'yyyyMMddHHmmss', $null) - $truthUtc).TotalHours, 3)
    cimKind = $cim.Kind.ToString()
    cimToUtcMinusTruthSeconds = [Math]::Round(($cim.ToUniversalTime() - $truthUtc).TotalSeconds, 3)
}
$json = $result | ConvertTo-Json
if ($Output) { [IO.File]::WriteAllText($Output, $json) }
$json
