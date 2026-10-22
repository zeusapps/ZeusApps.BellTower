# Proposal

## Why

Several Claude or Codex sessions often share one project directory, so the directory-derived title does not identify their work. Session titles should follow the OpenSpec change selected during the conversation, and Codex should expose that identity inside its native footer with useful Git context.

## What Changes

- Observe explicit OpenSpec change selection in a session's supported tool hooks and update that session's association when the selection changes. Never choose a repository-wide active change for all sessions.
- Preserve the existing precedence of manual name, selected change, and directory fallback; keep identity and palette across resumes.
- Refresh existing Bell Tower displays, Claude's bundled badge, and managed tab titles from the same session identity without resetting attention state.
- Add a capability-checked Codex integration that synchronizes the session display name to its native chat name and enables native footer fields for that name and branch. Include a readable linked-worktree label in the synchronized presentation when a dedicated native worktree field is unavailable.
- Preserve unrelated provider settings and report unavailable live synchronization or footer capabilities accurately.
- Following the approved runtime extension, investigate a dedicated connectable Codex app-server for each new managed local session; integrate it only after live native-name refresh is demonstrated, preserving session-specific terminal ownership and leaving existing sessions running.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-identity`: Follow a session's selected OpenSpec change automatically, preserve manual overrides, and present the resulting identity with branch/worktree context in supported native Codex displays.

## Impact

- Provider hook ingestion and new bounded selection recognition, identity persistence/catalog updates, and shared presentation formatting.
- Codex native-name synchronization, launch integration, reversible provider configuration, and capability diagnostics. Exact live transport support remains an implementation verification gate.
- Provider, identity, presentation, setup, and attention-preservation regression coverage; installation, compatibility, and live-acceptance documentation.
- OpenSpec remains optional. No WorkLog dependency, MCP server, shared project-level active-change file, patched agent binary, custom Codex renderer, or release tooling is introduced.
