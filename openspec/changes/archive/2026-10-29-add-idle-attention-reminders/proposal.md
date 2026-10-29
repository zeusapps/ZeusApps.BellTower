# Proposal

## Why

A single attention notification can be missed while the user is away. The next release must repeat eligible unresolved attention after a configurable interval and interactive-session inactivity, without creating new provider waits.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Add configurable reminder delay, idle threshold, repeat interval and cap; use bounded GetLastInputInfo-based current-session idle observation.
- Persist per-episode reminder receipts and revalidate current waits, dismissal, input activity and liveness before scheduling and playback.
- Route reminders through the existing serialized audio path, preserving mute, microphone speech policy and aggregate behavior.
- Prevent restart catch-up bursts and publish a stable escalation eligibility record for the webhook change.
- Provide configuration controls and silent deterministic timing/restart tests.

## Capabilities

### New Capabilities

- `attention-reminders`: Idle-gated, persisted reminders for unresolved attention.

### Modified Capabilities

None.

## Impact

Core/AlertPolicy and reminder policy/receipts; tray scheduling and AudioItem; IO/state configuration with backward-compatible fields; Windows idle probe and policy tests.

Execution dependencies: `reconcile-session-liveness-and-title-refresh`, `confirm-claude-human-permission-waits`, `suppress-voice-during-microphone-use`. Coverage: Local reminders in field report 12.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
