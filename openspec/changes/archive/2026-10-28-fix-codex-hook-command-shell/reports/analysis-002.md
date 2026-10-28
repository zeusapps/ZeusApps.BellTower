# Analysis report: fix-codex-hook-command-shell / 002

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 local tooling; delegated Default-mode recheck; role-configured model `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`, Analysis recheck 002; new regression IDs ANALYSIS-050 through ANALYSIS-099.
- Scope: recheck ANALYSIS-001, ANALYSIS-002 and ANALYSIS-003 plus the reported seven Core/Sessions failures and directly relevant regressions. No new suggestions or performance/architecture investigation.
- Requirements: tasks 2.1/2.2, 3.2 and 4.2; shared correlated permission resolution, preserved parallel work, quoted paths, safe diagnostics. See [tasks](../tasks.md) and [ordering delta](../specs/session-tracking/spec.md).
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior report: [analysis-001](analysis-001.md), retained unchanged. Developer handoff: [implementation-002](../evidence/implementation-002.md).

Before/after identity command:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests scripts tools/BellTower.Ingress.Bench docs/hook-ingress.md docs/installation.md docs/compatibility.md docs/follow-ups.md
```

Manifest:

```text
? src/BellTower/Diagnostics/HookDeliveryReporter.cs
? src/BellTower/Setup/ProviderHookProbe.cs
? tests/BellTower.Tests/Diagnostics/HookDeliveryReporterTests.cs
M docs/compatibility.md
M docs/follow-ups.md
M docs/hook-ingress.md
M docs/installation.md
M src/BellTower/Core/SessionReducer.cs
M src/BellTower/Diagnostics/CapabilityMatrix.cs
M src/BellTower/Diagnostics/DiagnosticsReporter.cs
M src/BellTower/Setup/HookCommandBuilder.cs
M src/BellTower/Setup/IngressProvision.cs
M src/BellTower/Setup/IntegrationSetup.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `697116446e1c282220b8fe6ebf00658996f55c0df26857b3bb07c9f38db6afdc`.
- Untracked hashes: reporter `fd6b6933dd692beaaf33cbb741c10e84fd296d5d8b529c78baf915d87008004d`; native probe `8eb90d45ead52c7caa8e94c4fa33db5fb48d7ee366a8f4062fa1f2cb17656f24`; reporter tests `e0b800afc786482cffbd4acbb6419c324924201fee5a4ad451b52c37829481fb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=16 diff=697116446e1c untracked=3/1b89a893996b`; matched. Reports, evidence and orchestrator tracking are excluded.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original subagent finding | Source inspection plus original isolated Core probe, matched provider/ingress variants | All four direct waits survive unrelated subagent start; repository focused checks also cover direct/candidate variants | Passed for ANALYSIS-001's specific defect; broader foreground correlation remains failed under ANALYSIS-050 |
| TOML encoding | Inspected complete Tomlyn model and special-character temp root; loaded a matching serializer-generated project table through installed Codex with disposable CODEX_HOME | Native parser returned exit 0 | Passed for ANALYSIS-002 encoding. Full product native hook flow not repeated by analyzer |
| Malformed receipts | Isolated report calls; repository matched-provider tests | Wrong-kind receipts return safely; tests assert preserved bytes, no private output and valid lowercase/legacy timestamps | Passed; ANALYSIS-003 |
| Focused regression tests | Filter `HookDeliveryReporterTests|SubagentStartDoesNotResolve`, using FullyQualifiedName predicates | 8 passed, 0 failed outside sandbox | Passed; does not approve the fixture's unrelated-foreground-clear assertion |
| Wider failures | Independently ran the five reported test names with all their variants | 7 failed, 2 passed of 9; source/fixture semantics assessed below | Failed, with five obsolete expectations and two genuine failure manifestations distinguished |
| Correlation / replay | Eight isolated unrelated-main-start cases plus a before/after causal-window sequence | Identified waits are cleared by unrelated starts for both providers, with matching or different fingerprints; restored wait is lost before the replay window is exceeded | Failed; ANALYSIS-050 |
| Package integrity | Read 0.1.9014 ZIP SHA-256 | Matches recorded hash | Passed integrity check; no installation or user-operated acceptance |
| Whitespace | `git diff --check` | No errors | Passed |
| Formatter / full suite / native gate / benchmark | Not rerun in this analysis round | Independent QA handles broader functional/native checks; prior named-pipe sandbox failure and successful retry remain historical | Not performed by analyzer; no reused result is presented as current executed acceptance |

## Findings

### ANALYSIS-050: An unrelated foreground start is treated as correlated permission resumption

