> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125) after the reviewer's write was refused.
> Reviewer: ANALYSIS-013 code-analyzer subagent; client Claude Code (delegated subagent, auto mode); model claude-opus-5-5.
> Refusal message as reported for earlier reports: "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-013 (task 2.4, DEV-010)

## Review target

- Reviewer / role: code-analyzer subagent for orchestrator session claude-release-gate-20261125 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (the version is not visible to the subagent), subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-26T11:14Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance`, ANALYSIS-013
- Task and requirement references: task 2.4 ([tasks.md](../tasks.md)); evidence [implementation-010](../evidence/implementation-010.md), [harness-2.4](../evidence/harness-2.4.md), [coverage-ledger](../evidence/coverage-ledger.md) rows F2, F4, U1; [findings.md](findings.md) entries of 2026-11-26 (user decision: plain `codex --no-daemon` loss documented, not fixed)
- Scope and relevant neighboring paths: `tools/BellTower.FieldAcceptance/Scenarios/{FaultsAndRollbackScenario,FaultGroupRun,FiveConversationsScenario,PackagedHookBenchmarkScenario,UpgradeWithoutSetupScenario}.cs`; `Harness/{ProcessEnvironmentSwap,ConversationFixture,SpoolInjection,InstallSwitch,StateView,IngressImage,ProviderModel,Instance,AgentSession,FakeAgent,ConversationSurfaces,TowerReader,TrayReplacement}.cs`; `Isolation/RealEnvironmentExposure.cs`; `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs`; product neighbors `src/BellTower/{Core/NativeAdoption,Core/SessionReducer,Hosts/AttachmentCapture,Providers/CaptureConsumer,Providers/AgentModeClassifier,IO/AttachmentStore,IO/ProcessTable,Integration/BellTower.psm1,Sessions/AgentLauncher,Sessions/CodexRuntimeHost,Hosts/CodexRuntimeProcess}`, `src/BellTower.Ingress/Program.cs`, and the same files at `v0.1.9` via `git show`
- Baseline commit: 02d4f56
- Implementation identity (before): `head=02d4f56 baseline=02d4f56 files=95 diff=d7b5089c18d0 untracked=77/c177af0ae6a9` (tracked diff SHA-256 `d7b5089c18d05c0220558c5c17ab258b510de90532c2ae0a561e670fe0a88911`), matching the assignment
- Before/after identity: after the analysis the same command printed `head=02d4f56 baseline=02d4f56 files=95 diff=d7b5089c18d0 untracked=77/c177af0ae6a9`. The identity is unchanged, and nothing was written: the report write was refused, so this text is inline.
- Prior reports / rechecked findings: [analysis-007](analysis-007.md) ANALYSIS-122; [analysis-009](analysis-009.md) ANALYSIS-162 (note), -170, -171; [analysis-010](analysis-010.md) ANALYSIS-186 (context only)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Launch-kind reachability table | Traced each kind through the 0.1.9 ingress (`git show v0.1.9:src/BellTower.Ingress/Program.cs:33-45`, `v0.1.9:.../Hosts/AttachmentCapture.cs:30-31`, `v0.1.9:.../Providers/CaptureConsumer.cs:61-62`) and the candidate (`Hosts/AttachmentCapture.cs:110-111`, `Core/NativeAdoption.cs:101-131,161-205`, `Providers/CaptureConsumer.cs:63-75`, `Core/SessionReducer.cs:37-41`, `BellTower.Protocol/CaptureProtocol.cs:81`) | The five modeled rows are correct by source. The `--remote` owned runtime, which is the default Bell Tower Codex profile launch, is unmodeled, and the rollback direction loses its hooks | ANALYSIS-240 |
| Product file:line spot checks | Read every cited line | Correct: `AttachmentCapture.cs:110`, `NativeAdoption.cs:101, 161-170, 178-205, 272, 275`, `AgentModeClassifier.cs:93, 120`, `SessionReducer.cs:37` (0.1.9 and candidate; the causal reducer replays through `ApplyUnordered`, `CausalSessionReducer.cs:39,51`), `CaptureConsumer.cs:71-75`, `psm1:40, 55-58, 105`, `AgentLauncher.cs:32-33`, v0.1.9 `Program.cs:33-41`, v0.1.9 `AttachmentCapture.cs:30-31`. `psm1` is unchanged since v0.1.9 | Claims verified. The incomplete one is `psm1:55-58` (`:97-106` then removes `--no-daemon` and keeps `BELLTOWER_DEDICATED=1`), see ANALYSIS-240 |
| Null-process checks | Traced ghost/heal ids per provider | For Codex the repair hook is dropped by the attachment-id rule, not only left unrepaired. Plain-launch null-process startups (a random id per hook) are unmodeled | ANALYSIS-241, -242 |
| Non-vacuity of the per-kind checks | Read the predicates at `FaultGroupRun.cs:313, 617` | The expected-failure predicates accept any cause of non-delivery, and each wastes a full timeout | ANALYSIS-243 |
| Fault, spool and rollback checks | Read `FaultGroupRun.cs:389-510, 531-715`, `SpoolInjection.cs`, `StateView.cs`, `InstallSwitch.cs` | The lost, late, duplicate, downtime, shuffle and retained checks assert concrete state (journal counts, operation sets, spool counts). `src/BellTower/Updates` and `src/BellTower.Distribution` are unchanged since v0.1.9, so the in-process `UpdateTransaction` equals 0.1.9's | Non-vacuous. Under-load window and reupgrade gaps: ANALYSIS-247, -248 |
| Exposure enforcement (ANALYSIS-122) | `AgentSession.cs:58,109-132`, `RealEnvironmentExposure.cs`, `FaultGroupRun.cs:515-527, 731-733` | Mechanism-based and checked. The evidence text is wrong for Codex and mixed | Verified; ANALYSIS-244 |
| `ProcessEnvironmentSwap` (ANALYSIS-171) | `ProcessEnvironmentSwap.cs`, `Instance.cs:47,98`, `AgentSession.cs:43`, `PackagedHookBenchmarkScenario.cs:53-63`, `FaultInjectionTests.cs:15-72`, xUnit 2.9.3 with no parallelization override | Enforced and correct. The unit tests clear the whole test-host environment while other test classes run in parallel, and the call sites are not tested | Verified; ANALYSIS-245 |
| Duplication / complexity / reuse for 2.5-2.7 | Compared `FaultGroupRun` with `UpgradeWithoutSetupScenario.cs:99-113, 340-380` and `TrayReplacement.cs` | The hook driver, tray downtime, update run and expected-failure composition are private or duplicated. `legacyImage` is a hand-maintained flag | ANALYSIS-246 |
| ANALYSIS-170 residual | `FiveConversationsScenario.cs:54,173`, `PackagedHookBenchmarkScenario.cs:248-249` | Fixture and version binding resolved; two provider-name branches remain | Re-verification table |
| Unit tests | `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~FieldAcceptance` | 102 passed, 0 failed. Build outputs are newer than every harness source | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 18 task 2.4 .cs files>` | exit 0 | passed |
| Harness run | not run (assignment) | n/a | not performed |

