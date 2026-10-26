# Proposal

## Why

PermissionRequest begins an approval flow that may resolve automatically. Claude currently receives the same 15 s timing fallback as unverified hook waits, so slow automatic review can cause false alerts despite no human approval being required.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Track approval-flow starts from both providers as provisional metadata and use each provider's verified human-wait evidence (Claude permission_prompt, Codex owning-runtime status) through shared confirmation policy.
- Make confirmed Claude permission attention eligible without adding another fixed debounce to Claude's own notification delay.
- Handle missing operation IDs, delayed notifications, parallel requests and resolution before arrival without resurrecting completed waits.
- Report unavailable notification coverage honestly and retain safe native confirmation transport while giving both clients the same confirmed/provisional semantics and evidence-based fallback policy.
- Preserve question/completion timing, attention episodes, dismissal/viewed state and silent restoration.
- Keep question-tool approval flows (Claude `AskUserQuestion`, Codex `request_user_input`/`request_user_input_async`) as question waits: a correlated PermissionRequest or permission_prompt confirms the question instead of replacing it with permission attention, fixing the field-observed Question→Permission flip.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `attention-notifications`: Shared human-wait confirmation with provider adapters and consistent grace semantics.
- `session-tracking`: Correlated provisional and confirmed approval observations for both clients.

## Impact

Providers/HookAdapter; Core/Observation, WaitState, SessionState and SessionReducer; tray eligibility/diagnostics; provider and permission regression fixtures.

Execution dependencies: `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`. Coverage: Field report 10; prerequisite for dependable reminders and escalation.

Evidence: `evidence/claude-askuserquestion-hooks.json`, `evidence/codex-request-user-input-hooks.json` (2026-10-26 isolated captures).

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
