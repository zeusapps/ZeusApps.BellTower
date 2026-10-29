# Implementation 006 handoff

## Scope and changed paths

This round addresses ANALYSIS-101 and the deterministic-clock fixture gap in ANALYSIS-004 / REGRESSION-001. The allowed changes are limited to:

- `src/BellTower/Tray/TrayApplication.cs`
- `tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs`
- `tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs`
- `tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs`
- `tests/BellTower.Tests/Tray/ReminderFixtureDriverTests.cs` (new)
- `openspec/changes/add-idle-attention-reminders/evidence/implementation-006.md` (this handoff)

No other product policy, provider, configuration, documentation, or planning path was changed in this round.

## Corrections

### ANALYSIS-101 — driver-owned release and cleanup

`ReminderFixtureDriver.RunAsync` now releases held work from the dedicated driver thread's `finally` before it publishes the work result or exception. The asynchronous caller retains an idempotent fallback release, then joins the driver on success, operation failure, or timeout. Work and cleanup exceptions are both preserved when both fail. Dedicated concurrent consumers are all joined before a join failure is reported.

New tests verify that release runs on the same dedicated thread as work and completes before the call returns. A failure-path test verifies that the assertion exception and cleanup exception are both surfaced, with release still on the owned thread.

### ANALYSIS-004 / REGRESSION-001 — deterministic validity time

Added an internal `TrayApplication.ReminderValidityUtcNow` seam. Its production default is `static () => DateTimeOffset.UtcNow`. It is consulted only for reminder validity; ordinary alerts retain their existing direct UTC checks. Tests assign the same thread-safe manual clock to this seam and the actual `WindowsUserIdleSource` wrapper.

Coverage includes healthy idle; current input before queued playback; idle changes during a mixed tone; observation aging by 501 ms; future-dated observations; and an aggregated Claude-plus-Codex reminder batch. The existing production-default 80 ms held-probe rejection test remains separate from healthy path tests, which use an explicit five-second test budget.

Production policy constants and boundaries remain unchanged: source playback refresh is still 80 ms, and both observation freshness checks remain 500 ms. No timeout, freshness threshold, cached fallback, or global thread-pool setting was changed. The internal clock seam changes no user control or provider policy.

The correction addresses reproducible fixture assumptions; it does not establish the cause of earlier QA full-suite failures. QA owns the next full default suite.

## Checks

- `dotnet build ZeusApps.BellTower.slnx --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- Focused Release tests for `ReminderPlaybackTests`, `WindowsUserIdleSourceTests`, and `ReminderFixtureDriverTests` — passed, 18/18.
- Scoped `dotnet format` verification for the five assigned C# paths — passed.
- `git diff --check` — passed.
- Full default suite — not run; reserved for QA.

## Review identity

Ran:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

Output summary:

```text
HEAD: 47dfca9fd46b309017d4d785a1f91fd249d42041
Baseline: 47dfca9fd46b309017d4d785a1f91fd249d42041
Tracked diff SHA-256: 830d1d13ae60eebfe3fc8e8a018fd954502f784dc61225f48d9242217d6d2f51
Summary: head=47dfca9 baseline=47dfca9 files=31 diff=830d1d13ae60 untracked=18/53ddcdc3b756
```

Changed test file hashes from that scope:

```text
461bd8190cfdb5dcd37c984593c6173717d8cd7787dcfa8d4e5a0f8b418691dc  tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs
ed5274be1b98e15599d90b9f30d8dd2cab975f29a31f8baccbcbc1d5012d1c89  tests/BellTower.Tests/Tray/ReminderFixtureDriverTests.cs
5a32c2942279f8de665200e3fcf77edb1351b14a45b7421fff07853c4b7a14a1  tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs
017dd5bc2fb2adf62ffd1083188b6014e6c99042a1c8d9b61d08407dbeb626855  tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs
```

The scoped identity includes the shared feature implementation from earlier rounds. This round adds the clock seam and changes the four listed test paths. The target is frozen for independent QA and analysis; no finding is self-closed here.
