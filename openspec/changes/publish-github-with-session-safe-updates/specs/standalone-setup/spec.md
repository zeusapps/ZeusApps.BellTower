# Spec Delta

## MODIFIED Requirements

### Requirement: Independent tray lifetime and safe management
Bell Tower SHALL run as a single tray instance that survives the exit of a launcher terminal. Normal event ingestion SHALL NOT start multiple tray instances. Management commands SHALL stop or control Bell Tower only, and SHALL NOT stop, signal, restart, or modify running agent sessions or their terminal hosts. Compatible updates SHALL permit the tray to stop and restart monitoring without stopping active dedicated session runtime managers. The restarted tray SHALL retain the selected data directory and restore existing session monitoring. Setup, diagnostics, and validation SHALL remain silent unless the user explicitly invokes a sound test.

#### Scenario: The starting terminal closes
- **WHEN** the terminal that originally launched Bell Tower exits
- **THEN** the tray remains available for other registered sessions

#### Scenario: Bell Tower is updated manually
- **WHEN** a user follows the documented manual executable replacement procedure
- **THEN** the procedure stops Bell Tower before replacement and restarts it afterward without stopping Claude, Codex, VS Code, or Windows Terminal

#### Scenario: Compatible version activation with live runtimes
- **WHEN** an explicitly initiated update closes and restarts the tray while dedicated session runtimes remain active
- **THEN** runtime and agent processes remain running and the tray resumes monitoring against the same data directory without creating a second tray instance
