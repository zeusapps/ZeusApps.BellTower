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

Shared-Codex conversation-to-client binding was decided on 2026-11-03 for Codex 0.165.0 in non-elevated default shared mode. Hook ancestry, inherited environment, log process identifiers and protocol thread metadata do not identify the client ([evidence 009](evidence/native-adoption-009.md), [evidence 010](evidence/native-adoption-010.md)). The selected binder uses each thread's effective `codex_tui` MCP endpoint, which is the interactive client's own loopback listener ([analysis 006](reports/analysis-006.md) section A3, [evidence 011](evidence/native-adoption-011.md)):

1. Map the hook root `session_id` to the protocol thread ID. They were equal in every observed run; gate on the provider version and schema field.
2. Over the verified non-owning transport (decision 4), call `mcpServerStatus/list` with `threadId`, `serverName: "codex_tui"` and `detail: "toolsAndAuthOnly"`. Never omit `serverName`, because the unscoped call runs full server discovery. Retain only the server name, the origin port and the runtime status; discard tools, metadata and capabilities. The probe showed no visible client change, hook or turn from this read.
3. Resolve the loopback port's listening owner. Accept only a live Codex interactive client process, not `app-server`, that has an established backend-owned connection to that port. Recheck PID creation identity at every action. Continue through that client's shell to the existing VS Code or Windows Terminal registration under the direct binding rules above.
4. Latest-only rule (user decision): a thread is actionable only while it is its owner client's most recent binding; older threads bound to the same client are unverified. Time-ordered thread IDs may order bindings. `createdAt` is not reliable for never-submitted threads.
5. Fail closed: a missing origin (IDE, app or remote clients, or a user-owned `codex_tui` server), a missing listener (`runtimeStatus` lags after client exit), a non-client owner, a missing backend connection, an unloaded thread, or an unknown version or field yields unverified and no host actions. Conversation tracking continues.

Known limits: resuming a thread that another client still has open keeps the original owner's endpoint and fires no hook. If that owner then moves to another thread, the latest-only rule demotes the joined thread rather than confirming the joining client. Not yet exercised natively: delegated or background threads (candidate discriminators `parentThreadId`, `threadSource` and `agentRole`; `forkedFromId` identifies forks), an owner leaving by resuming a third client's thread, and port reuse. These stay acceptance items. The supported `features.daemon_auto_start = false` setting does not avoid an already running shared backend and is not offered in setup or documentation (user decision).

On 2026-10-29 the user explicitly retained the full terminal-support scope. Tracking/runtime-only shared-Codex behavior is not an accepted scope reduction. Native host acceptance in VS Code and Windows Terminal remains required under task 6.1.

### 4. Add non-owning Codex runtime transport

Extend the transport boundary beneath the existing name and attention clients to support verified local shared-runtime access as well as owned authenticated loopback runtimes. Locate the endpoint for the appropriate client home/version and verify its local provenance; do not enumerate arbitrary sockets or trust an endpoint from a tool payload. Use bounded connection/read budgets and existing backoff. A normal observer must not call `thread/resume`, take approval ownership, launch model work or enumerate transcript contents to obtain status.

Reuse `thread/read` for exact-conversation runtime state and existing native-name ownership policy for permitted `thread/name/set`. Capability discovery must use exact conversation IDs; the trial's native TUI rows reported `source: vscode`, so source labels are not terminal identities. Map hook session identity to protocol thread identity explicitly; do not assume those identifiers are interchangeable for every version, fork or subagent.

Access and lifecycle ownership are distinct records. Closing Bell Tower closes its observer connection only. The existing managed-runtime manager retains authority solely for backends it created. This change adds no Claude native-title transport by assumption: reconcile its still-unfinished write surface with `sync-native-session-titles`, and preserve honest per-surface diagnostics.

The shared transport derives the backend from the connected socket's peer process and requires a live `codex` image classified as a shared backend (task 3.1). Bell Tower observes only a non-elevated shared backend: an elevated peer is refused with its own reason rather than observed across the privilege boundary, matching the non-elevated scope of decision 2 (orchestrator default recorded 2026-11-05, ANALYSIS-2302).

Parse and retain only the fields needed for identity, name ownership and runtime status. A metadata response can still contain a prompt preview even when turn history is omitted; discard previews, message contents and credentials rather than persisting or logging the raw response.

Alternative: starting a dedicated backend from every hook changes the user's launch and cannot take over an already running native TUI safely. Reading or editing native databases/transcripts to simulate protocol control is not selected.

### 5. Separate presentation consent from destination evidence

An adopted destination does not acquire `ManagedTitle` or `ManagedBackground` merely because a hook was delivered. Reuse applicable saved explicit controls; otherwise adopt in tracking-only mode until presentation is enabled through the normal reviewed configuration/UI path. A surface becomes writable only when its destination, write mechanism and safe restoration are verified. Keep console attachment in the disposable writer and check attachment generation at every write/restoration.

Pre-launch coloring remains an existing managed-launch capability. Native first-hook presentation begins after adoption, possibly at the first prompt. Do not change shared terminal themes or restore into a shell that has been reused. If safe prior-value restoration is unavailable, leave that surface unchanged and expose its specific status; proving or explicitly resolving that gap is part of acceptance, not an automatic permanent exemption.

### 6. Setup selects hooks and bridges independently of profiles

