> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125) after the reviewer's write was refused.
> Reviewer: ANALYSIS-008 code-analyzer subagent; client Claude Code 2.1.332 (delegated subagent, role default); model claude-opus-5-5.
> Refusal message as reported by the reviewer: "Subagents should return findings as text, not write report files." Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-008

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.332 / delegated subagent, role default / `claude-opus-5-5`
- Date: 2026-11-25, 14:43Z to 14:50Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-008. This is the final recheck of task 2.1 after fix cycle 2 (developer DEV-005).
- Task and requirement references:
  - [tasks.md](../tasks.md) 2.1, plus fitness for 2.3, 2.4 and 2.7
  - [implementation-005.md](../evidence/implementation-005.md), [harness-2.1.md](../evidence/harness-2.1.md)
- Scope:
  - new files: `tools/BellTower.FieldAcceptance/Harness/{RunAbort,LockedList,TrayReplacement}.cs` and `Isolation/RunLock.cs`
  - rewritten files: `Isolation/RealEnvironmentExposure.cs` and `Harness/CodexShell.cs`
  - modified files: `Program.cs`, `Harness/{Instance,AgentSession,ProviderModel,TowerReader}.cs`, `Scenarios/{RunContext,UpgradeWithoutSetupScenario}.cs`, `Evidence/RunReport.cs` and `scripts/field-acceptance.ps1`
  - tests: `tests/BellTower.Tests/FieldAcceptance/{RunControlTests,ProviderModelTests,LeftoverSweepTests}.cs`
  - docs: `docs/distribution-validation.md:27-29` and `docs/development.md:65`
