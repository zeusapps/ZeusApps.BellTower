# Implementation 043 (DEV-043, fix cycle 2 for task 3.4)

Fixes awaiting independent verification: ANALYSIS-3000, ANALYSIS-3001. No other product policy change.

## Changes
- ANALYSIS-3000: the Claude direct-clients cell (`CapabilityMatrix` and `docs/compatibility.md`) now reads "Claude Code is treated as direct (no known shared-backend mode; the interactive shape is a stated assumption, see the shared backend row)". `rg "always direct"` over src, docs, tests and vscode finds only the new negative assertions. `CapabilityMatrixTests.CompatibilityDocumentStatesMatchTheMatrixSource` now asserts neither the matrix nor the docs contain "always direct".
- ANALYSIS-3001: `ConfirmationCoverageReporter` counts a Codex session as verifiable when `CodexRuntimeOpener.Select` returns access (owned or shared), the single selection rule the permission pump uses; no second policy. Status text, `Configured` text and `Policy` text now say "neither a verified owning runtime nor a verified shared backend (explicit --no-daemon, remote, and shared conversations not yet reached by the task 4.2 binder)". `docs/compatibility.md` human-wait prose aligned and says a verified shared-backend conversation is counted verifiable. No consumer of the coverage fields exists in `vscode/` (only doctor output and tests). Claude coverage is unchanged.
- Tests (`ConfirmationCoverageTests`): `CodexCoverageCountsOwnedAndSharedBackendSessionsAsVerifiableAndOthersAsUnavailable` (owned and shared counted; unclassified shared and plain unreachable unavailable), `AVerifiedSharedBackendSessionAloneLeavesCodexCoverageWithoutAnUnavailableCount`, `ClaudeCoverageIsUnchangedByCodexBackendSelection`.

## Mutants (killed, source restored)
1. Count only owned runtime records (old rule): 2 new tests failed.
2. Count every session as verifiable: the new mixed test and the existing no-runtime test failed.

## Checks
- Targeted (ConfirmationCoverageTests, CapabilityMatrixTests): 19 passed, 0 failed. Formatter check on changed files: see handoff.
- Full suite (default output path): 1551 total, 1548 passed, 3 failed in the run. Rerun alone: `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks` and `CodexSharedRuntimeServiceTests.AStalledSharedBackendIsRecordedOnceAsATimeoutAndBacksOffTheNameService` pass; `CodexSharedAccessTests.AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` fails alone 4 of 4 (no exception thrown from the slow open; a timing race against a Silent stand-in). It is in `Providers/`, which this diff does not touch; reported as unrelated, not fixed.
- A first full run with an isolated `--artifacts-path` failed 164 process tests because they look for the executable under `src/BellTower/bin`; not a product result, discarded.