## Findings

### ANALYSIS-240: The default Bell Tower Codex launch (owned `--remote` runtime) is unmodeled, and by source a rolled-back 0.1.9 drops its candidate-image hooks; "not reachable through Bell Tower launches" is wrong

- Severity: warning
- Related task / requirement: 2.4 (rollback while the hooks reference the candidate image), 4.4 release notes, ledger U1; parity and honesty rules
- Locations:
  - `src/BellTower/Integration/BellTower.psm1:52-58, 97-106` (unchanged since v0.1.9). `$standalone` is computed before the `--no-daemon` prepend. Without a user `--no-daemon`, the launch prepares the owned runtime and replaces the arguments with `--remote <endpoint>` (dropping `--no-daemon`), while `BELLTOWER_DEDICATED=1` (line 58) and `BELLTOWER_ATTACHMENT` (line 40) stay set.
  - `src/BellTower/Hosts/CodexRuntimeProcess.cs:16-17` (`CreateProcess` with a null environment, so the manager inherits the launch variables), `src/BellTower/Sessions/CodexRuntimeHost.cs:82-111` (the `app-server --listen` backend inherits them). Hooks run under that backend.
  - `src/BellTower/Diagnostics/CapabilityMatrix.cs:67, 115` ("the Codex profile (Start-Codex, a dedicated runtime)"): this is the primary supported Bell Tower Codex route.
  - 0.1.9 ingress: `git show v0.1.9:src/BellTower.Ingress/Program.cs:33`. `DEDICATED=1` keeps the managed `att-` id.
  - Candidate: `AgentModeClassifier.cs:93-95` (`app-server` is the shared backend). `IO/ProcessTable.cs:55-60` and `NativeAdoption.cs:62-74`: the backend's parent is the manager, not a Codex client, so the mode becomes `unknown`. `NativeAdoption.cs:106-128` then emits the provisional shared-shaped wire (`Id = codex-session-<hash>`, the managed id only in `Evidence.Candidate`). `CaptureProtocol.cs:81` makes the observation carry the same wire id. `Providers/CaptureConsumer.cs:63-75` promotes it back to `att-` (via `Reconcile` with the runtime record, `NativeAdoption.cs:195-202`). Product test `tests/BellTower.Tests/Hosts/NativeOwnedRuntimeProcessTests.cs:225,236` pins this: the session is on the managed id and `Evidence.Mode == Unknown`.
  - 0.1.9 consumer: `git show v0.1.9:src/BellTower/Providers/CaptureConsumer.cs:61-62` has no `Promote` and uses the wire id.
  - Claims: [implementation-010](../evidence/implementation-010.md) lines 66 and 105 ("not reachable through Bell Tower's own managed launches"; "not a rollback-specific hazard"); `docs/distribution-validation.md:36` ("Bell Tower launched and shared-backend Codex and every Claude Code conversation are unaffected"); [coverage-ledger](../evidence/coverage-ledger.md) row U1 (line 77); `tools/BellTower.FieldAcceptance/Harness/ProviderModel.cs:44` ("a Codex launch gets `--no-daemon` and the marker together").
