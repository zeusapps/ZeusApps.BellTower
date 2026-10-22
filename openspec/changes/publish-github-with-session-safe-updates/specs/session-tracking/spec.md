# Spec Delta

## MODIFIED Requirements

### Requirement: Lifecycle reconciliation and restart
Bell Tower SHALL reconcile persisted observations with the liveness of registered attachments at startup and during operation. A lack of recent activity alone SHALL NOT prove that a session ended. Confirmed ended attachments SHALL leave the running overview and attention strip. After a compatible application update, Bell Tower SHALL restore live sessions from the same data directory, consume events queued while the tray was stopped, and reconnect to verified existing authenticated runtime endpoints without creating replacement agent sessions. Reconciliation SHALL preserve session identity, attachment ownership, palette, attention episodes, wait timestamps, acknowledgment and viewed state, and the existing silent-restoration/grace rules. Incompatible state or runtime contracts SHALL prevent activation rather than corrupting records or marking live sessions ended.

#### Scenario: A waiting session is quiet
- **WHEN** a registered process remains alive without new events while waiting for input
- **THEN** Bell Tower retains its wait instead of expiring it by inactivity

#### Scenario: A session crashes without an end hook
- **WHEN** Bell Tower confirms that the registered process attachment no longer exists
- **THEN** the session leaves the running overview and any stale attention chip is removed

#### Scenario: Bell Tower restarts
- **WHEN** persisted state refers to live and ended attachments
- **THEN** Bell Tower restores live sessions and excludes confirmed ended attachments

#### Scenario: Events arrive during a version change
- **WHEN** hooks publish activity or attention events while the tray is stopped for a compatible update
- **THEN** the restarted tray consumes those events idempotently, reconciles the live attachments, and preserves the resulting attention state without playing catch-up sound for already-due waits

#### Scenario: Existing authenticated Codex runtime survives
- **WHEN** the new tray finds a live managed Codex session and its compatible verified runtime record
- **THEN** it reconnects using the existing endpoint and credential, preserves manual/native naming ownership, and resumes supported permission/name verification without restarting the runtime or agent

#### Scenario: Mixed versions cannot share state safely
- **WHEN** the candidate version cannot safely read and coexist with records written by still-running helpers
- **THEN** activation is deferred with compatibility guidance and the current monitoring installation remains selected
