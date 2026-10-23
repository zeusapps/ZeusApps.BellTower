# Bounded presentation refresh delta

## ADDED Requirements

### Requirement: Change-driven terminal presentation
Managed terminal presentation SHALL refresh only when its resolved title, palette or verified destination changes. Unchanged refresh ticks SHALL NOT spawn writer or Git child processes. Failed or unverified destinations SHALL use bounded backoff and diagnostics, and console attachment SHALL remain isolated from the tray and hook processes.

#### Scenario: Unchanged presentation
- **WHEN** several refresh cycles see unchanged identity and a fresh shared checkout cache
- **THEN** no title writer or Git child is launched

#### Scenario: Dead destination
- **WHEN** a managed attachment cannot be verified across repeated refreshes
- **THEN** no repeated full-rate writer launches or log flood occurs

#### Scenario: Title changed
- **WHEN** a live verified session changes its display identity
- **THEN** one isolated bounded write updates its terminal while preserving attention and detach restoration


### Requirement: Provider parity for attachment health and title refresh in session-identity
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
