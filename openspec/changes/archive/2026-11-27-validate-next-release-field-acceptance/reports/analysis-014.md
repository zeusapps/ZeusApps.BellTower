# Analysis report: validate-next-release-field-acceptance / ANALYSIS-014 (task 2.4, fix cycle 1 re-review)

> Saved verbatim by the orchestrator from the ANALYSIS-014 hand-back. Reviewer: code-analyzer subagent; it did not attempt the report write ("Subagents should return findings as text"). Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

## Review target

- Reviewer / role: ANALYSIS-014 code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code. The subagent cannot see the version. Delegated subagent, auto mode. Model `claude-opus-5-5`.
- Date: 2026-11-26T15:49Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance`, ANALYSIS-014. Reserved finding IDs ANALYSIS-260 to ANALYSIS-279. Used: ANALYSIS-260 to ANALYSIS-266.
- Task and requirement references:
  - task 2.4 ([tasks.md](../tasks.md));
  - [implementation-011](../evidence/implementation-011.md), [harness-2.4](../evidence/harness-2.4.md), [coverage-ledger](../evidence/coverage-ledger.md) row U1;
  - [analysis-013](analysis-013.md), [qa-013](qa-013.md).
- Scope:
  - `tools/BellTower.FieldAcceptance/Harness/{OwnedRuntimeRecord,ProviderModel,StateView,CaptureObserver}.cs`;
  - `Scenarios/{FaultGroupRun,PackagedHookBenchmarkScenario}.cs`;
  - `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs`;
  - `docs/distribution-validation.md`;
  - evidence `coverage-ledger.md`.
- Relevant neighboring paths:
  - harness: `Harness/{AgentSession,Instance,IngressImage,TowerReader}.cs`;
  - candidate product: `src/BellTower/Integration/BellTower.psm1`, `Core/NativeAdoption.cs`, `Providers/CaptureConsumer.cs`, `Providers/AgentModeClassifier.cs`, `IO/AttachmentStore.cs`, `IO/CodexRuntimeStore.cs`, `Sessions/CodexRuntimeHost.cs`, `Hosts/CodexRuntimeProcess.cs`, `Updates/UpdateTransaction.cs`, `Setup/IntegrationRefresh.cs`, `Setup/IngressProvision.cs`, `Tray/TrayController.cs`, `Tray/TrayHost.cs`;
  - the same areas at `v0.1.9`, read with `git show`.
- Baseline commit: 02d4f56
- Implementation identity (before): `head=02d4f56 baseline=02d4f56 files=97 diff=64f9efc62679 untracked=79/9ea6eff4a58f`. It matches the assignment.
- Before/after identity: after the analysis the same command printed `head=02d4f56 baseline=02d4f56 files=97 diff=64f9efc62679 untracked=79/9ea6eff4a58f`. The two are identical. The only command that touches files was `dotnet format --verify-no-changes`, which is read-only.
- Prior reports / rechecked findings: [analysis-013](analysis-013.md) ANALYSIS-240 to -249; [qa-013](qa-013.md) QA-240 to -242.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Owned-runtime kind vs launch script | Read `BellTower.psm1:40-58, 97-106`, `CodexRuntimeHost.cs:63-111`, `CodexRuntimeProcess.cs:16-17`, `AgentSession.cs:39-88`, `ProviderModel.cs:231-238` | Matches the script. The attachment is exported at `:40`. The marker is set whenever there is no `--remote` (`:54-58`). The runtime is prepared when there is neither `--remote` nor `--no-daemon` (`:97`). The arguments become `--remote` without `--no-daemon` (`:104`). The backend inherits the environment (null environment at `CreateProcess`; the `ProcessStartInfo` is not edited). The real backend arguments `app-server --listen <ep> --ws-auth ...` and the fake's `app-server --listen ...` both classify as `SharedBackend` (`AgentModeClassifier.cs:93`). In both cases the parent is not a Codex client, so `CrossCheck` gives Unknown (`NativeAdoption.cs:73`) and the capture is provisional (`:106-128`). The fake does not set `BELLTOWER_SHELL_PID` (`psm1:45`), which affects only `Host.Shell`, not the id. `UsesSharedBackend` is true for the owned-runtime arguments, which is a conservative exposure mark and harmless | Faithful for the ingress and wire id. Promotion gap: ANALYSIS-264 |
| Promotion path | `CaptureConsumer.cs:63-66`, `AttachmentStore.cs:40-45`, `NativeAdoption.cs:187-205`; 0.1.9 `CaptureConsumer.cs` `Adopt(... Ownership = envelope.Dedicated ? ...)` | 0.1.9 registers `att-codex-wait-owned-runtime` with ownership `managed` and process = the agent. The candidate therefore verifies the promoted capture through `recordedLaunch` (`:187`), and the runtime record branch (`:195`, gated by `!recordedLaunch`) is never reached | ANALYSIS-264 |
| Rollback-path claim | `UpdateTransaction.cs:46, 131, 146-175, 291-297`; `IntegrationRefresh.cs:7-40`; callers of `IngressProvision`; `scripts/install-dev.ps1`; `docs/releases.md:41`; `git diff --stat v0.1.9 HEAD -- src/BellTower/Updates src/BellTower/Setup/IntegrationRefresh.cs` | `Stage` refuses anything that is not newer (`:46`). Activation runs `IntegrationRefresh`, which rewrites only the shell module and the VS Code bridge. The catch path restores `plan.Original` and restarts its tray. Ingress images are provisioned only through `IntegrationSetup.cs:51` (setup). `install-dev.ps1` runs no setup. `Updates` is unchanged since v0.1.9, and `IntegrationRefresh` differs by one line | Verified. Only a manual reselect exposes the loss: the documented `current.json` restore, which includes the stale-pending recovery in `releases.md:41`. A dev-build Rollback exposes it only after a user setup |
| Expected-failure predicates | `FaultGroupRun.cs:115-138, 334-347, 417-457, 685-696`; `StateView.cs:16-21`; reducer journaling in 0.1.9 and the candidate (`CausalSessionReducer` journals before replay; `SessionReducer` ignores an attachment mismatch) | `LossByIdMismatch` requires that the event reached the journal, was not applied, and carries an id that differs from the session's in the shared-form way. That rules out spool, tray and rejection causes. Three residuals: the XOR is direction-free and reads the last event; the Claude `ghostplain` predicate does not use it; the rollback phase still has a fixed 60 s wait | ANALYSIS-262, -263, -266 |
| `CaptureObserver.ResolveFrom` | `CaptureObserver.cs:98-114`, `PackagedHookBenchmarkScenario.cs:93-100, 230-249, 316-323`, unit test `FaultInjectionTests.cs:326-352` | It fills in only captures whose envelope id is in an applied journal, using the tray's own provider and session values. `unknown == 0`, `accepted == captures == matched == delivered`, and the matching inside each hook's ingress-time window are all unchanged | Gate not loosened (QA-242) |
| `final/stable-state` retry | `FaultGroupRun.cs:799-816`; `TowerReader.List` (one `list` process per `Row` call) | The loop accepts any change and stops at the first quiet window, for up to 120 s. A pass does not say whether it retried | ANALYSIS-261 |
| Provider-name branches | grep over `Scenarios/` and `Harness/` | No new branch in the 2.4 files: `:160` is a lookup by kind name, and `:127` builds its prefix from the model name. Residuals are unchanged: `FiveConversationsScenario.cs:54, 173` and `PackagedHookBenchmarkScenario.cs:252` (`SessionOf`, formerly `:248-249`). `NullProcessPlainStartupGetsFreshIds => !UnclassifiedStartupFailsClosed` derives one product fact from another; it is correct by source for both providers today | No new drift |
| Null-process classification vs 0.1.9 | `git show v0.1.9:src/BellTower.Ingress/Program.cs:31-45`, `v0.1.9:.../Hosts/AttachmentCapture.cs:34-44`, `v0.1.9:.../IO/AttachmentStore.cs` `Adopt`; candidate `NativeAdoption.cs:101-130` | 0.1.9 keeps `att-` for a dedicated Codex hook that has no process, and a later hook fills the process in. The candidate fails it closed to the shared id | ANALYSIS-265 |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 7 changed .cs files>` | Exit 2, `ENDOFLINE` on every line of all seven files. `tr -cd '\r'` counts 0 CR bytes in each. Unchanged harness files are CRLF (`Instance.cs` 288/288). `.editorconfig` sets `end_of_line = crlf` | failed: ANALYSIS-260 |
| Unit tests, harness | not run | QA runs the checks (assignment) | not performed |

