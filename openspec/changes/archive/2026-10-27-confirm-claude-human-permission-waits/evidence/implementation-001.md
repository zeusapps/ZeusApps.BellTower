# Implementation 001: confirm-claude-human-permission-waits

Developer handoff against baseline `754aed5`. Tasks 1.1, 1.2, 2.1 to 2.5, 2A.1 to 2A.5, 3.1 to 3.4 and 4.1, 4.2 were assigned. Nothing is committed. All results below are fixtures, fake runtimes and isolated probes; no live auto-versus-human acceptance on real sessions was performed (see Limitations).

## What changed

Shared policy in `Core`, thin provider adapters, one set of states for both clients.

- `Core/WaitState.cs`: `HumanConfirmation` (Unverified, Provisional, Confirmed); `WaitState` gains `Confirmation`, `ConfirmedAt`, `ConfirmationCredit`, `Ambiguous` and a `JsonExtensionData` bag (unknown wait fields round trip); `ApprovalReceipt` for resolved waits.
- `Core/Observation.cs`: additive `HumanPrompt`, `QuestionTool`, `ProviderDelay`, `PermissionMode`. No new `EventKind`, so an older consumer still sees a `Permission` observation.
- `Providers/HookAdapter.cs` (the Claude/Codex adapter): `Notification/permission_prompt` is a `HumanPrompt` carrying `ClaudePermissionPromptDelay` (5 s floor, below the observed 6.27 s); question tools set `QuestionTool` on every hook stage; `PermissionRequest` stays an approval-flow start.
- `Core/SessionState.cs`: a permission wait is attention only when Confirmed by a hook signal or by fresh owning-runtime status (`NativePermissionPending == true`); legacy/provisional/unverified never qualify and a fresh negative runtime status suppresses. `Reason`, `Since`, `Due` and `NotificationDue` are computed over active waits only. The 5 s `HumanWaitGrace` counts any credited provider delay (Claude eligible on arrival; Codex keeps 5 s from the first runtime report). `EffectiveConfirmation(wait)` is the exported confirmed-eligibility contract for reminders/webhooks (with `RequiresAttention`). New `ResolvedApprovals` receipts (additive, preserved by the causal reducer copy).
- `Core/SessionReducer.cs`: `AddWait` creates Provisional permission/question waits; a provisional request owns the episode only when nothing else is actionable (a newer auto-approved request cannot re-announce or un-dismiss an older wait); `ConfirmPrompt` (attributed by operation/fingerprint, or unattributed by turn scope; question+permission pending = ambiguous and assigned to neither; several same-fingerprint candidates are confirmed as a set, never resolved or assigned; confirmation-only creates a confirmed wait unless stale); `ApproveQuestionTool` (question-tool approval flow is confirming evidence for the question wait, never a permission wait; precursor-missing creates the question, later tool start adopts it); receipts recorded at every resolution and cleared at prompt/stop/interrupt/end/replacement attachment; a late approval hook after Stop cannot start a question.
- `Providers/CodexConnection.cs`, `ICodexAttentionClient.cs`, `Sessions/CodexPermissionPump.cs`: `waitingOnUserInput` read (interface default method keeps fakes compiling) and applied transiently as `NativeUserInputPending`; evidence only, never gates or delays a question. Pump now also polls sessions with a pending Codex question.
- Diagnostics: `ConfirmationCoverageReporter` (doctor `confirmationCoverage`: policy, per provider signal, configured, observed with last time and permission modes, status, verified version/mode evidence, wait counts confirmed/provisional/unverified/ambiguous, Codex sessions without a verified runtime reported unavailable); `IO/ConfirmationEvidence` (best-effort `<provider>-confirmation-observed.json`, no session content, written by `CaptureConsumer` and the pump); `CapabilityMatrix` gains three rows (confirmation; confirmation without an owning runtime = Codex **unverified**; question-tool approval flows) and the Codex `ApprovalResolution`/`Questions` doctor strings are rewritten.
- Docs: `docs/architecture.md` (policy, receipts, question tools), `docs/compatibility.md` (prose, matrix rows, closing-evidence list), `docs/installation.md`, `docs/hook-ingress.md` (additive fields, snapshot rollback note), `docs/follow-ups.md` (dated entry; older paragraphs left as history).
- Tests (919 pass; baseline 870): redacted fixtures `tests/BellTower.Tests/Fixtures/hooks/{claude-askuserquestion-auto,codex-request-user-input-plan,codex-request-user-input-default-rejected}.json`, `Testing/HookSequence.cs`, `Core/QuestionToolApprovalTests.cs` (12), rewritten `Core/PermissionNotificationTests.cs`, `Diagnostics/ConfirmationCoverageTests.cs`, additions to `ProviderTests`, `CaptureTests`, `CodexPermissionTests`, `ReducerTests`. Tests that used a bare `Permission` observation as a stand-in for "a waiting session" now use `TestWorkspace.ConfirmedPermission`.