- Observation:
  - Under 0.1.9 an owned-runtime conversation registers under `att-<id>`. After `setup --host-bridge --apply` the candidate promotes and verifies it to the same `att-` id, so the upgrade direction is followed (as DEV-010 says).
  - After a rollback with the hooks still naming the candidate image, every capture arrives at 0.1.9 as `codex-session-<hash>`. 0.1.9 cannot promote it, so `SessionReducer.cs:37-41` ignores every non-registration event of that conversation. A `SessionStart` would rebind the session to the shared id and clear its waits.
  - Unlike the documented plain `--no-daemon` case, this also hits conversations the candidate registered after the setup rerun. It is a rollback-specific loss on the main Bell Tower Codex path.
  - The same provisional path is taken by any managed Codex launch whose command line classifies `unknown` (an option outside `AgentModeClassifier.cs:34-45`).
  - The harness's "managed" Codex kind (`ProviderModel.cs:219`) models only the `--no-daemon` fallback (a direct mode, never provisional), so `rollback/candidate-hooks-applied-by-0.1.9/wait-managed` cannot see this.
  - Recovery is the same as documented: 0.1.9's own setup rerun restores the 0.1.9 image, which emits the `att-` id. But the scope statement, the user-decision premise ("not reachable through Bell Tower launched ... Codex") and the planned 4.4 release-note wording are wrong.
