# Implementation evidence 002 (DEV-002, fix cycle 1)

Baseline 605a513, same worktree. The planning files `design.md` and `specs/session-identity/spec.md` were copied from the main checkout. Fixes: QA-001 (with duplicate ANALYSIS-002), ANALYSIS-001, ANALYSIS-003, ANALYSIS-004, ANALYSIS-005. QA-002 and ANALYSIS-006 are deferred and unchanged. Each fix awaits independent verification; none is closed here.

## Fixes

- **QA-001 / ANALYSIS-002** (`Palette.Allocate`): with every series color in use, entries compare by the number of in-use accents within `SameColorDistance` (fewest first), then the largest minimum OKLab distance, then series order. `AllocatePalette` now counts each session or pending launch once (a map keyed by identity key or launch id), so a session's own accent is not double counted. Tests: `RepeatsSpreadOverTheSeriesBeforeAnyColorIsSharedTwice` (13 live sessions on the full series plus `Series[0]` give the 14th `Series[1]`; sessions 13 to 36 cycle the series twice) and `FourteenthSessionSpreadsRepeatsThroughTheRealAllocation` for claude and codex. `docs/session-identity.md` and `docs/architecture.md` describe the repeat rule.
- **ANALYSIS-001**: background chroma 0.05, lightness 0.22 on even hue steps and 0.27 on odd ones; table regenerated, accents unchanged. Background to `#f1f5f9` contrast is 13.5:1 or better; accent to foreground still 4.6:1 or better. Minimum background distance among the first six entries is 0.048, among all twelve 0.045. Tests: the re-derivation test uses the new constants, and `TerminalBackgroundsOfTheFirstSixEntriesStayVisiblyDifferent` requires at least 0.04 (including entries 0 and 4).
- **ANALYSIS-003**: `LaunchPresentationStore.Validity` (24 hours) is the one constant used by `Read` and by allocation. An unconsumed launch counts for that validity only while `ProcessProbe.Identify(shell.Pid) == shell`, the check `Read` makes. Tests, for claude and codex: a two-hour-old launch with a live shell reserves its color; one with a dead shell reserves nothing; a launch older than the validity reserves nothing (updated `ConsumedAndExpiredLaunchPalettesDoNotReserveColors`).
- **ANALYSIS-004**: `verified-live` and `unverified` non-ended sessions always count; an `unavailable` one counts only while `LastObserved` is within one hour. Test `AnUnavailableSessionCountsOnlyWhileItWasObservedRecently` (claude and codex): a stale unavailable session holding `Series[0]` lets a new session get `Series[0]`, a recent one does not, and a three-day-old unverified session still counts.
- **ANALYSIS-005**: recent identity files whose name is the hash of a key the state lists are no longer read a second time (live ones are read once by key; ended ones never count).

## Measurement (ANALYSIS-005)

Test `AllocationStaysFarBelowTheIdentityLockTimeoutWithManySavedIdentities`, disposable data directory: 200 ended and 12 live sessions in the state, 212 recent identities, plus 5,000 older identity files. One `AllocatePalette` call took 11 ms on this machine (single run; the test asserts under 2 seconds, well below the 5-second identity-lock timeout). The state used was a few hundred sessions, not the 16 MiB budget; the cost of a full-budget decode was not measured.

## Checks

- `dotnet build -c Release --no-restore`: 0 warnings, 0 errors.
- Palette, series, allocation, policy, prelaunch and mixed launch classes: 123 passed, 0 failed.
- Full suite: 1867 total, 1850 passed, 17 failed:
  - 11 `HookTrayStartTests` (known).
  - 1 `WindowsUserIdleSourceTests.PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation` (known).
  - 4 `CodexNameTests` (they fail only inside a git worktree, as QA-001 verified on a clean export).
  - 1 `CodexSharedAccessTests.AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain` (Codex transport flake: 28 of 28 passed on an immediate re-run).
- `dotnet format --verify-no-changes` on the changed C# files: passed.
- `openspec validate distinct-session-colors --strict`: valid.

## Not performed

Task 4.3 (installed multi-session check with the user), including the six-session check with a 30-degree pair (entries 0 and 4).
