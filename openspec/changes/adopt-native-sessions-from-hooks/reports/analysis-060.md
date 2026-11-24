> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-060); client: Claude Code 2.1.331, code-analyzer subagent; mode: read-only analysis; model: claude-opus-5-5.
> Refusal (as reported by the reviewer): the client refused the write of this report; the reviewer did not retry.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-060 (recheck of ANALYSIS-5900 and ANALYSIS-5800(a) against DEV-069)

## Review target

- Reviewer / role: ANALYSIS-060 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331, code-analyzer subagent (read-only analysis; the only write would be this report, which the client refused), claude-opus-5-5
- Date: 2026-11-24T21:39Z to 2026-11-24T21:43Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-060 (recheck), plus one bounded side question (the orphaned test tray)
- Task and requirement references: tasks 5.2 and 5.3 ([tasks](../tasks.md)); [implementation 069](../evidence/implementation-069.md)
- Scope and relevant neighboring paths:
  - `src/BellTower/Diagnostics/{HookDeliveryReporter,HookIngressImageReporter,HookStartReporter,DiagnosticsReporter,ConfirmationCoverageReporter}.cs`
  - `tests/BellTower.Tests/Diagnostics/HookIngressImageReporterTests.cs`
  - `docs/installation.md`, `docs/compatibility.md`
  - `src/BellTower/Setup/HookCommandBuilder.cs` (shell forms)
  - Side question:
    - `src/BellTower/Tray/{TrayController,TrayHost}.cs`
    - `src/BellTower/Sessions/AgentLauncher.cs`
    - `src/BellTower/Integration/BellTower.psm1`
    - `tests/BellTower.Tests/Cli/CliTests.cs`
    - `tests/BellTower.Tests/Setup/{PowerShellModuleTests,PrelaunchPresentationTests}.cs`
    - `tests/BellTower.Tests/Testing/TestWorkspace.cs`
