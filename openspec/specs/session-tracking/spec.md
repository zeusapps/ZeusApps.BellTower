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
Bell Tower SHALL reconcile persisted conversation observations, runtime evidence and terminal attachments independently at startup and during operation. A lack of recent activity alone SHALL NOT prove that a conversation or attachment ended. A confirmed ended terminal attachment SHALL lose its navigation and presentation destination; its running overview entry and attention SHALL be removed only when conversation-ending evidence also establishes that its work ended or no independent verified conversation evidence remains. A terminal disconnect SHALL NOT mark a shared-backend conversation ended when fresh exact-conversation runtime evidence confirms continuing work. A loaded-thread listing alone SHALL NOT prove that a terminal is open or work is active. Incomplete managed provider attachments SHALL become unavailable after a configurable evidence grace, and only fresh verified evidence SHALL restore their live status. Unowned modes for either provider SHALL receive the same evidence-based capability classification and honest unavailable status. An alive idle directly attached agent SHALL never expire solely because it emits no hooks. After a compatible application update, Bell Tower SHALL restore live sessions from the same data directory, consume events queued while the tray was stopped, and reconnect to verified existing authenticated runtime endpoints without creating replacement agent sessions. Reconciliation SHALL preserve session identity, attachment ownership, palette, attention episodes, wait timestamps, acknowledgment and viewed state, and the existing silent-restoration/grace rules. Incompatible state or runtime contracts SHALL prevent activation rather than corrupting records or marking live sessions ended.

#### Scenario: A waiting session is quiet
- **WHEN** a registered directly attached process remains alive without new events while waiting for input
- **THEN** Bell Tower retains its wait instead of expiring it by inactivity

#### Scenario: A session crashes without an end hook
- **WHEN** Bell Tower confirms that the registered directly attached agent no longer exists and no independent continuing runtime is verified
- **THEN** the session leaves the running overview and any stale attention chip is removed

#### Scenario: Bell Tower restarts
- **WHEN** persisted state refers to live conversations, ended conversations and disconnected terminal attachments
- **THEN** Bell Tower restores independently verified live conversations, excludes confirmed ended work and does not restore stale terminal destinations

#### Scenario: Restart with stale unknown attachment
- **WHEN** a persisted incomplete managed provider attachment is already beyond its grace
- **THEN** restart retains unavailable terminal health instead of displaying it as verified

#### Scenario: Quiet verified agent
- **WHEN** a verified directly attached agent remains alive longer than the evidence grace without emitting hooks
- **THEN** its session and unresolved wait remain live

#### Scenario: Events arrive during a version change
- **WHEN** hooks publish activity or attention events while the tray is stopped for a compatible update
- **THEN** the restarted tray consumes those events idempotently, reconciles the live attachments, and preserves the resulting attention state without playing catch-up sound for already-due waits

#### Scenario: Existing authenticated Codex runtime survives
- **WHEN** the new tray finds a live managed Codex session and its compatible verified runtime record
- **THEN** it reconnects using the existing endpoint and credential, preserves manual/native naming ownership, and resumes supported permission/name verification without restarting the runtime or agent

#### Scenario: Mixed versions cannot share state safely
- **WHEN** the candidate version cannot safely read and coexist with records written by still-running helpers
- **THEN** activation is deferred with compatibility guidance and the current monitoring installation remains selected

#### Scenario: Terminal closes while backend work continues
- **WHEN** a terminal disconnects and fresh runtime evidence for its exact conversation confirms active work
- **THEN** Bell Tower retains that conversation's activity and supported attention while disabling the closed terminal destination

#### Scenario: Backend retains an idle thread
- **WHEN** a thread remains loaded after all terminal clients disconnect but there is no fresh active-work or unresolved-attention evidence
- **THEN** Bell Tower does not infer an open terminal or a working session from the loaded-thread record alone

#### Scenario: Runtime observation fails
- **WHEN** independent runtime evidence becomes stale and no other live conversation evidence is available
- **THEN** Bell Tower exposes uncertainty through the shared evidence policy without guessing a terminal or declaring the provider permanently unsupported

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

### Requirement: Hook-triggered adoption of native sessions
After user-level integration is installed and trusted, Bell Tower SHALL discover Claude Code and Codex conversations from supported hooks when the user starts the original client command without a Bell Tower launch function or dedicated terminal profile. Adoption SHALL reuse the shared identity, attention and replay policy. A missing SessionStart SHALL NOT prevent a later valid lifecycle event from establishing the conversation. Capture SHALL remain neutral, bounded and independent of tray availability: a hook SHALL persist its capture whether or not the tray is running, and SHALL NOT wait for the tray. After persisting a capture, the ingress SHALL start a stopped tray through the recorded stable launcher, unless the user stopped the tray in the current Windows sign-in session or an update or switch is pending.

#### Scenario: Native commands in ordinary terminals
- **WHEN** native Claude Code and Codex sessions emit valid hooks from ordinary local terminals
- **THEN** Bell Tower discovers separate conversations without requiring launcher metadata or a project-specific profile

