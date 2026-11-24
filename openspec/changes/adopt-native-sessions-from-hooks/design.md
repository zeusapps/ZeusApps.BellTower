# Design

## Context

See [proposal.md](proposal.md) for the requested behavior and [evidence/findings.md](evidence/findings.md) for the evidence boundaries. Design is warranted because runtime ownership, persisted lifecycle semantics, provider transport and terminal presentation cross several modules.

The existing fast ingress already accepts hooks independently of launch profiles. `AttachmentCapture` inspects agent and shell ancestry with creation-time identities; VS Code registers terminal shell identities through its bridge. However, both ingress and legacy `HookIngestor` currently treat Codex as dedicated only when the launch supplies `BELLTOWER_DEDICATED=1`; otherwise they clear process/host/presentation fields. A native standalone Codex launch is therefore not automatically a verified direct attachment today, even if its process topology could support one.

`CodexConnection` currently accepts an authenticated loopback WebSocket for a Bell Tower owned runtime. Native probes on Codex 0.164.0 established a second connection to an existing private shared backend through its Windows Unix-domain socket and WebSocket handshake. `thread/read` and `thread/name/set` worked without `thread/resume`; a native TUI emitted the updated title. This establishes a transport opportunity, not a terminal binding or permission to own that backend.

`AttachmentHealth`, reduction and presentation currently rely heavily on one process attachment. Terminal disconnection from the native Codex TUI can leave backend work alive. Existing `unavailable` policy must remain protective for terminal actions while independently verified conversation evidence can support observation and attention.

## Goals / Non-Goals

**Goals:**

- Use one adoption and capability policy for both providers, with adapters supplying evidence rather than separate product flows.
- Adopt native commands without command shadowing; preserve existing launch paths and all explicit user controls.
- Keep conversation identity, runtime access, terminal destination and lifecycle ownership distinct.
- Reuse the current fast ingress, host routing, isolated writer, name policy and attention logic.

**Non-Goals:**

- Taking over keyboard input, answering approvals, sending prompts or changing the model's permissions.
- Making shell wrappers, PowerShell aliases or dedicated terminal profiles prerequisites.
- Stopping/replacing shared provider backends or upgrading a running client to change its mode.
- Treating local Windows results as proof for WSL, SSH, cloud, arbitrary shells or remote endpoints.
- Completing unrelated native-title or VS Code activation changes by assertion; their actual contracts and verification remain separate dependencies.

## Decisions

### 1. Hooks initiate shared adoption; the consumer resolves additional evidence

Keep bounded capture and neutral output in the ingress. Resolve runtime connections, host bridge matches and presentation policy in the consumer or existing asynchronous services. A later accepted lifecycle event can establish a missed conversation; replay must not synthesize a past attention episode.

Use the native provider/conversation identity for saved identity and attention. A direct client process or verified host binding identifies an attachment generation. A shared backend's PID is runtime evidence, never a unique terminal attachment. Never promote a captured `BELLTOWER_*` flag or terminal variable to ownership without verifying that it belongs to the current client generation.

Alternative: requiring the existing launch module supplies strong launch evidence but does not meet the native-command objective. Replacing `claude` and `codex` with shell functions remains an unselected alternative and requires a separate scope decision if later needed.

### 2. Model independent evidence with common capability policy

Persist independently evaluated evidence for conversation activity/attention, direct agent or backend access, client/shell destination, and presentation ownership. Preserve PID plus creation-time checks, evidence freshness, replay ordering, ended generations, opt-outs and extension data.

For directly attached processes, existing exit and idle rules remain valid. For a detached shared conversation, terminal exit removes host actions without ending independently verified work. `thread/loaded/list` is discovery evidence only: the native trial retained idle threads and an additional active thread after both TUIs exited. Neither a loaded thread nor backend liveness alone proves an open terminal or active work for a particular conversation.

Terminal disconnect and continuing work are separate signals, decided by one shared rule with a thin provider adapter on the terminal side ([analysis 011](reports/analysis-011.md) ANALYSIS-1001):

