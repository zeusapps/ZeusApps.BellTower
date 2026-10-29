# Design

## Context

AlertPolicy currently announces each episode once, using an in-memory receipt set and silent restoration based on tray start time. SessionState already holds episode, viewed and acknowledged state. No idle probe or reminder receipt exists. Dependencies establish reliable approval/liveness and shared speech policy.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `reconcile-session-liveness-and-title-refresh`, `confirm-claude-human-permission-waits`, `suppress-voice-during-microphone-use`.

**Primary source boundaries:** Core/AlertPolicy and reminder policy/receipts; tray scheduling and AudioItem; IO/state configuration with backward-compatible fields; Windows idle probe and policy tests.

## Goals / Non-Goals

**Goals:** Repeat missed unresolved attention while the user is away and publish stable escalation eligibility without modifying provider waits.

**Non-Goals:** Do not resolve waits, synthesize new attention episodes, remind acknowledged/unverified sessions, or play catch-up audio at startup.

## Decisions

### 1. Explicit defaults and validation

Add reminder settings: enabled true, initialDelayMinutes 5, idleThresholdMinutes 5, repeatIntervalMinutes 10, maxPerEpisode 3, escalationAfterReminder 1. Validate finite positive durations within one minute to 24 hours and a count from one to ten; allow explicit disable. These are proposed operational defaults, not recovered field facts. Read local keyboard/mouse idle through a replaceable GetLastInputInfo boundary for the current Windows interactive session, handling tick wrap and unknown/session-disconnected results conservatively.

### 2. A separate per-episode scheduler

Consume live current-session attention projections after permission confirmation and liveness reconciliation. Track firstEligibleAt, lastReminderAt, ordinal, nextDue and receipt ID per provider-qualified session and episode. Eligibility requires both configured wait delay and observed idle threshold; a recent input blocks until idle again. Viewed is not acknowledgment: a still-unanswered viewed chip may remind after later inactivity, while explicitly acknowledged waits do not. Provider wait timestamps, episode IDs, palette and viewed timer preference are never rewritten.

### 3. Persistence and restart behavior

Persist scheduler receipts in a dedicated bounded app-owned record or additive state section with unknown-field preservation; do not overload the normal one-announcement receipt set. Commit a scheduling receipt before enqueueing to prevent duplicate replay, revalidate before playback and retain failed/muted outcomes without later catch-up. Startup restores quietly and establishes the next future deadline; multiple missed intervals never emit a burst. New episodes reset independent counters; resolved/ended/acknowledged episodes cancel queued work and eventually prune receipts.

### 4. Shared delivery contract

Extend AudioItem with an additive notification kind and reminder ordinal while keeping Key/Episode validity checks. Route local reminders through AudioQueue so mute, microphone policy and aggregation remain authoritative. Publish an escalation candidate only after idle/time eligibility crosses the configured threshold, regardless of successful audio playback or Mute. Define the record as protocol version, provider/session key, episode, reason, wait start, reminder ordinal, eligibility time and opaque stable receipt ID. Derived identity is attached from the current identity projection by the consumer; no commands/transcripts belong in it.

## Risks / Trade-offs

- [Reminder storms after restart or long absence] -> Persist ordinal/deadline, cap per episode and skip elapsed intervals.
- [Input APIs measure only an interactive session] -> Document RDP scope and treat unknown state as ineligible for automatic reminders.
- [A queued reminder races resolution] -> Validate current key/episode/health at scheduling and playback.

## Migration Plan

Add validated defaults without modifying existing Voice/Mute/grace values. Introduce scheduler receipts compatibly and keep rollback from interpreting them as provider lifecycle. Provide preview/status/config controls. Startup and disabled-to-enabled transitions establish future deadlines instead of reannouncing past waits.

## Verification and fresh-context notes

Deterministic clock and idle fakes cover threshold edges, repeat caps, input resetting eligibility, acknowledged versus viewed, resolved/new episode races, mute, microphone suppression, aggregate audio, receipt crash/restart and unavailable idle state. No real user input or sound is generated. Export the escalation contract for the dependent webhook implementation.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Local reminders in field report 12.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
