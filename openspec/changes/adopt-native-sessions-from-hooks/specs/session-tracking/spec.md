# Session Tracking Delta

## ADDED Requirements

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
When a hook starts a stopped tray, Bell Tower SHALL create it as a child of the verified desktop shell of the same sign-in session, with no inherited handles, so the tray receives the shell's jobs, token and environment and never the provider's. If no shell can be verified, it SHALL start nothing and keep the capture queued. On start, the tray SHALL check its immediate job and, if that job would end it when the job closes, record a diagnostic and exit. When the shell start fails, Bell Tower SHALL use a per-user scheduled task with no trigger, started on demand, which setup creates and removes and doctor reports. The behavior SHALL be identical for Claude Code and Codex. The user-stop suppression, burst control and update gate SHALL be unchanged.

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

## MODIFIED Requirements

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
