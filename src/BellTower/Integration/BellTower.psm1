# Configuration and exported role functions are supplied by Bell Tower setup.
function Invoke-BellTowerSession {
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [string]$Provider,
        [string]$Role,
        [string]$Name,
        [string]$Change,
        [string]$Directory,
        [switch]$NoBackground,
        [switch]$Silent,
        [string]$Executable,
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$AgentArguments
    )

    $commandName = if ($Executable) { $Executable } else { $Provider }
    $agent = Get-Command -Name $commandName -CommandType Application, ExternalScript -ErrorAction Stop | Select-Object -First 1
    $environmentNames = @(
        'BELLTOWER_ATTACHMENT', 'BELLTOWER_DATA_DIR', 'BELLTOWER_NAME',
        'BELLTOWER_CHANGE', 'BELLTOWER_ROLE', 'BELLTOWER_SHELL_PID',
        'BELLTOWER_DEDICATED', 'BELLTOWER_MANAGED_TITLE', 'BELLTOWER_MANAGED_BACKGROUND'
    )
    $previousEnvironment = @{}
    foreach ($variableName in $environmentNames) {
        $previousEnvironment[$variableName] = [Environment]::GetEnvironmentVariable($variableName, 'Process')
    }
    $originalTitle = $null
    $originalLocation = Get-Location
    $managedBackground = $false
    $agentExitCode = 1
    $attachmentId = [Guid]::NewGuid().ToString('N')
    try {
        if ($Directory) {
            $targetDirectory = Get-Item -LiteralPath $Directory -ErrorAction Stop
            if (!$targetDirectory.PSIsContainer) { throw 'The session directory must be a folder.' }
            Set-Location -LiteralPath $targetDirectory.FullName -ErrorAction Stop
        }
        $env:BELLTOWER_ATTACHMENT = $attachmentId
        $env:BELLTOWER_DATA_DIR = $script:BellTowerDataDirectory
        $env:BELLTOWER_NAME = $Name
        $env:BELLTOWER_CHANGE = $Change
        $env:BELLTOWER_ROLE = $Role
        $env:BELLTOWER_SHELL_PID = [string]$PID
        $env:BELLTOWER_DEDICATED = $null
        $env:BELLTOWER_MANAGED_TITLE = $null
        $env:BELLTOWER_MANAGED_BACKGROUND = $null

        $nativeArguments = @($AgentArguments)
        $remote = @($nativeArguments | Where-Object { $_ -eq '--remote' -or $_ -like '--remote=*' }).Count -gt 0
        if ($Provider -eq 'codex' -and !$remote) {
            if ($nativeArguments -notcontains '--no-daemon') {
                $nativeArguments = @('--no-daemon') + $nativeArguments
            }
            $env:BELLTOWER_DEDICATED = '1'
        }

        if ($env:TERM_PROGRAM -eq 'vscode' -or $env:WT_SESSION) {
            try {
                $originalTitle = $Host.UI.RawUI.WindowTitle
                $title = if ($Name) { $Name } elseif ($Change) { $Change } else { Split-Path (Get-Location).Path -Leaf }
                $title = -join ($title.ToCharArray() | Where-Object { ![char]::IsControl($_) } | Select-Object -First 100)
                $Host.UI.RawUI.WindowTitle = $title
                $env:BELLTOWER_MANAGED_TITLE = '1'
                if (!$NoBackground -and ($Provider -ne 'codex' -or !$remote)) {
                    $env:BELLTOWER_MANAGED_BACKGROUND = '1'
                    $managedBackground = $true
                    try {
                        $preparedJson = & $script:BellTowerExecutable prepare-session --attachment $attachmentId --directory (Get-Location).Path --data-dir $script:BellTowerDataDirectory | Out-String
                        if ($LASTEXITCODE -eq 0) {
                            $prepared = $preparedJson | ConvertFrom-Json
                            if ($prepared.palette.background -match '^#[0-9a-fA-F]{6}$') {
                                [Console]::Write($prepared.colors)
                                $Host.UI.RawUI.WindowTitle = $prepared.title
                            }
                        }
                    } catch {
                        # Hooks can prepare presentation later if pre-launch decoration is unavailable.
                    }
                }
            } catch {
                $originalTitle = $null
            }
        }
        try {
            $trayArguments = @('start', '--data-dir', $script:BellTowerDataDirectory)
            if ($Silent) { $trayArguments += '--silent' }
            & $script:BellTowerExecutable @trayArguments | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Warning 'Bell Tower could not start. The agent will still run.' }
        } catch {
            Write-Warning 'Bell Tower is unavailable. The agent will still run.'
        }

        # The native agent is a child of this PowerShell process, never of bell-tower.exe.
        & $agent.Source @nativeArguments
        $agentExitCode = $LASTEXITCODE
    } finally {
        try {
            & $script:BellTowerExecutable detach --attachment $attachmentId --data-dir $script:BellTowerDataDirectory | Out-Null
        } catch {
            # Hook/liveness reconciliation remains available when Bell Tower is unavailable.
        }
        if ($null -ne $originalTitle) {
            try { $Host.UI.RawUI.WindowTitle = $originalTitle } catch { }
        }
        if ($managedBackground) {
            # Restore this terminal's configured colors; no global theme or keyboard input is touched.
            try { [Console]::Write("$([char]27)]110$([char]27)\$([char]27)]111$([char]27)\") } catch { }
        }
        if ($Directory) { Set-Location -LiteralPath $originalLocation.Path }
        foreach ($variableName in $environmentNames) {
            [Environment]::SetEnvironmentVariable($variableName, $previousEnvironment[$variableName], 'Process')
        }
        $global:LASTEXITCODE = $agentExitCode
    }
}
