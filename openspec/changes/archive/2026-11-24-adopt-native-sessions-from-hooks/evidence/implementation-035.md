# Implementation 035: fix cycle 1 for DEV-034 (task 3.1)

Developer evidence, not reviewer acceptance. Baseline HEAD a744d27. The working tree is left uncommitted. Fixes below await independent verification; none is marked closed here. implementation-034 is unchanged history; its corrections are recorded in this file.

## ANALYSIS-2200 / QA-2901: provenance bound to the connected socket

- After the Unix-domain socket connects and before the WebSocket upgrade, `CodexSharedPeer.PeerPid` reads the server's process ID with the `SIO_AF_UNIX_GETPEERPID` ioctl (0x58000100) on the connected socket. The backend identity is `ProcessProbe.Identify(pid)` (PID plus creation time). It must be live, its image name must be `codex`, and its command line must classify as `SharedBackend` through `AgentModeClassifier`. Nothing is sent to the peer before this passes. The managed package root is not required (and not recorded; image name only).
- Documentation: no learn.microsoft.com page for the ioctl was found. Secondary sources found: the Microsoft-generated Windows API metadata docs (`microsoft.github.io/windows-docs-rs`, constant `SIO_AF_UNIX_GETPEERPID` = 1476395264 = 0x58000100) and Wine developer notes stating the Windows SDK `afunix.h` declares it as `_WSAIOR(IOC_VENDOR, 256)`. The behavior is also measured: analysis-022 and the tests here show the ioctl returns the listener's PID on this Windows 11 build (child-process test). The implementation is fail-closed regardless.
- Distinct refusals: `PeerUnavailable` (ioctl unavailable or returns no PID, or the peer is unreadable such as elevated), `BackendNotVerified` (not live, not the codex image, wrong command line, or differs from an optional expected identity). There is no silent fallback.
- `CodexSharedAccess.OpenAsync(home, ct, expected = null)` works from the resolved home alone and returns `(Connection, Replaced, Backend)`; the transport also exposes `Backend`. The caller-supplied identity is only an optional extra comparison. This removes the "no production home-to-backend resolver" gap for 3.2 and 4.2.
- Recheck: `EnsureCurrent` (after the handshake and before every request) requires the verified backend to be live and identifies the process currently listening at the path with a short connect that sends nothing. A different process, a removed socket or a refused connect raises `EndpointReplaced`. The socket creation-time comparison was dropped: NTFS tunneling keeps it across a quick re-creation, so it adds nothing beyond the peer check. Side effect: each request causes one extra connect and immediate close on the backend's control socket.

## QA-2900: replacement tests with specific outcomes

New or rewritten tests (`CodexTransportTests`, 26 tests; `ChildListener` in `CodexStandInServer.cs` is a real second `pwsh` process that listens on a socket):
- `DifferentProcessListeningAtTheSamePathAfterVerificationIsDetected`: the verified stand-in is replaced immediately by another process at the same path (inside the tunneling window); the next request asserts `EndpointReplaced`.
- `ConnectedSocketReportsTheListeningChildProcess`: derived identity equals the child's identity, not this process.
- `ExpectedIdentityThatIsAliveButNotTheListenerIsRefused` (`BackendNotVerified`, no request sent) and `ExpectedIdentityEqualToTheListenerIsAccepted`.
- `DefaultProvenanceRequiresTheCodexImageEvenWithASharedBackendCommandLine`: the child's command line classifies as `SharedBackend`, so only the image check can refuse.
- `DefaultProvenanceRefusesAPeerThatIsNotACodexSharedBackend`, `UnavailablePeerLookupFailsClosedWithItsOwnReason` (`PeerUnavailable`), `EndedBackendDropsTheOldConnection`, `RemovedSocketIsReportedAsReplaced`.
- `DisposingTheObserverLeavesTheBackendServing` now asserts `server.Serving` and a request on a second live connection (the tautologies are gone).

Mutants (single-line edits, rebuilt, `CodexTransportTests` run, then restored):

