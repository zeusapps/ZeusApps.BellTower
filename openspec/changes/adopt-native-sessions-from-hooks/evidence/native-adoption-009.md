# Native adoption probe 009: shared-mode Codex 0.165.0 two-client binding (non-elevated)

Date: 2026-11-03. Task 1.2. Developer evidence, not reviewer acceptance. All provider state, fixtures, helpers and raw captures are under ignored `artifacts/`. A dummy credential and a closed loopback model endpoint were used; no real account, model service, prompt, user session, `~/.codex` or `~/.claude` was used or modified. Sandbox mode (`read-only`), trust records, hook definition and enablement were unchanged.

## Result in one paragraph

The first genuine non-elevated shared-mode run was achieved: two native TUI clients, one distinct private backend, the same fixture directory and titles, and two real `SessionStart` captures. The run produced a precise **evidence gap**, not a binder. Hook ancestry and inherited environment name the process that *spawned* the backend, not the client that owns the conversation; hook `session_id` equals the protocol thread ID for root threads; but every thread-scoped diagnostic log row is emitted by the backend process, and the client processes emit only thread-less rows. No supported, non-guessing metadata source joined a thread to exactly one client process. The only discriminating signals found are temporal (thread creation shortly after a client's creation time) and an unnamed per-client loopback link, neither of which is an admissible binder. Two safety deviations (an outbound backend connection and an update-loop helper) are recorded below.

## Step 0: remote featured-plugin warm-up (QA-700 prerequisite)

- Source: `rust-v0.165.0` `core-plugins/src/manager.rs`. `maybe_start_plugin_startup_tasks_for_config` runs the featured-plugin warm-up only inside `if config.plugins_enabled`, and `featured_plugin_ids_for_config` itself returns an empty list when `plugins_enabled` is false. `Config::plugins_config_input` derives `plugins_enabled` from `Feature::Plugins`, whose key is `plugins` (stable, default on).
- Supported setting applied to the private home only (original backed up first, line diff asserted to be exactly this addition and nothing else):

```toml
[features]
plugins = false
```

- Proof: one bounded (25 s) traced headless `exec`, same isolation as the earlier headless probe, debug-level tracing for all targets, non-elevated child (token elevation 0). The trace contains no featured-plugin line and the only URLs present are the loopback model endpoint and a configuration dump naming the default ChatGPT base URL (a value, not a request). No non-loopback request appears in the trace. SessionStart still fired. Limitation: this is a trace check, not a socket capture (see the deviation below, which a trace missed).

## Harness defect found and fixed (affects any ConPTY TUI trial in this environment)

The first shared attempts hung before any TUI output. Cause: the private ConPTY helper's child inherited the helper's own redirected standard handles, so the child saw a non-console stdin (a PowerShell `ReadKey` control reported "console input has been redirected"; Codex wrote its stderr to the helper's stderr). Fix in a private copy of the helper: create the .NET console streams first, then clear the helper's three standard handles before `CreateProcess`, so the pseudo-console handles apply. After this, the control child read keys and the Codex TUI rendered. The helper's job object also needed `JOB_OBJECT_LIMIT_BREAKAWAY_OK`: without it a default shared launch would hit the documented "launcher prevents background processes from outliving it" restriction and fall back to embedded mode. These are harness findings, not Codex restrictions.

## Before launch (step 1)

- No Codex or app-server process was running for the user's real home; the real home's control-socket and daemon state files were recorded before and compared after: unchanged. They were never touched.
- Endpoint resolution, by source: the control socket path is derived only from the resolved Codex home (`<home>/app-server-control/app-server-control.sock`), as are the daemon state directory and the managed package root. Observation: with the private home, the first default launch created a private control socket, private daemon state files and a private managed package copy of the same CLI version inside the private home; the backend's executable image was that private copy.
- Elevation: both launched clients were checked by process-token query on the actual child: **not elevated** (0) for client A and client B. The Codex 0.165.0 elevated-launch downgrade to embedded mode therefore did not apply, and the daemon path was used.

## Distinct shared backend (step 2)

- A shared backend process existed that was not either client: an `app-server --listen unix:// --managed-daemon` process launched from the private package copy, recorded with PID and creation time, spawned by client A (the first client) and then used by client B.
- A read-only protocol connection (through the product's `app-server proxy` over the private socket, `initialize`, `thread/loaded/list`, `thread/read` with `includeTurns` false) reported the private Codex home in its `initialize` response, so the endpoint is observably the private one, not the user's.
- Each client launched an update-loop helper (`pid-update-loop`). These were stopped by identity (PID plus creation time) immediately after they appeared, before any activity, because their network behavior was unknown. The backend still ran. This is a deviation from "launch only what you were asked to" and is recorded as such.

## Native turns and captures (step 3)

One literal fixture turn per client, typed and submitted with a separate Enter (a text-plus-Enter single write is held in the composer, not submitted: paste-burst handling). Two `SessionStart` captures were written, one per client turn, each at submission time:

- Capture ancestry for **both** clients: hook shell (twice) -> **backend** -> **client A** -> outer helper and tool processes. Client B's hook chain does not contain client B at all. The immediate hook parent is the backend, and the backend's parent is client A only because A happened to start the backend.
- Capture environment names (names only, by design of the retained handler): the backend-inherited `CODEX_HOME`, the daemon shutdown-socket variable, and client A's per-client marker variable. **Client B's marker variable is absent from both captures**, including B's own. This is direct evidence that the backend environment carries the first client's terminal-style variables and cannot be trusted for ownership.

## Questions from step 4

1. Hook ancestry parent: the shared backend, never the client TUI. The next ancestor is whichever client started the backend, so ancestry actively misleads for any later client.
2. Hook `session_id` versus protocol thread ID: equal for both root user threads (the same IDs appeared in `thread/loaded/list` and as the thread identity on backend log rows). Not assumed for forks or subagents (not exercised). Two additional ephemeral `thread_title` threads (own session IDs, `source` reported as the IDE-style label, `threadSource` `thread_title`) appeared per turn; they had no corresponding hook capture. Protocol `source` labels are not terminal identities.
3. Join of each thread to exactly one client process by allowed metadata: **not achieved.**
   - Read-only projection of the six allowed log columns: all rows that carry a `thread_id` have the **backend's** process UUID; client A and client B each have their own process UUID (encoding their PID) but only thread-less rows (about 20 and 14 rows: HTTP client setup, TUI startup, configuration, local transport). So the log-metadata channel names the backend, not the client, for conversation rows.
   - Protocol thread metadata (id, session id, source, originator, status, cwd, timestamps) contains no connection or client-process identifier; both clients report the same originator, source and working directory.
   - Temporal inference only: each root thread was created within seconds after its client's process creation time (about 6 s and under 1 s here, clients 44 s apart). That would be ambiguous or wrong for simultaneous launches, resume, or a second thread in one client, so it is not a binder.
   - Each client process owns a loopback listener to which the backend holds one established connection (per-client, observable by the OS). Nothing in metadata ties a connection to a thread, so this is a process-pair fact, not a conversation binding.
4. Robustness: client B was stopped by identity-checked kill (a disconnect). The backend kept B's thread loaded and still reported it as active, while B's process was gone, which confirms the existing design rule that backend thread liveness is not terminal liveness. Thread switch/new-thread in a client, and reconnect of a client to its prior thread, were **not performed** (see below).
5. Stale daemon environment: confirmed (client B's hook saw client A's marker, not its own).

## Safety deviation and why robustness tests were not run

While both clients and the backend were idle, the developer observed in an OS socket listing for the backend an **established connection to a non-loopback public HTTPS address** (attributable to the product's own background behavior: the backend is launched with default analytics, and the protocol reported a remote-control status change on connect; the exact cause was not determined and no payload was inspected). This observation is developer-reported: no raw socket record was retained, so it is not independently verifiable. Only the dummy key existed, so no credential or account was involved, but the instruction was loopback only. The trace check in step 0 covers only the headless embedded run and did not exercise the daemon. I stopped the experiment at that point rather than disable further product behavior by editing more of the private configuration without authorization. Consequently switch/new-thread and a true reconnect of a client to the same thread were not run. A follow-up run needs an authorized, supported setting set (for example analytics and remote control off for the private home) and a socket-level check.

## Cleanup

Only processes started by this run were stopped, each after PID plus creation-time verification: both clients, both update-loop helpers, and the private backend. No Codex process remained. The user's real Codex home state files were identical before and after, and no process of the user's was affected.

## Disposition

- No binder is proven and no hard provider restriction is claimed.
- Precise gap: for a non-elevated shared Codex 0.165.0 backend, none of hook ancestry, inherited environment, log `process_uuid`, protocol thread metadata or the per-client loopback link names the client that owns a given conversation. Log process UUIDs identify clients, but only on thread-less rows; thread rows belong to the backend.
- Candidate avenues still open: whether any client-originated log row ever carries a thread ID under ordinary use (not observed here, including at turn submission), whether a TUI emits a thread-scoped row on thread switch or resume, protocol thread-attachment records created by a client (not shown to be created by the TUI), and host-side registration (shell/terminal identity) joined to the thread by a verified hook-time signal other than ancestry.
- Not performed: thread switch or new thread in a client, reconnect to the same thread, duplicate-title disambiguation beyond identical titles, VS Code or Windows Terminal host registration, Claude pairing, and any product-ingress parsing of a native Codex envelope (the retained handler produced the captures).
