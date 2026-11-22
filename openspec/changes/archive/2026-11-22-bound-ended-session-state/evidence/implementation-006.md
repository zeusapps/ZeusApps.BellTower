# Implementation 006 (DEV-006, fix cycle 5): ANALYSIS-300 residual, ANALYSIS-400, analysis-005 observations

## Changes

- `src/BellTower/IO/RefreshHealth.cs` (CRLF kept)
  - Structural quoted-path rule (design decision 4, revised 2026-11-21). The profile prefix is first replaced by a private-use root marker (`\uE001`), like the user name (`\uE000`), so the profile counts as a path root. ANALYSIS-400: `'C:\Users\First Last\source\My Repo'` leaves no `Repo`.
  - `Quoted` is now one linear pattern: a quote (not after a letter or digit) followed by a root (`<data>`, the profile marker, a drive, UNC or Unix root), then `[^\r\n]*` up to the LAST quote on the line that is followed by whitespace, `.`, `,`, `;`, `:`, `)`, `!` or the end of text. The earlier lookahead pair and the 300-character lazy bodies are gone. The 20 ms match timeout and the type-only fallback are unchanged.
  - `PathPattern` gained a profile-marker alternative with the same segment rules as the other roots. The `PathTail` constant is removed (no longer used). Unquoted path rules are otherwise as DEV-005 made them. Any leftover marker maps to `<path>`.
  - Accepted over-masking: text between two quoted paths on one line is masked too; text after the last close is kept.
  - Housekeeping: the stale comment in `Succeeded` is replaced. It now says a window recorded after `RecordAfter` whose own line was suppressed gets one extra recovery line after a restart (bounded, one per restart), so no logged-window state is persisted. The sanitizer summary comment and the old `Quoted` comment are updated.
- `tests/BellTower.Tests/IO/RefreshHealthTests.cs` (CRLF kept)
  - Added to the apostrophe theory: `'D:\Shared\Students' Work.xlsx'.`, `'D:\Music\Rock 'n' Roll.mp3'`, the profile cases (`'C:\Users\First Last\source\My Repo'` with and without trailing text), a UNC final-name case, two Unix final-name cases (one under a profile-like `/home/First Last/...`). The fragment list gained `Repo`, `source`, `xlsx`, `mp3`, `txt`.
  - New theory `TwoQuotedPathsOnOneLineLeaveNoFragmentAndKeepTheTextAfterTheLastClose` (5 inputs: drive, apostrophes, profile, UNC, Unix; asserts the whole output is `Denied '<path>' today` and no separator or name fragment survives). New `ATrailingMessageAfterTheLastClosingQuoteIsKept`.
  - Adversarial theory gained `'/a `, `'C:\a ` and `'\\ ` (many quote and root starts).
  - New `ARestartAfterARecordedWindowWithASuppressedLineLogsOneRecoveryLine` documents and covers the extra recovery line (exactly one, not repeated on a second success).
- `docs/architecture.md`: the tray-logging sentence now says quoted segments from a path root are masked through the last closing quote on the line and text between two quoted paths may be masked too (the "quoted paths" wording is replaced). The file also holds the other change's uncommitted edits, untouched.
- Not edited: tasks.md, findings.md, design.md.

## Expectations changed only by accepted over-masking

1. `PathsInAReasonAreMaskedWhateverTheirRootOrSpaces...` and the other single-pair cases: no change.
2. `AnApostropheInAPathOutsideTheProfileLeaksNoFragmentOfThePath`: `Denied 'D:\a\Bob's x\f' and 'E:\b\Al's y\g' today` now gives `IOException: Denied '<path>' today` (was `... '<path>' and '<path>' today`).
3. The DEV-005 test `TwoQuotedPathsOnOneLineStillMaskSeparatelyWhateverTheirApostrophes` is replaced by the new two-path theory. Its two inputs (`'D:\a\x' and 'E:\b\y' today`, `'D:\Clients\Jones' Project\x' and 'E:\Al's y\g' today`) now give `Denied '<path>' today`; both are in the new theory.

No other existing case changed.

## Timings (2,000-character crafted message after `C:\Secret\`, 5 runs each, worst case, scratch test since deleted)

| Input unit | Worst |
|---|---|
| `\'a` | 17 ms (includes JIT of the first call) |
| `'a/` | under 1 ms |
| `/'a\ ` | under 1 ms |
| `'x\ ` | under 1 ms |
| `'/a ` | 5 ms |
| `'C:\a ` | 2 ms |
| `'\\ ` | 4 ms |
| `'C:\a' ` | 1 ms |
| `'/' ` | under 1 ms |
| `'/a' ` | under 1 ms |

Worst case about 17 ms, against 28-41 ms (timeout-bound) before. Every case completes inside the per-rule budget.

## Results

- Release build: 0 warnings, 0 errors.
- RefreshHealth, StateReporter and BoundedState tests: 122 passed.
- Full suite: 1777 passed, 14 failed. Failures: the 11 known HookTrayStartTests, ProcessStopAuthorityTests, and the load-only WindowsUserIdleSourceTests and HookMaintenanceTests. The last two passed in isolation (10 of 10).
- `dotnet format --verify-no-changes --include` on both changed C# files: passed.

## Review identity

`head=5e95554 baseline=5e95554 files=22 diff=9f3ab1aba1d2 untracked=7/9196c160cff0`
(`./scripts/review-identity.ps1 -Baseline 5e95554 -Paths src/BellTower/IO src/BellTower/Core src/BellTower/Tray src/BellTower/Diagnostics tests/BellTower.Tests docs`)

## Notes

- Awaiting independent verification of ANALYSIS-300 and ANALYSIS-400; neither is marked closed here.
- A quoted segment with no closing quote followed by a terminator is not matched by `Quoted`; the unquoted path rules mask it as before.
- Not run: nothing against a real data directory, installation or live tray.
