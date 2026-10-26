# Tasks

## 1. Provider signal contracts

- [x] 1.1 Verify reliable-ingress ordering and lifecycle health predecessors; retain redacted fixtures proving PermissionRequest versus Notification/permission_prompt fields and timing for supported Claude and Codex versions/modes.
- [x] 1.2 Define provisional/confirmed provenance and current-turn/attachment correlation through the same policy for both clients; verify old snapshot reads and additive field round trips.

## 2. Claude eligibility and correlation

- [x] 2.1 Track provisional requests for both clients immediately but keep them ineligible for chips/audio/reminders/webhooks; verify an automatic review lasting longer than fifteen seconds still produces no permission alert.
- [x] 2.2 Confirm a current unresolved human wait from either verified provider adapter without adding another fixed grace; verify confirmation-only and precursor-present cases retain honest elapsed time.
- [x] 2.3 Handle missing IDs and same-fingerprint parallel operations without arbitrary assignment; verify unrelated result, partial parallel resolution and ambiguous confirmation cases.
- [x] 2.4 Reject delayed notifications after resolution, new prompt, stop/end or replacement attachment using causal receipts; verify shuffled/duplicate confirmation fixtures never resurrect completed waits.
- [x] 2.6 Show a visibly unverified, silent "possibly waiting for approval" chip after 15 s for sessions without a supported confirmation signal (Codex without a verified owning runtime or with a failed status read); verify no sound, reminder or escalation eligibility and correct clearing, plus doctor/matrix wording.
- [x] 2.7 Start a fresh attention episode when supported evidence confirms a request whose unverified chip was dismissed; verify announcement and visibility for both clients.
- [x] 2.8 Apply the silent unverified chip to Claude sessions where no permission_prompt has been observed and name the disabled-notification setting in doctor/capability matrix; verify no sound, reminder or escalation eligibility.
- [x] 2.5 Persist current confirmation without restored sound and keep distinct new blockers independent; verify restart, viewed/dismissed waits and subsequent real approval episodes.

## 2A. Question-tool approval flows

- [x] 2A.1 Add redacted fixtures from `evidence/claude-askuserquestion-hooks.json` and `evidence/codex-request-user-input-hooks.json`; verify the current Question→Permission flip in a failing regression first.
- [x] 2A.2 Correlate question-tool PermissionRequest/permission_prompt to the pending question wait through shared policy; verify a single episode, announcement and dismissal for Claude and the matching Codex sequence without an approval hook.
- [x] 2A.3 Handle a question and a permission pending together, plus mixed Claude/Codex sessions; verify ambiguous confirmation, no reassignment and independent episodes.
- [x] 2A.6 Never credit a held ambiguous prompt to a permission by elimination when the question resolves; verify with the sequential question-then-Bash capture that the question's prompt confirms only the question and the permission waits for its own prompt.
- [x] 2A.4 Clear a rejected Codex Default-mode question at Stop with no residual restore, reminder or escalation; investigate an owning-runtime user-input signal and record it or its proven absence.
- [x] 2A.5 Capture Claude AskUserQuestion in `default` permission mode and Codex `request_user_input_async`; record any differences as evidence.

## 3. Compatibility and diagnostics

- [x] 3.1 Expose configured/observed/unavailable confirmation coverage for both clients and conservative legacy snapshot migration; verify absent signal is not mislabeled confirmed human wait by a timing-only fallback.
- [x] 3.2 Run matched human/auto/unverified conformance plus Codex native transport/name/runtime safety tests; verify former timing-only fallback is not labeled confirmation and any mode limitation has evidence.
- [x] 3.3 Verify question/completion/background timing remains intact and export confirmed eligibility for reminders; run focused reducer/provider/permission suites with silent sinks.
- [x] 3.4 Document supported human-signal timing and unresolved provider limitations; verify formatting and Release build and retain targeted live auto-versus-human parity acceptance separately from synthetic fixtures.


## 4. Provider parity acceptance

- [x] 4.1 Implement and verify this feature for both Claude Code and Codex using shared policy and provider adapters; run matched provider fixtures plus a mixed-session case and retain version/mode-specific evidence.
- [x] 4.2 Audit setup, defaults, configuration, diagnostics and controls for equal feature availability; document any hard provider restriction with its proof, honest shared fallback and regression check, and treat missing implementation or unverified capability as incomplete work.

## Acceptance evidence still required at Codex resumption

Seventeen tasks are reconciled from QA 004–006 and Analysis 004–006 evidence. QA 006 passes the full Release suite, 979/979, on the frozen Implementation 007 target; Analysis 006 independently passes 62 assertions and verifies canonical ANALYSIS-002, including candidate narrowing, stale-result rejection and fresh-query recovery. The earlier 974/976 suite and its isolated 2/2 rerun remain historical evidence; the final quiet full run passes without declaring the earlier failures unrelated. Build and scoped formatting are developer-executed evidence on the same target. These records are distinct from native acceptance.

| Existing task | Remaining evidence |
| --- | --- |
| 2A.5 | Capture the Codex Plan-mode async question surface or prove the exact operation is unavailable in that version/mode. The Default-mode tool-list absence is narrower evidence. Preserve the captured Claude default-mode sequence and its response-attribution caveat. |
| 3.4, 4.1 | On the reviewed candidate, observe a slow automatic review and a genuine human approval for both clients; record the provider version/mode and expected confirmed versus unverified UI. Retain the separate mixed-session fixture evidence. Fixture success does not replace the named live auto-versus-human trial. |
| 4.2 | Closed 2026-10-26 by user decision (a): the silent unverified fallback ships and the runtime-less Codex confirming signal stays a documented unverified parity gap (see findings.md history). Original note: Preserve the supported silent unverified fallback for runtime-less Codex sessions while the human-confirmation capability remains unverified. A full confirmation claim requires supported evidence or an exact, proven restriction. Claude's notification-disable setting applies to SDK callback-hosted sessions, not terminal sessions. |

Any installation switch is a separately authorized operation after a concrete candidate and preflight. Do not alter user provider settings, answer real approvals, play test audio or interrupt active sessions to manufacture acceptance evidence. User-operated trial sessions and observations must be explicitly attributed.

The earlier packaging attempt failed at audited NuGet restore (`NU1900`); that failure remains historical. On continuation, the audited restore and normal package build succeeded. Candidate **0.1.9012**, source `754aed5` plus the reviewed dirty-tree identity, passed read-only preflight against installed **0.1.9011**. The user explicitly authorized the installation switch for native acceptance. The reviewed script selected 0.1.9012, restarted only Bell Tower's tray, preserved original installed bytes and left configuration/runtime files untouched. Launcher version, selected/running status and doctor agree on the candidate. Auditing remains enabled. Current package/evidence review is QA/Analysis 007; real-session approval observations are still pending and are not inferred from installation. No completion commit has been created.
