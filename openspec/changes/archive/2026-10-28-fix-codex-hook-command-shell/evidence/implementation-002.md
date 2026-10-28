# Implementation evidence 002

Developer role model: `gpt-6-luna`.

## Fix round

This round addresses canonical `ANALYSIS-001`, `ANALYSIS-002`, and `ANALYSIS-003`, plus the task 2.2 failure-before-settings-write fixture requested by QA. Existing implementation and documentation changes were preserved.

| Finding / task | Change and evidence | Result / remaining limit |
| --- | --- | --- |
| `ANALYSIS-001`, task 4.2 | Shared `SessionReducer.ResumeForeground` now returns for non-main-thread starts. Matched Claude and Codex regressions cover captured and normalized ingress, direct operation waits and candidate waits. An unrelated subagent start preserves each wait; a later distinct foreground start resolves it. Existing correlated result behavior remains covered by reducer tests. | Focused tests passed. The policy remains shared across providers. |
| `ANALYSIS-002`, task 2.2 | The isolated Codex config is serialized from a Tomlyn model, including the project table key, rather than manually embedding a path in TOML. The native probe temp root now includes an apostrophe and Unicode independently of the retained helper path. | Codex CLI 0.164.0 delivered SessionStart using the fresh packaged helper and the special-character temp root. The fake loopback Responses endpoint and isolated `CODEX_HOME` remain in use. |
| `ANALYSIS-003`, task 3.2 | Receipt parsing verifies an object root and string timestamp before parsing. Matched tests cover `[]`, numeric, absent and invalid timestamps, valid lowercase `at`, valid legacy `At`, both providers, unmodified receipt bytes, unverified status and private-content omission. | Focused tests passed; malformed receipt shapes no longer abort the diagnostics report. |
| Task 2.2 failure safety | Added an opt-in native `SetupPlan` fixture for each provider. A successful generated command applies both settings and ownership changes. A deliberately invalid helper fails native verification and leaves both original files byte-for-byte unchanged; no capture protocol settings marker is written. | Both providers passed against the fresh package. This proves the failure path through the provider runner, not just call ordering. |

## Checks

- `dotnet build -c Release --no-restore` -> passed, zero warnings and errors.
- Focused `CaptureTests|HookDeliveryReporterTests|FastHookSetupTests` -> 57 passed.
- Opt-in native filter `OptInNativeProbeConfirmsRealProviderHookDelivery|OptInNativeSetupProbeFailureLeavesProviderSettingsAndOwnershipBytesUnchanged`, using package 0.1.9014 -> 4 passed. It exercised both provider delivery probes and both provider pass/failure settings snapshots. Codex CLI version observed for these probes: 0.164.0.
- Scoped `dotnet format ZeusApps.BellTower.slnx --no-restore --include ...` for the six changed C# files -> passed. The same command first hit a sandbox named-pipe access denial; the identical formatter command passed after auto-reviewed escalation.
- The same focused test command initially stalled before testhost output in the default sandbox. Its identical focused retry passed after auto-reviewed escalation. No full test suite was run in this implementation round; QA handles the independent full-suite retry.
- `scripts/package.ps1 -Dev -Offline` -> passed, producing ignored candidate `0.1.9014` with the documented NativeAOT packaging path. It was not installed.
- The scoped source review identity was captured immediately before packaging and after it. Both summaries matched: `head=aadbfb3 baseline=aadbfb3 files=16 diff=697116446e1c untracked=3/1b89a893996b`. The package release metadata records commit `aadbfb3` and `dirty: true`; the package ZIP SHA-256 is `8B78EB348A651C1F39FC71BBCE8BE0F63CBECAE20875136AA810133135312EB2`.
- `git diff --check` -> passed.
- Broader existing Core and Sessions regression command `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~BellTower.Tests.Core|FullyQualifiedName~BellTower.Tests.Sessions" --verbosity minimal` -> **failed: 7 failed, 310 passed, 317 total**. It ran outside the sandbox after the observed testhost IPC failure. Failures: `ReducerTests.MatchingOperationResumptionClearsPermissionWithoutResolvingOtherWaits` for both fingerprint-only variants (expected `Question`, actual `Permission`); `PermissionNotificationTests.PermissionResolvedDuringGraceNeverCreatesAChipOrSound` for Claude and Codex with `PreToolUse` at 5 seconds (permission wait remained); `ReducerTests.AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete` (expected no reason, actual `Permission`); `ReducerTests.UnrelatedResumptionWithIdenticalArgumentsCannotClearAnIdentifiedWait` (expected `Permission`, actual no reason); and `HumanWaitCorrectionTests.ABackDatedRestoreApprovalStillConfirmsAfterMoreThanTheCausalWindowOfLaterEvents` (expected causal event absent). This wider regression result is unresolved and requires orchestrator routing; no source changes were made after the package build.

## Acceptance still pending

- Task 1.1: Codex argv-array acceptance/rejection and source selection outside the previously tested PowerShell 7 environments remain unverified. This fix round did not infer a provider restriction.
- Task 1.3: Claude Code's own CLI hook runner remains unverified; the native setup probe exercises the configured Git Bash command.
- Task 2.3: the existing required 300 ms Codex median gate remains failed. The recorded run 08 has 121 ingress samples: median 385.4529 ms, p95 492.4313 ms, p99 594.4172 ms, max 595.8976 ms, 121 durable captures, zero failed calls. The matched PowerShell no-op median is 343.9491 ms. Their difference only suggests shell startup contributes; it is not an isolated helper-cost measurement. No new 120-sample benchmark was run because this fix round changes no ingress performance path.
- Task 5.2: the user-operated post-migration trial, mixed simultaneous sessions and trust review remain pending. The isolated native probes are not presented as that acceptance.
- No provider settings, user installation, user data or running user processes were changed.
