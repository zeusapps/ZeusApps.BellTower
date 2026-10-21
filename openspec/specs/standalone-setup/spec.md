# Standalone Setup

## Purpose

Install and operate Bell Tower as personal Windows tooling with bundled integrations, independent data ownership, reversible configuration changes, and useful compatibility diagnostics.

## Requirements

### Requirement: Independent installation
Bell Tower SHALL ship its own session identity support, agent event integrations, launcher helpers, and required terminal bridges. A separate Claude mod framework, existing personal launchers, an OpenSpec installation, a WorkLog installation, or a Bell Tower MCP server SHALL NOT be required for normal tracking and notification operation.

#### Scenario: Installation on a clean user profile
- **WHEN** a user installs Bell Tower with a supported agent and terminal present but without the personal PoC, badge mod, WorkLog, or a Bell Tower MCP registration
- **THEN** Bell Tower can register sessions, assign names and colors, and deliver supported attention notifications

### Requirement: Explicit reversible integration setup
Bell Tower setup SHALL show which user configuration files and integration entries it will change, preserve unrelated settings, and support removal of only Bell Tower-owned additions. Setup SHALL NOT bypass the provider's hook trust or approval mechanisms. Setup SHALL be sufficient using Bell Tower-owned hooks and host bridges without requiring cross-project managed instructions or connections to another helper application.

#### Scenario: Existing agent hooks are present
- **WHEN** the user enables Bell Tower's integration
- **THEN** setup adds Bell Tower's hooks while preserving existing unrelated hook definitions

#### Scenario: Provider trust is pending
- **WHEN** an installed hook requires review before the provider will run it
- **THEN** Bell Tower reports pending setup and supplies the review steps without claiming that event tracking is operational

### Requirement: App-owned runtime data and compatibility import
Bell Tower SHALL maintain its own configuration, session identity, position, and runtime state without requiring storage under an agent's home directory. It SHALL support an explicitly selected import of available legacy names, palettes, voice preference, and position without modifying the original files or requiring legacy tooling afterward.

#### Scenario: Legacy badge metadata is imported
- **WHEN** a user selects import from available legacy badge files
- **THEN** Bell Tower preserves valid names and palette values in its own state and does not require later reads from those files

#### Scenario: Legacy files are absent
- **WHEN** Bell Tower starts without any PoC configuration or badge files
- **THEN** it creates usable app-owned defaults and operates normally

### Requirement: Integration capability diagnostics
Bell Tower SHALL report provider/host version information when available and separately identify event-tracking coverage, terminal badge/background support, hook trust status, and navigation precision. A failure of optional decoration or navigation SHALL NOT disable otherwise supported notifications.

#### Scenario: Tracking works but an in-session badge is unsupported
- **WHEN** an agent supplies valid attention events but cannot display the custom footer
- **THEN** diagnostics distinguish operational tracking from unavailable decoration and notifications continue

### Requirement: Independent tray lifetime and safe management
Bell Tower SHALL run as a single tray instance that survives the exit of a launcher terminal. Normal event ingestion SHALL NOT start multiple tray instances. Management commands SHALL stop or control Bell Tower only, and SHALL NOT stop, signal, restart, or modify running agent sessions or their terminal hosts. Setup, diagnostics, and validation SHALL remain silent unless the user explicitly invokes a sound test.

#### Scenario: The starting terminal closes
- **WHEN** the terminal that originally launched Bell Tower exits
- **THEN** the tray remains available for other registered sessions

#### Scenario: Bell Tower is updated manually
- **WHEN** a user follows the documented manual executable replacement procedure
- **THEN** the procedure stops Bell Tower before replacement and restarts it afterward without stopping Claude, Codex, VS Code, or Windows Terminal

### Requirement: Limited local observability
Bell Tower SHALL retain bounded local diagnostics and avoid recording prompt bodies, full transcripts, credentials, or complete tool payloads in ordinary logs. Malformed event data SHALL be diagnosed without crashing the tray or treating that data as an instruction.

#### Scenario: A hook sends malformed input
- **WHEN** Bell Tower receives an invalid event
- **THEN** it rejects or quarantines the input, records a bounded diagnostic without sensitive payload content, and continues processing other sessions

