#Requires -Version 7.0
<#
.SYNOPSIS
Read-only review target identity for a dirty working tree.

.DESCRIPTION
Emits (to the success pipeline, so it can be captured) HEAD, a sorted manifest including additions
and deletions, the SHA-256 of the exact bytes of
`git -c diff.suppressBlankEmpty=false -c diff.indentHeuristic=true diff --binary --no-renames
--no-color --no-ext-diff --no-textconv --src-prefix=a/ --dst-prefix=b/ --diff-algorithm=myers -U3
--inter-hunk-context=0 <baseline> -- <paths>`,
the SHA-256 of each untracked file in scope, and a one-line summary. It stores nothing.

Paths are resolved relative to the caller's current directory (absolute paths are accepted) and
must stay inside the repository. Their letter case is taken from the file system when they exist, so
a wrong-case spelling cannot pass as a clean target on a case-insensitive checkout. They are matched literally, not as pathspec globs. A path that
matches nothing in the working tree, the index, or the baseline commit fails with a non-zero exit
code; a path deleted since the baseline is valid. Default scope is the repository root.

Output format changed when the diff arguments were pinned and the label was shortened: identities recorded by earlier versions
of this script are not comparable.

.PARAMETER Baseline
Commit-ish to diff against. Default HEAD.

.PARAMETER Paths
One or more paths, space-separated or comma-joined. Every form below is equivalent:
  ./scripts/review-identity.ps1 -Baseline <commit> -Paths src docs/a.md
  ./scripts/review-identity.ps1 -Baseline <commit> -Paths src,docs/a.md
  pwsh -File scripts/review-identity.ps1 -Baseline <commit> -Paths src docs/a.md
Paths containing a literal comma are not supported.

.PARAMETER MorePaths
Collects the extra space-separated values after -Paths; do not pass it by name.
#>
param(
    [string]$Baseline = 'HEAD',
    [string[]]$Paths = @(),
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$MorePaths = @()
)

$ErrorActionPreference = 'Stop'
$callerDirectory = (Get-Location).ProviderPath
$gitOptions = @('-c', 'core.quotepath=false', '-c', 'diff.suppressBlankEmpty=false', '-c', 'diff.indentHeuristic=true', '--literal-pathspecs')

function Get-CanonicalRelativePath {
    # Takes each existing segment's spelling from the file system; stops at the first missing segment
    # (for example a path deleted since the baseline) and keeps the rest as given.
    param([string]$RepositoryRoot, [string]$Relative)
    if ($Relative -eq '.') { return $Relative }
    $current = $RepositoryRoot
    $segments = $Relative -split '/'
    for ($index = 0; $index -lt $segments.Length; $index++) {
        $entries = @(Get-ChildItem -LiteralPath $current -Force -ErrorAction SilentlyContinue)
        $entry = $entries | Where-Object { $_.Name -ceq $segments[$index] } | Select-Object -First 1
        if (-not $entry) {
            $entry = $entries | Where-Object { $_.Name -ieq $segments[$index] } | Select-Object -First 1
        }
        if (-not $entry) { break }
        $segments[$index] = $entry.Name
        $current = $entry.FullName
    }
    return $segments -join '/'
}

function Invoke-GitText {
    param([string[]]$Arguments)
    $errorFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = & git @gitOptions @Arguments 2>$errorFile
        if ($LASTEXITCODE -ne 0) {
            throw "git $($Arguments -join ' ') failed (exit $LASTEXITCODE): $([System.IO.File]::ReadAllText($errorFile).Trim())"
        }
        return @($output | ForEach-Object { "$_".TrimEnd("`r") } | Where-Object { $_ -ne '' })
    } finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

function Get-GitStreamHash {
    param([string[]]$Arguments)
    $start = [System.Diagnostics.ProcessStartInfo]::new('git')
    foreach ($argument in $gitOptions + $Arguments) {
        $start.ArgumentList.Add($argument)
    }
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $root
    $process = [System.Diagnostics.Process]::Start($start)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $errorText = $process.StandardError.ReadToEndAsync()
        $hash = $sha.ComputeHash($process.StandardOutput.BaseStream)
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "git $($Arguments -join ' ') failed (exit $($process.ExitCode)): $($errorText.GetAwaiter().GetResult().Trim())"
        }
        return [System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
        $process.Dispose()
    }
}

