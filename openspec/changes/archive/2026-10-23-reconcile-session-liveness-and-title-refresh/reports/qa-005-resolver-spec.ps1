$ErrorActionPreference = 'Stop'
$qaRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$qaChange = Join-Path $qaRepo 'openspec/changes/reconcile-session-liveness-and-title-refresh'
$qaSource = Get-Content -LiteralPath (Join-Path $qaChange 'evidence/development-trial-operations.ps1') -Raw
$qaTokens = $null
$qaErrors = $null
[void][Management.Automation.Language.Parser]::ParseInput($qaSource, [ref]$qaTokens, [ref]$qaErrors)
if ($qaErrors.Count) { throw 'Operational script syntax failed.' }
$qaStart = $qaSource.IndexOf('$helperLayout =')
$qaEnd = $qaSource.IndexOf('$bin =', $qaStart)
if ($qaStart -lt 0 -or $qaEnd -le $qaStart) { throw 'Bounded resolver fragment not found.' }
# Mock only the automatic location input; execute the unchanged bounded resolver logic.
$qaFragment = $qaSource.Substring($qaStart, $qaEnd - $qaStart).Replace('$PSScriptRoot', '$qaLocation')
$qaFixture = Join-Path $qaRepo ('artifacts/qa-005-resolver/' + [guid]::NewGuid().ToString('N'))
$qaLayouts = @(
    @{ name = 'active'; root = (Join-Path $qaFixture 'active-root'); suffix = 'openspec/changes/reconcile-session-liveness-and-title-refresh/evidence' },
    @{ name = 'dated-archive'; root = (Join-Path $qaFixture 'archive-root'); suffix = 'openspec/changes/archive/2026-10-23-reconcile-session-liveness-and-title-refresh/evidence' },
    @{ name = 'mirrored-private'; root = (Join-Path $qaFixture 'parent-root/private/recovery-tool'); suffix = 'openspec/changes/reconcile-session-liveness-and-title-refresh/evidence' },
    @{ name = 'mirrored-private-archive'; root = (Join-Path $qaFixture 'parent-root/private/recovery-archive-tool'); suffix = 'openspec/changes/archive/2026-10-23-reconcile-session-liveness-and-title-refresh/evidence' }
)
[void][IO.Directory]::CreateDirectory((Join-Path $qaFixture 'parent-root/src/BellTower/bin/Release/net10.0-windows'))
[IO.File]::WriteAllText((Join-Path $qaFixture 'parent-root/src/BellTower/bin/Release/net10.0-windows/api-location.txt'), 'wrong-parent')
$qaResults = @()
foreach ($qaCase in $qaLayouts) {
    $qaLocation = Join-Path $qaCase.root $qaCase.suffix
    [void][IO.Directory]::CreateDirectory($qaLocation)
    $qaExpected = (Resolve-Path $qaCase.root).Path
    $qaResolved = & ([scriptblock]::Create($qaFragment + "`n" + '$repo'))
    if ($qaResolved -ne $qaExpected) { throw ('Incorrect resolver root: ' + $qaCase.name) }
    $qaMarker = Join-Path $qaExpected 'src/BellTower/bin/Release/net10.0-windows/api-location.txt'
    [void][IO.Directory]::CreateDirectory((Split-Path $qaMarker))
    [IO.File]::WriteAllText($qaMarker, $qaCase.name)
    if ([IO.File]::ReadAllText((Join-Path $qaResolved 'src/BellTower/bin/Release/net10.0-windows/api-location.txt')) -ne $qaCase.name) { throw 'Wrong nearby API root.' }
    $qaResults += @{ layout = $qaCase.name; ownRoot = $true; nearbyMarker = $true }
}
$qaLocation = Join-Path $qaFixture 'unsupported/evidence'
[void][IO.Directory]::CreateDirectory($qaLocation)
$qaRejected = $false
try { & ([scriptblock]::Create($qaFragment)) } catch { $qaRejected = $_.Exception.Message -match 'Unsupported recovery helper layout' }
if (!$qaRejected) { throw 'Unsupported layout was not rejected.' }

function Requirement-Blocks([string]$Text) {
    $qaBlocks = @{}
    foreach ($qaMatch in [regex]::Matches($Text, '(?ms)^### Requirement: ([^\r\n]+)\r?\n.*?(?=^### Requirement:|^## |\z)')) {
        $qaBlocks[$qaMatch.Groups[1].Value] = ($qaMatch.Value -replace '\s+', ' ').Trim()
    }
    return $qaBlocks
}
$qaSpecResults = @()
foreach ($qaCapability in @('session-identity', 'session-tracking')) {
    $qaMainPath = 'openspec/specs/' + $qaCapability + '/spec.md'
    $qaOld = Requirement-Blocks ((& git -C $qaRepo show ('0a7445a506e5306cca515c251e1fadad38fd99de:' + $qaMainPath)) -join "`n")
    $qaMain = Requirement-Blocks (Get-Content -LiteralPath (Join-Path $qaRepo $qaMainPath) -Raw)
    $qaDelta = Requirement-Blocks (Get-Content -LiteralPath (Join-Path $qaChange ('specs/' + $qaCapability + '/spec.md')) -Raw)
    foreach ($qaName in $qaDelta.Keys) {
        if ($qaMain[$qaName] -cne $qaDelta[$qaName]) { throw ('Delta/main mismatch: ' + $qaName) }
    }
    foreach ($qaName in $qaOld.Keys) {
        if (!$qaDelta.ContainsKey($qaName) -and $qaMain[$qaName] -cne $qaOld[$qaName]) { throw ('Unrelated requirement changed: ' + $qaName) }
    }
    $qaSpecResults += @{ capability = $qaCapability; deltaBlocks = $qaDelta.Count; originalBlocks = $qaOld.Count; currentBlocks = $qaMain.Count; unrelatedPreserved = $true; deltaExact = $true }
}
[ordered]@{ syntaxPassed = $true; boundedResolverOnly = $true; dllLoaded = $false; operationalFunctionsExecuted = $false; layouts = $qaResults; unsupportedRejected = $qaRejected; specs = $qaSpecResults } | ConvertTo-Json -Depth 8
