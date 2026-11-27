# Stopgap for load-sensitive .NET test failures (see docs/follow-ups.md): run the suite once, and if a
# small number of tests fail, rerun only those tests exactly once. Fails if any still fail, if too
# many fail on the first run (a broad failure is real), or if the run fails without parsable failures.
param(
    [int]$MaxRetry = 10,
    [string]$ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Reads TRX files. Methods maps "Class.Method" to the list of result outcomes; Unresolved counts failed results
# without a test definition; Abnormal lists nonzero counters other than failed/notExecuted (a real failure).
function Read-TrxResult {
    param([string[]]$TrxPath)
    $methods = @{}
    $unresolved = 0
    $abnormal = @()
    foreach ($path in $TrxPath) {
        $xml = [xml](Get-Content -LiteralPath $path -Raw)
        $definitions = @{}
        foreach ($test in $xml.SelectNodes("//*[local-name()='UnitTest']")) {
            $method = $test.SelectSingleNode("*[local-name()='TestMethod']")
            if ($method) { $definitions[$test.id] = "$($method.className).$($method.name)" }
        }
        foreach ($result in $xml.SelectNodes("//*[local-name()='UnitTestResult']")) {
            if ($definitions.ContainsKey($result.testId)) {
                $name = $definitions[$result.testId]
                if (-not $methods.ContainsKey($name)) { $methods[$name] = [System.Collections.Generic.List[string]]::new() }
                $methods[$name].Add([string]$result.outcome)
            } elseif ($result.outcome -eq 'Failed') {
                $unresolved++
            }
        }
        $counters = $xml.SelectSingleNode("//*[local-name()='Counters']")
        if ($counters) {
            foreach ($counter in 'error', 'timeout', 'aborted', 'notRunnable', 'disconnected') {
                $value = $counters.GetAttribute($counter)
                if ($value -and [int]$value -ne 0) { $abnormal += "$counter=$value" }
            }
        }
    }
    return [pscustomobject]@{ Methods = $methods; Unresolved = $unresolved; Abnormal = @($abnormal) }
}

# Failed test methods, sorted and unique (theory cases collapse to the method).
function Get-FailedTestMethod {
    param($Trx)
    return @($Trx.Methods.Keys | Where-Object { $Trx.Methods[$_] -contains 'Failed' } | Sort-Object -CaseSensitive)
}

# A retried method passed only if it has results and all are Passed or NotExecuted with at least one Passed.
function Test-RetryPassed {
    param($Trx, [string]$Name)
    if (-not $Trx.Methods.ContainsKey($Name)) { return $false }
    $outcomes = @($Trx.Methods[$Name])
    return ($outcomes -contains 'Passed') -and -not @($outcomes | Where-Object { $_ -notin 'Passed', 'NotExecuted' })
}

# VSTest filter: escape its special characters inside a value.
function ConvertTo-FilterValue {
    param([string]$Value)
    return [regex]::Replace($Value, '([\\()&|=!~])', '\$1')
}

function New-TestFilter {
    param([string[]]$Name)
    return (@($Name | ForEach-Object { 'FullyQualifiedName=' + (ConvertTo-FilterValue $_) }) -join '|')
}

if ($MyInvocation.InvocationName -eq '.') { return }

$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $ResultsDirectory) { $ResultsDirectory = Join-Path $projectRoot 'TestResults' }
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

function Invoke-TestRun {
    param([string[]]$ExtraArgument)
    $before = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter *.trx -ErrorAction SilentlyContinue | ForEach-Object FullName)
    dotnet test -c Release --no-build --logger trx --results-directory $ResultsDirectory @ExtraArgument | Out-Host
    $code = $LASTEXITCODE
    $new = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter *.trx -ErrorAction SilentlyContinue | ForEach-Object FullName | Where-Object { $_ -notin $before })
    return [pscustomobject]@{ ExitCode = $code; Trx = $new }
}

Push-Location $projectRoot
try {
    $first = Invoke-TestRun
    if ($first.ExitCode -eq 0) { exit 0 }

    $firstTrx = Read-TrxResult @($first.Trx)
    $failed = @(Get-FailedTestMethod $firstTrx)
    if ($firstTrx.Abnormal.Count -gt 0 -or $firstTrx.Unresolved -gt 0) {
        Write-Host "::error::The first run has errors a retry cannot cover ($($firstTrx.Abnormal -join ', '); $($firstTrx.Unresolved) failed result(s) without a test definition); not retrying."
        exit 1
    }
    if ($failed.Count -eq 0) {
        Write-Host "::error::dotnet test failed (exit $($first.ExitCode)) without any parsable failed tests; not retrying."
        exit 1
    }
    if ($failed.Count -gt $MaxRetry) {
        Write-Host "::error::$($failed.Count) test methods failed (more than $MaxRetry); treating as a real failure, not retrying."
        $failed | ForEach-Object { Write-Host "RETRY-SKIPPED $_" }
        exit 1
    }

    Write-Host "RETRY: rerunning $($failed.Count) failed test method(s) once."
    $second = Invoke-TestRun @('--filter', (New-TestFilter $failed))
    $secondTrx = Read-TrxResult @($second.Trx)
    $exitBad = ($second.ExitCode -ne 0) -or $secondTrx.Abnormal.Count -gt 0
    $allPassed = -not $exitBad

    foreach ($name in $failed) {
        if (Test-RetryPassed $secondTrx $name) {
            Write-Host "RETRY-RESULT PASSED $name"
            Write-Host "::warning::Flaky test passed on retry: $name"
        } elseif ($secondTrx.Methods.ContainsKey($name) -and $secondTrx.Methods[$name] -contains 'Failed') {
            $allPassed = $false
            Write-Host "RETRY-RESULT FAILED $name"
            Write-Host "::error::$name failed again on retry."
        } else {
            $allPassed = $false
            Write-Host "RETRY-RESULT UNKNOWN $name"
            Write-Host "::error::$name has no passing result on retry (not run?)."
        }
    }
    if ($second.ExitCode -ne 0 -or $secondTrx.Abnormal.Count -gt 0) {
        Write-Host "::error::Retry run failed (exit $($second.ExitCode); $($secondTrx.Abnormal -join ', '))."
    }
    exit ($allPassed ? 0 : 1)
} finally {
    Pop-Location
}
