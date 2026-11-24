## MODIFIED Requirements

### Requirement: Viewed wait timer
After successful navigation initiated by a chip click, Bell Tower SHALL hide that chip's timer and trailing separator while keeping its name, reason and provider wait active. A chip click whose result is a stated permanent navigation limit for that session (no terminal destination exists for it, for example a conversation on the shared Codex backend) SHALL hide the timer in the same way once the reason has been shown, for both Claude Code and Codex. Failed navigation for any other reason, including declined foreground activation, a stale or missing session and a navigator failure, SHALL leave the timer visible. This preference SHALL persist only for the viewed episode, survive tray restarts, and SHALL NOT hide a newer wait's timer when an older navigation request completes. Overview and CLI navigation SHALL NOT hide chip timers.

#### Scenario: A chip opens its terminal
- **WHEN** the user clicks a chip and supported navigation succeeds
- **THEN** the chip remains visible without its timer and the underlying wait remains unresolved

#### Scenario: A chip click shows a permanent navigation limit
- **WHEN** the user clicks the chip of a session that has no terminal destination, such as a shared-backend Codex conversation, and Bell Tower shows the reason
- **THEN** the chip remains visible without its timer and the underlying wait remains unresolved

#### Scenario: A chip click fails for a temporary reason
- **WHEN** the user clicks a chip and navigation fails because Windows declines foreground activation, the session is stale, or the navigator fails
- **THEN** the chip's timer stays visible

#### Scenario: A new wait replaces the viewed episode
- **WHEN** the session starts a new attention episode
- **THEN** the chip displays its timer again

#### Scenario: An older navigation request finishes late
- **WHEN** a navigation request for a previous episode succeeds after a new episode has begun
- **THEN** the newer episode's timer remains visible
