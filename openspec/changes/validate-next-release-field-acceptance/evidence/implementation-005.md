# Implementation 005: task 2.1, fix cycle 2 (DEV-005)

Fixes ANALYSIS-120, -121, -122, the ANALYSIS-104 shell-order residual and the parts of ANALYSIS-123 tied to them. All of these await independent verification; none is closed here. Harness, tests, script and docs only; no product code.

- Baseline `02d4f56`. Identity after this work: `head=02d4f56 baseline=02d4f56 files=49 diff=42172a159ba4 untracked=43/0485b37ebb18 (including this file)`.
- Client / model: Claude Code, `claude-sonnet-5-5`.
- Context7 (`/websites/learn_microsoft_en-us_dotnet`): `PosixSignalRegistration.Create` with `PosixSignal.SIGHUP` is the Windows `CTRL_CLOSE_EVENT` route; only SIGINT and SIGQUIT can be cancelled on Windows, so a close handler cannot keep the process alive and must do bounded work; .NET 10 no longer installs default `CTRL_CLOSE_EVENT` and `CTRL_SHUTDOWN_EVENT` handlers.

## What changed

### ANALYSIS-120: interrupt handling

- `Harness/RunAbort.cs` (new): a run-wide stop request plus `RunInterruptedException`.
- `Ctrl+C` only signals: `Cancel = true`, sets the stop request, and ends the run's sandbox processes through `LeftoverSweep.KillProcessesUnder(<run sandbox directory>, own pid)`. A second Ctrl+C takes the default action.
- `Instance.Run`, `Instance.Create`, `AgentSession.Start`, `AgentSession.Hook` and `TowerReader.WaitFor` refuse new work once the request is set. A session that ends mid-hook, or a child that was ended, becomes the stop and not a misleading failed check. Disposal is exempt per thread (`[ThreadStatic]`), so a second thread unwinding does not slip past the stop.
- The main thread unwinds through the existing `finally`: one cleanup, the production comparison, then a failing `run/interrupted` check, `Finish` and exit code 130. The scenario loop catches `RunInterruptedException` and stops. The committed evidence path is never written from the interrupted run (`--evidence-out` is dropped).
- QA's gap (an interrupt between cleanup and `Finish`) is closed because no handler runs `Finish` any more: the main thread always reaches it. An interrupt that lands after the final stop check but before `Finish` yields a complete, valid result for a finished run (documented, exit by result).
- `RunReport.Checks`, `RunContext.Instances` and `Instance.Sessions` are `LockedList<T>` (snapshot reads). `InstanceCleanup` is idempotent and serialized by a lock, so the main thread, the SIGHUP handler and the exit handler never overlap.
- Console close: `PosixSignalRegistration.Create(PosixSignal.SIGHUP, ...)` signals, ends sandbox processes, runs the shared cleanup under that lock and waits up to 4 seconds for the main thread to write `result.json`. Verified twice with a real window close (below); documented as best effort with the preflight sweep as the fallback.

### ANALYSIS-121: concurrent runs

Chosen: a run-exclusive named mutex `Local\BellTower.FieldAcceptance` (`Isolation/RunLock.cs`), taken before anything is created, held for the whole run; a second run exits 2 with a clear message. `scripts/field-acceptance.ps1` probes it (`Mutex.TryOpenExisting`) before it builds, so a running harness's binaries are not rewritten.

Why not owner records: the before and after production comparison diffs the Bell Tower scheduled tasks, so a concurrent run's task would count as a production change in every other run even with a precise sweep. Concurrency cannot be made correct by sweeping alone; refusing is the honest behavior. A killed run's mutex is abandoned and acquired by the next run (tested).

### ANALYSIS-122: exposure guard as a mechanism

- `RealEnvironmentExposure` is per instance (`Instance.Exposure`), sticky and thread-safe. `NoteSessionStart(model, arguments)` marks shared-backend Codex (`ProviderModel.UsesSharedBackend`: Codex without `--no-daemon`). `NoteHook(before, after)` marks a hook-started tray when `HookTrayStart.LastAttempt` changed to a `Started` outcome across a hook. `AgentSession.Start` and `Hook` call them through `Instance.Expose`; both marks in one instance throw `IsolationException` after `Guard` ended that instance's tray processes (everything under its install directory; sessions untouched).
- `StartSession` takes optional arguments, so a 2.7 scenario can start a plain Codex and is refused automatically in an instance with hook starts. The run-wide `RunContext.Exposure` and the scenario's manual `BeginHookStartedTray()` call are gone.
- `Harness/TrayReplacement.cs`: exit, require the tray stopped, otherwise end it directly, then `start --silent` and require a running tray. The scenario records `hook-started-tray-replaced` per provider from it. The "restarted" sentence appears only when the restart succeeded, and the old `hook-started-tray` detail no longer claims it. `TowerReader.WaitFor` now returns false on timeout. In the stale state, where nothing started, the restore of the sandbox tray is a `tray-restored` check.
- Docs state which combinations need separate instances and that `--scenario all` stays workable (marks are per instance).

### ANALYSIS-104 shell-order residual

`CodexShell.Resolve` now follows `rust-v0.164.0`: `pwsh.exe` on the whole PATH (first allowed hit), the fixed PowerShell 7 path, `powershell.exe` on the whole PATH, the fixed Windows PowerShell path, `cmd.exe`. The guard exception remains; a skipped PATH hit sets `ResolvedShell.Note` naming the edition Codex would have used (no path), and the scenario puts it in the run notes. The test now proves PowerShell 7 wins when Windows PowerShell comes first on PATH.

### ANALYSIS-123 (only the parts tied to these fixes)

