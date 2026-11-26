# Implementation 004 (DEV-004): task 2.1 fix cycle 1

Developer handoff, 2026-11-25, baseline `02d4f56`. Fixes for ANALYSIS-100 (harness side only), ANALYSIS-101 (with duplicate QA-100), ANALYSIS-102 and ANALYSIS-104 (with the provider model of ANALYSIS-107 step 1). No product (`src/`) file, planning artifact, report, handoff, spec or version declaration was changed. Nothing was committed. Each fix is awaiting independent verification; none is marked closed here. Deferred and untouched: ANALYSIS-103, -105, -106, -108, QA-101.

## Fixes

### ANALYSIS-100 (harness side)

- The `hook-started-tray` check reads the route in the `last-attempt` record (`HookTrayStart.LastAttempt`) and, in the current state, requires `started-via-shell`. A task route fails the check; its detail always names the route. The harness copy of the name (`HookStartRoute.Shell`) is pinned to `TrayProcess.RouteShell` by a unit test, because `TrayProcess` is internal.
- Immediately after the check the tray is exited and restarted with `start --silent` under the sandbox environment, so no later step runs a tray with the real profile environment. The stale expectation (tray stays stopped, no attempt recorded) is unchanged.
- The exposure is stated in the `isolation-environment` detail, the report notes and `docs/distribution-validation.md` as the second effect outside the sandbox, next to the Task Scheduler task.
- A run-wide guard, `RealEnvironmentExposure` on `RunContext`, refuses to combine a hook-started tray with a shared-backend Codex fixture in either order (`BeginHookStartedTray`, `UseSharedBackendCodex` throw `IsolationException`). The probe calls it; no scenario uses a shared-backend fixture yet. A unit test covers both orders.
- Not done by design: the product option (passing `CODEX_HOME` and `CLAUDE_CONFIG_DIR` through the shell route) is a pending user decision. I did not run the bounded probe that would read the recorded settings path from a hook-started tray's `provider-titles.json`.

### ANALYSIS-101 / QA-100

