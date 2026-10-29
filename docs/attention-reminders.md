# Attention reminders

Bell Tower shares reminder policy and settings across Claude Code and Codex. A reminder is eligible only for a current actionable, unacknowledged episode on an attachment whose health is `verified-live`. Permission reminders require the existing supported human-wait confirmation; timing-only and visibly unverified approval requests remain ineligible.

Defaults are enabled, first reminder after five minutes, five minutes of local keyboard/mouse inactivity, ten-minute repeat interval and three reminders per episode. The first timer begins when Bell Tower first observes the actionable episode. Every reminder then requires both its deadline and the current idle threshold. Acknowledging, resolving, ending or replacing an episode cancels later reminders. Temporary loss of attachment verification suspends reminders and candidate exposure while retaining that same episode's ordinal, deadline and opaque receipt identity. Restoring verification resumes saved history and does not create catch-up bursts. Viewing a chip leaves the episode unanswered and does not cancel reminders.

Open the tray menu's **Attention reminders > Settings…** to review and save the enable switch, delay, idle threshold, repeat interval, per-episode cap and escalation ordinal. The settings form checks the values before writing. The JSON configuration uses the same names under `reminders`; missing fields use the defaults. Durations accept one minute through 24 hours, the cap accepts one through ten, and the escalation ordinal must be within the cap. An invalid reminder section leaves the last valid configuration in use. Unknown fields are preserved at both config and reminder-setting levels.

Reminders reuse the reason-specific alert tone and serialized queue. Voice reads “Still waiting” followed by the same session subject and reason. Mute, Voice, microphone-aware speech suppression and queue aggregation continue to apply. Suppressed or muted audio is not replayed later, while escalation eligibility is independent of audio playback.

State keeps a separate bounded `reminderReceipts` map keyed by provider-qualified session and opaque attention episode. It records the first observation time, next deadline, last eligible reminder, ordinal and opaque stable receipt ID. The receipt is atomically saved before audio enters the queue. Restart moves an already elapsed deadline into the future; missed reminders are not emitted in a catch-up burst. Disabling then enabling reminders establishes new future deadlines for active episodes.

## Escalation record for the webhook consumer

`StateStore.Escalations(currentSessions, now)` is the candidate query. The caller supplies its current provider projection; the method also reads current persisted ownership and configuration. It returns previously earned candidates only while reminders remain enabled and the episode is currently actionable and verified-live. Reminder eligibility is earned after the configured deadline and idle threshold; a new idle-threshold interval is not required for each candidate query. Codex provisional permission waits require current runtime confirmation. Muting and audio failure do not affect candidates. Temporary loss of verification or confirmation suspends exposure but preserves the durable receipt and its stable identity; resolution, acknowledgment, ending or episode replacement removes it. Diagnostics does not have the live provider projection and reports candidates as unevaluated. The record has protocol version 1:

```json
{
  "protocolVersion": 1,
  "providerSessionKey": "claude:opaque-session-key",
  "episode": "opaque-attention-episode",
  "reason": "Question",
  "waitStartedAt": "2026-01-01T14:00:00Z",
  "reminderOrdinal": 1,
  "eligibleAt": "2026-01-01T14:05:00Z",
  "receiptId": "opaque-stable-receipt"
}
```

The record contains no prompt, command, transcript, endpoint or credential. It is published at the configured reminder ordinal even when audio is muted or playback fails. This feature performs no network requests. A future webhook consumer should attach the current permitted identity projection and use `receiptId` as its stable per-episode idempotency reference.

## Input observation

On Windows, the probe uses `GetLastInputInfo` and the current process session's `WTSQuerySessionInformation(WTSConnectState)` result. A disconnected/inactive session, unavailable API, stale bounded observation or inconsistent tick yields unknown and suppresses automatic reminders. Playback requests a fresh single-flight observation with a fixed wait budget on the probe worker; a background sample taken before that request cannot authorize a reminder, and a timeout yields unknown. The Win32 `dwTime` is a 32-bit system-uptime millisecond tick; elapsed time uses unsigned subtraction from `GetTickCount64`, preserving short legitimate wrap intervals. Durations above half the 32-bit tick range (about 24.8 days) are rejected as ambiguous, including future ticks that would otherwise appear nearly 49.7 days old. This supports the full configured 24-hour threshold and longer ordinary inactivity. Future or stale observation timestamps and negative or out-of-range idle durations also yield unknown. These APIs report current interactive-session activity and do not observe other sessions.

## Verification boundary

Deterministic tests use injected time and idle values, synthetic Claude/Codex waits, mixed sessions, silent audio sinks and disposable state directories. They do not generate input, open a microphone, play physical sound, or send webhooks. Doctor reports the shared settings and current idle-observation status; candidate count is unevaluated because doctor has no current provider projection. Native user acceptance remains part of the integrated release gate.
