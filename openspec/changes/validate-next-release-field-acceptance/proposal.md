# Proposal

## Why

Component tests passed for 0.1.8 while the installed hook chain, resumed Claude identity and VS Code foreground activation failed in real use. The retained repairs and additions in both field reports require installed-package acceptance. On 2026-10-29 the user cancelled remote webhook notifications, including Teams; that scope is removed rather than deferred or counted as implemented. Version 0.1.9 was published on 2026-10-27 with four of the retained changes (hook ingestion speed-up, liveness and title refresh, VS Code window activation, human permission waits); the remaining retained changes and later work exist only in development builds, so this gate now covers the first release after 0.1.9.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Add a packaged acceptance harness and release evidence manifest covering the retained feature changes and the post-0.1.9 work against the real installation layout and the published 0.1.9 archive as the upgrade and rollback baseline.
- Exercise matched Claude and Codex conversations plus mixed sessions, history recovery, distinct worktrees, delayed hooks, null-process startup and SHA-256-verified updates while enforcing matched feature acceptance for both integrations.
- Verify microphone-aware audio and idle reminders with silent sinks and fake device/idle probes.
- Maintain an explicit field-report coverage ledger and distinguish fixture evidence, desktop probes, live user acceptance and unavailable external prerequisites.
- Reconcile older active change artifacts/specs in execution order and prepare one immutable versioned release only after the integrated gate passes.

## Scope decisions (2026-11-22)

- Publisher-signed updates (`verify-signed-releases-and-update-policy`) are deferred to a later release by user decision. This release keeps the existing SHA-256 verification, claims no publisher signature and states that in its release notes. The deferral is not counted as implementation.
- Only `adopt-native-sessions-from-hooks` must still land before the release. `verify-codex-repository-subagents` and `sync-native-session-titles` move to a later release.
- Work completed after 0.1.9 joins the release scope for both providers: native session adoption from hooks (plain `claude`, `codex --no-daemon`, plain shared-backend `codex`), the hook-started tray with its Exit suppression, the Codex hook command shell fix, local idle reminders, microphone-aware voice, session titles, badge identity, and bounded ended-session state with the doctor state report. The dev Finalize step (`recover-unready-dev-operation`) is maintainer tooling; it is covered by documentation, not by product acceptance.

## Transferred acceptance (2026-10-29)

The user approved archiving `follow-openspec-session-titles` and `restore-claude-badge-identity` with three live checks still unverified: provider tool-hook captures, the two-session desktop identity trial and the five-conversation identity/worktree trial. Tasks 6.1-6.3 retain these required checks, their archived source IDs and provider/mixed-session criteria. Task 6.4 retains the documented ANALYSIS-300 fork parity gap and related unobserved capability evidence. This transfer changes evidence ownership only; it does not pass or waive release acceptance or authorize production session changes.

## Capabilities

### New Capabilities

- `release-readiness`: Installed-package acceptance and evidence required for the next release.

### Modified Capabilities

None.

## Impact

New packaged acceptance fixtures/scripts; scripts/update-acceptance.ps1 and release checks; docs/handoffs/next-release-plan.md, compatibility and release evidence; older OpenSpec reconciliation and version agreement.

Execution dependencies: shipped in 0.1.9: `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`, `fix-vscode-window-activation`, `confirm-claude-human-permission-waits`; archived after 0.1.9: `fix-codex-hook-command-shell`, `suppress-voice-during-microphone-use`, `add-idle-attention-reminders`, `follow-openspec-session-titles`, `restore-claude-badge-identity`, `bound-ended-session-state`; still to land: `adopt-native-sessions-from-hooks`. `verify-signed-releases-and-update-policy` is deferred (2026-11-22). Coverage: Release gate for retained field report items 1-13, badge parity, original VS Code report and confirmed release scope; item 12 retains local reminders only.

This is required for the single next release after 0.1.9. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
