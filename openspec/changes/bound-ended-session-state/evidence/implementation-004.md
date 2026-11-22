# Implementation 004 (DEV-004, fix cycle 3): ANALYSIS-200 and ANALYSIS-201

## Changes

- `src/BellTower/IO/RefreshHealth.cs`
  - ANALYSIS-200: the `Quoted`, `PathPattern` and `PathTail` rules let an apostrophe followed by a letter (digit in `Quoted`) belong to a path segment. `Quoted` closes only on an apostrophe followed by whitespace, `.`, `,`, `;`, `:`, `)` or the end, lazily, with an opening apostrophe not preceded by a letter or digit, so two quoted paths on one line are masked separately.
  - ANALYSIS-201: `Succeeded` no longer clears the per-reason limiter (`logged`, `lastSuppressedLogged`). New flags `windowLogged` (a failure of this window was logged) and `recordShowsFailure` (the record shows a failure). `tray-refresh-recovered` is logged only when `windowLogged`. The record is written for a failure only when it is logged and the record shows no failure yet or the reason changed (at most once per interval). The recovery record is written whenever the record shows a failure. A record read at the first success (earlier run) counts as a logged failure.
- `tests/BellTower.Tests/IO/RefreshHealthTests.cs`: added `AnApostropheInAPathOutsideTheProfileLeaksNoFragmentOfThePath` (the four cases from the finding plus two quoted paths on one line), `FailuresInterleavedWithSuccessesStayWithinTheBoundOfAContinuousFailure` (strict alternation and 1 failure in 4 ticks over 100 ticks: one failure line, at most 9 lines, final record matches the final outcome) and `AFailureStillFailingAfterTheIntervalIsLoggedAndRecordedAndEachEndedFailureIsRecovered`.
- No existing test needed adjusting; none encoded the reset-on-recovery behavior. All 13 existing sanitizer cases and the 4 apostrophe cases keep their outputs.

## Results

- Release build: 0 warnings, 0 errors.
- RefreshHealth, StateReporter and BoundedState tests: 80 passed.
- Full suite: 1735 passed, 14 failed. The failures are the 11 HookTrayStartTests, ProcessStopAuthorityTests, and two load-only flakes (CodexSharedRuntimeServiceTests, CodexTransportTests) that pass in isolation (45 passed).
- `dotnet format --verify-no-changes` on the whole solution reports only import ordering in `tests/BellTower.Tests.FakeLauncher/Program.cs` and `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs`, files outside this assignment (the other change). Restricted to my two files it passes.

## Review identity

`head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/e2351c042def`
