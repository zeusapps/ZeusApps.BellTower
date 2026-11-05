<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-022 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the report write (as reported by the reviewer: "the client refused the write"). -->

```
status: done
role / client / model: code-analyzer / Claude Code (Agent SDK subagent; version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.1 (DEV-034); ANALYSIS-2200 (warning), 2201 (warning), 2202 (warning), 2203 (suggestion), 2204 (suggestion), 2205 (suggestion), 2206 (suggestion)
changed or report paths: reserved path D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-022.md was NOT written (the client refused the write). The full report text is below for the orchestrator to save verbatim.
checks: targeted dotnet test --no-build (CodexTransportTests|CodexNameTests) x5 -> passed (28/28 each run); dotnet format --verify-no-changes --include <8 changed files> -> passed; disposable AF_UNIX probe (socket creation-time tunneling, SIO_AF_UNIX_GETPEERPID) -> measured (see report); build -> not run (avoid output contention with concurrent QA; test DLL newer than sources); full suite -> not run; native Codex -> not run (outside analyzer scope)
target identity: before head=a744d27 baseline=a744d27 files=9 diff=5e58adf8aa72 untracked=8/eb6713282a4d; after (10:13:19Z) identical. Transient mismatch at 10:10:50Z: untracked=8/9a04eb110453, CodexSharedEndpoint.cs rewritten at 10:10:56Z by another actor (not the analyzer) and reverted by 10:11:27Z.
limitations / decisions needed: report write refused, so the text is inline. Decision: adopt ANALYSIS-2200 (peer-PID-derived backend identity) in 3.1, or add an explicit home-to-backend resolver to 3.2 acceptance. Decide on ANALYSIS-2201/2202 before 3.2 wiring. Primary documentation for SIO_AF_UNIX_GETPEERPID was not retrieved (Context7 returned nothing); the ioctl result was measured on one Windows 11 build. Confirm which actor wrote CodexSharedEndpoint.cs during review (probably QA mutation work).
next: orchestrator saves the report, triages 2200-2206, and assigns developer fixes or task updates.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-022

## Review target

- Reviewer / role: ANALYSIS-022 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent; client version not exposed to the subagent, and launching `claude` is outside this assignment) / code-analyzer subagent, auto mode / `claude-opus-5-5`
- Date: 2026-11-05T10:12Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-022, independent analysis of DEV-034
- Task and requirement references: task 3.1 ([tasks.md](../tasks.md)); judged for fit with 3.2, 3.3, 3.4 and 4.2; [design.md](../design.md) decisions 2, 4 and 7; [implementation-034](../evidence/implementation-034.md); [native-adoption-009](../evidence/native-adoption-009.md), [native-adoption-010](../evidence/native-adoption-010.md)
- Scope and relevant neighboring paths: `src/BellTower/Providers/CodexConnection.cs`, `ICodexTransport.cs`, `CodexLoopbackTransport.cs`, `CodexSharedEndpoint.cs`, `CodexSharedTransport.cs`, `CodexSharedAccess.cs`; `tests/BellTower.Tests/Providers/CodexStandInServer.cs`, `CodexTransportTests.cs`. Neighbors read: `Providers/AgentModeClassifier.cs`, `Providers/ICodexNameClient.cs`, `Providers/ICodexAttentionClient.cs`, `IO/ProcessProbe.cs`, `Core/NativeNameSchedule.cs`, `Core/NativeAdoption.cs` (CrossCheck), `Sessions/CodexNameService.cs`, `Sessions/CodexPermissionPump.cs`, `Hosts/AttachmentCapture.cs`, `docs/development.md`
- Baseline commit: a744d27
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline a744d27 -Paths src/BellTower/Providers tests/BellTower.Tests/Providers openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-034.md`: HEAD a744d27b9ac8; manifest `M CodexConnection.cs`, `? implementation-034.md`, `? CodexLoopbackTransport.cs`, `? CodexSharedAccess.cs`, `? CodexSharedEndpoint.cs`, `? CodexSharedTransport.cs`, `? ICodexTransport.cs`, `? CodexStandInServer.cs`, `? CodexTransportTests.cs`; tracked diff SHA-256 5e58adf8aa72...; summary `head=a744d27 baseline=a744d27 files=9 diff=5e58adf8aa72 untracked=8/eb6713282a4d` (matches the assignment).
- Before/after identity: the first after-run (10:10:50Z) printed `untracked=8/9a04eb110453`. `CodexSharedEndpoint.cs` hashed `3cd5549f97a6...` instead of `48977529448c...`, and `CodexSharedEndpoint.cs` and `CodexSharedTransport.cs` both had a modification time of 10:10:56Z. The `CodexSharedTransport.cs` bytes were unchanged. I did not cause this: I only read files, ran a verify-only format, ran no-build tests and ran a socket probe in a separate temporary directory outside the repository. Re-runs at 10:11:27Z and 10:13:19Z printed `head=a744d27 baseline=a744d27 files=9 diff=5e58adf8aa72 untracked=8/eb6713282a4d`, and `CodexSharedEndpoint.cs` hashed `48977529448c...` again. **The final identity matches the assigned target.** A transient write by another actor happened during the analysis and was reverted. It was probably concurrent QA mutation work, but I have not confirmed that. My reads of the source happened before the write window. The orchestrator should confirm who wrote the files.
- Prior reports / rechecked findings: none rechecked. This is a first review of DEV-034.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture: transport altitude | Inspection of `CodexConnection` and the new transports | The JSON-RPC layer (ids, the skip loop, `result` extraction) stays in `CodexConnection`. Each transport only connects, sends, receives, verifies and rechecks. `CodexSocket.ReceiveAsync` is shared by both transports. | Right altitude for the message layer. An interface is justified because there are two real implementations (`docs/development.md`, "Add interfaces where there are actual alternate implementations"). The open/connect step above the clients is not covered yet: see ANALYSIS-2201. |
| Architecture: layering | `grep "using BellTower.IO" src/BellTower/Providers` | `CaptureConsumer`, `HookAdapter`, `HookIngestor`, `StatusLineReader` and others already use IO from Providers. Backoff comes from `Core/NativeNameSchedule`. | Consistent with earlier decisions. No Core contamination. |
| Duplication | Comparison of old and new `CodexConnection`, the loopback transport and the shared transport | The old receive loop moved to `CodexSocket`. The two transports repeat `SendAsync`, `ReceiveAsync` and `Dispose` in three one-line members. | Acceptable. A shared base class would add hierarchy for three lines, which `docs/development.md` discourages. No finding. |
| Ownership types for 3.3 | Inspection | `CodexSharedAccess` creates only `CodexConnection` observers. `CodexRuntime`, `CodexRuntimeStore` and the manager are untouched. `Dispose` aborts only Bell Tower's `ClientWebSocket`. | Distinct, as the evidence states. 3.3 can rely on this. |
| Provenance and TOCTOU | Inspection plus a disposable native probe (below) | The backend identity is supplied by the caller and is not bound to the connected socket. The creation-time recheck does not detect a quick re-creation. | ANALYSIS-2200 |
| Measured: AF_UNIX socket file lifecycle | Disposable PowerShell 7 probe in a short temporary directory created and removed by the analyzer. It never touched a Codex process, the repository or the user's data. | On this Windows 11 build: bind creates an `Archive, ReparsePoint` file. Disposing the .NET listener removed the file. Re-binding the same path 1.5 s later produced an identical `CreationTimeUtc`. | Measured: NTFS tunneling preserves the socket's creation time on re-creation within the tunneling window. The developer's caveat is real. |
| Measured: peer process ID | The same probe: a child `pwsh` listened, the parent connected and called `Socket.IOControl(0x58000100 /* SIO_AF_UNIX_GETPEERPID */, null, out[4])` | The returned PID equalled the listener child's PID, not the caller's PID. | Measured on one machine only. I could not retrieve primary documentation: Context7 lookups in the Win32 API libraries returned nothing for `SIO_AF_UNIX_GETPEERPID`, and no Windows SDK `afunix.h` is installed locally. Input to ANALYSIS-2200. |
| Mode classification (decision 7) | Inspection of `AgentModeClassifier` and `NativeAdoption.CrossCheck` | The transport uses the command-line-only `Classify("codex", true, ...)`. Any command line that contains an `app-server` token before `--` is classified as `SharedBackend`. `Arguments` drops the executable, so the image is never checked. | Not using CrossCheck is correct, because task 4.2 says a backend whose launcher exited classifies as unknown and must still be reachable. The missing image check is part of ANALYSIS-2200. |
| Endpoint derivation and overlong paths | Inspection and tests | The path is `<home>/app-server-control/app-server-control.sock`. It requires a fully qualified home, refuses at more than 107 UTF-8 bytes, and refuses a linked control directory. .NET's own `UnixDomainSocketEndPoint` limit is 108 characters (seen in the probe error text). | Sound and fail-closed before any connect. The containment check is effectively dead code: see ANALYSIS-2205. |
| Budgets and backoff | Inspection | Connect, Handshake and Read each default to 3 s. The `initialize` call runs inside CallAsync's own Read budget, so the effective handshake limit is min(Handshake, Read). The current consumers wrap everything in a 3 s total timeout (`CodexNameService` 48, `CodexPermissionPump` 112). `NativeNameSchedule` is reused with a zero steady-state interval and the existing 2/5/15/30/60 s backoff. | Bounded and the backoff is reused correctly. The handshake/read nesting is harmless: no finding. Replacement and backoff interplay: ANALYSIS-2202. |
| Cancellation and disposal | Inspection | Caller cancellation is not counted as a backoff failure. A budget expiry becomes `Timeout` only inside `ConnectAsync`. The handler and invoker are disposed right after the upgrade. In the Microsoft websockets doc example (retrieved through Context7 `/dotnet/docs`), the invoker outlives the call, but the doc does not say that disposing it is unsupported. | The disposal works in the tests and in the native check. The post-handshake error contract is in ANALYSIS-2203. Disposing the invoker early is a hypothesis to check when the unidentified HttpConnection failure is investigated (ANALYSIS-2204). |
| New 1000-message bound | Inspection | `for (skipped = 0; skipped <= 1000; ...)` allows 1001 receives, then throws `InvalidDataException`, which every consumer already catches as `IOException`. On the owned path it replaces an unbounded loop that only the caller's 3 s token used to bound. Bell Tower never subscribes to threads, so a flood of unrelated notifications is not expected. | Safe behavior change: off by one against the constant's wording, with no impact. No finding. |
| No raw logging or persistence | Inspection | Exceptions carry fixed reason text. Inner exceptions such as `JsonException` hold at most a character position. Consumers log only fixed event codes (`native-name-unavailable`, `permission-verification-unavailable`). `CallAsync` returns the cloned `result` to callers and nothing persists it. | Meets decision 4 for 3.1. Field minimization for `thread/read` and `mcpServerStatus/list` falls to 3.2 and 4.2 callers. |
| Parity (AGENTS.md) | Inspection | The Codex-only part sits in `Providers/CodexShared*`. Claude has no shared backend (`AgentModeClassifier.MayShareBackend`). The shared policies (`NativeNameSchedule`, `ProcessIdentity`, `ProcessProbe`, the classifier) stay shared and unchanged. Claude native-title work remains open under 3.4 and is not claimed as a restriction. | Justified Codex-only adapter, with no duplicated per-provider policy. Covers Codex only by design. Nothing in this change applies to Claude. |
| Performance | Inspection only (hypothesis) | Each request runs `EnsureCurrent`: `Process.GetProcessById` + `StartTime`, `OpenProcess` + `NtQueryInformationProcess`, and one `FileInfo` stat. One open is a UDS connect, an HTTP upgrade, `initialize` and three `EnsureCurrent` calls. | Hypothesis: about 1 ms or less of local work per check and a few ms per open. Not measured. Plan: time 200 iterations of `OpenAsync` + `ReadPermissionPendingAsync` against `CodexStandInServer` with a Stopwatch, using the real default provenance on a disposable process. Only worth doing if 3.2 polls faster than once a second. |
| Tests: determinism | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~CodexTransportTests\|FullyQualifiedName~CodexNameTests"` run 5 times. The test DLL timestamp was later than the sources. | 28/28 passed in each of the 5 runs, about 2 s each | Stable when run in isolation. I did not run the full suite under load. See ANALYSIS-2204 for assertion strength and environment sensitivity. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <8 changed files>` | exit 0 | Passed. |
| Build | not run | I did not rebuild, to avoid output contention with the concurrent QA-029 work. The existing test build was newer than every source file. | Not performed (reason stated). |