- Neighbors read:
  - `src/BellTower.Protocol/HookTrayStart.cs:95-148`, the ingress call site `src/BellTower.Ingress/Program.cs:63-66`, `src/BellTower/Setup/ProviderHookProbe.cs:45-66`
  - `Isolation/LeftoverSweep.cs:76-98`, `Harness/FakeAgent.cs:50-60`
  - Codex `codex-rs/shell-command/src/shell_detect.rs` at tag `rust-v0.164.0`, fetched read-only with `gh api`
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence` printed `Summary: head=02d4f56 baseline=02d4f56 files=49 diff=42172a159ba4 untracked=43/b78f69b87af3`. This matches the assignment.
- Before/after identity: the same command at 14:50Z printed the same summary line. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-007.md](analysis-007.md). Rechecked: ANALYSIS-120, ANALYSIS-121, ANALYSIS-122, the ANALYSIS-104 shell-order residual, and the parts of ANALYSIS-123 tied to them.

The harness files are untracked. Their line numbers refer to the working tree captured by the identity above.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 120: stop request | Read every place that starts work: `Instance.Create`, `Instance.Run`, `AgentSession.Start`, `AgentSession.Hook`, `TowerReader.WaitFor`, `TowerReader.WaitForSessions` (through `List`), `TrayReplacement` (through its lambdas) and `Instance.WithProcessEnvironment` | Every process start checks the stop request, except the in-process update call. After a stop, every later step throws `RunInterruptedException`. The final cleanup sweeps anything started in the window | Verified. Residual: ANALYSIS-140 |
| 120: disposal exemption | Read `Instance.cleaning` (`[ThreadStatic]`, `Instance.cs:29-31,89-92,144-148,236-251`) | Only the thread inside `Dispose` may run processes after a stop. The main thread is not exempt while the console-close handler disposes. `Dispose` is not re-entrant, so the reset in its `finally` block is safe | Verified |
| 120: console-close handler | Read `Program.cs:137-147,236-271` | The handler takes the same `gate` as the main thread's cleanup and waits on `finished` only after it has released `gate`, so the two cannot deadlock. `finished` is set in a `finally` block on every path through the second `try` block. The 4 s wait is best effort, as documented | Verified |
| 120: evidence file | Traced every `Finish` caller | `Finish` is called only on the main thread. The interrupted branch passes `interruptedOptions` without `evidence-out` (`Program.cs:122-123,202-207`). No handler calls `Finish` | Verified: the committed evidence file is never written on the interrupted path |
| 120: shared collections | Read `LockedList`, `RunReport.Checks`, `RunContext.Instances`, `Instance.Sessions` and the remaining plain lists (`Notes`, `CommandLog`) | Reads take a snapshot under the lock. `Notes` is only touched by the main thread. `CommandLog` is written by the handler thread only while the main thread is refused before its own write | Verified |
| 121: mutex | Read `RunLock`, its call site (`Program.cs:78`) and both lock tests | `WaitOne(0)` refuses while another run holds the lock. An `AbandonedMutexException` from `WaitOne` grants ownership. If a killed run's mutex has no other handle, it is destroyed and the next run creates a fresh one. The lock is released on the acquiring thread by `using`. Fake agents, which are copies of the harness executable, return before the lock | Verified. Scope wording: ANALYSIS-141 |
| 121: script probe | Read `scripts/field-acceptance.ps1:29-35` | `TryOpenExisting` succeeds only while a run holds a handle. The probe runs before both the candidate build and the harness build. A run that starts after the probe can still overlap with the build; the harness's own lock still holds | Verified (small race window, accepted) |
| 121: reasoning against owner records | Read `ProductionSnapshot.Diff(before, after, ownTasks)` (`Program.cs:178-183`) | Only the run's own tasks are excluded, so another run's sandbox task would gate as a production change. A precise sweep would not fix that | Sound |
| 122: marks | Read `RealEnvironmentExposure`, `Instance.Expose/EndTrays`, `AgentSession.Start:47-50` and `Hook:93-120` | The marks are set automatically for every session start and every `AgentSession.Hook`. They are per instance, sticky and taken under a lock. A combination of both marks ends the trays (`KillProcessesUnder(Layout.Install)`) and propagates as an `IsolationException`, which the scenario loop records | Verified for sessions and session hooks. A hook run through `Instance.Run` is not observed: ANALYSIS-142 |
| 122: hook-start detection race | Read `HookTrayStart.TryStart` and the ingress call site | The ingress calls `TryStart` synchronously before it exits, and the record is written before `TryStart` returns. The harness reads the record before and after the hook, inside the session's lock. Records differ by their timestamp. The setup probe cannot start a tray: it uses a temporary data directory with no launcher record (`ProviderHookProbe.cs:57`), so `TryStart` returns `NoLauncher` | Race-free enough. Two fail-open gaps remain, both narrow (notes) |
| 122: replacement | Read `TrayReplacement` and the scenario (`UpgradeWithoutSetupScenario.cs:368-379`), plus the committed rows (`harness-2.1.md:84-87`) | The check passes only when the tray stopped after `exit` and a tray is running after `start --silent`. The restart sentence appears only after a restart. A tray that did not stop fails the check, even if it is still running | Verified: the replacement is real evidence now |
| 122: `WaitFor` | Read every caller | It returns false on timeout and throws on a stop request. The callers that ignore the return value (`:59, :101, :111, :121, :345, :358, :389`) all re-observe the state in the predicate or the next read, except `:101` (note) | Verified |
| 122: `--scenario all` | Read `RunContext` and the scenario loop | The marks are per instance. A refusal fails `isolation-guard` for that scenario only, and the loop continues | Workable |
| 104: shell order | Compared `CodexShell.Resolve` with `shell_detect.rs` at `rust-v0.164.0`: `get_powershell_shell`, `get_shell_path` (`get_user_shell_path` is `None` on Windows), `PWSH_FALLBACK_PATHS`, `POWERSHELL_FALLBACK_PATHS` and `ultimate_fallback_shell` | Both use the same order: `which("pwsh")`, the fixed `pwsh`, `which("powershell")`, the fixed `powershell`, then `cmd.exe` | Verified |
| 104: guard-exception note | Read `CodexShell.cs:39-98` and `UpgradeWithoutSetupScenario.cs:150-152` | A skipped PATH hit sets `Note` with the edition and no path, and the run notes include it | Verified |
| 123: docs | Compared `docs/distribution-validation.md:27-29`, `docs/development.md:65` and the comments in `CodexShell`, `ProviderModel` and `Program.cs` with the code | All four earlier contradictions are fixed. A new mismatch on lock scope and exit code: ANALYSIS-141 | Verified for the four points |
| Architecture / duplication | Read the new types against the harness layering | Run control (`RunAbort`, `RunLock`), exposure (`Isolation`) and replacement (`Harness`) are single-purpose, and none duplicates policy. The mutex name is repeated once, as a literal in the script, which is acceptable. No product file changed | No drift |
| Parity | Read the per-provider paths | The hook-start mark, the replacement and interrupt handling apply to Claude Code, Codex and the mixed rows alike. The shared-backend mark is Codex-only by nature | No gap |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~FieldAcceptance"` | 67 passed, 0 failed. The test binary (14:34:10Z) is newer than the newest harness or test source (14:33:24Z) | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <every .cs file under tools/BellTower.FieldAcceptance and tests/BellTower.Tests/FieldAcceptance, excluding bin and obj>` | exit code 0 | passed |
| Build, harness run, signals | Not run | QA runs the harness concurrently, and the assignment forbids a build or run. The developer's Ctrl+C, console-close, forced-kill and overlap probes are taken from implementation-005, not repeated | not performed (deliberate) |

## Findings

### ANALYSIS-140: After a stop, a few failure paths still record a misleading `scenario-completed` failure

- Severity: suggestion
- Related task / requirement: 2.1 interrupt handling (residual of ANALYSIS-120)
- Locations:
  - `tools/BellTower.FieldAcceptance/Program.cs:170-173`: the generic catch records a failing check whether or not a stop was requested
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:73-77`: a session killed by Ctrl+C before it prints `ready` becomes `IOException("did not become ready")`
  - `AgentSession.cs:104`: only `IOException` is mapped to the stop. On the console-close path the handler thread disposes the session's process and streams while the main thread may be inside `Hook`, which can raise `ObjectDisposedException` or `InvalidOperationException` instead
  - `tools/BellTower.FieldAcceptance/Harness/Instance.cs:171-198`: `WithProcessEnvironment`, which runs the update transaction and its helper process, does not check the stop request
