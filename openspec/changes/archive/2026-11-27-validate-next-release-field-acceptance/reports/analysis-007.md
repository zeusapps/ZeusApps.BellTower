> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125) after the reviewer's write was refused.
> Reviewer: ANALYSIS-007 code-analyzer subagent; client Claude Code 2.1.332 (delegated subagent, role default); model claude-opus-5-5.
> Refusal message as reported by the reviewer: "Subagents should return findings as text, not write report files." Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-007

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.332 / delegated subagent, role default / `claude-opus-5-5`
- Date: 2026-11-25, finished 14:09Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-007, a recheck of task 2.1 after fix cycle 1 (developer DEV-004)
- Task and requirement references:
  - [tasks.md](../tasks.md) 2.1, plus fitness for 2.4 and 2.7
  - [implementation-004.md](../evidence/implementation-004.md), [harness-2.1.md](../evidence/harness-2.1.md)
  - [findings.md](findings.md), user decisions of 2026-11-25: the ANALYSIS-100 product option is deferred to a later release, and the ANALYSIS-101 product folder option is dismissed
- Scope:
  - new files: `tools/BellTower.FieldAcceptance/Harness/{ProviderModel,CodexShell,HookStartRoute}.cs` and `Isolation/{LeftoverSweep,RealEnvironmentExposure}.cs`
  - modified files: `Program.cs`, `Evidence/{CheckResult,RunReport}.cs`, `Harness/{AgentSession,FakeAgent,Instance,Tooling}.cs`, `Isolation/ProductionSnapshot.cs` and `Scenarios/{RunContext,UpgradeWithoutSetupScenario}.cs`
  - tests: `tests/BellTower.Tests/FieldAcceptance/{LeftoverSweepTests,ProviderModelTests,ProductionSnapshotTests,RunReportTests}.cs`
  - docs: `docs/distribution-validation.md`, `docs/development.md`