## Findings

### ANALYSIS-2200: Provenance is not bound to the connected socket; derive the backend from the socket's peer instead of a caller-supplied identity

- Severity: warning
- Related task / requirement: 3.1 (provenance-checked), 3.2 (the missing home-to-backend resolver), design decisions 4 and 7
- Locations: `src/BellTower/Providers/CodexSharedTransport.cs` 16-17, 34-61, 101-116, 125-128 at a744d27 plus working tree; `src/BellTower/Providers/CodexSharedAccess.cs` 28; `src/BellTower/Providers/AgentModeClassifier.cs` 76-79, 166-169
- Observation:
  1. `isBackend(Backend)` checks a PID and creation time that the caller supplies. Nothing ties that process to the server that accepts the connection. A live, genuine backend therefore "verifies" whatever is listening at the path. The only link is the socket file's creation time, which is read before `connect` and compared after it.
  2. Measured on this machine: re-binding the same socket path 1.5 s after removal kept an identical `CreationTimeUtc` (NTFS tunneling). The creation-time half of the endpoint identity therefore misses a quick re-creation. When the backend restarts, only a newly resolved backend identity reveals the replacement. When the socket is replaced while the expected backend is still alive, the creation time misses it. That leaves `initialize.codexHome`, which any responder can echo.
  3. The default provenance accepts any process whose command line contains an `app-server` token. The executable image is never checked, because `Arguments` drops it. Evidence 009 records that the backend image is the managed package copy inside the Codex home.
  4. Production has no way to obtain the identity that `OpenAsync` requires (implementation-034, limitations). The evidence states "Windows offers no direct owner lookup for a Unix-domain socket". That holds before connecting. After connecting, `SIO_AF_UNIX_GETPEERPID` returned the listener's PID in the probe.
