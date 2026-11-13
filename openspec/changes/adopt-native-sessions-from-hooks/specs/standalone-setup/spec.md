# Standalone Setup Delta

## ADDED Requirements

### Requirement: Native-command integration without dedicated profiles
Bell Tower SHALL offer reversible user-level provider hook and optional host-bridge setup sufficient for native session discovery without generating dedicated terminal profiles or importing shell launch functions. It SHALL NOT require aliasing or replacing `claude` or `codex`, changing PowerShell command precedence, or writing integration files into each project. Existing generated profiles, managed imports and launch functions SHALL remain usable and SHALL NOT be removed merely because native adoption is enabled.

#### Scenario: Hook and bridge setup only
- **WHEN** the user selects native-command integration for Claude Code and Codex
- **THEN** setup previews the selected global hooks and host bridge, preserves unrelated settings and leaves shell command resolution and terminal-profile definitions unchanged

#### Scenario: Existing personal commands are present
- **WHEN** PowerShell already has aliases or functions named claude or codex
- **THEN** enabling native adoption does not replace those commands or remove existing Bell Tower launch functions

#### Scenario: Hooks require trust
- **WHEN** either provider has not accepted the installed hook definitions
- **THEN** setup reports pending trust and does not claim that native session discovery is operational

#### Scenario: Monitoring is stopped
- **WHEN** a native session emits a hook while the Bell Tower tray is stopped
- **THEN** capture remains durable, monitoring starts through its existing independent lifecycle, and the hook does not start a heavy desktop process on every event

### Requirement: Separate native adoption capability diagnostics
Bell Tower SHALL report native adoption capabilities separately for conversation discovery, conversation/runtime health, terminal attachment, navigation precision, native-name access, title writes, background writes and restoration. Diagnostics SHALL identify the provider version, mode, host and verification status; a missing binding or unperformed trial SHALL be labeled unverified rather than a proven provider restriction. An exception SHALL be limited to the exact evidenced surface and SHALL retain independently supported features for either provider. A capability left out by an explicit user scope decision SHALL be labeled unfinished with that decision's date, not restricted.

#### Scenario: Conversation works without a terminal destination
- **WHEN** hooks and exact-conversation runtime evidence work but terminal binding is unavailable
- **THEN** diagnostics report the working conversation capabilities and unverified navigation/presentation separately

#### Scenario: Plain default Codex on a shared backend
- **WHEN** diagnostics describe a Codex conversation on a shared backend
- **THEN** tracking, alerts, name sync and permission status are reported as supported, navigation, title and color are reported as unfinished by user decision, and the Codex profile and `codex --no-daemon` are named as the supported routes

#### Scenario: A mode has not been tested
- **WHEN** a provider version or local, remote, shared or standalone mode lacks evidence for an operation
- **THEN** diagnostics do not generalize another mode's success or failure and release acceptance retains the missing verification explicitly

#### Scenario: Both providers share an adoption policy
- **WHEN** equivalent native adoption scenarios occur in Claude Code, Codex or mixed simultaneous sessions
- **THEN** the same user controls and evidence rules apply, with adapter-specific mechanisms, and the only parity gap is the documented shared-Codex terminal gap
