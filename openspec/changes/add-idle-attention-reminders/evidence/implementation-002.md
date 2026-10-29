# Implementation handoff 002: accepted reminder corrections

Date: 2026-10-28. Correction round assigned against frozen target-001; baseline/head commit remains `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Corrections made

- **ANALYSIS-001 / QA-001:** Separated receipt retention from immediate delivery eligibility. `StateStore.AdvanceReminders` retains a receipt when persisted state still proves the same unacknowledged, non-ended episode, even while attachment verification or transient runtime confirmation is missing. The policy only schedules from the strict verified-live actionable projection. Acknowledgment, end, or replacement prunes the old receipt. The ordinal, deadline, and opaque receipt/escalation IDs survive a temporary gap and restart; resumed episodes do not catch up.
- **ANALYSIS-002 / QA-002:** Changed `StateStore.Escalations` to require the caller's current session projection and current time, read current reminder configuration, and apply the same persisted/current ownership, acknowledgment, wait-reason, and current attention/confirmation gate used for delivery. The API does not require another idle-threshold interval after a candidate was earned; reminders themselves still require the configured idle boundary. Muting and audio outcome are not inputs. Doctor reports candidate availability as unevaluated because it has no current provider projection. Updated the documented API and v1 schema contract.
- **ANALYSIS-003 / QA-003:** Centralized observation freshness checks, rejecting future timestamps, negative idle values, durations above half the uint32 tick range (about 24.8 days), and unsigned tick deltas above that bound. This supports the full configured 24-hour threshold and longer ordinary inactivity while rejecting future post-wrap deltas near 49.7 days. Short valid wrap intervals remain accepted. The native poller throttles retries and permits only one in-flight probe; unknown/failure results remain conservative.
- **ANALYSIS-004 / QA-004:** Reminder playback validity now reads the injected idle source at each queue/sink validity callback rather than authorizing playback from the tray tick cache. Invalid members cancel reminder speech/tone, while a still-valid ordinary item can preserve its tone. Tests cover recent input before playback and during an aggregate tone.
- **ANALYSIS-005 / QA-005:** Settings save mutates the latest loaded `ReminderSettings` instance, preserving its extension data and root unknown fields. `ConfigStore.Update` continues to validate a clone before writing or replacing last-good state. Tests exercise the exact mapping and failed validation.
- **QA-006:** Added deterministic Windows connection-result mapping tests for active, disconnected, query-error and malformed-buffer cases; `LASTINPUTINFO` layout checks; manual-clock stale/future checks; bounded single-flight and query-failure fixtures; and wrap/future tick boundaries. No real input or native session mutation was used.
- **QA-007:** Added a serialized `StateSnapshotStore` / fresh `StateStore` restart scenario. It verifies receipt persistence before the scheduled item can enqueue, a 90-day absence without catch-up, stable receipt identity, and exactly one next ordinal. A Codex provisional-confirmation gap fixture verifies that exposure suspends while receipt ID/ordinal persist and the next ordinal resumes when current runtime confirmation returns.
- **QA-008:** Added an over-limit in-memory pruning fixture. It verifies inactive records are removed while a capped active episode retains its ordinal, deadline, and stable receipt ID. New records are admitted only when the persisted map is below the 10,000-record bound.

The parent clarified that a previously earned escalation candidate is not re-gated on a new idle interval for every enumeration. This contract is documented; current actionability, enabled configuration, ownership, and confirmation are still required each time it is exposed.

## Verification

- Focused tests, outside the sandbox: **68 passed, 0 failed, 0 skipped**. Filter: `ReminderPolicyTests`, `WindowsUserIdleSourceTests`, `ReminderSettingsFormTests`, `ReminderPlaybackTests`, and `AudioQueueTests`.
- Full solution Release build: `dotnet build ZeusApps.BellTower.slnx -c Release --no-restore`; succeeded with 0 warnings and 0 errors.
- Scoped `dotnet format ... --verify-no-changes` across all changed C# files; passed.
- The in-sandbox VSTest/formatter IPC setup remains blocked by named-pipe access. All assigned focused tests and format verification passed outside the sandbox. The full default test suite remains with QA, including recheck of `REGRESSION-001`; it was not run in this correction round.
- No live input, physical audio, provider-state mutation, network send, production config, installation, or process management was performed. These results are focused implementation checks, not independent review or native desktop acceptance.

## Exact implementation target and identity

Scope: `src/BellTower`, `tests/BellTower.Tests`, `README.md`, `docs/compatibility.md`, `docs/attention-reminders.md`.

Exact manifest paths:

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
tests/BellTower.Tests/Tray/ReminderPlaybackTests.cs
tests/BellTower.Tests/Tray/ReminderSettingsFormTests.cs
tests/BellTower.Tests/Tray/WindowsUserIdleSourceTests.cs
```

Identity command:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

Result: `head=47dfca9 baseline=47dfca9 files=29 diff=2cabb1effe53 untracked=16/08007d8b2afe`. Tracked diff SHA-256: `2cabb1effe533ce8f545f5f7b109e436656497058bf1c28e3786acb1b0cd1007`; untracked digest: `08007d8b2afe`.

Only these implementation paths and this handoff evidence were written in this correction round. Planning artifacts, tasks, findings ledger, prior evidence, reviewer reports, other change roots, and unrelated untracked drafts were left untouched. No commit was made. Independent QA and analysis rechecks are pending; implementation is frozen at this identity.
