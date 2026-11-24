# Implementation 034: local shared-Codex transport (task 3.1)

Developer evidence, not reviewer acceptance. Baseline HEAD a744d27. The working tree is left uncommitted.

## What was built

A message-level transport boundary beneath the existing name and attention clients (`ICodexNameClient`, `ICodexAttentionClient` are unchanged):

- `ICodexTransport` (Providers): connect, send one text message, receive one bounded text message (1 MiB), optional per-phase `CodexTransportBudget` (connect, handshake, read; default 3 s each), `VerifyInitialized` and `EnsureCurrent` hooks. Failures from a strict transport are `CodexTransportException` (an `IOException`, so existing catch blocks still apply) with a `CodexTransportFailure` reason: `EndpointMissing`, `PathTooLong`, `Refused`, `Timeout`, `Malformed`, `HomeMismatch`, `BackendNotVerified`, `EndpointReplaced`, `BackedOff`. Messages carry the reason only, never response or credential content.
- `CodexConnection` now runs the JSON-RPC layer over a transport. The parameterless constructor plus `ConnectAsync(endpoint, token, ct)` is the unchanged owned path and builds `CodexLoopbackTransport` (authenticated `ws://127.0.0.1` WebSocket, same endpoint validation and `InvalidDataException`). `CodexConnection(ICodexTransport)` plus `ConnectAsync(ct)` is the new path that 3.2 and 4.2 use. Parsing keeps only the `result` element of the matching response; unmatched messages are skipped, now bounded at 1000. Raw responses are never logged or persisted.
- `CodexSharedEndpoint`: the socket path is `<resolved home>/app-server-control/app-server-control.sock`, derived only from an absolute resolved Codex home. No enumeration, no endpoint from a payload. A path over 107 UTF-8 bytes (Windows `sun_path` limit) fails closed with `PathTooLong` before any connect. A control directory that is a link is refused.
- `CodexSharedTransport` (non-owning): connects to the Unix-domain socket via `SocketsHttpHandler.ConnectCallback` with the standard `ClientWebSocket` upgrade. Provenance before use: the caller-supplied backend identity (PID plus creation time) must be live and its command line must classify as a Codex shared backend through the existing `AgentModeClassifier`; the `initialize` response `codexHome` must equal the resolved home (case-insensitive on Windows, trailing separator and `\\?\` tolerant); a missing or non-string `codexHome` is `Malformed`. The backend identity and the socket file's creation time are rechecked after the handshake and before every request, so a restarted backend or recreated or removed socket raises `EndpointReplaced` instead of reusing the connection. It sends only `initialize`, `initialized` and the caller's requests; it has no resume, turn, approval or shutdown surface, and `Dispose` closes only Bell Tower's WebSocket.
- `CodexSharedAccess`: per-home opener that reuses `NativeNameSchedule` (existing 2, 5, 15, 30, 60 s backoff, zero steady-state interval) so a failed home is not retried until its backoff expires (`BackedOff`, no connection attempt), and tracks the last verified endpoint (backend identity plus socket creation time) per home; `OpenAsync` returns `Replaced = true` when it differs. Ownership types stay distinct: this class creates only observer connections; `CodexRuntime`/`CodexRuntimeStore` and the manager are untouched, so 3.3 can rely on them.
- Nothing was wired into `CodexNameService` or `CodexPermissionPump` (3.2) and detach/exit logic is unchanged (3.3).

## Parity

The shared backend transport is Codex-specific by design (Claude Code has no shared backend; design decision 4 adds no Claude native-title transport by assumption). The Codex-only part is confined to the provider adapter (`Providers/CodexShared*`). The backoff and process-identity policies it reuses (`NativeNameSchedule`, `ProcessIdentity`, `AgentModeClassifier`) are shared and provider-neutral; no shared policy became Codex-only. The Claude native-title write surface remains unfinished work for task 3.4, not a claimed restriction.

## Tests

`tests/BellTower.Tests/Providers/CodexStandInServer.cs` is an in-process WebSocket server over a Unix-domain socket (or loopback TCP for the owned case); it never touches a real runtime. `CodexTransportTests` (19 tests): successful initialize with only the derived socket contacted (a second stand-in at another home receives zero connections), no resume/turn methods, home mismatch, tolerant home comparison, unverified or dead process identity (injected and the real default provenance with this test process and a wrong creation time), missing socket with backoff, backoff expiry and escalation, backoff recovery, refused (bound, not listening), handshake timeout within budget, malformed response and missing home, overlong path, path derivation rules, backend replacement (old connection rejected, reopen reports replaced), recreated socket, disposal leaves the backend serving and reopenable, three concurrent connections across two homes with independent answers, and an owned-loopback regression (Authorization header, name read, second connect rejected). Existing `CodexNameTests`, including `NativeTransportRejectsNonlocalEndpointsBeforeConnecting`, pass unchanged.

Results: targeted `CodexTransportTests|CodexNameTests` 28 passed; Providers, Sessions and Codex host tests 608 passed; full suite 1465 tests, 1464 passed, 1 failed. See the limitations section for the failed test.

## Native check (Codex CLI 0.166.0, Windows, shared managed-daemon backend)

Performed. Private disposable home with a short path (about 30 characters), dummy `OPENAI_API_KEY`, `[analytics] enabled = false`, `[features] plugins = false`, daemon `settings.json` with remote control and auto-update off (as in evidence 010). Egress was fail-closed through a loopback proxy (`ALL_PROXY`/`HTTP(S)_PROXY`, answering 403 to everything); its log recorded no request. No model request, turn or user home was involved.

- Started: loopback proxy (Node) PID 4336, creation ticks 639294690648755818; backend `codex.exe app-server --listen unix:// --managed-daemon` PID 26212, creation ticks 639294690754520796. The sandbox-alias helper warning for a home under the temp directory was non-fatal.
- Product transport (`CodexSharedAccess` with default provenance, no injected predicates) against that home:
  - matching live backend identity: connected, `initialize` reported the private home and passed, `initialized` sent, `thread/loaded/list` returned an object; `Replaced = False`.
  - same identity with a creation time off by one tick: refused `BackendNotVerified`.
  - a second private home without a socket: refused `EndpointMissing` (no connection to the live backend).
  - a home with a 90-character component: refused `PathTooLong`.
- Cleanup: the backend and the proxy were stopped after verifying PID plus creation ticks; the private home and the extra directory were removed. The user's running Codex processes (started 8:37 in this session's listing, and the earlier one) and Claude and Bell Tower processes were not touched.
- Not natively exercised: a real backend restart or socket recreation and a real home mismatch (covered by stand-in tests only), and a restart on this Windows version where NTFS file tunneling could preserve a socket's creation time; the backend identity recheck covers that case.

## Limitations and notes

- Production does not yet resolve a backend `ProcessIdentity` for a home. Windows offers no direct owner lookup for a Unix-domain socket, so the 3.2 binder must supply the identity (for example from the process table, mode `SharedBackend`) and the transport verifies it live and by command line. This is a dependency for 3.2, not a restriction claim.
- `ICodexNameClient.ConnectAsync(endpoint, token)` is unchanged; 3.2 should open `CodexConnection` through `CodexSharedAccess.OpenAsync` and pass it to the existing clients via their factory.
- No lock files were rewritten (`git status` shows none).