- Neighbors read:
  - `src/BellTower.Protocol/{TrayProcess,HookTrayStart,HookStartTask}.cs`, `src/BellTower/Setup/HookStartTaskSetup.cs`, `src/BellTower.Launcher/Program.cs`, `vscode/extension.js`
  - `openspec/changes/archive/2026-10-28-fix-codex-hook-command-shell/evidence/native-investigation-002.md` and `-003.md`
  - the pinned Codex source those reports cite: `codex-rs/shell-command/src/shell_detect.rs` at tag `rust-v0.164.0`
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence` -> `Summary: head=02d4f56 baseline=02d4f56 files=43 diff=d73401e972b1 untracked=37/e6f9cb8f1838`. This matches the assignment.
- Before/after identity: the same command at the end printed the same summary line. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-006.md](analysis-006.md). Rechecked: ANALYSIS-100 (harness side only), ANALYSIS-101, ANALYSIS-102 and ANALYSIS-104, including step 1 of ANALYSIS-107 (the provider model).

The harness files are untracked. Their line numbers refer to the working tree captured by the identity above.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 100: route, replacement, documentation | Read the probe (`UpgradeWithoutSetupScenario.cs:337-378`) against `HookTrayStart.TryStart`, `LastAttempt` and `TrayProcess.Start`. Confirmed that `start --silent` starts the tray through the launcher's managed `Process.Start` (`src/BellTower.Launcher/Program.cs:23`), which inherits the sandbox environment | The route is read from the record and required in the current state. The replacement tray inherits the sandbox environment. The docs and the detail now state the exposure | Verified for 2.1. The replacement is not itself verified (see ANALYSIS-122) |
| 100: guard for 2.7 | Read `RealEnvironmentExposure`, every caller of it, and `ProviderModel.Codex.AgentArguments`. Compared them with the text of 2.4 and 2.7 | Callers must opt in. The guard is sticky for the whole run. Nothing ties it to a shared-backend start or to a hook start outside the probe | ANALYSIS-122 |
| 101: selector safety | Read `LeftoverSweep.IsHarnessTask`, `PathRelation` and the product task arguments (`HookStartTaskSetup.cs:26`, `tray --data-dir "<root>"`). Traced these inputs: `..`, relative paths, environment-variable forms, trailing arguments, a prefix-sibling folder, malformed XML and a DTD | A task is selected only when it holds a rooted data directory that is under `artifacts\field-acceptance` after `GetFullPath` and link resolution. `..` is normalized away. Unrooted forms are rejected. `XDocument.Parse` prohibits DTDs by default, and the resulting `XmlException` is caught. A failed `/xml` query is never deleted | Verified: production cannot be selected. Remaining gaps all fail safe (re-verification notes) |
| 101: handlers and cleanup | Read `InstanceCleanup`, `Abort`, both handlers and `Instance.Dispose`. Checked the .NET 10 documentation through Context7 (`/websites/learn_microsoft_en-us_dotnet`) | Each instance is cleaned in its own try/catch, and both `Dispose` and `InstanceCleanup` are idempotent. The handlers run cleanup and `Finish` on another thread while the main thread keeps working. .NET 10 no longer installs a default `CTRL_CLOSE_EVENT` handler | ANALYSIS-120 |
| 101: concurrent runs | Read the sweep scope and the docs caveat | A second run's preflight kills the first run's processes and deletes its task. Only the docs warn against this; nothing prevents it | ANALYSIS-121 |
| 102 | Read `ProductionSnapshot.Diff:87-96` and the new test (`ProductionSnapshotTests.cs:101-114`) | A new process and a reused pid both gate | Verified |
| 104: Codex shell | Compared `CodexShell.Resolve` and its test with the archived reports and with `shell_detect.rs` at `rust-v0.164.0` (fetched read-only) | Codex searches the whole `PATH` for `pwsh`, then checks the fixed `pwsh` path, then searches `PATH` for `powershell`, then checks the fixed `powershell` path, then falls back to `cmd.exe`. The harness instead tries `pwsh` and then `powershell` in each `PATH` entry in turn, and a unit test pins that order | ANALYSIS-104 is partly still failing (re-verification notes) |
| 104: payloads, expected failures, labels | Read `ProviderModel.Payload`, its field-name test, `FakeAgent.Probe`, the `RunReport` counters and the stale-state predicates. Compared them with the committed evidence | Capture payloads come from the fixtures and the invented `turn_id` is gone. An expected failure cannot hide a different failure. Rows are labeled, and the mixed row reports both counts | Verified. New regression: raw ANSI escapes in the evidence (ANALYSIS-124) |
| ProviderModel (step 1 of 107) | Searched the scenario for remaining provider-name branches | The listed items moved into the model. Several `provider == ...` branches remain, while the docs say scenarios never branch | Partly done. The wording contradiction is in ANALYSIS-123 |
| `HookStartRoute.Shell` copy | Read the pin test (`ProviderModelTests.cs:34-36`) | The test compiles against the internal `TrayProcess.RouteShell` and runs in the normal suite, so any drift fails the test | Acceptable (assessment below) |
| Docs and tasks consistency | Compared `docs/distribution-validation.md:27-29`, `docs/development.md:65` and the code comments with the code | Four statements contradict the code | ANALYSIS-123 |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~FieldAcceptance"` | 53 passed, 0 failed | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 19 harness and test .cs files of this cycle>` | exit code 0 | passed |
| Build, harness run | Not run | QA runs the harness concurrently. A build would rewrite the harness binaries under it, and the assignment forbids a run. Findings rest on source, the committed evidence and documentation | not performed (deliberate) |

## Findings

### ANALYSIS-120: The interrupt handlers clean up concurrently with the still-running main thread, and a closed console runs no handler on .NET 10

- Severity: warning
- Related task / requirement: 2.1 isolation (fix for ANALYSIS-101 and QA-100), and the interruption-prone tasks 2.4 and 2.5
- Locations:
  - `tools/BellTower.FieldAcceptance/Program.cs:117-143` (`Abort`, `CancelKeyPress` and `ProcessExit`) and `:195-233` (`InstanceCleanup`)
  - `tools/BellTower.FieldAcceptance/Evidence/RunReport.cs:29,35-39` (`Checks` is a plain `List`)
  - `tools/BellTower.FieldAcceptance/Scenarios/RunContext.cs:17` (`Instances` is a plain `List`)
  - `docs/distribution-validation.md:28` ("it also runs from `Console.CancelKeyPress` and `AppDomain.ProcessExit`")
- Observation:
  - `CancelKeyPress` runs on its own thread. It disposes the instances and then calls `Finish` while the main thread keeps running the scenario.
  - The main thread can still start processes after the sweep in `Dispose`, for example the next hook or a `start --silent`. Those processes survive `Environment.Exit(130)`.
  - `Finish` serializes `report.Checks` while the main thread may be adding to it. That throws `InvalidOperationException`, which the handler's filter (`IOException`, `InvalidDataException`, `UnauthorizedAccessException`) does not catch. The handler thread then crashes the process without writing `result.json`.
  - A Ctrl+C after the `finally` block has already run cleanup finds `cleanup.Run()` returning false. `Environment.Exit` then ends the production comparison and `Finish` midway, and no result is written.
  - Console close: the .NET 10 breaking change "runtime no longer provides default termination signal handlers" (learn.microsoft.com, core-libraries/10.0/sigterm-signal-handler) says that on Windows the runtime no longer handles `CTRL_CLOSE_EVENT`. A closed console therefore raises no `ProcessExit`. The claim in the code comment and the docs that a closed console runs the same cleanup is not true for this `net10.0-windows` tool.
  - The preflight sweep still removes leftovers from any of these cases. So this is not a production-safety gap, but the "always writes `result.json`" and "same cleanup" claims do not hold.
- Proposed improvement and benefit:
  1. Make `CancelKeyPress` signal only:
     - set `Cancel = true` and set a run-wide abort flag, or cancel a `CancellationTokenSource`;
     - end the in-flight sandbox children with `LeftoverSweep.KillProcessesUnder(<sandbox base>, own pid)`, so the main thread does not stay blocked.
  2. Have `Instance.Run`, `AgentSession.Start` and `AgentSession.Hook` refuse new work once the flag is set. The main thread then unwinds through the existing `finally` block: one cleanup, the production comparison and `Finish`, all on one thread, with a `run/interrupted` check added there.
  3. For console close, register `PosixSignalRegistration.Create(PosixSignal.SIGHUP, ...)`, which maps to `CTRL_CLOSE_EVENT` on Windows. Its handler can only do the same bounded in-handler cleanup, so take a lock shared with the normal path. Otherwise state in the docs that a closed console is covered only by the next run's preflight.
- Behavior-preservation risks: the exit code 130 and the failing `run/interrupted` check must stay. Never write the committed evidence file from the interrupted path.
- Evidence type: source observation, plus primary documentation for the .NET 10 console-close behavior
- Verification / measurement plan:
  - A unit test: abort while a fake scenario is still adding checks. Expect one `result.json` with `run/interrupted` and no exception.
  - A disposable run: send Ctrl+C with `GenerateConsoleCtrlEvent` to a hidden console process group after `setup --host-bridge`. Confirm the result file exists and that nothing remains under `runs\` before the next preflight.
- Relationship: residual of ANALYSIS-101 (handler correctness)

### ANALYSIS-121: The preflight sweep destroys a concurrently running harness run; only the docs prevent it

- Severity: warning
- Related task / requirement: 2.1 isolation and evidence integrity for every later scenario
- Locations:
  - `tools/BellTower.FieldAcceptance/Isolation/LeftoverSweep.cs:10` (the doc comment: "Run it while no other harness run is active") and `:50-73`
  - `tools/BellTower.FieldAcceptance/Program.cs:110`
  - `docs/distribution-validation.md:28` ("Do not start a second harness run while another is active")
- Observation:
  - The sweep matches every task and process under `artifacts\field-acceptance`, including those of a run that is still alive.
  - The delegated workflow already runs harness invocations from more than one agent. In this cycle the developer made three runs and QA runs one concurrently with this review.
  - If two runs overlap, the second run kills the first run's trays, fake sessions and hook shells and deletes its task. The first run then fails with misleading checks: a session that "ended while running a hook", or a missing tray. The cause appears only as a `PREFLIGHT removed` line in the other run's output.
  - Nothing reaches production, and the first run fails rather than passing falsely. The cost is lost and misattributed evidence.
- Proposed improvement and benefit:
  - Take a run-exclusive lock before the sweep, for example a named mutex `Local\BellTower.FieldAcceptance` held for the whole run. A second run then stops with exit code 2 and a clear message.
  - A more precise alternative: write an owner record (pid plus start time) into each run directory and sweep only directories whose owner is gone. That keeps concurrent runs possible.
  - Either way, the docs caveat becomes enforced behavior.
- Behavior-preservation risks: an abandoned mutex after a forced kill is released by Windows, so the next run still proceeds. The owner-record variant must treat an unreadable record as "owner gone" only for directories under `runs\`.
- Evidence type: source observation
- Verification / measurement plan:
  - A unit test of the lock helper: a second acquisition fails.
  - A disposable check: start two runs; the second refuses and the first passes unchanged.
- Relationship: residual of ANALYSIS-101 (introduced by the sweep)

### ANALYSIS-122: The real-environment exposure guard and the tray replacement are conventions, not mechanisms; not robust enough for 2.4 and 2.7

- Severity: warning
- Related task / requirement: 2.7 (plain shared-backend `codex`; a stopped tray started by the first hook; never started during an update or switch), 2.4 (tray downtime, upgrade and rollback), 2.1 isolation
- Locations:
  - `tools/BellTower.FieldAcceptance/Isolation/RealEnvironmentExposure.cs:9-36`
  - `tools/BellTower.FieldAcceptance/Scenarios/RunContext.cs:19-20` (the guard is run-wide)
  - `tools/BellTower.FieldAcceptance/Harness/ProviderModel.cs:96` (Codex agent arguments are fixed to `--no-daemon`)
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:36-95` (neither starting a session nor running a hook touches the guard)
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:343` (the only caller) and `:364-377` (the replacement)
  - `tools/BellTower.FieldAcceptance/Harness/TowerReader.cs:57-68` (`WaitFor` returns silently on timeout)
- Observation:
  - Shared-backend side: nothing calls `UseSharedBackendCodex`. A 2.7 scenario that starts plain `codex`, a new model variant or a different argument list, is protected only if its author remembers to call it.
  - Hook-start side: the product starts a tray from any hook while the tray is not running and no user stop is recorded. 2.4's "tray downtime" (killing the tray) and 2.7's update and switch cases can produce hook-started trays outside the probe. They never reach `BeginHookStartedTray` and are never replaced, so they run with the real environment for the rest of the run.
  - Scope: the hazard is per data directory. A hook-started tray resolves shared Codex access only for sessions in its own data. The guard, however, is sticky for the whole run. Once `upgrade-without-setup` has run, any shared-backend scenario in the same `--scenario all` run fails with `isolation-guard`. Tasks 2.7 and the docs do not say that such scenarios need a separate run.
  - The replacement is unchecked:
    - `WaitFor(!TrayRunning)` and `WaitFor(TrayRunning)` return silently on timeout. If `exit` does not stop the hook-started tray, the `TrayRunning` test skips the restart and the real-environment tray keeps running.
    - The check was recorded before the replacement, and its detail already says "the harness exited it and restarted the tray silently". So the evidence asserts an action that may not have happened.
- Proposed improvement and benefit:
  1. Enforce the guard at the mechanism, per instance and sticky:
     - `AgentSession.Start` (or a model property) marks shared-backend Codex for its instance whenever a Codex session starts without `--no-daemon`.
     - After every hook, `AgentSession.Hook` or `Instance` compares `HookTrayStart.LastAttempt` with its value before the hook. On a new `Started` outcome, it marks the hook-started exposure for that instance.
     - A combination in one instance throws `IsolationException` and immediately ends that instance's trays through `KillProcessesUnder(<instance root>)`.
  2. Turn the replacement into evidence. After `start --silent`, require that the tray is running and that the earlier `exit` stopped the hook-started tray. Throw or fail a dedicated check otherwise, and write the "restarted" sentence only after the restart succeeded.
  3. State in the 2.7 planning or the docs which combinations need separate instances, and keep `--scenario all` workable.
- Behavior-preservation risks:
  - Detection after a hook cannot stop a tray that has already started. It must therefore end that tray at once and fail the run, rather than only record the exposure.
  - Per-instance scoping must stay sticky for the instance's whole life.
- Evidence type: source observation; that 2.4 and 2.7 cause hook starts outside the probe is inferred from product logic (`HookTrayStart.TryStart`) and the task text, not exercised
- Verification / measurement plan:
  - Unit tests: a Codex session without `--no-daemon` marks the instance; a recorded `Started` attempt after a hook marks the instance; the combination in one instance throws, while the same pair in two instances does not.
  - A scenario test where `exit` is stubbed to fail, so the replacement check fails.
- Relationship: residual of ANALYSIS-100 (harness side). This answers the assignment's question "is the guard robust enough for 2.7": not yet.

### ANALYSIS-123: Docs and comments now contradict the code in four places

- Severity: suggestion
- Related task / requirement: 2.1 documentation; parity wording
- Locations:
  - `docs/distribution-validation.md:27`: the executable allow-list reads "other than Git Bash, Windows PowerShell, `schtasks` and `cmd`". `SystemTools.All` (`Harness/Tooling.cs:9`) now also allows PowerShell 7.
  - `docs/distribution-validation.md:29`, `docs/development.md:65` and `Harness/ProviderModel.cs:9-10` say scenarios "never branch on the provider". `UpgradeWithoutSetupScenario.cs` still branches on the provider name at `:88`, `:234`, `:255-256`, `:311` and `:313-315`, and at `:84` for seed data.
  - `docs/distribution-validation.md:29` and the `CodexShell` doc comment (`Harness/CodexShell.cs:20-24`) present the harness order as the 0.164.0 order. It differs (see the ANALYSIS-104 notes).
  - `Program.cs:117-118` and `docs/distribution-validation.md:28` say a closed console runs the cleanup. It does not on .NET 10 (ANALYSIS-120).
- Observation: each statement is a rule that later scenario authors will rely on.
- Proposed improvement and benefit:
  - Add PowerShell 7 to the documented allow-list.
  - Either move the remaining branches into the model or soften the wording:
    - `:234` and `:255-256` encode "the 0.1.9 form needs the modeled fallback", which is `ModeledFallbackShell(...) is not null`;
    - `:311` can use `AgentArguments`;
    - the native-adoption expectations at `:313-315` could become a model property.
  - Fix the shell-order and console-close texts together with their code.
- Behavior-preservation risks: moving the branches must keep all 57 check IDs and their predicates.
- Evidence type: source observation
- Verification / measurement plan: text review after the fix. If the branches move, a 2.1 run gives identical check IDs and outcomes.
- Relationship: ANALYSIS-104 (provider model), ANALYSIS-120

### ANALYSIS-124: The switch to PowerShell 7 puts raw ANSI escape sequences into check details and the committed evidence

- Severity: suggestion
- Related task / requirement: 2.1 evidence; committed content hygiene (readability only, no private data)
- Locations:
  - `openspec/changes/validate-next-release-field-acceptance/evidence/harness-2.1.md:57` (raw ESC (0x1B) characters in the `stale/captures-default-shell` detail, for example `ESC[36;1mprovider ESC[0m`)
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:212` and `:459` (the hook's standard error is recorded verbatim, cut at 160 characters)
- Observation:
  - PowerShell 7 colors its error output; Windows PowerShell 5.1 did not.
  - The committed Markdown now holds terminal control codes. These also use up the 160-character excerpt, which is cut before the useful part of the parser message.
  - `IsParserError` still matches, so no check outcome is affected.
- Proposed improvement and benefit: strip `\x1B\[[0-9;?]*[A-Za-z]` from the hook output before it goes into a detail, either in `Trim` or once in `RunReport.Add`. Do not set `NO_COLOR` or similar in the hook environment, because that would change the modeled Codex shell environment.
- Behavior-preservation risks: none for the predicates. The detail text changes.
- Evidence type: source observation plus the committed evidence bytes (`grep -c $'\x1b'` finds 1 line)
- Verification / measurement plan: a unit test of the stripping. In the next evidence file, the same grep finds no ESC character.
- Relationship: regression of the ANALYSIS-104 fix

## Re-verification

All rechecks ran against the target `head=02d4f56 baseline=02d4f56 files=43 diff=d73401e972b1 untracked=37/e6f9cb8f1838`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-100 (harness side) | The current-state probe requires `HookStartRoute.Shell` from `HookTrayStart.LastAttempt`, and a task route fails with the route named (`UpgradeWithoutSetupScenario.cs:360-366`). The committed evidence shows `route started-via-shell` for claude and codex (`harness-2.1.md:81-82`). The replacement tray comes from `start --silent`, which the launcher starts with the managed `Process.Start` (`src/BellTower.Launcher/Program.cs:23`), inheriting the sandbox environment. The shell route alone uses `CreateEnvironmentBlock` (`TrayProcess.cs:242`). The exposure is documented in the detail (`:47-49`), the notes (`:147-149`) and `docs/distribution-validation.md:27`. The guard exists and has a test for both orders (`LeftoverSweepTests.cs:95-110`). The product option was deferred by the user and is not rechecked | Verified for 2.1. The guard and the replacement are not yet robust enough for 2.4 and 2.7: ANALYSIS-122 |
| ANALYSIS-101 | **Selector:** production cannot be selected (see the notes below). **Per-instance cleanup:** each instance's `Dispose`, and each step inside it, has its own try/catch. Both `Dispose` and `InstanceCleanup` are idempotent through `Interlocked.Exchange`, so the handler path, the `finally` path and `ProcessExit` after a normal finish clean up only once. The production comparison is wrapped, and `result.json` is written on the normal path. **Handlers:** they race with the main thread, and a closed console runs no handler on .NET 10. **Concurrent runs:** the sweep destroys them | Partly verified: the selector, per-instance cleanup and idempotence are verified. Still failing: handler correctness (ANALYSIS-120) and the hazard to concurrent runs (ANALYSIS-121) |
| ANALYSIS-102 | `Diff` adds a gating change for every after-run process missing from the before snapshot, compared as a `(pid, start ticks, image)` record (`ProductionSnapshot.cs:93-96`). The test covers a new process and a reused pid (`ProductionSnapshotTests.cs:101-114`) and passed | Verified. Known safe false positive: a production `process-info` or `foreground-window` call made by the VS Code extension (`vscode/extension.js:24,36`) and still running at the after snapshot fails the run. This direction was accepted in ANALYSIS-102 |
| ANALYSIS-104 | See the notes below | Partly verified: payloads, expected-failure semantics, stale-row labels, the mixed counts and shell naming are verified. Still failing: Codex PATH precedence. Partly done: the provider model (ANALYSIS-123). Regression: ANALYSIS-124 |

### Recheck notes

**ANALYSIS-101 selector**

- `IsHarnessTask` (`LeftoverSweep.cs:17-41`) selects a task only when the definition's `Exec/Arguments` starts with `tray --data-dir` and the rest is a rooted path that is under `artifacts\field-acceptance`.
- The comparison uses `PathRelation.IsUnderOrEqual`: `GetFullPath`, link resolution, case-insensitive, and a separator-terminated prefix. So `..` is normalized away and `field-acceptance-elsewhere` does not match.
- Relative and `%VAR%` forms are not rooted, so they are rejected.
- A trailing extra argument still yields a path that is either under the base (then it is harness-owned) or not.
- A DTD raises `XmlException`, which is caught, and a failed `/xml` query is never deleted.
- The product task's arguments are exactly `tray --data-dir "<root>"` (`HookStartTaskSetup.cs:26`), and the forced-kill run in implementation-004 shows the real XML parses.
- Gaps that fail safe:
  - `RunSchtasks` reads `schtasks` output in the console code page. A non-ASCII data path could be mangled and then not selected.
  - Processes of a `--run-dir` outside `runs\` are not swept, although their task is.
  - When the user has no Bell Tower task, `schtasks /delete` still leaves an empty `\ZeusApps.BellTower\` folder behind. The product's own unregister removes it (`HookStartTaskSetup.cs:53-54`). This is unchanged from ANALYSIS-101; the folder option was dismissed.

**ANALYSIS-104: Codex shell resolution, still failing**

- The pinned source (`shell_detect.rs` at `rust-v0.164.0`, the `get_powershell_shell` and `get_shell_path` functions that native-investigation-003 cites) resolves in this order:
  1. `which("pwsh")` across the whole `PATH`;
  2. `C:\Program Files\PowerShell\7\pwsh.exe`;
  3. `which("powershell")`;
  4. `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`;
  5. `cmd.exe`.
- `CodexShell.Resolve` (`Harness/CodexShell.cs:34-52`) instead tries `pwsh.exe` and then `powershell.exe` within each `PATH` entry in turn.
- With the Windows PowerShell directory ahead of PowerShell 7 on `PATH` (the usual system order, because the PowerShell 7 installer appends its directory), the harness picks `powershell.exe` and Codex picks `pwsh.exe`. The test `ThePathPowerShellIsUsedBeforeTheFixedPaths` (`ProviderModelTests.cs:45-46`) pins the divergent result.
- On this machine both orders gave `pwsh.exe`, so the committed evidence and the U1 conclusion are unaffected: both editions fail to parse the 0.1.9 form.
- Fix:
  1. Search all of `PATH` for `pwsh.exe` and stop at the first allowed hit.
  2. Then check the fixed `pwsh` path.
  3. Then search all of `PATH` for `powershell.exe`.
  4. Then check the fixed Windows PowerShell path, then `cmd.exe`.
  5. Change the test so that `pwsh` wins even when Windows PowerShell comes first on `PATH`.
- The guard-imposed exception (a `pwsh` elsewhere on `PATH` is skipped) is documented, and it should name the edition Codex would have used.

**ANALYSIS-104: payloads, verified**

- `ProviderModel.Payload` takes the fixture's `payload` object and replaces only `session_id`, `cwd` and `transcript_path`, the last one pointing into the sandbox.
- The test compares the field sets for all four provider and event pairs and asserts that `turn_id` is absent. It passed.
- The `codex exec` setup probe payload is still built by hand (`FakeAgent.cs:56`). Setup only checks that the hook is delivered, so this is acceptable. Note it if 2.2 ever reads probe captures.
- The two-level `codex.exe` ancestry from the archived evidence is still not modeled. That was an observation in ANALYSIS-104, not one of its proposal steps.

**ANALYSIS-104: expected-failure semantics, verified, no masking**

- `stale/captures-default-shell` passes only when no capture was accepted and every one of the expected captures failed with a parser error (`UpgradeWithoutSetupScenario.cs:236`).
- Any other failure mode makes the check fail, and `RunReport` then counts it as a harness failure. Examples: a timeout, an ingress crash, or a machine with no PowerShell, where the resolver returns `cmd` and the captures succeed.
- `Passed` still requires every check, and `ExpectedFailures` counts only observed predictions (`RunReport.cs:31-33`).
- The markers in `IsParserError` are generic. The control that the parser error comes from the 0.1.9 command form, and not from the harness's PowerShell invocation, is the current-state run: 7 of 7 Codex captures pass through the same shell and hook path.

**ANALYSIS-104: stale-row labels and counts, verified**

- `captures-modeled-cmd-fallback` is labeled.
- The seeding, native-adoption and hook-start steps that use the fallback say so.
- The mixed row reports claude 7 of 7, codex 0 of 7, and the fallback 7 of 7.
- Every Codex capture row names its executable.

**ANALYSIS-104: ProviderModel (step 1 of 107), partly done**

- The settings path, owned-command lookup, runtime and fallback shell, agent arguments, payload builder and acceptance predicate are in the model, and `Instance.OwnedHookCommand` is gone.
- Provider-name branches remain in the scenario (ANALYSIS-123).

### `HookStartRoute.Shell` duplicate of the product constant

Acceptable as it is:

- The copy is a single string pinned by `TheHarnessCopyOfTheShellRouteNameMatchesTheProduct`. That test compiles against the internal `TrayProcess.RouteShell` and runs in the normal `dotnet test` suite, so any rename fails the build or the test.
- The alternatives widen the product surface for a test tool: a public constant in `BellTower.Protocol`, or `InternalsVisibleTo` for the harness.
- The task route's name, `HookStartTask.RouteTask`, is already public, so a failure label can use it directly.

## Assessment and limitations

The fix cycle closes most of the four findings.

Holding up:
- The shell route is required in the current state.
- The replacement tray inherits the sandbox environment.
- The exposure is stated honestly.
- The sweep selector cannot select the user's task or tray.
- Cleanup is isolated per instance and idempotent.
- The snapshot gates in both directions.
- Payloads come from the native fixtures.
- Expected failures are reported separately and cannot hide a different failure.
- The stale Codex rows say what they model.

Still open:
- Two warnings on interruption and concurrency:
  - the handlers race with the main thread, and a closed console runs no handler on .NET 10 (ANALYSIS-120);
  - the sweep destroys a concurrent run (ANALYSIS-121).
- One warning on readiness for 2.4 and 2.7: the exposure guard and the tray replacement are opt-in conventions, and the run-wide scope blocks `--scenario all` (ANALYSIS-122).
- The Codex `PATH` precedence differs from the pinned 0.164.0 source, and a test pins the difference (ANALYSIS-104 notes).
- Two suggestions: four doc and comment contradictions (ANALYSIS-123), and raw ANSI escapes in the evidence (ANALYSIS-124).

No product file needs to change for any of these.

Parity:
- Every recheck covered Claude Code, Codex and the mixed rows.
- The shell-order and probe-payload notes are Codex-only by nature.
- The shared-backend guard concerns Codex, because Claude Code has no shared backend.

Not performed:
- A build or harness run: QA runs the harness concurrently, and the assignment forbids a run here.
- A real Ctrl+C or console-close probe.
- The 2.7 shared-backend path.

The console-close conclusion rests on the .NET 10 breaking-change documentation fetched through Context7, not on a probe.

External read: the Codex `shell_detect.rs` source at tag `rust-v0.164.0` was fetched read-only from GitHub. No project or user data was sent.

Writes: none. The report write was refused, so this text is returned inline for the orchestrator to save. The review identity is unchanged. The orchestrator owns triage and ledger status.