- `Isolation/LeftoverSweep` runs before the "before" snapshot. It removes `\ZeusApps.BellTower\HookStart-*` tasks whose exported XML runs `tray --data-dir` for a directory under `artifacts\field-acceptance\`, and processes whose image is under `artifacts\field-acceptance\runs\`. Each removal is printed (`PREFLIGHT removed ...`) and listed in the isolation section (`PreflightRemovals`; "none found" otherwise).
- Cleanup is per instance in its own try/catch (`InstanceCleanup`, and each step inside `Instance.Dispose` is isolated and the call is idempotent). The whole production comparison is wrapped, and `result.json` is always written.
- `Console.CancelKeyPress` and `AppDomain.ProcessExit` run the same cleanup once, add a failing `run/interrupted` check and write `result.json` and `summary.md` (never the committed evidence file).
- Unit tests of the selector on invented task XML: a harness path is selected; production, `field-acceptance-elsewhere`, a parent directory, a different command and malformed XML are not. A fake `schtasks` test shows only the selected task is deleted and queried.
- Not done by design: no product folder knob (pending user decision). The sweep assumes no other harness run is active; documented.

### ANALYSIS-102

`ProductionSnapshot.Diff` also gates on production-image processes present after but not before (pid plus start time; a reused pid counts as new). Unit test added, including the reused pid case.

### ANALYSIS-104 and ANALYSIS-107 step 1

- `Harness/ProviderModel` (`Claude`, `Codex`, `All`, `For`) owns the settings path, owned-command lookup, runtime shell, modeled fallback shell, agent arguments, payload builder and the acceptance predicate. The scenario no longer branches on the provider name for these. `Instance.OwnedHookCommand` and the scenario-local helpers were removed.
- `Harness/CodexShell` resolves the Codex 0.164.0 order: a PowerShell on `PATH` (only the two system PowerShells the guard allows), then `pwsh` 7 at its fixed path, then Windows PowerShell, then cmd. `SystemTools` gained `Pwsh` (in `All`). The fake `codex exec` probe uses the same resolution. Every Codex capture check names its shell executable (this machine: `pwsh.exe`).
- Payloads are the `payload` objects of `tests/BellTower.Tests/Fixtures/native/*.json` with `session_id`, `cwd` and `transcript_path` set into the sandbox; the invented `turn_id` is gone. The fixtures are linked into the harness output (`FieldAcceptanceFixtures/native`, one source copy). A unit test compares the built payload field names with each fixture.
- Stale state: the primary Codex capture check runs under the default shell and expects failure with the parser error (`captures-default-shell`, flagged `expectedFailure`). Reports separate expected failures from harness failures (`RunReport.ExpectedFailures`, `HarnessFailures`, a "expected failure (observed)" result cell). The cmd captures remain, labeled "modeled cmd fallback" (`captures-modeled-cmd-fallback`), and every other stale-state step that needs a successful Codex capture (seeding, native adoption, the hook-started-tray probe) uses and labels that fallback. The mixed row reports both counts.
- Not done: running the Codex captures under both PowerShell editions (optional in the finding); the Claude `Git\bin\bash.exe` to `usr\bin\bash.exe` ancestry was not verified.

## Check IDs

Stable (same meaning): all `baseline-*`, `upgrade-activated`, `setup-not-rerun`, `candidate-*`, `setup-host-bridge`, `*/doctor-hook-image`, `*/doctor-availability`, `*/native-adoption`, `*/codex-command-form`, `*/inactive-features`, `*/baseline-data-preserved`, `isolation/production-unchanged`, the guard self-test IDs, and the Claude `*/captures-accepted` rows and the current-state Codex `current/captures-accepted`.

Changed meaning, same ID: `stale/captures-accepted [mixed]` (now claude 7 of 7, codex default shell 0 of 7 plus modeled cmd fallback 7 of 7, listed sessions), `*/hook-started-tray` (current state also requires the shell route and replaces the tray), `isolation-environment` and `isolation` details.

New or renamed: `stale/captures-default-shell [codex]` (new, expected failure; replaces the Codex row of `stale/captures-accepted`), `stale/captures-modeled-cmd-fallback [codex]` (new). The total is 57 checks (56 before).

## Results

Full run (`scripts/field-acceptance.ps1`): 57 of 57 passed, 0 harness failures, 1 expected-failure check observed. Details are in [harness-2.1.md](harness-2.1.md).

| Check | Claude Code | Codex | Mixed |
| --- | --- | --- | --- |
| baseline seed | pass (Git Bash) | pass (modeled cmd fallback, labeled) | baseline data pass |
| stale doctor image + advice | pass | pass | availability "needs a setup refresh": pass |
| stale captures | 7 of 7 (Git Bash) | default shell `pwsh.exe`: 0 of 7, 7 parser errors (expected failure observed); modeled cmd fallback 7 of 7 | claude 7/7, codex 0/7 + fallback 7/7, 6 of 6 sessions listed |
| stale native adoption inactive | pass | pass | both listed |
| stale codex command form | | parser error under `pwsh.exe`: pass | |
| stale hook-started tray inactive | pass | pass | |
| stale baseline data preserved | pass | pass | pass |
| current doctor `current` | pass | pass | available: pass |
| current captures | 7 of 7 | 7 of 7 (`pwsh.exe`) | 14 of 14, 6 of 6 listed |
| current native adoption active | `interactive-client` | `embedded-runtime`, verified-live | both listed |
| current hook-started tray active, route `started-via-shell` | pass | pass | |
| current PowerShell-safe Codex command | | pass | |
| current baseline data preserved | pass | pass | pass |
| production unchanged | | | pass (87 files, tasks, processes) |

U1, plainly: after an update from 0.1.9 and before setup is rerun, Codex hooks fail outright where Codex runs them through PowerShell (7 of 7 captures failed through `pwsh.exe` with the parser error), so the setup rerun is required for Codex to deliver hooks at all, not only for the new features. Claude Code hooks keep working. The evidence note, `docs/distribution-validation.md` and the ledger row U1 say so. The cmd captures model a Codex whose PowerShell discovery failed; they do not claim Codex falls back to cmd.

## Forced-kill test (ANALYSIS-101, QA-100)

1. Started a full run in a hidden process, waited for the sandbox `HookStart-*` task to appear, then force-killed the process tree (`taskkill /F /T`). Result: the sandbox task and two sandbox `bell-tower` processes survived, and the killed run wrote no `result.json`. This reproduces QA-100 (an in-process handler cannot cover a forced kill).
2. Started the next run. Preflight printed `PREFLIGHT removed process bell-tower.exe pid <pid> (killed)` and `PREFLIGHT removed scheduled task \ZeusApps.BellTower\HookStart-<hash> (deleted)`; the run passed 57 of 57.
3. Afterwards the only Bell Tower task is the user's own production task (never selected: its data directory is outside `artifacts\field-acceptance\`), and no process remained under `artifacts\field-acceptance\runs\`.
4. A real console Ctrl+C was not sent; the `CancelKeyPress` and `ProcessExit` handlers are verified by source review only (the forced kill approximates an interruption but bypasses the handlers by definition).

## Checks

- `dotnet build -c Release --no-restore` -> passed, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~FieldAcceptance` -> 53 passed (28 before; new: sweep selector, sweep deletion, exposure guard, snapshot new-process gate, expected-failure report, preflight report, payload field names, route pin, Codex shell resolution).
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <changed .cs files>` -> passed (the formatter was applied first).
- `scripts/field-acceptance.ps1 -EvidenceOut ...` -> 57 of 57, run three times (the evidence run, the post-kill run, the final evidence run); no production change observed.
- `dotnet test -c Release --no-build --no-restore` (full suite) -> passed, 2159 of 2159 (no load-sensitive failures this time).

## Notes for reviewers

- New untracked files: `tools/BellTower.FieldAcceptance/Harness/{ProviderModel,CodexShell,HookStartRoute}.cs`, `Isolation/{LeftoverSweep,RealEnvironmentExposure}.cs`, `tests/BellTower.Tests/FieldAcceptance/{LeftoverSweepTests,ProviderModelTests}.cs`, this file. Modified in this cycle: `Program.cs`, `Evidence/{CheckResult,RunReport}.cs`, `Harness/{AgentSession,FakeAgent,Instance,Tooling}.cs`, `Isolation/ProductionSnapshot.cs`, `Scenarios/{RunContext,UpgradeWithoutSetupScenario}.cs`, the harness csproj (fixture link), `ProductionSnapshotTests.cs`, `RunReportTests.cs`, `docs/distribution-validation.md`, `docs/development.md`, `evidence/harness-2.1.md` (regenerated) and `evidence/coverage-ledger.md` (U1 sentence only).
- The sweep selects by an exported task definition, so an unreadable definition is never deleted. The real production task on this machine was queried read-only to confirm it is not selected; it was not touched.