- Proposed improvement and benefit: after `ConnectSocketAsync` succeeds and before the WebSocket upgrade, read the peer PID from the connected socket (`IOControl(0x58000100)`) and build the backend identity from it with `ProcessProbe.Identify`. Then apply the existing liveness and `SharedBackend` command-line check, and add an image check: the executable is `codex` and, per evidence 009, preferably lies under the resolved home's managed package root. Keep an optional expected identity only as an extra comparison. Recheck the peer identity in `EnsureCurrent` instead of, or in addition to, the creation time. Benefits:
  - It closes the check-then-connect TOCTOU, because the identity belongs to the actual server.
  - It removes the tunneling blind spot.
  - It makes the transport usable in product terms without a separate home-to-backend resolver. That shrinks 3.2, and the transport can expose `Backend` for 4.2's decision 7 check ("the client is connected to the backend that hosts the conversation").
  - The `CodexSharedAccess` replacement key stays meaningful, as (peer identity, creation time).
- Behavior-preservation risks: the owned loopback path is unaffected. An elevated or otherwise unreadable peer must stay `BackendNotVerified`, which `Inspect` already does through `Unverified` → false. If the ioctl is unavailable on an older supported Windows build, the transport must fail closed with a distinct reason instead of falling back silently. An image check that is too narrow (npm/script shims, a non-managed `codex app-server`) could refuse legitimate backends. Prefer image name plus `SharedBackend` classification, and make the managed-root location diagnostic-only until it is natively proven.
- Evidence type: source observation, plus two measured probe results on one machine. Primary documentation for `SIO_AF_UNIX_GETPEERPID` was not retrieved, so the developer must cite it before relying on it.
- Verification / measurement plan: (a) Add a stand-in test that runs the server in a child process and asserts that the derived identity equals the child's identity, and add a stand-in test where the expected backend is alive but a different process listens, which must be refused. (b) Add a test that re-creates the socket within 1 s and asserts `EndpointReplaced` or a refusal for a specific reason, not `ThrowsAny`. (c) Repeat the native no-quota check from implementation-034 with no injected identity.
- Relationship: new. It answers the stated "no production resolver" gap. My judgment on that gap: the transport is acceptable as a component for 3.1 and was natively exercised. As built, though, it cannot be used in the product until a resolver exists, and task 3.2 does not list one. Either adopt this finding in 3.1 or add the resolver to 3.2 explicitly.

