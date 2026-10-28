# Analysis 002 recheck evidence

All runtime checks used the current Release assemblies and disposable settings/data under ignored `artifacts/analysis-hook-fix/`. No user configuration, installation, active process or implementation file was changed.

## Original finding rechecks

The existing disposable console probe was rebuilt against the corrected target:

```text
dotnet run --project artifacts/analysis-hook-fix/AnalysisProbe.csproj --no-restore -c Release
```

- `ANALYSIS-001`: each original foreground permission wait remained at count 1 after the unrelated subagent start, for Claude and Codex with captured and normalized observations. Before the fix, all four counts became zero.
- `ANALYSIS-003`: the array-root and numeric-timestamp receipt cases both returned a report instead of throwing. The focused repository receipt regressions additionally check unchanged bytes, absence of private receipt/command text, safe unverified status, and valid `at`/legacy `At` timestamps for both providers.
- `ANALYSIS-002`: generated the same Tomlyn project-table structure used by the corrected product with a synthetic project key containing an apostrophe and Unicode. Installed Codex `features list`, with that disposable `CODEX_HOME`, returned exit 0. The old manually quoted key returned a bootstrap TOML parse error. Source inspection confirms the product now uses this serializer for the complete isolated config and always includes apostrophe/Unicode in its temporary root. The analyzer did not repeat the complete product native hook probe; QA and the developer's separately labeled native runs cover that boundary.

Focused repository recheck, outside the sandbox after the previously demonstrated testhost IPC failure:

```text
dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~HookDeliveryReporterTests|FullyQualifiedName~SubagentStartDoesNotResolve" --verbosity minimal
```

Result: **8 passed, 0 failed**, including both providers and both captured/normalized direct/candidate subagent cases. This pass verifies the original finding's subagent portion; the final different-foreground-operation assertion in that fixture is separately challenged by `ANALYSIS-050`.

## ANALYSIS-050: unrelated foreground operation

The console probe sent an identified Permission request and then a later, main-thread ToolStart with a different operation ID. It independently varied provider, captured/normalized ingress, and whether the unrelated operation's fingerprint equaled the request's fingerprint.

| Provider | Captured | Same fingerprint | Actual wait count after unrelated start | Expected |
| --- | --- | --- | --- | --- |
| Claude | false | false | 0 | 1 |
| Claude | false | true | 0 | 1 |
| Claude | true | false | 0 | 1 |
| Claude | true | true | 0 | 1 |
| Codex | false | false | 0 | 1 |
| Codex | false | true | 0 | 1 |
| Codex | true | false | 0 | 1 |
| Codex | true | true | 0 | 1 |

The observation IDs were unique, session and attachment identities matched, the unrelated start was later than the request, and both observations used the current adapter revision. The request had operation ID `foreground-operation`; the later start had ID `unrelated-main-operation`. A distinct operation is not correlated approval resolution merely because it runs on the main thread.

For the backdated-restore regression, a captured Codex request for operation `old` was followed by `CausalSessionReducer.Window + 40` unrelated main-thread starts. The wait count was already **zero after the first start**, and remained zero after the entire sequence. Therefore the reported restoration failure is caused before the causal window is exceeded; the evidence does not establish a new floor/backdating defect.

## Independent reproduction of the reported seven failures

Ran the existing test names as a bounded filter:

```text
MatchingOperationResumptionClearsPermissionWithoutResolvingOtherWaits
PermissionResolvedDuringGraceNeverCreatesAChipOrSound
AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete
UnrelatedResumptionWithIdenticalArgumentsCannotClearAnIdentifiedWait
ABackDatedRestoreApprovalStillConfirmsAfterMoreThanTheCausalWindowOfLaterEvents
```

The exact command used `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter` with these names ORed by `FullyQualifiedName~`, outside the sandbox. Result: **7 failed, 2 passed, 9 total**. The two passing cases use PostToolUse; the failing PreToolUse cases and other fixtures are classified in the report rather than accepted or discarded solely by their outcome.

No raw test stack traces or machine-specific paths are copied into this evidence.

## Package and identity

The 0.1.9014 ZIP hash independently matches the recorded SHA-256 `8B78EB348A651C1F39FC71BBCE8BE0F63CBECAE20875136AA810133135312EB2`. No installation was performed. Package-time source identity binding is documented in implementation-002; no new benchmark was run.

Before/after implementation identity matched: `head=aadbfb3 baseline=aadbfb3 files=16 diff=697116446e1c untracked=3/1b89a893996b`.