- Proposed improvement and benefit:
  1. Add a `LaunchKind` "owned-runtime" for Codex: a fake `codex.exe app-server --listen ...` backend started by a fake manager process, `BELLTOWER_ATTACHMENT` plus `BELLTOWER_DEDICATED=1`, and a `CodexRuntimeStore` record naming the manager and a live shell. It needs no WebSocket: `Reconcile` reads only the record and process identities.
  2. Run it through all per-kind checks. By source, the rollback check is a third expected failure (or a product fix: emit the promoted id directly when a direct candidate exists, or keep the 0.1.9-compatible id on the wire).
  3. Correct `docs/distribution-validation.md`, ledger U1, `ProviderModel.cs:44` and the 4.4 release-note scope.
  4. Return the corrected scope to the user, since the 2026-11-26 decision was made on the narrower premise.
- Behavior-preservation risks: none for the harness. A product change to the wire id must keep fail-closed behavior for unverified captures (a non-promoting consumer must still not see a direct claim).
- Evidence type: source observation (plus the cited product test). Not run in an installed harness.
- Verification / measurement plan: the new kind in `faults-and-rollback`, Codex and mixed. By source, `rollback/candidate-hooks-applied-by-0.1.9/wait-owned` shows the loss, and `rollback/setup-rerun-restores-delivery/wait-owned` passes. A unit test can pin the kind's environment and arguments to `psm1:52-58,97-106`.
- Relationship: extends DEV-010 findings 1 and 2. Reachability caveat in implementation-010 line 66.

### ANALYSIS-241: The Codex null-process "unavailable" pass hides that every later hook of that conversation is dropped

- Severity: warning
- Related task / requirement: 2.4 ("repaired/unavailable liveness"), ledger F4, 5.2 parity audit
- Locations:
  - `tools/BellTower.FieldAcceptance/Scenarios/FaultGroupRun.cs:369-385` (repair: one `UserPromptSubmit` from the identified process; outcome judged only on attachment process and health)
  - `Harness/ProviderModel.cs:33-39, 216`
  - Product: `NativeAdoption.cs:101,130` (the first hook has no process, so a Codex `unknown` capture fails closed to `Shared`, which is `codex-session-<hash>`); `AttachmentCapture.cs:110` (the later hook keeps `att-` and is direct); `SessionReducer.cs:37-41`; `Providers/HookAdapter.cs:27-28` (`UserPromptSubmit` is `Prompt`, not `Register`)
- Observation:
  - The session registers under `codex-session-<hash>`, and the identified process's hooks carry `att-<id>`. The reducer therefore ignores them: the conversation is not "honestly unavailable but tracked". Its prompts, permission requests and completions are lost until a `SessionStart`.
  - Claude keeps one id (`att-`) on both hooks, so it is repaired.
  - The check passes whether the later hook was dropped or applied, because no tool hook is ever sent to a heal conversation.
  - The developer and orchestrator framing ("fail-closed policy outcome" for the 5.2 audit) omits the attention loss. This is the same mechanism as the documented limitation.
- Proposed improvement and benefit:
  - Send a `PreToolUse` and `PermissionRequest` from the identified process to each heal conversation.
  - Assert a wait for the repaired provider, and record the Codex outcome explicitly: the event is in the journal (`StateView.LastEventAttachmentId`) but has no effect. Make it an expected failure or a finding for decision, not a pass.
  - Correct the F4 and 5.2 wording.
- Behavior-preservation risks: none (harness-only).
- Evidence type: source observation
- Verification / measurement plan: a run shows Claude heal with one wait. Codex heal then shows journal growth with `Waits == 0` and a differing last-event id.
- Relationship: new. Same mechanism as DEV-010 findings 1 and 2.

### ANALYSIS-242: Null-process startup is modeled only for Bell Tower launches; for a plain launch every hook gets a fresh random attachment id

- Severity: warning
- Related task / requirement: 2.4 (null-process startup, both providers), 2.7 (plain `claude`, `codex --no-daemon`)
- Locations:
  - `FaultGroupRun.cs:49, 139` (ghost and heal always take `LaunchKinds[0]`, the managed kind)
  - `src/BellTower/Hosts/AttachmentCapture.cs:110-111` (no env id and no identified agent gives `Guid.NewGuid()`; the same at `v0.1.9:.../AttachmentCapture.cs:30-31`)
  - `NativeAdoption.cs:101-104` (Claude `unknown` is direct and keeps that id)
  - `IO/AttachmentStore.cs:40-61` (no session-based reuse)