### ANALYSIS-2201: The stated 3.2 integration path does not fit the client interfaces; the connect step is owned-only

- Severity: warning
- Related task / requirement: 3.1 (beneath the existing runtime client boundary), 3.2, 4.2
- Locations: `src/BellTower/Providers/ICodexNameClient.cs` 5; `src/BellTower/Providers/ICodexAttentionClient.cs` 5; `src/BellTower/Providers/CodexConnection.cs` 64-72; `src/BellTower/Sessions/CodexNameService.cs` 47-49; `src/BellTower/Sessions/CodexPermissionPump.cs` 111-113; `evidence/implementation-034.md` (limitations: "pass it to the existing clients via their factory")
- Observation: both client interfaces declare `ConnectAsync(endpoint, token)`, and both services always call it right after the factory returns a client. `CodexSharedAccess.OpenAsync` returns an already connected `CodexConnection`. If 3.2 follows the evidence and returns that connection from the factory, the service's `ConnectAsync(endpoint, token)` throws `InvalidOperationException("already has a transport")`. The services catch that exception, so the result is a silent `unavailable` status, not a crash. The transport boundary is in the right place, but the access decision (owned runtime versus shared home) still sits inside the services as an owned-only call.
- Proposed improvement and benefit: in 3.2, not now, move "obtain a connected client for this attachment" into one small opener. It returns a connected client either from the owned runtime record (endpoint and token) or from `CodexSharedAccess` (home), and it drops `ConnectAsync` from `ICodexNameClient` and `ICodexAttentionClient`. Both services then share the same selection rule. Until then, correct the implementation-034 note so 3.2 does not follow it.
- Behavior-preservation risks: the existing `CodexNameTests` and `CodexPermissionTests` fakes implement `ConnectAsync`, so the fakes must move to the opener. Owned-path behavior is unchanged.
- Evidence type: source observation
- Verification / measurement plan: a 3.2 test where a shared-mode attachment reaches `ReadPermissionPendingAsync` through the services, plus the unchanged owned-path tests.
- Relationship: new.

