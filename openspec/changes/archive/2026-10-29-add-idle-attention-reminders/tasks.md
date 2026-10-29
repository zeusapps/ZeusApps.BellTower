# Tasks

## 1. Dependencies and settings

- [x] 1.1 Verify lifecycle health, Claude human-confirmation and microphone speech predecessors expose their final contracts; read this design and existing episode/audio validity checks.
- [x] 1.2 Add validated reminder controls with enabled/5-minute initial/5-minute idle/10-minute repeat/three-per-episode defaults and escalation threshold one; verify config reload, disable and invalid bounds.
- [x] 1.3 Add an injectable current-interactive-session idle probe with documented GetLastInputInfo/tick-wrap behavior; verify recent/idle/unknown and disconnected-session fixture cases.

## 2. Scheduling and receipts

- [x] 2.1 Implement a separate per-session/episode reminder scheduler that consumes only verified current actionable waits; verify threshold edges, recent input, provisional Claude approvals and stale attachments.
- [x] 2.2 Persist ordinal, deadline and receipt state before enqueueing without modifying provider wait state; verify crash/replay cannot duplicate a reminder and metadata refresh cannot reset it.
- [x] 2.3 Implement bounded repeats/caps and startup future-deadline restoration; verify long absence/restart produces no catch-up burst or extra provider episode.
- [x] 2.4 Revalidate current episode, liveness and acknowledgment before scheduling/playback; verify viewed-but-unanswered versus acknowledged behavior and cancellation after resolution/new episode.
- [x] 2.5 Route reminder AudioItems through shared serialized mute/voice/microphone/aggregation policy; verify muted reminders remain silent and do not replay later.

## 3. Escalation boundary and acceptance

- [x] 3.1 Publish the documented versioned escalation record after configured reminder eligibility independently of audio success; verify opaque receipt identity, allowed fields and invalidation on resolution/acknowledgment.
- [x] 3.2 Add status/config documentation and receipt pruning; verify unknown-field preservation and no production writes in tests.
- [x] 3.3 Run deterministic reminder/restart/input-race integration tests and existing policy/audio tests; verify formatting and Release build and export the final escalation schema to the webhook change.


## 4. Provider parity acceptance

- [x] 4.1 Implement and verify this feature for both Claude Code and Codex using shared policy and provider adapters; run matched provider fixtures plus a mixed-session case and retain version/mode-specific evidence.
- [x] 4.2 Audit setup, defaults, configuration, diagnostics and controls for equal feature availability; document any hard provider restriction with its proof, honest shared fallback and regression check, and treat missing implementation or unverified capability as incomplete work.
