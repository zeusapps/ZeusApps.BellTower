# Provider hook delivery delta

## ADDED Requirements

### Requirement: Durable provider hook ingress
Hook capture for both providers SHALL atomically record a bounded event and verified launch/process evidence before returning a neutral response. It SHALL remain available while the tray is stopped or restarting and SHALL NOT require starting the full desktop application or stable application launcher for each event. Accepted events SHALL survive consumer crashes and be consumed at most once by envelope identity.

#### Scenario: Tray is stopped
- **WHEN** either provider delivers a supported hook while monitoring is stopped
- **THEN** capture persists the event without starting an agent or losing it, and monitoring later consumes it

#### Scenario: Consumer crashes after commit
- **WHEN** a committed envelope remains in the inbox after a crash
- **THEN** replay preserves the already-applied effect and safely removes the envelope

#### Scenario: Malformed input
- **WHEN** input is malformed or oversized
- **THEN** the hook returns neutral output with a bounded sanitized reason and does not alter provider approvals

### Requirement: Causally ordered provider observations
Both providers' observations SHALL retain ingress time, available provider/source ordering and operation identity separately from processing time. Given the same causal/source-order metadata, replay order and duplicate envelope delivery SHALL NOT change the final session, operation or attention state. A resolved operation or ended attachment SHALL NOT be reopened by its delayed start or registration.

#### Scenario: Result arrives before start
- **WHEN** a tool result is consumed before its causally earlier start
- **THEN** the operation remains completed when the start arrives

#### Scenario: Delayed registration after exit
- **WHEN** an earlier registration arrives after the same attachment's end
- **THEN** the session remains ended

#### Scenario: Post-tool selection without pre receipt
- **WHEN** a supported successful main-session tool result supplies sufficient unambiguous change evidence but no pre receipt exists
- **THEN** the result can establish its metadata association without a synthetic attention event

#### Scenario: Shuffled delivery
- **WHEN** a fixture's prompt, approval, result, stop and end envelopes are replayed in differing orders with their source metadata intact
- **THEN** the same final state, palette and notification receipts are obtained

### Requirement: Bounded installed hook latency
The installed hook path for each provider SHALL be measured through its real provider shell and packaged ingress. On the representative loaded Windows field setup its median ingress completion SHALL be below 300 milliseconds. Tests SHALL independently verify durable completion within a three-second budget; asynchronous timeout suppression alone SHALL NOT count as meeting the latency requirement.

#### Scenario: Installed performance trial
- **WHEN** at least 100 supported hooks are measured through the installed command under representative parallel-session load
- **THEN** the record reports median and tail latency, durable event counts and the complete process path, with median below 300 milliseconds


### Requirement: Provider parity for fast hook delivery in session-tracking
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded

### Requirement: One product with shared provider features
Bell Tower SHALL maintain one feature model and shared identity, lifecycle, attention and configuration policies for Claude Code and Codex. New features SHALL include both integrations by default. Provider-specific code SHALL translate capabilities and observations rather than duplicate product policy or freeze one integration's feature set. Compatibility SHALL preserve user data and safe runtime ownership while extending features.

#### Scenario: A feature originates in one provider report
- **WHEN** a field defect or feature request is first observed in Claude or Codex
- **THEN** its plan and acceptance include both providers, with exclusions permitted only for evidenced hard restrictions
