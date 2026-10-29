# Implementation 005 handoff

## Scope

This round contains only the test-fixture scheduling correction for the accepted playback freshness finding (ANALYSIS-004 / REGRESSION-001). Production code and the 80 ms fail-closed playback budget are unchanged.

Changed paths in this round:

- `tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs`
- `tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs`
- `tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs` (new)
- `openspec/changes/add-idle-attention-reminders/evidence/implementation-005.md` (this handoff)

## Changes

- Added a test-owned dedicated-thread driver that signals completion asynchronously to xUnit and always releases and joins held work in `finally`.
- Moved native-observer seed/start checks, playback-predicate checks, and the held-probe timeout assertion onto the dedicated driver. Concurrent freshness callers use dedicated test-owned threads rather than consuming the shared ThreadPool while the probe is blocked.
- Changed queue sink start/next notifications to asynchronous task signals and made actual-wrapper queue tests await those signals. The tests still use the production `WindowsUserIdleSource` wrapper, `TrayApplication` predicate, and `AudioQueue`.
- The healthy queue scenario uses an explicit five-second test probe budget and first confirms the observer was able to provide a healthy sample. Recent input before playback and during a mixed tone remain covered through the actual wrapper.
- The separate fail-closed case retains the production-default 80 ms budget. It starts and holds the observer first, confirms that start signal, then verifies that neither playback validation nor eight concurrent callers can treat the earlier positive cached idle sample as fresh. This removes observer-start scheduling from that assertion.

The controlled scheduler-starvation reproduction documented in analysis-005 demonstrates one mechanism by which the old fixture could miss its signal. It does not establish the exact cause of either prior full-suite failure. No production scheduling policy, timeout, retry, or parallel-test setting changed.

## Checks

- `dotnet build ZeusApps.BellTower.slnx --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- Focused Release tests for `ReminderPlaybackTests` and `WindowsUserIdleSourceTests` — passed, 15/15.
- Scoped `dotnet format` verification for the three changed test files — passed.
- `git diff --check` — passed.

An initial Debug test invocation failed during compilation because the existing Debug reference assembly lacked managed metadata (`CS0009`), followed by cascading missing-namespace errors. The clean Release build and focused Release run above succeeded; no Debug artifacts were removed or altered manually.

## Review identity

Ran:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

Output summary:

```text
HEAD: 47dfca9fd46b309017d4d785a1f91fd249d42041
Baseline: 47dfca9fd46b309017d4d785a1f91fd249d42041
Tracked diff SHA-256: 3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296
Summary: head=47dfca9 baseline=47dfca9 files=30 diff=3c87f683b02f untracked=17/7591a4f4753a
```

The review scope includes the shared implementation files already present from prior rounds. This round's test files have these SHA-256 values in the scoped identity output:

```text
839f5a6371d1914151a2dd267104edf1bf03bfa4981394586492f80d166b3564  tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs
ba35a7bbddf005de1c5245eada47ac19aa455ce4a5f56f0d311b1adcc3c9fdd9  tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs
017dd5bc2fb2adf62ffd1083188b6014e6c99042a1c8d9b61d08407dbeb626855  tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs
```

No implementation outside the listed test paths was changed in this round.

## Handoff status

The target is frozen for independent QA and analysis. The full default suite remains assigned to QA. This handoff does not claim independent verification or attribute the earlier full-suite failure to the fixture scheduling mechanism.
