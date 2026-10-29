# Analysis report: add-idle-attention-reminders / 006

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / focused delegated recheck / `gpt-6.1-sol` (role default).
- Date: 2026-10-29, 06:53 UTC.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-006.
- Task and requirement references: ANALYSIS-004 / REGRESSION-001 and affected fixture cleanup/time regression only. No optional review scope.
- Scope: the three changed tray test paths and unchanged source/tray/queue neighbors; complete [target-005 manifest](../evidence/review-target-005.txt).
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked SHA-256 `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`; 17 untracked files/aggregate `7591a4f4753a`.
- Before/after identity: exact full-output matches; `head=47dfca9 baseline=47dfca9 files=30 diff=3c87f683b02f untracked=17/7591a4f4753a`.
- Prior reports: [analysis-005](analysis-005.md), [QA-004](qa-004.md), [implementation-005](../evidence/implementation-005.md). The original report-004 interruption was an account usage limit, **not** an automatic approval rejection or demonstrated report-write restriction; this corrects analysis-005's imprecise phrasing without altering history.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correction scope | Exact target-004/005 comparisons | [Evidence 006](../evidence/analysis-006.md) | Passed: three test paths changed; production/source/docs unchanged. |
| Setup/concurrency substance | Inspect real driver, async queue signals and dedicated consumers | Source at target-005 | Shared-pool consumer drivers removed; real source/tray/default 80 ms checks retained. Release ordering still has a shared-worker dependency. |
| Assertion propagation | Ignored probe links actual helper file; driver/consumer delegates throw recognizable errors | Same evidence | Passed: original work error and all eight consumer errors remain visible; successful cleanup joins driver. |
| Default-budget and cleanup ordering | Actual helper/source, dedicated consumers and controlled process-local worker constraint | Same evidence | Default rejection/single-flight assertions pass; held source poll self-times out before awaiting cleanup can release it. ANALYSIS-101 below. |
| Healthy freshness assumption | Actual source/tray/queue, five-second test read budget, controlled 501 ms speech-observation age | Same evidence | Tone retained and speech canceled as expected by production policy; demonstrates the positive fixture's separate 500 ms age precondition. Exact QA failure cause unknown. |
| Current full regression | QA ran alone; analyzer waited for QA completion before executable work | Orchestrator-relayed result retained in evidence | Failed 1,084/1,086; healthy isolate and eight child-launch variants pass isolated. No analyzer broad run. |
| Prior coverage | Unchanged product hashes and explicit path mapping | [Mapping](../evidence/analysis-006.md#target-mapping-and-integrity) | Receipt/escalation/native/config and matched/mixed provider evidence carried forward at prior scopes. |
| Integrity, docs and own hygiene | Exact before/after match; reuse previously fetched official .NET sources; inspect own outputs | Evidence and prior primary links | Passed for analyzer scope. Broad hygiene/build/format remain QA-owned; no redundant CLI lookup. |

## Findings

### ANALYSIS-101: Held-work release still depends on the worker occupied by that work

- Severity: warning.
- Related tasks / requirements: tasks 2.4, 2.5, 3.3; reliable owned fixture setup/cleanup for ANALYSIS-004 acceptance.
- Locations: `tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs:12`, `:16`, `:24`, `:28`, at target-005.
- Observation: the driver publishes success/failure before releasing held source work. Release runs in the awaiting continuation's finally. In the controlled single-worker reproduction, that continuation cannot execute while the source poll occupies the worker. The poll reaches its own three-second timeout; only after it exits does release run. Both recorded flags are true: poll self-timed out, and release saw it already exited.
- Proposed correction and benefit: release owned held work in the dedicated driver's finally **before** publishing completion; keep the outer finally for joining/fallback cleanup. This removes the cleanup dependency rather than relying on the poll's self-timeout.
- Behavior-preservation risks: retain original assertion exceptions, propagate cleanup failures honestly, release/join on success and failure, and preserve the actual public default 80 ms checks and single-flight behavior.
- Evidence type: source observation and bounded execution of the actual linked helper. No attribution to QA's particular full failure.
- Verification: under the same controlled constraint, default rejection and eight-consumer assertions must finish, explicit release must precede probe exit, and no self-timeout may be needed. Verify original driver/consumer failures still propagate.
- Relationship: new test-helper regression in this correction; associated with canonical ANALYSIS-004 rather than a production-policy defect.

### ANALYSIS-004 remains: healthy positive coverage still depends on real observation age

- Severity: warning, existing required acceptance.
- Locations: `tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs:162`; `src/BellTower/Tray/TrayApplication.cs` reminder validity's UTC reads and 500 ms freshness check.
- Observation: the healthy fixture's five-second read budget does not control the independent 500 ms age guard. The actual wrapper/tray/queue probe with positive idle and only the speech sample aged 501 ms yields `ToneCancelled=False` and `SpeechCancelled=True` after sink entry. This is correct fail-closed policy and reproduces the reported symptom without proving the actual full-run cause.
- Minimal scope clarification: source already accepts an injected clock; tray reminder validity hardcodes UTC. To fulfill the original deterministic clock/idle requirement while retaining real wrapper/tray/queue coverage, add a tiny **internal tray-validity clock seam with unchanged production UTC default**, and use the same manual clock in the source and validity fixture. Keep separate stale/future, current-input and real monotonic default-timeout tests.
- Why cleanup alone is insufficient: this healthy method does not invoke the new helper; the controlled aging symptom also occurs without that helper and under normal restored pool settings. Removing cleanup coupling cannot by itself control its elapsed observation age. The clock seam is needed to remove this fixture assumption, not to fix a proven product defect or explain an exact QA failure.
- Risks/verification: no widened age/deadline, cached-positive fallback, global tuning or new user control. Healthy fixed-time samples must stay valid; advancing manual time across 500 ms or injecting future time must still suppress; input changes must still invalidate; public 80 ms timeout remains fail-closed.
- Relationship: existing ANALYSIS-004 / duplicate QA-004, no duplicate ID allocated for the clock aspect.

## Re-verification

| Finding ID | Target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-004 | `3c87f683b02f` / `7591a4f4753a` | Original stale-cache safety retained; default rejection checks pass; positive fixture has demonstrated real-clock age dependency | Still open for deterministic healthy/default-regression acceptance. |
| ANALYSIS-101 | Same target | Actual helper release runs only after held poll self-timeout under controlled worker availability | Reported for orchestrator triage and bounded correction. |
| REGRESSION-001 | Same target | QA reports 1,084 pass/two fail; exact healthy isolate and eight child-launch variants pass isolated | Still open; causes unknown and no clean default suite established. |
| ANALYSIS-001/002/003/005 and prior matched/mixed cases | Byte-identical product/source/docs | Explicit unchanged path/hash mapping in evidence | Prior verification retained at documented scopes. |

## Assessment and limitations

The correction improves fixture setup and preserves assertions, but leaves a concrete release-order dependency and an independent real-clock positive-test assumption. The smallest follow-up is owned release before completion plus the internal UTC-default clock seam required for deterministic fixture time. These recommendations preserve all production safety policy; no implementation or test edit was made by the reviewer.

Current QA's full healthy-playback failure and unreadable child snapshot remain recorded facts with unknown exact causes. Controlled mechanisms and isolated passes do not overrule that run. No physical input/audio, native/RDP trial, real provider, installation, production process, message, broad retry, commit or publication was performed. The orchestrator owns scope clarification, triage and completion.
