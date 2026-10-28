# Spec Delta

## MODIFIED Requirements

### Requirement: Causally ordered provider observations
Both providers' observations SHALL retain ingress time, available provider/source ordering and operation identity separately from processing time. Given the same causal/source-order metadata, replay order and duplicate envelope delivery SHALL NOT change the final session, operation or attention state. A resolved operation or ended attachment SHALL NOT be reopened by its delayed start or registration. An approval request SHALL be resolved only by its correlated result, resumption, stop or end; a start of the same operation delivered after the request, including under asynchronous hook execution, SHALL NOT resolve it.

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

#### Scenario: Approval request delivered before its tool start
- **WHEN** asynchronous hooks deliver a provider's approval request before the start of the same operation, for either client
- **THEN** the request stays pending under the shared human-wait policy until its correlated resolution, and the late start does not clear it