- Terminal side. For Claude and embedded Codex, the terminal is disconnected when the verified agent process (PID plus creation time) is gone. For shared Codex, it is disconnected when the decision 3 owner client is gone (PID plus creation time), when that client's `codex_tui` listener or its backend connection is gone, or when the latest-only rule demotes the binding. Codex `runtimeStatus` is not used: probe 011 showed it still reporting `connected` after the client was killed.
- Work side. Continuing work comes from exact-conversation runtime status (`thread/read` for Codex) and hook events, never from backend liveness, `thread/loaded/list`, `runtimeStatus` or the presence of a helper process.
- A shared conversation that was never bound (no origin, an IDE or app client) has no terminal-side signal. It stays terminal-unverified, not disconnected, and attention and tracking continue. A latest-only demotion or a terminal disconnect is never reported as conversation completion.

A Bell Tower MCP server was evaluated as a liveness and identity source and rejected ([analysis 011](reports/analysis-011.md) ANALYSIS-1000). In shared Codex mode the server is a per-thread child of the backend: it outlives the terminal until the thread unloads (1800 s by default, or indefinitely while another client is subscribed) and carries no client identity, so it only repeats "thread loaded". For Claude and embedded Codex its lifetime equals the agent process Bell Tower already verifies, and Claude's session ID in its environment goes stale after `/clear`. It would add provider settings edits and per-thread processes without new evidence.

Persisting these lifecycle semantics is gated on the task 2.4 compatibility assessment. An older tray reads the 2.1 evidence through the wire encoding that fails closed, but a disconnect without completion is a new meaning: if an older consumer would read it as an ended session, or treat a still-open shared conversation as live, the new semantics require a consumer-level gate in the existing distribution compatibility mechanism (`AttachmentHealthCompatibility` and `CaptureCompatibility`) before they are written.

Use the same freshness and uncertainty rules for either provider's independently available evidence. Keep actionable attention based on confirmed supported waits; missing terminal targeting must not fabricate or erase a wait. Missing runtime support retains the current conservative unavailable classification for the affected operation.

Alternative: marking every shared backend thread as a live terminal would violate stale-target safety. Marking every terminal disconnect as conversation completion would lose continuing work.

### 3. Establish direct bindings from process evidence and host registration

For a direct local agent, validate the hook-to-agent and agent-to-shell relationship, then match that live shell identity to the existing fresh host registration. Do not hardcode Claude as direct or assume every executable named codex is a dedicated TUI: distinguish interactive client, embedded runtime, shared daemon and headless execution with actual evidence. Native/headless observations remain useful without a terminal destination.

Reuse `VsCodeNavigator` and the current window-verification contract, including partial terminal-only results when the window cannot be activated safely. Reuse verified Windows Terminal routing and its current tab/pane precision limits. A host marker or title can only contribute to a checked binding protocol; a title alone is not durable identity.

Shared-Codex conversation-to-client binding is not implemented in this change (decision 8). The binder below was selected on 2026-11-03 for Codex 0.165.0 in non-elevated default shared mode and is retained as investigated evidence for a possible later change; until then every shared-backend conversation is terminal-unverified. Hook ancestry, inherited environment, log process identifiers and protocol thread metadata do not identify the client ([evidence 009](evidence/native-adoption-009.md), [evidence 010](evidence/native-adoption-010.md)). The investigated binder uses each thread's effective `codex_tui` MCP endpoint, which is the interactive client's own loopback listener ([analysis 006](reports/analysis-006.md) section A3, [evidence 011](evidence/native-adoption-011.md)):

