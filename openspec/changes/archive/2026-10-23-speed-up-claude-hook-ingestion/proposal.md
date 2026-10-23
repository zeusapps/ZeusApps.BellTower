# Proposal

## Why

Release 0.1.8 starts Git Bash, an encoded PowerShell wrapper, the stable launcher and the full application for every Claude event. The field install measured a 5.0 s median against a 3 s hook timeout and lost most tool hooks; delayed processing and mandatory pre-tool receipts then prevent correct state and naming.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Ship a small dedicated Windows x64 NativeAOT shared provider ingress executable that bypasses the full launcher, captures bounded input, process ancestry and creation identities, launch environment and ingress time, atomically spools an event, prints neutral JSON and exits.
- Move both providers' normalization and metadata observation into a bounded tray consumer; preserve durable ingestion when the tray is stopped or restarting and preserve versioned-update leases and compatibility.
- Generate direct fast hooks for both clients, using async where verified supported and an equally fast neutral capture adapter otherwise; document any hard transport restriction.
- Make successful provider post-tool observations usable without an earlier pre receipt, and make lifecycle/operation reduction tolerate late and duplicated delivery.
- Preview and migrate edited owned hooks explicitly; add sanitized failure causes, backlog/latency diagnostics and read-only transcript timeout summaries.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-tracking`: Durable, bounded shared ingress and causally correct delayed-event processing.
- `standalone-setup`: Direct capability-aware setup for both providers, edited-entry migration and useful hook diagnostics.

## Impact

New focused ingress project; Providers/HookIngestor, HookAdapter, OpenSpecSelectionObserver; IO/EventStore and StateStore; Core/Observation and SessionReducer; Setup/HookCommandBuilder and ProviderSettingsEditor; Diagnostics; packaging, version leases and process/ordering tests.

Execution dependencies: none; can be implemented against the 0.1.8 baseline. Coverage: Field report 1, 2 and 6; prerequisite for dependable naming and permission signals.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
