# Spec Delta

## ADDED Requirements

### Requirement: Bounded monitoring state
Monitoring state SHALL stay within its snapshot budget for both providers. For an ended session that has stayed quiet for the settle interval, Bell Tower SHALL keep only what it needs to display it, to keep it ended, and to order observations delivered after its end. It SHALL NOT keep replay history up to its end. Bell Tower SHALL bound the number of retained ended sessions, removing the oldest first, in addition to the age limit. When a save would exceed the budget, Bell Tower SHALL remove the oldest ended sessions until the save fits rather than refuse it, and SHALL keep consuming captures. It SHALL refuse a save only when live sessions alone exceed the budget. Captures SHALL then stay queued and the condition SHALL be reported. Retention SHALL NOT remove a live session, reopen an ended session from a delayed earlier event, or prevent a resumed session from being tracked. A refresh failure SHALL be logged with a bounded, sanitized reason without one entry per tick. Doctor SHALL report state size against the budget, live and ended session counts, and any persistent refresh failure.

#### Scenario: Ended session is compacted after it settles
- **WHEN** a Claude Code or Codex session ends and no further event for it arrives during the settle interval
- **THEN** its retained record no longer carries replay history, it remains ended, and its displayed identity is unchanged

#### Scenario: Delayed earlier event after compaction
- **WHEN** an event causally earlier than a compacted session's end arrives, for either provider
- **THEN** the session remains ended and no attention is raised

#### Scenario: Resumed session after compaction
- **WHEN** a compacted ended session is resumed by its provider with a later registration
- **THEN** it becomes live and its later events are tracked and ordered as for any live session, including events of the resumed session delivered before its registration

#### Scenario: Many ended sessions
- **WHEN** more ended sessions accumulate than the retained count, from either or both providers
- **THEN** the oldest ended sessions are removed and live sessions are unaffected

#### Scenario: State at the budget recovers
- **WHEN** persisted state is at or near the budget and queued captures are waiting, as written by an earlier version
- **THEN** the first drain compacts and, if needed, removes the oldest ended sessions, saves within the budget, and consumes the queued captures for both providers without a manual edit

#### Scenario: Live sessions alone exceed the budget
- **WHEN** a save cannot fit even after every ended session is removed
- **THEN** captures stay queued, the failure is logged once with its reason and not on every tick, and doctor reports the persistent refresh failure and state size

#### Scenario: Doctor reports state health
- **WHEN** the user runs doctor while the tray is running
- **THEN** it reports state size, the budget, live and ended session counts, and whether refresh is currently failing
