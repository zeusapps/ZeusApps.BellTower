# Playback speech eligibility delta

## MODIFIED Requirements

### Requirement: Serialized chime and voice
For a newly actionable episode observed while Bell Tower is running and unmuted, Bell Tower SHALL play a bundled reason-specific tone and optionally speak the available role, at most four words of session subject, and reason. Defaults SHALL be doorbell for permission, magic ring for questions, interface sound for completion, and fail sound for errors. The app SHALL operate independently of Downloads and retain the PoC's 880 Hz, 400 ms chime as an explicit option. Users SHALL be able to select one copied custom PCM WAV for all reasons; source and normalized files SHALL each fit within 16 MB and last at most 30 seconds. Audio SHALL be serialized. More than two pending session phrases SHALL collapse to one phrase stating the count of sessions waiting and use the highest-priority reason's tone. Unavailable speech SHALL preserve tone-only operation. When microphone-aware suppression is enabled, speech SHALL additionally require a fresh inactive microphone state at playback. Active or unknown state SHALL retain tone-only behavior without altering Voice or replaying speech later. This shared audio policy SHALL apply to both providers and reminder playback.

#### Scenario: Several sessions require attention together
- **WHEN** more than two session phrases are pending
- **THEN** Bell Tower speaks a single count phrase instead of overlapping individual phrases

#### Scenario: A queued wait resolves
- **WHEN** its session no longer requires attention before its audio is played
- **THEN** that stale notification is excluded from playback and the aggregate count

#### Scenario: Microphone blocks aggregate speech
- **WHEN** several eligible alerts are aggregated while microphone usage is active
- **THEN** the highest-priority tone plays under the normal mute rules and the count phrase is withheld

#### Scenario: Best-effort microphone observation
- **WHEN** a complete fresh local observation finds no active shared-mode capture stream
- **THEN** microphone-aware policy may permit speech, while diagnostics disclose that exclusive-mode capture can be missed and RDP or observation failures yield unknown



## ADDED Requirements

### Requirement: Provider parity for microphone-aware audio in attention-notifications
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
