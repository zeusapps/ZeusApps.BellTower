# DEV-063 implementation evidence: task 4.11 (viewed timer after a permanent navigation limit)

Baseline: 76beb52. Decision: user decision 2026-11-23 (trial T-13), design decision 13, spec "Viewed wait timer".

## Change
- `NavigationReasons.IsPermanentLimit(FocusResult)` (src/BellTower/Hosts/NavigationReasons.cs): the single classifier, shared by Claude Code and Codex.
- `NavigationOutcome.ApplyViewed` (src/BellTower/Tray/NavigationOutcome.cs) marks the episode viewed for a success or a permanent limit, only for chip clicks (`hideTimer`), and only through `StateStore.MarkViewed(key, episode)` with the episode captured at click time, so a late result never hides a newer episode.
- TrayApplication.cs: comment only; Overview and CLI paths still pass `hideTimer: false`.

## Classification (evidence per reason)
| Reason | Result | Why |
|---|---|---|
| `SharedBackend` | permanent, hides | Conversation on the shared Codex backend never has a terminal of its own; only a different launch (profile or `--no-daemon`) creates a new session. |
| `TerminalUnverified` | keeps | Unrecognized mode may be learned from later evidence for the same session. |
| `ProcessUnverified` | keeps | Process re-verification can succeed; attachment can be refreshed. |
| "No verified terminal destination is registered." | keeps | Host kind may be registered or learned later; not a stated no-terminal limit. |
| Declined activation, stale/missing session, navigator failure | keep | Temporary, per spec scenario. |
| WindowOnly* reasons | already hide | They are successful focus results. |

## Tests (tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs)
Permanent limit hides (claude and codex attachments through the real Navigator), chip-only; 12 temporary-failure cases for both providers keep; unrecognized-mode conversation keeps; success hides (existing); late older-episode limit and late success do not hide the newer timer.

## Checks
Release build 0 warnings; NavigationOutcomeTests + ClickReasonTests 63 passed; `dotnet format --verify-no-changes` on changed files passed; `openspec validate adopt-native-sessions-from-hooks --strict` valid. Live tray not exercised (not performed).