## Findings

### ANALYSIS-260: All seven changed C# files have LF line endings and fail format verification

- Severity: warning
- Related task / requirement: 2.4, the 3.4 clean-gate run; implementation-011's "Format verification on the changed files: clean"
- Locations: `tools/BellTower.FieldAcceptance/Harness/{OwnedRuntimeRecord,ProviderModel,StateView,CaptureObserver}.cs`, `Scenarios/{FaultGroupRun,PackagedHookBenchmarkScenario}.cs`, `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs`; `.editorconfig` (`end_of_line = crlf`)
- Observation:
  - `dotnet format --verify-no-changes` exits 2 with an `ENDOFLINE` error on every line of each file (353, 123, 19, 257, 69, 842 and 325 lines).
  - The files contain no CR bytes. Neighboring files that this cycle did not change are CRLF.
  - The file times (12:25Z to 12:35Z) are earlier than the harness run that wrote `harness-2.4.md` (13:20Z) and than the build of the harness DLL (12:35:31Z). The evidence run therefore used these sources, and only the formatting claim is wrong.
- Proposed improvement and benefit: the developer runs `dotnet format ZeusApps.BellTower.slnx --no-restore --include <these 7 files>`, then reruns the verify-only form, and corrects the implementation-011 check line.
- Behavior-preservation risks: none. Only whitespace changes.
- Evidence type: measured result (command exit code and byte counts)
- Verification / measurement plan: the verify-only command exits 0 on the seven files.
- Relationship: new