- Severity: warning.
- Related task / requirement: 4.2; an approval request may resolve through its correlated result or established resumption, stop or end. Preserve simultaneous operation and wait identities.
- Locations: `src/BellTower/Core/SessionReducer.cs:233`, `:240`, `:252`, `:314`, `:322`. Existing failure fixtures: `tests/BellTower.Tests/Core/ReducerTests.cs:141` and `tests/BellTower.Tests/Core/HumanWaitCorrectionTests.cs:404`.
- Observation: the foreground gate fixes ANALYSIS-001, but a different operation ID later than the request still resolves every identified or candidate permission wait. ID inequality proves that the start belongs to another operation, not that the blocked operation resumed. Fingerprint equality does not repair that identity conflict. Other work in the same foreground session can proceed while a separate request remains pending.
- Evidence type: executed reproduction in all eight provider/ingress/fingerprint combinations; existing unrelated-operation and backdated-restore failures independently reproduced. See [recheck evidence](../evidence/analysis-002-reproductions.md).
- Proposed bounded correction and benefit: preserve attributed/candidate permission waits on unrelated starts; remove ID-inequality-only resolution inference. Retain protection for a late start of the same operation. Resolve through matching result(s) or other independently established resolution/resumption evidence. Preserve the existing foreground-activity fallback for genuinely uncorrelated notifications with neither operation ID nor fingerprint, and preserve prompt/stop/end handling. This avoids losing real permission alerts while parallel work starts.
- Behavior-preservation risks: retain correlated ToolEnd cleanup, every-candidate completion, matching wait scope/revision, silent backdated restoration and candidate narrowing. Do not reintroduce the original same-operation PreToolUse bug to make older tests green.
- Verification plan: matched Claude/Codex captured/normalized cases for unrelated main and subagent starts, same and different fingerprints, identified and candidate waits; retain waits across more than the causal window and confirm backdated runtime restoration remains silent; close by correlated result(s). Correct obsolete fixtures as described below while retaining their other safety assertions.
- Relationship: new regression finding; the backdated-restore failure is a manifestation of this broad-clear branch, not evidence of an independently identified causal-floor defect.

## Classification of the seven existing failures

| Failure | Classification | Requirement-grounded correction |
| --- | --- | --- |
| `MatchingOperationResumptionClearsPermissionWithoutResolvingOtherWaits`, both variants | Two obsolete ToolStart-resolution expectations | Keep the operation-ID/fingerprint cases and unrelated Question wait. Assert both waits survive the matching start, then apply the matching ToolEnd and assert only the Question remains. |
| `PermissionResolvedDuringGraceNeverCreatesAChipOrSound`, Claude/Codex PreToolUse variants | Two obsolete PreToolUse-resolution expectations | A delayed start does not establish approval. Assert the provisional wait survives and remains silent without confirmation, then use the correlated PostToolUse to resolve it. The two existing PostToolUse variants independently passed unchanged. |
| `AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete` | One obsolete candidate-start-as-completion expectation | Candidate one starting again is not completion; after only candidate two ends, candidate one remains unresolved. Assert the wait persists, then end candidate one and assert resolution. Preserve the all-candidates safety invariant. |
| `UnrelatedResumptionWithIdenticalArgumentsCannotClearAnIdentifiedWait` | Genuine first-assertion failure, ANALYSIS-050; its later matching-start-clear expectation is also obsolete | Preserve the first assertion that conflicting operation identity does not clear the wait. After matching ToolStart, keep the wait too; clear only through the corresponding result. |
| `ABackDatedRestoreApprovalStillConfirmsAfterMoreThanTheCausalWindowOfLaterEvents` | Genuine failure caused by ANALYSIS-050, not shown to be an independent replay-window defect | Keep the restoration test and its unrelated-event load. The isolated probe shows the wait is removed on the first unrelated start, before window compaction. After correlation is corrected, require the same backdated, silent confirmation and pending attention. |

The newer `CaptureTests.CandidateOperationStartsDoNotClearApprovalWaitButDifferentOperationResumes` and the final foreground portion of `UnrelatedSubagentStartDoesNotResolveDirectOrCandidateForegroundApprovalWaits` also currently assert resolution by a wholly different operation. That assertion is not independent evidence of genuine resumption. Retain their late-start/subagent coverage, assert preservation on unrelated foreground work, and demonstrate correlated completion instead. These changes follow the accepted correlation rule rather than merely matching current implementation.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | This report's target | Original four Core variants now retain the wait; eight focused tests include matched direct/candidate subagent cases | Verified specific subagent defect; ANALYSIS-050 separately blocks broader correlation |
| ANALYSIS-002 | This report's target | Complete config now uses Tomlyn; matching apostrophe/Unicode project-key config is accepted by installed Codex | Verified encoding correction; full native flow remains separately classified |
| ANALYSIS-003 | This report's target | Wrong-kind receipt calls return; matched receipt regressions verify bytes, output and valid timestamp compatibility | Verified |

## Assessment and limitations

The three original corrections are supported by independent recheck evidence. The target remains blocked by ANALYSIS-050 and unreconciled fixture expectations; green focused additions alone do not establish reducer correctness. Five failures conflict with the accepted late-start requirement, while the unrelated-ID and restoration failures are genuine manifestations of overly broad resumption inference.

The existing Codex latency failure, exact resolver/argv evidence, actual Claude CLI runner and user-operated post-migration/mixed acceptance remain open under QA-002/QA-003. This recheck grants no budget exception, provider restriction, installation approval or completion decision. The package hash matches its recorded handoff, and no user installation was changed.

Only the assigned report/evidence and ignored disposable analysis outputs were written. The implementation identity matched before and after review. The orchestrator owns finding verification state and further routing.
