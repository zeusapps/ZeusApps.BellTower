# Proposal

## Why

Users should be able to run the native `claude` or `codex` command in an ordinary local terminal and have Bell Tower discover the conversation, without choosing a dedicated terminal profile. Investigation confirmed native hook delivery for both providers and access to Codex's existing shared backend, but exact terminal ownership must be established independently rather than inferred from the hook payload or stale backend environment.

## What Changes

- Adopt native sessions from trusted global hooks using shared identity, attention, lifecycle and capability policy for Claude Code and Codex. Dedicated profiles and existing launch functions remain optional, compatible entry points.
- Resolve direct local agent/shell process evidence and correlate it with existing host bridges. Bind shared Codex conversations through each conversation's verified client endpoint, actionable only while it is that client's most recent conversation, before enabling terminal actions for that mode.
- Add a non-owning connection to an existing local Codex backend for exact-conversation runtime status and supported native-name operations, preserving existing naming ownership controls. Connection permission does not confer backend lifecycle ownership.
- Separate conversation health, runtime access and terminal attachment health. A terminal can disconnect while its backend conversation remains active; a loaded thread alone is not proof that a terminal is open.
- Apply terminal presentation only after destination and presentation ownership are verified. Preserve existing managed-launch behavior, manual overrides, data and running sessions; report first-hook timing and unverified capabilities honestly.
- Make hook/bridge setup usable without generating terminal profiles or importing shell wrappers. Command shadowing, aliases and changes to PowerShell command lookup are not selected by this proposal.
- Retain the investigation in `evidence/findings.md`, with sanitized native-probe results and explicit unperformed acceptance checks.

## Capabilities

### New Capabilities

None; extend the existing capabilities rather than introduce a second session integration policy.

### Modified Capabilities

- `session-tracking`: Hook-triggered native adoption, independently evaluated conversation/runtime/terminal evidence, and non-owning runtime observation for both providers.
- `terminal-navigation`: Evidence-based binding of adopted sessions to existing host registrations, with shared-backend ambiguity handled explicitly.
- `session-identity`: Identity continuity and separately verified native-name and terminal-presentation capabilities during adoption, disconnection and rebinding.
- `standalone-setup`: Reversible native-command setup with optional terminal profiles and no required changes to shell command resolution; precise capability diagnostics.

## Impact

Affected areas include `BellTower.Ingress`, capture-v1 evidence, `AttachmentCapture`, attachment persistence/health, session reduction, Codex transports/runtime lookup, name and permission pumps, host navigation/presentation, setup, diagnostics, the VS Code bridge and their tests. Reuse shared policy and thin adapters; do not move host discovery or runtime RPC work into the fast hook path.

The design records the shared-Codex terminal binding decision, a client-endpoint binder with a latest-only rule based on native probe evidence, without assuming a provider restriction or a need for wrappers. It must compose with `fix-vscode-window-activation`, current identity controls and the unfinished `sync-native-session-titles` work without claiming those changes' acceptance. No new cloud service, provider binary patch or personal-session restart is proposed.
