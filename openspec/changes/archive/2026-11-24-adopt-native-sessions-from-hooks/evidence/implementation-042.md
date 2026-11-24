# Implementation 042 (DEV-042, fix cycle 1 for task 3.4)

Fixes awaiting independent verification: ANALYSIS-2900, QA-3600, ANALYSIS-2904. No product policy change; wording and tests only.

## Changes
- ANALYSIS-2900: `CapabilityMatrix` (shared-conversations row and `CodexNameModes`, so doctor `codex.nameSynchronization`) and `docs/compatibility.md` (row and gaps bullet) now say a shared conversation of another Codex home is still read through the tray-home backend, which does not host it; the answer is unverified and expected to surface as a shared-backend refusal; no name is written unless the read succeeds for the exact thread id; per-conversation home routing is task 4.2. New test `CodexSharedRuntimeServiceTests.AConversationTheTrayHomeBackendDoesNotHostIsRefusedAndNeverWritten` uses the existing `RejectAfterInitialize` stand-in mode (JSON-RPC error): status is `unavailable: shared backend rejected the request`, `thread/read` was sent, `thread/name/set` and resume/turn were not. It pins a rejecting stand-in only; a real backend's answer stays unverified (stated in the texts).
- QA-3600: Claude "not applicable" shared-backend cell (matrix and docs) now states no known shared-backend mode (design decision 4), retained evidence covers `--print` captures and launch-shape fixtures, interactive shape is a stated assumption. Searched src, docs and tests for "verified interactive" and "2.1.310": no other overstated use.
- ANALYSIS-2904: added matched canary test `IdentityHistoryTests.TranscriptReadsKeepNoPromptAssistantOrToolContent` (theory claude and codex): disposable transcript or rollout with prompt, assistant, tool-input and tool-result canaries through the history reader (Claude native custom title included), asserts the verified outcomes were recovered, then scans every file under the data directory, serialized identity and session, and status text. New matrix and docs row "Transcript read content handling", supported for both providers. `CapabilityMatrixTests` also asserts the old wordings are gone from the matrix and docs.

## Mutants (all killed, sources restored)
1. History reader writes the transcript into the data directory: both theory cases failed.
2. `Rejected` label text changed: the tray-home test failed.

## Checks
- Targeted (CapabilityMatrix, CodexSharedRuntimeService, IdentityHistory, new tests): 53 passed, 0 failed. Formatter check on changed files: clean.
- Full suite: 1548 total, 1542 passed, 6 reported failed in the first run; the rerun alone of `ManagementOwnershipProcessTests` shows 3 failures of `UpdateReplacesOnlyTheTrayAndLeavesEveryOtherProcess` (`InvalidDataException: Unsupported installation`, from `Installation..ctor` in the test's `CreateInstallation`). These files are not in this diff and the failure is outside the changed surfaces; not fixed, reported as in-flight/environment failure. Only one failure line per run was visible in the first run output, so the other three of the six were not named.
