# Session Tracking

## Purpose

Track running Claude Code and Codex sessions consistently while keeping foreground attention, background activity, and terminal ownership independent.

## Requirements

### Requirement: Agent-independent event tracking
Bell Tower SHALL accept lifecycle and attention events from bundled Claude Code and Codex integrations and expose their provider, running status, attention reason, and observation capability. Unsupported observations SHALL be reported as unavailable instead of being presented as confirmed working or waiting.

#### Scenario: Both providers are running
- **WHEN** a Claude Code session and a Codex session emit supported events
- **THEN** both appear in the tray overview under their respective providers with independently maintained states

#### Scenario: A required observation is unavailable
- **WHEN** the installed provider cannot expose a question-wait event
- **THEN** Bell Tower reports that limitation and continues processing supported activity and attention events

### Requirement: Conversation and running attachment identity
Bell Tower SHALL distinguish provider session identity from the current process attachment and terminal destination. Titles, working directories, branch names, and bare process IDs MUST NOT be the sole identity of a session.

#### Scenario: Identical titles in separate terminals
- **WHEN** two running sessions have the same title and working directory
- **THEN** Bell Tower tracks them separately and routes each focus request to its own registered attachment

#### Scenario: A process ID is reused
- **WHEN** an old attachment has ended and another process obtains its process ID
- **THEN** old events and focus targets are not associated with the new process merely because that ID matches

### Requirement: Attention independent of background work
Bell Tower SHALL maintain foreground attention separately from active background tasks. A confirmed permission wait, question wait, or terminal failure SHALL remain actionable while background agents work. Completion SHALL remain suppressed while known background tasks of type subagent, workflow, or monitor continue; shell tasks alone SHALL NOT suppress completion.

#### Scenario: A question arrives during background work
- **WHEN** a foreground session requests an answer while a subagent remains active
- **THEN** the session requires attention for the question and retains its background activity information

#### Scenario: A foreground stop leaves background work
- **WHEN** the foreground stops without an attention request and a known subagent remains active
- **THEN** Bell Tower does not create a completion alert until that work ends and the session is otherwise eligible for completion

### Requirement: Correlated attention transitions
Bell Tower SHALL clear the applicable wait on a new user prompt or evidence that its blocked operation resumed or resolved. Unrelated parallel tool completions and delayed events from an older turn or attachment SHALL NOT clear a current wait or recreate a resolved wait. Recoverable tool errors SHALL NOT automatically become terminal session failures. Interruption SHALL NOT make a session eligible for completion when background work later ends.

#### Scenario: Approval resolves
- **WHEN** an approved operation resumes or its correlated tool result arrives
- **THEN** its permission wait clears even if that tool subsequently reports a recoverable error

#### Scenario: An unrelated tool completes
- **WHEN** a session awaits permission for one tool while a different parallel tool completes
- **THEN** the outstanding permission wait remains active

#### Scenario: A delayed completion arrives
- **WHEN** a completion event from the previous turn arrives after the next prompt has begun
- **THEN** the current session does not acquire a completion wait from that event

### Requirement: Lifecycle reconciliation and restart
Bell Tower SHALL reconcile persisted observations with the liveness of registered attachments at startup and during operation. A lack of recent activity alone SHALL NOT prove that a session ended. Confirmed ended attachments SHALL leave the running overview and attention strip.

#### Scenario: A waiting session is quiet
- **WHEN** a registered process remains alive without new events while waiting for input
- **THEN** Bell Tower retains its wait instead of expiring it by inactivity

#### Scenario: A session crashes without an end hook
- **WHEN** Bell Tower confirms that the registered process attachment no longer exists
- **THEN** the session leaves the running overview and any stale attention chip is removed

#### Scenario: Bell Tower restarts
- **WHEN** persisted state refers to live and ended attachments
- **THEN** Bell Tower restores live sessions and excludes confirmed ended attachments

### Requirement: Stage-independent command correlation
For supported shell and patch command hooks, Bell Tower SHALL correlate command content across approval, operation-start and result observations despite approval-only descriptions or execution options. For arbitrary tools, descriptions that are actual arguments SHALL remain part of correlation. Ambiguous parallel operations SHALL NOT be resolved without matching evidence.

#### Scenario: Approval includes an extra description
- **WHEN** a permission observation adds a human-readable description to the same command seen in operation hooks
- **THEN** the correlated resumption or result clears that permission wait