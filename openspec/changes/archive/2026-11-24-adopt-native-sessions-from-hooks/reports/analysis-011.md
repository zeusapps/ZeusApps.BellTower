<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-011 / code-analyzer; Client: Claude Code delegated subagent / source-only; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path ("Subagents should return findings as text", as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / 011

## Review target

- Reviewer / role: ANALYSIS-011 / code-analyzer.
- Client / version / mode / model (resolved ID): Claude Code delegated subagent built on the Claude Agent SDK (client version not exposed to the agent) / source-only and documentation-only / `claude-opus-5-5`.
- Date: 2026-11-04 (UTC).
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-011. The question: could an optional Bell Tower stdio MCP server, configured in Claude Code and Codex, give a useful per-conversation liveness or identity signal for task 2.3?
- Task and requirement references: [tasks.md](../tasks.md) 2.3 (plus 4.2 and 5.1 where relevant); [design.md](../design.md) decisions 2, 3 and 7; [analysis-006](analysis-006.md) A3; [native-adoption-010](../evidence/native-adoption-010.md); [native-adoption-011](../evidence/native-adoption-011.md).
- Scope and relevant neighboring paths: no implementation target. I read these repository files for comparison only: `src/BellTower/Providers/AgentModeClassifier.cs` and the `AttachmentHealth` users under `src/BellTower/Core`.
- Primary sources:
  - Codex: the official tagged source archive `openai/codex` at `rust-v0.165.0`, extracted into a disposable scratch directory outside the repository and read with text tools only. Nothing was built or run. Links below point to the tag.
  - Claude Code: the official documentation at `code.claude.com`, fetched through Context7 (`/websites/code_claude`) and one direct fetch of the MCP page.
- Baseline commit: `b6c0b452025f27a5e3ee4841689affde683cdda2`.
- Implementation identity (`./scripts/review-identity.ps1 -Baseline b6c0b45 -Paths src/BellTower/Providers src/BellTower/Hosts src/BellTower/Sessions src/BellTower.Ingress`), before:

```text
HEAD: b6c0b452025f27a5e3ee4841689affde683cdda2
Baseline: b6c0b452025f27a5e3ee4841689affde683cdda2
Paths: src/BellTower.Ingress src/BellTower/Hosts src/BellTower/Providers src/BellTower/Sessions
Manifest (A added, M modified, D deleted, ? untracked):
Tracked diff SHA-256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
Untracked file SHA-256:
Summary: head=b6c0b45 baseline=b6c0b45 files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc
```

- Before/after identity: the same command after the analysis printed the same summary line (`head=b6c0b45 baseline=b6c0b45 files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc`). No unexpected mutation.
- Prior reports / rechecked findings: none rechecked. This is a new investigation that builds on the accepted facts in analysis-006 and probes 010 and 011.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Codex stdio MCP spawn owner, granularity and laziness | Read the tagged `rmcp-client`, `codex-mcp`, `core` session and app-server thread lifecycle | Section 1 | Source fact: the process that hosts the app server spawns the MCP server, once per loaded thread, eagerly when the thread's MCP runtime is published. |
| Codex stdio MCP exit | Read the session shutdown, thread unload and Windows job sources | Section 1.4 | Source fact: the server exits when the thread unloads or when the spawning process exits. TUI exit alone does not stop it in shared mode. |
| Codex environment and identification | Read the MCP environment builder, the initialize params and the tool-call metadata | Section 1.5 | Source fact: the environment is an allowlist taken from the spawning process. Nothing identifies the thread or client except `_meta.threadId` on `tools/call`. |
| Codex embedded and `--no-daemon` modes | Read the TUI target selection and the in-process start | Section 1.6 | Source fact: the same per-thread rules apply, but inside the TUI process. |
| Claude Code MCP lifecycle and identity | Official docs (env-vars, mcp, features-overview) | Section 2 | Documented: one server set per Claude Code process, stopped at session end, not restarted on `/clear`. The session ID can be stale. |
| Comparison with existing evidence | Design decisions 2, 3 and 7; probes 010 and 011 | Section 3 | The MCP signal is equivalent to or weaker than evidence Bell Tower already has. |
| Costs | Docs and source for tool deferral, trust and process count | Section 3.3 | Low context cost; non-trivial setup, trust and process cost. |
| Build/tests/format | Not run | No implementation target | Not applicable. |
| Provider launches, protocol connections, settings edits | Not performed | Source-only assignment; no Codex quota | Behavior is source-derived, not probed. |

## 1. Codex 0.165.0

### 1.1 Which process spawns a user-configured stdio MCP server

Short answer: the process that runs the app server. In shared mode that is the managed backend (`app-server --managed-daemon`), never the TUI.

- `LocalStdioServerLauncher` "starts the configured command as a child of the orchestrator process" ([stdio_server_launcher.rs L1-L13](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/stdio_server_launcher.rs#L1-L13), [L178-L196](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/stdio_server_launcher.rs#L178-L196)).
- The local launcher is chosen for every local-environment stdio server. The executor launcher is used only for remote environments ([rmcp_client.rs L1354-L1369](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/rmcp_client.rs#L1354-L1369)).
- The orchestrator is the `core` session inside the app server that owns the thread. In shared mode that is the backend. Per analysis-006 A1 and analysis-004, a daemon-attached TUI is only a protocol client and runs no thread runtime of its own.
- The TUI does run one MCP-related server: its own `codex_tui` HTTP listener. It is a server that the backend connects to, not a user stdio server (analysis-006 A1).

### 1.2 Granularity: one per thread

- `McpRuntime` "Owns all mutable MCP state for one Codex thread" ([runtime.rs L109-L112](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/runtime.rs#L109-L112)). The module header calls it "the thread-owned MCP runtime" ([L1-L7](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/runtime.rs#L1-L7)).
- Connection reuse is limited to the same runtime's previous publication (`previous` / `reusable_previous`; [connection_manager.rs L343-L367](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/connection_manager.rs#L343-L367), [L642-L652](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/connection_manager.rs#L642-L652)). I found no pool of stdio processes shared across threads.
- So a shared backend with N loaded threads that have MCP enabled runs N copies of a configured stdio server. This includes threads that are retained but idle, forks, and subagent threads (each subagent has its own session). Threads marked `McpServersDisabled` get none ([core/src/mcp.rs L265-L284](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/mcp.rs#L265-L284)).
- Whether the ephemeral title threads observed in probe 011 instantiate MCP servers was not traced. They are started as prediction-mode forks ([thread_processor.rs L5315-L5340](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L5315-L5340)). Unverified.
- Each `thread/start` reloads the config layers through `load_with_overrides` ([thread_processor.rs L1398-L1401](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L1398-L1401)). A server newly added to `config.toml` should therefore reach new threads on a running backend. This is a source reading; it was not probed.

### 1.3 Laziness: eager when the thread loads

- Startup policy: `Eager` for ordinary threads, `LazyWhenCached` for `SessionSource::SubAgent` ([core/src/session/mcp_runtime.rs L370-L374](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mcp_runtime.rs#L370-L374); policy docs at [runtime.rs L67-L74](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/runtime.rs#L67-L74)).
- Deferred startup applies only when a previous connection set exists ([connection_manager.rs L345-L347](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/connection_manager.rs#L345-L347)).
- The initial runtime is installed during session initialization (`install_initial_mcp_runtime`, [mcp_runtime.rs L98-L133](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mcp_runtime.rs#L98-L133)). So the server starts when the thread is created or loaded (`thread/start`, `thread/resume` cold load, `thread/fork`), not at the first tool listing or turn.
- Consequence: the spawn precedes the first `SessionStart` hook. Probe 011 saw that hook at first turn submission. An idle new thread has an MCP process but no hook.
- A republish happens when the cwd, workspace roots, approval policy, reviewer, permission profile or Windows sandbox level change ([mcp_runtime.rs L36-L48](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mcp_runtime.rs#L36-L48)). It reuses a connection only when the connection identity matches, so such a change may restart the process mid-conversation. Hypothesis: I did not trace which inputs alter the identity.

### 1.4 Exit

- Session shutdown calls `mcp_runtime.shutdown()` ([core/src/session/handlers.rs L328-L333](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/handlers.rs#L328-L333), [runtime.rs L890-L893](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/runtime.rs#L890-L893)). Dropping the process handle also terminates the process ([stdio_server_launcher.rs L510-L545](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/stdio_server_launcher.rs#L510-L545)).
- On Windows the child is placed in a job created with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` ([stdio_server_launcher.rs L297-L307](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/stdio_server_launcher.rs#L297-L307), [utils/pty/src/win/job.rs L63-L65](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/utils/pty/src/win/job.rs#L63-L65)). The server therefore dies when the spawning process exits, unless job assignment failed and the fallback path was taken (L325-L343).
- A thread unloads only when it has had no subscribers and has been inactive for `thread_unload_delay` ([thread_lifecycle.rs L58-L65](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_lifecycle.rs#L58-L65)). Even then, a still-running agent postpones it ([L398-L417](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_lifecycle.rs#L398-L417)). Unload shuts down the thread and emits `thread/closed` ([L559-L620](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_lifecycle.rs#L559-L620)).
- The delay defaults to 1800 s ([core/src/config/mod.rs L4100-L4101](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/config/mod.rs#L4100-L4101)). It can be set as the `config.toml` key `thread_unload_delay_secs` or as a per-thread override.
- A closed client connection removes that connection's subscriptions ([app-server/src/thread_state.rs L651-L663](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/thread_state.rs#L651-L663)). Probe 011 also showed that a TUI unsubscribes on `/new` and on `/resume` away.

Shared mode, summarized:

| Event | Bell Tower MCP process for that thread |
| --- | --- |
| TUI exit or crash while thread idle | Stays alive up to 1800 s more (default), then exits on unload |
| TUI exit while a turn is running | Stays alive until the work ends, then up to 1800 s more |
| TUI `/new` or `/resume` away | Same as exit (the old thread loses its subscriber) |
| Another client still subscribed (case b) | Stays alive indefinitely |
| Backend exit or restart | Exits (job closed); the new backend spawns one again only for threads it reloads |
| `thread/read` by an observer of an unloaded thread | No spawn (probe 011: "thread not found", not loaded) |

### 1.5 Environment, arguments and identification

- Arguments: exactly the configured `command` and `args`. The cwd is the configured `cwd`, otherwise the thread's local process cwd ([stdio_server_launcher.rs L263-L292](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/stdio_server_launcher.rs#L263-L292); fallback set from `runtime_context.local_process_cwd()` at [rmcp_client.rs L1358-L1360](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/rmcp_client.rs#L1358-L1360)). A directory is not a conversation identity.
- Environment ([rmcp-client/src/utils.rs L16-L59](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rmcp-client/src/utils.rs#L16-L59)): an allowlist read from the spawning process's own environment. On Windows the list is `WINDOWS_CORE_ENV_VARS` (PATH, profile and temp roots and similar; [protocol/src/shell_environment.rs L184-L214](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/protocol/src/shell_environment.rs#L184-L214)). On top of that come the variables named in the server's `env_vars`, the literal `env` values from config, and the custom CA keys. Non-inheritable secrets are stripped.
- `CODEX_THREAD_ID` and `CODEX_SESSION_ID` exist, but they are injected for shell and unified-exec tool children, not MCP servers ([core/src/exec_env.rs](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/exec_env.rs); the MCP environment builder above never sets them).
- In shared mode the spawning process is the backend. Any forwarded variable, `WT_SESSION` or a `BELLTOWER_*` flag listed in `env_vars` included, therefore carries the backend launcher's values. That is the same misattribution class as ANALYSIS-500 and probes 009/010.
- `initialize` sends `clientInfo` `codex-mcp-client` with the Codex version, elicitation capability and optional extensions. It sends no roots capability and no thread, session or client identifier ([rmcp_client.rs L1208-L1237](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/codex-mcp/src/rmcp_client.rs#L1208-L1237)).
- The only thread identity a server receives is `_meta.threadId` and `_meta.sessionId` on each `tools/call` ([core/src/mcp_tool_call.rs L543-L555](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/mcp_tool_call.rs#L543-L555), [L1424-L1440](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/mcp_tool_call.rs#L1424-L1440)). That happens only when the model decides to call one of the server's tools. A passive server never learns which thread it serves, and never learns which client is attached.
- The parent PID is the backend for every thread, so it does not tell threads apart.

### 1.6 Embedded and standalone (`--no-daemon`) mode

- Embedded mode starts the app server in-process inside the TUI (`start_embedded_app_server` producing `AppServerClient::InProcess`; [tui/src/lib.rs L587-L631](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/lib.rs#L587-L631)). `--no-daemon`, the CLI-only exclusions and elevated Windows launches all select it (analysis-006 C). Headless `exec` is similar, with its own in-process server (analysis-005).
- The spawn owner is therefore the TUI (or `exec`) process. The same per-thread, eager and unload rules apply inside it.
- On TUI exit the job closes and every MCP child dies, so lifetime is bounded by the client process.
- A TUI that ran `/new` still keeps the old thread's server for up to the unload delay. So even here "MCP process alive" means "client process alive and thread loaded", not "this thread is displayed".
- Bell Tower already has the client process directly (PID plus creation time, task 1.1). The MCP process adds nothing.

## 2. Claude Code (official documentation)

- Ownership and lifetime:
  - Stdio servers are subprocesses of the Claude Code session ([env-vars](https://code.claude.com/docs/en/env-vars)). They connect at session start ([features-overview](https://code.claude.com/docs/en/features-overview)).
  - When Claude Code disconnects a stdio server, "for example when your session ends", it signals the server and then forces it to stop ([mcp](https://code.claude.com/docs/en/mcp)). On Windows it closes stdin and ends the process tree after 300 ms; before v2.1.296 it ended the tree at once.
- Exit mid-session:
  - In an interactive session the server is marked failed and is restarted only through `/mcp` Reconnect.
  - In `claude -p` and Agent SDK sessions it is restarted on the next tool call ([mcp](https://code.claude.com/docs/en/mcp), "Stdio servers that exit or fail to start").
  - So a crashed or slow-starting Bell Tower server would read as "dead" while the interactive session is alive.
- `CLAUDE_CODE_SESSION_ID` (confirmed, [env-vars](https://code.claude.com/docs/en/env-vars)):
  - "An MCP server subprocess retains the ID it was spawned with."
  - On `--resume <id>` it receives the resumed ID.
  - On `--continue` or `--resume` without an ID "it may receive the initial startup ID instead".
  - Hooks and Bash see the updated ID after `/clear`; MCP servers do not. This also confirms the servers are not restarted on `/clear`.
- Other identification:
  - `CLAUDE_PROJECT_DIR` is the stable project root ([mcp](https://code.claude.com/docs/en/mcp); [2026-w20](https://code.claude.com/docs/en/whats-new/2026-w20)).
  - `roots/list` answers with the launch directory plus added directories. Each request carries client capabilities in `_meta`.
  - None of these is a conversation identity.
- Trust and setup:
  - Project-scope `.mcp.json` servers need interactive approval, and an untrusted folder cannot approve its own.
  - User scope lives in `~/.claude.json` ([mcp](https://code.claude.com/docs/en/mcp)).
- Context cost: tool search is on by default. Only tool names and server instructions load at session start ([features-overview](https://code.claude.com/docs/en/features-overview), [mcp](https://code.claude.com/docs/en/mcp)).
- Not documented in what I read: subagent-specific server instances and in-session `/resume` switching. I treat both as unverified. The env-vars text implies `/clear` reuses the same server.

Result: a Claude MCP server lives at most as long as the `claude` process, which Bell Tower already verifies directly (PID plus creation time, direct-process binding in decision 3). Its session ID is weaker than the hook `session_id`, which follows `/clear`.

## 3. Assessment against task 2.3

### 3.1 What the MCP process would tell Bell Tower

| Provider / mode | MCP process alive means | Terminal-disconnect signal? | Continuing-work signal? | Conversation identity? |
| --- | --- | --- | --- | --- |
| Codex shared | Thread loaded in the backend: any subscriber, or active, or within the unload delay | No. It survives TUI exit by up to 1800 s by default, and indefinitely while another client is subscribed | No. Idle retained threads keep it alive, the same as `thread/loaded/list` | No, except `_meta.threadId` on model-initiated tool calls. Parent and env name the backend and its launcher |
| Codex embedded / `--no-daemon` / exec | Client process alive and thread loaded | Only as a proxy for the client process, which Bell Tower already checks | No | No (same as above) |
| Claude Code (all modes) | `claude` process alive and server not failed | Only as a proxy for the `claude` process; false "dead" after a server crash in interactive mode | No | Weak. `CLAUDE_CODE_SESSION_ID` is stale after `/clear`, and possibly after `--continue` or `--resume` without an ID |

Comparison with existing evidence:

- **Hooks:** give the exact conversation ID per event, including after `/clear` (Claude) and for every root thread (Codex). They are the only exact per-conversation identity in either provider. The MCP server receives nothing comparable without a model tool call.
- **PID plus creation checks:** for Claude and embedded Codex these already give exact client-process liveness. The MCP child is a strictly derived, lossier copy of it.
- **`thread/read` status and `thread/loaded/list` over the non-owning transport (decision 4):**
  - In shared Codex these already give the backend's view: Active versus idle per exact thread, and loaded or not.
  - MCP process presence is equivalent to "loaded", which decision 2 already classifies as discovery-only.
  - It is delayed relative to `thread/closed` and the loaded list.
  - It costs one process per thread instead of one bounded read.
- **The `codex_tui` endpoint (decision 3):**
  - This is the terminal-side signal for shared Codex. The listener belongs to the interactive client process and disappears when that client exits. Probe 011 step 4 found no LISTEN owner and no ESTABLISHED peer immediately after the kill, while `runtimeStatus` still said `connected`.
  - A Bell Tower MCP server cannot reproduce it, because it lives on the backend side.

Conclusion: a Bell Tower stdio MCP server gives no reliable per-conversation liveness or identity signal in any mode of either provider.

- In shared Codex it measures backend thread retention, which design decision 2 already rejects as evidence of an open terminal or active work.
- In direct modes it duplicates the client process check with extra failure modes.
- The only per-conversation datum, `_meta.threadId` or `sessionId`, depends on the model choosing to call a Bell Tower tool. That cannot be a liveness basis, and steering the model to call it would spend tokens and model turns. That runs against the Non-Goals (no prompts, no changing model behavior).

### 3.2 Failure cases if it were adopted anyway

1. **Shared Codex.** A TUI closes while its thread is idle, and the MCP process stays alive for 30 minutes, so the terminal reads as live. Lowering `thread_unload_delay_secs` would change user backend behavior: warm rejoin goes away. It needs a provider settings edit, which the user has declined for `daemon_auto_start` (design decision 3).
2. **Shared Codex, rejoin.** A thread joined by a second client keeps its MCP process however many terminals close, until the last subscriber leaves.
3. **Concurrent creations.** Correlating an MCP spawn with a new thread by time is the heuristic that probe 010 rejected. Spawn also precedes the first hook.
4. **Backend environment.** Forwarded variables come from the backend launcher (ANALYSIS-500 class).
5. **Republish.** cwd or permission changes may restart the server mid-conversation (hypothesis, section 1.3), which would produce spurious exit and start events.
6. **Windows job fallback.** If job assignment fails, the MCP child might outlive the backend. Source fallback path; not probed.
7. **Claude interactive.** A server crash or startup timeout leaves the server failed without auto-restart, so the session would read as dead.
8. **Claude identity.** A stale `CLAUDE_CODE_SESSION_ID` after `/clear`, and possibly after `--continue`, would attribute the process to the wrong conversation.
9. **Version drift.** The lifecycle is internal behavior, not a documented contract, for Codex in particular. Decision 7 runtime checks would need yet another self-check.

### 3.3 Costs

- **Context tokens:** small in both providers.
  - Claude: tool search is on by default; only names and instructions are loaded.
  - Codex 0.165.0: "MCP tools are always deferred when tool_search is available" ([features/src/lib.rs L276-L279](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/features/src/lib.rs#L276-L279)).
  - A tool-less server would cost almost nothing, but would then carry only the process-existence signal.
- **Trust and setup:**
  - The server needs a new owned entry in each provider's user configuration (`~/.claude.json` user scope; Codex `config.toml` `mcp_servers`) through the setup editor, with backup and removal.
  - Project scope would need approval prompts or project trust.
  - It must never be marked `required`, because a required server's failure blocks the thread ([mcp_runtime.rs L133](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mcp_runtime.rs#L133)).
  - Parity: both providers need the setup, diagnostics and tests.
- **Processes:**
  - Claude: one per `claude` process.
  - Codex: one per loaded thread. That includes retained idle threads for up to 30 minutes after the last subscriber, forks and subagents, all inside the backend's job (shared) or the TUI (embedded).
  - Each process must stay lightweight and must not contact the tray synchronously during startup, or it delays thread startup within the 30 s startup budget for both providers.

### 3.4 Recommendation

**Reject for 2.3.** Keep it at most as an unscheduled idea, and only if a future, documented provider contract delivers conversation identity or client attachment to MCP servers; that is not the case today. Task 2.3 should rely on the evidence that design decisions 2 and 3 already select, per provider and mode:

- **Direct Claude and embedded or standalone Codex:** client process PID plus creation time for terminal disconnect, and hooks plus the existing idle rules for work.
- **Shared Codex:**
  - Terminal-side liveness comes from the `codex_tui` listener owner and the backend ESTABLISHED connection to it, rechecked at evaluation time.
  - Conversation work comes from the exact-thread `thread/read` status (Active versus idle) over the non-owning transport, plus hook events.
  - `thread/loaded/list` and `thread/closed` serve only as discovery and retention evidence.

## Findings

### ANALYSIS-1000: A Bell Tower stdio MCP server is not a valid 2.3 liveness or identity source for either provider

- Severity: suggestion (design input; no code exists).
- Related task / requirement: task 2.3; design decisions 2, 3 and 7.
- Locations: no repository location. Provider source as cited in sections 1.1 to 1.6, and the Claude docs in section 2.
- Observation:
  - Shared Codex: the MCP process is a per-thread child of the backend, created at thread load and killed at thread unload (default 1800 s after the last subscriber leaves and the thread goes idle) or at backend exit. It receives the backend launcher's allowlisted environment and no thread or client identity except `_meta.threadId` on model-initiated tool calls.
  - Embedded Codex and Claude Code: the server's lifetime is bounded by the client process that Bell Tower already verifies. Claude's `CLAUDE_CODE_SESSION_ID` is stale after `/clear` and possibly after `--continue`.
- Proposed improvement and benefit: do not add an MCP server to the 2.3 scope or to setup. Record in design decision 2 (or in the 2.3 evidence) that MCP-process presence is equivalent to "thread loaded" and was evaluated and rejected. This keeps a later reviewer from reopening it and avoids per-thread processes, provider settings edits and parity work that bring no new evidence.
- Behavior-preservation risks: none; nothing is implemented. This finding adds no provider exception and no restriction claim. It assesses a candidate evidence source only.
- Evidence type: source observation (tagged Codex source) and primary documentation (Claude). Nothing was probed natively.
- Verification / measurement plan: if the orchestrator wants native confirmation, run one isolated, no-quota shared-mode probe in the 011 harness with a dummy stdio server that logs its start, its stdin EOF and its PID and parent:
  1. A TUI creates a thread and exits.
  2. Check that the MCP process outlives the TUI and exits only after the unload delay, using a short `thread_unload_delay_secs` in the private home.
  3. Check that its parent is the backend.
  4. Check that `initialize` carries no thread ID.

  Not required for the recommendation.
- Relationship: new. Complements analysis-006 A3 and probe 011. Overlaps the decision 2 statement that loaded threads are discovery-only.

### ANALYSIS-1001: Name the shared-Codex terminal-disconnect signal for 2.3 explicitly as the `codex_tui` listener check

- Severity: suggestion.
- Related task / requirement: task 2.3 ("terminal disconnect versus independently verified continuing conversation work"); design decisions 2 and 3.
- Locations: `openspec/changes/adopt-native-sessions-from-hooks/design.md`, decision 2 (paragraph two) and decision 3 (step 5), at `b6c0b45`.
- Observation:
  - Decision 2 says terminal exit removes host actions in shared mode. Decision 3 uses a missing listener only as a binder fail-closed condition. Neither says which observable 2.3 uses to detect the terminal disconnect of a shared conversation.
  - Probe 011 step 4 shows `runtimeStatus` keeps reporting `connected` after the client dies, while the LISTEN owner and the backend ESTABLISHED peer vanish at once.
  - The backend-side signals (`thread/loaded/list`, MCP process presence, `thread/closed`) lag by the unload delay or never fire while another client is subscribed.
- Proposed improvement and benefit: in 2.3, derive "terminal disconnected" for a shared Codex conversation from the binder's owner client. The conversation is disconnected when the owner PID and creation identity are gone, or when its listener or backend connection is gone, or when the latest-only rule demotes the binding. Derive "work continuing" independently from exact-thread `thread/read` status and hooks, and never from `runtimeStatus`, the loaded list or an MCP process. This gives 2.3 one shared rule: a terminal-side process check for both providers, with only the shared-Codex adapter differing.
- Behavior-preservation risks:
  - An unbound shared conversation (missing origin, IDE client) has no terminal-side signal. It must stay "terminal unverified", not "disconnected", so attention and tracking continue (decision 3 step 5).
  - The latest-only demotion must not be reported as conversation completion.
- Evidence type: source observation plus retained probe 011 measurements. Not measured for 2.3 timing.
- Verification / measurement plan: provider-paired 2.3 tests with fake process and transport evidence:
  1. Shared owner killed while the thread is idle: disconnected, conversation retained, no completion.
  2. Owner killed while the thread is Active: disconnected, work continuing.
  3. `runtimeStatus` `connected` with no listener: disconnected.
  4. Retained loaded idle thread with no owner: not live.
  5. The matched Claude and embedded Codex PID-exit cases, plus a mixed simultaneous session.
- Relationship: new. Builds on analysis-006 failure mode 2 and probe 011 step 4.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| Not applicable | Source-only investigation | No correction assigned | No closure claimed. |

## Assessment and limitations

- Scope covered: Codex 0.165.0 stdio MCP spawn ownership, granularity, laziness, exit, environment and identification in shared, embedded, standalone and exec modes; Claude Code stdio MCP lifetime and identity from official docs; comparison with hooks, PID and creation checks, `thread/read` and `thread/loaded/list`, and the `codex_tui` endpoint; costs; recommendation.
- Verdict: reject for 2.3 (ANALYSIS-1000). Use the existing per-provider evidence and name the shared-Codex terminal-side check explicitly (ANALYSIS-1001).
- Not performed: provider launches, protocol connections, socket queries, settings edits and Codex quota use (per assignment). Everything about Codex behavior here is tagged-source reading, not a probe. The installed binary was not compared with the tag for these code paths.
- Unverified or hypotheses:
  - Whether ephemeral title threads spawn MCP servers.
  - Whether a cwd or permission republish restarts the stdio process.
  - The Windows job-assignment fallback lifetime.
  - Claude subagent server instances and in-session `/resume` behavior (not in the docs I read).
  - The Claude MCP docs page was read only to about 100,000 of 155,000 characters. The remainder may hold further lifecycle detail, but the env-vars statement is authoritative for the session ID question.
- Documentation workflow: Context7 was used for the Claude docs (three commands), plus one direct fetch of the official MCP page. Codex used the tagged primary source instead of Context7 because it is version-exact.
- Parity: both providers were assessed with matched questions. No provider exception or hard restriction is claimed.
- Report write: the client refused my single attempt to write `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-011.md` ("Subagents should return findings as text"). Per the role rules I did not retry; the orchestrator should save this text there.
- Unexpected writes: none in the repository. The scratch source archive lives outside the repository.
- Before/after identity: identical summary lines (see Review target). The orchestrator owns triage, ledger status and any design edit.
