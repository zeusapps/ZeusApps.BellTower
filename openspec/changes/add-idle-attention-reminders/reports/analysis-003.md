# Analysis report: add-idle-attention-reminders / 003

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / final delegated recheck / `gpt-6.1-sol` (role default).
- Date: 2026-10-28, 19:34 UTC.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-003.
- Task and requirement references: ANALYSIS-004 and affected playback/native freshness regression; existing REGRESSION-001 assessment requested by orchestrator. No new optional scope.
- Scope: frozen 29-file implementation manifest, with this round focused on `IUserIdleSource`, `WindowsUserIdleSource`, tray validity, idle/playback tests and corresponding docs. [Target 003](../evidence/review-target-003.txt) records exact paths and hashes.
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked SHA-256 `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`; 16 untracked files, digest `cbe46cdb0a67`.
- Before/after identity: exact full-output match both times; `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/cbe46cdb0a67`.
- Prior reports: [analysis-001](analysis-001.md), [analysis-002](analysis-002.md), [developer handoff 003](../evidence/implementation-003.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Fresh playback observation | Adapted original cached-input probe to actual `GetFreshSnapshot` wrapper; manual clock and low-level counter | [Analysis 003 evidence](../evidence/analysis-003.md) | Passed: T+100 ms input causes a new read and cannot authorize stale reminder eligibility. |
| In-flight query provenance | Start an older blocked poll, request fresh input, release old poll, verify second observation is used | Same evidence | Passed: pre-request sample is not accepted. |
| Timeout and single-flight behavior | Prior positive cache, blocked observer, concurrent fresh callers; monotonic budget inspected | Same evidence | Passed: unknown on timeout; no overlapping worker. Observed default timeout calls 79.7/82.8 ms; no hard wall-clock performance claim. |
| Healthy and mixed audio | Real wrapper and tray validity predicate with silent `AudioQueue` sinks for Claude/Codex; input changes during mixed tone | Same evidence | Passed: healthy reminder phrase/tone/speech preserved for both; stale mixed speech canceled while ordinary tone survives. |
| Affected time/failure regression | Future observation, observer exception, post-wrap future tick and legitimate short wrap | Same evidence | Passed; conservative boundary remains effective after refactor. |
| Worker-availability/fixture assumption | Isolated process-only thread-pool constraint and blocked worker; observer itself is instant | Same evidence | Fast observer is not invoked before the budget, so reminder is correctly withheld and sink never entered. This is a supported mechanism for the fixture failure, not attribution of QA's run. |
| Unaffected findings | Change-scope mapping, identical untracked hashes and unchanged service/mapping inspection | [Carry-forward mapping](../evidence/analysis-003.md#target-continuity-and-carry-forward) | ANALYSIS-001/002/003/005 verification carried from target 002, with affected 003 boundary cases independently rerun. |
| Full suite/build/format | QA owns these; waited for its full/isolated runs before executing scratch | Orchestrator-relayed QA result | Not run by analyzer. Full regression remains unresolved: 1,085 passed/one failed as reported by QA. |
| Integrity and own output hygiene | Exact before/after manifest comparison; report/evidence inspection | Reserved target and analyzer outputs | Passed; no implementation or historical-report edits. Broad hygiene remains QA-owned. |

## Findings

No new finding IDs allocated. The original stale-cache defect in ANALYSIS-004 is corrected on target 003 at the independently checked source/bounded-fixture scope: the real playback path now requires a bounded post-request observation and does not authorize from its earlier cached idle sample. Full ANALYSIS-004 acceptance remains open with QA-003's failed healthy-wrapper regression; this report does not override that gate.

Existing REGRESSION-001 remains unresolved at the release acceptance level because QA's full suite failed. The failing successful-playback fixture at `tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs:173` assumes a worker will execute a fast low-level observer within the production 80 ms budget. The independent worker-availability reproduction shows why the observer's execution speed alone cannot establish that precondition. A timed-out first validity check discards the queued reminder, so waiting longer for the sink does not recover it.

This does not reopen ANALYSIS-004's safety defect or prove the cause of the specific full-suite failure. No observation timing was captured in that run. The orchestrator must retain the failed full result separately from isolated/bounded passes and decide the remaining regression gate with the user after the two permitted correction rounds. No third correction was performed or assigned by this reviewer.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-004 | `3c87f683b02f` / `cbe46cdb0a67` | Real-source pre-input cache replaced by new observation; pre-request query excluded; timeout/single-flight and healthy/mixed silent playback probes pass | Original stale-cache defect corrected at bounded scope; full acceptance remains open with QA-003's healthy-wrapper full-run failure. Orchestrator owns ledger state. |
| ANALYSIS-001 | Same target; unaffected policy/store paths | Explicit target-002 carry-forward mapping and unchanged policy hash | Prior verification remains applicable. |
| ANALYSIS-002 | Same target; unaffected store/config/schema paths | Current-projection/config helper remains unchanged; target-002 API probes carry forward | Prior verification remains applicable. |
| ANALYSIS-003 | Same target; unchanged snapshot/elapsed guards within refactored source | Future, failed-read and wrap probes rerun; target-002 nonnegative-age/25-hour checks mapped explicitly | Verification retained with affected boundary rechecked. |
| ANALYSIS-005 | Same target; unchanged form/config paths | Same form hash and Save mapping; target-002 executed preservation evidence | Prior verification remains applicable. |
| REGRESSION-001 | QA full run on target 003 | QA full failure remains; isolated healthy method and analyzer quiet/bounded checks pass; worker-availability mechanism reproduced separately | Not resolved by analyzer; no full-suite cause established. |

## Assessment and limitations

The assigned playback freshness correction passes independent checks on the real wrapper and queue path, preserving boundedness, healthy delivery and ordinary mixed-session audio under the observed controlled/quiet conditions. The original safety defect is corrected, while ANALYSIS-004 full acceptance and REGRESSION-001 remain open as recorded by QA-003. The scheduling reproduction is a demonstrated mechanism, not evidence of the cause of QA's particular failure. Successful bounded probes and carry-forward evidence do not convert QA's failed default suite into a pass.

No physical input/audio, native/RDP desktop trial, live provider, installed-build operation or network send was performed. Scratch execution started only after QA's full/isolated runs completed. The implementation identity stayed unchanged; this reviewer wrote only its assigned report/evidence and ignored scratch artifacts. Completion and any further correction decision belong to the orchestrator/user.
