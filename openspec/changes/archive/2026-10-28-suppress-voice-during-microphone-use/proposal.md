# Proposal

## Why

Spoken Bell Tower notifications can interrupt calls. The next release must preserve notification tones while withholding speech when the current Windows interactive session is using its microphone.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Add a bounded current-user/session best-effort microphone activity probe with explicit active/inactive/unknown states using documented shared-mode capture-session observations across active capture endpoints. Inactive means no observed active shared-mode stream, not proven global microphone idle. Exclusive-mode capture may be missed; RDP and probe failures yield unknown.
- Recheck microphone state at audio playback time and suppress only speech while active; retain tones, mute, voice preference and serialized aggregation.
- Provide a persisted opt-out and diagnostics; use tone-only fallback when the probe cannot establish a safe speech state.
- Apply the audio behavior consistently to both providers and later reminder notifications.
- Verify through fake probes and recording sinks without opening a real microphone or playing sound.

## Capabilities

### New Capabilities

- `microphone-aware-audio`: Current-session microphone detection and tone-only speech suppression.

### Modified Capabilities

- `attention-notifications`: Speech eligibility at playback time while retaining chime and aggregation behavior.

## Impact

Tray/AudioQueue, WindowsAudioSink and IAudioSink boundary; Core/AppConfig and config editing; a Windows microphone probe; audio policy/tests and compatibility docs.

Execution dependencies: none; can be implemented against the 0.1.8 baseline. Coverage: Field report 11. This shared audio feature applies equally to both providers.

On 2026-10-28 the user explicitly selected best-effort detection after investigation found that the documented audio-session API excludes exclusive-mode streams and the proposed CapabilityAccessManager registry activity semantics lacked a verified primary contract. Speech resumes when no observable capture stream is active; exclusive-mode use may be missed. This accepted platform limitation applies equally to both providers.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
