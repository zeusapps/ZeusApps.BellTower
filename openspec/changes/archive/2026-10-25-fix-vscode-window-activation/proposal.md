# Proposal

## Why

The user reports that clicking an attention chip selects the correct VS Code terminal while its window remains behind another application or minimized. The bridge also leaked 114 temporary files after failed atomic renames, interrupting focus processing.

## What Changes

- Apply this feature to both Claude Code and Codex by default, using shared product policy; record verified hard provider restrictions with versions, evidence and supported fallbacks.
- Bind bridge registrations to a verified owning editor window and use a supported activation path with a narrowly verified native fallback when required.
- Restore only a minimized owning window, preserve maximized/fullscreen state and reveal the exact terminal without routing to another panel terminal.
- Acknowledge navigation only after the owning window is confirmed foreground and the matching terminal reveal succeeds; preserve waits and failed-navigation timers.
- Clean up temporary writes on every failure, bound retry/log behavior and prevent a registration-write failure from unnecessarily starving valid focus requests.
- Keep bridge compatibility and deferred editor activation explicit during installation and updates.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `terminal-navigation`: Verified editor foreground activation and recoverable bridge storage.

## Impact

vscode/extension.js, focus.js, service.js and Node tests; Hosts/VsCodeNavigator, HostRegistration, WindowProbe and activation interfaces; packaged bridge acceptance.

Execution dependencies: `reconcile-session-liveness-and-title-refresh`. Coverage: Original user foreground-activation report; field report 5. Host behavior applies to both providers; provider APIs stay unchanged.

This is required for the single next release after 0.1.8. Deliver each feature for both Codex and Claude Code through shared product policies and provider adapters. Preserve data, user overrides and safe runtime compatibility; provider-specific limitations require versioned evidence and a supported fallback. Test only isolated data/configuration, fake or fixture agents and silent sinks; never modify production settings, transcripts or sessions. The ordered release handoff is docs/handoffs/next-release-plan.md; the design and tasks repeat all context needed for this change.


## Provider parity amendment

The user's later instruction supersedes earlier requests to freeze Codex behavior. This is one Bell Tower feature implemented for both integrations; source-specific mechanics and native display restrictions belong in adapters. Missing implementation or evidence is an outstanding task, not a provider exception. The existing change ID reflects the originating field report and does not restrict its provider scope.