### ANALYSIS-261: The `final/stable-state` retry accepts any change for two minutes and records nothing, so it can mask a periodic instability

- Severity: warning
- Related task / requirement: 2.4 (stable final state); honest acceptance evidence
- Locations: `tools/BellTower.FieldAcceptance/Scenarios/FaultGroupRun.cs:801-816`; evidence `harness-2.4.md:82, 131, 190`
- Observation:
  - The loop repeats "sleep 2 s, snapshot, sleep 3 s, snapshot" until the two snapshots agree or 120 s pass. Any differing component is retried: waits, operations, completed memory, journal size, ended state or health. Only the last iteration's differences are reported.
  - A conversation whose verdict or waits oscillate, with any quiet interval of about 5 s or more, therefore passes, and the pass text ("Two reads three seconds apart agree") looks exactly like a first-try pass. Before this change the single comparison could catch it.
  - The code comment's rationale is "a conversation registered a moment ago", but no conversation registers shortly before the final checks. The flip the developer observed was on the shared-backend conversation, which registered in phase 1. A plausible cause is that the `ref_2` probe and the reupgrade fault hooks fall inside the one-minute grace, but nothing records the transition.
- Proposed improvement and benefit: choose one of these.
  - (a) Wait until every conversation's last hook is older than the grace plus a margin, then keep the single comparison. This is deterministic.
  - (b) Retry only when the health verdict is the sole differing component. Allow at most one transition per conversation, into a post-grace verdict. Print each transition (key, from, to, attempt) in the detail. Fail on any change to waits, operations, completed memory, journal size or ended state.
