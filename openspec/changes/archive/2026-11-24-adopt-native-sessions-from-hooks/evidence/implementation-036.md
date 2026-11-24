# Implementation 036: exact-conversation status and native names on owned and shared runtimes (task 3.2)

Developer evidence (DEV-036), not reviewer acceptance. Baseline HEAD a58c4e3. The working tree is left uncommitted. Fixes below await independent verification; none is marked closed here.

## One opener, one selection rule (ANALYSIS-2201)

- `ICodexNameClient` and `ICodexAttentionClient` no longer declare `ConnectAsync`. `CodexConnection` keeps the owned `ConnectAsync(endpoint, token)` (used by the runtime host and by the opener) and the transport-based `ConnectAsync`.
- `CodexRuntimeOpener` (Sessions) is the only place that chooses access, for both `CodexNameService` and `CodexPermissionPump`:
  1. a live attachment with an owned runtime record whose recorded shell equals the attachment shell selects `CodexAccess.Owned` (endpoint plus token file path; no credential in the record);
  2. otherwise a live shared-runtime attachment whose stored evidence mode is `shared-backend` selects `CodexAccess.Shared` for the resolved Codex home (`CODEX_HOME`, else `~/.codex`; the tray's own resolution);
  3. anything else (ended, Claude, unknown mode, hook not proven to come from a shared backend) has no access and is reported as before (`unavailable: no verified dedicated runtime`).
  `ConnectAsync(access)` returns an already connected `CodexConnection` from the owned path or from `CodexSharedAccess.OpenAsync`. The tray composes one opener for both services, so shared-backend backoff and replacement tracking are shared. A `Replaced` result is recorded as the code `codex-shared-backend-replaced` only.
- Test seam: both services take `Func<CodexAccess, CancellationToken, Task<TClient>>` instead of an unconnected-client factory; fakes keep their own connect logic inside it.
- Mode gate: the one pre-existing test row that expected a `shared:shared-backend` conversation to be unavailable (`NativeOwnedRuntimeProcessTests`) was changed. A proven shared-backend conversation now selects shared access (never the owned record); `shared:unknown` still has no access and writes nothing.

## Hook session to protocol thread (design decision 4)

`CodexThreadIdentity.Require` runs on every exact-conversation read. For a runtime Bell Tower does not own (`RequiresProvenThreads`, true for the shared transport) the thread must have `id` equal to the hook session id, `sessionId` absent or equal to `id` (the root of its own session), and no `parentThreadId`, `forkedFromId`, `agentRole` or `agentNickname`. Otherwise `ThreadUnmapped` is raised and nothing is read further or written (status: `unavailable: conversation is not proven to match the runtime thread`). For an owned runtime only a present, different `id` is refused (the previous behavior assumed equality).

Decision for the orchestrator: forks are refused on a shared backend. Native evidence 011 shows a fork's `id` and `sessionId` agree, but the hook `session_id` of a fork was never observed, and a wrong mapping would write another conversation's name. This is unfinished evidence, not a claimed restriction; lift it with a fork hook probe. Delegated threads were never observed and are refused for the same reason. No provider-version gate is used (design decision 7); the self-check is the schema fields.

## Shared transport refinements

- ANALYSIS-2300: one peer-verified connection per open. The per-request and post-handshake listener probes (and the 1 s synchronous probe) are removed. `EnsureCurrent` is now only the liveness of the verified backend (PID plus creation time) before each request. A different process listening at the path is seen by the next open (its peer differs: `Replaced`), and a removed socket by the next open (`EndpointMissing`). Probe tests were replaced: `EachOpenMakesOneBackendConnectionAndRequestsMakeNone` (stand-in `Accepted == 1` before and after three requests), `ASupersedingListenerIsSeenByTheNextOpenAndTheVerifiedConnectionKeepsItsBackend`, `RemovedSocketFailsTheNextOpenAsMissing`; `EndedBackendDropsTheOldConnection` is unchanged.
- ANALYSIS-2302 / design decision 4 / QA-3000: `CodexTransportFailure` now distinguishes `BackendNotVerified` (not live, ended before the read, or differs from an expected identity), `PeerUnsupported` (ioctl unsupported or no PID; replaces `PeerUnavailable`), `PeerUnreadable` (process exists but its identity, command line or token cannot be read), `ImageMismatch`, `CommandLineMismatch` and `ElevatedPeer`. `CodexBackendCheck` applies live, image, command line, then elevation, so identity problems are named first. Elevation is `ProcessProbe.IsElevated`: the peer token's `TokenElevation` or an integrity level of High or above, read with limited rights; an unreadable token is `PeerUnreadable` (fail closed), and an elevated peer is refused before any byte is sent. Statement correction (ANALYSIS-2302): an elevated peer was previously probably accepted, not refused; it is now refused explicitly.
- QA-3000 direct tests: the real ioctl on a non-local (TCP) socket returns null and `Identify` raises `PeerUnsupported`; the real lookup on a disposed socket returns null; a nonexistent PID is `BackendNotVerified`; an existing process whose identity cannot be read is `PeerUnreadable` (identity lookup injected, because the unreadable-but-existing case cannot be created reliably without privileges).
- ANALYSIS-2203: one mapping (`CodexConnection.Reason`) turns post-handshake raw failures into reasoned `CodexTransportException` on strict transports: read-budget expiry `Timeout`, dropped or refused `Refused` (a close frame is now an `IOException` instead of an invalid-data error), parse errors `Malformed`, a JSON-RPC error `Rejected` (message names the reason only). Caller cancellation is not a timeout and passes through. Owned (non-strict) failures keep their previous exception types.
- ANALYSIS-2205: `CodexSharedEndpoint.HomeKey` (absolute, normalized, upper-cased on Windows) keys both backoff and replacement. The dead containment check in `SocketPath` is removed, which also makes a drive-root home derivable.
- Content discipline: `thread/read` is sent with `includeTurns: false` for names as well (previously only for status), and only `id`, identity fields, `name` and `status` are read; the response is cloned only for its `result` and never logged or stored. Status text and log codes name reasons, never content.

## Behavior verified (stand-in tests; Codex only)

`CodexSharedRuntimeServiceTests` (11) and `CodexSharedAccessTests` (24 cases) plus the updated transport, name, permission and owned-runtime tests. The shared policy (selection, name ownership, status) is not Codex-only in structure, but there is no Claude shared backend or native-title transport in this change (design decision 4; reconciliation is 3.4). Claude behavior is unchanged: `NativeNameControl` still refuses Claude, `NativeNamePump` still visits only Codex sessions, and the existing Claude native-name and confirmation tests pass unmodified.

- Reads never resume or start a turn: every shared and owned stand-in run asserts no `thread/resume`, `thread/start` or `turn/*`, and asserts `includeTurns: false` on every `thread/read`.
- Native user title pauses writes: a thread whose name was set outside Bell Tower receives no `thread/name/set`, and the identity is paused and disabled.
- Explicit enable and disable persist: disabled makes zero connections, survives a new service and opener, and enable writes the name.
- Isolation: two conversations on one shared backend, synced in parallel, write only their own names and every request names only its own thread; permission status is read per thread (one pending, one idle).
- Mixed: an owned loopback runtime (with its bearer token) and a shared backend at once; each stand-in sees only its own thread, and the other gets no request for it.
- Elevated shared backend: refused with `unavailable: shared backend runs elevated` and no request sent (name service), and the pump reports no supported signal.
- Forked thread: refused with its own status, nothing written.
- Missing backend: reported with its reason; a second attempt inside the backoff neither retries nor replaces the status.

Mutants (single-line edits, rebuilt, targeted tests run, restored): thread proof off killed 6 tests; elevation condition inverted killed 5; selection ignoring the shared-backend mode killed `SelectionRuleIsOneForBothServicesAndFailsClosed`; raw home as the key killed 6.

## Results

- Targeted (transport, shared access, shared service, name, permission, native-name and owned-runtime tests): 149 passed.
- Full suite: 1508 tests, 1507 passed, 1 failed (`ConsoleTitleProcessTests.HelperChangesOnlyADisposablePrivateConsoleAndRejectsDetachedAttachments`, Hosts, process timing under load, unrelated to this change); rerun alone: `ConsoleTitleProcessTests` 4/4 passed.
- `dotnet build -c Release --no-restore`: 0 warnings, 0 errors. `dotnet format --verify-no-changes --include` on all changed `.cs` files: passed. No lock file, `global.json` or provider setting changed.

## Native check (Codex CLI 0.166.0, Windows, installed native `codex.exe`; no quota)

Performed. A private short Codex home in the temp area, dummy API key, analytics off, plugins off, daemon settings with remote control and auto-update off, all proxy variables pointing to a loopback proxy that answers 403 to everything. The proxy logged exactly one denied `CONNECT` (to the provider's public API host, port 443) during the whole run; it was refused, no turn or model request was made by the product or the harness, and the origin of that single attempt (backend start versus `thread/start`) was not isolated. Implementations 034 and 035 recorded none, so this is a difference to note.

Backend started with `app-server --listen unix:// --managed-daemon`. A harness (outside the product) called `thread/start` once to create a thread without a turn; the product code then ran unmodified:
- `CodexSharedAccess` opened with default provenance (derived peer equals the started PID; `Replaced = False`).
- Exact-conversation status read (`thread/read`, no turns): permission pending `False`; initial name empty. `CodexThreadIdentity` accepted the native thread (id equals session id, root).
- `CodexRuntimeOpener.Select` chose `Shared` for the shared-backend attachment. `CodexNameService.Sync`: status `synchronized`, the native name equals the Bell Tower name (read back through a second call).
- After an outside rename (`thread/name/set` by the harness), the next sync was `paused: native chat name was set outside Bell Tower` and the user title was preserved.
- `NativeNameControl.SetEnabled(false)`: the next sync reported `disabled` and the name was unchanged. `SetEnabled(true)` then restored the Bell Tower name on the next sync.
- Started and stopped (verified by PID plus creation ticks before stopping): the loopback proxy (Node) and the backend; earlier harness attempts that failed before the backend started left no process. The private home and data directory were removed. The user's running Codex, Claude, Bell Tower and Node processes were not touched.
- Not natively exercised: an elevated peer (needs an elevated Codex backend; stand-in tests only), a fork or delegated thread (needs a model), a real backend restart, and the per-open connection count against the real backend (stand-in `Accepted` only). The permission pump was not run natively because no approval wait can occur without a model; only its status read was.

## Notes and limits

- Work-side `RuntimeWorkFacts` are not produced yet: nothing in the tray feeds `StateStore.Drain` a `ConversationEvidenceSnapshot`, so the status reads here feed the permission and name policies only. Feeding work facts is part of the wiring that also needs the 4.2 binder.
- A conversation whose Codex home differs from the tray's resolved home is refused by the initialize home check (`HomeMismatch`), not guessed; per-conversation home is not persisted anywhere yet.
- `bell-tower native-sync` (`NativeSyncCommand`) builds its own default opener, so the CLI path uses the same selection; no backoff state carries across CLI runs.