| Mutant | Result |
| --- | --- |
| Remove the peer-identity recheck in `EnsureCurrent` | killed: `DifferentProcessListeningAtTheSamePathAfterVerificationIsDetected`, `RemovedSocketIsReportedAsReplaced` |
| Remove the image-name check | killed: `DefaultProvenanceRequiresTheCodexImageEvenWithASharedBackendCommandLine` |
| Force the `Replaced` comparison in `CodexSharedAccess` to false | killed: `ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween` |
| Remove the liveness recheck in `EnsureCurrent` | killed: `EndedBackendDropsTheOldConnection` |
| Remove the expected-identity comparison | not run: compile error (parameter unread); the test `ExpectedIdentityThatIsAliveButNotTheListenerIsRefused` covers it by design, not mutation-verified |

## ANALYSIS-2202: `Replaced` after restart

`CodexSharedAccess` keeps the last successfully verified backend per home, never clears it on failure, and computes `Replaced` against it. With the backend derived from the socket, a stale caller-held identity can no longer cause a refusal before a connection attempt, so that case collapses: every refusal now follows an attempt and the existing backoff applies to it legitimately. Test: `ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween` (verify B1, backend gone and a failing attempt, restart as B2 with a different peer, open: `Replaced == true`, then `Replaced == false` again).

## Evidence corrections

- ANALYSIS-2206: implementation-034 stated "1465 tests, 1464 passed, 1 failed" and pointed to a limitations entry that does not exist; its own ledger line says 1465/1465. The truth as far as recorded: the full-suite failure seen by the predecessor was not identified. QA-029 observed a failure in its own full run: `AudioQueueTests.ResolvedReminderDuringAggregateToneCancelsAggregateSpeech` (Tray audio timing, passes alone 44/44). In this cycle one full-suite run had one failure: `WindowsUserIdleSourceTests.PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation` (Tray, timing under load, unrelated to Providers), passing alone (9/9). No failure in `CodexTransportTests` or `CodexNameTests` has been reproduced; the predecessor's "unidentified HttpConnection failure" remains unattributed.
- ANALYSIS-2201: implementation-034's note that 3.2 should pass the opened connection to the existing clients through their factory is wrong. `ICodexNameClient.ConnectAsync` and `ICodexAttentionClient.ConnectAsync` are owned-only (`endpoint`, `token`) and throw on a client that already has a transport. Task 3.2 now carries a single opener; do not follow the earlier note.

## Results

- Targeted `CodexTransportTests|CodexNameTests`: 35 passed.
- Full suite: 1472 tests, 1471 passed, 1 failed (`WindowsUserIdleSourceTests...`, unrelated Tray timing); rerun alone 9/9 passed.
- Format: `dotnet format --verify-no-changes --include` on all changed files: passed. The formatter also normalized line endings of the changed sources. No lock file changed.

## Native check (Codex CLI 0.166.0, Windows, installed `codex.exe` from the global npm install)

Performed, no quota. Private disposable short home in the temp area, dummy API key, analytics off, plugins off, daemon settings with remote control and auto-update off, all proxy variables pointing to a loopback proxy answering 403 to every request (log stayed empty: 0 requests). No turns or model requests. Product `CodexSharedAccess` with default provenance, no injected identity, one instance:
- Backend 1 started (PID 38924, creation ticks 639294781250581359). `OpenAsync(home)` connected; derived backend equals the started PID and ticks; `Replaced = False`; `thread/loaded/list` returned an object.
- Backend 1 stopped (identity verified; stale socket file remained). An intermediate open failed (a refusal exception, surfaced by PowerShell as `MethodInvocationException`; the specific reason was not printed). Backend 2 started (PID 2180, creation ticks 639294781259969007). The later open connected with `Replaced = True` and derived backend equal to backend 2.
- Also started: loopback proxy (Node) PID 20980, ticks 639294781248925410. Earlier harness attempts that failed on an assembly path started proxies (PID 15516, 35180) that the script's cleanup stopped after identity checks. All were stopped; the private home and logs were removed. The user's own `codex`, `node`, `claude` and `bell-tower` processes were not touched.
- Not natively exercised: elevated peer (`PeerUnavailable`), which is covered by a stand-in test only.
