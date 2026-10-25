# VS Code navigation delta

## MODIFIED Requirements

### Requirement: Exact VS Code terminal selection
With a valid VS Code registration, Bell Tower SHALL activate the owning VS Code window and reveal the terminal corresponding to that live attachment. Multiple VS Code windows and duplicate terminal titles SHALL NOT cause another terminal to be selected. Activation SHALL be confirmed against the verified owning top-level editor window after user interaction. Only a minimized destination SHALL be restored; visible maximized/fullscreen state SHALL be preserved. Terminal reveal or most-recent active-terminal state alone SHALL NOT count as foreground success. Unverified native window mappings SHALL fail safely.

#### Scenario: Two VS Code windows have identically named terminals
- **WHEN** the user navigates to a session in one registered window
- **THEN** that window's integration reveals the matching live terminal and the other window does not consume the request

#### Scenario: Window behind another app
- **WHEN** the user clicks a chip whose owning VS Code window is in the background
- **THEN** the exact owning window comes forward and its registered terminal is revealed

#### Scenario: Minimized or fullscreen owner
- **WHEN** the owning window is minimized or is already fullscreen
- **THEN** the minimized window is restored, while a visible fullscreen window preserves its show state

#### Scenario: Activation denied
- **WHEN** the terminal is selected but Windows does not foreground its owner
- **THEN** navigation reports failure and the chip's wait and timer remain unchanged

#### Scenario: Duplicate editor windows
- **WHEN** different editor windows contain identical terminal names
- **THEN** only the verified owning instance receives activation and terminal selection

## ADDED Requirements

### Requirement: Recoverable VS Code bridge storage
The bridge SHALL remove temporary files after a failed atomic write, retry safely on later ticks and bound its diagnostics. A recoverable registration refresh failure SHALL NOT indefinitely starve verified focus requests or make a stale destination appear live.

#### Scenario: Rename failure
- **WHEN** an injected rename failure occurs after a temporary file is written
- **THEN** no temporary file is left behind and a later tick can publish a valid registration

#### Scenario: Transient registration failure
- **WHEN** a valid focus request exists while one registration refresh fails
- **THEN** the bridge either processes it using still-valid ownership or returns a bounded honest failure, and later ticks recover


### Requirement: Provider parity for terminal navigation in terminal-navigation
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
