# Implementation 009 (DEV-009, fix cycle 2) - ANALYSIS-200 to ANALYSIS-206

Client: Claude Code developer subagent. Not committed.

## Per-finding resolution

| Finding | Resolution |
| --- | --- |
| ANALYSIS-200 | `OverviewForm`: the Clear checkout handler now calls `Configure(false, false, clear: true)`, which always sends `""` (shared policy, thin adapter). Test `ClearCheckoutOnAnUnpinnedRetainedRowStillSendsAnEmptyCheckout` (claude, codex). |
| ANALYSIS-201 | `IdentityResolver` update lambda decides retention on locked fresh state: `retained` is written only while `CheckoutPin is null` and the stored `CheckoutSource` still equals the snapshot's; otherwise `launch`. Test `AClearThatLandsMidPassIsNotUndoneByThePassThatStartedBeforeIt` (claude, codex; clear lands inside the `CheckoutCache` read; launch after that pass and after one more). |
| ANALYSIS-202 | The worktree listing cache stores a failed listing as `Paths = null` (a failure, never an empty success) with a 10 s life (`FailureLife`); success keeps 30 s. `IdentityResolver` gained an optional `listing` seam. Test `AFailedListingIsCachedAsAFailureForABoundedLife` (claude, codex): two passes within the life list once, a pass after 11 s lists again. ANALYSIS-181 tests still pass. |
| ANALYSIS-203 | Option 1. New optional identity field `SessionIdentity.CheckoutWork`, written with the snapshot work whenever the source is `unique-work`. The vanishing-folder case in `RetainedAsync` requires `ActiveWork == CheckoutWork`. 0.1.9 declares `[JsonExtensionData] AdditionalData` on `SessionIdentity` (`git show v0.1.9:src/BellTower/Core/SessionIdentity.cs`), so it keeps the unknown field. Tests: `UnrelatedWorkObservedAfterTheSwitchIsNotClearedByTheVerifiedCheckoutsBranchChange` (claude, codex; work B kept, source launch) and `TheVerifyingWorkFieldIsAdditiveAndKeptByAnOlderReaderThroughAdditionalData`. QA-181 tests pass. |
| ANALYSIS-204 | `RunReport.ToJson` adds `verdict` and `exitCode`; `AnUnfinishedItemIsCountedAsItsOwnOutcomeAndIsNeverAPass` asserts both. |
| ANALYSIS-205 | `scripts/field-acceptance.ps1` prints the message and `exit`s 2 (another run active) and with the harness code for any other non-zero code (1, 130, ...); header and `docs/distribution-validation.md` agree. |
| ANALYSIS-206 | `GitReader.Worktrees` removed. |

Docs: `docs/session-identity.md` (retained checkout paragraph), `docs/distribution-validation.md`.

## Results

- Release build: 0 warnings, 0 errors.
- Checkout/identity/tray/FieldAcceptance filter: 291 passed, 0 failed.
- Format verification on changed `.cs` files: clean.
- Full suite, run 1: 2218 passed, 3 failed (one named: `CodexTransportTests.ConcurrentConnectionsDoNotShareState`; the other two names were cut by `tail`). Run 2: 2220 passed, 1 failed: `LiveSessionColorAllocationTests.AllocationStaysFarBelowTheIdentityLockTimeoutWithManySavedIdentities` (2 s timing bound). `CodexTransportTests` and `LiveSessionColorAllocationTests` alone: 48 passed. These are load-sensitive tests unrelated to the changed code; the failure set differs between runs.
- `pwsh -File scripts/field-acceptance.ps1`, another run active (mutex held by a second pwsh): message printed, exit 2.
- Harness, all via `pwsh -File` with evidence under `evidence/`:
  - five-conversations (`-RebuildCandidate`): 84/86 passed, 2 unfinished, exit 3 (reaches the caller). Evidence `harness-five-conversations-009.md`.
  - packaged-hook-benchmark: 38/38, exit 0. Evidence `harness-packaged-hook-benchmark-009.md`.
  - `--scenario all`: 159/161 passed, 2 unfinished, exit 3. Evidence `harness-all-009.md`.
  - Production fingerprint unchanged in all runs.