- Observation:
  - The developer states that a child that was ended "becomes the stop and not a misleading failed check". That holds for `Run` and `Hook` when they fail with an `IOException`. It does not hold for the paths above.
  - In those cases an interrupted `result.json` holds a `scenario-completed` failure next to `run/interrupted`. With `--scenario all`, the loop then also enters the next scenario, which is refused at `CreateInstance`.
  - The effect is cosmetic. The run is already failing and labeled interrupted, no committed evidence is written, and the final cleanup still sweeps any process started in the window.
- Proposed improvement and benefit:
  - In the scenario loop, add `catch (Exception) when (context.Abort.Requested) { break; }` before the `IsolationException` and generic catches. This one line covers every such path.
  - Optionally, call `Abort.ThrowIfRequested()` at the start of `WithProcessEnvironment`.
- Behavior-preservation risks:
  - An `IsolationException` raised while not interrupted must still be recorded.
  - With the filter placed first, an isolation refusal that coincides with a stop is not recorded. That is acceptable, because the refusal also ended the trays, and the run is already failing.
- Evidence type: source observation
- Verification / measurement plan: a unit test of the loop with a scenario that throws `IOException` after a stop request. Expect only `run/interrupted`.
- Relationship: residual of ANALYSIS-120

### ANALYSIS-141: The run lock is per logon session, while the docs and messages say machine-wide; the script's refusal is not exit code 2

- Severity: suggestion
- Related task / requirement: 2.1 concurrency (residual of ANALYSIS-121), and the ANALYSIS-123 docs
- Locations:
  - `tools/BellTower.FieldAcceptance/Isolation/RunLock.cs:11` (`Local\BellTower.FieldAcceptance`)
  - `tools/BellTower.FieldAcceptance/Program.cs:78` ("active on this machine")
  - `scripts/field-acceptance.ps1:32-34` ("active on this machine", then `throw`)
  - `docs/development.md:65` ("holds a machine-wide lock")
  - `docs/distribution-validation.md:28` ("a second run (and `scripts/field-acceptance.ps1`, before it builds) stops at once with exit code 2")