- Observation:
  - For a plain Claude conversation whose hooks see no `claude.exe` ancestor, each hook carries a new GUID. Every non-registration event then misses the session's attachment and is ignored, and one attachment file is written per hook.
  - Codex is unaffected for this sub-case, because `Shared` replaces the id with the stable session hash. Its later identified hooks still diverge, as in ANALYSIS-241.
  - The harness never exercises the plain kind, so the outcome is untested for the most common Claude start. This is pre-existing in 0.1.9.
- Proposed improvement and benefit:
  - Add a plain-kind ghost per provider, or note it as unmodeled in the 2.4 evidence and the matrix.
  - If observed, triage the product rule (for example, derive a stable id from provider and session when no process is found, as `Shared` does).
- Behavior-preservation risks: a product change alters attachment identity for null-process conversations and needs migration consideration.
- Evidence type: source observation. Field reachability (how often the ancestry walk misses the agent) is unknown.
- Verification / measurement plan: a plain unidentified Claude conversation sends `SessionStart`, `PreToolUse` and `PermissionRequest`. By source, no wait appears, and the attachments directory gains one file per hook.
- Relationship: new

### ANALYSIS-243: The expected-failure checks pass on any cause of non-delivery, and each costs a full timeout

- Severity: warning
- Related task / requirement: 2.4; honest acceptance evidence
- Locations:
  - `FaultGroupRun.cs:111-120` (`Probe` waits for all targets, 45 s), `:311-315` (`segment.AllAccepted && !ok`), `:609` (60 s wait), `:615-620` (`!ok`)
  - `Harness/ProviderModel.cs:177` (accepted means exit 0 with `{}`)
  - `src/BellTower.Ingress/Program.cs:55-60` (the ingress prints `{}` even after `CaptureSpool.RecordRejection`)
- Observation:
  - `wait-plain` reports "expected failure (observed)" whenever its hook was not applied, for any reason: a capture the candidate ingress rejects (written to `capture-health`, still `{}`), an undrained spool, or a session missing from `state.json`.
  - A future regression that breaks plain `--no-daemon` captures outright would therefore stay green. The `Diagnosis` text prints the mechanism (session on `codex-session-`, last event `codex-<pid>-<ticks>`), but the predicate does not require it.
  - Because the probes wait for the lost conversation, each Codex and mixed group spends 45 s + 60 s deterministically, about 210 s of the 878 s run (source observation, not measured separately).
- Proposed improvement and benefit:
  - For `HooksLostAcrossUpgrade`, pass only when all of these hold:
    - the session's attachment id is the shared form;
    - `LastEventAttachmentId` equals `NativeAdoption.OwnId` of the conversation's agent process, and the journal contains the probe event;
    - `Completed` lacks it;
    - no new `capture-health` rejection appeared.
  - Wait on journal presence rather than on application. This makes the check mechanism-specific and removes the fixed waits.
- Behavior-preservation risks: none to product behavior. Check ids stay the same.
- Evidence type: source observation; time saving computed from the timeouts
- Verification / measurement plan: unit-test the predicate over a `StateView` fixture with a dropped event versus a missing event. Rerun Codex and mixed and compare durations.
- Relationship: new

### ANALYSIS-244: The exposure evidence says "no shared-backend Codex ran" in groups that start one

- Severity: suggestion
- Related task / requirement: 2.4; ANALYSIS-122 resolution
- Locations:
  - `FaultGroupRun.cs:732-733`
  - [harness-2.4](../evidence/harness-2.4.md) lines 118 and 163 (Codex, mixed)
  - [implementation-010](../evidence/implementation-010.md) line 79 ("Codex sessions use `--no-daemon`")
  - `ProviderModel.cs:221` (the `shared` kind starts `codex.exe app-server`)