## Behavior change to confirm (not a defect of the implementation)

The spec removes the timing fallback. Codex sessions without a verified owning runtime (explicit `--no-daemon`, existing/remote sessions) have no human-wait signal at 0.164.0 (`PermissionRequest` runs before guardian/user review: openai/codex `codex-rs/hooks/src/events/permission_request.rs`, `core/src/tools/approvals.rs`), so their permission requests are now unverified and raise no chip or sound. This is recorded as an **unverified** parity gap in the capability matrix, doctor and docs, not as a restriction, and is open release work (see Proposed planning changes).

## Per-task status

| Task | Status | Evidence |
| --- | --- | --- |
| 1.1 | done | Fixtures above from `evidence/claude-askuserquestion-hooks.json` and `codex-request-user-input-hooks.json` (PermissionRequest vs permission_prompt fields/timing, Claude 2.1.310, Codex 0.164.0). Ordering/health predecessors verified through the existing suites that exercise them (causal replay and attachment-health tests, all passing); I did not re-audit the archived predecessor documents. |
| 1.2 | done | Provenance and correlation in shared policy; `WaitStateRoundTripPreservesUnknownFieldsAndOldSnapshotsReadAsUnverified`, `RestartRestoresConfirmedAttentionSilentlyAndLegacyTimingOnlyWaitsAsUnverified`, `ConfirmationFieldsSurviveTheCaptureProtocol...` |
| 2.1 | done | `AutomaticReviewLongerThanFifteenSecondsWithoutConfirmationNeverBecomesPermissionAttention` (claude, codex; 119 s), `PermissionResolvedDuringGraceNeverCreatesAChipOrSound` |
| 2.2 | done | `ConfirmedClaudePromptIsEligibleOnArrival...` (6.274 s, due == arrival, `Since` stays the request time), `ConfirmationOnlyWaitStartsAtTheNotification...` (credit math), `CodexNativeConfirmationKeepsItsFiveSecondGrace...` |
| 2.3 | done | `SameFingerprintParallelRequestsAreNeverArbitrarilyAssignedOrResolved` (with/without tool IDs), `PartialParallelResolution...`, `UnrelatedToolDoesNotResolvePermission` |
| 2.4 | done | `LateConfirmationNeverResurrectsAResolvedWait` (resolved, stop, interrupt, end, new prompt, attributed), `ReplacementAttachmentRejects...`, `ShuffledAndDuplicatedDelivery...`, `DeniedOrInterruptedFlows...` |
| 2.5 | done | `RestartRestores...`, `DistinctConfirmedPermissionNotifies...(viewed/dismissed)`, `ANewConfirmedPermissionAfterRestart...`, `AReplacementRequestNeedsItsOwnConfirmation` (claude, codex), `SubagentStop...` |
| 2A.1 | done | Regression written before any production change and run on the baseline code: `ClaudeQuestionDialogApprovalFlowNeverFlipsTheQuestionToPermission` failed with `Assert.Equal() Failure: Values differ Expected: Question Actual: Permission` at `QuestionToolApprovalTests.cs:27`; it passes after the change. |
| 2A.2 | done | Claude full sequence, single episode/announcement, dismissal survives, restart silent, either order of start/approval, no resurrection after resolve; Codex Plan sequence (no approval hook, correlated PostToolUse clears, then Done) |
| 2A.3 | done | `AQuestionAndAnUncorrelatedPermissionPending...Ambiguous`, `ResolvingTheQuestionLeavesThePermissionProvisional`, `ClaudeQuestionAndCodexPermissionPendingTogether...` (independent kinds/episodes/dismissal) |
| 2A.4 | done | `ARejectedCodexDefaultModeQuestionClearsAtStopWithNoResidual` (clears at Stop, no restore after restart, late hook cannot revive). Owning-runtime signal found and recorded: `evidence/implementation-001-2a4-codex-appserver-user-input-status.json` (`waitingOnUserInput` in Plan mode and Default mode with `default_mode_request_user_input`); read as evidence only (`RuntimeQuestionStatusIsConfirmationEvidenceAndNeverGatesTheQuestion`). |
| 2A.5 | partial | Claude `default` mode captured (`implementation-001-2a5-claude-askuserquestion-default-mode.json`): identical sequence and fields to auto, Notification +6.2 s. Codex `request_user_input_async`: **not obtainable** at 0.164.0 (tool not in the model's tool list, no enabling feature; `implementation-001-2a5-codex-request-user-input-async-not-offered.json`). Codex Default mode with the experimental dialog feature captured (no approval hook). Plan-mode async needs a person typing `/plan`: **not performed**. Caveat: in both interactive probes a PostToolUse arrived 7.7 s / 16.5 s after the start although the probes never answer; the windows were visible on the desktop so a person may have responded; recorded as unattributed. Claude modes other than auto/default are unverified. |
| 3.1 | done | `ConfirmationCoverageTests` (fresh dir configured-not-observed; observed with mode; Codex without runtime unavailable; legacy counted unverified, never confirmed) |
| 3.2 | done | Matched human/auto/unverified conformance by provider theories plus the existing Codex native transport/name/runtime suites (all pass); the former timing-only fallback now asserts unverified (`FailedUnknownOrStaleVerificationLeavesTheRequestUnverifiedNeverConfirmed`). |
| 3.3 | done | Question, completion (20 s) and background tests unchanged and passing; confirmed eligibility exported via `EffectiveConfirmation` and `RequiresAttention` (documented in architecture). Full suite 919 passed with silent/fake sinks. |
| 3.4 | partial | Docs, formatter and Release build done. The targeted live auto-versus-human parity acceptance is **not performed** (not delegated). |
| 4.1 | partial | Matched provider fixtures and mixed sessions (`MixedClaudeAndCodexSessions...`, `ClaudeQuestionAndCodexPermission...`) with version/mode evidence retained. Live matched acceptance **not performed**. |
| 4.2 | partial | Audit: setup already registers `PermissionRequest` for both clients and `Notification` (`permission_prompt|elicitation_dialog|idle_prompt`) for Claude; no new configuration option or control was added, so none can differ by client; doctor/matrix state the Codex runtime-less gap as unverified with closing evidence. That gap is outstanding work, not a restriction. |

## Provider coverage

| Behavior | Claude Code | Codex |
| --- | --- | --- |
| Provisional on approval-flow start, never attention alone | fixture (2.1.310 shape) | fixture (0.164.0 shape) |
| Confirmation signal | `permission_prompt`, eligible on arrival | owning-runtime `waitingOnApproval`, 5 s from first report (fake runtime) |
| No signal | unverified, silent | unverified, silent (runtime-less sessions unavailable, unverified gap) |
| Question approval flow stays a question | auto and default modes (fixture + probe) | no approval hook in Plan; Default-mode rejection clears at Stop; `waitingOnUserInput` evidence |
| Late/duplicate/shuffled confirmation, ambiguity, restart, legacy | shared Core policy, tested | shared Core policy, tested |
| Mixed simultaneous sessions | tested | tested |

## Checks run

- `dotnet build -c Release --no-restore` -> passed, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore` -> passed 919/919.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <changed .cs files>` -> passed.
- Node tests for `vscode/` -> not run (not changed). NativeAOT ingress publish -> not run; the source-generated protocol round trip is covered by `CaptureTests`.
- Probes (isolated config, loopback fixture models, no real provider settings): Claude `--permission-mode default`; Codex TUI Default mode with `request_user_input_async` and with `default_mode_request_user_input`; hidden `codex app-server` on a loopback websocket for the `waitingOnUserInput` status in Plan and Default modes. Probe processes were stopped by command-line-verified PID and none remain.

## Limitations and decisions needed

- Codex sessions without a verified owning runtime no longer alert on permission requests (unverified parity gap, release-blocking per AGENTS.md until a signal or restriction proof exists). Decision: accept as the spec intends, or add a task.
- Live auto-versus-human acceptance (3.4, 4.1) not performed. Claude modes beyond auto/default and `request_user_input_async` unverified.
- A prompt received while a question and an uncorrelated permission are pending stays ambiguous; the permission then stays provisional until another signal (no heuristic).
- The 30 s stale-confirmation window for unattributed prompts and the 5 s Claude delay floor are Bell Tower constants chosen from the single observed 6.27 s timing; no provider documents either (Claude hook docs list no delay).
- Codex native-status confirmation applies to all provisional permission waits of the thread (the runtime cannot say which); when a question already owns the episode, a simultaneous native-confirmed Codex permission shares that episode (edge case).
- Rollback: an older tray rewriting the snapshot drops per-wait confirmation fields (those waits then read as unverified); episodes and dismissal receipts survive.
- `README.md` line 36 still describes the 15-second debounce but is outside my write ownership. Suggested replacement is in the next section.

## Proposed planning changes (for the orchestrator)

1. `design.md` Decision 5, "Open": resolved. Claude `default` mode behaves like `auto`; Codex has an owning-runtime user-input signal (`waitingOnUserInput`). Still open: other Claude modes, `request_user_input_async`.
2. Add a task (or release-gate note) for a Codex human-wait signal without an owning runtime, with the closing evidence listed in `docs/compatibility.md`.
3. README.md line 36 replacement: "Completion waits 20 seconds. Permission requests are provisional until a supported signal confirms a human wait (Claude's permission prompt notification, or the verified Codex owning-runtime status) and then use a 5-second grace that counts the provider's own delay; automatic approvals and requests without a supported signal never alert. Question dialogs stay questions. Questions and terminal failures remain immediate."
4. Consider noting in the spec that the reminders/webhooks contract is `SessionState.EffectiveConfirmation` plus `RequiresAttention`.

## Files

Source: `src/BellTower/Core/{CausalSessionReducer,Observation,SessionReducer,SessionState,WaitState}.cs`, `Diagnostics/{CapabilityMatrix,DiagnosticsReporter,ConfirmationCoverageReporter}.cs`, `IO/ConfirmationEvidence.cs`, `Providers/{CaptureConsumer,CodexConnection,HookAdapter,ICodexAttentionClient}.cs`, `Sessions/CodexPermissionPump.cs`. Tests, fixtures and docs as listed above. Evidence: `evidence/implementation-001.md`, `evidence/implementation-001-2a4-*.json`, `evidence/implementation-001-2a5-*.json`. Local-only (ignored) probe tools updated under `artifacts/askq-probe-tools/` (`probe.js` `--permission-mode`, `codex-probe.js` `PROBE_FEATURES`, new `codex-appserver-probe.js`).

```
status: partial
role / client / model: developer / Claude Code / claude-sonnet-5-5
task / finding IDs: 1.1, 1.2, 2.1-2.5, 2A.1-2A.4, 3.1-3.3 done; 2A.5, 3.4, 4.1, 4.2 partial (live acceptance, Plan-mode async and non-auto/default Claude modes not performed); no findings
changed or report paths: src/BellTower/Core, src/BellTower/Diagnostics, src/BellTower/IO/ConfirmationEvidence.cs, src/BellTower/Providers, src/BellTower/Sessions/CodexPermissionPump.cs, tests/BellTower.Tests (incl. Fixtures/hooks), docs/{architecture,compatibility,installation,hook-ingress,follow-ups}.md, openspec/changes/confirm-claude-human-permission-waits/evidence/implementation-001.md and implementation-001-2a4-*/2a5-*.json, artifacts/askq-probe-tools/ (ignored)
checks: dotnet build -c Release --no-restore -> passed; dotnet test -c Release --no-build --no-restore -> passed (919/919); dotnet format --verify-no-changes on changed files -> passed; node vscode tests -> not run (vscode/ unchanged); NativeAOT ingress publish -> not run
target identity: head=754aed5 baseline=754aed5 files=50 diff=6b6da7c0e29e untracked=10/b02e4c1ab43a (src tests docs; the two pre-existing docs/handoffs files are included in the untracked count and untouched)
limitations / decisions needed: Codex sessions without an owning runtime now raise no permission alert (unverified gap, needs decision/task); live auto-versus-human acceptance not performed; README.md line 36 outdated and outside ownership; probe PostToolUse responses unattributed
next: orchestrator QA and code-analyzer review of the target; decide the Codex runtime-less gap and README edit; run live acceptance with the user
```
