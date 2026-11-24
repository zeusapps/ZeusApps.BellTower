> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-051); client: Claude Code 2.1.331, subagent; mode: non-interactive, auto mode; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-051 (DEV-063, task 4.11)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331 / subagent, non-interactive, auto mode / claude-opus-5-5
- Date: 2026-11-23T22:33Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-051
- Task and requirement references: task 4.11 ([tasks.md](../tasks.md)); attention-notifications "Viewed wait timer" ([delta spec](../specs/attention-notifications/spec.md)); design decision 13 ([design.md](../design.md)); [implementation-063](../evidence/implementation-063.md)
- Scope and relevant neighboring paths: `src/BellTower/Hosts/NavigationReasons.cs`, `src/BellTower/Tray/NavigationOutcome.cs`, `src/BellTower/Tray/TrayApplication.cs`, `tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs`; neighbors `src/BellTower/Hosts/FocusResult.cs`, `src/BellTower/Hosts/Navigator.cs`, `src/BellTower/Tray/NavigationNotice.cs` (the assignment named it under `Hosts`; it is under `Tray`), plus `src/BellTower/Hosts/WindowsTerminalNavigator.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/Providers/AgentModeClassifier.cs`, `src/BellTower/Core/ConversationLifecycle.cs`
- Baseline commit: 76beb52
- Implementation identity (before): `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 76beb52 -Paths src tests docs vscode` printed HEAD 76beb5216df7, manifest `M` for the 4 scoped files, tracked diff SHA-256 `36ac12ee1b4d9eb15843435092b470373681aefa35fefa1155bbce45cd8b3693`, no untracked files. Summary: `head=76beb52 baseline=76beb52 files=4 diff=36ac12ee1b4d untracked=0/e3b0c44298fc` (matches the expected value)
- Before/after identity: after: `head=76beb52 baseline=76beb52 files=4 diff=36ac12ee1b4d untracked=0/e3b0c44298fc`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: none rechecked (new analysis)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication | Inspection of the diff and neighbors | One classifier (`NavigationReasons.IsPermanentLimit`) next to the constant it compares; one consumer (`NavigationOutcome.ApplyViewed`); `TrayApplication.Navigate` is the only caller of `ApplyViewed` (grep) | No duplicated policy. Tests repeat two production literals ("No verified terminal destination is registered.", "Windows declined foreground activation."); not reported, no defect |
| Complexity / simplification | Inspection | Guard `!(result.Success \|\| IsPermanentLimit(result)) \|\| !hideTimer \|\| episode is null`; `IsPermanentLimit` already includes `!Success` | Minimal and readable; no simplification worth the churn |
| Architecture | Judged against `docs/development.md` layers | The classifier is in `Hosts` (owner of click reasons and `FocusResult`); the decision stays in `Tray/NavigationOutcome`; the episode check stays in `StateStore.MarkViewed` | Consistent with the layer boundaries; no drift |
| Fragility of the text comparison (developer question) | Traced every producer of a `FocusResult` reaching `ApplyViewed` (grep `FocusResult`, `NavigationReasons.`, `Deserialize<FocusResult>`) | `SharedBackend` comes from one in-process source: `Navigator.FocusAsync` short-circuits through `ForSharedConversation` before any helper or bridge runs. The only deserialized result (`WindowsTerminalNavigator.cs:50`) belongs to a process-bound attachment and never carries this text, and the VS Code bridge does not either (repo grep). `Normalized()` (QA-5304) rewrites only a null or blank message, so this text passes through unchanged | See ANALYSIS-5100: low fragility, fails safe, pinned by tests. A typed field is not warranted now |
| Classification completeness, both providers | Traced every refusal in `Navigator.FocusAsync`, plus mode and host learning in `AttachmentEvidence.Merge` and `AttachmentStore` | An unknown mode is learned later (`AttachmentEvidence.cs:169`). An unknown host kind is learned from a later capture (`AttachmentStore.cs:94-96`). A dead process ends the session through the lifecycle. A headless run never gains a host (`NativeAdoption.cs:87-96`) | The classification is correct for the stated limits. Headless runs are a permanent limit that is not stated, and the evidence misdescribes them (ANALYSIS-5101). For Claude, the shared backend cannot be reached (ANALYSIS-5101) |
| Error path introduced by widening | Inspection of `TrayApplication.Navigate` | The catch at `TrayApplication.cs:436-440` sets "Session focused; the timer preference could not be saved." That branch is now reachable for a refused, permanent-limit click | ANALYSIS-5102 |
| Performance | Inspection | One ordinal string comparison per chip click | Not applicable; nothing to measure |
| Parity | Inspection of tests and classifier signature | The classifier takes a `FocusResult` that carries no provider value. In `TemporaryFailures` the provider parameter is discarded (`_ = provider;`, test line 137), so 12 cases reduce to 6 distinct inputs | One rule for both clients. The per-provider temporary cases add no coverage beyond the provider-neutral rule. Covered in ANALYSIS-5101 as an evidence-accuracy point |
| Build / tests / format | Not run | QA-058 owns build and tests | Not performed by assignment |

