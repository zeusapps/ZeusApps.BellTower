# Developer handoff: idle reminders

Date: 2026-10-28

## Delivered

- Added shared validated reminder defaults and tray controls: enabled, 5-minute initial delay, 5-minute idle threshold, 10-minute repeat interval, cap three, escalation ordinal one. Invalid values are rejected without replacing the last valid settings; unknown config fields survive reload and update.
- Added a replaceable current-session idle source. The bounded worker observes `GetLastInputInfo`, reads current-session `WTSConnectState`, treats disconnected/unavailable/stale/inconsistent observations as unknown, uses unsigned subtraction for tick wrap and rejects a last-input duration greater than system uptime.
- Added state-locked reminder advancement for current, actionable, unacknowledged, strictly `verified-live` provider sessions. Claude and Codex waits use the same scheduler and keep separate receipt keys by provider-qualified session and episode. Runtime-confirmed Codex permission is accepted only when the current projection confirms it and a matching persisted wait remains; provisional and timing-only permissions do not qualify.
- Added atomic per-episode receipts before enqueue, a 10,000-receipt bound, quiet future deadlines after restart or re-enable, repeat caps and current-episode escalation candidates. The candidate has protocol version 1, derived key/episode/reason/wait-start/ordinal/eligibility time and opaque stable receipt ID; it is exposed by `StateStore.Escalations()` and performs no network operation.
- Routed reminders through the existing reason-specific serialized audio queue. Voice adds “Still waiting”; Mute, Voice, microphone suppression and aggregate playback remain authoritative. Stale aggregate members cancel aggregate speech after a tone, and an acknowledgment immediately invalidates local playback and persisted escalation eligibility.
- Added doctor status, tray settings UI, README and capability-matrix documentation, plus the webhook handoff schema and input-probe limits in `docs/attention-reminders.md`.

## Checks

- `dotnet build -c Release --no-restore` -> passed, zero warnings/errors.
- Focused `ReminderPolicyTests` -> 10 passed; reminder storage/config -> 1 passed; stale mixed-audio cancellation -> 1 passed; reminder audio aggregate -> 1 passed. All focused runs were performed outside the sandbox because in-sandbox VSTest could not connect to its testhost pipe after 90 seconds. The full suite is reserved for QA.
- Scoped `dotnet format ... --verify-no-changes --include ...` -> passed.
- `git diff --check -- README.md docs/compatibility.md docs/attention-reminders.md src/BellTower tests/BellTower.Tests` -> passed.
- No authenticated provider sessions, real input, microphone capture, physical audio, production config, install, or webhook send was used. Native user acceptance remains for the integrated release gate.

## Native documentation basis

- Microsoft Windows docs, `GetLastInputInfo`: https://microsoft.github.io/windows-docs-rs/doc/windows/Win32/UI/Input/KeyboardAndMouse/fn.GetLastInputInfo.html
- Microsoft Windows docs, `LASTINPUTINFO`: https://microsoft.github.io/windows-docs-rs/doc/windows/Win32/UI/Input/KeyboardAndMouse/struct.LASTINPUTINFO.html
- Microsoft Windows docs, `GetTickCount`: https://microsoft.github.io/windows-docs-rs/doc/windows/Win32/System/SystemInformation/fn.GetTickCount.html
- Microsoft Windows docs, `WTSQuerySessionInformationW`: https://microsoft.github.io/windows-docs-rs/doc/windows/Win32/System/RemoteDesktop/fn.WTSQuerySessionInformationW.html

The docs establish that `dwTime` and system uptime are milliseconds in 32-bit ticks and that WTS session queries return an allocated buffer that must be released. The implementation uses `GetTickCount64` for the full uptime and frees the WTS result. No provider-specific behavior is introduced.

## Scope and review identity

Implementation paths owned by this handoff:

```text
README.md
docs/attention-reminders.md
docs/compatibility.md
src/BellTower/Core/AppConfig.cs
src/BellTower/Core/AppState.cs
src/BellTower/Core/EscalationEligibility.cs
src/BellTower/Core/ReminderAdvance.cs
src/BellTower/Core/ReminderPolicy.cs
src/BellTower/Core/ReminderReceipt.cs
src/BellTower/Core/ReminderSettings.cs
src/BellTower/Core/ScheduledReminder.cs
src/BellTower/Diagnostics/DiagnosticsReporter.cs
src/BellTower/IO/ConfigStore.cs
src/BellTower/IO/StateSnapshotStore.cs
src/BellTower/IO/StateStore.cs
src/BellTower/Tray/AudioItem.cs
src/BellTower/Tray/AudioNotificationKind.cs
src/BellTower/Tray/AudioQueue.cs
src/BellTower/Tray/IUserIdleSource.cs
src/BellTower/Tray/ReminderSettingsForm.cs
src/BellTower/Tray/TrayApplication.cs
src/BellTower/Tray/UserIdleSnapshot.cs
src/BellTower/Tray/WindowsUserIdleSource.cs
tests/BellTower.Tests/Core/ReminderPolicyTests.cs
tests/BellTower.Tests/IO/StorageTests.cs
tests/BellTower.Tests/Tray/AudioQueueTests.cs
```

Baseline and head: `47dfca9fd46b309017d4d785a1f91fd249d42041`.

`./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md`:

```text
Summary: head=47dfca9 baseline=47dfca9 files=26 diff=dd04597165e1 untracked=13/471dd618c9a9
```

The identity manifest and SHA-256 per untracked source/doc/test file were recorded at the stable handoff. Unrelated `docs/handoffs/` and other OpenSpec drafts were preserved and excluded.

## Remaining

The orchestrator owns OpenSpec task checkboxes and review triage. QA owns the full regression run, provider and mixed-session acceptance review, and hygiene check. A code analyzer owns the independent architecture review. User-operated/native field timing remains unperformed. No commit, archive, install, or push was made.