### ANALYSIS-2202: The `Replaced` signal is lost when a stale identity is tried first, and that try also backs off the whole home

- Severity: warning
- Related task / requirement: 3.1 (endpoint replacement), 4.2 (reconnect, dead owner)
- Locations: `src/BellTower/Providers/CodexSharedAccess.cs` 30, 40-54 (`verified.Remove` on `EndpointReplaced` or `BackendNotVerified`), 57-63; `tests/BellTower.Tests/Providers/CodexTransportTests.cs` 208-227
- Observation: the sequence is: verified (B1, t1); the backend restarts as B2; the caller still holds B1, so `OpenAsync(home, B1)` fails `BackendNotVerified`. That failure removes the home's verified entry and records a backoff failure. The next `OpenAsync(home, B2)` within 2 s is `BackedOff`. Once the backoff expires it succeeds with `Replaced = false`, because the earlier entry is gone. Holding an old identity is the most likely caller pattern after a restart, so the documented replacement report is missed exactly when it matters. The test covers only the order where the caller already has B2.
- Proposed improvement and benefit: keep the last successfully verified endpoint until a different one is verified, and do not remove it on failure. Compute `Replaced` against that endpoint. Optionally, do not count a `BackendNotVerified` that comes from a stale identity toward the home's backoff when no connection was attempted. If ANALYSIS-2200 is adopted, a caller-held identity no longer exists and this problem largely disappears.
- Behavior-preservation risks: `Replaced` would then also be true after a long outage followed by a new backend. That is the intended meaning.
- Evidence type: source observation
- Verification / measurement plan: a stand-in test that opens with B1, swaps the predicate to B2, opens with B1 (refused), advances the injected clock and opens with B2, then asserts `Replaced == true`.
- Relationship: new.