## Findings

### ANALYSIS-5100: The permanent limit is recognized by its message text; a typed reason is not yet warranted

- Severity: suggestion (non-blocking)
- Related task / requirement: 4.11; "Viewed wait timer" scenario "A chip click shows a permanent navigation limit"
- Locations: `src/BellTower/Hosts/NavigationReasons.cs:42-43` (`IsPermanentLimit`), `src/BellTower/Hosts/Navigator.cs:20-22` (sole producer), `src/BellTower/Hosts/FocusResult.cs` (`Normalized`), `src/BellTower/Tray/NavigationNotice.cs` (`RunAsync`)
- Observation: the classifier compares `result.Message` ordinally with the `SharedBackend` constant. The developer's three concerns, checked against the reviewed identity:
  - Deserialized results: this result never crosses a process boundary. `Navigator` builds it in process from the same constant before any host navigator runs. The helper JSON at `WindowsTerminalNavigator.cs:50` and the VS Code bridge serve only process-bound attachments, and neither produces this text.
  - Normalization (QA-5304): `Normalized()` replaces only a null or blank message, so it does not change this result.
  - Future wording: the producer and the classifier both reference the same constant, so a wording edit changes both. The real risk is a future edit that builds the message instead of passing the constant, for example appending a version or route hint, localizing it, or formatting it per provider. That would silently stop matching. The failure is safe, because the timer stays visible, which is today's behavior. `AStatedPermanentLimitHidesTheCurrentEpisodeTimerForEitherProvider` (test line 102) runs the real `Navigator` and asserts both the exact message and the hide, so such a change would fail a test.
  - Cost of the alternative: a typed field, for example an init-only `bool PermanentLimit` defaulting to false or a reason enum on `FocusResult`, would add a property to a record that is also deserialized from helper output. It would also have to be kept out of `Normalized`, `LogCode` and the helper contract. That cost buys nothing while only one cause exists.
- Proposed improvement and benefit: keep the current form. Add a typed reason when a second permanent cause is introduced (see ANALYSIS-5101, headless) or when the shared-backend message starts being built rather than passed as the constant. Then set the reason only in `Navigator`, give it a false default so helper JSON cannot claim it unless it is explicitly added, and leave `Message` as presentation only. Optional now: add one line to the `IsPermanentLimit` remarks saying the comparison relies on the result being passed through unchanged.
- Behavior-preservation risks: none for the current code. A future typed field must default to false so older helper payloads keep the timer.
- Evidence type: source observation
- Verification / measurement plan: the existing test at line 102. If a typed reason is added later, add a test that deserializes a helper payload without the field and asserts the timer stays visible.
- Relationship: answers the developer's question in DEV-063

### ANALYSIS-5101: The classification is correct for stated limits; the evidence misdescribes headless runs and the Claude coverage

- Severity: suggestion (non-blocking)
- Related task / requirement: 4.11; parity policy (AGENTS.md); [implementation-063](../evidence/implementation-063.md) classification table
- Locations: `src/BellTower/Hosts/Navigator.cs:31-32`, `src/BellTower/Core/NativeAdoption.cs:87-96`, `src/BellTower/IO/AttachmentStore.cs:94-96`, `src/BellTower/Core/AttachmentEvidence.cs:169`, `src/BellTower/Providers/AgentModeClassifier.cs:14`, `tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs:90-117,120-139`
- Observation:
  - Correct as implemented:
    - `SharedBackend` is permanent. A stored mode other than unknown is never replaced on a non-replacement merge, and only a different launch creates a navigable session.
    - `TerminalUnverified` (unknown mode) can resolve: `AttachmentEvidence.Merge` adopts a later known mode.
    - "No verified terminal destination is registered." can resolve for a direct capture: `AttachmentStore` upgrades an `unknown` host kind to `vscode` or `windows-terminal` from a later capture.
    - `ProcessUnverified`, declined activation, a missing session and a navigator failure keep the timer, as the spec requires.
  - Gap 1: a headless run (`claude -p`, `codex exec`) always gets `Host = unknown` (`NativeAdoption.cs:92`), so its click always returns the generic "No verified terminal destination is registered." That is a permanent no-terminal condition for both providers. The evidence table's reason for keeping the timer ("Host kind may be registered or learned later") is wrong for this case. The behavior is still allowed: the spec requires a stated limit, and the generic text does not state one. The practical impact is small, because a headless wait normally ends when the process exits.
  - Gap 2: for Claude, a shared-backend conversation cannot be reached. `MayShareBackend` is true only for `codex`, and an unknown-mode Claude capture is direct (`NativeAdoption.cs:98`). The Claude case in the permanent-limit test therefore runs on a synthetic attachment, and the message names Codex.
  - The `TemporaryFailures` provider parameter is discarded, so the "for either provider" temporary cases are 6 provider-neutral inputs shown twice.
  - None of this is a parity defect: the rule is provider-neutral and has no per-provider branch. The evidence should say that, today, the only reachable permanent limit is Codex's shared backend.