- Behavior-preservation risks: (a) adds up to a minute per group. The check id stays the same.
- Evidence type: source observation
- Verification / measurement plan: a unit test of the decision over two synthetic fingerprint sequences: a single health transition passes, while a fingerprint change or a second transition fails. The rerun detail names any transition.
- Relationship: new, from the fix-cycle change listed under "Also" in implementation-011

### ANALYSIS-262: The Claude plain null-process expected failure passes even if its probe events never reached the journal

- Severity: warning
- Related task / requirement: 2.4; ANALYSIS-242, ANALYSIS-243 and QA-241 (expected failures tied to their mechanism)
- Locations: `FaultGroupRun.cs:441-457` (`Until(... Journal.Count >= 4)` at `:448`, result discarded; predicate at `:453`)
- Observation:
  - For Claude the predicate is `Waits == 0 && JournalAttachmentIds.Count > 1`.
  - The `SessionStart` and `UserPromptSubmit` captures already carry two different random ids. So if the `PreToolUse` and `PermissionRequest` captures are rejected by the consumer (`CaptureConsumer.cs:47-51`, after the ingress printed `{}`), lost, or never applied, the check still reports "expected failure (observed)".
  - This is the same weakness that QA-241 and ANALYSIS-243 fixed for the other expected failures, which use `LossByIdMismatch`.
- Proposed improvement and benefit:
  - Require `JournalOperationIds.Contains("toolu_plain_1")` and a journal of at least 4 events.
  - Require that the attachment id of the journal event carrying `toolu_plain_1` differs from `AttachmentId`.
  - Keep `Waits == 0`.
- Behavior-preservation risks: none. The check id stays the same.
- Evidence type: source observation
- Verification / measurement plan: a `StateView` fixture unit test. Two events with two ids and no pre event must fail; four events with the pre event under a foreign id must pass.
- Relationship: residual of ANALYSIS-242 and ANALYSIS-243

### ANALYSIS-263: `LossByIdMismatch` is direction-free and reads the latest event rather than the probed operation's event

- Severity: suggestion
- Related task / requirement: 2.4; QA-241, ANALYSIS-243
- Locations: `FaultGroupRun.cs:127, 133-138` (used at `:342, :432, :691`); `StateView.cs:18` (`LastEventAttachmentId` is `events[^1]` of the `At`-sorted journal)
- Observation:
  - The XOR accepts either direction. Each documented loss has one direction:
    - plain `--no-daemon`: the session is on the shared id, the event on `codex-<pid>-<ticks>`;
    - owned-runtime rollback: the session is on `att-`, the event on the shared id;
    - Codex heal: the session is on the shared id, the event on `att-`.
  - A different defect therefore also passes as the documented loss, for example an owned-runtime session that got rebound to the shared id while later hooks carry `att-`.
  - In the rollback check, `downIds.All(id => LossByIdMismatch(conv, id))` judges the same latest event three times.
  - Delivery causes (spool, tray, rejection) are correctly excluded by the journal requirement, so the predicate cannot pass on those.
- Proposed improvement and benefit:
  - Compare exact expected ids per kind. These are computable in the harness: `NativeAdoption.SharedId(provider, conv.Id)`, `NativeAdoption.OwnId(provider, ProcessProbe.Identify(conv.Session.ProcessId))` and `conv.Attachment`.
  - Read the `attachmentId` of the journal event whose `operationId` is the probe.
  - Replace the string prefix in `SharedForm` with `SharedId`.
- Behavior-preservation risks: none. The check ids stay the same.
- Evidence type: source observation
- Verification / measurement plan: fixture unit tests for each direction; the reversed direction must fail.
- Relationship: residual of QA-241 and ANALYSIS-243

### ANALYSIS-264: The new owned-runtime record is never consulted; a Codex conversation first started under the candidate by the default launch is unmodeled

