# Proposal

## Why

Five work conversations launched in the same D360 checkout all displayed its folder and branch. Bell Tower replaced only part of the legacy session-badge resolver: it misses work-touch evidence, native custom titles, history recovery, ticket fallbacks and the ticket checkout association.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Resolve both providers' display names as Bell Tower manual title, explicit provider-native custom title, then an automatic subject (no session role in names or speech; legacy role inputs ignored) from pinned change, active work association, unique branch/ticket match, ticket, branch or directory fallback.
- Observe successful change edits and workflow skills, bounded supported shell forms and successful post-tool events; read-only inspection of another change must preserve the active association.
- Recover explicit titles and eligible work evidence from a bounded incremental conversation reader on initial registration and resume, without copying transcripts into Bell Tower state.
- Store the working checkout separately from process/launch directory and planning root; verify unique worktree mappings and offer explicit checkout association when ambiguous.
- Share one identity across both clients' supported native/bundled displays, managed tab, chip and speech; normalize spoken subjects, preserve overrides/resume colors, add color/reset controls and avoid live palette collisions.
- Support explicit legacy badge import including OSC rgb background conversion, without depending on the old mod.
- Reconcile older provider-specific naming defaults while retaining safe runtime/name transports and extending shared work/inspection, overrides and recovery to both clients.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-identity`: Shared provider identity precedence, work evidence, resume recovery, checkout association, palette controls and consistent presentation.
- `standalone-setup`: Explicit compatible import of legacy badge metadata and bounded migration.

## Impact

Core/SessionIdentity, SessionPresentation, IdentityText and Palette; Providers selection/status-line readers; a Claude transcript adapter; Sessions/SessionCatalog; tray editors and speech formatting; IO/GitReader, IdentityStore; legacy import and metadata tests.

Execution dependencies: `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`. Coverage: Field report 7, 8 and 9; the complete session-badge behavior report and the confirmed inspection-does-not-rename decision.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Native-title writing scope amendment (2026-10-24)

By user decision, Bell Tower writing its name into provider-native session titles (Claude `/resume` title and any extension of Codex native names) moves to its own researched change, `sync-native-session-titles`. This change reads verified explicit native titles for both clients, never treats generated titles as user overrides, and preserves the already shipped Codex native-name synchronization unchanged. No Node runtime or SDK helper ships with this change.

## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
