# Attachment reconciliation delta

## MODIFIED Requirements

### Requirement: Lifecycle reconciliation and restart
Bell Tower SHALL reconcile persisted observations with the liveness of registered attachments at startup and during operation. A lack of recent activity alone SHALL NOT prove that a session ended. Confirmed ended attachments SHALL leave the running overview and attention strip. Incomplete managed provider attachments SHALL become unavailable after a configurable evidence grace, and only fresh verified evidence SHALL restore their live status. Unowned modes for either provider SHALL receive the same evidence-based capability classification and honest unavailable status. An alive idle process SHALL never expire solely because it emits no hooks.

#### Scenario: A waiting session is quiet
- **WHEN** a registered process remains alive without new events while waiting for input
- **THEN** Bell Tower retains its wait instead of expiring it by inactivity

#### Scenario: A session crashes without an end hook
- **WHEN** Bell Tower confirms that the registered process attachment no longer exists
- **THEN** the session leaves the running overview and any stale attention chip is removed

#### Scenario: Bell Tower restarts
- **WHEN** persisted state refers to live and ended attachments
- **THEN** Bell Tower restores live sessions and excludes confirmed ended attachments

#### Scenario: Restart with stale unknown attachment
- **WHEN** a persisted incomplete managed provider attachment is already beyond its grace
- **THEN** restart retains unavailable health instead of displaying working

#### Scenario: Quiet verified agent
- **WHEN** a verified agent remains alive longer than the evidence grace without emitting hooks
- **THEN** its session and unresolved wait remain live

## ADDED Requirements

### Requirement: Recoverable attachment health
Bell Tower SHALL expose verified-live, confirmed-ended and unverified attachment health separately. An incomplete managed provider attachment lacking a process SHALL be recaptured on later evidence; a durable verified launch-shell identity SHALL remain available independently of consumed palette metadata. Unowned/shared-runtime modes for both clients SHALL use the same safety policy without being mislabeled failed managed captures.

#### Scenario: Temporary capture failure
- **WHEN** a managed provider registration has no agent or shell identity and a later hook has valid ownership evidence
- **THEN** the same conversation is repaired without changing its palette or creating a new wait

#### Scenario: Verified shell exits
- **WHEN** a managed provider attachment has no agent identity but its durable owning shell is confirmed gone
- **THEN** the attachment is reconciled as ended

#### Scenario: No evidence after grace
- **WHEN** an incomplete managed provider attachment receives no fresh evidence for the configured grace, default ten minutes
- **THEN** it becomes unavailable and leaves actionable alerts without being falsely labeled confirmed-dead; restart preserves that status


### Requirement: Provider parity for attachment health and title refresh in session-tracking
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
