> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-052); client: Claude Code 2.1.331, subagent; mode: non-interactive, auto mode; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-052 (DEV-064 recheck, task 4.11)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331 / subagent, non-interactive, auto mode / claude-opus-5-5
- Date: 2026-11-24T16:45Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-052
- Task and requirement references: task 4.11 and the task 4.3 note ([tasks.md](../tasks.md)); attention-notifications "Viewed wait timer" ([delta spec](../specs/attention-notifications/spec.md)); design decision 13 ([design.md](../design.md)); [implementation-064](../evidence/implementation-064.md); T-8 entries in [findings.md](findings.md)
- Scope and relevant neighboring paths: `src/BellTower/Tray/TrayApplication.cs`, `src/BellTower/Tray/NavigationOutcome.cs`, `src/BellTower/Hosts/NavigationReasons.cs`, `tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `docs/compatibility.md`, `docs/installation.md`. Neighbors: `src/BellTower/Tray/NavigationNotice.cs`, `src/BellTower/Tray/NoticeForm.cs`, `src/BellTower/IO/StateStore.cs` (`MarkViewed`), `src/BellTower/IO/LocalLog.cs`, [implementation-058](../evidence/implementation-058.md) (T-8 probe)
- Baseline commit: 76beb52
- Implementation identity (before): `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 76beb52 -Paths src tests docs vscode` printed HEAD 76beb5216df7 and manifest `M` for the 8 assigned files. Tracked diff SHA-256: `6ce6aec6e2117799dc887a046bfc474a3d53a1be79a1c04ce1bd4f51cba85bb5`. No untracked files. Summary: `head=76beb52 baseline=76beb52 files=8 diff=6ce6aec6e211 untracked=0/e3b0c44298fc`. This matches the expected value.
- Before/after identity: the after summary was `head=76beb52 baseline=76beb52 files=8 diff=6ce6aec6e211 untracked=0/e3b0c44298fc`. It is identical to the before summary, so nothing changed during the analysis.
- Prior reports / rechecked findings: [analysis-051](analysis-051.md) ANALYSIS-5101 and ANALYSIS-5102; [qa-058](qa-058.md) QA-5801 (ordering part, as the regression scope). ANALYSIS-5100 and QA-5800 are deferred and were not reopened.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-5102 fix | Inspection of `NavigationOutcome.SaveFailedStatus` and its one caller | `NavigationOutcome.cs:29-31`; `TrayApplication.cs:463` | The code is fixed. A refused result keeps its stated reason. A grep of `tests` finds no reference to `SaveFailedStatus` or to the status text, although the evidence says there are tests (ANALYSIS-5201) |
| Viewed-after-notice ordering (QA-5801 regression) | Traced `Navigate` and `MarkViewed`, `TrayApplication.cs:405-465` | A success marks the episode viewed at line 432, before the log line and the notice, which is the earlier behavior. A refusal marks it at line 446, after `notice.ShowNotice` at line 442 | Matches the spec wording "once the reason has been shown". Between the notice and the mark there is only synchronous UI-thread code, so neither `disposed` nor the captured episode can change |
| Episode captured at click time | Inspection | `episode = s.Episode` is read before the `await` (line 414). `StateStore.MarkViewed` (`StateStore.cs:131-141`) writes only when that episode is still the current one | Correct. A late limit cannot hide a newer wait, and `ALateLimitForAnOlderEpisodeCannotHideANewerTimer` pins this |
| Error paths | Inspection of `MarkViewed`, `NoticeForm.ShowNotice` and `LocalLog.Write` | `LocalLog.Write` catches its own exceptions. If `ShowNotice` throws, the `async void` handler ends before line 446, so the timer stays visible. That matches the evidence claim, and the unhandled-exception path itself is unchanged from before. The save-failure catch covers both call sites | No defect. Only a permanent limit gets past the `ApplyViewed` guard and reaches the store, so the refused branch of `SaveFailedStatus` is reached only for a stated limit |
| Duplication / complexity | Inspection | One private `MarkViewed` serves two call sites with complementary conditions (`result.Success` and `!result.Success`). The decision stays in `NavigationOutcome.ApplyViewed` | No duplicated policy and no drift from `docs/development.md` (the decision is in `Tray/NavigationOutcome` and the classifier is in `Hosts`). This is a recheck, so no new simplification is offered |
| ANALYSIS-5101 evidence correction | Read [implementation-064](../evidence/implementation-064.md) and the `IsPermanentLimit` remarks | The headless case, the synthetic Claude case and the provider-neutral temporary cases are all corrected in 064. The remark at `NavigationReasons.cs:36-42` gained the pass-through sentence | Verified, with one residual remark inaccuracy (see Re-verification) |
| Codex first-hook color timing: consistency | Compared `docs/compatibility.md:80,96`, `docs/installation.md:80`, `CapabilityMatrix.cs:105-106` and `DiagnosticsReporter.cs:101` | All five state the same facts: Claude Code sends its first hook at launch; `codex --no-daemon` sends none at launch; 12-54 s in the 6.1 trial; color possibly as late as the end of the first turn | Consistent across the matrix, doctor and docs for both providers. A repository grep finds no remaining "first prompt" wording |
| Codex first-hook color timing: parity honesty | Compared the new wording with [implementation-058](../evidence/implementation-058.md) (Phase 1, T-8) and the T-8 entries in findings.md | 058 measured that Codex sends `SessionStart` at the first prompt submit. It also found the hook-to-write path immediate in isolation and left Bell Tower-side delay candidates open. findings.md records that which hook arrived first is "not recorded" | The causal claim is stronger than the evidence (ANALYSIS-5200) |
| Performance | Inspection | No new loops or I/O. The refused path does one extra synchronous `MarkViewed`, which returns early when `ApplyViewed` declines | Not applicable |
| Build / tests / format | Not run | Owned by QA-059 | Not performed by assignment |

## Findings

### ANALYSIS-5200: The Codex color-timing wording attributes the end-of-turn delay to the provider beyond what the evidence shows

- Severity: warning (non-blocking; decision needed, because it touches the recorded T-8 user decision)
- Related task / requirement: task 4.3 note, the T-8 trial observation, and the parity policy on honest capability statements (AGENTS.md)
- Locations: `docs/compatibility.md:96` ("This is how the providers deliver hooks, not a Bell Tower gap or a provider restriction."); `docs/compatibility.md:80`, `docs/installation.md:80`, `src/BellTower/Diagnostics/CapabilityMatrix.cs:105-106` and `src/BellTower/Diagnostics/DiagnosticsReporter.cs:101` ("the color can appear as late as the end of the first turn", tied causally to "sends none at launch")
- Observation:
  - What the evidence supports:
    - Codex sends no hook at launch.
    - In the trial, the first accepted hook arrived 12-54 s after the process started (findings.md, T-8 reclassification).
    - The probe in implementation-058 showed that Codex sends `SessionStart` at the first prompt submit, followed by `UserPromptSubmit` 0.3-1.5 s later. It also showed the hook-to-write path is immediate in isolation.
  - What the evidence does not support:
    - findings.md states that which hook arrived first (prompt submit or completion) "is not recorded", while the user saw the color at completion.
    - If the first accepted hook was the prompt-submit one, as the probe predicts, then the gap between prompt submit and the end of the turn is not caused by Codex's hook timing. implementation-058 lists open Bell Tower-side candidates for that gap: writer retry backoff, the 2-second tick, and rendering.
  - The new text therefore turns an unexplained part of the delay into a provider fact. It also drops the earlier, probe-backed statement that the first Codex hook comes at the first prompt.
  - Claude Code is unaffected: the color at launch is observed (trial case 1 correction).
- Proposed improvement and benefit: either option keeps the matrix and doctor honest about verified versus unexplained behavior, as AGENTS.md requires.
  - Option (a): state the verified part. Codex sends its first hook only when the first prompt is submitted (12-54 s after start in the 6.1 trial). Then state the rest as observed, not as a cause: in the trial the color was seen only at the end of the first turn.
  - Option (b): settle it first. Identify the first accepted hook kind for a `codex --no-daemon` session, for example from the capture order or the `title-unavailable` and `title-recovered` log lines named in implementation-058, in an isolated or user-run trial. If it was `Stop`, keep the current sentence.
- Behavior-preservation risks: text only. No test pins these strings (per implementation-064), so a wording change affects no test.
- Evidence type: source and evidence observation. No live measurement was taken (a live tray is not allowed).
- Verification / measurement plan: for option (b), in one user-operated `codex --no-daemon` session, record the time of the first prompt submit, the hook kind and time of the first capture, and the time the color appears. No new instrumentation is needed.
- Relationship: T-8 (findings.md). The orchestrator should bring this to the user, because the user withdrew the "unfinished" classification on 2026-11-23.

### ANALYSIS-5201: The evidence claims unit tests for `SaveFailedStatus` that do not exist

- Severity: warning (non-blocking)
- Related task / requirement: 4.11; the ANALYSIS-5102 correction
- Locations: [implementation-064](../evidence/implementation-064.md) "Fixes" (ANALYSIS-5102: "Unit tests cover success and the permanent-limit result for both providers."); `tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs` (no reference); `src/BellTower/Tray/NavigationOutcome.cs:29-31`
- Observation: `grep -rn "SaveFailed\|timer preference" tests` returns nothing. The test diff against 76beb52 adds only `SharedClick`, `AStatedPermanentLimitHidesTheCurrentEpisodeTimerForEitherProvider`, `TemporaryFailures` and `ATemporaryFailureKeepsTheTimerForEitherProvider`, `AnUnrecognizedModeConversationKeepsTheTimer` and `ALateLimitForAnOlderEpisodeCannotHideANewerTimer`, all from the DEV-063 scope. The code fix is correct, but it has no test, and the evidence misstates the coverage.
- Proposed improvement and benefit: add the two-case theory the evidence describes. A success result should return the "Session focused; ..." text. A `SharedClick` result for `claude` and `codex` should start with `NavigationReasons.SharedBackend` and not contain "Session focused". Alternatively, correct the evidence line. Either way the acceptance record becomes accurate and the ANALYSIS-5102 wording is pinned.
- Behavior-preservation risks: none (test or evidence only).
- Evidence type: source observation
- Verification / measurement plan: grep for `SaveFailedStatus` in `tests`; the QA-059 focused run would then show the new cases.
- Relationship: the ANALYSIS-5102 correction

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-5102 | head 76beb52 + diff 6ce6aec6e211 | `TrayApplication.cs:463` uses `NavigationOutcome.SaveFailedStatus(result)`. A refused result yields "<stated reason> The timer preference could not be saved." and a success keeps the earlier text. The log code `viewed-wait-save-failed` is unchanged. The new ordering keeps the save-failure status from being overwritten: it is set after `SetStatus(result.Message)` on both paths | Verified in code. The claimed tests are missing (ANALYSIS-5201) |
| ANALYSIS-5101 | head 76beb52 + diff 6ce6aec6e211; [implementation-064](../evidence/implementation-064.md) | 064 corrects the headless rationale, states the synthetic Claude attachment and the provider-neutral temporary inputs, and the `IsPermanentLimit` remark gained the pass-through sentence. Residual: the remark at `NavigationReasons.cs:38-39` still says "unregistered-host refusals can resolve". That is not true for a headless run (`NativeAdoption.cs:92`, Host unknown), which is the same misdescription 064 corrects in the evidence. It is a comment only, and behavior is correct | Verified. Optional one-clause remark fix for the orchestrator to triage |
| QA-5801 (ordering part, regression scope) | same | A refusal marks the episode viewed only after `notice.ShowNotice` (lines 442 and 446). A success is unchanged (line 432). The episode is captured before the `await`. Overview and CLI clicks still pass `hideTimer` false, so `ApplyViewed` returns false before it touches the store | Verified |

## Assessment and limitations

- Scope covered: the DEV-064 diff (8 files) and the neighbors listed above, for both providers.
  - Claude Code: the shared permanent-limit rule on a synthetic attachment, the save-failure status, and the at-launch color statement.
  - Codex: the reachable shared-backend limit, the ordering and error paths, and the `--no-daemon` color-timing statements in the matrix, doctor and docs.
  - The click policy has no per-provider branch, and the timing text covers both clients in every surface.
- The tray ordering, the captured episode and the error paths are correct, and the policy is not duplicated.
- ANALYSIS-5200 needs a user-level decision, because it qualifies a recorded T-8 decision. ANALYSIS-5201 is a test or evidence correction.
- Not performed: build, tests and format verification (owned by QA-059); the live tray; any trial measurement of Codex hook order.
- Unexpected writes: none. The one attempted write (this report) was refused by the client. The before and after identity match.
