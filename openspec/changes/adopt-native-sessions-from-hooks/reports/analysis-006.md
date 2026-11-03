<!--
Saved by the orchestrator verbatim from the reviewer's inline return.
Reviewer: ANALYSIS-006 / code-analyzer
Client / version / mode: Claude Code delegated subagent (Claude Agent SDK; client version not exposed) / source-only
Model: claude-opus-5-5
Refusal: the client refused the reviewer's write of this path ("subagents must return reports inline", as reported by the reviewer); the reviewer did not retry.
-->

# Analysis report: adopt-native-sessions-from-hooks / 006

## Review target

- Reviewer / role: ANALYSIS-006 / code-analyzer.
- Client / version / mode / model (resolved ID): Claude Code agent built on the Claude Agent SDK (client version not exposed to the agent) / delegated subagent, source-only / `claude-opus-5-5`.
- Date: 2026-11-03T12:26Z to 12:39Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-006, bounded source-only investigation of the shared-Codex conversation-to-client binder.
- Task and requirement references: [tasks.md](../tasks.md) 1.2, 1.3 and 4.2; [design.md](../design.md) decisions 1 to 5. Full terminal support is still the required scope; this report gives evidence and a recommendation, not a scope decision.
- Accepted inputs: [native-adoption-009](../evidence/native-adoption-009.md), [native-adoption-010](../evidence/native-adoption-010.md), [QA-010](qa-010.md), [analysis-004](analysis-004.md), [analysis-005](analysis-005.md). I read two ignored artifacts for context only and copied no raw values: `artifacts/native-adoption-001` (the installed 0.165.0 generated schema) and `artifacts/native-adoption-010/monitor.log`.
- Scope and relevant neighboring paths: `src/BellTower/Hosts`, `src/BellTower/Providers`, `src/BellTower/Sessions/CodexRuntimeHost.cs`, `src/BellTower/IO/CodexRuntimeStore.cs`, `vscode`. Neighbors read: `src/BellTower.Ingress/Program.cs`, `src/BellTower/Integration/BellTower.psm1`, `src/BellTower/Sessions/AgentLauncher.cs`.
- Upstream source: the official `rust-v0.165.0` tag of `openai/codex`, fetched as the tagged source archive into a disposable scratch location outside the repository (not committed). The cached `tui/src/lib.rs` and `tui/src/startup_orchestration.rs` in `artifacts/binder-analysis-004` are byte-identical to the archive copies. This is version-tagged primary source. It does not prove the installed binary is identical; where it matters I cross-checked the installed binary's generated schema (see avenue A).
- Baseline commit: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Implementation identity, before (`./scripts/review-identity.ps1 -Baseline a69392e9ae6734b7f229d9266ebe4a3144ef49a4 -Paths src/BellTower/Hosts src/BellTower/Providers src/BellTower/Sessions/CodexRuntimeHost.cs src/BellTower/IO/CodexRuntimeStore.cs vscode`):

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: src/BellTower/Hosts src/BellTower/IO/CodexRuntimeStore.cs src/BellTower/Providers src/BellTower/Sessions/CodexRuntimeHost.cs vscode
Manifest (A added, M modified, D deleted, ? untracked):
Tracked diff SHA-256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
Untracked file SHA-256:
Summary: head=a69392e baseline=a69392e files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc
```

- Before/after identity: the same command after the analysis printed byte-identical output (compared line by line). Nothing changed unexpectedly. I made no repository writes; the report write was refused (see limitations).
- Prior reports / rechecked findings: none rechecked; this is a new investigation.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| A. Per-connection association | Read the tagged app-server thread state, resume path, MCP status processor, protocol method table and diagnostics, plus the TUI loopback-listener source | Sections A1 to A3 | No direct connection or client exposure. New source-supported indirect channel: each thread's effective MCP endpoint, which is the TUI's own listener. Needs a native probe. |
| B. Sole-client exclusion | Read daemon attachment, TUI listener start conditions and Windows platform limits | Section B | Not viable as a binder; at most fail-closed corroboration. |
| C. Shared-daemon opt-out | Read TUI daemon selection, the feature table, the daemon README and the exclusion logic | Section C | One supported config key exists (`features.daemon_auto_start`). It does not force standalone mode while a daemon is running. |
| D. Host correlation | Read TUI title items, defaults and truncation, the existing VS Code bridge and Windows Terminal navigation | Section D | By default the title has no stable thread identifier. Host bridges provide the final hop once a TUI process is proven. |
| E. Other thread-to-process links | Read the rollout, log, attachment, worktree and command-line paths | Section E | Nothing better than A. |
| Product code relevant to binding | Inspected `AttachmentCapture`, `HookIngestor`, the ingress, the module and the launcher | ANALYSIS-500 | One risk recorded. |
| Performance | No measurement | Hypotheses are labelled in the final section | Not performed. |
| Build/tests/format | Not run | The target has an empty diff; no implementation changed | Not applicable. |
| Native/protocol/provider operations | Not performed | Source-only assignment | No provider launch, socket connection or settings edit. |

## A. Per-connection thread association

### A1. What the TUI's loopback listener is (source fact)

The per-client loopback listener seen in probes 009/010 is the TUI's own approval-gated MCP server for task tools.

- It binds `127.0.0.1:0`, creates a random bearer token, and builds an MCP server config `{"url":"http://<addr>/mcp","http_headers":{"Authorization":...}}` ([dynamic_tools_mcp.rs L1](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L1), [L149-L162](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L149-L162), [token check L222-L236](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L222-L236)).
- The TUI adds that config to every `thread/start`, `thread/resume` and `thread/fork` it sends, as the per-thread override `mcp_servers.codex_tui` ([configure/configure_mcp L77-L97](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L77-L97), [start L924](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app_server_session.rs#L924), [fork L1127](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app_server_session.rs#L1127), [resume](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app_server_session/rollout_history.rs#L179), [namespace `codex_tui`](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools.rs#L76)).
- The backend's per-thread MCP runtime then connects to this server. That is the connection pair observed at client launch and at each `/new`.

When the server starts:

- It starts for local-daemon and embedded targets that do not use a remote workspace or environment ([startup.rs L497-L512](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app/startup.rs#L497-L512)).
- Task tools are on exactly when the app server is not in-process ([L520](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app_server_session.rs#L520)).
- It refuses to start if a user-configured server already uses the namespace, or if managed requirements do not allow it ([L526-L545](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app_server_session.rs#L526-L545)). A start failure is only logged as a warning.
- There is one server per TUI process for its whole lifetime. When the backend reconnects, the TUI suspends and re-attaches the same server and re-resumes its thread with the same config ([reconnect.rs L49-L51](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app/reconnect.rs#L49-L51), [L565-L567](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app/reconnect.rs#L565-L567)).

Can an observer query this listener read-only? No, and it should not try:

- It needs the per-process bearer token, which only travels inside the backend's thread config.
- Its tools include mutating delegation tools (`create_thread`, `send_message_to_thread`, `fork_thread`; [dynamic_tools.rs L78](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools.rs#L78)).
- None of its tools reports the displayed thread. `call_tool` receives a thread ID only when the model calls a tool ([L314-L319](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L314-L319)).

Verdict for using the listener directly: **not viable**.

### A2. Connection, subscription and turn-submitter tracking (source fact)

- The app server tracks subscribers internally (`subscribed_connection_ids`, used in resume decisions; [thread_processor.rs L4699](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L4699)). Connection IDs are internal server counters ([analysis-004](analysis-004.md)).
- No method or notification in the tagged method table returns connection, subscriber or turn-submitter identities.
- `server/diagnostics` (experimental) returns only the backend's own PID, memory figures and gauges, and no gauge counts connections ([diagnostics.rs L14-L28](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-protocol/src/protocol/v2/diagnostics.rs#L14-L28), [method L506-L512](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-protocol/src/protocol/common.rs#L506-L512)).
- `remoteControl/client/list` covers remote-control pairings, not local clients.
- Stored thread attachments are durable resource references that clients write (worktrees, pull requests). They are not connection records ([README L330-L338](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/README.md#L330-L338)).

Verdict for direct connection association: **not viable** (not exposed).

### A3. Indirect channel: each thread's effective MCP endpoint (new avenue)

What the protocol offers:

- `mcpServerStatus/list` is supported and not experimental ([common.rs L1277-L1281](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-protocol/src/protocol/common.rs#L1277-L1281)). Its params accept `threadId` and `serverName` ([mcp.rs L50-L66](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs#L50-L66)).
- Each returned status includes `httpOrigin`, documented as the "HTTP origin of the effective configured endpoint ... Excludes credentials, path, query, and fragment" ([mcp.rs L79-L99](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs#L79-L99)).
- With both `threadId` and `serverName` set, the server reads that thread's own MCP runtime config and reuses the thread's existing connection without starting other servers ([mcp_processor.rs L375-L416](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/mcp_processor.rs#L375-L416), [origin L572-L583](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/mcp_processor.rs#L572-L583), [runtime/status.rs L16-L37](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/runtime/status.rs#L16-L37), [README L446-L450](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/README.md#L446-L450)).
- The installed 0.165.0 generated schema in `artifacts/native-adoption-001` has the same `threadId`, `serverName` and `httpOrigin` fields.

The resulting binding chain:

1. An observer on the already proven private-socket transport (decision 4) calls `mcpServerStatus/list {threadId: <hook session_id>, serverName: "codex_tui", detail: "toolsAndAuthOnly"}` and gets back `http://127.0.0.1:<port>`.
2. Windows names the process that owns the LISTEN socket on that port. Separately, it lists the backend's ESTABLISHED connections to that port.
3. That gives thread -> port -> TUI process (PID plus creation time) -> its shell -> the existing host registration. No wrappers, input injection or name writes are involved.