- Observation: the predicate correctly expects `SharedBackendCodexUsed` to be true for Codex and mixed, but the fixed detail text asserts the opposite. The resolution note for ANALYSIS-122 is also stale.
- Proposed improvement and benefit: build the sentence from `instance.Exposure.SharedBackendCodexUsed`, for example "a modeled shared-backend Codex ran, so no hook may start a tray in this instance; none did". Correct the implementation note.
- Behavior-preservation risks: none
- Evidence type: source observation
- Verification / measurement plan: the rerun detail for Codex and mixed names the shared-backend session.
- Relationship: residual wording of ANALYSIS-122

### ANALYSIS-245: The swap unit tests clear the whole test-host environment while other test classes run in parallel, and the enforcement call sites are untested

- Severity: warning
- Related task / requirement: ANALYSIS-171; task 3.4 (clean full-suite run; unexplained load-sensitive failure family)
- Locations:
  - `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs:15-72`
  - `tools/BellTower.FieldAcceptance/Harness/ProcessEnvironmentSwap.cs:31-62`
  - `tests/BellTower.Tests/BellTower.Tests.csproj:13` (xUnit 2.9.3). No `CollectionBehavior`, `xunit.runner.json` or `[Collection]` exists, so test classes run in parallel.
  - 32 test files start processes.
- Observation:
  - Each `ProcessEnvironmentSwap.Run` in these tests removes every process variable (`PATH`, `SystemRoot`, `TEMP`, ...) for the duration of the call (four swaps in two tests).
  - Any concurrently running test that populates `ProcessStartInfo.Environment`, starts a child that inherits the environment, or calls `Path.GetTempPath()` in that window sees an empty or default environment. The resulting failures would be rare and differ between runs.
  - Separately, ANALYSIS-171's plan ("`Instance.Run` from a second thread during the swap throws") is tested only at the primitive (`ThrowIfForeign`). Removing the guard calls at `Instance.cs:47,98` or `AgentSession.cs:43` fails no test.
- Proposed improvement and benefit:
  - Put the swap tests in a non-parallel collection (`[CollectionDefinition(DisableParallelization = true)]`), or make the swap target an injectable environment accessor so the unit test never clears the real process environment.
  - Add one test that calls `Instance.Create` (or a thin seam) on a second thread inside `WithProcessEnvironment` and expects the refusal.
  - The guard in `Instance.Run` is broader than needed (it never reads the process environment; it clears and fills the child environment, `Instance.cs:101-120`). It is harmless, but worth a note for 2.7 load during updates.
- Behavior-preservation risks: none to the product. A non-parallel collection lengthens the suite slightly.
- Evidence type: source observation. The contribution to the observed flaky failures is a hypothesis.
- Verification / measurement plan: run the full suite N times with and without the collection isolation, and count environment-related failures (for example a temp-path or child-start failure). Run the new call-site test.
- Relationship: residual of ANALYSIS-171

### ANALYSIS-246: The reusable 2.4 machinery is private or duplicated, and the shell choice rests on a hand-maintained flag

- Severity: suggestion
- Related task / requirement: reuse for 2.5-2.7 and 4.4; `docs/development.md:65`
- Locations:
  - `FaultGroupRun.cs` (753 lines): `Segment` `:66-86`, `Send`/`Tool`/`Probe` `:111-120, 177-192`, `legacyImage` `:31, 181, 295, 665, 691`
  - Tray downtime with hooks, duplicated: `:452-481` and `:586-612`, plus `TrayStayedDown` `:515-527` versus `Harness/TrayReplacement.cs:27-38`
  - Expected-failure composition, duplicated: `:311-315` and `:613-620`
  - Update run, duplicated: `:250-287` and `UpgradeWithoutSetupScenario.cs:99-113`
- Observation:
  - 2.7 needs the same parts: plain kinds through hooks, a tray kept stopped after Exit, never started during an update or switch, and an update with hooks.
  - The hook driver, downtime block and update runner are private to `FaultGroupRun`, and the update runner exists twice already.
  - `legacyImage` must be flipped by hand after every setup rerun. A missed flip runs a 0.1.9 Codex command under PowerShell (parser error) or the reverse, and the shell choice is mislabeled in evidence.
