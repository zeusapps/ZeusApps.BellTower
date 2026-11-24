<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-027 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

```
status: done
role / client / model: code-analyzer / Claude Code delegated subagent (Agent SDK; client version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.3 (DEV-039). New findings: ANALYSIS-2700 and 2701 (warnings), ANALYSIS-2702 to 2706 (suggestions). ANALYSIS-2707 to 2799 are unused.
changed or report paths: none. The write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-027.md was refused ("Subagents should return findings as text"). The full report text is inline below. Disposable output only: the ignored folder D:\Code\ZeusApps.BellTower\artifacts\analysis-027 (isolated build).
checks:
- dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <6 changed .cs> -> passed
- dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-027 -p:RestoreLockedMode=true -> passed (0 warnings, 0 errors)
- dotnet test <isolated dll> filtered to ProcessStopAuthorityTests and CodexSharedRuntimeServiceTests -> passed 17/17
- CodexSharedRuntimeServiceTests plus CodexTransportTests, 5-run loop -> passed 5 × 42/42
- regex probe of the source-scan pattern against 13 sample lines -> measured (8 misses)
- timed copy of the 180 MB build output -> measured (about 0.5 s)
- ManagementOwnershipProcessTests -> not run. BuiltApplication cannot resolve the isolated layout, and running in the standard layout would contend with the concurrent QA rebuild.
- standard-layout full suite and native Codex -> not run (role limits)
target identity: before (about 17:05Z) and after (17:35Z), identical: head=b669cf8 baseline=b669cf8 files=8 diff=d00dee1b2ca0 untracked=4/71b90bee802e
limitations / decisions needed:
- The report write was refused; the text is inline.
- Decision needed (ANALYSIS-2700): is it intended that owned-runtime stop terminates the Codex backend's entire descendant tree, including tool, MCP and agent-started background processes? Either way, docs/architecture.md and the guard's owner text currently describe it inaccurately.
- No defect was found that affects the independent processes the task names. I agree with DEV-039's "no defect" conclusion for those processes.
next: the orchestrator saves the report below as reports/analysis-027.md and triages ANALYSIS-2700 to 2706. Suggested: a developer fix for the docs, comment and test wording (2700, 2703, 2704), test coverage for a live observer and a shared session at tray exit and update (2701), and a stricter runtime-host test (2702).
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-027 (DEV-039, task 3.3)

## Review target

- Reviewer / role: ANALYSIS-027 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code delegated subagent (Agent SDK). The client version is not available to the subagent. Mode: the parent's permission mode. Model: claude-opus-5-5.
- Date: 2026-11-05T17:35Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-027. This is an independent analysis of DEV-039.
- Task and requirement references:
  - [tasks.md](../tasks.md) 3.3
  - [design.md](../design.md) decision 4
  - [implementation-039](../evidence/implementation-039.md)
  - AGENTS.md parity policy
- Scope:
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs`
  - `src/BellTower/IO/CodexRuntimeStore.cs`
  - `docs/architecture.md`
  - `tests/BellTower.Tests/Testing/ManagementCohort.cs`
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs`
  - `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs`
  - `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`
  - the implementation-039 evidence
- Neighboring paths audited:
  - Runtime:
    - `src/BellTower/Sessions/CodexRuntimeHost.cs`
    - `src/BellTower/Hosts/CodexRuntimeProcess.cs`
    - `src/BellTower/Hosts/CodexRuntimeConsole.cs`
    - `src/BellTower/Hosts/AttachedSessionConsole.cs`
  - CLI commands:
    - `src/BellTower/Cli/StopCodexRuntimeCommand.cs`
    - `src/BellTower/Cli/PrepareCodexRuntimeCommand.cs`
    - `src/BellTower/Cli/ExitCommand.cs`
    - `src/BellTower/Cli/DetachCommand.cs`
  - Sessions: `src/BellTower/Sessions/AttachmentLifecycle.cs`
  - Shell wrapper: `src/BellTower/Integration/BellTower.psm1` (wrapper `finally` block)
  - Tray:
    - `src/BellTower/Tray/TrayHost.cs`
    - `src/BellTower/Tray/TrayController.cs`
    - `src/BellTower/Tray/TrayApplication.cs` (exit and dispose)
  - Updates and launcher:
    - `src/BellTower/Updates/UpdateTransaction.cs`
    - `src/BellTower.Launcher/Program.cs`
  - Setup:
    - `src/BellTower/Setup/IntegrationRefresh.cs`
    - `src/BellTower/Setup/IntegrationSetup.cs`
    - `src/BellTower/Setup/SetupAction.cs`
    - `src/BellTower/Setup/ProviderHookProbe.cs`
    - `src/BellTower/Setup/IngressProvision.cs`
  - Other kill sites and process probes:
    - `src/BellTower/IO/OwnedProcessRunner.cs`
    - `src/BellTower/IO/GitReader.cs`
    - `src/BellTower/IO/ProcessProbe.cs`
    - `src/BellTower/IO/ProcessTable.cs`
    - `src/BellTower/Hosts/WindowsTerminalNavigator.cs`
    - `src/BellTower/Updates/ReleasePackage.cs`
  - VS Code extension: `vscode/*.js`
  - Test helpers:
    - `tests/BellTower.Tests/Testing/StandIns.cs`
    - `tests/BellTower.Tests/Testing/BuiltApplication.cs`
    - `tests/BellTower.Tests/Updates/HookMaintenanceTests.cs`
    - `tests/BellTower.Tests/Updates/DistributionTests.cs`
- Baseline commit: b669cf8
- Implementation identity before the analysis (`scripts/review-identity.ps1`, at about 17:05Z):
  - HEAD: b669cf8c4aca0a54a721f8aa771c9b29775d92da
  - Manifest:
    - untracked (`?`): `implementation-039.md`, `ProcessStopAuthorityTests.cs`, `ManagementOwnershipProcessTests.cs`, `ManagementCohort.cs`
    - modified (`M`): `docs/architecture.md`, `CodexRuntimeStore.cs`, `CodexRuntimeOpener.cs`, `CodexSharedRuntimeServiceTests.cs`
  - Tracked diff SHA-256: d00dee1b2ca04bef91e5210a12f49784dc8068499c9de6a6684da4cf9d0b9a2c
  - Untracked file hashes:

    | File | SHA-256 prefix |
    | --- | --- |
    | `implementation-039.md` | a0934bbf |
    | `ProcessStopAuthorityTests.cs` | f2ff0970 |
    | `ManagementOwnershipProcessTests.cs` | 3c31ae7c |
    | `ManagementCohort.cs` | b06e1000 |

  - Summary line: `head=b669cf8 baseline=b669cf8 files=8 diff=d00dee1b2ca0 untracked=4/71b90bee802e`
- Before/after identity: the after run at 17:35Z printed the same summary line, `head=b669cf8 baseline=b669cf8 files=8 diff=d00dee1b2ca0 untracked=4/71b90bee802e`. The target did not change during the analysis.
- Prior reports / rechecked findings: none. This is a new analysis. The 2700 series is new.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture / safety: terminating and signalling routes | Searched `src` for `.Kill(`, `TerminateProcess`, `TerminateJobObject`, job objects, `PostMessage`/`SendMessage`, `GenerateConsoleCtrlEvent`, `AttachConsole`/`FreeConsole`, `taskkill`, `Stop-Process`, the PowerShell `kill`/`spps` aliases, `Environment.Exit` and `ExitProcess`. Searched `vscode/*.js` for `kill`/`terminate`. Read every hit. | There are 9 kill sites in 7 files. Each one acts on a `Process` handle the same operation created and still holds: `Process.Start`, or `CodexRuntimeProcess.Start`, which holds the `CreateProcess` handle until `GetProcessById` has its own handle, so PID reuse is impossible. No job objects, window-close messages, console control events or console input writes. `SendMessage` in `StripForm` targets the tray's own window. `AttachConsole` is identity-checked and only joins a console. The VS Code extension has no kill. | The DEV-039 claim holds for the processes the task names: no route stops or signals an independent shared or private backend, a user's Claude or Codex client, their shell, or a process whose PID was reused. One nuance is in ANALYSIS-2700: tree kills reach descendants of the owned backend. |
| Owned-runtime stop and stop-marker races | Read `CodexRuntimeStore` (`Get`, `Stop`, `StopAndWait`), `CodexRuntimeHost.StartAsync`/`RunAsync`, the stop and prepare commands, and the wrapper `finally` block. | Attachment IDs are fresh GUIDs (psm1 line 33). `Stop` writes the marker only while a record or token exists. `StopAndWait` deletes files only after `IsAlive(manager)` is false, using PID plus creation ticks. `IsAlive` treats access denied as alive, which fails safe. A stale marker could stop a new runtime only if an attachment ID were reused, which never happens. | No route to an unowned process. The marker sends no signal, but the manager that honors it terminates the backend's entire process tree (ANALYSIS-2700). |
| Tray exit and single instance | Read `TrayHost`, `TrayController`, `DataPaths.LockName`, `TrayApplication.ExitThreadCore` and `TerminalTitlePump.Dispose`. | The exit request is a file under the data root. The mutex name is a hash of the normalized data root. A new tray deletes a stale `exit.request` before it starts polling. On exit, the tray only disposes its services and makes no title or presentation write. | An exit request cannot reach a tray that uses a different data directory. The edge case where two spellings of one directory (for example an 8.3 short name) produce different mutex names involves two Bell Tower trays, not an unowned process. This is not a finding. |
| Update coordinator and rollback, launcher | Read `UpdateTransaction.Apply`, `StartTray`, `WaitUntil`, `RefreshIntegrations`, `Cleanup` and the launcher's `Program.Main`. | The coordinator only writes an exit request and waits on the mutex. If the tray does not stop, it throws and terminates nothing. Before requesting exit during rollback, it checks the candidate tray's `presence.Activation`. `RefreshIntegrations` rewrites only the owned module and bridge listed in the isolated `integration.json`. Payload removal needs an exclusive lease. The launcher waits for its child and kills nothing. | No signal reaches an unowned process. |
| Setup removal | Read `IntegrationSetup.SettingsPath`, the default `Plan` flags and `SetupAction`. | With `--home` given, the setup ignores `CODEX_HOME` and `CLAUDE_CONFIG_DIR`. The test's removal plan has no bridge action. `SetupAction` gives up after 45 s and states that no editor process was stopped. | No process signal. The test is isolated from the user's homes. |
| Ownership explicitness | Read the diff of `CodexRuntimeOpener` and `CodexRuntimeStore` and the record-shape test. | Only doc comments changed. `CodexRuntimeStore.Stop(string)` is untyped. The separation of access and lifecycle records (`CodexAccess`, `CodexRuntime`) existed before this change. | See ANALYSIS-2704. |
| Source-scan guard | Ran the test's regex against 13 sample lines in PowerShell. | It does not match `TerminateJobObject`, job kill-on-close, `PostMessage`/`SendMessage` WM_CLOSE, `kill $pid`, `spps`, CIM `Terminate`, `WriteConsoleInput` or `.Kill (true)`. It does match `.Kill(`, `NtTerminateProcess`, `Stop-Process` and JS `child.kill(`. It counts per file. | Measured. See ANALYSIS-2703. |
| Test quality, isolation and parity | Read `ManagementCohort`, `ManagementOwnershipProcessTests`, `StandIns`, `BuiltApplication`, and the new test in `CodexSharedRuntimeServiceTests`. | Stand-ins are copies of cmd.exe that end after about 180 s. `Dispose` and `KillUnder` reach only processes under the workspace. Trays and coordinators use isolated data, installation and Codex homes. The tray row registers no Codex shared session, and the update rows register only the owned Codex session. No row holds a live observer connection when the action runs. | See ANALYSIS-2701 and ANALYSIS-2702. Isolation is sound. No user location is touched. |
| Duplication | Inspected the test helpers. | `CreateInstallation`, `CopyDirectory` and `Repository` overlap with `HookMaintenanceTests` (`CopyDirectory`, `ProjectRoot`), `BuiltApplication` and `TestWorkspace`. The update fixture's contract omits the `Capture` and `AttachmentHealth` fields that `HookMaintenanceTests` sets. | See ANALYSIS-2705. |
| Docs accuracy | Compared `docs/architecture.md` line 28 with the code. | The text says the owned runtime "can be stopped … only by the stop marker … which sends no signal" and that every process Bell Tower terminates "is a handle it created in the same operation". `StartAsync` kills the manager tree directly, and the manager kills the backend's whole tree. | See ANALYSIS-2700. |
| Performance | Timed two copies of the 180 MB `src/BellTower/bin/Release/net10.0-windows` folder into the scratch directory, then deleted them. Checked for test collections and parallelization settings. | Each copy took about 0.44 to 0.50 s, so copying the update payload is not the cost. No `[Collection]` or parallelization settings exist, so the process-heavy class runs in parallel with everything else. | Measured copy cost. The contention theory is a hypothesis (ANALYSIS-2706). |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <6 changed .cs>` | Exit code 0 | Passed |
| Build and tests (isolated) | `dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-027 -p:RestoreLockedMode=true`, then `dotnet test <isolated dll>` filtered to `ProcessStopAuthorityTests` and `CodexSharedRuntimeServiceTests`, then a 5-run loop of `CodexSharedRuntimeServiceTests` plus `CodexTransportTests`. | Build: 0 warnings, 0 errors. 17/17 passed. The loop passed 5 × 42/42. | Passed |
| Process tests | `ManagementOwnershipProcessTests` | `BuiltApplication.Executable` resolves `src/BellTower/bin/<config>/<tfm>`, which does not exist in the isolated artifacts layout. Running in the standard layout would compete with the concurrent QA rebuild. | Not run, for those two reasons. |
| Native check | none | The analyzer must not launch `codex` or `claude`. | Not performed. |

## Findings

### ANALYSIS-2700: Owned-runtime shutdown terminates the backend's whole descendant tree, and the docs and the guard's owner text say otherwise

- Severity: warning
- Related task / requirement: 3.3; design decision 4 ("The existing managed-runtime manager retains authority solely for backends it created")
- Locations:
  - `src/BellTower/Sessions/CodexRuntimeHost.cs:43`: `manager.Kill(entireProcessTree: true)` on the prepare-side failure or cancellation path
  - `src/BellTower/Sessions/CodexRuntimeHost.cs:150`: `server.Kill(entireProcessTree: true)` after the stop marker, shell death or a readiness failure
  - `docs/architecture.md:28`, the shutdown-authority sentences
  - `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs:17`, the owner text
- Observation: `Process.Kill(entireProcessTree: true)` on Windows terminates every descendant. The .NET runtime's `IsParentOf` matches a child by parent PID and by a start time later than the parent's (dotnet/runtime `Process.Windows.cs`, fetched through Context7). That start-time check guards against PID reuse, so no unrelated process is hit. The tree still includes every process the owned Codex app-server spawned: tool and shell commands, MCP servers, and background processes the agent started at the user's request. Bell Tower did not create those processes. The docs say the opposite in three places:
  - The owned runtime can be stopped "only by the stop marker … which sends no signal". The marker leads the manager to terminate the backend tree, and `StartAsync` kills the manager tree directly without a marker.
  - "Every process Bell Tower terminates is a handle it created in the same operation."
  - "Detach, setup removal, tray exit, update and rollback dispose Bell Tower's observer connections". Only the tray holds observer connections.

  This is the only gap I found in the "no defect" conclusion. It does not affect the independent processes the task names. It affects the scope of what owned shutdown reaps, and that behavior predates DEV-039.
- Proposed improvement and benefit:
  1. Correct the docs paragraph and the guard's owner text. Say that the marker asks the owned manager to end. The manager then terminates the backend it started together with that backend's descendants, with PID reuse guarded by .NET's start-time check. A failed or cancelled prepare terminates the manager's tree.
  2. The orchestrator decides whether reaping agent-started background processes when an owned session ends is intended. It differs from a native launch only if native Codex leaves such processes running when its TUI exits. That is unverified, so treat it as a hypothesis. If it is not intended, terminate only the backend and the wrapper process, without the tree. This risks orphaned MCP servers or tool processes that stay attached to the shell console.
- Behavior-preservation risks:
  - Changing to a non-tree kill could leave orphans, including the PowerShell-wrapper case where the `server` is `powershell.exe`.
  - A docs-only fix carries no risk.
- Evidence type: source observation, plus the .NET source fetched through Context7. Native Codex child cleanup was not observed.
- Verification / measurement plan:
  - Use a `.cmd` stand-in backend, which takes the non-native PowerShell-wrapper path, that starts a detached grandchild. Assert the documented outcome after the stop marker.
  - Optionally, run an isolated native probe comparing what happens to a Codex-spawned helper on native TUI exit and on owned-runtime stop.
- Relationship: none

### ANALYSIS-2701: Process coverage claims more than it shows: there is no Codex shared session at tray exit, the update rows are not mixed, and no row holds a live observer when the action runs

- Severity: warning
- Related task / requirement: 3.3 ("verify an independently started private provider/backend survives every Bell Tower management action"); the AGENTS.md requirement for matched and mixed parity scenarios
- Locations:
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:240-244`: the comment says "(direct, shared, owned)", but only Claude direct, Codex direct and Codex owned are registered
  - same file, `:329`: the update rows register only the owned Codex session
  - same file, `ManagementCohort.OwnedRecord`, `:69`: endpoint `ws://127.0.0.1:9`, where any connection attempt fails
  - [implementation-039](../evidence/implementation-039.md), Coverage and Native check sections
- Observation:
  - When the tray exits or is replaced by the update, it has no open owned or shared connection. The owned endpoint is a dead port, and no shared session is registered. "Observer disposal" during management actions is therefore proven only in process, by `CodexSharedRuntimeServiceTests.DisposingAnObserverConnectionLeavesBothBackendsServingTheNextOne`.
  - The native check ran the real backend in a fresh home under the drive root. The tray's `CODEX_HOME` was the workspace `codex-home`, so Bell Tower never peer-verified or connected to the real backend. Its unchanged PID shows only that actions never addressed to it did not stop it.
  - The evidence says "mixed sessions in the tray and update rows". The update rows are not mixed.
- Proposed improvement and benefit:
  - Tray row:
    - Register a Codex shared session.
    - Point the owned record at an in-test `CodexStandInServer` with the fixture token, so the tray actually opens an owned observer. Wait for `initialize` before sending `exit`.
    - After exit, assert that the stand-in saw the connection close and received no `thread/resume`, `thread/start`, `thread/archive` or `turn/*`.
  - Update rows: register Claude direct and Codex direct and shared sessions next to the owned one.
  - Native rerun: give the tray the native backend's isolated home as `CODEX_HOME` and register a shared session, so the real backend is observed before tray exit and update.
  - Correct the evidence wording.

  Benefit: the claim that management actions dispose only observers is then shown end to end for both Codex access kinds, with Claude present at the same time.
- Behavior-preservation risks: tests only. The shared path fails closed at peer verification against stand-ins, which is acceptable for the stand-in row, while the native row covers a real peer.
- Evidence type: source observation
- Verification / measurement plan:
  - Confirm that the native-name or permission pump opens a connection for the registered owned session, by checking the stand-in's `initialize` count before exit.
  - Run the class three times.
- Relationship: none

### ANALYSIS-2702: `RealRuntimeHostStopsOnlyTheBackendItStarted` can pass without the marker and without seeing the backend

- Severity: suggestion
- Related task / requirement: 3.3
- Locations: `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:187-206`
- Observation:
  - The test falls back to `backend = []`, so the "backend the manager started was left running" assertion can be skipped.
  - `CodexRuntimeStore.Stop` writes nothing if the manager has not yet written its token.
  - The stand-in is a copy of cmd.exe given `app-server …` arguments. It runs as an interactive cmd whose lifetime depends on the inherited stdin. If stdin is at end of file, the stand-in exits at once, and the manager ends on its own path (`codex-runtime-exit-N`) rather than on the marker.
  - Either way the test passes, so it does not show that the marker-driven stop works.
- Proposed improvement and benefit:
  - Fail when the backend is not observed instead of falling back.
  - Wait for the token file before calling `Stop`.
  - Assert the log reason, or that the manager was alive when the marker was written.
  - Use a `.cmd` stand-in (non-native path) that loops and starts a grandchild. This also covers the tree reach from ANALYSIS-2700.
- Behavior-preservation risks: none, tests only.
- Evidence type: source observation. Not run here (see Analysis performed).
- Verification / measurement plan: run the test 5 times and confirm that the backend is observed and the marker path is taken each time.
- Relationship: ANALYSIS-2700

### ANALYSIS-2703: The source-scan guard misses several ways to terminate or signal a process, and the docs call its list complete

- Severity: suggestion
- Related task / requirement: 3.3
- Locations:
  - `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs:23-24` (regex) and `:31-33` (scanned roots)
  - `docs/architecture.md:28` ("pins the complete list of terminating call sites")
- Observation:
  - Measured against sample lines, the pattern misses `TerminateJobObject`, job kill-on-close flags, `PostMessage`/`SendMessage` with WM_CLOSE, the PowerShell `kill` and `spps` aliases, CIM `Terminate`, `WriteConsoleInput` (injected Ctrl+C) and `.Kill (`.
  - The VS Code extension (`vscode/*.js`) is a product component but is not scanned.
  - The check counts calls per file, so swapping an owned kill for an unowned one in an allowed file keeps the count and passes.
  - The record-shape test checks only property names. Its `EqualityContract` filter is redundant, because `GetProperties()` returns only public properties.
- Proposed improvement and benefit:
  - Add the missing patterns to the regex and scan `vscode/`.
  - Pin each allowed site by its receiver expression, for example `child.Kill(` and `manager.Kill(`, instead of by count.
  - Reword the docs to "pins the known terminating API call sites".

  This keeps the guard a cheap tripwire without claiming a proof.
- Behavior-preservation risks: none, test and docs only. Watch for false positives from `SendMessage` in `StripForm`, which targets the tray's own window and should be allowlisted.
- Evidence type: measured result (regex probe) and source observation
- Verification / measurement plan: rerun the probe lines against the new pattern. The test still passes on the current tree with the `StripForm` allowance added.
- Relationship: ANALYSIS-2700, for the owner text

### ANALYSIS-2704: Stop authority rests on the marker mechanism, not on the access type, and the new comment suggests otherwise

- Severity: suggestion
- Related task / requirement: 3.3; design decision 4
- Locations:
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs:15-16`
  - `src/BellTower/IO/CodexRuntimeStore.cs:20-30`
- Observation:
  - The ownership changes are documentation only. `CodexRuntimeStore.Stop(string attachment)` takes any string and never receives a `CodexAccess.Owned`. The new comment on `Owned` says only this kind "can be stopped (through `CodexRuntimeStore.Stop`)".
  - The real authority is that the marker is honored only by the manager Bell Tower started. That is stronger than a type, and it was already in place.
  - Design decision 4's "distinct records" (`CodexAccess` for access, `CodexRuntime` for lifecycle) predates DEV-039. The task's separation of observer disposal from owned shutdown is therefore met by structure, not by the new comments.
- Proposed improvement and benefit: reword the `Owned` comment to describe the mechanism. Access records never stop anything, and an attachment's runtime record plus a marker honored by its own manager is the only stop path. No new abstraction is needed. This stops future maintainers from treating an `Owned` instance as required or sufficient for a stop.
- Behavior-preservation risks: none (comment only).
- Evidence type: source observation
- Verification / measurement plan: inspection
- Relationship: none

### ANALYSIS-2705: The packaged-installation test fixture is duplicated and differs from the existing one

- Severity: suggestion
- Related task / requirement: 3.3, test maintainability
- Locations:
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:392-446`
  - `tests/BellTower.Tests/Updates/HookMaintenanceTests.cs:~130-170`
  - `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs:53`
  - `tests/BellTower.Tests/Testing/BuiltApplication.cs:5`
- Observation:
  - `CreateInstallation`, `CopyDirectory` and `Repository` repeat the existing real-payload builder in `HookMaintenanceTests` and the repository lookup in `BuiltApplication` and `TestWorkspace`.
  - The new fixture's `ReleaseContract` omits `Capture` and `AttachmentHealth`, which the other fixture sets. The two update fixtures therefore exercise different compatibility gates.
  - The new fixture also copies the whole output folder, including a 176 MB `win-x64` subfolder that the payload does not need.
- Proposed improvement and benefit: when either test class is next touched, extract one `Testing` helper for a packaged installation with an explicit contract. This gives one fixture and consistent gates.
- Behavior-preservation risks: compatibility-gate assertions in `HookMaintenanceTests` must keep their explicit values.
- Evidence type: source observation; copy time measured at about 0.5 s
- Verification / measurement plan: both classes pass unchanged after the extraction.
- Relationship: none

### ANALYSIS-2706: The process-heavy class runs in parallel with the whole suite (performance hypothesis)

- Severity: suggestion
- Related task / requirement: 3.3, suite stability
- Locations: `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs`. No `[Collection]` or parallelization setting exists anywhere in the test project.
- Observation:
  - Each of the 19 cases starts 6 stand-ins plus their ping children, plus real bell-tower, tray, launcher or coordinator processes, with waits of up to 90 s.
  - Implementation-039 reports three different timing-sensitive failures in full-suite runs that pass alone: two `CodexTransportTests` and one `VsCodeWindowNavigationTests`.
  - Hypothesis, not measured: CPU and process-creation contention from this class contributes to those failures.
- Proposed improvement and benefit: if measurement confirms the hypothesis, put process-heavy classes in one non-parallel collection.
- Behavior-preservation risks: a longer serial wall time.
- Evidence type: performance hypothesis
- Verification / measurement plan:
  1. Time the class alone.
  2. Run the full standard-layout suite 3 times with the class and 3 times filtered out.
  3. Compare failure counts and durations.
- Relationship: none

## Re-verification

None. This is a new analysis.

## Assessment and limitations

- Verdict on "the existing code had no defect": I agree for the processes the task names. I found no route by which detach, `stop-codex-runtime`, setup removal, tray exit, the update coordinator, rollback, the launcher, the runtime host or the exit and single-instance mechanism stops, signals or reconfigures an independent shared or private Codex backend, a user's Claude or Codex client, their shell, or a process whose PID was reused:
  - Every kill acts on a handle the same operation holds.
  - Stop markers are honored only by the owned manager.
  - Exit requests are scoped to the data root.
  - The coordinator never kills.
- The remaining issue is that the docs overstate what owned shutdown terminates, because it reaps the backend's descendants (ANALYSIS-2700). That behavior predates DEV-039 and needs an orchestrator decision on whether it is intended.
- Ownership: the separation is structural and predates the change. DEV-039 added comments, one of which is slightly misleading (ANALYSIS-2704).
- Tests:
  - The tests are well isolated: data directories, installations and Codex and Claude homes are disposable, and process cleanup is bounded.
  - The coverage claims in the evidence and test comments go beyond what runs (ANALYSIS-2701 and ANALYSIS-2702).
  - The source-scan test is a useful but incomplete tripwire (ANALYSIS-2703).
- Parity, by client:
  - Claude: covered at detach, tray exit and setup removal.
  - Codex: direct, shared and owned are covered at detach and stop.
  - Missing: Codex shared at tray exit, and Claude or Codex direct and shared in the update rows (ANALYSIS-2701).
- Not performed:
  - `ManagementOwnershipProcessTests`: not runnable in the isolated layout, and running it in the standard layout risked contending with the concurrent QA rebuild.
  - The standard-layout full suite.
  - Native Codex checks: the analyzer does not launch providers.
- Unexpected writes: none. I wrote only the ignored folder `artifacts/analysis-027` (isolated build output) and a deleted scratch copy outside the repository. The report write was refused, so the report text is inline in this message.
- Decision needed: whether the tree-wide reap on owned-runtime stop is intended product behavior (ANALYSIS-2700).