Docs: PowerShell 7 added to the executable allow-list; the shell-order, console-close and concurrency texts rewritten to match the code; the "never branch on the provider" claim in `docs/distribution-validation.md`, `docs/development.md` and the `ProviderModel` comment softened to "avoid provider-name branches" with the remaining branches named. I did not move the remaining branches into the model (check IDs and predicates unchanged). ANALYSIS-124 (ANSI) not taken.

Check IDs: all 57 earlier IDs and predicates are unchanged; 4 new ones (`hook-started-tray-replaced` for claude and codex in the current state, `tray-restored` for both in the stale state), so the run now has 61 checks.

## Tests

`tests/BellTower.Tests/FieldAcceptance/RunControlTests.cs` (new) and `ProviderModelTests.cs` (shell order rewritten), `LeftoverSweepTests.cs` (the old run-wide guard test removed):

- a Codex session without `--no-daemon` marks the instance, Claude and `--no-daemon` do not; a hook that changes the attempt record to `Started` marks it, an unchanged record does not;
- the combination throws in either order, stays sticky, and the same pair in two instances does not throw; a refused combination calls the tray-ending action;
- `TrayReplacement`: success; a failing `exit` fails and ends the tray directly; a failing start fails and never says "restarted";
- `WaitFor` returns false on timeout; a stop request makes `WaitFor` throw; the first stop reason wins;
- the report stays readable while another thread adds checks; an interrupt check fails the run and serializes;
- `RunLock`: a second acquisition fails while held and succeeds after release; an abandoned lock (owner ended without release) is acquired;
- shell order: PowerShell 7 wins before Windows PowerShell on PATH; fixed pwsh beats `powershell.exe` on PATH; the full five-step order; a skipped PATH PowerShell names the edition and carries no path.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build -c Release --no-restore` | passed, 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~FieldAcceptance` | passed, 67 of 67 (was 53) |
| `dotnet test -c Release --no-build --no-restore` (full) | passed, 2173 of 2173 |
| `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <all .cs files under tools/BellTower.FieldAcceptance and tests/BellTower.Tests/FieldAcceptance>` | passed, exit 0 (after the mutating run on the same files) |
| Full harness run (`scripts/field-acceptance.ps1 -EvidenceOut .../harness-2.1.md`), three runs | 61 of 61 passed, 0 harness failures, 1 expected failure observed (`stale/captures-default-shell [codex]`); about 92 s each; `harness-2.1.md` regenerated by the last run on the final code; no private value found in it |

Per-check results: every earlier check passes as before (the provider-by-provider table is in `harness-2.1.md`), `current/hook-started-tray-replaced` passes for claude and codex ("stopped after exit; restarted silently ... running"), `stale/tray-restored` passes for both, `isolation/production-unchanged` passes (87 files fingerprinted, no task or process change).

### Real Ctrl+C

The harness ran in its own hidden console (`CreateProcess` with `CREATE_NEW_CONSOLE`, output redirected). A separate helper process attached to that console, ignored the event for itself and called `GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0)`, which reaches every process on the console, as a real keystroke does.

- Sent after `baseline-tray`: exit code 130, `result.json` with 17 checks, `run/interrupted` failing, `isolation` passed (production comparison made), the optional evidence file not written, no sandbox process left, no extra scheduled task, sandbox directory deleted.
- Sent after `stale/captures-default-shell` (about 1.5 s later, tray and sessions running): exit 130, `result.json` with 31 checks, `run/interrupted` failing, same clean state.

### Console close

The same helper posted `WM_CLOSE` to the harness console window (what the close button does), twice (after `baseline-tray` and after `stale/native-adoption`). Both times the `SIGHUP` handler ran: `result.json` exists with a failing `run/interrupted` ("The console was closed."), isolation passed, no sandbox process or extra task left. Two samples are not a guarantee; the documented fallback is the next run's preflight sweep. Not tested: logoff or shutdown events.

### Forced kill

Started a run, waited for a sandbox task and sandbox processes (13 processes, 2 tasks including the production one), then `taskkill /F /T` on the run tree: no `result.json`; 2 sandbox processes and the sandbox task survived. The next run printed `PREFLIGHT removed process bell-tower.exe ... (killed)` and `PREFLIGHT removed scheduled task ...HookStart-... (deleted)`, passed 61 of 61, and afterwards no process was under the run directories and only the production task remained.

### Overlapping runs

- A second run started 20 s into a first one exited with code 2 and the mutex message, created no run directory, and the first run finished 61 of 61, unaffected.
- `scripts/field-acceptance.ps1` started during an active run threw "Another field acceptance run is active" before building.

## Limits and things not made reliable

- Console close is best effort by design (Windows ends the process shortly after the handler starts). The documented fallback is the preflight sweep. Logoff and shutdown signals were not tested.
- A Ctrl+C on the second press, or a process hung in code that never checks the stop request (an in-process wait that is neither a harness process, a hook nor `WaitFor`), ends only via the default action; the sweep recovers.
- An interrupt after the final stop check but before `Finish` produces a complete valid result and exit by result, not 130.
- The tray-ending action on a refused combination is covered by a unit test of `Guard` with a stubbed action; killing the real tray processes on that path was not exercised in a run (no scenario combines the two, by design).
- The shared-backend side is Codex only by nature (Claude Code has no shared backend); the hook-start side, the tray replacement and the interrupt behavior cover both providers and the mixed rows in the full run.
- Remaining provider-name branches in `UpgradeWithoutSetupScenario` stay (docs say so). ANALYSIS-124 (ANSI escapes in the evidence) is deferred as instructed.
- Not touched: `src/`, handoffs, specs, planning files and reports, the user's installation, data, tray and production task, `~/.claude`, `~/.codex`. The production `HookStart-*` task and its tray were present and unchanged throughout.