Separate provider hook and bridge setup from profile generation and PowerShell imports in the setup plan. Preserve current ownership/backups, handler trust and removal rules. Existing profile users can continue unchanged; native-command setup need not import a shell module or define commands. It also does not implicitly enable a new sign-in service: monitoring must be running through Bell Tower's existing independent lifecycle, and capture remains durable while it is stopped.

Diagnostics expose discovery, conversation health, runtime access, terminal binding, navigation precision and each presentation/restoration surface separately. Use supported, proven-restricted and unverified consistently, including the exact provider version and mode and any first-hook timing limit.

### 7. Tolerate provider drift with runtime self-checks, not version gates

Claude Code and Codex release new versions daily, so a version allowlist that disables features until each release is re-verified would not scale (user decision, 2026-11-04). Terminal actions stay available on any provider version while Bell Tower's own runtime checks pass; a check failure disables host actions for that conversation only, while tracking, attention and notifications continue and diagnostics explain the failed check.

- **Cross-check classification.** Mode classification uses the command line and the process relationships together. A Codex interactive client must not be the backend that other Codex clients connect to; a shared backend is started by a Codex client rather than directly by a shell. When the two signals disagree, the mode is unknown and fails closed. A renamed subcommand is then caught by process relationships even when the vocabulary misses it.
- **Revalidate bindings at the action boundary.** In addition to the decision 3 listener, backend-connection and creation-identity checks, the shared-Codex binder confirms that the resolved client is connected to the backend that hosts the conversation. A field whose meaning has changed then fails this check instead of producing a silent wrong binding.
- **Version table for diagnostics only.** A per-provider table records which versions were verified for each capability. A newer version does not disable anything; doctor reports it as newer than verified. Unrecognized command-line shapes are recorded locally for diagnostics, without prompt or path content.
- **Retained real hooks as regression fixtures.** Sanitized hook inputs and classification records captured from native probes are kept as test fixtures and refreshed when a provider change is investigated.
- **Scheduled drift detection.** A scheduled CI job installs the latest published `codex` and `claude` packages, compares their help output and Codex protocol schema with the recognized vocabulary and schema fields, and runs the isolated no-quota Codex probe. It opens an issue only when something differs, so releases follow actual drift rather than every provider release. Whether a Claude check can run without model quota must be confirmed before relying on it.

Alternative: gating terminal actions on verified versions is safer for unknown behavior but would disable features for most users most of the time; rejected.

## Risks / Trade-offs

- A shared-Codex client endpoint can go stale when another client joins a still-subscribed thread -> apply the latest-only rule, recheck the listener, backend connection and creation identity at the action boundary, and report unverified on any doubt; native acceptance covers the remaining sub-cases.
- New lifecycle semantics could let an older tray misclassify shared conversations -> the task 2.4 assessment found no new gate is needed while shared records add only `evidence.terminal` fields and keep legacy `Health` and `UnverifiedSince` meanings; the binder must never write legacy `Process`, `Host.Shell`, window, terminal-session or `EndedAt` fields for a shared conversation, because an older tray would end it on a dead legacy shell. Writing them would first require an `attachmentHealth` 2 gate across the release contract, the stable launcher and snapshot validation. Rolling back to an older tray restores its permanent `unavailable` for shared conversations, which a newer hook no longer keeps.
- Shared backend environment can carry old launch metadata, including a Bell Tower flag inherited by a nested client ([analysis 006](reports/analysis-006.md) ANALYSIS-500) -> classify direct-client evidence separately and require process evidence rather than an inherited flag as proof of ownership.
- A bridge or runtime may disappear between observation and action -> recheck creation identities, generation and freshness at the action boundary and retain safe partial/unavailable results.
- Native-name writes could erase a user title -> reuse pause/ownership/override policy; the disposable successful write is not consent to rename real sessions.
- Observation may add startup latency or hook contention -> keep runtime and host work outside capture; verify unchanged hook latency budgets and bounded polling/backoff.
- Provider versions drift daily -> rely on the decision 7 runtime cross-checks and action-time revalidation rather than version gates; report newer-than-verified versions and unrecognized shapes in diagnostics, and detect drift with a scheduled CI job. Residual risk: a change that keeps both the command line and the process relationships identical while changing behavior is only caught by the scheduled job or acceptance trials.
- Probe topology differs from desktop behavior -> require native Claude/Codex trials in VS Code and Windows Terminal, including mixed and repeated same-directory sessions; do not count pseudo-consoles as that acceptance.

## Migration Plan

1. Preserve the retained evidence and finish mode/terminal-binding probes before extending ownership decisions. Keep unresolved surfaces visible and do not silently expand to wrappers.
2. Add shared evidence/transport boundaries with backward-readable records and any required compatibility gating; exercise native and legacy ingress consistently.
3. Add native adoption and optional setup selection while retaining current profiles, launch functions, owned runtimes, explicit overrides and existing active sessions.
4. Verify fresh native launches and resumes in both supported local hosts; exercise mixed managed/native providers, Bell Tower restart, terminal detach, continuing backend work and provider crash.
5. Enable only independently verified surfaces. Update diagnostics/documentation with remaining evidence gaps; do not declare all native terminal capabilities complete while a required surface is unverified without an explicit user disposition.
6. Rollback removes only unchanged owned setup additions and stops only Bell Tower owned helpers. Existing native clients/backends continue; shared-state rollback follows the versioned compatibility contract.
