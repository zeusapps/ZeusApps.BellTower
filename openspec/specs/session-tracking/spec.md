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

### Requirement: Stage-independent command correlation
For supported shell and patch command hooks, Bell Tower SHALL correlate command content across approval, operation-start and result observations despite approval-only descriptions or execution options. For arbitrary tools, descriptions that are actual arguments SHALL remain part of correlation. Ambiguous parallel operations SHALL NOT be resolved without matching evidence.

#### Scenario: Approval includes an extra description
- **WHEN** a permission observation adds a human-readable description to the same command seen in operation hooks
- **THEN** the correlated resumption or result clears that permission wait

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
