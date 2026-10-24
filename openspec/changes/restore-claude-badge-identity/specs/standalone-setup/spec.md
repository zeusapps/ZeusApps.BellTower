# Legacy identity import delta

## ADDED Requirements

### Requirement: Legacy badge format compatibility
Explicit badge import SHALL accept legacy background values represented as supported OSC rgb color strings or hex RGB and normalize them to Bell Tower's internal palette. Import SHALL be provider/session-scoped and SHALL NOT introduce a runtime dependency on the legacy mod or its temporary badge folder.

#### Scenario: OSC background import
- **WHEN** the user explicitly imports a fixture badge containing an OSC rgb background and hex accent
- **THEN** the selected identity receives a valid normalized palette and title without modifying the source badge

#### Scenario: Invalid color sequence
- **WHEN** a fixture includes unsupported escapes or an invalid color value
- **THEN** import rejects it without executing or emitting the sequence


### Requirement: Provider parity for session identity and checkout in standalone-setup
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
