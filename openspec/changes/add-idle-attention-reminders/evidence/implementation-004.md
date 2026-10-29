# Implementation handoff 004: isolate healthy playback from worker scheduling

Date: 2026-10-28. User-authorized focused correction after target-003; scope limited to `ReminderPlaybackTests.cs` and `WindowsUserIdleSourceTests.cs`. No product code, docs, config, or source behavior changed.

## Fixture correction

The target-003 full-suite failure was the healthy-wrapper queue fixture waiting for the audio sink while the production source's post-request `Task.Run` probe had an 80 ms production budget. Its observer was fast after execution began, but the fixture did not guarantee that a worker would start it within that production budget. QA recorded the test passing alone and failing once in the full suite. The analyzer reproduced worker unavailability as a possible mechanism; that does not establish the cause of QA's specific full-suite result.

Healthy success fixtures now make their preconditions explicit:

- `ReminderPlaybackTests` uses an internal five-second test budget for the healthy actual-wrapper predicate, queued healthy reminder, recent-input-before-playback, and mixed-tone scenarios. The queued healthy fixture first obtains a successful 20-minute source observation. The probe still runs through the actual `WindowsUserIdleSource` wrapper and actual tray validity predicate; recent input must still be detected by a new post-request probe.
- `WindowsUserIdleSourceTests` uses the same explicit test budget for responsive positive observations and the pre-request-poll scenario. Its observer gates establish whether a poll is already running and when it is released.

The product's default 80 ms budget, timeout behavior, and single-flight policy are unchanged. Separate fail-closed cases use the public default budget:

- `PlaybackRefreshTimeoutWithholdsCachedPositiveIdleAndConcurrentRequestsStaySingleFlight` first seeds positive idle, then starts and synchronizes a blocked background poll. `GetFreshSnapshot()` with no test override times out with unknown; concurrent fresh requests share that single blocked observation.
- `TimedOutFreshReadCannotAuthorizeAnOlderPositiveObservation` seeds an actual positive sample, starts a synchronized blocked new observation, then checks the actual tray reminder predicate with the source's public default budget. The queued item is withheld instead of using the old positive sample.

These tests separate successful-observation behavior from worker scheduling assumptions and retain explicit production-budget safety coverage. No thread-pool tuning, retries, production timeout change, or audio playback was added.

## Checks

- Focused tests outside the sandbox: **59 passed, 0 failed, 0 skipped**. Filter: `WindowsUserIdleSourceTests`, `ReminderPlaybackTests`, and `AudioQueueTests`.
- Full solution Release build: `dotnet build ZeusApps.BellTower.slnx -c Release --no-restore`; succeeded with 0 warnings and 0 errors.
- Scoped `dotnet format ... --verify-no-changes` for the two edited test files; passed.
- `git diff --check` for both edited test files; passed.
- Full default suite was not run. QA owns the next full run. QA-003's 1,085-pass/1-failure result remains a failed run; the bounded reproduction does not classify its cause.

## Exact changed paths and identity

```text
tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs
tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs
```

Identity command:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

The product/source/docs hashes match target-003. Final complete scoped target: `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/7001ad675d0d`. Tracked diff SHA-256 remains `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`; untracked aggregate digest is `7001ad675d0d`.

ANALYSIS-004 and REGRESSION-001 await independent review and QA's full regression run. Neither is self-closed. Implementation is frozen at this identity.