- Severity: warning
- Related task / requirement: 2.4; ANALYSIS-240 (its sub-case "conversations the candidate registered after the setup rerun")
- Locations:
  - `FaultGroupRun.cs:161-165, 216-225` (the only owned-runtime conversation is `wait-owned-runtime`, started in the 0.1.9 phase);
  - `Harness/OwnedRuntimeRecord.cs:11-18`;
  - product `NativeAdoption.cs:187, 195-205` and `IO/AttachmentStore.cs:40-45`.
- Observation:
  - 0.1.9 registers that conversation under `att-` with ownership `managed` and the agent as its process (0.1.9 `CaptureConsumer` keeps the ownership for `envelope.Dedicated`).
  - When the candidate promotes the provisional capture, `previous` is that record, so `recordedLaunch` is true and `Reconcile` returns verified. The runtime-record branch requires `!recordedLaunch` and is skipped.
  - The harness's record and its "manager = shell = harness process" design therefore exercise nothing. implementation-011's "which is what NativeAdoption.Reconcile requires" holds only for a conversation the candidate registers first.
  - After an upgrade that is every new default Bell Tower Codex launch, and its candidate follow-through and rollback loss are not shown by 2.4. Product unit tests (`NativeOwnedRuntimeProcessTests`) and packaged update acceptance may cover the candidate side, but 2.4 does not.
- Proposed improvement and benefit:
  - Add a Codex `fresh-owned-runtime` wait conversation started in the candidate phase after the setup rerun, with the record.
  - Run it through the per-kind candidate, rollback, setup-rerun and reupgrade checks. By source: followed by the candidate, the same expected rollback loss, and restored by the setup rerun.
  - Optionally add a negative control without the record, which by source fails closed to the shared id. It proves that the record is what verifies the conversation.
  - Correct the implementation-011 sentence.
- Behavior-preservation risks:
  - `Reconcile` would then set `Host.Shell` to the recorded shell (`NativeAdoption.cs:200`). Record a dedicated idle helper process as the shell and manager, never the harness process, so that no product presentation or focus action can target the harness console.
  - The rollback expected-failure count rises by one per Codex-bearing group.
- Evidence type: source observation
- Verification / measurement plan: the new rows pass as described. The negative-control row shows `codex-session-<hash>` as the session id.
- Relationship: residual of ANALYSIS-240

### ANALYSIS-265: The docs classify the Codex null-process later-hook loss as pre-existing in 0.1.9, but by source it is new in the candidate; two evidence statements are stale

- Severity: warning
- Related task / requirement: 2.4; the 5.2 parity decision; ANALYSIS-241, ANALYSIS-244; 4.4 release notes
- Locations:
  - `docs/distribution-validation.md:36` ("Plain launches whose hooks find no agent process, and the later hooks of a Codex null-process conversation, are reported as expected failures too (ANALYSIS-241, -242; pre-existing in 0.1.9)"; also "The two per-kind checks ... are declared expected failures");
  - `evidence/implementation-010.md:79` ("asserts no hook started a tray and no shared-backend Codex ran. Codex sessions use `--no-daemon`.");
  - 0.1.9 source: `git show v0.1.9:src/BellTower.Ingress/Program.cs:33` (with `DEDICATED=1` the managed id is kept); `v0.1.9:src/BellTower/Hosts/AttachmentCapture.cs:41-44` (an environment id wins even with no process); 0.1.9 `AttachmentStore.Adopt` fills `Process` from a later live hook;
  - candidate: `NativeAdoption.cs:101, 106, 130` (an Unknown capture with no process fails closed to `Shared`).
- Observation:
  - Under 0.1.9, a Bell Tower standalone Codex conversation (attachment plus marker) whose first hook found no process keeps `att-` on every hook and is repaired by the later identified hook. The candidate registers it under `codex-session-<hash>` and drops the later hooks, so the ANALYSIS-241 loss is a 0.1.11 change for that launch. Only ANALYSIS-242 (plain Claude, a fresh id per hook) is pre-existing.
  - The doc counts "two per-kind checks", but six check ids now expect failure, with 12 observations in the run.
  - The second half of ANALYSIS-244 (the implementation note) was not corrected.