1. Map the hook root `session_id` to the protocol thread ID. They were equal in every observed run; gate on the provider version and schema field.
2. Over the verified non-owning transport (decision 4), call `mcpServerStatus/list` with `threadId`, `serverName: "codex_tui"` and `detail: "toolsAndAuthOnly"`. Never omit `serverName`, because the unscoped call runs full server discovery. Retain only the server name, the origin port and the runtime status; discard tools, metadata and capabilities. The probe showed no visible client change, hook or turn from this read.
3. Resolve the loopback port's listening owner. Accept only a live Codex interactive client process, not `app-server`, that has an established backend-owned connection to that port. Recheck PID creation identity at every action. Continue through that client's shell to the existing VS Code or Windows Terminal registration under the direct binding rules above.
4. Latest-only rule (user decision): a thread is actionable only while it is its owner client's most recent binding; older threads bound to the same client are unverified. Time-ordered thread IDs may order bindings. `createdAt` is not reliable for never-submitted threads.
5. Fail closed: a missing origin (IDE, app or remote clients, or a user-owned `codex_tui` server), a missing listener (`runtimeStatus` lags after client exit), a non-client owner, a missing backend connection, an unloaded thread, or an unknown version or field yields unverified and no host actions. Conversation tracking continues.

Known limits: resuming a thread that another client still has open keeps the original owner's endpoint and fires no hook. If that owner then moves to another thread, the latest-only rule demotes the joined thread rather than confirming the joining client. Not yet exercised natively: delegated or background threads (candidate discriminators `parentThreadId`, `threadSource` and `agentRole`; `forkedFromId` identifies forks), an owner leaving by resuming a third client's thread, and port reuse. These stay acceptance items. The supported `features.daemon_auto_start = false` setting does not avoid an already running shared backend and is not offered in setup or documentation (user decision).

On 2026-10-29 the user retained the full terminal-support scope. On 2026-11-13 the user reversed that for shared Codex (decision 8): tracking, alerts, name sync and permission status without a terminal binding is the accepted scope of this change. Native host acceptance in VS Code and Windows Terminal remains required under task 6.1.

### 4. Add non-owning Codex runtime transport

Extend the transport boundary beneath the existing name and attention clients to support verified local shared-runtime access as well as owned authenticated loopback runtimes. Locate the endpoint for the appropriate client home/version and verify its local provenance; do not enumerate arbitrary sockets or trust an endpoint from a tool payload. Use bounded connection/read budgets and existing backoff. A normal observer must not call `thread/resume`, take approval ownership, launch model work or enumerate transcript contents to obtain status.

Reuse `thread/read` for exact-conversation runtime state and existing native-name ownership policy for permitted `thread/name/set`. Capability discovery must use exact conversation IDs; the trial's native TUI rows reported `source: vscode`, so source labels are not terminal identities. Map hook session identity to protocol thread identity explicitly; do not assume those identifiers are interchangeable for every version, fork or subagent.

Access and lifecycle ownership are distinct records. Closing Bell Tower closes its observer connection only. The existing managed-runtime manager retains authority solely for backends it created. This change adds no Claude native-title transport by assumption: reconcile its still-unfinished write surface with `sync-native-session-titles`, and preserve honest per-surface diagnostics.

The shared transport derives the backend from the connected socket's peer process and requires a live `codex` image classified as a shared backend (task 3.1). Bell Tower observes only a non-elevated shared backend: an elevated peer is refused with its own reason rather than observed across the privilege boundary, matching the non-elevated scope of decision 2 (orchestrator default recorded 2026-11-05, ANALYSIS-2302).

Parse and retain only the fields needed for identity, name ownership and runtime status. A metadata response can still contain a prompt preview even when turn history is omitted; discard previews, message contents and credentials rather than persisting or logging the raw response.

Alternative: starting a dedicated backend from every hook changes the user's launch and cannot take over an already running native TUI safely. Reading or editing native databases/transcripts to simulate protocol control is not selected.

### 5. Separate presentation consent from destination evidence

