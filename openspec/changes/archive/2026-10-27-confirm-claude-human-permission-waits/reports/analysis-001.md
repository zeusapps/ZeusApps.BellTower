> Saved by the orchestrator under the write fallback. Reviewer: code-analyzer subagent; client: Claude Code 2.1.310, subagent of the orchestrator session; model: `claude-opus-5-5`. The client refused the reviewer's native write of `reports/analysis-001.md` (the reviewer reported the refusal and, per its role file, did not retry; exact refusal message not returned). Text below is the reviewer's report, verbatim.

# Analysis report: confirm-claude-human-permission-waits / 001

## Review target

- Reviewer / role: code-analyzer subagent (Analysis 001) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310, subagent of the orchestrator session, role-default model `claude-opus-5-5`
- Date: 2026-10-26T08:22Z (UTC)
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits` / Analysis 001
- Task and requirement references:
  - Tasks 1.2, 2.1–2.5, 2A.2–2A.4, 3.1–3.3, 4.1–4.2.
  - [attention-notifications delta](../specs/attention-notifications/spec.md): Completion and permission grace; Shared human-wait confirmation policy; Question-tool approval flows remain question waits; Provider parity.
  - [session-tracking delta](../specs/session-tracking/spec.md): Provisional and confirmed provider approval correlation; Provider parity.
- Scope and relevant neighboring paths:
  - Core: `src/BellTower/Core/{SessionState,SessionReducer,WaitState,Observation,CausalSessionReducer}.cs`
  - Providers: `src/BellTower/Providers/{HookAdapter,CodexConnection,ICodexAttentionClient,CaptureConsumer}.cs`
  - Sessions: `src/BellTower/Sessions/CodexPermissionPump.cs`
  - Diagnostics and IO: `src/BellTower/Diagnostics/{ConfirmationCoverageReporter,CapabilityMatrix,DiagnosticsReporter}.cs`, `src/BellTower/IO/ConfirmationEvidence.cs`
  - Tests: `Core/{PermissionNotificationTests,QuestionToolApprovalTests}.cs`, `Diagnostics/ConfirmationCoverageTests.cs`, `Testing/{HookSequence,TestWorkspace}.cs`, `Fixtures/hooks/*`
  - Neighbors: `src/BellTower.Protocol/CaptureEnvelope.cs`, `src/BellTower/Setup/{IntegrationSetup,IngressProvision}.cs`, `src/BellTower/Core/AlertPolicy.cs`, `src/BellTower/IO/StateStore.cs`, `docs/{architecture,compatibility,hook-ingress,development}.md`
- Baseline commit: `754aed5`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs` printed:
  - HEAD `754aed525415d0cb3f47e8b6064e91fe1abdc618`
  - 40 modified and 10 untracked paths
  - tracked diff SHA-256 `6b6da7c0e29e4314c714756cab1683150eec0913a15d6c7a1bda256827cc15a3`
  - summary `head=754aed5 baseline=754aed5 files=50 diff=6b6da7c0e29e untracked=10/b02e4c1ab43a`, which matches the assigned identity
- Before/after identity: after the analysis the same command printed `head=754aed5 baseline=754aed5 files=50 diff=6b6da7c0e29e untracked=10/b02e4c1ab43a`. It is identical; there was no unexpected mutation.
- Prior reports / rechecked findings: none (first analysis)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture (shared Core vs adapters) | Read the Core diff for provider branching; checked adapter responsibilities against `docs/development.md` | The Core diff adds no `"claude"`/`"codex"` branch. Question-tool names, `HumanPrompt` and `ProviderDelay` live in `HookAdapter`. Question handling in `SessionReducer` is provider-neutral (`QuestionTool` flag). | Question-tool handling is shared policy for both clients. Confirmation itself has two mechanisms (persisted hook confirmation and a transient runtime flag): ANALYSIS-002 |
| Correctness: correlation and receipts | Read `ConfirmPrompt`, `Confirm`, `IsStale`, `ApproveQuestionTool` and `AddWait`; ran disposable probes P2–P7 | [analysis-001-reducer-probes.md](../evidence/analysis-001-reducer-probes.md) | ANALYSIS-002, ANALYSIS-003, ANALYSIS-007. No invented IDs found. Receipts and `CompletedOperations` correctly reject a late question approval after PostToolUse and after Stop. |
| Persistence and compatibility | Read `WaitState` (extension data), `SessionState.ResolvedApprovals`, `CausalSessionReducer.Copy`, `CaptureEnvelope` and ingress provisioning; ran probe P1 | Only `IntegrationSetup` calls `IngressProvision`; hooks call an immutable `integrations/provider-ingress/1/<sha256>/` image. See probe P1, `docs/hook-ingress.md` line 17, and its statement "Existing images are retained for cached hooks". | An older consumer reading new output is handled. A new consumer reading old ingress output is not: ANALYSIS-001. `WaitState` unknown fields round trip. `ResolvedApprovals` survives an old writer through `SessionState.AdditionalData`. Rollback drops per-wait confirmation, as the developer documented. |
| Constants (5 s floor, 30 s window) | Fetched primary docs via Context7 (`/websites/code_claude`, hooks page, Notification and PermissionRequest sections) and compared them with the code and the handoff claims | Claude docs say `permission_prompt` fires "once you haven't typed for about six seconds"; the timer starts when the prompt appears; keystrokes defer it; it does not run when the request is answered sooner; `CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS=1` disables it | ANALYSIS-004 |
| Duplication and simplification | Compared the hook-confirmation and runtime-confirmation due/notification math and how `ConfirmationCredit` is derived | `SessionState.cs` lines 59–94; `SessionReducer.WithConfirmation` | ANALYSIS-005 |
| Performance | Read the `ActiveWaits()` allocation in `Reason`/`Since`/`Due`/`NotificationDue`, the pump RPCs, and the per-prompt evidence read/write | Hypothesis only, not measured | `ActiveWaits()` allocates a small list several times per `RequiresAttention`/`NotificationDue` call. With a handful of waits per session the cost should be negligible; this is a hypothesis, not a finding. Extra pump RPC: ANALYSIS-006 |
| Diagnostics honesty | Read `ConfirmationCoverageReporter`, `ConfirmationEvidence`, `CaptureConsumer` and the pump's evidence writes | Source observation | ANALYSIS-006 (Codex "observed" meaning), ANALYSIS-001 (outdated ingress reported as "not yet observed"), ANALYSIS-004 (environment-variable disablement) |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <40 changed/untracked .cs files>` | exit 0 | Passed (shared Claude/Codex code) |
| Build and tests | Not run by the analyzer | The developer reported 919/919; QA 001 owns functional checks | Not performed, for proportionality. The probes used the developer's Release build, which is newer than the last source edit. |

Provider coverage of the checks:
- P1 and P4–P7 use Claude observation shapes.
- P2 and P3 use Codex, with the owning-runtime flag set exactly as the pump sets it. P3 also runs the Claude analog.
- No finding affects mixed simultaneous sessions, because every finding concerns per-session state.

## Findings

### ANALYSIS-001: A new consumer silently loses Claude permission confirmation when hooks still invoke an older cached ingress image

- Severity: critical
- Related task / requirement:
  - Tasks 1.2, 3.1, 4.2.
  - Shared human-wait confirmation policy: "Existing native APIs and snapshot data SHALL be migrated safely".
  - Design: "New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed".
- Locations:
  - `src/BellTower/Providers/HookAdapter.cs:89-92`: the new fields are set only by the producer.
  - `src/BellTower/Core/Observation.cs:26-34`: `bool HumanPrompt` defaults to false, so "absent" and "false" look the same.
  - `src/BellTower/Core/SessionReducer.cs:117-125`: a `Permission` observation without `HumanPrompt` becomes a provisional wait.
  - `src/BellTower.Protocol/CaptureEnvelope.cs`: the observation is built in the ingress process.
  - `src/BellTower/Setup/IntegrationSetup.cs:41`: only setup provisions the ingress image.
  - `docs/hook-ingress.md:17`
- Observation:
  - The observation, including the new `humanPrompt`/`questionTool`/`providerDelay` fields, is built by `HookAdapter.Parse` inside the NativeAOT ingress.
  - Hooks run an immutable image under `integrations/provider-ingress/1/<sha256>/`. Only `setup` provisions it; updates do not refresh it, and "existing images are retained for cached hooks".
  - After the tray updates, an image built from 0.1.8 still sends `permission_prompt` as `Kind=Permission` with no tool fields and no `humanPrompt`.
  - The new reducer turns it into a second provisional permission. Nothing ever confirms it, so Claude permission waits never alert.
  - Probe P1 runs the same sequence twice: the new shape gives one alert, the old shape gives none.
  - Doctor's `confirmationCoverage` then says Claude is "configured, not yet observed", which hides the cause.
  - `docs/hook-ingress.md` covers only the opposite direction ("an older consumer ignores them").
- Proposed improvement and benefit:
  - Make the producer version visible. For example, make `HumanPrompt` nullable, or add an adapter-revision field to the observation or envelope.
  - For captures from a legacy revision, either re-derive the signal or report the problem. Re-deriving works because a legacy `Permission` with neither a tool ID nor a fingerprint is the old `permission_prompt` mapping. Reporting means doctor and the capability rows say "ingress image predates human-wait confirmation; rerun setup".
  - State the required setup refresh in the release handoff.
  - This prevents a silent loss of Claude permission alerts on the default upgrade path.
- Behavior-preservation risks:
  - Re-deriving from the legacy shape must not turn a tool-less legacy `PermissionRequest` into a confirmation. Claude's `PermissionRequest` always carries `tool_name`, so it has a fingerprint whenever `tool_input` is present.
  - The capture protocol must stay at version 1, and the change must remain additive.
- Evidence type: measured result (probe P1) plus source observation
- Verification / measurement plan:
  - Add a capture-protocol fixture: an old-shape `permission_prompt` envelope consumed by `CaptureConsumer`. Assert either a confirmation or a doctor "ingress outdated" status.
  - Run the isolated packaged update trial with an image provisioned before the update.
- Relationship: none

### ANALYSIS-002: Codex runtime confirmation is not stored in the shared wait state, so episode handling diverges by provider and by reducer path

- Severity: warning
- Related task / requirement:
  - Tasks 2.5, 2A.3, 4.1.
  - Completion and permission grace: "distinct episodes SHALL remain independent".
  - Shared human-wait confirmation policy: "normalize their supported approval signals into the same provisional, confirmed, resolved and unverified states".
  - Design Decision 1 and the "Signal differences fork the policy" risk.
- Locations:
  - `src/BellTower/Core/SessionState.cs:56-60`: `IsActive`, `ActiveWaits` and `Reason` read `NativePermissionPending`, which is `JsonIgnore`d.
  - `src/BellTower/Core/SessionReducer.cs:164`, `:410-419`, `:517-520`: reducer decisions use `session.Reason` and `IsActive`.
  - `src/BellTower/Core/CausalSessionReducer.cs:44-61,91-116`: replay sessions are built by `Copy`, which leaves out the native flags; they are put back only after replay.
  - `src/BellTower/Core/SessionState.cs:84-94`: `NotificationDue` counts only waits of the current episode.
  - `docs/architecture.md:38`
- Observation:
  - For Claude, hook confirmation is a stored per-wait state change made by the reducer, and `Confirm()` moves the episode to the confirmed wait.
  - For Codex, runtime confirmation is a temporary session flag read only when attention is computed. The reducer cannot see it during causal replay, which is the production capture path, but can see it on the unordered path.
  - Probe P2 (Codex): a newer provisional request brings back and re-announces an older, already dismissed, natively confirmed wait on the causal path. It does not on the unordered path.
  - So the guarantee in `docs/architecture.md` ("a newer unconfirmed or auto-approved request never re-announces or un-dismisses an older wait") holds only for Claude. `AutoApprovedNewRequestCannotReannounceAnOlderWait` has no Codex variant.
  - Probe P3: when a question owns the episode, a natively confirmed Codex permission gets `NotificationDue = null`, so it is never announced. If the question chip was dismissed, the permission disappears entirely.
  - In the Claude version of P3 the permission gets its own episode and announcement. The developer's limitation note understates this ("shares that episode").
- Proposed improvement and benefit:
  - Make the reducer's internal decisions depend only on stored state. For example, use an internal hook-only actionability check in `AddWait`, `Confirm` and episode restore. The replay and unordered paths would then agree.
  - Route Codex runtime confirmation through the same shared transition. For example, when the pump first sees `waitingOnApproval` for a provisional wait, apply the same confirm-and-episode step `Confirm()` performs, keyed to the current attachment and episode.
  - Keep the temporary flag only for freshness and suppression.
  - This gives one confirmation policy for both clients, as design Decision 1 requires.
- Behavior-preservation risks:
  - Keep the existing Codex safety properties: a fresh negative status suppresses attention, and restored waits are not replayed after restart.
  - The pump's verification scope is keyed by episode, so moving the episode changes the pump's `since` logic.
  - The 0.1.8 baseline already re-announced Codex waits on every new request, so P2 is a parity gap rather than a regression. P3 is new behavior.
- Evidence type: measured result (probes P2, P3) plus source observation
- Verification / measurement plan:
  - Add Codex variants, with `IngressAt` set, of `AutoApprovedNewRequestCannotReannounceAnOlderWait` and of the question-plus-permission case. Set the native flag the way `CodexPermissionPump.Apply` does.
  - Add a test asserting that the causal and unordered paths give the same episode for the same sequence.
- Relationship: none

### ANALYSIS-003: Unattributed prompt correlation ignores the documented six-second gate and contradicts the documented 30 s receipt rule

- Severity: warning
- Related task / requirement:
  - Tasks 2.3, 2.4, 2A.3.
  - session-tracking "Two parallel approvals": the resolved request stays resolved and "only supported current human-wait evidence can alert".
  - attention-notifications "Question and permission pending together".
- Locations:
  - `src/BellTower/Core/SessionReducer.cs:458-508` (`ConfirmPrompt`)
  - `src/BellTower/Core/SessionReducer.cs:527-540`: `IsStale` is checked only when no candidate wait is pending.
  - `docs/architecture.md:38`: "an unattributed prompt within 30 s of a resolution ... is ignored".
- Observation:
  - Claude's hooks documentation says `permission_prompt` fires only after the prompt has waited about six seconds. The timer starts when the request arrives, keystrokes defer it, and it does not fire if the request is answered sooner.
  - The reducer ignores this timing: any pending permission counts as a candidate.
  - Probe P4: a request that started 1.5 s before the prompt is confirmed together with the request that can actually be the prompt's source.
  - Probe P5: P1 is answered at 6.0 s and its prompt arrives in a race at 6.4 s. The prompt confirms P2, which started only 0.9 s earlier and may still be under automatic review. A resolution receipt exists within 30 s, yet the architecture doc says such a prompt is ignored.
  - That false confirmation hits exactly the automatic-review case this change is meant to fix.
  - Probe P6: when a question and a permission are pending together, the prompt stays ambiguous, as the spec requires. But once the question is answered, the permission never alerts, because Claude sends only one prompt per request. That human-wait false negative needs to be accepted explicitly or fixed.
- Proposed improvement and benefit:
  - Use the documented gate as a correlation rule; this invents no identity. A wait whose `Since` is later than `prompt.At - ClaudePermissionPromptDelay` cannot be the prompt's source.
  - Apply the same receipt-window check when a resolution receipt falls inside that window and other candidates remain: mark them ambiguous rather than confirmed.
  - This removes the P4 and P5 false confirmations and brings the code in line with `docs/architecture.md`.
  - For P6, the orchestrator needs to decide between:
    - keeping the spec's "ambiguous, assign nothing"; or
    - re-checking the ambiguous prompt when the question resolves before the permission. Under the documented gate, that permission is then the only possible source left.
- Behavior-preservation risks:
  - The 5 s floor is below the documented ~6 s, so genuine prompts still match.
  - Codex sends no hook prompt, so it is unaffected.
  - The same-fingerprint "confirmed as a set" tests keep passing when both requests are older than the floor.
- Evidence type: measured result (probes P4–P6) plus primary documentation (code.claude.com/docs/en/hooks, Notification and PermissionRequest sections, fetched 2026-10-26 via Context7)
- Verification / measurement plan:
  - Reducer test for P4: the newer sibling stays provisional.
  - Reducer test for P5: a racing prompt after a resolution, with a sibling pending, does not confirm the sibling.
  - A test for P6 that records the chosen decision.
- Relationship: none

### ANALYSIS-004: The 5 s floor and 30 s window are recorded as undocumented, though the floor has a primary source; a documented way to disable the signal is not reported

- Severity: suggestion
- Related task / requirement: tasks 3.1, 3.4, 4.2; "Native approval verification becomes unavailable"; "Claimed provider restriction"
- Locations:
  - `src/BellTower/Providers/HookAdapter.cs:9-11`
  - `src/BellTower/Core/SessionState.cs:48-51`
  - `tests/BellTower.Tests/Providers/ProviderTests.cs:155` (`ClaudePermissionPromptDelay >= HumanWaitGrace`)
  - `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs:24-35`
  - `src/BellTower/Diagnostics/CapabilityMatrix.cs:22,66-67`
  - Developer handoff, Limitations: "no provider documents either (Claude hook docs list no delay)"
- Observation:
  - The Claude hooks documentation does document the delay: about six seconds, deferred by keystrokes, and skipped if the request is answered sooner. The 5 s floor is therefore backed by a primary source, not only by the single 6.27 s capture. The handoff statement is inaccurate.
  - The floor equals `HumanWaitGrace`, so its only effect is "eligible on arrival". A test enforces this coupling, but the code does not express it: changing the shared grace would silently add a Claude debounce.
  - The 30 s window has no documented basis. Per the docs, a late prompt can only come from a race at about 6 s, so the window is a Bell Tower heuristic and should be named as one.
  - The docs also name `CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS=1`, which turns `permission_prompt` off. Under the new policy that silently disables Claude permission alerts, and neither doctor nor the capability matrix mentions it.
- Proposed improvement and benefit:
  - Cite the documentation next to the floor, in `CapabilityMatrix.ClaudeConfirmationEvidence`, and in `docs/compatibility.md`.
  - Express the floor relative to the shared grace, or document why it must not exceed the documented delay.
  - Record the 30 s window as a heuristic, with its rationale and a boundary test.
  - Add the environment-variable switch to the Claude coverage `Configured`/`Status` text as a known way the signal becomes unavailable. Doctor does not need to read the user's environment; naming the switch is enough.
  - This makes diagnostics honest and removes a hidden coupling.
- Behavior-preservation risks: none for runtime behavior, since these are documentation and diagnostics text changes. The boundary test must use isolated data.
- Evidence type: primary documentation plus source observation
- Verification / measurement plan: review the updated text, and add reducer tests at 29.9 s and 30.1 s after a resolution.
- Relationship: related to ANALYSIS-003 (same documented gate)

### ANALYSIS-005: Two due-time formulas express one shared grace; the stored credit can be replaced by a single human-wait start time before release

- Severity: suggestion
- Related task / requirement: tasks 1.2, 2.2; "shared five-second human-wait grace SHALL account for an already elapsed verified provider notification delay"
- Locations:
  - `src/BellTower/Core/SessionReducer.cs:428-438`: `WithConfirmation` stores `ConfirmedAt` and `ConfirmationCredit = max(elapsed, delay)`.
  - `src/BellTower/Core/SessionState.cs:69-73` (`HookDue`, `PermissionDue`) and `:84-94` (`NotificationDue` repeats both).
  - `src/BellTower/Core/WaitState.cs:19`
- Observation:
  - Hook confirmation computes `ConfirmedAt + max(0, Grace - credit)`.
  - Runtime confirmation computes `anchor + Grace`, where the anchor is `NativePermissionSince` or the wait start.
  - Both mean the same thing: the human wait began at T, and it is due at `max(evidence time, T + Grace)`.
  - The credit can be derived from `Since`, `ConfirmedAt` and the provider delay.
  - So two stored fields and two formulas, each repeated in `Due` and `NotificationDue`, express one rule.
- Proposed improvement and benefit:
  - Store one `HumanWaitSince`. For hook evidence it equals `min(wait.Since, ConfirmedAt - ProviderDelay)`.
  - Feed the runtime's first-report time into the same field.
  - Compute one formula for both providers: `due = max(ConfirmedAt, HumanWaitSince + HumanWaitGrace)`.
  - This stores fewer fields, removes the duplicated `Due`/`NotificationDue` branches, and gives one shared-policy formula.
  - It is cheap to do now because these fields have not shipped, and it fits naturally with ANALYSIS-002.
- Behavior-preservation risks:
  - The current tests on exact due times must keep passing: 6.274 s eligible on arrival, confirmation-only eligible at arrival, and Codex due 5 s after its first report.
  - Restored waits stay silent only because they are anchored on `EpisodeStarted`; that must not change.
- Evidence type: source observation
- Verification / measurement plan: the existing `PermissionNotificationTests` due-time cases plus the restart round trip
- Relationship: related to ANALYSIS-002

### ANALYSIS-006: The pump polls question sessions with a second RPC whose result no production code uses, and Codex "observed" only means the runtime answered

- Severity: suggestion
- Related task / requirement: tasks 2A.4, 3.1, 3.3
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs:63`: sessions with only a question pending are now polled.
  - `src/BellTower/Sessions/CodexPermissionPump.cs:107-126`: a second `thread/read` via `ReadUserInputPendingAsync`; evidence is written on any non-null status.
  - `src/BellTower/Providers/CodexConnection.cs:22-36`: two calls for one thread status.
  - `src/BellTower/Core/SessionState.cs:36-46`: `NativeUserInputPending` is read only by `EffectiveConfirmation`, which has no production caller.
  - `src/BellTower/Providers/CaptureConsumer.cs:57-60`: the `ClaudeSignal` label is hard-coded for any provider, and the diagnostics write inside the drain is not guarded.
  - `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs:59-70`: counts the raw `Confirmation`, not `EffectiveConfirmation`.
- Observation:
  - Every second, each Codex session with a pending question now costs one connection and two `thread/read` calls. Both flags come from the same `status.activeFlags`. The second result is used only by `EffectiveConfirmation`, which only tests call.
  - The pump records Codex "observed" evidence whenever the runtime answers at all, including `waitingOnApproval=false` for a question-only session. In doctor, "observed" therefore means a real human wait for Claude but only that the runtime was reachable for Codex.
  - `CaptureConsumer` labels any `HumanPrompt` with the Claude signal.
  - `CaptureConsumer` also does not guard the diagnostics write the way the pump does. A temporary IO failure on the evidence file therefore aborts the drain commit until the next retry.
  - The cost figures are an unmeasured hypothesis. They are bounded by the existing 1 s cadence and a concurrency of 4.
- Proposed improvement and benefit:
  - Read both flags from one `thread/read`, for example with one method returning `(approval, userInput)`. Alternatively, defer question polling until the reminders change consumes it.
  - Write Codex evidence only when `waitingOnApproval == true`, which is the actual human-wait signal. Alternatively, rename the field to "runtime status reachable".
  - Pass the signal label in from the adapter or provider instead of hard-coding Claude.
  - Wrap the consumer's evidence write the same way the pump's is wrapped.
  - This removes redundant RPCs, makes "observed" mean the same thing for both providers, and keeps diagnostics from affecting ingestion.
- Behavior-preservation risks:
  - `ICodexAttentionClient` fakes rely on the default interface method, so a combined method needs the same kind of default.
  - Runtime status must still never gate a question.
- Evidence type: source observation; performance hypothesis (unmeasured RPC cost)
- Verification / measurement plan:
  - A fake-client test that counts `thread/read` calls per verification.
  - Coverage tests for the Codex "observed" status when `waitingOnApproval=false`.
  - Optionally, count loopback calls per minute with one question session in an isolated runtime.
- Relationship: none

### ANALYSIS-007: A question approval hook without correlation data creates a second question episode

- Severity: suggestion
- Related task / requirement: task 2A.2; "Question-tool approval flows remain question waits" (SHALL NOT restart timing, announcement or dismissal receipts)
- Locations:
  - `src/BellTower/Core/SessionReducer.cs:442-456`: `ApproveQuestionTool` falls through to `AddWait`.
  - `src/BellTower/Core/SessionReducer.cs:395-408`: the adoption and duplicate checks need a fingerprint.
- Observation:
  - Suppose a question-tool `PermissionRequest` has neither `tool_use_id` nor `tool_input`, so it has no fingerprint.
  - `Targets` then matches nothing, and `AddWait` creates a second question wait with a new episode.
  - That re-announces the question and loses its dismissal (probe P7).
  - Current Claude documentation and captures include `tool_input`, so this is a robustness gap, not a case seen in the field.
- Proposed improvement and benefit: an unattributed question approval should count as evidence for the pending question when exactly one is pending, and be ignored otherwise. It should never add a wait while a question is already pending. This removes one re-announcement path.
- Behavior-preservation risks: when the approval arrives before PreToolUse and no question is pending, it must still create the question.
- Evidence type: measured result (probe P7)
- Verification / measurement plan: a reducer test with a fingerprint-less question approval while one question is pending
- Relationship: none

## Re-verification

None (first analysis; no rechecks assigned).

## Assessment and limitations

- Scope covered: the full Core, provider, pump and diagnostics diff and the new files listed above, for both clients.
  - Question-tool handling is shared policy with thin adapter inputs; there is no per-provider fork.
  - No invented operation IDs were found.
  - Causal receipts and `CompletedOperations` prevent a resolved wait from coming back after resolution, stop, interrupt, end, a new prompt or a replacement attachment. This holds in the cases the developer tested and in my probes.
- Main risks:
  - ANALYSIS-001 (critical): on the default upgrade path, Claude human-permission alerts stay off until setup re-provisions the ingress image.
  - ANALYSIS-002: Codex confirmation lives outside the shared reducer. The documented episode guarantees hold only for Claude, and outcomes depend on which reducer path runs.
  - ANALYSIS-003: false confirmations of auto-reviewed sibling requests, plus one spec-level false negative that needs a decision.
- Persistence:
  - The new `WaitState` fields round trip through `JsonExtensionData`.
  - Old snapshots read as unverified.
  - `ResolvedApprovals` survives an older writer through `SessionState.AdditionalData`.
  - One residual risk, not raised as a finding because the existing `AttentionReason` enum has the same pattern: the new stored `HumanConfirmation` enum is written as a string, so a value added by a future version would fail to deserialize in this version. Declare that before claiming rollback compatibility.
- Performance: nothing was measured. The `ActiveWaits()` allocations and the pump RPC cost are hypotheses, with measurement plans above.
- Not performed:
  - Build and full test run. QA 001 owns these; the developer reported 919/919.
  - Live provider sessions, the installed app, the tray and audio. None were touched.
  - The packaged update trial for ANALYSIS-001.
- Writes:
  - Only `evidence/analysis-001-reducer-probes.md` and this report text.
  - The disposable probe project is in the session scratch directory, outside the repository.
  - No source, tests, docs, planning artifacts or `findings.md` were modified.
- Decisions needed from the orchestrator:
  - Treat ANALYSIS-001 as release-blocking, or require a setup refresh in the release procedure.
  - The P6 ambiguity policy (ANALYSIS-003).
  - Whether `EffectiveConfirmation` and the pump's question polling ship now or with the reminders change (ANALYSIS-006).
