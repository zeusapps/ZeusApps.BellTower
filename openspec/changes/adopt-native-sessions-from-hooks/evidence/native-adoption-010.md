# Native adoption probe 010: shared-mode Codex 0.165.0 two-client binding, network-isolated rerun with robustness cases

Date: 2026-11-03. Task 1.2 (continuation of probe 009). Developer evidence, not reviewer acceptance. All provider state, helpers, monitors and raw captures are under ignored `artifacts/`. A dummy credential, a closed loopback model endpoint, a private Codex home and a private working directory were used; no real account, model service, user session, `~/.codex` or `~/.claude` was used or modified. Sandbox mode (`read-only`), trust records, hook definitions and hook enablement were unchanged. No hook-review keystrokes were sent and no hook review screen appeared.

## Result in one paragraph

The binding gap from probe 009 is confirmed, now across thread creation, a new conversation in a running client, a switch to an already loaded thread, a client reconnect to a thread whose original client was gone, and two simultaneous launches (warm backend, and a cold start where both clients race to start the backend). No metadata source joined a conversation to exactly one client process. Every thread-scoped log row (about 1,400 of 2,900 projected rows) is emitted by the backend process; every client process emitted only thread-less rows (zero thread-scoped rows from any of seven clients, 12 to 36 rows each). Hook ancestry and inherited environment name the process that started the backend (a different client than the owner in most cases), and a resumed or new conversation from any other client looks identical. A per-thread loopback connection from the backend to the owning client's local listener exists and is OS-visible, but it is unlabelled by thread and its creation time has one-second resolution on this platform, so it is at best a timing heuristic. No binder is proven and no provider restriction is claimed.

## Step 0: network isolation (supported settings and their sources)

Private home only; original config backed up first; line diff asserted to be exactly the additions below (no deletions, no other lines). Sources: `rust-v0.165.0` `codex-rs/core/config.schema.json` field descriptions, `codex-rs/app-server-daemon/README.md` and `src/settings.rs`, `codex-rs/otel/README.md`.

```toml
check_for_update_on_startup = false   # TUI update check (tui/src/updates.rs gates on this)

[analytics]
enabled = false                       # disables OpenAI analytics; also governs the daemon's --analytics-default-enabled default (explicit opt-out is preserved)

[feedback]
enabled = false                       # disables feedback uploads

[otel]
exporter = "none"                     # default; made explicit
metrics_exporter = "none"             # default "statsig" follows analytics; made explicit
```

Plus, in the private home's `app-server-daemon/settings.json` (documented in the daemon README; new file, created by hand because the managed daemon had not yet written one): `remoteControlEnabled: false` and `updater.autoUpdateEnabled: false`. The latter prevents the detached update-loop helper (the daemon README says the loop is launched only when automatic updates are enabled). Defaults for remote control are already false in `settings.rs`; the explicit value is belt and braces. The earlier featured-plugin warm-up stays disabled by `[features] plugins = false` from probe 009.

### A network path that no supported setting disables

The first TUI launch under the socket monitor opened a non-loopback HTTPS connection (port 443) from the **client** process about 1 s after launch, which tripped the abort rule. Cause (confirmed by capturing the request target through a loopback fail-closed proxy in later runs, one request per client launch): the TUI always prefetches an announcement tip from the project's public raw-content host. Source: `tui/src/lib.rs` calls `tooltips::announcement::prewarm(...)` unconditionally, and `tui/src/tooltips.rs` fetches the fixed announcement URL through the generic "other" route class. The config key `tui.show_tooltips` only controls display, not this fetch; `check_for_update_on_startup` does not cover it. So no supported config setting disables this request in 0.165.0.

Mitigation used for all later runs: the standard proxy environment variables (`ALL_PROXY`, `HTTPS_PROXY`, `HTTP_PROXY`) set only in the launched clients' environment to a loopback listener that answers 403 to every request, with `NO_PROXY` listing loopback names. Source for the honoured variables: `http-client/src/outbound_proxy.rs` (`ALL_PROXY` is the route for the "other" class). The proxy log shows exactly one proxied request per client launch, always to that one public host and port 443, never fulfilled. This is an environment-level control, not a config setting, so it is recorded as a documented deviation from "supported config only"; the socket monitor remained the backstop.