An adopted destination does not acquire `ManagedTitle` or `ManagedBackground` merely because a hook was delivered. Reuse applicable saved explicit controls; otherwise adopt in tracking-only mode. One global native-presentation setting, on by default (user decision 2026-11-14) and switchable off, in the normal configuration/UI path enables title and background for adopted direct sessions (a verified agent process and shell: plain `claude`, `codex --no-daemon`); a shared-backend Codex conversation never acquires either. In Windows Terminal the managed title is also what the existing tab selector matches, so the same setting moves navigation from `window only` to the tab; with it off, navigation stays at the window. A surface becomes writable only when its destination, write mechanism and safe restoration are verified. Keep console attachment in the disposable writer and check attachment generation at every write/restoration.

Pre-launch coloring remains an existing managed-launch capability. Native first-hook presentation begins after adoption, possibly at the first prompt. Do not change shared terminal themes or restore into a shell that has been reused. For an adopted direct session the title is restored to the value read before Bell Tower's first write, only on the recorded shell and only while the console still shows Bell Tower's title. The background has no recorded prior value: on session end, switch-off or loss of qualification it is reset to the terminal default with OSC 110 and OSC 111, the sequences the managed wrapper sends on exit, and only when Bell Tower wrote a color to that shell (user decision 2026-11-13: the color exists for visual association between simultaneous sessions, not to preserve the prior color). Accepted trade-off: a background the user set by hand before the session becomes the profile default when the session ends. Saving the prior color is a possible later refinement. For any other surface whose prior value cannot be restored, leave it unchanged and expose its specific status.

### 6. Setup selects hooks and bridges independently of profiles

Separate provider hook and bridge setup from profile generation and PowerShell imports in the setup plan. Preserve current ownership/backups, handler trust and removal rules. Existing profile users can continue unchanged; native-command setup need not import a shell module or define commands. It also does not implicitly enable a new sign-in service. Capture remains durable while monitoring is stopped, and the ingress starts a stopped tray after writing a capture (decision 9, user decision 2026-11-15).

Diagnostics expose discovery, conversation health, runtime access, terminal binding, navigation precision and each presentation/restoration surface separately. Use supported, proven-restricted and unverified consistently, including the exact provider version and mode and any first-hook timing limit.

### 7. Tolerate provider drift with runtime self-checks, not version gates

Claude Code and Codex release new versions daily, so a version allowlist that disables features until each release is re-verified would not scale (user decision, 2026-11-04). Terminal actions stay available on any provider version while Bell Tower's own runtime checks pass; a check failure disables host actions for that conversation only, while tracking, attention and notifications continue and diagnostics explain the failed check.

- **Cross-check classification.** Mode classification uses the command line and the process relationships together. A Codex interactive client must not be the backend that other Codex clients connect to; a shared backend is started by a Codex client rather than directly by a shell. When the two signals disagree, the mode is unknown and fails closed. A renamed subcommand is then caught by process relationships even when the vocabulary misses it.
  - Revised 2026-11-24 (user decision, ANALYSIS-5600): the parent rule does not apply to a backend launched as the provider's managed daemon (Codex `app-server` with `--managed-daemon` before `--`). The provider documents that daemon as detached and relaunched by its updater, so its launcher is transient and its parent is often gone (trial 6.1: the updater restarted the backend to 0.172.0 and exited, and every plain `codex` conversation under it was stored as unknown). The shape is a Codex adapter fact; one Core rule skips the relationship check for it and keeps R1-R3 for every other shape, including Bell Tower's owned runtime. If the provider renames the hidden flag, classification falls back to the parent rule and fails closed to unknown. Claude has no shared backend and is never cross-checked. The shared transport keeps its `app-server` gate, of which the managed-daemon predicate is a subset by construction (pinned by tests), so capture and transport agree on this shape without the transport becoming stricter (ANALYSIS-5601).
- **Revalidate bindings at the action boundary.** Direct bindings recheck agent, shell and window creation identities at every action. The additional shared-Codex binder checks are not built (decision 8).
- **Retained real hooks as regression fixtures.** Sanitized hook inputs and classification records captured from native probes are kept as test fixtures and refreshed when a provider change is investigated.
- The diagnostics version table and the scheduled drift job agreed on 2026-11-04 are removed from this change (decision 8).

