param(
    [ValidateSet('claude', 'codex')][string]$Provider,
    [string]$Name,
    [string]$Change,
    [string]$Role,
    [string]$Executable,
    [string]$DataDirectory,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$AgentArguments
)
$ErrorActionPreference = 'Stop'
$launchArguments = @('launch', $Provider)
foreach ($entry in @(@('name', $Name), @('change', $Change), @('role', $Role), @('executable', $Executable), @('data-dir', $DataDirectory))) {
    if ($entry[1]) {
        $launchArguments += @('--' + $entry[0], $entry[1])
    }
}
if ($AgentArguments) {
    $launchArguments += '--'
    $launchArguments += $AgentArguments
}
$oldShell = $env:BELLTOWER_SHELL_PID
try {
    $env:BELLTOWER_SHELL_PID = [string]$PID
    & bell-tower @launchArguments
} finally {
    $env:BELLTOWER_SHELL_PID = $oldShell
}
