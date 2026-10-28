# Analysis 004 independent recheck evidence

Target: `head=aadbfb3 baseline=aadbfb3 files=19 diff=262b223e8a79 untracked=3/1b89a893996b`. Full hashes and scope are in [analysis-004](../reports/analysis-004.md). All inputs are synthetic or disposable; no user configuration, production data or active provider session was changed.

## Original isolated reproductions

Command:

```text
dotnet run --project artifacts/analysis-hook-fix/AnalysisProbe.csproj --no-restore -c Release
```

This ignored disposable project references the current Release product assemblies. It reuses the analysis 002 reproduction sequences against the final correction, rather than changing those sequences to make the corrected source pass.

| Probe | Variants | Observed | Required | Result |
| --- | --- | --- | --- | --- |
| Unrelated subagent ToolStart after identified request | Claude/Codex x normalized/captured: 4 | Wait count remains 1 | 1 | Passed, ANALYSIS-001 |
| Unrelated main ToolStart after identified request | Claude/Codex x normalized/captured x equal/different fingerprint: 8 | Wait count remains 1 in every combination | 1 | Passed, ANALYSIS-050 |
| Old captured Codex request with unrelated main starts | After first start, then after `CausalSessionReducer.Window + 40` | Wait count 1 at both checkpoints | 1 | Passed; no early loss or compaction loss |
| Wrong-kind Codex observed receipt | Root array; numeric timestamp | Reporter returns, no exception | Safe unverified report | Passed, ANALYSIS-003 |

The isolated replay-window probe measures preservation only. The independent repository restoration fixture below additionally verifies runtime restoration confirmation, silent alert behavior and active attention.

## Focused repository execution

```text
dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ReducerTests|FullyQualifiedName~PermissionNotificationTests|FullyQualifiedName~HumanWaitCorrectionTests|FullyQualifiedName~CaptureTests|FullyQualifiedName~CodexPermissionTests|FullyQualifiedName~HookDeliveryReporterTests' --verbosity minimal
```

Result outside sandbox: **181 passed, 0 failed, 0 skipped**, reported duration 3 seconds. The initial default-sandbox invocation reached test execution but produced no result; it was canceled. No specific failure diagnostic was received in that attempt, so this record does not infer a new file-access or IPC root cause from the stall.

Meaningful assertions inspected and executed:

- `ReducerTests.MatchingOperationStartKeepsPermissionUntilResultWithoutResolvingOtherWaits`: operation-ID and fingerprint-only variants keep both waits through the matching start; matching result removes only permission, retaining Question and its original time.
- `UnrelatedResumptionWithIdenticalArgumentsCannotClearAnIdentifiedWait`: conflicting identity with identical arguments and the later matching start both preserve permission; correlated result resolves.
- `AmbiguousParallelPermissionRequiresEveryCandidateToComplete`: start does not complete a candidate; one result narrows membership and the last result resolves.
- `PermissionNotificationTests.PermissionResolvedDuringGraceNeverCreatesAChipOrSound`: both providers retain silent provisional state after late PreToolUse, then resolve through PostToolUse. Existing result variants also pass.
- `HumanWaitCorrectionTests.ABackDatedRestoreApprovalStillConfirmsAfterMoreThanTheCausalWindowOfLaterEvents`: retains the old request through more-than-window unrelated starts; restored confirmation uses the original wait time, stays silent and requires attention.
- `CodexPermissionTests.DelayedStatusForAmbiguousPermissionCannotConfirmUntilCandidateCompletes`: both reducer paths retain candidate set and revision through late start; a result narrows scope and increments revision; delayed old evidence cannot confirm; fresh scoped evidence can.
- `CaptureTests`: both providers and captured/normalized paths preserve identified/candidate waits through late same-operation, unrelated subagent and unrelated main starts, and close only by corresponding results. Existing causal shuffle, duplicate, mixed-session and bounded replay fixtures pass in the same run.
- `HookDeliveryReporterTests`: invalid receipts for both providers retain exact bytes and omit private command/marker content; valid lowercase and legacy uppercase timestamp keys continue to report delivery.

## Carry-forward and evidence boundaries

The native probe, reporter and receipt-test hashes exactly match analysis 002. Its Tomlyn/native-parser proof remains mapped to the same probe bytes. This round inspected construction again and executed current receipt regressions, but did not repeat the full provider-native flow. Native investigation 001/002 and implementation 003 were read; their actual CLI/source observations are distinguished from this analysis's fixture execution.

The 0.1.9015 ZIP independently hashes to `B6F4A1C1184190C4527C3423DF3146E4EA4335AE17CFC25C88971C0601A12488`, matching the developer handoff. No package was installed. `git diff --check` passed. The before/after source identity matched. The assigned report/evidence hygiene scan found no targeted personal or secret patterns.

No latency run, full-suite rerun, formatter run or user-operated post-migration acceptance was performed by the analyzer. The existing failed median gate and pending mixed-session acceptance remain open.