- Observation:
  - `Local\` objects are scoped to one Windows logon session. Two runs by the same user in two sessions, for example a console session and an RDP session (task 4.1 mentions RDP), are not refused.
  - Such runs still share the user's Task Scheduler folder. Each run's production comparison would see the other run's sandbox task.
  - The script refuses with `throw`. That exits 1 under `pwsh -File`, not 2.
- Proposed improvement and benefit: pick one of these.
  - Say "per logon session" in the two docs and in both messages, and state the script's exit code.
  - Or use a `Global\` name in `RunLock` and the script. Before adopting it, verify that a standard user can create it.
- Behavior-preservation risks: none for the single-session flow that every recorded run used.
- Evidence type: source observation; `Local\` semantics are Windows kernel object namespace behavior, not probed here
- Verification / measurement plan: text review. If `Global\` is chosen, run the existing lock tests with that prefix.
- Relationship: ANALYSIS-121, ANALYSIS-123

### ANALYSIS-142: Hook-start detection covers only `AgentSession.Hook`, not a hook run through `Instance.Run`

- Severity: suggestion
- Related task / requirement: fitness of the ANALYSIS-122 mechanism for 2.3 (at least 100 Git Bash hooks), 2.4 and 2.7
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:95,117-118`: the only `NoteHook` caller
  - `tools/BellTower.FieldAcceptance/Harness/Instance.cs:86-150`: `Run` is public and allows Git Bash, PowerShell and `cmd` with a hook command and standard input
  - `docs/distribution-validation.md:27` ("every hook compares the recorded tray start attempt")
- Observation:
  - A later scenario can run the installed hook command directly with `instance.Run(Tools.GitBash, ["-c", command], stdin: payload)`. The guard allows this, and a load scenario in 2.3 might do it to avoid a session round trip.
  - If the tray is stopped, such a hook can start a real-environment tray that is never marked.
  - The exposure matters only when the same instance also has a shared-backend Codex session, which today's scenario never has. So this is not a current defect. It is a gap in the "mechanism, not convention" claim.
- Proposed improvement and benefit: pick one of these.
  - Move the before and after `HookTrayStart.LastAttempt` comparison into `Instance.Run`, which every process start goes through, and keep the call in `Hook` or drop it. The cost is one small file read per process.
  - Or state in the `Instance` and `Run` comments and in the docs that hooks must go through `AgentSession.Hook`.
- Behavior-preservation risks:
  - Inside `Run`, skip the comparison while `cleaning`.
  - Do not mark when `start --silent` runs. It writes no attempt record, so the comparison stays unchanged for it.
- Evidence type: source observation; the 2.3 usage is a plausible future use, not existing code
- Verification / measurement plan: a unit test in which a hook through `Instance.Run` (or a stub) changes the record to `Started` and marks the instance.
- Relationship: residual of ANALYSIS-122

## Re-verification

All rechecks ran against the target `head=02d4f56 baseline=02d4f56 files=49 diff=42172a159ba4 untracked=43/b78f69b87af3`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-120 | **Stop request:** Ctrl+C now only signals: it sets `Cancel`, the first reason wins, and it ends the run's sandbox processes. `KillProcessesUnder` kills each process tree, so the hook shells under a fake session end too. The main thread refuses new work at every process start and wait, except the in-process update call (ANALYSIS-140). **Disposal exemption:** the `[ThreadStatic]` exemption is correct: per thread, set only inside `Dispose`, not re-entrant. **Console close:** the handler shares `gate` with the main thread's cleanup and waits on `finished` only after releasing it, so the two cannot deadlock. The 4 s wait plus the handler's own cleanup can exceed the time Windows allows before it ends the process. That is documented as best effort, and the preflight sweep is the fallback. **Evidence file:** never written from the interrupted path. `Finish` runs only on the main thread, and `interruptedOptions` drops `evidence-out`. **Report:** `LockedList` removes the serialization race. The developer's two real Ctrl+C runs and two window-close runs are recorded in implementation-005 and were not repeated here | Verified. Residual suggestion: ANALYSIS-140 |
| ANALYSIS-121 | **Mutex:** the mutex is taken before any sandbox, sweep or snapshot and held for the whole run. A held lock refuses with exit code 2. An abandoned mutex is acquired, and a destroyed one is recreated, so a killed run never locks later runs out. Both cases are covered by `ALockAbandonedByAKilledRunIsAcquiredByTheNextRun` and the second-acquisition test, with test-unique names so they do not collide with QA's live run. **Script:** it probes before both builds. **Owner records:** the reasoning against them is sound, because the diff excludes only the run's own tasks | Verified. Residual suggestion on scope wording: ANALYSIS-141 |
| ANALYSIS-122 | **Marks:** automatic, per instance, sticky and thread-safe. Sessions are marked in `AgentSession.Start` before the process starts, so a refused plain Codex never runs. Hooks are marked after every `AgentSession.Hook`. **Detection:** race-free for the product's synchronous ingress record. **Combination:** ends the instance's install-tree processes (tray, launcher, update helper) at once and records `isolation-guard`. **Replacement:** observed in both steps, and the restart sentence is conditional. Committed rows: `current/hook-started-tray-replaced` passes for claude and codex, and `stale/tray-restored` passes for both. **`WaitFor`:** reports timeouts. **`--scenario all`:** workable, and the docs name the instances that must stay apart | Verified. Residual suggestion on coverage: ANALYSIS-142 |
| ANALYSIS-104 (shell-order residual) | `CodexShell.Resolve` matches `shell_detect.rs` at `rust-v0.164.0` step for step. The rewritten tests pin that PowerShell 7 wins even when Windows PowerShell comes first on `PATH`, and that the fixed `pwsh` path beats a `powershell.exe` on `PATH`. The guard-exception note names the edition, contains no path, and appears in the run notes | Verified |
| ANALYSIS-123 (tied parts) | All four points are fixed in the docs and comments: PowerShell 7 is on the allow-list; "avoid provider-name branches" names the remaining branches; the shell order is stated correctly; console close is described as best effort with the `SIGHUP` route. Remaining branches stay by decision; ANALYSIS-124 is deferred | Verified. A new wording mismatch on the lock scope is ANALYSIS-141 |

