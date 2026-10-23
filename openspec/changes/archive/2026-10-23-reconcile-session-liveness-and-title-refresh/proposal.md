# Proposal

## Why

The field install retained null-process sessions as working forever and repeatedly retried their titles. The current title pump starts a full helper per session every two seconds and each helper recreates its Git cache.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Retain durable launch-shell ownership independently of the one-shot palette record and repair incomplete attachments from subsequent verified captures.
- Reconcile verified agent and shell exit; classify stale unverified sessions as unavailable after a configurable grace, without treating inactivity as proof of death.
- Resolve desired presentation in the tray using shared checkout caching, update only changed values and back off failed or unverified destinations.
- Keep process-wide console attachment inside an isolated bounded writer; end or cancel only Bell Tower-owned writers and retain presentation-lock/detach ordering.
- Expose attachment health and bounded failure diagnostics without flooding the log.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-tracking`: Distinguish live, ended and stale unverified attachments across restarts.
- `session-identity`: Bounded, change-driven title and palette synchronization.

## Impact

Hosts/TerminalTitlePump, TerminalTitleService, AttachedSessionConsole; IO/LaunchPresentationStore, AttachmentStore, StateStore and GitReader; launch/detach lifecycle, tray projections and host/process tests.

Execution dependencies: `speed-up-claude-hook-ingestion`. Coverage: Field report 3, 4 and attachment diagnostics in 6.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
