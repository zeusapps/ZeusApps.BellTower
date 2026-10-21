# Proposal

## Why

Parallel Claude Code and Codex sessions need a single, recognizable signal when they require attention, whether they run in VS Code or Windows Terminal. The existing personal PoC couples this behavior to Claude hooks, separate session-badge tooling, and VS Code terminal focusing; Bell Tower should own its session identity and installation experience.

## What Changes

- Build a standalone Windows tray application in this repository, using the supplied PoC description as the behavioral baseline and reusing its source if it becomes available. Use WorkLog as a reference for a small, independently runnable .NET helper organized by responsibility.
- Normalize Claude Code and Codex session activity and attention events. Track foreground attention independently of background work, and distinguish a saved conversation from its current running attachment.
- Keep the floating strip limited to sessions requiring attention; provide an all-running-session overview from the tray.
- Own short session names, optional OpenSpec change associations, random dark backgrounds, brighter chip accents, and Git branch/worktree context. Use the same name and accent in terminal identity displays and Bell Tower chips.
- Bundle agent hooks, launcher helpers, and host integrations. Existing Claude badge tooling may be supported for migration but is not required.
- Support navigation to sessions in VS Code and Windows Terminal, with explicit capability reporting and a safe, visible fallback when exact tab/pane targeting cannot be verified.
- Preserve the PoC's alert timing, episode dismissal, silent restart, serialized chime/voice, live voice preference, mute, position persistence, and bounded logging.
- Investigate Codex footer extensibility and Windows Terminal targeting before committing to specific presentation or navigation mechanisms. Tracking remains available when an optional terminal decoration is unsupported.
- Keep setup limited to the hooks and terminal bridges required by this change. Defer MCP-based identity automation, direct WorkLog integration, shared helper components, per-repository managed instructions, and release/update automation to handoff.md for later discussion.

## Capabilities

### New Capabilities

- `session-tracking`: Provider adapters, session/run/terminal association, independent activity and attention, event correlation, and lifecycle reconciliation.
- `session-identity`: Standalone naming and palette ownership, optional change references, Git context, and consistent terminal/Bell Tower presentation.
- `attention-notifications`: Attention strip, tray overview, grace periods, episode acknowledgment, sound policy, and display persistence.
- `terminal-navigation`: Agent-independent focus routing for VS Code and Windows Terminal, including verified targets and explicit degraded behavior.
- `standalone-setup`: App-owned installation, integration diagnostics, runtime/config ownership, compatibility migration, and safe operation.

### Modified Capabilities

None. The project has no existing capability specs.

## Impact

- Introduces Windows desktop application, core logic, provider/host integrations, installation tooling, and tests; this repository currently contains workflow configuration only.
- Integrates with Claude/Codex hooks and user terminal configuration. Setup changes must be explicit, reversible, and preserve unrelated configuration.
- Proposed implementation direction: retain the PoC's .NET Windows desktop approach. Its source and reported 259 tests were not available for inspection here.
- Bell Tower owns its runtime data and config; legacy `~/.claude/` paths are compatibility inputs, not required installations. No live configuration is changed by this planning work.
- Scope remains personal, local Windows tooling. Teams webhooks, phone alerts, cloud synchronization, a custom agent runtime, and modifications to agent binaries are excluded. WorkLog-inspired integration and distribution extensions are discussion notes in handoff.md, not implementation requirements or dependencies.

