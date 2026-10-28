# Microphone-aware audio

## Purpose

Protect calls from spoken notifications by observing current-session microphone usage while retaining tones, user preferences and serialized audio behavior.

## ADDED Requirements

### Requirement: Current-session microphone state
Bell Tower SHALL obtain bounded best-effort active, inactive or unknown microphone-usage state for the current interactive Windows user/session without recording audio or opening a microphone stream. The probe SHALL observe shared-mode capture sessions across all active capture endpoints visible in that local session. Inactive SHALL mean no observed active shared-mode capture stream after a successful complete fresh observation; it SHALL NOT claim global microphone idle. Exclusive-mode capture may be missed, as explicitly accepted by the user. Remote/RDP sessions, unavailable, incomplete or stale evidence SHALL yield unknown. Diagnostics and capability documentation SHALL disclose this scope and limitation for both providers.

#### Scenario: Desktop call uses microphone
- **WHEN** the current-session probe has fresh evidence that an application is using the microphone
- **THEN** speech eligibility is false while ordinary tones remain available

#### Scenario: Probe unavailable
- **WHEN** the probe cannot establish current usage
- **THEN** the state is unknown, diagnostics explain the limitation and the default enabled policy uses tone-only for that playback

#### Scenario: No observable shared-mode capture is active
- **WHEN** a complete fresh local-session observation succeeds across active capture endpoints and finds no active shared-mode stream
- **THEN** the state is inactive and speech may resume under Voice/Mute policy, with no guarantee about exclusive-mode capture

#### Scenario: Remote interactive session
- **WHEN** Bell Tower runs in an RDP session whose microphone visibility is outside the supported local-session scope
- **THEN** the state is unknown and enabled suppression retains tone-only playback

### Requirement: Microphone speech policy controls
Microphone-aware suppression SHALL default to enabled and SHALL be individually configurable without changing the user's Voice preference. Playback SHALL recheck current state, preserve Mute and aggregation, and never replay previously suppressed speech after microphone use ends.

#### Scenario: Queued during call startup
- **WHEN** an alert is queued while idle but the microphone becomes active before playback
- **THEN** the alert plays its allowed tone without speech

#### Scenario: Call ends
- **WHEN** the microphone becomes inactive after a tone-only notification
- **THEN** that notification's speech is not played later

#### Scenario: Opt out
- **WHEN** the user disables microphone-aware suppression while Voice remains enabled
- **THEN** subsequent speech follows the existing Voice/Mute policy


### Requirement: Provider parity for microphone-aware audio in microphone-aware-audio
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