- Proposed improvement and benefit:
  - Move a `HookDriver` (segment counting, `Send`, `Probe`) and a `TrayDowntime` helper (exit, user stop, fire, spool count, `TrayStayedDown` built on `TrayReplacement`) into `Harness`, together with an `UpdateRun` helper that has an optional "while waiting" callback.
  - Derive the shell from the command itself: `IngressImage.HashOf(command) == baselineImages[model]`, then use `ModeledFallbackShell`.
  - Build the expected-failure check once.
  - Expected result: roughly 150 fewer lines in the scenario and one tested path for 2.7.
- Behavior-preservation risks: check ids and details must stay identical; compare the next run's report.
- Evidence type: source observation
- Verification / measurement plan: `faults-and-rollback` and `upgrade-without-setup` produce identical check ids and details. Add unit tests for the image-to-shell selection.
- Relationship: continues ANALYSIS-170 (reuse)

### ANALYSIS-247: `upgrade/under-load` does not show that any hook arrived while the tray was down

- Severity: suggestion
- Related task / requirement: 2.4 (upgrade under load)
- Locations: `FaultGroupRun.cs:262-291`
- Observation:
  - Hooks fire in 200 ms rounds from `UpdateTransaction.Start` until the outcome reads `monitoring-restored` (2-3 rounds in the run).
  - The predicate requires delivery and stability, but not that any capture was spooled while the coordinator had the tray stopped. A fast coordinator, or slow hooks, can make the "under load" part vacuous without failing.
- Proposed improvement and benefit: per round, record `TowerReader.TrayRunning` or the spool count before each burst. Require at least one round fired while no tray ran (or a non-empty spool between stop and restart), and print that count.
- Behavior-preservation risks: may need more rounds or a shorter sleep to be reliable.
- Evidence type: source observation
- Verification / measurement plan: the rerun detail shows "N hooks while the tray was down", with N > 0 in each group.
- Relationship: new

### ANALYSIS-248: The second upgrade neither checks its setup rerun nor probes per-kind delivery

- Severity: suggestion
- Related task / requirement: 2.4 (upgrade again), 4.4
- Locations: `FaultGroupRun.cs:690-691` (result discarded), `:681-715` (no per-kind probe)
- Observation:
  - A failing `setup --host-bridge --apply` shows up only indirectly, through `segment.AllAccepted`.
  - After the 0.1.9 setup rerun and the reupgrade, the documented loss recurs for `wait-plain` (and, per ANALYSIS-240, the kind of loss changes for the owned runtime), but the scenario does not observe it.
  - The release note's "rerun setup, then resume" is therefore unverified for a repeated upgrade.
- Proposed improvement and benefit: check the rerun's exit and image hash (as at `:298`), and call `ProbeCandidateAfterSetup` again under a `reupgrade/` prefix.
- Behavior-preservation risks: adds about 45 s per Codex group unless ANALYSIS-243 is applied.
- Evidence type: source observation
- Verification / measurement plan: new `reupgrade/candidate-hooks-applied-after-setup-refresh/<kind>` rows.
- Relationship: new

### ANALYSIS-249: The launch-kind facts are not pinned, and one test assertion is tautological

- Severity: suggestion
- Related task / requirement: 2.4 fixture fidelity
- Locations:
  - `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs:201` (`Assert.False(string.Equals("claude.exe", "unidentified-claude.exe"))` is always true)
  - `ProviderModel.cs:186-190, 217-222` (no test ties the arguments, `ManagedAttachment` and `Dedicated` to `psm1` or `AgentLauncher`)
- Observation:
  - The reachability conclusions depend on each kind's environment and arguments matching the product launch. A drift in `psm1` (as ANALYSIS-240 shows, already incomplete) would not be caught.
  - The image assertion tests nothing.
