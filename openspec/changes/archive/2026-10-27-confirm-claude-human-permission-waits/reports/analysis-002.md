> Saved by the orchestrator under the write fallback. Reviewer: code-analyzer subagent; client: Claude Code 2.1.310, subagent of the orchestrator session; model: `claude-opus-5-5`. The client refused the reviewer's native write of `reports/analysis-002.md` with the message "Subagents should return findings as text, not write report files"; the reviewer did not retry. Text below is the reviewer's report, verbatim.

# Analysis report: confirm-claude-human-permission-waits / 002 (recheck 1)

## Review target

- Reviewer / role: code-analyzer subagent (Analysis 002) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310, subagent of the orchestrator session, role-default model `claude-opus-5-5`
- Date: 2026-10-26T09:30Z (UTC)
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits` / Analysis 002 (recheck of fix round 1, Implementation 002)
- Task and requirement references:
  - Rechecks: ANALYSIS-001, ANALYSIS-002, ANALYSIS-003 ([analysis-001](analysis-001.md)).
  - New task code: 2.6 (unverified chip) and 2A.6 (held-prompt re-evaluation).
  - Deviations 1–3 in [implementation-002](../evidence/implementation-002.md).
  - [attention-notifications delta](../specs/attention-notifications/spec.md):
    - Completion and permission grace, including the scenarios "Permission is automatically approved" and "Codex session without a verified owning runtime".
    - Question-tool approval flows remain question waits, including the scenario "Question answered while an ambiguous prompt is held".
  - Design Decision 6.
- Scope and relevant neighboring paths:
  - Core: `src/BellTower/Core/{SessionReducer,SessionState,Observation,WaitState,CausalSessionReducer,AlertPolicy}.cs`
  - Sessions and providers: `src/BellTower/Sessions/CodexPermissionPump.cs`, `src/BellTower/Providers/{HookAdapter,CaptureConsumer}.cs`
  - IO and diagnostics: `src/BellTower/IO/ConfirmationEvidence.cs`, `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs`
  - Tray: `src/BellTower/Tray/{SessionViewFactory,ChipView,OverviewForm,TrayApplication}.cs`
  - Tests: `Core/HumanWaitCorrectionTests.cs`, `Providers/{CodexPermissionTests,LegacyIngressTests}.cs`, `Tray/UnverifiedChipTests.cs`
  - Docs: `docs/{architecture,compatibility,installation}.md`, `README.md`
- Baseline commit: `754aed5`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md` printed:
  - HEAD `754aed525415d0cb3f47e8b6064e91fe1abdc618`
  - 45 modified and 13 untracked paths
  - tracked diff SHA-256 `b9e37ea92406608bb5f29bd3665eb11076689a69b90c118ebfacfb113d2b13f3`
  - summary `head=754aed5 baseline=754aed5 files=58 diff=b9e37ea92406 untracked=13/6f4f55b6a727`, which matches the assigned identity