Honest disclosure: the one pre-mitigation connection (the aborted first launch) is, as reported by the developer, the single non-loopback network event of this assignment. No raw record of it was retained (the monitor log is rewritten per run, so the retained monitor log covers only the post-mitigation run, and no abort file exists), so the single-event statement is not independently verifiable. It carried no credential or account data (only a dummy key existed), was an unauthenticated fetch of a static public file, and no payload was inspected. A defect in the monitor's first version (it compared two different clock sources for process identity) meant its kill step did not run for that launch; the processes were stopped by hand with PID plus creation-time checks, moments later, and the monitor was fixed before every later run.

## Step 1: socket monitor

- Method: poll about every 0.7 s the TCP connections and UDP endpoints owned by every `codex*` and helper process and all their descendants (clients, backend, hook shells, any updater). Non-loopback TCP remote means immediate identity-checked termination of all tracked processes and of the controller, recorded as address class and port only.
- Pre-check: one bounded traced headless `exec` (debug tracing, 40 s bound, killed by identity at the bound because the closed-endpoint retry loop does not exit): clean (only loopback connection attempts to the closed model endpoint).
- Main run (seven TUI clients A to G, one backend, plus the cold-restart backend): **clean after mitigation**. Zero non-loopback connections, zero UDP endpoints, no update-loop helper process, no abort. The whole-run summary shows only loopback: client-to-backend local transport, backend-to-client listener connections, and refused model-endpoint attempts.

## Step 2: re-verification

- Real home: no Codex process existed before or after; the real home's control-socket and daemon state file listing (names, sizes, timestamps) and config file were byte-for-byte identical before and after.
- Distinct private backend: an `app-server --listen unix:// --managed-daemon` process distinct from every client, spawned by the first client to start it; protocol `initialize` reported the private Codex home. After the first backend was stopped, a second backend was started by whichever client won the cold race.
- Elevation (process-token query on the exact child of each ConPTY helper): all seven clients **not elevated** (0).

## Step 3: baseline two-client turn (default shared mode, same working directory)

Client A launched, client B launched about 30 s later, one literal harmless turn each, typed text and a separate Enter. Two `SessionStart` captures (one per turn). Both capture ancestries read: hook shell, hook shell, **backend**, **client A**, helper; neither contains client B. Both capture environments carry client A's per-client marker and not client B's (names only). Same as probe 009, now with isolation verified. Protocol thread read: two user threads (originator `codex-tui`, source label `vscode`, identical for both clients, no connection or client field) plus ephemeral title threads that fire no `SessionStart` capture.

## Step 4: robustness cases

Each case collected: hook captures (session ID plus ancestry plus environment names), the read-only projection of `logs_2.sqlite` limited to `id, ts, ts_nanos, target, thread_id, process_uuid`, and read-only protocol metadata (`thread/loaded/list`, `thread/read` with turns excluded; previews discarded from this record). No `thread/resume` or turn start was issued through the protocol by the probe.

### (a) New thread in a running client

Client A issued the TUI's `/new`, accepted "use current directory" in the follow-up chooser, then submitted a turn. Result: a third root thread appeared; a `SessionStart` capture fired with that thread's ID; ancestry again ended in **backend then client A**, env marker A. Thread-scoped log rows: backend only. Client A's own new rows: thread-less. New OS-level fact: the backend opened a **new pair of loopback connections to client A's existing local listener** at the moment of the new conversation (the same pair pattern appears at each client launch).

### (b) Switch to an existing thread

Client A used `/resume` and selected the thread owned by client B (both clients now attached to that thread). Result: **no `SessionStart` hook fired** for the switch or for the following turn (capture count unchanged), no new backend-to-client connection pair appeared, and protocol metadata for the thread did not change. The thread-resume request is visible only as a backend-emitted, thread-scoped log row (backend process UUID). Consequence: a hook-time-only adoption design cannot see an in-client switch to an already loaded thread, and nothing marks that the thread now has two attached clients.

### (c) Disconnect and reconnect

Client B was terminated (identity-checked kill). The backend kept B's thread loaded. After client A moved to a different conversation (so the thread had no other attached client), a fresh client C was launched with the resume subcommand for B's thread and submitted a turn. Result: a `SessionStart` capture with B's thread ID fired; its ancestry was the **backend then client A** (A was the backend's launcher), and its environment carried A's marker, **not C's**. Thread rows: backend only. Client C's 14 rows: thread-less. After all clients exited, the backend still listed all user threads as loaded and idle, confirming again that backend thread liveness is not terminal liveness.

