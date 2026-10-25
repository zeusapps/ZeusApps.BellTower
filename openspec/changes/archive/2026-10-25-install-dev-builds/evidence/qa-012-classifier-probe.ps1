#Requires -Version 7.4
# QA 012 read-only probe: classify synthetic command lines with the Release TrayInvocationCommand.
# Names are parsed from root.Subcommands.Add(new XCommand(...)) lines of CliApplication.cs and mapped to each Command's base("<name>") literal.
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
[void][Reflection.Assembly]::LoadFrom((Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows/bell-tower.dll'))
$exe = 'C:\x\versions\1.0.0\bell-tower.exe'; $data = 'C:\x\data'
$registered = [regex]::Matches((Get-Content (Join-Path $repo 'src/BellTower/Cli/CliApplication.cs') -Raw), 'Subcommands\.Add\(new (\w+)\(') | ForEach-Object { $_.Groups[1].Value }
$rows = foreach ($type in $registered) {
    $src = Get-Content (Join-Path $repo "src/BellTower/Cli/$type.cs") -Raw
    $name = [regex]::Match($src, 'base\("([^"]+)"').Groups[1].Value
    $line = "`"$exe`" $name --provider claude --data-dir `"$data`""
    $kind = try { [string][BellTower.IO.TrayInvocationCommand]::Classify($line, $exe, $data) } catch { 'THROWS: ' + $_.Exception.InnerException.Message }
    [ordered]@{ type = $type; name = $name; kind = $kind }
}
$extra = foreach ($v in 'statusline', 'status-line') { foreach ($p in 'claude', 'codex') {
    $line = "`"$exe`" $v --provider $p --data-dir `"$data`""
    [ordered]@{ verb = $v; provider = $p; kind = $(try { [string][BellTower.IO.TrayInvocationCommand]::Classify($line, $exe, $data) } catch { 'THROWS: ' + $_.Exception.InnerException.Message }) }
} }
$bad = @($rows | Where-Object { $_.name -ne 'tray' -and $_.kind -ne 'NotTray' })
[ordered]@{ registeredCount = @($registered).Count; commands = $rows; statuslineProbe = $extra; nonTrayMisclassified = $bad } | ConvertTo-Json -Depth 5
