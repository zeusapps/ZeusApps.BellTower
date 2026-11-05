# implementation-033: refresh lock files at the pinned SDK (ANALYSIS-1900)

Baseline 3e5eccf. SDK 10.0.114 (global.json, rollForward disable).

## Before
`dotnet restore ZeusApps.BellTower.slnx --locked-mode` failed with NU1004 for BellTower, BellTower.Launcher, BellTower.Protocol
(Microsoft.NET.ILLink.Tasks [10.0.13) -> [10.0.14)) and BellTower.Ingress (Microsoft.DotNet.ILCompiler 10.0.13 -> 10.0.14).

## Change
`dotnet restore ZeusApps.BellTower.slnx --force-evaluate`. Seven lock files changed (27 +/27 -):
src/BellTower, src/BellTower.Ingress, src/BellTower.Launcher, src/BellTower.Protocol, tests/BellTower.Tests,
tools/BellTower.Ingress.Bench, tools/BellTower.Smoke. Distribution unchanged.
- ILLink.Tasks requested/resolved 10.0.13 -> 10.0.14 (7 files) with new contentHash.
- Ingress only: runtime.win-x64.Microsoft.DotNet.ILCompiler and ILCompiler 10.0.13 -> 10.0.14, new hashes (the ingress is the
  NativeAOT project; ILCompiler ships with the same SDK servicing band).
- DEVIATION from the expected "ILLink only" diff: project-reference versions BellTower.Protocol and BellTower.Distribution
  "[0.1.8, )" -> "[0.1.9, )" (3 lines each). Cause: Directory.Build.props Version is already 0.1.9 (commit a6b5cac) and the old locks were stale on it; locked mode did not flag it. Nothing else changed.

## After / checks
- `dotnet restore ... --locked-mode` -> exit 0. Script-style `--locked-mode -r win-x64 -p:SelfContained=true` for BellTower, Launcher, Ingress -> exit 0 each.
- `dotnet build -c Release --no-restore` -> 0 warnings, 0 errors.
- `dotnet format ... --no-restore --verify-no-changes` -> exit 0.
- Full suite once: 1444 passed, 2 failed (StripRenderingTests.IncreasingTheInheritedFontRefitsTheChipAndItsStrip,
  AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor); both rerun alone: 2/2 passed (load-sensitive, unrelated).
- docs/compatibility.md states SDK 10.0.114 (true); no ILLink/ILCompiler version stated in docs; no doc edit.

## Limits
- NativeAOT ingress publish (win-x64) was attempted twice (bash, PowerShell): the 10.0.14 ILCompiler compiled, but the native link
  step failed in this environment ("vswhere.exe is not recognized" before link.exe, exit 123). Environment toolchain issue, not a lock issue; the AOT publish is not confirmed. scripts/package.ps1 was not run.
- Nothing committed; no csproj/global.json edited.
