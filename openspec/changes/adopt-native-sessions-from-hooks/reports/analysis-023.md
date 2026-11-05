<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-023 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the report write (as reported by the reviewer: "the client refused the write"). -->

```
status: done
role / client / model: code-analyzer / Claude Code (Agent SDK subagent; client version not visible to the subagent) / claude-opus-5-5
task / finding IDs: task 3.1 recheck of DEV-035. Rechecked: ANALYSIS-2200 verified; ANALYSIS-2202 verified; QA-2900 test design adequate, but the expected-identity comparison is not mutation-verified. New: ANALYSIS-2300 (suggestion, regression from the fix), ANALYSIS-2301 (suggestion), ANALYSIS-2302 (suggestion)
changed or report paths: reserved path D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-023.md was NOT written (the client refused the write). Full report text is below for the orchestrator to save verbatim.
checks: targeted dotnet test --no-build (CodexTransportTests|CodexNameTests) x3 -> passed 35/35 each; dotnet format --verify-no-changes --include <9 changed files> -> passed; disposable timing probe of the recheck primitives (built assembly copy, child pwsh listener) -> measured; build -> not run; full suite -> not run; native Codex -> not run (outside analyzer scope)
target identity: before head=a744d27 baseline=a744d27 files=11 diff=5e58adf8aa72 untracked=10/a837b9d32cd2. After: first run at 12:29:20Z gave untracked=10/594328da860c (another actor rewrote CodexSharedTransport.cs, CodexSharedAccess.cs and CodexSharedPeer.cs at 12:29:11-24Z, then restored them). Re-run at 12:29:55Z gave head=a744d27 baseline=a744d27 files=11 diff=5e58adf8aa72 untracked=10/a837b9d32cd2, identical to before.
limitations / decisions needed:
- The report write was refused, so the text is inline.
- No primary Microsoft documentation for SIO_AF_UNIX_GETPEERPID: one web search plus a fetch of the afunix index (404); no local SDK.
- Decision needed: may an elevated Codex backend be observed? It is probably accepted today, not refused (ANALYSIS-2302).
- bell-tower.dll in the test output was rebuilt at 12:29:39Z by another actor, possibly from mutated sources. Rebuild before the next --no-build run.
next: orchestrator saves the report and triages ANALYSIS-2300 to 2302. None blocks the 3.1 boundary.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-023 (recheck of DEV-035, task 3.1)

## Review target

- Reviewer / role: ANALYSIS-023 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent; the subagent cannot see the client version, and launching `claude` is outside this assignment) / code-analyzer subagent, auto mode / `claude-opus-5-5`
- Date: 2026-11-05T12:30Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-023, recheck after fix cycle 1 (DEV-035)
- Task and requirement references: task 3.1 ([tasks.md](../tasks.md)); [design.md](../design.md) decisions 4 and 7; [implementation-035](../evidence/implementation-035.md), [implementation-034](../evidence/implementation-034.md); [analysis-022](analysis-022.md), [qa-029](qa-029.md); [native-adoption-009](../evidence/native-adoption-009.md), [native-adoption-010](../evidence/native-adoption-010.md)
- Scope and relevant neighboring paths:
  - Target: `src/BellTower/Providers/CodexSharedPeer.cs`, `CodexSharedTransport.cs`, `CodexSharedAccess.cs`, `CodexSharedEndpoint.cs`, `ICodexTransport.cs`, `CodexLoopbackTransport.cs`, `CodexConnection.cs`; `tests/BellTower.Tests/Providers/CodexStandInServer.cs`, `CodexTransportTests.cs`.
  - Neighbors read: `src/BellTower/IO/ProcessProbe.cs`, `src/BellTower/Providers/AgentModeClassifier.cs`, and `.github/workflows/validate.yml` (runner and shell).
- Baseline commit: a744d27
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline a744d27 -Paths src/BellTower/Providers tests/BellTower.Tests/Providers openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-034.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-035.md`:
  - HEAD: a744d27b9ac8.
  - Manifest: `M CodexConnection.cs`. Untracked: `implementation-034.md`, `implementation-035.md`, `CodexLoopbackTransport.cs`, `CodexSharedAccess.cs`, `CodexSharedEndpoint.cs`, `CodexSharedPeer.cs`, `CodexSharedTransport.cs`, `ICodexTransport.cs`, `CodexStandInServer.cs`, `CodexTransportTests.cs`.
  - Tracked diff SHA-256: 5e58adf8aa72...
  - Summary: `head=a744d27 baseline=a744d27 files=11 diff=5e58adf8aa72 untracked=10/a837b9d32cd2`. This matches the assignment.