### Notes (no finding)

- **Detection fail-open cases.** Two gaps remain, both narrow. Neither matters without a shared-backend Codex in the same instance.
  - `TryStart` swallows a failed record write (`HookTrayStart.cs:137-141`).
  - A hook killed by the fake agent's timeout after the tray start, but before the record is written, leaves no mark.
- **`UpgradeWithoutSetupScenario.cs:101` and `:116`.** The timeout from the pre-update `exit` wait is ignored. The `upgrade-activated` detail then states "The tray was stopped" before it reports the observed tray state, and the predicate does not include the stop. This is narrative only for 2.1. The "never started during an update" assertion belongs to 2.7, which should check it explicitly.
- **`TrayReplacementResult.Detail`.** When `exit` did not stop the tray, the detail says the processes "were ended directly" without observing that. The check fails in that case anyway.
- **`which` versus the harness.** The Rust `which` crate also matches `PATHEXT` variants such as `pwsh.cmd`. The harness looks only for `.exe`, and the guard would refuse a `.cmd` anyway. The difference is not visible on standard installations.

## Assessment and limitations

The fix cycle closes the three warnings and the shell-order residual.

- **Interrupts:** the main thread now owns cleanup, the production comparison and the result file; handlers only signal. The committed evidence path is never written from an interrupted run.
- **Concurrency:** one run at a time is enforced, not just documented.
- **Exposure guard:** a per-instance mechanism for sessions and session hooks. The tray replacement is observed rather than asserted.
- **Codex shell:** the order matches the pinned 0.164.0 source.

What remains is three suggestions. None of them breaks the acceptance of 2.1 or production safety.
- ANALYSIS-140: misleading extra failures in an interrupted `result.json`.
- ANALYSIS-141: the lock scope wording and the script's exit code.
- ANALYSIS-142: hook-start detection is not observed for a hook run through `Instance.Run`.

No product file needs to change.

Parity: every recheck covered Claude Code, Codex and the mixed rows. The shell order and the shared-backend mark are Codex-only by nature, because Claude Code has no shared backend.

Not performed:
- a build, a harness run, and real Ctrl+C or console-close probes (QA runs the harness concurrently, and the assignment forbids a run);
- logoff and shutdown signals;
- a cross-session lock probe.

External read: `shell_detect.rs` at `rust-v0.164.0` was fetched read-only from the public Codex repository with `gh api`. No project or user data was sent.

Writes: none. The report write was refused, so this text is returned inline for the orchestrator to save to `D:\Code\ZeusApps.BellTower\openspec\changes\validate-next-release-field-acceptance\reports\analysis-008.md`. The review identity is unchanged. The orchestrator owns triage and ledger status.
