param(
    [Parameter(Mandatory)][ValidateSet('Start','Release')][string]$Action,
    [Parameter(Mandatory)][string]$FixtureFile,
    [Parameter(Mandatory)][string]$ResultFile,
    [ValidateSet('LauncherVerbFirst','DirectVerbFirst','DirectOptionsFirst','DirectQuotedVerb')][string]$Invocation = 'LauncherVerbFirst',
    [ValidateRange(1000,30000)][int]$TimeoutMilliseconds = 12000
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$artifacts = Join-Path $repo 'artifacts'
if (!('Qa005CommandLine' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class Qa005CommandLine {
    [DllImport("shell32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int argc);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern IntPtr LocalFree(IntPtr memory);
    public static string[] Parse(string commandLine) {
        int count;
        IntPtr argv = CommandLineToArgvW(commandLine, out count);
        if (argv == IntPtr.Zero) throw new InvalidOperationException("CommandLineToArgvW failed.");
        try {
            var result = new string[count];
            for (int i = 0; i < count; i++) result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? "";
            return result;
        } finally { LocalFree(argv); }
    }
}
'@
}
$target = [IO.Path]::GetFullPath((Split-Path -Parent $ResultFile))
if (!$target.StartsWith((Join-Path $artifacts 'qa-005') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Results must stay in artifacts/qa-005-*.' }
if (Test-Path -LiteralPath $ResultFile) { throw 'Use a new result file for each delayed-child trial.' }
$fixture = Get-Content -LiteralPath $FixtureFile -Raw | ConvertFrom-Json
if (!$fixture.helperInjected -or !$fixture.helperImage) { throw 'The fixture must be created with -UseDelayedTray and the QA005 simulator.' }
$control = [IO.Path]::GetFullPath($fixture.control)
if (!$control.StartsWith((Join-Path $artifacts 'qa-005') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Control files must stay under artifacts/qa-005-*.' }
$entered = Join-Path $control 'can-start-passed.json'
$release = Join-Path $control 'release-before-instance-mutex'
$ownerResult = Join-Path $control 'mutex-result.json'
$identityFile = Join-Path $control 'qa-005-process-identity.json'
$presence = Join-Path $fixture.data 'updates/fixture-tray-owner.json'
$result = $null

if ($Action -eq 'Start') {
    if (Test-Path $entered) { throw 'Fixture control directory is already in use.' }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $root = [IO.Path]::GetFullPath($fixture.data)
    if ($Invocation -eq 'LauncherVerbFirst') {
        $info.FileName = Join-Path $fixture.installation 'bell-tower.exe'
        foreach ($arg in @('tray','--data-dir',$root,'--silent')) { [void]$info.ArgumentList.Add($arg) }
    } else {
        $info.FileName = $fixture.helperImage
        if ($Invocation -eq 'DirectOptionsFirst') { foreach ($arg in @('--data-dir',$root,'tray','--silent')) { [void]$info.ArgumentList.Add($arg) } }
        elseif ($Invocation -eq 'DirectQuotedVerb') { $info.Arguments = '"tray" --data-dir "' + $root + '" --silent' }
        else { foreach ($arg in @('tray','--data-dir',$root,'--silent')) { [void]$info.ArgumentList.Add($arg) } }
    }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.Environment['QA005_DELAYED_TRAY_CONTROL'] = $control
    $launcher = [Diagnostics.Process]::Start($info)
    if ($null -eq $launcher) { throw 'Could not start the disposable delayed-child fixture.' }
    $launcherId = $launcher.Id
    $launcher.Dispose()
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $entered -PathType Leaf) -and $watch.ElapsedMilliseconds -lt $TimeoutMilliseconds) { Start-Sleep -Milliseconds 25 }
    if (!(Test-Path -LiteralPath $entered -PathType Leaf)) { throw 'The simulator did not reach its CanStartTray-passed barrier.' }
    $payload = Get-Content -LiteralPath $entered -Raw | ConvertFrom-Json
    $child = Get-Process -Id ([int]$payload.pid) -ErrorAction Stop
    $ticks = $child.StartTime.ToUniversalTime().Ticks
    $image = [IO.Path]::GetFullPath($child.MainModule.FileName)
    if (![string]::Equals($image,[IO.Path]::GetFullPath($fixture.helperImage),[StringComparison]::OrdinalIgnoreCase)) { throw 'The live child image is outside its fixture payload.' }
    if (![string]::Equals([IO.Path]::GetFullPath($payload.dataRoot),$root,[StringComparison]::OrdinalIgnoreCase)) { throw 'The child used a different data root.' }
    $cim = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$([int]$payload.pid)" -Property ProcessId,CreationDate,ExecutablePath,CommandLine
    if ($null -eq $cim -or ![string]::Equals([IO.Path]::GetFullPath($cim.ExecutablePath),$image,[StringComparison]::OrdinalIgnoreCase)) { throw 'Local CIM identity/image did not match the live helper process.' }
    if ([string]::IsNullOrWhiteSpace($cim.CommandLine)) { throw 'CIM did not expose the helper command line.' }
    $argv = [Qa005CommandLine]::Parse($cim.CommandLine)
    if ($argv.Count -lt 4 -or ![string]::Equals([IO.Path]::GetFullPath($argv[0]),$image,[StringComparison]::OrdinalIgnoreCase)) { throw 'Parsed process argv did not identify the verified helper image.' }
    $argsOnly = @($argv | Select-Object -Skip 1)
    $seenTray = $false; $seenData = $false; $seenSilent = $false; $parsedRoot = $null
    for ($i=0; $i -lt $argsOnly.Count; $i++) {
        $arg = [string]$argsOnly[$i]
        if ($arg -ieq 'tray' -and !$seenTray) { $seenTray=$true; continue }
        if ($arg -ieq '--silent' -and !$seenSilent) { $seenSilent=$true; continue }
        if ($arg -ieq '--data-dir' -and !$seenData -and $i+1 -lt $argsOnly.Count) { $parsedRoot=[string]$argsOnly[++$i]; $seenData=$true; continue }
        if ($arg.StartsWith('--data-dir=',[StringComparison]::OrdinalIgnoreCase) -and !$seenData) { $parsedRoot=$arg.Substring(11); $seenData=$true; continue }
        throw 'Parsed command contains an unapproved tray argument.'
    }
    if (!$seenTray -or !$seenData -or ![string]::Equals([IO.Path]::GetFullPath($parsedRoot),$root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Parsed tray verb/data root do not match the fixture.' }
    if ([Math]::Abs([long]$child.StartTime.ToUniversalTime().Ticks - ([DateTime]$cim.CreationDate).ToUniversalTime().Ticks) -gt 10) { throw 'CIM creation time did not correlate with the verified PID/start-time.' }
    $result = [ordered]@{ action='started-paused'; invocation=$Invocation; childPid=[int]$payload.pid; childStartedUtcTicks=$ticks; wrapperPid=$launcherId; imageVerified=$true; commandFormVerified=$true; dataRootVerified=$true; canStartPassed=[bool]$payload.canStart; createdNew=$null; hasPresence=(Test-Path -LiteralPath $presence); rawCommandLinePersisted=$false }
    [IO.File]::WriteAllText($identityFile, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
} else {
    if (!(Test-Path -LiteralPath $entered -PathType Leaf) -or !(Test-Path -LiteralPath $identityFile -PathType Leaf)) { throw 'No verified paused child is recorded.' }
    $payload = Get-Content -LiteralPath $entered -Raw | ConvertFrom-Json
    $identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
    $ownedPid = [int]$identity.childPid
    $ownedTicks = [long]$identity.childStartedUtcTicks
    $live = Get-Process -Id $ownedPid -ErrorAction Stop
    if ($live.StartTime.ToUniversalTime().Ticks -ne $ownedTicks -or ![string]::Equals([IO.Path]::GetFullPath($live.MainModule.FileName),[IO.Path]::GetFullPath($fixture.helperImage),[StringComparison]::OrdinalIgnoreCase)) { throw 'The recorded fixture PID/start/image changed; refusing to release or signal a different process.' }
    [IO.File]::WriteAllText($release, [DateTimeOffset]::UtcNow.ToString('o'), [Text.UTF8Encoding]::new($false))
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $ownerResult -PathType Leaf) -and $watch.ElapsedMilliseconds -lt $TimeoutMilliseconds) { Start-Sleep -Milliseconds 25 }
    $exited = $false
    try {
        $child = Get-Process -Id $ownedPid -ErrorAction Stop
        if ($child.StartTime.ToUniversalTime().Ticks -ne $ownedTicks) { throw 'The fixture PID was reused; refusing to signal or classify the new process.' }
        $child.WaitForExit(500)
        $exited = $child.HasExited
    } catch [Microsoft.PowerShell.Commands.ProcessCommandException] { $exited = $true } catch [ArgumentException] { $exited = $true }
    $mutex = if (Test-Path -LiteralPath $ownerResult) { Get-Content -LiteralPath $ownerResult -Raw | ConvertFrom-Json } else { $null }
    $result = [ordered]@{ action='released-and-checked'; childPid=$ownedPid; childExited=$exited; createdNew=if($mutex){[bool]$mutex.createdNew}else{$null}; ownerPresenceCreated=(Test-Path -LiteralPath $presence); expectedCreatedNew=$false; rawCommandLinePersisted=$false }
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
[IO.File]::WriteAllText($ResultFile, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output $ResultFile
if ($Action -eq 'Release' -and (!$result.childExited -or $result.createdNew -ne $false -or $result.ownerPresenceCreated)) { exit 1 }