- Before/after identity: after the analysis the same command printed `head=754aed5 baseline=754aed5 files=58 diff=b9e37ea92406 untracked=13/6f4f55b6a727`. It is identical; there was no unexpected mutation.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md) and [analysis-001-reducer-probes](../evidence/analysis-001-reducer-probes.md); rechecked ANALYSIS-001, ANALYSIS-002 and ANALYSIS-003

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness recheck (ANALYSIS-001…003) | Reran probes P1–P6 against the Release build of the reviewed identity, adding P5b (racing-window boundary) and P9 (real pump end to end) | [analysis-002-reducer-probes.md](../evidence/analysis-002-reducer-probes.md) | ANALYSIS-001 and ANALYSIS-003 verified. ANALYSIS-002 is partly fixed: the two paths agree, but the P2 outcome still occurs end to end |
| Architecture: 2.6 unverified chip | Read the Core, pump and tray diffs and the tray tick order (`TrayApplication.cs:197-208`: drain, then pump `Apply`, then chips, then `Collect`) | Source observation | Shared Core policy (`SessionState.UnverifiedSince`, `PossiblyWaiting`, `UnverifiedGrace`, `AlertPolicy.UnverifiedText`). The provider adapter only sets the transient `ConfirmationSignalUnavailable` flag. The chip is outside `RequiresAttention` and `Collect`, so it is silent and not eligible for reminders or escalation. It is not a Codex-only fork. No finding |
| Architecture: 2A.6 held prompt | Read `ConfirmPrompt`, `ReevaluateHeldPrompt` and `CausalSessionReducer.Copy`; ran probe P6 with a human and an automatically reviewed permission; read the AskUserQuestion fixture timing | `tests/BellTower.Tests/Fixtures/hooks/claude-askuserquestion-auto.json`: `permission_prompt` arrives 6274 ms after the question's approval flow starts | The code implements the spec as written. The spec rule conflicts with "Permission is automatically approved": ANALYSIS-100 |
| Deviation 1 (transient flag still makes waits active for presentation) | Source reading of `SessionState.IsActive` against the reducer decisions | `SessionState.cs:66-70`; reducer decisions use only `IsConfirmed` and `StoredReason` | Acceptable as a one-tick bridge. Episode and announcement decisions do not read the flag. Residual effect: a chip can show briefly (at most one pump cadence), with no sound. No finding |
| Deviation 2 (2 s racing window, 15 s grace, 5 s gate) | Source reading, docs, tests and probe P5b | `SessionReducer.cs:476,528-534`; `docs/architecture.md:38`; P5b output | Named and documented. Tested at 0.4 s and 10 s but not at the boundary. P5b shows the window is inclusive at 2.000 s. Its justification is a Bell Tower guess, not a measurement. Not raised as a finding (recheck rule, and related to deferred ANALYSIS-004) |
| Deviation 3 (back-dated restored Codex waits) | Probe P10 (causal ordering and replay-window floor) | P10a, P10b | ANALYSIS-101 |
| Regression | `dotnet test -c Release --no-build --no-restore` | 957 passed, 0 failed | Passed for both providers. The fixtures include mixed-session tests |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <47 changed/untracked .cs files under src tests>` | exit 0 | Passed |
| Performance | Read the per-capture `ConfirmationEvidence.RecordProducer` read | Hypothesis only, not measured | One small-file read per accepted capture, bounded by the batch size of 128. Expected to be negligible. Not a finding |

Provider coverage of the checks:
- P1, P4–P6 use Claude.
- P2, P3, P9 and P10 use Codex.
- The full test suite covers Claude, Codex and the mixed-session fixtures.

## Findings

### ANALYSIS-100: The 2A.6 held-prompt re-evaluation confirms an automatically reviewed permission when the held prompt was the question's own notification

- Severity: warning
- Related task / requirement:
  - Tasks 2A.6 and 2.1.
  - attention-notifications, Question-tool approval flows remain question waits: the scenario "Question answered while an ambiguous prompt is held".
  - attention-notifications, Completion and permission grace: the scenario "Permission is automatically approved".
  - Design Decision 6.
- Locations:
  - `src/BellTower/Core/SessionReducer.cs:505-513`: the held prompt is recorded.
  - `src/BellTower/Core/SessionReducer.cs:557-573` (`ReevaluateHeldPrompt`)
  - `openspec/changes/confirm-claude-human-permission-waits/specs/attention-notifications/spec.md:58,72-74` (re-evaluation rule) versus `:12-14` (automatic approval)
- Observation:
  - The re-evaluation assumes that once the question resolves, the remaining permission is "the only possible source left". Resolving the question does not remove it as the source of a prompt that arrived earlier.
  - The retained Claude AskUserQuestion fixture shows the question's own approval flow emitting `permission_prompt` 6274 ms after it starts. So when a question and a permission are pending together, the question is a normal source of the held prompt.
  - Probe P6 (`autoReview`): the question is at 1 s and a parallel permission at 1.2 s. The prompt at 7.3 s is the question's own, and the question is answered at 20 s while the permission is still under automatic review. The permission is confirmed with `ConfirmedAt` set to 7.3 s, so it is eligible at once and produces one permission alert at 21 s. Automatic approval follows at 40 s.
  - This contradicts "Permission is automatically approved" (no chip or sound without verified human-wait evidence) and task 2.1 (an automatic review longer than 15 s produces no alert).
  - The code follows the spec text exactly. The conflict sits in the plan, which was based on my Analysis 001 option for P6. That option overlooked the question as a source.
  - The plan's premise may also not hold. If Claude presents one approval dialog at a time, a human permission would get its own `permission_prompt` after the question dialog closes, and the original P6 false negative would not occur. That premise is an unverified hypothesis.
- Proposed improvement and benefit:
  - The orchestrator and user decide between two options:
    - Keep 2A.6 and accept a rare false alert for an automatically reviewed sibling.
    - Revert to "a held prompt never confirms by elimination". A human permission would then rely on its own later prompt, which the 2 s racing window does not block when it arrives about 6 s after the question closes.
  - Either way, settle it with one isolated capture: Claude in default mode with AskUserQuestion plus a parallel tool that needs approval. Record whether each dialog emits its own `permission_prompt`, and at what times.
  - This removes a spec-level contradiction in a human-wait policy.
- Behavior-preservation risks:
  - Reverting changes the test `QuestionToolApprovalTests.ResolvingTheQuestionConfirmsTheOneOldEnoughPermissionFromTheHeldPrompt` and the five held-prompt tests in `HumanWaitCorrectionTests`.
  - The question episode and Codex behavior are unaffected, because Codex sends no hook prompt.
- Evidence type: measured result (probe P6), retained fixture timing, and source observation. The single-dialog presentation is a hypothesis.
- Verification / measurement plan: the isolated capture above, then a reducer test that records the chosen outcome for an automatically reviewed sibling.
- Relationship: follow-up to ANALYSIS-003 (P6 decision); the user decision is recorded in [findings](findings.md)

### ANALYSIS-101: A back-dated Codex runtime approval is reordered in the causal journal and can fall below its floor, so a restored wait is never confirmed

- Severity: warning (narrow trigger)
- Related task / requirement: tasks 2.5 and 4.1; Shared human-wait confirmation policy; deviation 3
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs:155-162`: `At = oldest + 1 ms`, and the ID is derived from `oldest`.
  - `src/BellTower/Core/CausalSessionReducer.cs:10-21`: the ID is added to `AppliedEvents` before the floor check.
  - `src/BellTower/Core/CausalSessionReducer.cs:80-89`: the journal is ordered by `At`.
