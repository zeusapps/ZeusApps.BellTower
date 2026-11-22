# Implementation 005 (DEV-005, fix cycle 4): ANALYSIS-300, ANALYSIS-301, QA-300, QA-301

## Changes

- `src/BellTower/IO/RefreshHealth.cs`
  - ANALYSIS-300 / QA-300: `Quoted` now lets the body hold any apostrophe. A closing apostrophe must be followed by whitespace, punctuation or the end, and the text after it up to the next apostrophe or line end must hold no `\` or `/` (otherwise it is a possessive or inner apostrophe and the path goes on). `PathPattern` and `PathTail` let a non-final segment hold any apostrophe (a separator must still follow); the final segment keeps an apostrophe before a letter or digit. Two quoted paths on one line still mask separately.
  - Bounded cost: every sanitizer regex (generated and the two runtime ones) has a 20 ms match timeout (five rules, about 100 ms in total). `Describe` catches `RegexMatchTimeoutException` and returns the exception type name alone.
  - ANALYSIS-301: new `RecordAfter` (30 s). `Failed` records the current window (`failingSince` = window start, current reason) once it has lasted 30 s and the record shows no failure, even when its log line is suppressed by the per-reason limiter or the reason-cap notice. A logged failure is still recorded as before. Recovery is recorded whenever the record shows a failure; the recovery line is still written only for a logged window. Log lines are unchanged.
- `tests/BellTower.Tests/IO/RefreshHealthTests.cs`
  - 18 sanitizer cases for apostrophe before a space, digit or separator (quoted and unquoted; Windows, UNC, Unix; profile subfolder; `Rock 'n' Roll`, `Students' Work`, `Jones' Project`, `'90s`, `Bob'\f`), asserting no backslash or slash and none of `Project`, `Work`, `Roll`, `90s` and the other name fragments; a two-quoted-paths test.
  - Adversarial-input test (`\'a`, `'a/`, `/'a\ `, `'x\ ` to 2,000 characters): completes under 1 s, leaks no path, reason keeps the exception type.
  - `AFailureWindowIsRecordedOnceItHasLastedThirtySecondsEvenWhenItsLineIsSuppressed` (record at 36 s with the window start, recovery recorded though its line was never logged); `ALoggedFailureThatRecoveredAndFailsAgainForFifteenMinutesIsRecordedWithItsWindowStart` (failingSince equals the window start); `AlternatingManyReasonsWithSuccessesStaysWithinSeventeenLinesPerTenMinutes` (at most 9 failure/suppression lines, 8 recovery lines, 17 in all).
  - The existing test at the old lines 393-398 was updated: the record still shows no failure at 4 s, and shows `failingSince` = window start at 40 s with one logged line. The strict-alternation and 1-in-4 tests (one failure line, one record) pass unchanged.
- `docs/architecture.md` (tray logging paragraph): at most nine failure or suppression lines per 10 minutes, plus at most one recovery line per logged failure (at most 17 in all), and the 30-second record rule. `docs/hook-ingress.md` states no such bound, so it is unchanged.
- Line endings: both edited C# files are CRLF as before.

## Timings (sanitizer worst case, 2,000-character crafted message, scratch probe, since removed)

| Input unit | Before (old `Quoted`, no timeout) | New rule without timeout | After (`Describe`, 20 ms per rule) |
|---|---|---|---|
| `\'a` | 658 ms | 193 ms | 28-32 ms |
| `'a/` | 628 ms | 225 ms | 22-31 ms |
| `/'a\ ` | 372 ms | 131 ms | 28-32 ms |
| `'x\ ` | 552 ms | 214 ms | 30-31 ms |
| `'a\` | 1062 ms | 426 ms | 27-31 ms |
| `' a/ ` | 0.2 ms | 206 ms | 28-41 ms |

The probe used interpreted `Regex` objects for the unbounded columns, so absolute values are higher than the generated rules (analysis-004 measured about 150 ms for the old rule). The new unbounded rule is slower than the old one only on `' a/ `; with the timeout every case ends within about 40 ms and returns the type alone. Benign messages are unaffected.

## Results

- Release build: 0 warnings, 0 errors.
- RefreshHealth, StateReporter, BoundedState, AudioQueue and HookMaintenance tests: 151 passed.
- Full suite: 1761 passed, 14 failed: the known 11 HookTrayStartTests, ProcessStopAuthorityTests, and load-only flakes AudioQueueTests and HookMaintenanceTests, which pass in isolation (included in the 151).
- `dotnet format --verify-no-changes --include` on both changed C# files: passed.

## Review identity

`head=5e95554 baseline=5e95554 files=22 diff=bc52a297142e untracked=7/4ae8dd33c705`
(`./scripts/review-identity.ps1 -Baseline 5e95554 -Paths src/BellTower/IO src/BellTower/Core src/BellTower/Tray src/BellTower/Diagnostics tests/BellTower.Tests docs`)

## Notes

- Awaiting independent verification of ANALYSIS-300, ANALYSIS-301, QA-300 and QA-301; none is marked closed here.
- Quoted text that is not a path (unbalanced apostrophes with no root) is not masked, as before; the adversarial test prefixes a drive path.
- Not run: nothing against a real data directory, installation or live tray.
