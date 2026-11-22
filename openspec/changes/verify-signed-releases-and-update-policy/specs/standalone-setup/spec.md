# Managed update discovery controls delta

## ADDED Requirements

### Requirement: Explicit automatic update-check policy
Setup and user controls SHALL expose the existing automatic discovery policy with explicit enabled/disabled choices, preserve the current value when omitted and support preview and owned restoration. Disabled discovery SHALL make no automatic feed requests; explicit checks and installations SHALL remain user actions and installation SHALL never be unattended.

#### Scenario: Managed setup disables checks
- **WHEN** the user explicitly applies setup with automatic update checks disabled
- **THEN** the selected data directory retains disabled policy and background discovery makes no network requests

#### Scenario: Ordinary setup preserves policy
- **WHEN** setup is repeated without an update policy choice
- **THEN** the existing setting and unrelated configuration are unchanged

#### Scenario: Explicit check while disabled
- **WHEN** the user deliberately checks for an update while automatic discovery is disabled
- **THEN** the explicit check can run under the signature policy without enabling background discovery

#### Scenario: Removal after user edit
- **WHEN** the user changes the update policy after setup and removes integration
- **THEN** removal preserves the later user edit


### Requirement: Provider parity for release trust and update policy in standalone-setup
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