### (d) Simultaneous launch

- Warm backend: two clients launched about 10 ms apart, same prompt text, one turn each. Both captures: backend then client A (the long-running original). Thread creation order and client start order were observed, but with identical prompts there is no ground truth, so no mapping is asserted.
- Cold backend: after stopping the backend, two clients launched about 10 ms apart with different literal prompts, which serve as local ground truth read from thread previews (discarded from this record). One client won the race and started the backend. Both captures: backend then the winner, environment marker of the winner for both. Thread-scoped rows: backend only (zero from either client). Temporal ordering of thread creation matched the ground truth in this single trial (first client first, second client about 120 ms later, both after both clients' first rows); one trial of a race is not a binder.

### Searches across all cases

- Thread ID on any client-originated log row: **none**, across seven clients and every case (zero of 112 client rows; about 1,400 thread-scoped rows all backend-emitted).
- Connection-to-thread association in protocol thread metadata: **none**. All keys of user threads were compared; originator, source label, direct-input flag and extra were identical across clients.
- Stored thread attachments (`thread/attachment/list`, read-only): empty for the two threads queried (2 of 6 user threads; the remaining threads were not queried in retained output), so the retained calls do not show that the TUI writes durable client or terminal references that could serve as a binder.
- Client-visible UDP: none.

## Step 5: answer

**Is there metadata joining a thread to exactly one client that survives cases (a) to (d)?** No. Exact gap, for non-elevated shared Codex 0.165.0: hook ancestry and inherited environment describe the backend's launcher; hook session ID equals the protocol thread ID but names no client; log process UUIDs identify clients only on thread-less rows and the backend on every thread row; protocol thread metadata has no connection or client field; no durable attachment appeared for the threads queried (2 of 6 user threads). A switch to a loaded thread does not even fire a hook.

### Host-side correlation notes (verified versus inferred)

- Verified: each client exposes a loopback listener and the backend holds connections to it; a fresh pair appears at each client launch and each new conversation in that client, not at a resume of an already loaded thread. The OS identifies the pair's owning client process and the backend as the other endpoint.
- Verified limitation: the OS connection creation timestamp on this platform has one-second resolution (fractional digits are zero), too coarse to order two sub-second events; log timestamps (nanoseconds) can order the thread row against the client's first row, but connection creation cannot be placed between them.
- Inferred only: correlating (client listener pair created) with (thread created) by time would give the right answer when launches are well separated, and matched ground truth in the one cold race trial, but it fails by construction for in-client switches to loaded threads (no new pair, no hook), for resumes into a different client than the original, and for near-simultaneous creations without a finer clock. It is not admissible as a binder without a host-registered, hook-time signal.
- Not probed (would require interacting with the client's local listener, outside this assignment): whether the listener's own protocol exposes an identifier of the backend session it serves.
- A host-registered terminal identity (shell, window or tab marker) joined to the thread by a verified hook-time signal other than ancestry remains the only open avenue; nothing found here supplies that hook-time signal.

## Cleanup

Stopped, each by PID plus creation-time identity check: all seven clients and their ConPTY helpers, both private backends, the controller, the socket monitor and the loopback proxy. No launched process or background shell remains (verified by process listing; an unrelated third-party helper process present on the machine was not touched). The real Codex home was unchanged. The private config additions remain in the ignored private home with the backup beside the evidence artifacts.

## Disposition and proposals for the orchestrator

- No binder is proven; no hard provider restriction is claimed. The precise gap above stands as the task 1.2 result unless the orchestrator authorizes a fresh avenue: a hook-time terminal registration path (task 4 host bridges), or investigation of the client listener's protocol.
- Design implication for decision 3: shared Codex terminal actions must stay disabled for any conversation whose terminal cannot be registered by a verified host-side signal; hook-time ancestry must never be used for shared mode; a switch to a loaded thread yields no hook and must be tolerated as "no new evidence" rather than treated as the thread's disappearance or ownership change.
- Harness notes: any TUI trial in this environment will make one unconditional public announcement-tip request per launch unless the proxy environment fail-closed mitigation (or a network block) is applied; the existing ConPTY helper fixes from probe 009 were reused unchanged.