- Observation:
  - Back-dating changes the observation's position in the time-ordered causal journal, not just its confirmation time.
  - P10a: when two provisional requests predate the tray run, the back-dated approval replays before the later request exists. The causal path confirms only the older request; the unordered path confirms both. In production this resolves on the next tick, because the pump republishes for the remaining request.
  - P10b: once more than `CausalSessionReducer.Window` (256) events follow the wait, the back-dated observation compares at or below the floor and is dropped. Its ID is already recorded as applied, and the pump republishes the identical deterministic ID every tick, so the restored wait stays provisional.
  - The session still counts as having a signal, so it gets neither the confirmed chip nor the unverified chip. It is silent while a person may be waiting.
  - The trigger is narrow: a tray restart while a Codex approval is pending and more than 256 journal events after it, for example from parallel or background activity.
- Proposed improvement and benefit:
  - Keep the journal position at observation time. Publish the runtime approval with `At = now` and carry the restore anchor as data, for example an additive `RestoredSince`. The reducer then sets `ConfirmedAt = Since + 1 ms` without reordering.
  - Alternatively, clamp `At` to after the session's causal floor.
  - Either change removes the P10a divergence and the P10b silent loss.
- Behavior-preservation risks:
  - Restored waits must still restore silently: `NotificationDue` must stay before `AlertPolicy` start.
  - The deterministic ID must still deduplicate republishing.
  - The capture protocol stays additive.