- Proposed improvement and benefit:
  - Correct the classification in the docs, the U1/F4 wording and the 5.2 decision input: a regression from 0.1.9 for Bell Tower standalone Codex launches whose startup hook missed the process; Claude is repaired.
  - List the expected-failure check ids.
  - Correct `implementation-010.md:79` as a marked correction, as was done at `:66`.
- Behavior-preservation risks: none, documentation only. The user decision may change.
- Evidence type: source observation. The 0.1.9 behavior is not run in the harness; a 0.1.9-phase heal probe would show it.
- Verification / measurement plan: optionally, a null-process heal conversation in the 0.1.9 phase. By source, 0.1.9 repairs it and makes the wait.
- Relationship: residual of ANALYSIS-241 and ANALYSIS-244

### ANALYSIS-266: The rollback phase still spends a full 60 s waiting for application before its journal wait

- Severity: suggestion
- Related task / requirement: 2.4; ANALYSIS-243 (timeouts)
- Locations: `FaultGroupRun.cs:681` (waits until every `rbdown` id is completed), then `:686` (journal wait)
- Observation:
  - `wait-plain` and `wait-owned-runtime` never complete under 0.1.9, so `:681` always times out at 60 s in the Codex and mixed groups.
  - The "dropped event costs no timeout" claim holds for `Probe`, the heal wait and the ghostplain wait, but not for this phase.
- Proposed improvement and benefit: wait only at `:686` on journal presence, or exclude expected-loss kinds from `:681`. This saves about 60 s per Codex-bearing group (computed from the timeout, not measured).
- Behavior-preservation risks: none. `rollback/tray-and-delivery` does not use the `:681` result.
- Evidence type: source observation
- Verification / measurement plan: compare the group durations in the next run's report.
- Relationship: residual of ANALYSIS-243

## Re-verification