### ANALYSIS-2203: The strict error contract covers only the handshake

- Severity: suggestion
- Related task / requirement: 3.1 (bounded and diagnosable), 5.x diagnostics ("runtime access" with honest reasons)
- Locations: `src/BellTower/Providers/ICodexTransport.cs` 15-16; `src/BellTower/Providers/CodexConnection.cs` 91-98, 111-136; `src/BellTower/Providers/CodexSharedTransport.cs` 118-121
- Observation: the interface says a strict transport reports failures as `CodexTransportException`, but only `ConnectAsync` maps them. After the handshake:
  - an expired Read budget surfaces as `OperationCanceledException` even though the caller did not cancel;
  - a dropped backend surfaces as `WebSocketException`, which is not an `IOException`;
  - a malformed reply surfaces as `JsonException`.
  During the handshake, a JSON-RPC `error` to `initialize` surfaces as a plain `IOException`. The current consumers catch all of these, so behavior is safe, but a later diagnostic cannot report the reason. The `Strict` flag exists only to switch this mapping on.
- Proposed improvement and benefit: map exceptions in one helper used by both `ConnectAsync` and `CallAsync`. Map budget expiry to `Timeout`, socket and WebSocket failures to `Refused` or `EndpointReplaced`, and parse failures to `Malformed`. If owned-path callers tolerate it, apply the mapping to all transports and remove `Strict`: `CodexTransportException` is an `IOException`, and both services already catch `IOException`.
- Behavior-preservation risks: `CodexNameTests` may assert on specific exception types for the owned path. Keep `InvalidDataException` for the non-loopback endpoint check, which `NativeTransportRejectsNonlocalEndpointsBeforeConnecting` asserts.
- Evidence type: source observation
- Verification / measurement plan: stand-in tests for a post-handshake silent reply (→ `Timeout`) and a server that closes after `initialize` (→ a reasoned failure).
- Relationship: new.

### ANALYSIS-2204: Several transport tests assert weakly or depend on the environment

- Severity: suggestion
- Related task / requirement: 3.1 acceptance (endpoint replacement, no unrelated connections, backend survives disposal)
- Locations: `tests/BellTower.Tests/Providers/CodexTransportTests.cs` 10, 14-20, 229-246, 248-265, 267-288
- Observation:
  - `RecreatedSocketIsDetectedAsReplacement` asserts `ThrowsAnyAsync<Exception>`. It passes when the old server dies, without exercising the creation-time path. Given the measured tunneling, that path probably never fires there.
  - In `DisposingTheObserverLeavesTheBackendServing`, line 256 is a tautology. Line 264 is satisfied by the file merely existing. `CodexStandInServer.Serving` exists but is unused.
  - `ConcurrentConnectionsDoNotShareState` opens the three connections one after another, so concurrent `OpenAsync` (the schedule check-then-act race, `verified` updates) is not exercised.
  - All shared tests root their homes in `Path.GetTempPath()`. A temp path longer than about 63 characters, for example a long account name, makes every shared test fail with `PathTooLong`.
  - Handshake and Read budgets of 400 ms, with a 150 ms delay for the "slow" thread, are the most plausible place for timing failures under full-suite load (hypothesis). The developer saw one unidentified `HttpConnection` failure. The only new `HttpConnection` user is the shared transport's own `SocketsHttpHandler`, which is disposed immediately after the upgrade (hypothesis, not reproduced in 5 isolated runs).