- Evidence type: measured result (probes P10a and P10b) plus source observation
- Verification / measurement plan: a reducer test that applies a back-dated runtime approval after `Window + 1` later events and to two pre-run requests, comparing the causal and unordered paths.
- Relationship: introduced by the ANALYSIS-002 fix (deviation 3)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=b9e37ea92406 untracked=13/6f4f55b6a727` | Probe P1: the legacy shape (no `adapterRevision`) now gives `Permission:Confirmed` and one alert, identical to the current shape. P1b: a legacy tool-bearing `PermissionRequest` stays provisional. `Normalize` is applied in `CaptureConsumer` and at reducer entry. The only other `Permission` producer is the pump, which stamps the revision. Doctor reports `ingressImage: outdated: rerun setup`. `LegacyIngressTests` pass in the full run. | verified (reducer and capture fixtures). The packaged update trial with a real cached image was not performed. The question-approval limitation on an outdated image is documented (deviation 4) |
| ANALYSIS-002 | same | Path divergence is fixed: P2 without a further runtime observation, and P3, give identical results on the causal and unordered paths. P3 now gets its own episode, is announced (due at +7 s) and stays visible after the question is dismissed. Still failing end to end: P2 with the observation the pump actually publishes, and P9 with the real `CodexPermissionPump`, fake runtime and `StateStore.Drain`, both on both paths. In P9, P1 is runtime-confirmed and dismissed; P2 then arrives while the runtime still reports a pending approval. `SubmitConfirmation` publishes for the oldest provisional wait (now P2, with a new ID). The `RuntimeApproval` branch (`SessionReducer.cs:487-497`) confirms every provisional permission, and `Confirm` (`:575-590`) opens a new episode. The dismissed chip returns with one re-announcement. `CodexAutoApprovedNewRequestCannotReannounceAnOlderWait` passes only because it omits the pump's second publication. | still failing (partially). The causal and unordered paths now agree, but a continuing positive status is still credited to newer requests. Suggested direction: confirm through the shared transition only when the status is attributable, for example when no confirmed permission wait is outstanding. The deterministic ID must then not be consumed while nothing is confirmed. This is the Codex counterpart of the Claude `OldEnoughToBeTheSource` gate |
| ANALYSIS-003 | same | P4: only the old-enough request is confirmed. P5: the young sibling stays provisional. P5b: the racing window holds siblings ambiguous up to and including 2.000 s and confirms at 2.001 s. The P6 decision is implemented as 2A.6 (see ANALYSIS-100). The window is named (`RacingResolutionWindow`) and documented in `docs/architecture.md:38`. | verified. The 2 s window is an unmeasured heuristic with no boundary test (not re-raised; related to deferred ANALYSIS-004) |

## Assessment and limitations

- Scope covered: the rechecks above, plus the architecture of 2.6, 2A.6 and deviations 1–3, for both clients. A full regression run passed (957/957), and format verification passed.
- 2.6 is shared Core policy with a thin adapter flag. It is silent, outside the reminder and escalation contract (`RequiresAttention`, `EffectiveConfirmation`), and dismissible. It clears on resolution, stop or end. The flag is refreshed every tick before rendering.
- Claude sessions are never flagged as signal-unavailable (deviation 5). That gap and the provider's environment-variable switch remain under deferred ANALYSIS-004.
- Decisions needed from the orchestrator:
  - ANALYSIS-100 needs a user policy decision, preferably after one isolated Claude capture.
  - ANALYSIS-002 still blocks Codex parity for the "newer request never re-announces" guarantee.
  - ANALYSIS-101 is narrow but loses a human wait silently.
- Not performed: live provider sessions, the installed app, the tray, audio, and the packaged update trial for ANALYSIS-001. No performance measurement was made; the per-capture producer read is a hypothesis only.
- Writes: only `evidence/analysis-002-reducer-probes.md`. The client refused the native report write, so this text is returned inline. The disposable probe project and its data directory were in the session scratch area outside the repository; the data directory was deleted after each run. No source, tests, docs, planning artifacts or `findings.md` were modified.