$requested = @(@($Paths) + @($MorePaths) | ForEach-Object { $_ -split ',' } | Where-Object { $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })
if ($requested.Count -eq 0) { $requested = @('.') }

$root = @(Invoke-GitText @('rev-parse', '--show-toplevel'))[0]
$scopePaths = [System.Collections.Generic.List[string]]::new()
foreach ($requestedPath in $requested) {
    $full = [System.IO.Path]::GetFullPath($requestedPath, $callerDirectory)
    $relative = [System.IO.Path]::GetRelativePath($root, $full).Replace('\', '/')
    if ($relative -eq '..' -or $relative.StartsWith('../', [StringComparison]::Ordinal) -or [System.IO.Path]::IsPathRooted($relative)) {
        throw "Path '$requestedPath' is outside the repository at $root."
    }
    $scopePaths.Add((Get-CanonicalRelativePath $root $relative))
}

Push-Location $root
try {
    $head = @(Invoke-GitText @('rev-parse', 'HEAD'))[0]
    $base = @(Invoke-GitText @('rev-parse', '--verify', "$Baseline^{commit}"))[0]
    $sortedScope = @($scopePaths | Sort-Object -Unique -CaseSensitive)
    $scopePaths = [System.Collections.Generic.List[string]]::new([string[]]$sortedScope)
    $scope = @('--') + $sortedScope

    for ($i = 0; $i -lt $scopePaths.Count; $i++) {
        $path = $scopePaths[$i]
        $found = (Test-Path -LiteralPath $path) -or
            (@(Invoke-GitText @('ls-files', '--', $path)).Count -gt 0) -or
            (@(Invoke-GitText @('ls-tree', '-r', '--name-only', $base, '--', $path)).Count -gt 0)
        if (-not $found) {
            throw "Path '$path' matches nothing in the working tree, the index, or baseline $($base.Substring(0, 7))."
        }
    }

    $manifest = [System.Collections.Generic.List[string]]::new()
    foreach ($line in @(Invoke-GitText (@('diff', '--no-renames', '--no-ext-diff', '--name-status', $base) + $scope))) {
        $parts = $line -split "`t", 2
        $manifest.Add("$($parts[0]) $($parts[1])")
    }
    $untracked = [System.Collections.Generic.List[string]]::new()
    foreach ($path in @(Invoke-GitText (@('ls-files', '--others', '--exclude-standard') + $scope))) {
        $untracked.Add($path)
        $manifest.Add("? $path")
    }
    $sortedManifest = $manifest.ToArray()
    [Array]::Sort($sortedManifest, [System.StringComparer]::Ordinal)
    $untrackedPaths = $untracked.ToArray()
    [Array]::Sort($untrackedPaths, [System.StringComparer]::Ordinal)

    $diffHash = Get-GitStreamHash (@('diff', '--binary', '--no-renames', '--no-color', '--no-ext-diff', '--no-textconv', '--src-prefix=a/', '--dst-prefix=b/', '--diff-algorithm=myers', '-U3', '--inter-hunk-context=0', $base) + $scope)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("HEAD: $head")
    $lines.Add("Baseline: $base")
    $lines.Add("Paths: $($scopePaths -join ' ')")
    $lines.Add('Manifest (A added, M modified, D deleted, ? untracked):')
    foreach ($entry in $sortedManifest) {
        $lines.Add("  $entry")
    }
    $lines.Add("Tracked diff SHA-256: $diffHash")
    $lines.Add('Untracked file SHA-256:')
    $untrackedHashes = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $untrackedPaths) {
        $fileHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $untrackedHashes.Add("$fileHash  $path")
        $lines.Add("  $fileHash  $path")
    }
    $combined = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes(($untrackedHashes -join "`n")))
    $untrackedDigest = [System.BitConverter]::ToString($combined).Replace('-', '').ToLowerInvariant()
    $lines.Add("Summary: head=$($head.Substring(0, 7)) baseline=$($base.Substring(0, 7)) files=$($sortedManifest.Length) diff=$($diffHash.Substring(0, 12)) untracked=$($untrackedPaths.Length)/$($untrackedDigest.Substring(0, 12))")
    $lines.ToArray()
} finally {
    Pop-Location
}
