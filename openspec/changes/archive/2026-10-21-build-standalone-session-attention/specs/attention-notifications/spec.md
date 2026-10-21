# Attention Notifications

## Purpose

Bring sessions needing input or review to the user's attention through a compact strip and controlled audio while keeping all running sessions available from the tray.

## ADDED Requirements

### Requirement: Attention-only nonactivating strip
Bell Tower SHALL show one chip per actionable, unacknowledged session in a topmost strip that does not steal keyboard focus. Each chip SHALL show the shared session name and accent, an attention reason, and elapsed wait time in HH:mm:ss in one compact horizontal row separated by centered dots. Elapsed time SHALL measure waiting for the displayed reason, including completion grace. Branch and worktree details SHALL be available in the tooltip and tray overview.

#### Scenario: Working and waiting sessions coexist
- **WHEN** one session is working and another has an actionable wait
- **THEN** only the waiting session appears in the strip and displaying it preserves the user's current keyboard focus

### Requirement: Completion grace and immediate waits
Bell Tower SHALL delay completion attention for 20 seconds after becoming eligible. Confirmed permission, question, and terminal error waits SHALL become actionable immediately. A completion that resolves before its grace expires SHALL NOT create an alert.

#### Scenario: A new prompt arrives during grace
- **WHEN** completion has been pending for fewer than 20 seconds and the user submits another prompt
- **THEN** no completion chip or sound is produced for that completed turn

#### Scenario: Permission is required
- **WHEN** the provider confirms an outstanding permission prompt
- **THEN** the session becomes eligible for a chip and sound without the completion grace

### Requirement: Per-episode acknowledgment
Right-clicking a chip SHALL acknowledge only its current attention episode and hide that chip. A later resolved-then-new wait or a distinct new blocker at the displayed attention priority SHALL create a fresh episode. Metadata refreshes and duplicate observations of the same wait SHALL NOT resurrect an acknowledged episode.

#### Scenario: An acknowledged wait is observed again
- **WHEN** repeated events or a name refresh describe the same unresolved wait
- **THEN** its chip remains dismissed

#### Scenario: The session needs attention again
- **WHEN** the acknowledged wait resolves and a new wait subsequently starts
- **THEN** a fresh chip is eligible under the applicable timing rule

### Requirement: Silent restoration
Bell Tower SHALL restore already-due attention after a tray restart without playing catch-up sound. A wait that became due while the tray was stopped SHALL also restore silently. If a restored completion grace has not elapsed, Bell Tower SHALL retain its remaining grace.

#### Scenario: A wait becomes due while Bell Tower is stopped
- **WHEN** Bell Tower starts and finds that the persisted wait is already due for a live session
- **THEN** it displays the chip without chime or speech

### Requirement: Serialized chime and voice
For a newly actionable episode observed while Bell Tower is running and unmuted, Bell Tower SHALL play a bundled reason-specific tone and optionally speak the available role, at most four words of session subject, and reason. Defaults SHALL be doorbell for permission, magic ring for questions, interface sound for completion, and fail sound for errors. The app SHALL operate independently of Downloads and retain the PoC's 880 Hz, 400 ms chime as an explicit option. Users SHALL be able to select one copied custom PCM WAV for all reasons; source and normalized files SHALL each fit within 16 MB and last at most 30 seconds. Audio SHALL be serialized. More than two pending session phrases SHALL collapse to one phrase stating the count of sessions waiting and use the highest-priority reason's tone. Unavailable speech SHALL preserve tone-only operation.

#### Scenario: Several sessions require attention together
- **WHEN** more than two session phrases are pending
- **THEN** Bell Tower speaks a single count phrase instead of overlapping individual phrases

#### Scenario: A queued wait resolves
- **WHEN** its session no longer requires attention before its audio is played
- **THEN** that stale notification is excluded from playback and the aggregate count

### Requirement: Tray controls and running overview
The tray SHALL provide an overview of all live tracked sessions with identity, provider, activity, attention, and available navigation. It SHALL provide Mute, Voice, Test sound, and Exit controls. Mute SHALL silence all alert audio while retaining visual alerts. Voice preference SHALL persist and reload live; disabling voice SHALL retain chimes when unmuted.

#### Scenario: Voice is disabled
- **WHEN** the live voice preference changes to false
- **THEN** subsequent notifications use chime only and visual alerts continue

#### Scenario: The overview opens
- **WHEN** the user opens the all-session overview
- **THEN** working and waiting sessions are listed with branch/worktree context where available

### Requirement: Position persistence and display recovery
The strip SHALL support dragging, persist its position, and stay reachable after display bounds change, including remote-desktop resizing.

#### Scenario: A saved position is off-screen
- **WHEN** Bell Tower restores a position outside current display bounds
- **THEN** it places the strip within a visible display area without taking keyboard focus

### Requirement: Viewed wait timer
After successful navigation initiated by a chip click, Bell Tower SHALL hide that chip's timer and trailing separator while keeping its name, reason and provider wait active. Failed navigation SHALL leave the timer visible. This preference SHALL persist only for the viewed episode, survive tray restarts, and SHALL NOT hide a newer wait's timer when an older navigation request completes. Overview and CLI navigation SHALL NOT hide chip timers.

#### Scenario: A chip opens its terminal
- **WHEN** the user clicks a chip and supported navigation succeeds
- **THEN** the chip remains visible without its timer and the underlying wait remains unresolved

#### Scenario: A new wait replaces the viewed episode
- **WHEN** the session starts a new attention episode
- **THEN** the chip displays its timer again

#### Scenario: An older navigation request finishes late
- **WHEN** a navigation request for a previous episode succeeds after a new episode has begun
- **THEN** the newer episode's timer remains visible