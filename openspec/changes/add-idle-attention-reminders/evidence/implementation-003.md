# Implementation handoff 003: playback idle freshness

Date: 2026-10-28. Final permitted correction round, assigned only for ANALYSIS-004 / duplicate QA-004 and its affected regression. Baseline/head remains `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Correction

Added `IUserIdleSource.GetFreshSnapshot()` with a default implementation for existing/test sources. `WindowsUserIdleSource` overrides it with an 80 ms production budget measured by a monotonic stopwatch. It starts or joins the one in-flight background probe; if that probe started before the playback request, it waits only within the same budget and starts a new single-flight probe afterward. A reminder is authorized only by a successful, valid observation produced after that request. Timeout, stale/future input, malformed/unknown or failed observations return unknown and withhold the reminder. The potentially blocking WTS/input probe remains on a worker; normal `GetSnapshot()` status polling keeps its existing 250 ms refresh and 500 ms bounded cache.

`TrayApplication` now calls the fresh-read API at reminder playback and cancellation checks, then evaluates observation age against a timestamp captured after the bounded read. Its cached tray tick sample and pre-request background probes cannot authorize a queued reminder. Ordinary alert validity remains unchanged. Mixed audio continues to cancel speech containing a stale reminder while a valid ordinary alert can retain its tone.

## Regression coverage

- `WindowsUserIdleSourceTests.PlaybackRefreshObservesInputThatChangedAfterCachedIdleSampleAndHealthyIdleStillWorks`: actual wrapper, manual clock, idle-to-recent input at T+100 ms, and fresh successful positive result.
- `PlaybackRefreshTimeoutWithholdsCachedPositiveIdleAndConcurrentRequestsStaySingleFlight`: a timed-out refresh returns unknown despite a prior positive result; concurrent callers share one blocked probe.
- `PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation`: a pre-request in-flight background poll is not accepted; a second post-request observation is required.
- `ReminderPlaybackTests.ActualWindowsSourceBlocksStaleReminderQueuedBeforeRecentInput`: an idle reminder queued behind a synchronized sink barrier is removed after the actual wrapper observes recent input before playback.
- `ActualWindowsSourceDuringMixedToneCancelsStaleReminderSpeechAndKeepsOrdinaryTone`: source changes to recent input during the tone; stale aggregate speech is canceled while the valid ordinary tone continues.
- `ActualWindowsSourceAllowsHealthyReminderThroughQueue`: the default 80 ms path successfully produces a reminder through the silent queue when current input remains inactive.
- Existing mutable-source, future/stale, ABI, query failure, and audio queue fixtures remain in the focused set.

Fixtures use the real `WindowsUserIdleSource` wrapper with its injected low-level probe, manual time where applicable, synchronization gates, and silent sinks. No native input, UI construction, physical audio, production process or provider session was used.

## Checks

- Focused tests outside the sandbox: **59 passed, 0 failed, 0 skipped**. Filter: `WindowsUserIdleSourceTests`, `ReminderPlaybackTests`, and `AudioQueueTests`.
- Full solution Release build: `dotnet build ZeusApps.BellTower.slnx -c Release --no-restore`; succeeded with 0 warnings and 0 errors.
- Scoped formatting applied and `dotnet format ... --verify-no-changes` passed for changed C# paths.
- Scoped `git diff --check` and new attention-reminder documentation whitespace check passed.
- The full default suite was not run by the developer. QA owns the clean regression retry; QA-002 recorded four full-suite failures with isolated passes and an inconclusive baseline comparison. No cause is inferred and no unrelated code was changed.

## Correction-round paths and review target

Paths changed in this round:

```text
docs/attention-reminders.md
src/BellTower/Tray/IUserIdleSource.cs
src/BellTower/Tray/WindowsUserIdleSource.cs
src/BellTower/Tray/TrayApplication.cs
tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs
tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs
```

Identity command:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

Final complete scoped target: `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/cbe46cdb0a67`. Tracked diff SHA-256: `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`; untracked digest: `cbe46cdb0a67`.

ANALYSIS-004 / QA-004 await independent recheck; they are not self-closed. The implementation is frozen at this identity. The final acceptance/regression decision remains with the orchestrator and QA.
