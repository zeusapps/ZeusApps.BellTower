#Requires -Version 7.4
# Analysis 007 proxy timing for the work install-dev.ps1 performs while holding the launch gate in the Rollback
# pointer step (Complete-RollbackPointer). Read-only against the repository: it loads the Release assemblies,
# defines the installer's functions without running its program, and touches only a scratch directory.
# It does not take any Bell Tower gate or lock, start a tray, or use an installation or data directory.
param(
    [Parameter(Mandatory)][string]$Scratch,
    [int]$Iterations = 25,
    [int]$PayloadFiles = 60
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$assemblies = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows'
foreach ($name in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll', 'bell-tower.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $assemblies $name)) }
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo 'scripts/install-dev.ps1'), [ref]$null, [ref]$null)
foreach ($definition in @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] })) { . ([scriptblock]::Create($definition.Extent.Text)) }

New-Item -ItemType Directory -Path $Scratch -Force | Out-Null
$selection = Join-Path $Scratch 'current.json'
$snapshot = Join-Path $Scratch 'snapshot-current.json'
$proposed = Join-Path $Scratch 'proposed-current.json'
[BellTower.Distribution.DistributionJson]::Write($snapshot, [BellTower.Distribution.VersionSelection]::new('0.1.8', '0.1.7', [Guid]::NewGuid().ToString('N')))
[BellTower.Distribution.DistributionJson]::Write($proposed, [BellTower.Distribution.VersionSelection]::new('0.1.9001', '0.1.8', [Guid]::NewGuid().ToString('N')))
$manifestPath = Join-Path $Scratch 'operation.json'
$files = @(1..$PayloadFiles | ForEach-Object { [ordered]@{ path = "lib\file$_.dll"; sha256 = ('a' * 64) } })
$plan = [ordered]@{ Id = ('1' * 32); Token = ('2' * 64); Root = $Scratch; DataDirectory = $Scratch; Original = '0.1.8'; Target = '0.1.8'; ContractHash = ('3' * 64); Created = [DateTimeOffset]::UtcNow; Owner = [ordered]@{ Pid = $PID; StartedUtcTicks = 1 }; HadTray = $true }
$manifest = [pscustomobject][ordered]@{ originalPayloadFiles = $files; originalMonitoringFiles = @([ordered]@{ name = 'current.json'; sha256 = ('b' * 64) }); coordinationPlan = $plan; state = 'restored-monitoring-candidate-pointer' }

$samples = [Collections.Generic.List[double]]::new()
foreach ($i in 1..$Iterations) {
    Copy-Item -LiteralPath $proposed -Destination $selection -Force
    $watch = [Diagnostics.Stopwatch]::StartNew()
    # Two Get-RollbackPointerBinding passes (one inside Assert-RollbackPointerIntent): 2 hashes + 2 selection reads each.
    foreach ($pass in 1..2) { [void](Hash $snapshot); [void](Hash $proposed); [void](Get-SelectionFields $proposed); [void](Get-SelectionFields $snapshot) }
    # Post-gate record check proxy: two record hashes and the current selection hash.
    [void](Hash $snapshot); [void](Hash $proposed); [void](Hash $selection)
    $manifest | Add-Member -NotePropertyName rollbackPointerTransition -NotePropertyValue ([ordered]@{ plan = $plan; sourceSelectionSha256 = ('c' * 64) }) -Force
    Save-Json $manifestPath $manifest
    # Restore-SnapshotFile proxy: copy to temp, atomic move, two hashes; then the verification hash.
    $temporary = $selection + '.' + [Guid]::NewGuid().ToString('N') + '.devtmp'
    [IO.File]::Copy($snapshot, $temporary, $false); [IO.File]::Move($temporary, $selection, $true)
    [void](Hash $snapshot); [void](Hash $selection); [void](Hash $selection)
    $manifest.PSObject.Properties.Remove('rollbackPointerTransition')
    Save-Json $manifestPath $manifest
    $watch.Stop()
    $samples.Add($watch.Elapsed.TotalMilliseconds)
}
$sorted = @($samples | Sort-Object)
[ordered]@{
    probe = 'analysis-007 launch-gate pointer-step proxy'; iterations = $Iterations; payloadFileEntries = $PayloadFiles
    firstMs = [Math]::Round($samples[0], 1); medianMs = [Math]::Round($sorted[[int][Math]::Floor($sorted.Count / 2)], 1); maxMs = [Math]::Round($sorted[-1], 1)
    pwsh = $PSVersionTable.PSVersion.ToString(); manifestBytes = (Get-Item -LiteralPath $manifestPath).Length
} | ConvertTo-Json