#### Scenario: No tray is running when a hook arrives
- **WHEN** a native Claude Code or Codex session emits a hook after Windows started and no tray is running
- **THEN** the hook persists its capture, returns its neutral output without waiting, and the tray is started once in the background, then consumes the queued captures and shows the session

#### Scenario: User stopped the tray
- **WHEN** the user chose Exit in the tray and a hook of either provider arrives in the same Windows sign-in session
- **THEN** the hook persists its capture and does not start the tray; after the user starts the tray or signs in again, hooks start it when it is not running

#### Scenario: Several hooks arrive while the tray is stopped
- **WHEN** hooks from several sessions of both providers arrive at the same time with no tray running
- **THEN** at most one start is attempted per bounded interval, exactly one tray runs, and the started tray does not inherit a provider's environment or hold the hook's output open

#### Scenario: First observation arrives after startup
- **WHEN** the provider delivers the first accepted lifecycle event only after initial user input or the startup event was missed
- **THEN** Bell Tower establishes the conversation from available evidence without inventing earlier activity or a terminal destination

#### Scenario: Managed and native launches run together
- **WHEN** native and Bell Tower launched sessions of both providers run simultaneously, including duplicate names and working directories
- **THEN** each retains independent identity, palette, attention, runtime access and terminal evidence without inheriting another launch's metadata

### Requirement: Non-owning access to existing provider runtimes
Bell Tower SHALL distinguish access to an existing provider runtime from ownership of that runtime. Runtime reads and supported native metadata operations SHALL address the exact conversation and obey existing user controls. Observation SHALL NOT resume a conversation, subscribe as its approval handler, or start a model turn merely to inspect status. Bell Tower shutdown, setup removal, detach and update SHALL NOT stop, restart or reconfigure an unowned provider runtime.

#### Scenario: Shared Codex runtime is available
- **WHEN** a native Codex conversation can be verified in its existing local shared backend
- **THEN** Bell Tower can read its runtime state without creating a replacement backend or claiming a terminal binding

#### Scenario: Provider runtime access is unavailable
- **WHEN** either provider lacks a verified connection for a runtime operation
- **THEN** Bell Tower reports that operation as unavailable or unverified while preserving independently supported hook tracking

#### Scenario: Bell Tower exits during shared work
- **WHEN** Bell Tower stops while native provider work remains active
- **THEN** only Bell Tower owned observer connections and helpers are closed and the provider work continues

### Requirement: Hook tray start from the user's shell
When a hook starts a stopped tray, Bell Tower SHALL create it as a child of the verified desktop shell of the same sign-in session, with no inherited handles, so the tray receives the shell's jobs, token and environment and never the provider's. If no shell can be verified, it SHALL start nothing and keep the capture queued. A tray started by a hook SHALL, on start, check its immediate job and, if that job would end it when the job closes, record a diagnostic and exit; a tray started by hand SHALL NOT be refused by this check. When the shell start fails, Bell Tower SHALL use a per-user scheduled task with no trigger, started on demand, which setup creates and removes and doctor reports. The behavior SHALL be identical for Claude Code and Codex. The user-stop suppression, burst control and update gate SHALL be unchanged.

#### Scenario: First hook after a restart
- **WHEN** the first Claude Code or Codex hook after sign-in runs with no tray running and no user stop recorded
- **THEN** exactly one tray starts as a child of the desktop shell, it consumes the queued captures, and doctor records the route used

#### Scenario: Elevated hook
- **WHEN** a hook from an elevated terminal starts the tray
- **THEN** the tray runs with the shell's non-elevated token

#### Scenario: Shell start unavailable
- **WHEN** no desktop shell can be verified or the shell start fails
- **THEN** the scheduled-task fallback starts the tray, or nothing starts and doctor states why, and the capture stays queued

### Requirement: Confirmed waits from provider evidence
A provisional permission wait SHALL become a confirmed human wait only when the session's provider evidence shows that a human must decide: for Codex the approval reviewer setting or an approval record in its transcript, for Claude Code its existing confirmation. A wait SHALL clear when the evidence shows the decision was made, including a declined prompt detected from a bounded read of the end of the transcript. Timing alone SHALL NOT confirm a wait. One shared rule SHALL apply to both providers, with thin provider adapters.

#### Scenario: Real Codex approval
- **WHEN** a direct `codex --no-daemon` session holds an operation for human approval
- **THEN** Bell Tower raises a confirmed waiting alert with sound and the Sessions window shows the session as needing permission

#### Scenario: Automatically reviewed Codex operation
- **WHEN** Codex reviews an operation automatically
- **THEN** no confirmed wait or sound is raised

#### Scenario: Declined Claude prompt
- **WHEN** the user declines a Claude Code permission prompt
- **THEN** the waiting state clears shortly after the decline, without waiting for the next prompt