- Proposed improvement and benefit:
  - Add a test that parses the exported variables and argument rewrites from `BellTower.psm1` (or asserts `AgentLauncher`'s environment) and compares them with `LaunchKinds`.
  - Replace line 201 with a check against the product's ancestry predicate (`provider + ".exe"`), or drop it.
- Behavior-preservation risks: none
- Evidence type: source observation
- Verification / measurement plan: the new test fails when the `--remote` kind is missing or `--no-daemon`/`DEDICATED` drift.
- Relationship: supports ANALYSIS-240

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-122 | `head=02d4f56 ... untracked=77/c177af0ae6a9` | Every 2.4 hook goes through `AgentSession.Hook` (`FaultGroupRun.cs:182`), which reports to the per-instance sticky `RealEnvironmentExposure` (`AgentSession.cs:58,109-132`). Tray-down phases follow `exit` with the user stop checked (`:459,587`). `TrayStayedDown` ends a running tray and fails the phase (`:515-527`). The final predicate compares the exposure with the expected shared-backend use (`:732`) | verified for 2.4; evidence wording residual in ANALYSIS-244 |
| ANALYSIS-162 (note) | same | 0.1.9 state is read only through 0.1.9's `list`, `doctor`, `statusline` and raw JSON (`StateView.cs`, `TowerReader.IdentityStores`). `ConversationSurfaces.RequireCandidate` refuses other versions (`ConversationSurfaces.cs:90-98`), and nothing in `FaultGroupRun` calls it | verified |
| ANALYSIS-170 | same | `ConversationFixture` extracted and used by both scenarios. `ProviderModel.HistoryRecords` replaces the transcript branch. The surfaces reader is version-bound. Remaining provider-name branches: `FiveConversationsScenario.cs:54` (`HasNativeTitle` tests `Model.Name`), `:173`, and `PackagedHookBenchmarkScenario.cs:248-249` (`SessionOf`), both listed in ANALYSIS-170. Reuse of the 2.4 machinery: ANALYSIS-246 | partially verified (fixture and binding done; a `ProviderModel` member for the native-title read and a model index for `SessionOf` would close it) |
| ANALYSIS-171 | same | The swap refuses foreign threads at `Instance.Create` (`Instance.cs:47`), `Instance.Run` (`:98`) and `AgentSession.Start` (`AgentSession.cs:43`). It does not nest and removes created variables (`ProcessEnvironmentSwap.cs:31-62`). The thread pool is restored in a `finally` (`PackagedHookBenchmarkScenario.cs:53-63`). The only swap user, `FaultGroupRun.cs:254-259`, fires hooks after it returns | verified; test-side residuals in ANALYSIS-245 |

## Assessment and limitations

- Covered: all assigned harness code, the per-check predicates, the five-row launch-kind table against v0.1.9 and candidate source, the `--remote` runtime, null-process paths, reuse for 2.5-2.7, and the carried findings.
- The fault, spool, rollback read-back and retained-checkout checks are non-vacuous and faithful: they assert concrete journal, operation and spool facts, read 0.1.9 through its own commands, and use the unchanged-since-0.1.9 update transaction.
- The plain `codex --no-daemon` characterization is correct by source for its row. However, the default Bell Tower Codex launch (owned `--remote` runtime) is not modeled and, by source, loses hooks in the rollback direction (ANALYSIS-240). The decision premise and the release-note scope need correction, and the user should be told.
- The same attachment-id mechanism also makes the Codex null-process "unavailable" pass hide lost attention (ANALYSIS-241). Plain-launch null-process startups are unmodeled (ANALYSIS-242).
- Not performed: harness runs (per assignment); live Codex process-tree checks. Whether a real Codex TUI classifies as `embedded-runtime` with no same-image children, as the fake does, is unverified. If it classified `unknown`, a managed `--no-daemon` launch would also take the provisional path, and a plain one would not lose hooks.
- Measurements: none. The about 210 s timeout cost (ANALYSIS-243) is computed from source timeouts.
- No unexpected writes. Target identity unchanged.