- Baseline commit: 9550ced
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 9550ced -Paths src tests docs vscode scripts`, HEAD 9550ced12d19d6a2d4e0f857e64879ff1d8007c6.
  - The manifest has 22 entries: 20 modified and 2 untracked (`src/BellTower/Diagnostics/HookIngressImageReporter.cs`, `tests/BellTower.Tests/Diagnostics/HookIngressImageReporterTests.cs`).
  - Tracked diff SHA-256 749f21e5fb076b783706c6e8adee3ba4420babd6ccbdd7d232843e6ef98ec98a.
  - Untracked hashes: `HookIngressImageReporter.cs` e3a110092f3dd3ebb3ff9bcbf8115a3f84f166ece78eddb80311b18f4f5ba5ec, `HookIngressImageReporterTests.cs` 3e55b5c971155b14b33a8f77c3e3648b571c6a22957ce4a38052a3be9886a437.
  - Summary `head=9550ced baseline=9550ced files=22 diff=749f21e5fb07 untracked=2/1c86223ba079` (as assigned).
- Before/after identity: the after run gives the same summary `head=9550ced baseline=9550ced files=22 diff=749f21e5fb07 untracked=2/1c86223ba079`. No unexpected mutation.
- Prior reports / rechecked findings: [analysis 059](analysis-059.md) (ANALYSIS-5900) and [analysis 058](analysis-058.md) (ANALYSIS-5800(a)). ANALYSIS-5800(b), 5802 and 5803 are deferred and were not reopened.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Source of the compared commands | Read `HookDeliveryReporter.cs` 20-24, 105-128 and 130-177, and `HookIngressImageReporter.cs` 23-28 | `LiveOwnedCommands(provider)` returns `ConfiguredHooks(path, OwnedCommands(path)).Commands`. Those are the `command` strings in the provider settings file that are also in the manifest set for that path, so the manifest only filters ownership. `HookIngressImageReporter.Provider` evaluates this list. No code reads the manifest list directly any more | The 5900 core fix is present |
| Shared bounded parse and `hookDelivery` regression | `git diff 9550ced -- src/BellTower/Diagnostics/HookDeliveryReporter.cs` | `ConfiguredHooks` keeps the 1 MB cap, `MaxDepth = 32` and the same `catch` set; it now returns the list instead of counts. `Provider()` sets `ConfiguredHookCount = liveCommands.Count` and computes the broken count with the same `IsKnownBrokenCodexCommand` (Codex only). Output fields, `Status` strings and `SettingsReadable` are unchanged. `HookDeliveryReporterTests.cs` is not in the manifest (unchanged) and still pins the output | No regression found in source. Execution belongs to QA-065 |
| Agreement with `hookDelivery.configured` | Compared constructor arguments in `DiagnosticsReporter.cs` 21-22 and `HookStartReporter.cs` 19 | Both resolve settings paths through the same `HookDeliveryReporter` code with the same overrides (null in doctor, so environment or profile). `configured == false` gives an empty list, so `unknown`. `configured == true` with only commands that cannot be compared also gives `unknown` | Agrees by construction |
| `unknown` cases | `HookIngressImageReporter.Evaluate` 31-40 and `ConfiguredHooks` | All of these give `unknown`, never `stale`: missing settings; oversized, malformed or unreadable (IO or access) settings; no owned command present; legacy `-EncodedCommand` or hashless commands; a missing or invalid selected hash | Verified in source. A locked, unreadable file has no fixture, but it takes the same `catch` branch as malformed, which is tested |
| "Last recorded command" not used | `grep` of the reporter | No `Last()` and no reliance on manifest order. The verdict is a set comparison over the live commands; stale wins if any live command names another image | Matches the 5900 caution |
| Both providers and every shell form | `HookCommandBuilder.cs` 17-46 compared with `HookIngressImageReporterTests.cs` 85-123 and 174-203 | Claude has one form (bash literal). Codex PowerShell, Cmd and Bash are each a theory case for the equal, different and manifest-history scenarios. The history case puts `[Older, Selected]` in the manifest with only Selected live (current, available) or only Older live (stale). The non-Windows bash literal has no fixture because the suite is Windows-only; the regex covers its native separator (analysis 059) | Covered |
| `unknown` fixtures | `HookIngressImageReporterTests.cs` 138-172 and 205-228 | Fixtures cover: no manifest; legacy encoded and hashless commands; no or invalid selected hash; missing settings; a foreign hook only; malformed JSON in both files; oversized files (1 MB + 16) for both providers. Each asserts `availability == "available"` | Covered |
| Docs honesty | `git diff 9550ced -- docs/installation.md`; `grep ingressImage docs/*.md` | `installation.md` 83 now says "the owned hook commands present in the provider settings (setup's manifest only identifies which commands are ours, so a superseded command no longer in the settings is ignored)". Its list of `unknown` cases matches the code. It includes "`hookStart.ingressImage` is authoritative for the image hash; `confirmationCoverage.*.ingressImage` only reports the producer shape of the latest capture", which matches `ConfirmationCoverageReporter.cs` 91 (`ReadProducer`). Line 13 adds the doctor summary. No other doc describes the comparison, and `compatibility.md` has no stale text | Honest |
| Doctor cost (regression only) | Reading the code | Each doctor run still parses each settings file twice (once for `hookDelivery`, once for `hookStart`) and reads `integration.json` several times. The code is shared, so the two cannot diverge, and every read is small and bounded | Acceptable, as analysis 059 recorded. Not raised (recheck rule) |
| Side question: orphaned test tray | Read-only process listing (`Get-CimInstance Win32_Process`, paths redacted to repository-relative); read of the orphan's data directory; review of every test path that can spawn `tray` | See ANALYSIS-6000 | Finding. No process was stopped |
| Build, tests, format verify | Not run | Owned by QA-065 | Not performed (assignment) |

Client coverage: the recheck covers Claude Code and Codex, which share one evaluation rule and one parse; only the settings path differs. The side finding covers the test fixtures for both providers' launch paths.

## Findings

### ANALYSIS-6000: Tests that start a detached tray can leave it running after the workspace is deleted

- Severity: warning (test hygiene, no user-data risk). A leaked tray runs indefinitely. It locks `src/BellTower/bin/Release`, which blocked the in-tree build in DEV-069, and it can skew later process-sensitive tests.
- Related task / requirement: none in this change. It affects the test suite that QA and the developer rely on.
- Locations (reviewed identity above):
  - `src/BellTower/Tray/TrayController.cs` 19-51: `Start(silent)` launches `StableExecutable(ProcessPath)` with exactly `tray --data-dir <root> --silent` (`UseShellExecute = true`, detached) and returns at once.
  - `src/BellTower/Tray/TrayHost.cs` 13-24: the tray takes the `tray-instance` mutex, writes the presence file, and only then deletes `exit.request`.
  - Only these tests reach `Start(silent: true)` from a child process of `src/BellTower/bin/Release/net10.0-windows/bell-tower.exe`. No other path produces this argument order: `ManagementOwnershipProcessTests` uses `tray --silent --data-dir`, and `TrayProcess`, the hook start and `InstallDevScriptTests` pass no `--silent`.
    - `tests/BellTower.Tests/Setup/PrelaunchPresentationTests.cs` 15-95, `ColorPrecedesTheNativeAgentAndItsFirstHookUsesTheSamePalette` (2 cases):
      - The module runs `start --data-dir X --silent` (`BellTower.psm1` 89-91).
      - The `finally` checks `tray.IsRunning()` once, without waiting for the start, then waits 4 s for the exit.
    - `tests/BellTower.Tests/Setup/PowerShellModuleTests.cs` 14-157, `SessionFunctionsLaunchNativeChildrenAndRestoreShellEnvironment` (8 cases): the same pattern with a 3 s exit wait.
    - `tests/BellTower.Tests/Cli/CliTests.cs` 57-103, `LaunchForwardsAgentArgumentsAndKeepsOwnMetadataSeparate` (2 cases), via `AgentLauncher.cs` 22:
      - It waits up to 5 s for the start before requesting the exit.
      - The exit wait shares that same 5 s deadline, so it can be zero.
    - `tests/BellTower.Tests/Cli/CliTests.cs` 250-289, `DetachedTraySurvivesStartingProcessAndManagementIsScoped`:
      - It waits 5 s for the start and asserts it.
      - The `finally` runs `exit` and ignores its failure.
      - Its exit wait uses the already expired deadline.
  - `tests/BellTower.Tests/Testing/TestWorkspace.cs` 26-42: `Dispose` deletes the workspace root whether or not a tray still serves it.
- Observation:
  - The orphan is a `bell-tower.exe` from `src/BellTower/bin/Release/net10.0-windows`, created 2026-11-24T21:08:09Z. Its command line is `tray --data-dir artifacts/unit-tests/524d990968474a71b63fd476d1dae300/data --silent`. Its parent process no longer exists.
  - The workspace now holds only `data/bell-tower.log` and `data/refresh-health.json`, which the tray recreated after the directory was deleted.
    - The log records `tray-refresh-failed: DirectoryNotFoundException` at 21:08:23Z and then every 10 minutes.
    - The health file shows `lastSuccess` 21:08:23.37Z and `failingSince` 21:08:23.62Z.
    - So the test deleted the workspace about 14 s after the tray started. The tray survived and can no longer get an exit request from the test.
  - The remaining evidence cannot pin down the exact test: the fixture files are gone and so is the parent process. All the candidates above produce this command line.
  - The two module-based tests are the most likely. Their `finally` checks `IsRunning()` once and does not wait for a detached start that is still in progress. Under full-suite parallel load the tray may not hold its mutex yet, because `CanStartTray`, `TrayJobGuard.Refuses` and process start come first. In that case no exit is requested.
  - Each of three separate races is enough to leak the tray:
    1. The tray takes its mutex after the single `IsRunning()` check, so no exit is requested.
    2. The test writes `exit.request` between the tray taking the mutex and its startup `File.Delete(exit.request)` (`TrayHost.cs` 13-24), which erases the request.
    3. The tray does not reach its next tick before the bounded wait ends. In both `CliTests` cases the exit wait reuses the expired start deadline. `Dispose` then deletes the directory the tray watches.
  - The tray has no rule to exit when its data directory disappears, so every leak is permanent.
- Proposed improvement and benefit:
  - Add one shared helper, for example `TestWorkspace.StopTray()`. Call it from `Dispose` or from the four `finally` blocks, replacing the copied loops. The helper:
    - (a) Finds processes whose command line contains the exact `--data-dir <Paths.Root>` argument for this workspace (via `ProcessProbe` or WMI). This never touches the user's tray or other workspaces.
    - (b) Requests the exit while a matching process exists, rewrites `exit.request` if it disappears, and uses its own deadline (for example 10 s).
    - (c) As a last resort, kills only the matching processes. `Dispose` then never deletes a directory that a live tray still serves.
  - Additionally or instead, stop tests that do not need a tray from starting one. Only `DetachedTraySurvives...` and possibly the launch test need a tray. Two ways to do it:
    - A test-only `--no-tray` path in the module and in launch. This is a product change and needs a decision.
    - Pre-hold the workspace's `tray-instance` mutex in the test, so `TrayController.Start` returns without starting.
  - Optional product hardening, as a separate decision: the tray exits when its data root no longer exists. That would make this leak end by itself.
  - Benefit:
    - A full-suite run no longer leaves background trays.
    - The Release `bin` stays buildable in place.
    - Later tests that count `bell-tower.exe` processes or rely on mutexes are not disturbed.
- Behavior-preservation risks:
  - The helper must match only this workspace's data directory. The safety rules forbid stopping the user's tray, so killing by process name is not acceptable.
  - `DetachedTraySurvives...` must still prove that the tray outlives the starting process before cleanup runs.
  - Pre-holding the mutex must not hide a test's assertion that a tray started.
- Evidence type: measured result for the leak itself (read-only observation of the live orphan and its recreated data directory). Source observation for the races. Which test leaked is a ranked inference and was not reproduced.
- Verification / measurement plan:
  - Run the four candidate test classes repeatedly, for example 20 iterations of `dotnet test --filter` with parallel classes. After each run, list `bell-tower.exe` processes whose command line contains `artifacts/unit-tests/`. After the fix there should be none.
  - Add a focused fixture for the helper: start a detached tray from the built exe, call `StopTray` immediately (before the mutex exists), and assert the process is gone.
  - Optionally, QA adds a suite-end check that fails on any remaining process whose command line names `artifacts/unit-tests`.
- Relationship: new. It explains the locked `bin` noted in [implementation 069](../evidence/implementation-069.md).

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-5900 | `head=9550ced baseline=9550ced files=22 diff=749f21e5fb07 untracked=2/1c86223ba079` | The verdict uses `LiveOwnedCommands`, the owned commands present in the provider settings, with the manifest only as the ownership filter. Neither the manifest history nor the last recorded command is used. Fixtures put `[Older, Selected]` in the manifest: Selected live is current and available, Older live is stale, for both providers and each Codex shell form. Missing settings, a foreign hook only, and malformed or oversized settings are `unknown`. The parse is the one `hookDelivery` uses, so the verdict agrees with `hookDelivery.configured`. `hookDelivery` output is unchanged. The docs sentence is corrected and the authoritative-field sentence is present. Tests were not run here (QA-065) | verified (source and fixtures; execution pending QA-065) |
| ANALYSIS-5800(a) | same | ANALYSIS-5900 was the only failing part in analysis 059, and it is resolved. Still true (`HookStartReporter.cs` 19-28): read-only reporting for both providers; `unknown`, never `stale`, for unparseable or unselected cases; `hookStart.available` unchanged; the advice carried in `availability`. The selected-hash lookup still repeats about 7 lines of the `HookDeliveryReporter.CodexShellStatus` installation lookup. DEV-069 left this as optional and analysis 059 rated it minor, so it does not block | verified (execution pending QA-065) |

## Assessment and limitations

- Scope covered:
  - The DEV-069 correction for ANALYSIS-5900 and ANALYSIS-5800(a), for Claude Code and Codex.
  - A regression check of `hookDelivery` output, the doctor wiring and the docs.
  - The side question, limited to finding the tray-leaking tests and proposing a fix.
- Results:
  - ANALYSIS-5900 and ANALYSIS-5800(a) are verified by reading the source and fixtures. Running the fixtures belongs to QA-065.
  - ANALYSIS-6000 (warning) records the test-suite tray leak: four candidate tests (13 cases) and a scoped cleanup proposal.
  - The orchestrator decides whether the ANALYSIS-6000 fix belongs in this change or a follow-up, and whether to pursue the optional product hardening or a test-only no-tray path.
- Not performed:
  - Build, tests and format verify (QA-065).
  - Any live, installed or setup check (prohibited).
  - Reproduction of the leak. Stopping the orphan is prohibited, so the tray for `artifacts/unit-tests/524d990968474a71b63fd476d1dae300` is still running; the orchestrator or user decides what to do with it.
- Unexpected writes: none. The client refused the write of this report, so it is returned inline. Identity was unchanged before and after.
