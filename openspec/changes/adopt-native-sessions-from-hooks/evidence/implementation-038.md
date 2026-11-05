# Implementation 038: fix cycle 2 for DEV-036/037 (task 3.2)

Developer DEV-038. Fixes await independent verification; nothing here is closed by the developer. Evidence is simulated (stand-in backends); no native check.

## ANALYSIS-2500: stalled shared backend was never recorded

Cause: both services pass a 3 s caller token to the open, and the shared transport's connect and handshake budgets were also 3 s and started later, so the caller token always fired first; `CodexSharedAccess` treats that as caller cancellation and recorded nothing.

Fix: new `CodexTransportBudget.Shared` (connect 1 s, handshake 1.5 s, read 3 s), the default of `CodexSharedTransport`. Open phases end at most 2.5 s after start, before the 3 s caller token, as a reasoned `Timeout`. A real caller cancellation (stop or dispose) still passes through unchanged and counts nothing. The owned path, its budgets and exception types are unchanged. A stalled read after a fast open is still bound by the caller token (unchanged, outside this finding).

Tests (production budgets, silent stand-in): `AStalledSharedBackendIsRecordedOnceAsATimeoutAndBacksOffTheNameService` (status "unavailable: shared backend did not answer in time", failure count 1, second Sync makes no connection) and `AStalledSharedBackendBacksOffThePermissionPumpForTheWholeHome` (count 1; a second poll with another conversation of the home: stand-in Accepted stays 1).

## ANALYSIS-2401 residuals

`CodexSharedAccess` keeps a per-home generation, changed under the access gate on every recorded failure and every success. The due check and the snapshot of the generation are taken in one gate section (closes the non-atomic Due/snapshot); a failure is recorded only if the generation is unchanged (closes the ABA after a success, and a failure that started before a success).

Tests: `AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain` (the analysis-025 sequence; count stays 1), `AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` (count stays 0), the concurrency test now asserts `Timeout` for each open and `Accepted == 4`, and QA-3200 `SequentialFailuresInLaterWindowsAdvanceTheBackoff` (counts 1, 2, 3 in later windows).

## Comment residuals of ANALYSIS-2400

`ICodexTransport.RequiresProvenThreads` now says exact id equality on a peer-verified backend is the proof and that the fork and agent refusal is a conservative policy. The `CodexThreadIdentity` sentence says a missing id is refused and absent fork and session fields read as a root. `docs/architecture.md` line 30 mentions the generation and the shared budgets.

## Mutants (each reverted fix fails a test; restored byte-identical)

- Shared budget back to `Default`: 3 failures (both new service tests, and `FailuresAfterTheHandshakeCarryAReason` Refused case, which is sensitive to the longer budgets).
- Snapshot from `schedule.Failures` instead of the generation: ABA test fails.
- Compare with `0` instead of the snapshot (QA-032 mutant C): ABA test and the sequential-advance test fail.
- No generation bump on success: `AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` fails.
- Non-atomic Due/snapshot: a few-instruction window, not deterministically testable; closed by construction (one gate section), no mutant test.

## Checks

Targeted access and service classes 42/42; full suite 1515/1515 (final tree, mutants restored and rebuilt). Formatter run on changed files.
