# Provider approval correlation delta

## ADDED Requirements

### Requirement: Provisional and confirmed provider approval correlation
Both providers' provisional requests and confirmed human-wait observations SHALL be scoped to the live attachment and turn. Where the provider omits operation IDs, correlation SHALL preserve ambiguity instead of resolving unrelated parallel waits. Confirmations SHALL persist without replaying restored sound, and cleared waits SHALL retain enough receipts to reject stale confirmation.

#### Scenario: Two parallel approvals
- **WHEN** one of two provisional requests resolves before an uncorrelated prompt notification arrives
- **THEN** the resolved request remains resolved and only supported current human-wait evidence can alert

#### Scenario: Restart during confirmed wait
- **WHEN** the tray restarts with an already-due confirmed provider wait
- **THEN** the chip restores silently and no new approval episode is invented

#### Scenario: Approval flow for a pending question tool
- **WHEN** a provider approval-flow observation matches a pending question-tool wait by tool name and input fingerprint or by prompt scope
- **THEN** it is recorded as provisional/confirming evidence for that question wait rather than as a new permission wait


### Requirement: Provider parity for human-wait attention in session-tracking
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
