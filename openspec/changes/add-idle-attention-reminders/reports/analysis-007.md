# Analysis report: add-idle-attention-reminders / 007

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / focused delegated recheck / `gpt-6.1-sol` (role default).
- Date: 2026-10-29, 07:07 UTC.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-007.
- Task and requirement references: ANALYSIS-004, ANALYSIS-101, REGRESSION-001 and affected regression only; no new optional scope.
- Scope: internal tray reminder clock seam and current fixture-driver/playback changes, plus unchanged native source/queue neighbors; complete [target-006 manifest and hashes](../evidence/review-target-006.txt).
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked SHA-256 `830d1d13ae60eebfe3fc8e8a018fd954502f784dc61225f48d9242217d6d2f51`; 18 untracked files, aggregate `53ddcdc3b756`.
- Before/after identity: exact full-output matches; `head=47dfca9 baseline=47dfca9 files=31 diff=830d1d13ae60 untracked=18/53ddcdc3b756`.
- Prior reports: [analysis-006](analysis-006.md), [QA-005](qa-005.md), [implementation-006](../evidence/implementation-006.md); current full-regression evidence [QA-006](qa-006.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Driver-owned release | Actual linked helper, actual held source/default-budget consumers, controlled isolated worker availability | [Analysis 007 evidence](../evidence/analysis-007.md) | Passed: release precedes probe exit/completion; no poll self-timeout; owned driver joined. |
| Assertion/cleanup propagation | Recognizable work-plus-cleanup failures and eight failing dedicated consumers | Same evidence | Passed: both error categories and all consumer errors preserved. |
| Shared-clock healthy validity | Actual source/tray predicate for Claude/Codex using one manual clock | Same evidence | Passed; no dependency on real 500 ms elapsed time in positive fixture. |
| Stale/future/input guards | Advance manual age 501 ms, future-date observation, change to recent input | Same evidence | Passed: all suppress reminders; production age and timeout constants unchanged. |
| Healthy/mixed queue behavior | Real source/tray/queue with silent sinks; Claude-plus-Codex reminder aggregate and ordinary/reminder input race | Same evidence | Passed: healthy tone/speech and count retained; stale speech canceled while ordinary tone survives. |
| Production default and ordinary branch | Source inspection of internal UTC default/unchanged 80 ms/500 ms code; ordinary alert validated with throwing reminder clock | Same evidence | Passed: injection used only for reminders; ordinary UTC path preserved. No new control or policy. |
| Current full regression | Read independently executed QA-006; no overlapping analyzer broad run | [QA-006](qa-006.md) | Passed: 1,089/1,089, no failures/skips. Historical failures remain recorded; causes not inferred. |
| Prior verified areas | Explicit unchanged path/hash mapping | [Carry-forward](../evidence/analysis-007.md#exact-carry-forward-mapping) | Prior receipt/escalation/native/config/provider scopes retained; affected playback/time behavior rechecked. |
| Integrity and own hygiene | Exact before/after target comparison and inspection of own report/evidence | Reserved target and own outputs | Passed for analyzer scope. Broad build/format/hygiene owned by current QA/developer reports. |

## Findings

**No new findings.** The two assigned corrections are independently verified on target-006. The helper now owns held-work release before it publishes completion, and the healthy fixtures control source and tray validity time together without weakening current-input, stale/future, public default-budget or ordinary-alert behavior.

The current clean QA default run supplies the previously missing regression result. It does not identify causes for earlier failed full runs or erase them. No additional correction, optimization, provider exception or acceptance waiver is proposed.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-101 | `830d1d13ae60` / `53ddcdc3b756` | Actual-helper constrained-worker release-order reproduction now passes without self-timeout; work/cleanup/consumer errors preserved | Verified on this target; orchestrator owns ledger closure. |
| ANALYSIS-004 | Same target | Shared-clock real-wrapper/tray/queue healthy/mixed cases pass; 501 ms/future/input suppress; ordinary branch and default timeout retained | Verified on this target at the required deterministic fixture scope. |
| REGRESSION-001 | Same target | Independent QA-006 full default Release suite passes 1,089 tests, zero failed/skipped | Required clean regression established; not an analyzer-run suite. |
| ANALYSIS-001/002/003/005 and QA-006/007/008 | Same target; unchanged paths explicitly mapped | Prior evidence carried forward; affected source/tray freshness guards additionally rechecked | Prior verification remains applicable at documented scopes. |

## Assessment and limitations

No unresolved blocker remains in this assigned recheck. The source seam is internal and defaults to the previous production UTC behavior; the native refresh budget remains 80 ms, age checks remain 500 ms, and no cached-positive fallback was added. Both providers and mixed sessions retain common policy and current queue coverage.

Checks used isolated synthetic observations and silent sinks. No native/RDP desktop trial, live provider, installed-build operation, physical input/audio, microphone, user-process control or network send was performed. Product/task/ledger/commit decisions remain with the orchestrator. The implementation stayed frozen, and the analyzer wrote only its assigned report/evidence plus ignored scratch artifacts.