Alternative: gating terminal actions on verified versions is safer for unknown behavior but would disable features for most users most of the time; rejected.

### 8. Reduce the scope to stable provider surfaces (user decision, 2026-11-13)

After eleven tasks of groundwork a first real trial (plain `claude` in Windows Terminal on dev build 0.1.9020) showed correct naming and a completion alert, no color, and a notification click that did nothing. The user decided not to add more code that depends on provider internals that change with frequent Claude Code and Codex releases.

- Kept: everything already built, including the non-owning shared-backend name and status access (decision 4), which disables itself when a check fails. If it proves to break often, removing it is a separate small change.
- Added: one slice. Adopted direct sessions get reliable click results (task 4.1) and title and color, on by default with an opt-out (decision 5, task 4.3; default changed by user decision 2026-11-14), which rest on documented hooks and operating-system process identity. The title is restored on exit; the background is reset to the terminal default rather than restored (decision 5).
- Not built: the shared-Codex binder (decision 3), the diagnostics extras and version table, the scheduled drift job (decision 7) and the heavy real-host acceptance runs. The task list names each.
- Parity: plain default `codex` gets tracking, alerts, name sync and permission status but no navigation, title or color. This is an explicit user decision recorded as unfinished in the capability matrix and documentation, not a provider restriction. Supported routes to navigation and color for Codex are the Codex profile and `codex --no-daemon`.
- Review depth for the remaining tasks: one QA and one code-analyzer review on the final stable target instead of both reviews on every fix round. The user-operated trial (task 6.1) is the acceptance evidence.
- A per-project opt-in for tracking (a Bell Tower allowlist checked by the hook) and a Bell Tower MCP server were discussed and are not part of this change.

### 9. A hook starts a stopped tray (user decision, 2026-11-15)

Trial observation T-2: after a Windows restart no tray was running, so plain `claude` hooks were only queued and the session never appeared, although the hooks worked. The user chose to have the hook start the tray rather than a sign-in entry or a warning only. The same ingress serves both providers, so the behavior is identical for Claude Code and Codex.

