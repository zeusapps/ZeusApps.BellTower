#Requires -Version 7.4
# Analysis 011 read-only probe: classify synthetic command lines (no process is started or inspected) with the
# repository Release build of TrayInvocationCommand, and list the CLI root command names the classifier does not know.
param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path, [string]$ResultFile)
$ErrorActionPreference = 'Stop'
if (!$ResultFile) { $ResultFile = Join-Path $PSScriptRoot 'analysis-011-classifier-probe-results.json' }
$assemblies = Join-Path $Repo 'src/BellTower/bin/Release/net10.0-windows'
foreach ($n in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $n)) }
$exe = 'C:\fixture\install\versions\0.1.9001\bell-tower.exe'
$data = 'C:\fixture\data'
$results = [ordered]@{}
foreach ($verb in @('statusline', 'status-line', 'event', 'tray')) {
    $line = '"' + $exe + '" ' + $verb + ' --provider claude --data-dir "' + $data + '"'
    if ($verb -eq 'tray') { $line = '"' + $exe + '" tray --data-dir "' + $data + '"' }
    try { $results[$verb] = [string][BellTower.IO.TrayInvocationCommand]::Classify($line, $exe, $data) }
    catch { $results[$verb] = 'throws: ' + $_.Exception.InnerException.GetType().Name + ': ' + $_.Exception.InnerException.Message }
}
# Root command names registered through `: base("<name>"` in src/BellTower/Cli versus the classifier's NonTrayCommands set.
$names = @(Get-ChildItem -LiteralPath (Join-Path $Repo 'src/BellTower/Cli') -Filter '*.cs' | ForEach-Object { [regex]::Matches((Get-Content -LiteralPath $_.FullName -Raw), ': base\("([a-z0-9-]+)"') | ForEach-Object { $_.Groups[1].Value } } | Sort-Object -Unique)
$field = [BellTower.IO.TrayInvocationCommand].GetField('NonTrayCommands', [Reflection.BindingFlags]'NonPublic,Static')
$known = @($field.GetValue($null))
$results['cliCommandsUnknownToClassifier'] = @($names | Where-Object { $_ -ne 'tray' -and $_ -notin $known })
$results['classifierEntriesWithoutCliCommand'] = @($known | Where-Object { $_ -notin $names -and !$_.StartsWith('-') -and $_ -notin @('help', 'version', '__apply-update') })
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultFile -Encoding utf8
$results | ConvertTo-Json -Depth 5
