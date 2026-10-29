# Attention Reminders Specification

## Purpose

Repeat missed attention only while the user is away, preserving provider state and durable per-episode scheduling across restarts.

## Requirements

### Requirement: Idle-gated reminder eligibility
Bell Tower SHALL provide configurable reminders for unresolved actionable, unacknowledged attention using current interactive-session input inactivity. Defaults SHALL be enabled with a five-minute initial delay, five-minute idle threshold, ten-minute repeat interval and three reminders per episode. Configuration SHALL reject invalid or unbounded values.

#### Scenario: User away
- **WHEN** a verified live session remains actionable beyond the initial delay and no local input occurs for the idle threshold
- **THEN** one reminder becomes eligible without creating a new provider wait

#### Scenario: Recent input
- **WHEN** the user provides keyboard or mouse input before a reminder deadline
- **THEN** the reminder is withheld until the idle threshold is reached again

#### Scenario: Dismissed episode
- **WHEN** the user acknowledges a chip
- **THEN** no further reminders are scheduled for that episode

### Requirement: Persistent bounded reminder receipts
Reminder receipts SHALL be scoped to session and attention episode and survive tray restarts. Replay, restart, configuration reload and metadata refresh SHALL NOT duplicate a reminder, reset the provider wait or produce catch-up bursts. Playback SHALL revalidate current episode and liveness.

#### Scenario: Restart with overdue reminder
- **WHEN** the tray restarts after a reminder deadline passed
- **THEN** it restores state silently and schedules future reminders without replaying overdue ones

#### Scenario: Wait changes while queued
- **WHEN** an old reminder is queued and the provider begins a new episode
- **THEN** the old reminder is discarded and the new episode has independent counters

#### Scenario: Mute and speech policy
- **WHEN** a reminder is eligible while Mute is enabled or microphone-aware suppression blocks voice
- **THEN** Mute suppresses audio; otherwise the existing tone-only policy is respected

### Requirement: Escalation eligibility interface
The reminder policy SHALL expose a stable per-episode escalation eligibility record after the first eligible reminder by default, independently of audio playback success. It SHALL contain only derived session identity, episode, reason, wait start and reminder ordinal and SHALL be invalidated on resolution, acknowledgment or loss of live ownership.

#### Scenario: Escalation eligible while audio muted
- **WHEN** an unresolved idle-gated episode reaches its configured escalation threshold while local audio is muted
- **THEN** one eligible escalation record is available without bypassing local mute for sound

#### Scenario: New episode
- **WHEN** the wait resolves and later needs attention again
- **THEN** the new episode has a distinct escalation identity


### Requirement: Provider parity for attention reminders in attention-reminders
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
