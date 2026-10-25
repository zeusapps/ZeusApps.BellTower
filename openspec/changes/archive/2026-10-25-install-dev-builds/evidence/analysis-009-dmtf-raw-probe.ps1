# Analysis 009: read-only comparison of the raw WMI DMTF strings with the CIM (MMI) DateTime values, for this
# process's Win32_Process.CreationDate and one file's CIM_DataFile.LastModified. Mutates nothing.
param([string]$File, [string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Management
$escaped = $File.Replace('\', '\\')
$process = [Management.ManagementObjectSearcher]::new("SELECT ProcessId,CreationDate FROM Win32_Process WHERE ProcessId=$PID").Get() | Select-Object -First 1
$data = [Management.ManagementObjectSearcher]::new("SELECT Name,LastModified FROM CIM_DataFile WHERE Name='$escaped'").Get() | Select-Object -First 1
$cimProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$PID" -Property CreationDate
$cimData = Get-CimInstance CIM_DataFile -Filter "Name='$escaped'" -Property LastModified
$format = 'yyyy-MM-ddTHH:mm:ss.fffffff'
$result = [ordered]@{
    processRawDmtf = [string]$process['CreationDate']
    processCimLocal = $cimProcess.CreationDate.ToString($format)
    processLiveUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString($format)
    fileRawDmtf = [string]$data['LastModified']
    fileCimLocal = $cimData.LastModified.ToString($format)
    fileFsUtc = (Get-Item -LiteralPath $File).LastWriteTimeUtc.ToString($format)
}
$json = $result | ConvertTo-Json
if ($Output) { [IO.File]::WriteAllText($Output, $json) }
$json