- Proposed improvement and benefit:
  - Correct the classification table in `implementation-063.md`: separate headless (permanent but not stated, so the timer is deliberately kept) from an unknown host (learned later).
  - State that the Claude permanent-limit case checks the shared rule on a synthetic attachment, because Claude has no reachable permanent limit.
  - Optionally, as a later product decision and not in this task: give headless clicks a stated reason. They would then qualify through ANALYSIS-5100's typed reason.
  - This gives honest capability and acceptance evidence for task 6.1, which names only "timer after a shared-backend click".
- Behavior-preservation risks: documentation-only recommendation; no code change proposed for this task.
- Evidence type: source observation
- Verification / measurement plan: re-read the corrected evidence table. Task 6.1 trial for plain default `codex` (Windows Terminal and VS Code) covers the reachable case.
- Relationship: none

### ANALYSIS-5102: A failed viewed-save after a permanent-limit click reports "Session focused"

- Severity: warning (non-blocking)
- Related task / requirement: 4.11; "Viewed wait timer"
- Locations: `src/BellTower/Tray/TrayApplication.cs:427-440` (status text in the `catch` after `NavigationOutcome.ApplyViewed`)
- Observation: before DEV-063, `ApplyViewed` reached `store.MarkViewed` only for a successful result, so the fallback status "Session focused; the timer preference could not be saved." was true. Now `MarkViewed` also runs for a refused shared-backend click. If it throws an `IOException`, `UnauthorizedAccessException` or `JsonException`, the overview status replaces the stated limit (set on line 427) with a false claim that the session was focused. The own notice still shows `result.Message` (line 449), so the user still sees the true reason. The overview status is wrong in this rare I/O-failure path, for both providers' shared conversations.
- Proposed improvement and benefit: choose the fallback text by `result.Success`. For a refused result, keep `result.Message` and add only the save-failure clause, or use a neutral text such as "The timer preference could not be saved." This is a one-line change in the same task that keeps the status truthful.
- Behavior-preservation risks: none for the success path. The log line `viewed-wait-save-failed` is unchanged.
- Evidence type: source observation
- Verification / measurement plan: inspection, or a unit test if the status selection is moved into `NavigationOutcome` (for example a `SaveFailedStatus(FocusResult)` helper). A store that fails to write is needed to run the tray path. Do not use the live tray.
- Relationship: none
- Blocking rationale: not blocking. The path needs a state-file write failure, the notice still states the real reason, and no data or timer state is corrupted. It is cheap enough to fix within 4.11.

## Re-verification

No rechecked findings in this assignment.

## Assessment and limitations

- Scope covered: the DEV-063 diff (4 files) and the click-result producers that can reach `ApplyViewed`, for Claude Code and Codex. Claude Code was analyzed through the shared rule and the synthetic test. Codex was analyzed through the reachable shared-backend path and the `unknown`-mode and `unknown`-host paths.
- Verdict on the developer's question: the text comparison is acceptable at this identity. It has one in-process producer, uses the same constant, is unaffected by normalization, fails safe and is pinned by tests. A typed reason is deferred until a second permanent cause exists (ANALYSIS-5100).
- Verdict on classification: correct for all stated reasons for both providers. Headless is a permanent but unstated limit, and the evidence misdescribes it (ANALYSIS-5101).
- Recommend fixing ANALYSIS-5102 within 4.11. ANALYSIS-5100 and ANALYSIS-5101 can be deferred or handled as evidence edits.
- Not performed: build, tests and format verification (owned by QA-058); the live tray and the 6.1 trial (not allowed). No performance measurement was needed.
- Unexpected writes: none; before and after identity match.
