# Terminal Navigation

## Purpose

Navigate from a Bell Tower session to its hosting terminal using verified destinations and communicate when a host supports only partial navigation.

## Requirements

### Requirement: Agent-independent host routing
Bell Tower SHALL select navigation by the session's registered terminal host, independently of whether the agent is Claude Code or Codex. Clicking a chip or a session's overview navigation action SHALL request navigation only after user interaction.

#### Scenario: Codex runs in VS Code
- **WHEN** the user clicks its attention chip
- **THEN** Bell Tower routes the request to the registered VS Code integration

#### Scenario: Claude runs in Windows Terminal
- **WHEN** the user invokes its navigation action
- **THEN** Bell Tower routes the request to the registered Windows Terminal integration

### Requirement: Exact VS Code terminal selection
With a valid VS Code registration, Bell Tower SHALL activate the owning VS Code window and reveal the terminal corresponding to that live attachment. Multiple VS Code windows and duplicate terminal titles SHALL NOT cause another terminal to be selected.

#### Scenario: Two VS Code windows have identically named terminals
- **WHEN** the user navigates to a session in one registered window
- **THEN** that window's integration reveals the matching live terminal and the other window does not consume the request

### Requirement: Windows Terminal navigation capability
Bell Tower SHALL support navigation to a verified Windows Terminal window and SHALL report whether exact tab/pane selection is available for its installed host integration. Exact targeting SHALL remain valid across tab reorder, close, and pane changes or be downgraded before use. A remembered tab index alone SHALL NOT be accepted as a stable session destination.

#### Scenario: A Windows Terminal tab is reordered
- **WHEN** the user navigates to its session after reordering tabs
- **THEN** Bell Tower resolves the current exact destination or explicitly falls back to the verified owning window

#### Scenario: Only window navigation is available
- **WHEN** the Windows Terminal integration verifies the owning window but cannot verify its session tab or pane
- **THEN** navigation brings forward that window and Bell Tower identifies the action as window-only

### Requirement: Safe stale-target handling
Bell Tower SHALL validate attachment and host liveness before navigation. It SHALL NOT create a new terminal window as an accidental consequence of a stale destination, focus an unverified terminal, or type into a session to discover its location. When no safe target is available, it SHALL keep the attention visible and explain that navigation is unavailable.

#### Scenario: The original target has closed
- **WHEN** a chip's stored host or attachment no longer exists
- **THEN** Bell Tower avoids acting on a reused target and reports that the destination is unavailable

### Requirement: Focus acknowledgment does not resolve a wait
Navigating to a session SHALL NOT itself dismiss its current attention episode or mark its outstanding input as answered.

#### Scenario: A permission chip is clicked
- **WHEN** navigation succeeds but the user has not answered the permission prompt
- **THEN** the session continues to require attention
