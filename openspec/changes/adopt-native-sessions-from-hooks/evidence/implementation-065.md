# Implementation 065 (DEV-065, task 4.11 fix cycle 2 of 2)

## Changes
- ANALYSIS-5200: removed "as late as the end of the first turn" and the causal sentence "not a Bell Tower gap or a provider restriction" from docs/compatibility.md (matrix row and First-hook timing note), docs/installation.md, CapabilityMatrix.cs and DiagnosticsReporter.cs. One rule for both providers: Claude Code's color appears a few seconds after launch; Codex (`--no-daemon`) sends no hook at launch, so its color appears a few seconds after the first prompt is submitted, during the first turn. About 4 s was measured after the first accepted hook for both providers (Codex 4.4 s, Claude Code 4.2 s); stated as a known Bell Tower latency, deferred, not a provider restriction.
- ANALYSIS-5201 / QA-5900: added NavigationOutcomeTests for SaveFailedStatus: success returns the "Session focused; ..." text; a shared-backend refusal for `claude` and `codex` keeps NavigationReasons.SharedBackend, ends with the save-failure clause and does not contain "Session focused". Evidence 064's claim that SaveFailedStatus tests existed was wrong; it is now true.
- ANALYSIS-5101 residual: NavigationReasons.IsPermanentLimit remark now says an unregistered-host refusal can resolve for a direct capture whose host is learned later, while a headless run keeps an unstated permanent limit that keeps the timer.

## Checks
- Release build: 0 warnings, 0 errors.
- Focused tests (NavigationOutcome, ClickReason, CapabilityMatrix, Doctor, Docs): 97 passed, 0 failed.
- dotnet format --verify-no-changes on the four source files and the test file: clean.
- `openspec validate adopt-native-sessions-from-hooks --strict`: valid.
- grep "end of the first turn" and "12-54" in src and docs: 0 matches.
- review-identity: head=76beb52 baseline=76beb52 files=8 diff=87b115578128 untracked=0/e3b0c44298fc
