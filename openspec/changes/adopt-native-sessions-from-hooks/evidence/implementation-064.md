# Implementation 064 (DEV-064): task 4.11 fixes

Builds on the uncommitted DEV-063 state (baseline 76beb52). Evidence 063 is unchanged; corrections to it are stated here.

## Fixes
- ANALYSIS-5102: `NavigationOutcome.SaveFailedStatus(result)` selects the status after a failed viewed-state save. Success keeps "Session focused; the timer preference could not be saved."; a refused result keeps its stated reason and appends "The timer preference could not be saved." Log code `viewed-wait-save-failed` unchanged. Unit tests cover success and the permanent-limit result for both providers.
- QA-5801 ordering: in `TrayApplication.Navigate` a success marks the episode viewed as before; a refused result (permanent limit) is marked viewed only after `notice.ShowNotice` returned, so a failure to show the reason leaves the timer visible. The episode is still captured at click time; Overview and CLI still never hide timers (`hideTimer`). The ordering lives in the tray method and is not unit-testable without a live tray; the decision parts are.
- Codex color timing: first-hook timing is stated per provider (Claude Code sends its first hook at launch; `codex --no-daemon` sends none at launch, first accepted hook arrived 12-54 s after start in trial 6.1, color can appear as late as the end of the first turn), not as a Bell Tower gap or provider restriction. Updated docs/compatibility.md (matrix cells, "First-hook timing"), docs/installation.md, CapabilityMatrix.cs and DiagnosticsReporter.cs. The "reset assumed until the 6.1 trial" wording is unchanged. No test pinned these strings.

## Corrections to evidence 063 (ANALYSIS-5101)
- Headless runs (`claude -p`, `codex exec`) get Host=unknown and the generic "No verified terminal destination is registered." That is a permanent but unstated limit, so it keeps the timer, which the spec allows. The 063 rationale that it "may be learned later" is wrong for that case.
- The Claude permanent-limit test uses a synthetic attachment, because a shared-backend Claude conversation cannot occur in practice.
- The per-provider temporary cases reduce to provider-neutral inputs (the rule inspects only the result message).
- `IsPermanentLimit` now notes that the comparison relies on the constant being passed through unchanged. No typed reason field (ANALYSIS-5100 deferred).

## Checks
Release build 0 warnings; focused tests (NavigationOutcome, ClickReason, matrix/doctor/docs/diagnostics filter) 111 passed; dotnet format verify on changed files; openspec validate --strict.
