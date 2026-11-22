# Proposal

## Why

SHA-256 detects corruption but the 0.1.8 updater has no independent publisher signature. Managed machines also need an explicit setup control for existing automatic update discovery without enabling unattended installation.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Publish detached signatures over release metadata that binds archive hash, version, runtime and compatibility contracts; verify against an embedded trusted publisher key before accepting an offer or activating bytes.
- Use a minisign-compatible pinned-key signature scheme, include key identifiers and rotation/revocation rules, and keep signing credentials out of ordinary validation jobs.
- Expose setup and user controls for automatic update checks using the existing updates.enabled policy; installation continues to require explicit user action.
- Preserve updater rollback, immutable payloads, old session leases and disabled-policy behavior through setup and removal.
- Document the unsigned 0.1.8 trust bootstrap boundary and a manual verification route for the first signed release; do not claim the old client already enforces signatures.

## Capabilities

### New Capabilities

- `release-trust`: Publisher-authenticated release metadata and trust lifecycle.

### Modified Capabilities

- `standalone-setup`: Explicit reversible setup controls for managed-machine update discovery.

## Impact

Distribution release contracts and metadata; Updates/ReleaseClient, ReleaseOffer, ReleasePackage and UpdateTransaction; setup/config; scripts/package.ps1, GitHub release workflow, signature fixtures and upgrade acceptance.

Execution dependencies: `speed-up-claude-hook-ingestion`. Coverage: Field report 13. Existing checks are already opt-out; this adds exposed controls and publisher authentication.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
