# Native adoption probe 011: A3 MCP-endpoint binder, shared-mode Codex 0.165.0 (non-elevated)

Date: 2026-11-03. Task 1.2 to 1.3 (bounded probe of the "A3" candidate from `reports/analysis-006.md`). Developer evidence, not reviewer acceptance. Raw data, scripts, screens and logs are under ignored `artifacts/native-adoption-011` (scripts are copies of the probe 010 harness; the private Codex home is the same `artifacts/n3c` as probes 009 and 010). No product source was changed.

Provider and mode: Codex CLI 0.165.0 (client and the private managed app-server package copy, same version), default shared mode (`app-server --listen unix:// --managed-daemon`), `--sandbox read-only`. Hook definition, hook trust record and enablement, sandbox mode and the private `config.toml`, `hooks.json` and daemon `settings.json` were unchanged (SHA-256 identical before and after; backups under `artifacts/native-adoption-011/bak`). No trust hash was transplanted. The user's real Codex home state files were identical before and after.

## Result in one paragraph

A3 works as a thread-to-TUI binder for the cases a hook can see, and fails closed or silently wrong in exactly the cases predicted by the source analysis. For every root thread, `mcpServerStatus/list {threadId, serverName:"codex_tui", detail:"toolsAndAuthOnly"}` returned one `codex_tui` entry with `httpOrigin` `http://127.0.0.1:<port>` and `runtimeStatus` `connected`; the port was always the loopback LISTEN socket of exactly one non-elevated TUI process (PID plus creation time resolved), with a backend-owned ESTABLISHED connection to it. Baseline, `/new`, cold resume, fork and backend reconnect gave the expected owner; a join of an already subscribed thread kept the original loader (case b); after the original loader died the port had no listener (unverified); and a cold resume while another client still displayed the thread was ignored (deviation from the plan's expected row, which fails closed). One silent wrong binding was reproduced: a thread rejoined by a second TUI keeps naming the original loader, and when that loader switches to a new thread the address still names a live TUI that no longer displays the thread. The read had no visible effect on any TUI, caused no hook capture and no turn.

## Isolation, elevation, distinct backend (before and during)

- Isolation revalidated before launch: private home config contains exactly the 010 isolation additions (`check_for_update_on_startup=false`, `[features] plugins=false`, `[analytics]`, `[feedback]` off, `[otel]` exporters none, `model_catalog_json`, a loopback closed model endpoint) and the daemon `settings.json` has `remoteControlEnabled:false` and `updater.autoUpdateEnabled:false`. Clients ran with a minimal environment, a dummy credential, `ALL_PROXY`, `HTTPS_PROXY`, `HTTP_PROXY` pointed at the fail-closed loopback proxy (403 to everything) and loopback `NO_PROXY`. The socket monitor (polling about every 0.7 s, all `codex*` and helper descendants, identity-checked kill on any non-loopback TCP remote) ran for the whole probe.
- Elevation: process-token queries are retained only for clients A, B, C and D (per-launch records on the exact child of each ConPTY helper, elevation 0). The launching PowerShell session, the restarted backend and the observer processes were also reported as not elevated (0) by the developer's own token queries, but that output was not retained and is developer-observed only; it is consistent with inheritance from non-elevated parents. The first backend had exited before it could be queried directly; it was spawned by non-elevated client A, so it could not be elevated, and it was not queried (recorded as such).
- Distinct shared backend before anything was called shared: a `codex.exe` whose image is the private home's managed package copy, command class `app-server`, a different PID from every TUI, with PID plus creation time recorded in each observation (`backends` in `obs.json`). Every TUI exposes its own loopback LISTEN socket to which the backend holds ESTABLISHED connections. The observer used the existing `app-server proxy --sock` path over the private control socket and the proxy `initialize` reported the private home (as in 009).

## Method

One observation per step (`q11.ps1`, wrapped by `step.sh`), all read-only:

1. `thread/loaded/list`, then `thread/read` with turns excluded for every loaded thread (metadata only; previews and names dropped).
2. One `mcpServerStatus/list` per loaded non-ephemeral (root) thread with `serverName` always set to `codex_tui` and `detail:"toolsAndAuthOnly"`. Only `name`, `httpOrigin`, `runtimeStatus` were kept; tools, metadata and capabilities were discarded unrecorded.
3. For each port: OS LISTEN owner (PID, creation time, command-line class only, TUI versus app-server) and the owners of ESTABLISHED connections whose remote port is that port.
4. Before and after the read: raw ConPTY output byte counts and the last 400 cleaned characters of every live TUI screen, the hook capture list, and the delta of log rows (read-only projection of `target`, `thread_id`).

Hook captures were the same retained handler as before. Exactly three `SessionStart` captures occurred during the whole probe (A first turn, B first turn, A turn after `/new`), each at its own turn submission; none occurred at any read. Three fixture turns went to the closed loopback endpoint and were interrupted with Esc to leave threads idle. Ground truth for which TUI owns which thread is the order of my own launches and inputs.

## Per-step results (thread IDs abbreviated to the last 8 characters)

| Step | Action | Expected | Actual (port, listener owner, backend ESTABLISHED) | Match |
| --- | --- | --- | --- | --- |
| 1 | A and B baseline, one turn each | each thread shows its own TUI | `3b2b2e80` -> 63520 -> A (29088); `5bf984a8` -> 56549 -> B (35688); backend connected to both | yes |
| 2 | A `/new`, turn | new thread shows A | new thread `c2e8c803` -> 63520 -> A; A's old thread unchanged | yes |
| 3 | A `/resume` into B's thread while B still shows it (case b) | stays B | `5bf984a8` -> 56549 -> B (unchanged, A and B both attached) | yes |
| 4 | kill B | no listener, unverified | `5bf984a8` still reports `httpOrigin` 56549 with `runtimeStatus` `connected`, but no LISTEN owner and no ESTABLISHED peer | yes (shows `runtimeStatus` lags) |
| 5 | C cold-resumes B's thread while A still displays it | C (plan row) | still 56549, no listener: the resume overrides were ignored because A is subscribed; C's launch has no effect on the address | no (fails closed; source-predicted, see below) |
| 6 | A and C `/new` away (nobody subscribed to B's thread) | each new thread shows its own TUI | A's new thread -> 63520 -> A; C's new thread -> 62936 -> C; B's thread still names dead 56549 | yes |
| 7 | D cold-resumes B's thread, no subscribers (clean case c) | D | `5bf984a8` -> 57598 -> D (36860), backend connected | yes |
| 8 | A `/resume` into D's thread while D shows it (case b, second instance) | stays D | `5bf984a8` -> 57598 -> D | yes |
| 9 | A `/fork` of that thread | fork shows A | fork `58858bee` -> 63520 -> A; parent still D | yes |
| 10 | stop backend once (identity-checked), start a new one with the supported `daemon start`, TUIs reconnect | same port per TUI | new backend (distinct PID, creation time); D's thread -> 57598 -> D, A's fork -> 63520 -> A; A and D ports identical to before (C not observable: its only thread was never submitted, so after the restart it showed read-only/unavailable, was not loaded and had no address); only the displayed threads are loaded again | yes |
| 11 | extra: A `/resume` into the thread D still shows, then D `/new` | (not in plan) | the thread A now displays still -> 57598 -> D (live listener, backend connected) though D displays a newer thread: silent stale | n/a (gap reproduced) |

Additional single read after step 10 (developer-reported; no output was retained; the backend request log proves one `mcpServerStatus/list` on connection 7 at 19:16:12, but the error text and the unchanged loaded list are not retained): the same call for a hook-captured thread that the new backend had not loaded returned the error "thread not found" and did not load it (loaded list unchanged). So an unloaded thread yields no address, which is fail-closed.

### Notes on deviations from the expected column

- Step 5: the plan's expected "case (c) shows C" holds only when no other client is subscribed. In the plan's order A still displayed the thread, so the resume was ignored. This is consistent with `thread_processor.rs` (resume of a loaded thread with subscribers ignores overrides) and with the stale-gap analysis. It fails closed here (dead owner), but the same state with a live original owner would be silently wrong (step 11).
- Steps 6 and 7 are the clean case (c): once A and C had left the thread, a cold resume re-owned it to the new TUI.

## Process mapping and port stability

- Port to owner: every probed port that had a live owner mapped to exactly one TUI process with a command-line class other than `app-server`, and all ESTABLISHED peers of those ports were the backend. One port (the dead B) mapped to no listener and no peer.
- Per-TUI port stability: A kept 63520 for every thread it started, resumed or forked, and across the backend restart; C kept 62936 until the restart (not observable afterwards, see step 10); D kept 57598 across the restart; B kept 56549 until killed. Threads bound to one TUI accumulate (A bound four threads while displaying one), as predicted by failure mode 7.
- Port reuse was not exercised.

## Screens, hooks and turns

- Screens: for every one of the 11 steps and every live TUI, the ConPTY output byte count was identical immediately before, 3 s later and after the read, and the retained screen text was identical. So no TUI output changed during any read. Limit: the retained "screen text" is the last 400 cleaned characters of the raw ConPTY output stream, not a rendered screen, so the byte-count equality (26 of 26 client-step pairs) is the effective proof.
- No extra hook capture: capture count unchanged across every read (three in total for the whole probe).
- No turn: thread status stayed `idle` for every root thread before and after each read. The only thread-scoped turn-related log rows in the read window (`responses_retry`) belonged to the ephemeral title threads, which retry against the closed endpoint independently of any read. The reads produced `tool_catalog` log rows without a thread ID, no thread-scoped row on a root thread, and no new connection pair. Limits: the retained log projection has no message text and the backend request log has no request rows before 18:59:37, so log corroboration is absent for the early steps (including that the two failed step-1 observer runs made no MCP call, which rests on the script). The read windows of steps 2, 5 and 9 also contain `rmcp::service` rows without a thread ID that cannot be attributed from the retained projection; thread status and turns stayed idle and empty.

## Does a TUI unsubscribe from the old thread when it switches?

Observed from backend request logs (rows carry the connection ID but not the thread ID, so the unsubscribed thread is inferred from request order): the TUI sends `thread/unsubscribe` right after a `thread/start` for `/new` (A twice, at 19:00:53 and 19:07:07, C once, D once) and right after a `thread/resume` for `/resume` out of a thread with history (A twice). No unsubscribe row followed an `/resume` out of an empty never-submitted thread, and none followed `/fork`. So the TUI does release the previous thread on `/new` and `/resume` in the ordinary case; this is why step 11 produces a thread whose only subscriber is a TUI that is not the address owner. After a fork the parent's subscription was not observed to be released.

Reach of the stale gap: it requires (1) a thread loaded by TUI X that a second TUI Y later joins while X is still subscribed, then (2) X leaves by `/new`, `/resume` or exit. Exit gives a dead listener (unverified, safe). Leaving by `/new` or by a cold resume of another thread gives X a newer own thread, so a "latest binding per TUI" rule could demote the older thread. Leaving by `/resume` into a thread loaded by a third TUI gives X no newer thread, and the address silently stays X; that sub-case was not run.

## Thread metadata that could distinguish forks and delegated threads (read-only `thread/read`)

- Available fields on every thread: `source` (always the IDE-style label here, identical for all clients), `threadSource` (`user` for root threads, `thread_title` for the ephemeral title threads), `ephemeral`, `originator` (`codex-tui`), `sessionId` (equals the thread ID for roots and the fork), `forkedFromId`, `parentThreadId`, `agentNickname`, `agentRole`, `cwd`, `createdAt`.
- Fork: `forkedFromId` held the parent thread ID; `parentThreadId` and the agent fields were null. A fork is therefore distinguishable from a root thread, and its address was the forking TUI as expected.
- Delegated/subagent threads: not performed (needs a model). The fields `parentThreadId`, `agentNickname`, `agentRole`, `threadSource` exist and are the candidate discriminators; their values for delegated threads are unknown.
- `createdAt` is unusable for a thread that has no rollout yet: for the two never-submitted threads it equals the observation time and changes at every observation (steps 6 to 9: four different values, tracking each read time), while the fork's value stayed fixed. The A3 guard "listener created before the thread's `createdAt`" must therefore not rely on `createdAt` for unmaterialized threads. (The thread IDs are time-ordered, which may serve as an ordering key.)
- A TUI whose only thread was never submitted could not resume it after a backend restart (the screen reported read-only or unavailable); this is a limitation of empty threads, not of A3.

## Network observation

The socket monitor recorded 609 distinct TCP rows in the retained `monitor.log` (295 listen, 314 loopback), all loopback or listen; no non-loopback remote and no UDP endpoint; no abort file; no update-loop helper process. The fail-closed proxy logged exactly four requests (one per client launch, A to D), all to the same fixed public raw-content host on port 443 (the unconditional announcement-tip prefetch documented in 010), all answered 403. Restarting the backend produced no outbound traffic.

## Deviations and caveats

- Backend restart: the TUIs did not start a new backend on their own; they showed "Reconnecting to server" until the backend was started with the supported command `codex app-server daemon start` (run with the same minimal private environment, then they reconnected). The ports stayed the same. The plan said "restart the backend once and let the TUIs reconnect"; I added this explicit start. The old backend was stopped by identity-checked kill (the first identity comparison failed because of 100 ns timestamp rounding between two APIs, so nothing was killed; the second check used the process-table creation time and matched).
- Extra TUI D and extra steps (6, 7, 8, 11) were added to separate the subscribed from the unsubscribed case (c) and to reproduce the stale gap; step 5 was run as written first.
- The first two observer runs for step 1 failed on a path-quoting bug in my wrapper and performed only `thread/loaded/list` and `thread/read` before aborting; no MCP call was made by them. The step 1 observation reported is the rerun. A PowerShell alias collision (`cls`) briefly blanked the process-class field in steps 1 and 2; the PID and creation-time fields were unaffected and the class was recomputed from step 3 on.
- Observer connections: each step used several short read-only protocol connections (loaded list, thread reads, MCP reads), which add connection IDs in backend logs. The read of tools is source-described as at worst one read-only tool listing to the TUI's local server; no payload was retained.
- The delegated-thread case (failure mode 6) was not performed.
- The first backend's elevation was not queried directly (see above).

## Cleanup

Stopped, each by PID plus creation-time identity check: clients B (mid-probe), then A, C, D; the second backend; the controller (exited on its quit command); the socket monitor (exited on its stop file); and the fail-closed proxy. The first backend was stopped by identity check mid-probe. Verification by process listing afterwards: no `codex*` or ConPTY helper process remains, no process referencing the probe directory remains, and the monitor log ends without an abort. An unrelated third-party helper process present on the machine was not touched. The user's real Codex home state was unchanged. The private home keeps its existing fixture state and its new thread and log rows (ignored artifacts).

## Disposition and proposals for the orchestrator

- Verdict: partially viable. A3 binds a hook thread to a verified live TUI for new threads, cold loads, forks and reconnect, with a listener-owner check making an exited owner unverified. It cannot detect that a live owner no longer displays a thread that another TUI joined (step 11). The binder is therefore usable only with fail-closed policy: accept a binding only if the owner TUI's most recent hook-time or loaded-thread binding is that thread; treat any thread that a second TUI may have joined as unverified when the owner has a newer thread; keep unloaded threads and missing `httpOrigin` unverified.
- No hard provider restriction is claimed; the remaining gap is the rejoin-then-owner-switches-to-another-owner's-thread sub-case, which no observed signal reveals.
- Candidate follow-ups: probe delegated threads when a model is allowed; run the unrun sub-case in step 11 with a third TUI; and check whether `thread/list` or `thread/read` with `includeTurns` exposes per-thread subscriber counts (not observed in the fields read here).