- Order and failure: the ingress writes the capture first, then checks the tray. It never waits for the tray, never delays or fails the hook, and never changes its neutral output. A failed start is recorded as a bounded diagnostic; the capture stays queued.
- Detection: the existing tray-instance mutex. When it exists nothing else happens, so the steady-state cost is one named-mutex open.
- Suppression: choosing Exit in the tray, or an equivalent user stop command, records a user stop for the current Windows sign-in session (for example the token's logon session ID). Hooks do not start the tray while that record matches the current sign-in; starting the tray by hand clears it; a sign-out or restart makes it stale. A crash, an update or dev-build switch, or a management stop that restarts the tray is not a user stop. No start while an update or switch transaction is pending; the tray's own start gate remains authoritative.
- Burst control: concurrent hooks from several sessions make at most one start attempt per bounded interval, through an exclusive-create attempt record in the data directory; the tray-instance mutex still guarantees one tray.
- Launcher location: the ingress does not search for executables. Setup and the tray record the stable launcher path in the data directory; the ingress accepts only an absolute path to an existing regular file with the expected name and no reparse points. With no valid record it does not start anything, and doctor says so.
- Process creation: the stable launcher with the normal tray arguments, quiet start, detached, with no inherited handles (a provider waits for the hook's output pipe to close), no console window, no worse off than the user's shell (revised 2026-11-22: on Windows build 26300 every process, Explorer included, runs inside one session-wide job that cannot be left, so "outside any job" is unattainable; decision 10 defines the start), the launcher's directory as working directory, and the user's default environment rather than the provider's (no `CLAUDECODE`, `CODEX_*` or `BELLTOWER_*` leakage into the tray and the launches it starts).
- Diagnostics report whether hook-start is available (launcher recorded), the latest attempt and its result, and whether a user stop suppresses it in this sign-in session.

Alternatives: a sign-in Run entry is simpler and has no hook-path cost but does not cover a tray that crashed or was stopped by an update failure; a warning only leaves the session missing. Both rejected by the user; a sign-in entry can still be added later.

### 10. The hook starts the tray as a child of the user's shell (user decision, 2026-11-22)

Trial T-12: after a restart the first plain `claude` hook's start was refused with `provider-job-breakaway-incomplete`. DEV-058 found the cause. Every process in the sign-in session runs inside one session-wide job (flags BREAKAWAY_OK and SILENT_BREAKAWAY_OK, no kill-on-close) that breakaway, parent attributes and job lists cannot leave. Claude Code 2.1.330 adds no job; Codex 0.171.0 adds a kill-on-close job that breakaway does leave. The DEV-056 check that the child is in no job at all can therefore never pass, and it is removed.

- Primary start: create the tray with `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` set to the desktop shell, so it inherits the shell's jobs, token and environment and never the provider's.
  - Find the shell through `GetShellWindow` and its process, and verify its image, session, owner and creation time. If no shell can be verified, start nothing and keep the capture queued.
  - `bInheritHandles = FALSE`, so no handle from the shell or the hook is inherited.
  - The shell's token decides elevation, so an elevated hook still yields a non-elevated tray. This is verified in the trial.
- Child-side self-check: on start, the tray queries its immediate job. If it has KILL_ON_JOB_CLOSE, the tray records a log code and a doctor field and exits. This is diagnostic first, so a false trigger is visible rather than silently keeping the tray down.
- Fallback: when the shell start fails, a per-user scheduled task with no trigger, started on demand.
  - It runs with the limited interactive principal, no execution time limit, no battery conditions, and the ignore-new instance policy.
  - Setup creates it, doctor reports it and setup removal deletes it. It is not a sign-in entry: it never runs on its own.
- Unchanged: the user-stop suppression, the one-attempt-per-interval burst control, the launcher record checks, no start during an update or switch, and the hook never waiting.
- Risk: some security products flag a changed parent process. Diagnostics record which route started the tray, so the fallback can be chosen if that becomes an issue.

### 11. Terminal titles for native sessions are opt-in (user decision, 2026-11-22)

Trial T-5: the providers overwrite the terminal title. Codex rewrites it about 20 times a second during a turn, and Claude Code writes its own conversation title. Writing after the provider cannot win.

- Setup offers an opt-in step that disables the provider's own terminal title:
  - Claude Code: `CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1` in the settings `env` block.
  - Codex: the `[tui] terminal_title` setting, but only after a probe shows that this value actually stops Codex's title writes.
  - The step follows the existing ownership, backup, repeat and removal rules, and its consent text says the setting applies to the whole provider profile.
- With the step applied, adopted direct sessions get Bell Tower's title as decision 5 describes, and Windows Terminal tab navigation works through it.
- Without it, native sessions get color and badge only. Bell Tower writes no title, Windows Terminal navigation is reported as window only with the stated reason, and the capability matrix and doctor say which state applies, per provider.

### 12. Confirmed waits from provider evidence, not timing (user decision, 2026-11-22)

Trials T-9 and T-10. A real `codex --no-daemon` approval stays provisional, because the embedded runtime exposes no status endpoint. A declined Claude Code permission prompt fires no hook, so the wait clears only at the next prompt.

- First, a probe captures the real Codex `PermissionRequest` payload and the related transcript records for a held approval, an automatic review and a decline, with isolated provider homes.
- Then one shared reducer rule for both providers:
  - A provisional wait becomes confirmed when the session's evidence shows that a human must decide, from the Codex approval reviewer setting or an approval record in its transcript.
  - A wait clears when the evidence shows the decision was made, including a decline.
  - Timing never confirms a wait.
- Thin adapters:
  - Codex: the approval reviewer setting, or the transcript approval record, whichever the probe shows is reliable.
  - Claude Code: a bounded read of the end of the transcript for the rejected tool result and the interrupted-request record.
- Reads are bounded, path-free in diagnostics, and never block the hook.

### 13. Every click result is shown in Bell Tower's own notice (2026-11-22)

ANALYSIS-4600 found that balloon visibility depends on Windows Focus and notification settings that applications cannot detect. Every click result that needs a reason is therefore shown in Bell Tower's own notice, with a balloon as an optional extra.

- The notice is topmost from creation and is never activated (ANALYSIS-4601), so the terminal the click focused keeps the keyboard.
- Stale-key and navigator-failure clicks are logged and explained too (ANALYSIS-4602).

## Risks / Trade-offs

- Plain default `codex` users see alerts they cannot click through to a tab -> state the limit in the chip result, doctor and documentation, and name the Codex profile and `codex --no-daemon` as the supported routes.
- Writing a title or background into a session Bell Tower did not launch could leave a shell altered -> the user can switch the setting off (on by default since the user decision of 2026-11-14; settings are revisited in a separate change), writes go through the existing isolated writer with identity checks, and restoration touches only what Bell Tower changed (task 4.3).
- New lifecycle semantics could let an older tray misclassify shared conversations -> the task 2.4 assessment found no new gate is needed while shared records add only `evidence.terminal` fields and keep legacy `Health` and `UnverifiedSince` meanings; a later binder must never write legacy `Process`, `Host.Shell`, window, terminal-session or `EndedAt` fields for a shared conversation, because an older tray would end it on a dead legacy shell. Writing them would first require an `attachmentHealth` 2 gate across the release contract, the stable launcher and snapshot validation. Rolling back to an older tray restores its permanent `unavailable` for shared conversations, which a newer hook no longer keeps.
- Shared backend environment can carry old launch metadata, including a Bell Tower flag inherited by a nested client ([analysis 006](reports/analysis-006.md) ANALYSIS-500) -> classify direct-client evidence separately and require process evidence rather than an inherited flag as proof of ownership.
- A bridge or runtime may disappear between observation and action -> recheck creation identities, generation and freshness at the action boundary and retain safe partial/unavailable results.
- Native-name writes could erase a user title -> reuse pause/ownership/override policy; the disposable successful write is not consent to rename real sessions.
- Observation may add startup latency or hook contention -> keep runtime and host work outside capture; verify unchanged hook latency budgets and bounded polling/backoff.
- Starting the tray from a hook could delay or hang the hook, leak the provider environment or restart a tray the user stopped -> decision 9: capture first, detached start without inherited handles or provider environment, a sign-in-scoped user-stop record, one attempt per interval; measure hook latency with the tray running and stopped.
- Provider versions drift daily -> rely on the decision 7 runtime cross-checks and action-time revalidation rather than version gates, and keep new work on documented hooks and process identity (decision 8). Residual risk: a provider change that keeps the command line and process relationships identical while changing behavior is caught only by use.
- Probe topology differs from desktop behavior -> require the user-operated trial of task 6.1 in VS Code and Windows Terminal, including a mixed and a same-directory case; do not count pseudo-consoles as that acceptance.

## Migration Plan

1. Preserve the retained evidence. Keep unresolved surfaces visible and do not silently expand to wrappers.
2. Add shared evidence/transport boundaries with backward-readable records and any required compatibility gating; exercise native and legacy ingress consistently.
3. Add native adoption and optional setup selection while retaining current profiles, launch functions, owned runtimes, explicit overrides and existing active sessions.
4. Verify fresh native launches in both supported local hosts through the task 6.1 trial, including one mixed managed/native run; restart, detach, continuing backend work and crash rely on existing fixture coverage.
5. Enable only independently verified surfaces. Update diagnostics/documentation with remaining evidence gaps; do not declare all native terminal capabilities complete while a required surface is unverified without an explicit user disposition.
6. Rollback removes only unchanged owned setup additions and stops only Bell Tower owned helpers. Existing native clients/backends continue; shared-state rollback follows the versioned compatibility contract.