- Proposed improvement and benefit: assert specific failures; make the disposal test assert `server.Serving` after the observer is disposed, or use a separate long-lived connection; start the three opens with `Task.WhenAll`; use a short dedicated root (for example a fixed short prefix under the system drive's temp folder, checked by an explicit precondition that names the length); raise Read and Handshake to at least 2 s except in the timeout test; record the identity of the failing test if the `HttpConnection` failure recurs.
- Behavior-preservation risks: tests only.
- Evidence type: source observation, plus the measured tunneling probe; timing is a hypothesis.
- Verification / measurement plan: run the targeted filter 20 times under `--parallel` with the full suite, and run once with `TMP` set to a long path to confirm the precondition message.
- Relationship: new. Related to ANALYSIS-2200 (b).

### ANALYSIS-2205: Home key normalization is inconsistent, and the containment check is dead code

- Severity: suggestion
- Related task / requirement: 3.1
- Locations: `src/BellTower/Providers/CodexSharedEndpoint.cs` 23-28; `src/BellTower/Providers/CodexSharedAccess.cs` 20, 30; `src/BellTower/Core/NativeNameSchedule.cs` 13
- Observation:
  - `verified` is keyed by the raw home string with `OrdinalIgnoreCase`, while the backoff schedule uses the same raw string with `Ordinal`. Spellings that differ only in case or a trailing separator therefore share one replacement record but get separate backoffs.
  - The containment check in `SocketPath` can never trigger for constant path components. It also wrongly refuses a drive-root home, because `home + separator` doubles the separator.
- Proposed improvement and benefit: normalize the home once, with the same `GetFullPath` and trailing-separator trim `SocketPath` already uses, and key both maps by the normalized value. Remove the containment check. The linked-directory refusal is the guard that matters.
- Behavior-preservation risks: none for realistic homes.
- Evidence type: source observation
- Verification / measurement plan: a unit test where two spellings of one home share their backoff and replacement state.
- Relationship: new.

### ANALYSIS-2206: The implementation evidence contradicts itself about the full-suite result

- Severity: suggestion
- Related task / requirement: 3.1 evidence
- Locations: `openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-034.md` (Tests: "1465 tests, 1464 passed, 1 failed. See the limitations section"; Limitations: no entry); `reports/findings.md` ledger line for DEV-034 ("1465/1465 (one earlier unidentified HttpConnection failure not reproduced)")
- Observation: the evidence points to a limitations entry that does not exist, and its count differs from the ledger.
- Proposed improvement and benefit: add the failing test's name if it is known, the run conditions, and the passing re-run to the evidence, so QA and future readers can tie it to ANALYSIS-2204.
- Behavior-preservation risks: none.
- Evidence type: source observation
- Verification / measurement plan: inspect the document.
- Relationship: new.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not applicable (first review of DEV-034) |

## Assessment and limitations

The shape of the change is right. A message-level `ICodexTransport` under an unchanged JSON-RPC layer keeps the owned authenticated loopback path intact and gives the shared path a home-derived endpoint, bounded budgets, reuse of the existing backoff, fail-closed reasons and observer-only disposal. Ownership types stay separate for 3.3. Layering and parity are consistent with `docs/development.md` and AGENTS.md: the adapter is Codex-only and justified, and the shared policies are reused without duplication. `UseProxy = false` correctly keeps proxy environment variables from redirecting the local socket.

The main issue is provenance (ANALYSIS-2200). The backend identity is supplied by the caller rather than bound to the connected socket. The measured NTFS tunneling shows that the creation-time recheck cannot stand in for that binding. On this machine, the peer-PID ioctl both closes the gap and supplies the resolver that implementation-034 leaves for 3.2. I recommend that the orchestrator decide whether to fold that into 3.1 or to add the resolver explicitly to 3.2 acceptance. The interface follow-up (ANALYSIS-2201) and the `Replaced` semantics (ANALYSIS-2202) should be settled before 3.2 wiring, so that 3.2 does not build on the integration note in the evidence.

Not performed: a build (to avoid contention with concurrent QA), the full suite, any native Codex run (outside analyzer scope), and performance measurement (hypotheses are labeled). Primary documentation for `SIO_AF_UNIX_GETPEERPID` could not be retrieved through Context7. The probe result covers one Windows 11 build.

Writes:
- None in the repository: the client refused the write of this report.
- Disposable probe scripts in the session scratchpad.
- A probe directory under the system temp folder, created and then removed. A short path was needed because the scratchpad path exceeds the AF_UNIX length limit.

A transient change to `CodexSharedEndpoint.cs` by another actor occurred during the analysis and was reverted (see Review target). The orchestrator owns triage and ledger status.