- Before/after identity:
  - First after-run (12:29:20Z): `untracked=10/594328da860c`. `CodexSharedTransport.cs`, `CodexSharedAccess.cs` and `CodexSharedPeer.cs` had modification times of 12:29:11Z, 12:29:18Z and 12:29:24Z.
  - Moments later the per-file hashes were back to the original values. A re-run at 12:29:55Z printed `head=a744d27 baseline=a744d27 files=11 diff=5e58adf8aa72 untracked=10/a837b9d32cd2`. **The final identity is identical to the assigned target.**
  - I did not cause this. My only writes were disposable files in the session scratchpad and the system temp folder. The assignment anticipated it as concurrent QA mutation, which is consistent with what I saw, but I have not confirmed the actor.
  - My source reads finished before that window.
  - My test runs and the timing probe both used the test-output build from 10:21:45Z. That build is newer than every reviewed source, which were last written at 10:21:42Z.
  - Another actor rebuilt `bell-tower.dll` in the test output at 12:29:39Z, after my runs. That build may come from a mutated source, so the orchestrator should rebuild before the next `--no-build` run.
- Prior reports / rechecked findings: [analysis-022](analysis-022.md) ANALYSIS-2200 and ANALYSIS-2202; the test design for [qa-029](qa-029.md) QA-2900; and regression from the fix.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Peer-bound provenance | Inspection of `CodexSharedPeer.cs` 17-39 and `CodexSharedTransport.cs` 103-139 | `ConnectSocketAsync` connects the Unix-domain socket and then calls `Verify`. `Verify` runs the ioctl and `ProcessProbe.Identify`, the optional expected identity comparison, and `isBackend`. All of this happens before the HTTP upgrade, so nothing is sent before verification. The verified identity is exposed as `Backend`. | ANALYSIS-2200 core resolved. See Re-verification. |
| Peer-PID race (ioctl, then identity read) | Inspection plus reasoning | `Identify(pid)` reads the creation time after the ioctl, so the PID could in principle be reused in between. Two things limit this. The window only matters if the listening process exits in between, and then the connection just made is dead, so `initialize` fails with `Refused`/`Timeout`/`Malformed`. Each later request also probes the listener again, which fails safe. Exploiting the window would need the same user to deliberately hand a listening socket handle to a second process and then win a PID reuse. | Race-free enough for this threat model. An optional cheap hardening, not a finding: refuse an identity whose creation time is later than the moment the connect began, because a reused PID always belongs to a newer process. |
| Image and command-line strength | Inspection of `CodexSharedTransport.cs` 215-232 and `AgentModeClassifier` | Three checks apply. The process is live by PID plus creation time. `ProcessName` is `codex`, ignoring case, with no location requirement. `Classify("codex", true, Arguments(cmdline))` must return `SharedBackend`. The npm shim (`codex.cmd`, then `node`, then the native `codex.exe`) puts the native image behind the socket, and peer derivation picks the actual listener rather than an ancestor. The native run in implementation-035, with the npm-installed CLI and the managed daemon, passed with no injected identity. | The strength is right: image name plus classification, with the managed root not required, as analysis-022 recommended. A future binary rename fails closed as `BackendNotVerified`. The reason is not distinguishable in diagnostics; see ANALYSIS-2302. |
| Fail-closed on ioctl unsupported or peer unreadable | Inspection and test `UnavailablePeerLookupFailsClosedWithItsOwnReason` | `PeerPid` maps `SocketException`, `PlatformNotSupportedException`, `NotSupportedException`, `ObjectDisposedException` and `InvalidOperationException` to null, and maps a PID of 0 or less to null. Null becomes `PeerUnavailable` ("could not be determined"). A failed `Identify` becomes `PeerUnavailable` ("not readable"). Neither path falls back. `OpenAsync` records the failure, so the existing backoff bounds retries. | Correct for an unsupported ioctl and for an unreadable process. The claim that an elevated peer yields `PeerUnavailable` is not established and is probably wrong; see ANALYSIS-2302. |
| Primary documentation for `SIO_AF_UNIX_GETPEERPID` | One web search (`SIO_AF_UNIX_GETPEERPID afunix.h learn.microsoft.com`) and one fetch of `learn.microsoft.com/en-us/windows/win32/api/afunix/`. I also checked locally for `Windows Kits\10\Include` | The search returned no Microsoft Learn page, only mingw-w64 `afunix.h` (`_WSAIOR(IOC_VENDOR, 256)`, https://mingw.googlesource.com/mingw-w64/+/refs/heads/v14.x/mingw-w64-headers/include/afunix.h) and GLib's fallback definition (https://fuchsia.googlesource.com/third_party/glib/+/refs/heads/upstream/2.73.2/gio/giowin32-afunix.h). The afunix header index URL returns 404. No Windows SDK is installed locally. | No primary Microsoft documentation located, which confirms the developer's statement. For the risk and the required diagnostics, see Assessment. |
| Recheck cost (measured, Bell Tower side) | Disposable probe. I copied the 10:21:45Z build of `bell-tower.dll` into the scratchpad and loaded it in PowerShell 7.6 (.NET 10.0.14). A child `pwsh` listened on a short socket under the system temp folder, with a 90 s self-exit. For 300 iterations the probe ran: socket, connect, the product's `CodexSharedPeer.Identify`, close; then the product's `ProcessProbe.Inspect`, `ProcessName`, `ProcessProbe.CommandLine` and `AgentModeClassifier.Classify`. Cleanup checked identity and removed the directory. | Probe: median 0.112 ms, p95 0.185 ms, max 22.5 ms (first iteration, JIT). The `isBackend` equivalent: median 0.088 ms, p95 0.124 ms, max 8.5 ms. There were 0 identity mismatches in 300 runs. The ioctl returned the listener's PID even when the child had not yet accepted the connection. | Measured, one machine, interpreter overhead included. Client-side cost is negligible. The cost on the backend side was not measured (no Codex process was used); see ANALYSIS-2300. |
| Recheck design (regression) | Inspection of `CodexSharedTransport.cs` 100, 169-204 and `CodexConnection.cs` (`ConnectAsync`, `CallAsync`) | `EnsureCurrent` runs three times during one open: at the end of `transport.ConnectAsync`, inside `CallAsync("initialize")`, and after `initialized`. It then runs once per request. Each run makes one extra connection to the backend's control socket. `ListenerIdentity` blocks synchronously, with a fixed 1 s limit that is not linked to the caller's token or the budget. A timeout or a refused connection is reported as `EndpointReplaced`. | ANALYSIS-2300 |
| Dropped creation-time comparison | Inspection, plus the tunneling measured in analysis-022 | The comparison could only show a re-creation outside the NTFS tunneling window, and the peer probe also covers that case: the listener is different, or the probe is refused. The one thing only the timestamp could show is the same backend process re-binding its own socket, which is harmless. | Nothing meaningful is lost. |
| `Replaced` and backoff | Inspection of `CodexSharedAccess.cs` 262-291 and test `ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween` | `verified` is updated only on success and never removed. `Replaced` compares the result with the last successfully verified backend. `Due` is checked before any attempt. Every failure that is not the caller's own cancellation records a backoff failure, including `PeerUnavailable` and `BackendNotVerified` after a connect. `BackedOff` itself is not counted. | ANALYSIS-2202 resolved, and the backoff still bounds retries (2/5/15/30/60 s). |
| Test quality | Inspection of `CodexTransportTests.cs` and `ChildListener`; targeted runs | Assertions are now specific (`EndpointReplaced`, `BackendNotVerified`, `PeerUnavailable`, `Replaced` true then false, `server.Serving`). Most tests run the real ioctl, because `Access()` injects no `peerOf`. `ChildListener` depends on `pwsh`, and CI is `windows-latest`, which ships it. | Good overall. ChildListener lifetime: ANALYSIS-2301. Expected-identity mutation: see Re-verification. |
| Targeted tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~CodexTransportTests\|FullyQualifiedName~CodexNameTests"`, run 3 times before 12:29Z | 35/35 passed each time, about 2 s each | Stable in isolation. Full suite not run (the developer recorded 1471/1472 with an unrelated Tray timing failure). |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 9 changed source and test files>` | exit 0 | Passed. |
| Layering and parity | Searched `src` for `CodexSharedAccess`, `CodexSharedTransport`, `CodexSharedPeer`, `PeerUnavailable`. Inspected the dependencies. | These names occur only under `src/BellTower/Providers/CodexShared*.cs` and `ICodexTransport.cs`. `CodexSharedPeer` uses `IO/ProcessProbe`, which has precedent in other Providers types. `Core` is untouched, and the shared policy (`NativeNameSchedule`, `ProcessIdentity`, `ProcessProbe`, `AgentModeClassifier`) is reused, not duplicated. | Unchanged and consistent with `docs/development.md` and AGENTS.md. The Codex-only part is justified because Claude Code has no shared backend. Covers Codex only by design; nothing in this task applies to Claude. Mixed sessions are not applicable until task 3.2 wires the transport. |

## Findings

### ANALYSIS-2300: The per-request listener probe opens more backend connections than it needs and mislabels transient failures

- Severity: suggestion
- Related task / requirement: 3.1 (non-owning, bounded, diagnosable); design decision 4 (observer only); decision 7 (the connection belongs to the backend that hosts the conversation)
- Locations: `src/BellTower/Providers/CodexSharedTransport.cs` 100, 169-204; `src/BellTower/Providers/CodexConnection.cs` (`ConnectAsync`: `CallAsync("initialize")` then `active.EnsureCurrent()`; `CallAsync`: `active.EnsureCurrent()` before the read budget) at a744d27 plus the working tree
- Observation:
  1. **Redundant probes at open.** One `OpenAsync` makes four connections to the backend's real control socket: the verified connection plus three probes. The first probe runs immediately after `Verify` on the same open and adds nothing, and the next two follow within milliseconds. After that, each request adds one more connection.
  2. **No benefit for an established connection.** Every frame on the established connection goes to the process verified at connect time, whoever is listening at the path later. The probe can only show that a newer listener has superseded our backend, which affects later opens and decision 7, not whether this connection still talks to the verified process. The current consumers open a short-lived connection per operation (`CodexNameService`, `CodexPermissionPump`), so a supersession would also be seen on the next open through `Replaced`.
  3. **Mislabeled failures.** A probe that times out, or is refused because the backend's accept backlog is momentarily full (for example during a cold start when several clients connect at once), is reported as `EndpointReplaced`, not `Timeout` or `Refused`.
  4. **Blocking and unbounded by the caller.** The probe blocks a thread-pool thread synchronously (`ConnectAsync(...).AsTask().GetAwaiter().GetResult()` with its own 1 s limit), ignores the caller's cancellation, and sits outside `Budget.Read`.
  5. **Effect on the backend not checked.** Each probe is a real connection that the Codex backend accepts and that ends without an HTTP request. Whether this costs the backend anything (a log row per connection, connection accounting, idle-lifetime bookkeeping) was not checked. The implementation-035 native run shows only that the backend kept working.
- Proposed improvement and benefit: keep the peer check at connect (`Verify`), which is the ANALYSIS-2200 binding. Per request, check only the cheap liveness of the verified backend (`isBackend(Backend)`), which already detects an ended or restarted backend. Do the path probe at most once per connection lifetime, for example only when a connection is reused after a minimum age, or detect supersession on the next `OpenAsync` through `Replaced`. Remove the `EnsureCurrent` call at the end of `CodexSharedTransport.ConnectAsync`. If the probe is kept, report its timeout or refusal as `Timeout`/`Refused`, and keep `EndpointReplaced` for "a different process is listening" and "the socket is gone".
  - Benefit: one backend connection per open instead of four, and none per request. Clearer diagnostics. Less unverified interaction with a backend Bell Tower does not own.
- Behavior-preservation risks: if the probe moves to the next open, `DifferentProcessListeningAtTheSamePathAfterVerificationIsDetected` and `RemovedSocketIsReportedAsReplaced` must change. They would assert `Replaced`, or `EndpointReplaced` on the next open, instead of on the next request. A long-lived pooled connection in 3.2 would need the once-per-lifetime check to keep catching supersession.
- Evidence type: source observation. The client-side cost was measured (median about 0.1 ms per probe, see the table), so cost is not the concern. The effect on the backend is a hypothesis.
- Verification / measurement plan: (a) Count the backend connections per open with `CodexStandInServer.Accepted` and assert a fixed number. (b) Run a stand-in test where the listener refuses a probe while the verified backend is alive, and assert a non-`EndpointReplaced` reason, or no failure if probes are removed from the request path. (c) Optional, in a native no-quota run like implementation-035: look at the private backend's log for one probe-only connection, to settle the hypothesis in point 5.
- Relationship: regression introduced by the fix for ANALYSIS-2200 / QA-2900 (it replaces the creation-time recheck).

### ANALYSIS-2301: ChildListener processes have no bounded lifetime

- Severity: suggestion
- Related task / requirement: 3.1 test reliability
- Locations: `tests/BellTower.Tests/Providers/CodexStandInServer.cs` 184-273 (script at 186-191, start at 207-229)
- Observation: the child `pwsh` runs `while ($true) { $held += $s.Accept() }` with no end. Cleanup relies on `Dispose` (`Kill(true)`). If the test host is aborted (CI cancellation, a test-run timeout, or a debugger stop), the children keep running and keep holding sockets and the temporary `.ps1` files in the temp folder. Accepted sockets also accumulate in `$held` for the child's lifetime (minor).
  - The children are started with `pwsh -File <temp .ps1>` and no `-ExecutionPolicy`. A developer machine with an `AllSigned` policy would fail at `WaitUntilListening` with "ended before listening". That is a hypothesis; CI `windows-latest` has `pwsh` and its default policy works.
  - Determinism is otherwise good: readiness is a real connect, with a 30 s limit.
- Proposed improvement and benefit: pass the parent PID and a deadline, and have the script exit when either is reached (for example `Poll` with a timeout, as my disposable probe did, plus a check for the parent). Pass `-ExecutionPolicy Bypass`, or use `-EncodedCommand` to avoid the temp script file. This prevents orphaned listeners and environment-dependent failures.
- Behavior-preservation risks: tests only. The deadline must be longer than the slowest test.
- Evidence type: source observation; the policy case is a hypothesis.
- Verification / measurement plan: abort a run of `CodexTransportTests` mid-way and check that no `pwsh` child of the test host is still running after the deadline.
- Relationship: new test fixture from the fix for QA-2900.

### ANALYSIS-2302: The evidence wrongly says elevated peers are refused, and `BackendNotVerified` hides why a peer was refused

- Severity: suggestion
- Related task / requirement: 3.1 provenance and reasons; 5.x diagnostics (honest capability reporting)
- Locations: `src/BellTower/Providers/CodexSharedPeer.cs` 33-39; `src/BellTower/IO/ProcessProbe.cs` 28-39 (`Process.StartTime`), 45-50 (`OpenProcess(0x1000)`, which is `PROCESS_QUERY_LIMITED_INFORMATION`); `src/BellTower/Providers/CodexSharedTransport.cs` 134-137, 215-218; `evidence/implementation-035.md` ("the peer is unreadable such as elevated")
- Observation:
  - **Elevated peers.** Both the creation-time read and the command-line read need only limited query access. A normal-integrity process usually has that access to an elevated process of the same user. So an elevated Codex shared backend would probably be identified and accepted, not refused with `PeerUnavailable`. This is a hypothesis: I did not run an elevated process (that is outside safety limits). The test `UnavailablePeerLookupFailsClosedWithItsOwnReason` injects a null PID and covers neither the "not readable" path nor elevation.
  - **Hidden reasons.** All three checks — not live, image not `codex`, and command line not a shared backend — produce the same `BackendNotVerified` with the same message. A Codex release that renames the binary, or a non-default launch, would show up only as "not a live Codex shared backend".
  - **Mislabeled ended peer.** A peer that ended between the ioctl and `Identify` is reported as `PeerUnavailable` ("not readable") rather than as ended.
- Proposed improvement and benefit:
  - Correct the evidence statement.
  - Decide whether an elevated backend may be observed. Design 66 scopes the binder to non-elevated shared mode. If an elevated backend must not be observed, add an explicit integrity-level check on the peer instead of relying on access failure.
  - Give `BackendNotVerified` a distinct message, or a sub-reason, for the image check and the command-line check, so diagnostics stay honest about an unverified or renamed backend.
- Behavior-preservation risks: wording and diagnostics only, unless the policy decision adds a refusal for elevated peers.
- Evidence type: source observation. The elevated-peer behavior is a hypothesis.
- Verification / measurement plan: a manual check on a test machine with an elevated stand-in listener (not on the user's install), recording the outcome. Unit tests that assert the distinct messages for the image and command-line refusals.
- Relationship: claims made in the fix for ANALYSIS-2200; complements QA-2901.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2200 | `untracked=10/a837b9d32cd2` | **Binding.** The identity comes from the connected socket's peer before anything is sent (`CodexSharedTransport.cs` 108-110, 127-139). A caller-supplied identity is now only an optional extra comparison. The image name check was added, and the managed root is not required.<br>**Tests.** `ConnectedSocketReportsTheListeningChildProcess` uses the real ioctl and a child process. `DefaultProvenanceRequiresTheCodexImage...` uses a child whose command line classifies as `SharedBackend`. `ExpectedIdentityThatIsAliveButNotTheListenerIsRefused` covers a mismatched expected identity.<br>**Native run.** Implementation-035 ran without an injected identity: the derived backend equalled the started PID and ticks, and `Replaced = True` after a restart.<br>**My measurement.** The ioctl returned the listener's PID in 300 of 300 runs, before the connection was accepted.<br>**Fail-closed.** An unsupported ioctl or an unreadable process gives `PeerUnavailable`, with no fallback.<br>**Residuals.** Primary documentation does not exist (see Assessment). The elevated-peer claim is unproven (ANALYSIS-2302). The recheck design is ANALYSIS-2300. | verified (residuals in ANALYSIS-2300 and 2302 do not reopen the binding) |
| ANALYSIS-2202 | same | `verified` is never cleared on failure, and `Replaced` compares with the last success (`CodexSharedAccess.cs` 285-290). A stale caller-held identity no longer exists in the default path. If a caller passes a stale `expected`, the refusal comes after a connect attempt, so counting it toward backoff is legitimate. `ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween` asserts `Replaced == true`, then `false`, and the developer's mutant forcing the comparison to false was killed. The backoff still bounds retries: `Due` is checked first, every failure is recorded, and `BackedOff` is not counted. | verified |
| QA-2900 (test design only; QA owns the functional recheck) | same | **Removed check.** The creation-time check is gone, and its role is taken by the peer probe.<br>**Probe tests.** `DifferentProcessListeningAtTheSamePathAfterVerificationIsDetected` and `RemovedSocketIsReportedAsReplaced` assert `EndpointReplaced`, which only `EnsureCurrent` raises. The developer's mutant removing the probe was killed by both.<br>**Test comment.** The comment in the first test says the verified server "stays alive and connected". In fact `first.Dispose()` aborts the stand-in's connections, but the assertion still pins the probe because of the specific reason.<br>**`Replaced` test.** `ReplacementIsReported...` uses an injected `peerOf`, but also passes it to the probe, so the result is consistent.<br>**Expected identity.** The comparison is not mutation-verified. By inspection, the pair `ExpectedIdentityThatIsAliveButNotTheListenerIsRefused` (with `isBackend` forced true, removing or inverting the comparison would let the open succeed) and `ExpectedIdentityEqualToTheListenerIsAccepted` (inverting it would refuse) would kill both a deletion and an inversion. The developer reported a compile error, but that came from removing the parameter's only use; a mutant such as replacing `wanted != identity` with `false` compiles. That is source reasoning, not a measured kill. | test design adequate; mutation of the expected-identity comparison not verified (source reasoning only) |

## Assessment and limitations

The fix resolves the substance of ANALYSIS-2200 and ANALYSIS-2202:

- The shared transport now derives the backend from the process serving the connected socket, before any byte is sent.
- It works from the resolved home alone.
- It reports `Replaced` against the last good backend, and the existing backoff still bounds every failure.
- Layering and parity are unchanged.

What remains is narrower:

- The recheck makes more backend connections than it needs, and labels transient probe failures as replacement (ANALYSIS-2300).
- The child listener test fixture has no bounded lifetime (ANALYSIS-2301).
- One unproven claim about elevated peers, and coarse refusal reasons (ANALYSIS-2302).

None of these blocks the 3.1 boundary.

**Documentation risk for `SIO_AF_UNIX_GETPEERPID`.**

- What I found: no Microsoft Learn page and no local Windows SDK header. The constant appears only in secondary headers (mingw-w64, GLib's fallback, and the Microsoft-generated windows-rs metadata cited by the developer).
- The risk: the behavior is undocumented, so a Windows build may not support it, may return a different PID (for example after socket handle duplication), or may change it.
- Mitigation already present: it fails closed to `PeerUnavailable`. The image and command-line checks independently constrain whatever PID is returned, and the per-open home echo remains.
- Diagnostics needed:
  - When the ioctl itself fails, record the reason as "peer lookup unsupported" together with the OS build. This should be distinct from "peer not readable" and from a backend that is not verified.
  - Report the shared-backend capability as proven only on the measured Windows 11 build, until it is checked on the oldest supported build.
  - Keep the native no-quota check from implementation-035 in the release checklist, so a regression appears as `PeerUnavailable` rather than as silent non-adoption.

**Not performed:**

- A build and the full suite.
- A run against a real Codex backend, including how the backend reacts to probe connections. That is outside the analyzer's scope.
- A run with an elevated peer (safety).
- Mutation runs: I do not own the sources.

**Writes:**

- None in the repository: the client refused the write of this report.
- Disposable files in the session scratchpad: a copy of the built assembly and the probe script.
- A short probe directory under the system temp folder. It was created and then removed, and the child listener was stopped after an identity check.
- No user process was touched.

**Outside my writes:** another actor rewrote three target sources between about 12:29:11Z and 12:29:24Z, restored them by 12:29:55Z, and rebuilt `bell-tower.dll` at 12:29:39Z. The orchestrator owns triage and ledger status.