Which TUI the address names (from the resume source): the TUI that most recently loaded the thread into the backend, which is not always the one displaying it now.

- `thread/start` and `thread/fork` from a TUI carry that TUI's address.
- `thread/resume` of an already loaded thread: if the thread has no subscribers, is idle and is not running, the backend shuts down the cached copy and reloads it with the new overrides, so the resuming TUI becomes the owner. Otherwise all overrides, including `mcp_servers`, are ignored with a warning ([L4693-L4745](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L4693-L4745), [L228-L231](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L228-L231)). This matches probe 010: case (c) produced a fresh pair to client C and a hook; case (b), a rejoin while B was still subscribed, produced neither.
- Threads created by the TUI's own delegation tools also carry its address ([app_server_events.rs L670-L676](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app/app_server_events.rs#L670-L676)), even though the TUI does not show them as its main conversation.
- A loaded thread with no subscribers and no activity is unloaded after `thread_unload_delay_secs`, default 1800 s ([thread_lifecycle.rs L58-L66](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_lifecycle.rs#L58-L66), [default](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/config/mod.rs#L4100-L4101)).

Side effects and auth of the read (source):

- The call runs on the observer's own initialized connection; no auth is needed beyond the socket provenance already required.
- With `serverName` and `threadId` set, it runs `refresh_mcp_if_dirty` and reads the selected connection's tool catalog ([codex_thread.rs L1167-L1183](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/codex_thread.rs#L1167-L1183)). At worst it sends one read-only tool listing to the TUI server, whose handler is purely local ([list_tools](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/dynamic_tools_mcp.rs#L253-L299)).
- It does not resume, subscribe or start a turn.
- It is serialized under `global("mcp-registry")`, so it can briefly wait behind other MCP registry operations.
- Never omit `serverName`: without it the server runs full-inventory discovery and may connect to other configured, possibly remote, servers.
- Parse only `name`, `httpOrigin` and `runtimeStatus`; discard tools, metadata and capabilities.

**Verdict A3: viable-needs-probe.** The source supports the field, its per-thread scope and the resume behavior, and the installed binary has the schema fields. A native run still has to confirm the actual `codex_tui` value on shared threads, that it stays the same across reconnect, and that the read has no visible effect on the TUI.

Failure modes and what makes the binder fail closed:

1. **Rejoin of a subscribed loaded thread (probe 010 case b).** The address stays with the original loader, and there is no hook and no connection pair. If that original TUI later switches away without creating or cold-loading another thread, the binding goes stale and nothing observable marks it. This is the remaining gap; the mitigations below reduce it but do not remove it.
2. **TUI exit or crash.** The listener disappears, but `runtimeStatus` may lag. Require a live listener owner and an established backend connection; otherwise report unverified.
3. **Port reuse.** Require all of the following: the owner is a Codex executable whose command line is not `app-server`; it was created before the thread's `createdAt` (from `thread/read`); and a backend-owned connection to the port exists, from the backend PID verified through hook ancestry or optionally `server/diagnostics`.
4. **PID reuse.** Compare creation times when binding and again before every action, using the existing `ProcessProbe` identities.
5. **No server, so `httpOrigin` is missing.** This covers IDE, app and remote-workspace clients, managed-requirement denial, listener start failure, and a user-owned `codex_tui` server (whose owner process will not be a TUI). Report unverified and take no host action.
6. **Delegated background threads.** They get bound to a TUI that does not display them. A probe needs to find something in thread metadata that tells them apart (`source`, `threadSource`, a parent link); otherwise treat a second binding on the same TUI as ambiguous.
7. **Several threads bound to one TUI.** Keep only the latest hook-time binding per TUI process actionable and demote older ones to unverified. Two TUIs can never both own the same thread, because the address has a single value.
8. **Subagents.** The hook `session_id` is the root identity ([analysis-004](analysis-004.md)), so query the root thread.
9. **Version drift.** Gate on the observed provider version and on the schema field being present.

Minimal native probe for A3. Reuse the isolated 010 harness, settings and fail-closed proxy, with two or three non-elevated private TUIs in one directory.

1. Run these steps in order: A and B baseline; A runs `/new`; A uses `/resume` to open B's thread while B still has it open (case b); kill B; C cold-resumes B's thread (case c); A forks a thread; restart the backend once and let the TUIs reconnect.
2. After each step, for every root thread from the hook captures, make the single read-only call above through the existing `app-server proxy` socket path. Keep only the server name, the port and `runtimeStatus`.
3. Map each port to its owner PID and creation time with an OS listener query, and confirm a backend-owned ESTABLISHED connection to it.
4. Expected results:
   - A and B baseline threads each show their own TUI's port.
   - `/new` shows A.
   - Case (b) keeps B.
   - After B dies there is no listener, so the result is unverified.
   - Case (c) shows C.
   - The fork shows A.
   - Each TUI keeps the same port across the reconnect.
5. Capture the TUI screens before and after each read to show nothing changed. Confirm no extra hook capture and no turn.
6. Record whether a TUI unsubscribes from the old thread when it switches threads. This decides how far failure mode 1 reaches.
7. Record as not performed: the delegated-thread case (failure mode 6), which cannot run without a model.

## B. Sole-client exclusion binder

Source facts:

- Implicit TUIs attach over the daemon's Unix-domain control socket ([lib.rs L535-L560](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/lib.rs#L535-L560), [L1056-L1092](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/lib.rs#L1056-L1092)).
- Only TUIs that started the MCP server have the TCP listener (A1). Non-TUI clients (IDE and desktop clients, explicit `--remote`, `app-server proxy`) use the same socket and have no listener.
- Embedded TUIs also run the listener, for worktree tools. So the number of listeners is not the number of daemon clients.
- Headless `exec` uses its own in-process server ([analysis-005](analysis-005.md)) and never attaches to the daemon.
- On Windows I found no supported way for a third process to list another process's AF_UNIX peers with their PIDs (not tested).
- Even a complete list would race with attach, reconnect and rejoins of loaded threads, and those rejoins fire no hook.

**Verdict B: not viable** as a binder. From outside the backend on Windows, "exactly one TUI and no other clients" cannot be established, and the condition changes without any hook. It can only add fail-closed corroboration, for example refusing a binding when more than one live TUI listener has backend connections and A3 is unavailable. No probe is recommended.

## C. Supported shared-daemon opt-out

Source facts:

- `[features] daemon_auto_start = false` in `config.toml` is a supported Stable feature key, default `true` ([features lib.rs L122-L123](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/features/src/lib.rs#L122-L123), [L1068-L1073](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/features/src/lib.rs#L1068-L1073); the key is also in the tagged `core/config.schema.json`).
- It only turns off auto-start ([startup_orchestration.rs L548-L552](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L548-L552)). Finding and reusing an already running daemon on the default socket happens regardless of this key ([L342-L356](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L342-L356), [lib.rs L1083](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/lib.rs#L1083)).
- With the key set, a launch runs embedded (`auto_start_disabled`) only when no daemon is running. If one is running, it attaches (`existing_daemon`) ([L710-L717](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L710-L717)).
- Every way to force embedded mode is per invocation or unsuitable: the `--no-daemon` flag, other CLI-only exclusions (`--oss`, `--profile`, `-c` overrides other than features, `--strict-config`, hook-trust bypass), and the `CODEX_EXEC_SERVER_URL` variable, which selects a different executor and so is not an opt-out ([daemon_startup.rs L61-L88](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/daemon_startup.rs#L61-L88), [daemon README L22-L28](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-daemon/README.md#L22-L28)).
- Elevated Windows launches are always embedded ([L27-L33](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L27-L33)).
- I found no config key or daemon `settings.json` field that forces embedded mode while a daemon exists. The daemon settings cover only remote control, the shutdown grace period and the updater ([README L48-L57](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-daemon/README.md#L48-L57)).

What users lose in embedded mode (from source):

- No task or delegation tools (they require a server that is not in-process; see A1).
- No agents overview of the shared daemon.
- Work ends when the terminal closes instead of continuing in the backend.
- No sharing or rejoining of threads across clients.
- No remote control, which needs the daemon.

Whether the IDE extension or the desktop app starts or uses the same daemon cannot be determined from source (closed clients); unverified.

**Verdict C: viable-verified-by-source, but only as a partial opt-in.** It cannot guarantee standalone mode, so it is not a binder. If setup offers it:

- Treat it as an explicit, reversible provider-settings edit with ownership and a backup.
- Report "shared daemon still in use" whenever a daemon is detected.
- Use the direct-process binder only for launches actually observed to be embedded (task 1.1 classification).

Minimal probe, in the isolated home with the key set:

1. Launch with no daemon running and confirm the hook ancestry ends at the TUI, which proves embedded mode.
2. Start a daemon with a separate supported command, launch again, and confirm the TUI attaches to it.

## D. Host-side correlation

Titles:

- The default title items are `activity`, `thread-name` and `project-name` ([status_surfaces.rs L29-L30](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/chatwidget/status_surfaces.rs#L29-L30)).
- A `thread-id` item exists, but the user must configure it, and it is cut to 32 characters, shorter than a UUID ([title_setup.rs L82-L83](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/bottom_pane/title_setup.rs#L82-L83), [L963-L965](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/chatwidget/status_surfaces.rs#L963-L965)).
- So the default title has no stable thread identifier. An opt-in title change would alter user configuration and still not give an exact identifier. Thread names are user data and can repeat.
- Verdict for titles as a binder: **not viable**. They can only veto: for example, a Windows Terminal tab title that contradicts the expected thread name blocks an action. They are never positive evidence.

VS Code bridge and Windows Terminal:

- Once A3 gives a verified TUI process, the existing direct path completes the join. For VS Code that is the TUI's parent shell plus the fresh bridge registration (`vscode/extension.js:58-71`). For Windows Terminal it is the Windows Terminal ancestor and window checks (`src/BellTower/Hosts/WindowsTerminalNavigator.cs:106-126`), at the current window-only precision unless presentation ownership is verified.
- Shell integration sees shell command lines, such as an explicit `codex resume <id>`. Those go stale after an in-client switch, so they are not identity.
- In shared mode the host variables visible at hook time come from the backend's inherited environment, which is client A's terminal (probes 009/010). Never use them.

**Verdict D: viable-needs-probe, but only as the final hop after A3.** It inherits A3's probe plus the native host acceptance in task 6.1.

## E. Other source links between a thread and a client process

- The rollout writer, state database rows, thread-scoped log rows and hook processes all belong to the backend (accepted facts; [analysis-004](analysis-004.md)).
- Clients write stored attachments only for worktree and pull-request use.
- A TUI command line names a thread only for explicit resume launches and does not follow `/new` or `/resume`.
- The `--worktree` binding writes a worktree attachment, not a client identity.
- The IDE-context IPC and terminal-status channels carry no conversation ID ([analysis-004](analysis-004.md)).

**Verdict E: not viable.** Nothing here beats A3.

## Findings

### ANALYSIS-500: An inherited environment flag alone makes shared-mode Codex hooks "dedicated", which binds them to the backend launcher's terminal

- Severity: warning (unlikely to trigger, high impact when it does).
- Related task / requirement: tasks 1.1, 2.2 and 4.2; design decision 1 ("Never promote a captured `BELLTOWER_*` flag ... without verifying that it belongs to the current client generation").
- Locations: `src/BellTower.Ingress/Program.cs:32-44`, `src/BellTower/Providers/HookIngestor.cs:15-27`, `src/BellTower/Hosts/AttachmentCapture.cs:10-16,22-23,30-31` at `a69392e`.
- Observation:
  - Codex counts as dedicated whenever the hook process sees `BELLTOWER_DEDICATED=1`.
  - `AttachmentCapture` then takes the first `codex.exe` ancestor as the agent and the next shell as the shell. It reads the terminal kind, `WT_SESSION` and `BELLTOWER_ATTACHMENT` from the hook's environment.
  - In shared mode, the hook's `codex.exe` ancestor is the backend (`app-server --managed-daemon`). The shell after it is the first launcher's shell, and the environment is the first launcher's.
  - The module and launcher set the flag only for `--no-daemon` launches (`BellTower.psm1:46-58`, `AgentLauncher.cs:31`). However, any child process of a managed session inherits the flag and the attachment ID, for example a nested `codex` started from an agent or user command.
  - If such a child starts the shared daemon, every later shared conversation from any terminal is attributed to the managed session's attachment and shell. Navigation, presentation and palette could then target the wrong terminal.
- Proposed improvement and benefit: when classifying a capture as dedicated, require process evidence as well as the flag:
  - The agent ancestor must be an interactive client, not an `app-server` daemon.
  - For a managed attachment, the agent must match the generation recorded at launch. Unmanaged IDs already encode PID plus start ticks; managed ones need the recorded agent identity.
  - On any mismatch, fall back to the existing shared-runtime branch.

  This closes a cross-terminal misattribution path without changing the managed embedded flow.
- Behavior-preservation risks: managed embedded sessions must stay dedicated (the TUI hosts the hooks in-process), as must owned-runtime launches. Claude is unaffected. Any command-line check must be bounded and tolerate failure within the ingress latency budget, or move to the consumer.
- Evidence type: source observation. The inheritance path is a reasoned scenario and was not reproduced.
- Verification / measurement plan:
  - Unit tests with fake ancestry [hook shell, `codex.exe app-server`, `codex.exe` TUI, `pwsh`] and `BELLTOWER_DEDICATED=1`, in both the ingress and the legacy ingestion path. Expect no process or host fields and `shared-runtime` ownership.
  - A paired managed embedded case should still be dedicated.
  - Keep the Claude path covered and unchanged, per parity rules.
- Relationship: new. It overlaps the planned task 2.2 check "inherited backend flags cannot transfer another terminal's ownership", and triage may fold it into that task.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| Not applicable | Source-only investigation | No correction assigned | No closure claimed. |

## Assessment and limitations

### Ranked recommendation for task 1.3

1. **Primary candidate: the MCP-address binder (A3), gated on the A3 probe.**
   - Bind only at hook time: SessionStart for startup, resume or fork, when the thread has just been loaded by the TUI whose address it carries.
   - Chain: hook `session_id` -> `mcpServerStatus/list` (`threadId`, `serverName: codex_tui`, `toolsAndAuthOnly`) -> loopback port -> OS listener owner, which must be a Codex TUI (not `app-server`) created before the thread, plus a backend-owned connection to that port -> the TUI's parent shell -> the existing VS Code or Windows Terminal registration.
   - Keep only the latest binding per TUI actionable. Recheck the owner, creation times and backend connection before every action. Treat any missing link as unverified while keeping observation working.
   - Show failure mode 1 (a stale binding after a rejoin of a subscribed thread followed by a switch away) in diagnostics. Decide with the user whether that remaining gap meets the full-support requirement or needs an extra veto, such as the title consistency check.
   - It uses only supported protocol and OS facts: no wrappers, input injection or name writes.
2. **Optional opt-in: `features.daemon_auto_start = false`**, offered as setup guidance and labelled as not guaranteeing standalone mode, with the side effects listed in section C. It raises the share of launches covered by the already planned direct-process path; it is not a shared-mode binder.
3. **Host title consistency (D) as a veto only**, layered on option 1 and never used as positive identity.
4. **Rejected:** sole-client exclusion (B); log, rollout and attachment links (E); hook ancestry and inherited environment (accepted 009/010 facts).

If the A3 probe fails (for example the address is missing or not per-thread in the installed binary), the precise evidence gap stands. This report establishes no hard provider restriction. Wrappers or a reduced terminal surface would still need a user scope decision.

### Source facts versus hypotheses

- **Source facts** (tagged source; installed schema cross-checked where stated):
  - The TUI listener is its task-tools MCP server.
  - Its address is injected per thread.
  - `httpOrigin` is exposed per thread with credentials excluded.
  - Resuming a loaded thread replaces its config only when it has no subscribers.
  - Delegated threads carry the TUI's address.
  - The default unload delay is 1800 s.
  - `daemon_auto_start` behaves as described in section C.
  - Title defaults and truncation are as described in section D.
- **Behavior hypotheses** (need the probe):
  - The installed binary returns the `codex_tui` address for native shared threads.
  - The read causes no visible TUI effect and at most one local tool listing.
  - The port stays stable across backend reconnect.
  - How a TUI switching threads affects its subscriptions.
  - Whether thread metadata can tell delegated threads apart.
- **Performance hypotheses** (not measured):
  - One bounded JSON-RPC call plus one OS TCP-table read per adoption, run in the consumer outside ingress, should not affect hook latency budgets.
  - `global("mcp-registry")` serialization may add waiting during MCP reloads, so use a short timeout with an unverified fallback.
  - Verify with the probe timings and the task 6.3 latency budgets.

### Limitations

- The client refused my one attempt to write this report to `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-006.md`. I did not retry, per the role rules; the orchestrator needs to save this text there.
- Source and documentation reading only. I performed no provider, protocol connection, socket query, configuration or host operation.
- How the IDE extension and the desktop app use the daemon is unverified (closed source). Windows AF_UNIX peer enumeration was not tested.
- The avenues concern only the shared-Codex surface; the Claude path is unaffected. ANALYSIS-500's verification includes the unchanged Claude path for parity.
- I did not use the Context7 documentation workflow because the tagged primary source was available and more specific.
- ANALYSIS-501 through ANALYSIS-599 are unused. The orchestrator owns triage, ledger status and all next-step authorization.