All rows are at the corrected target `head=02d4f56 ... diff=64f9efc62679 untracked=79/9ea6eff4a58f`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-240 | The `owned-runtime` kind matches `psm1:40,52-58,97-106` and the backend environment inheritance (Analysis table). The rollback check is a declared expected failure tied to `LossByIdMismatch` (`FaultGroupRun.cs:689-695`; evidence rows 111 and 166). The setup rerun restores delivery (rows 120 and 177). Upgrade and reupgrade probes pass. Scope corrected in `distribution-validation.md:36`, ledger U1 and `ProviderModel.cs:52-65`. A pinning test was added (`FaultInjectionTests.cs:297-324`). The user decision is listed as open in implementation-011 | verified for the 0.1.9-registered conversation; the candidate-registered sub-case is unmodeled (ANALYSIS-264) |
| ANALYSIS-241 | New check `candidate/null-process-later-hooks/<heal>` (`:417-437`) sends a pre hook and a permission request from the identified process. Codex expects `waits == 0` plus the id mismatch, and the evidence (rows 96 and 148) shows `codex-session-...` against `att-codex-heal`. Claude makes one wait | verified; classification wrong in the docs (ANALYSIS-265) |
| ANALYSIS-242 | A `ghostplain` role per provider (`:160, 441-457`). The evidence shows 4 ids for Claude and 1 for Codex | partially verified: the Claude predicate is weak (ANALYSIS-262) |
| ANALYSIS-243 | `Probe` waits on journal presence (`:123`). The expected failures require journal presence, non-application and an id mismatch (`:133-138`) | partially verified (ANALYSIS-263, -266) |
| ANALYSIS-244 | The detail text now depends on `SharedBackendCodexUsed` (`:821`) | partially verified: `implementation-010.md:79` is still stale (ANALYSIS-265) |
| ANALYSIS-245 | `[CollectionDefinition(DisableParallelization = true)]` plus `[Collection]` (`FaultInjectionTests.cs:12-20`); under xUnit 2.x such a collection runs after the parallel ones. The call-site test `:259-280` asserts the "swapped" refusal from `Instance.Create`. Minor: it catches only `InvalidOperationException` on a raw `Thread`. If the guard were removed, the different exception thrown with null tools would end the test host instead of failing one test. That is still a failure, but a disruptive one | verified |
| ANALYSIS-246 | The shell now follows the installed command (`:202-203`). `legacyImage` is still a field and is assigned at `:31, 323, 740, 767`, but only `:202`'s value is read, so the other assignments are dead state. Deferring the extraction is reasonable: it is behavior-preserving, it is better designed against a real second caller, and the check ids must stay identical. Condition: record it as an open item on the first 2.5-2.7 task. `FaultGroupRun` grew from 753 to 842 lines, and the expected-failure composition now exists four times (`:343, 433, 454, 692`) with differing predicate shapes, which is where ANALYSIS-262 came from | partially verified; deferral reasonable with that condition |
| ANALYSIS-247 | Each hook is counted as fired while down through `TrayController.IsRunning` (mutex `TryOpenExisting`, the same name as 0.1.9 `LockName` for a full-path root), and the check requires more than 0 (`:291-317`). The evidence shows 2 of 10, 3 of 16 and 3 of 12. A fast coordinator makes the check fail rather than pass vacuously, but the margins are small, so a fail-safe flake is possible | verified |
| ANALYSIS-248 | `reupgrade/setup-rerun-points-hooks-at-candidate-image` checks the exit code and image hash, and `ProbeCandidateAfterSetup("reupgrade", ...)` reprobes every kind (`:766-771`) | verified |
| ANALYSIS-249 | Text pinning of `psm1` and `AgentLauncher.cs` plus the harness kinds (`:297-324`). The ancestry assertion (`:211-216`) now depends on `UnidentifiedPrefix` being non-empty: weak, but no longer constant-true. The owned-runtime arguments are not pinned to `CodexRuntimeHost.cs`'s `"app-server", "--listen"`; low risk, because classification needs only `app-server` | verified |
| QA-240 | Same code as ANALYSIS-244 (`:821`) | verified |
| QA-241 | Same as ANALYSIS-243 | verified, with residual ANALYSIS-263 |
| QA-242 | `ResolveFrom` fills in only captures whose envelope id is in an applied journal, using the tray's own values. The exact-count conjunction including `unknown == 0` is unchanged (`PackagedHookBenchmarkScenario.cs:316-323`). Matching still requires the ingress time inside the hook's window (`:230-249`). Unit test `:326-352` covers an applied capture and one the tray never applied | verified by source (gate not loosened). Whether the flake recurs is for QA |

## Assessment and limitations

- Specific checks:
  - The owned-runtime kind is faithful to `BellTower.psm1` for the ingress and the wire id. The candidate's promotion is shown only through the recorded-launch path (ANALYSIS-264).
  - The rollback-path claim is verified by source.
  - The new expected-failure predicates exclude delivery causes. They can still pass on a different id-mismatch cause (ANALYSIS-263), or, for Claude `ghostplain`, without the probe events (ANALYSIS-262).
  - `ResolveFrom` does not loosen the exact-count gate.
  - The stable-state retry can mask a periodic instability (ANALYSIS-261).
  - No new provider-name branches.
- Format verification fails on all seven changed files (ANALYSIS-260), contrary to the developer's check line.
- Decision input for the orchestrator and user: ANALYSIS-265 changes the premise of the 5.2 and U1 decisions. The Codex null-process later-hook loss is new in 0.1.11 for Bell Tower standalone Codex launches; it is not pre-existing.
- Not performed: harness and unit-test runs (QA owns them); live Claude Code or Codex sessions. The 0.1.9 null-process repair behavior is established by source only.
- Clients covered: Claude Code and Codex source paths, plus the mixed groups through the shared scenario code.
- No unexpected writes. Target identity unchanged.
- Report write: not attempted. This client's subagent instructions forbid writing report `.md` files (the known restriction of earlier rounds), so the orchestrator should save this text as `reports/analysis-014.md`.
