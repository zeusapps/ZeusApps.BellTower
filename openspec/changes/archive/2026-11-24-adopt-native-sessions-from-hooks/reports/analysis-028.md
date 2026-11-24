<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-028 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

```
status: done
role / client / model: code-analyzer / Claude Code delegated subagent (Agent SDK; client version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.3, recheck of DEV-040. Rechecked: ANALYSIS-2700 verified with one residual clause (ANALYSIS-2800); ANALYSIS-2701 verified (ANALYSIS-2801 is a weaker-than-claimed native assertion); ANALYSIS-2704 verified; QA-3401 fix design verified, and no other proxy leak exists in src or vscode. New: ANALYSIS-2800 to 2803, all suggestions, none blocking. ANALYSIS-2804 to 2899 are unused.
changed or report paths: none in the repository. Writing D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-028.md was refused ("Subagents should return findings as text"), so the full report is inline below. Disposable output only: the ignored folder D:\Code\ZeusApps.BellTower\artifacts\analysis-028\tree (an isolated source copy, its build and its test workspaces).
checks:
- dotnet build ZeusApps.BellTower.slnx -c Release -p:RestoreLockedMode=true (isolated copy) -> passed (0 warnings, 0 errors)
- dotnet format --verify-no-changes --include <9 changed .cs> (copy) -> passed
- dotnet test filtered to ManagementOwnershipProcessTests, ProcessStopAuthorityTests and CodexSharedRuntimeServiceTests (copy) -> passed, 3 runs of 37/37
- mutant with the proxy setting removed from CodexLoopbackTransport (copy only) -> killed (30 proxied connections)
- mutant where the post-readiness loop ignores the stop marker (copy only) -> survived, 24/24 (ANALYSIS-2802)
- timing of a refused loopback connect -> measured at about 2.0 s, so the readiness window is about 44 s
- native Codex rerun, full suite -> not run (role limits)
target identity: before (about 17:55Z) head=b669cf8 baseline=b669cf8 files=12 diff=16550cc450dd untracked=5/6321761928cb, which matches the assignment. After (18:28Z and 18:29Z): head=b669cf8 baseline=b669cf8 files=12 diff=2f491c3fb898 untracked=5/6321761928cb. The difference is the concurrent QA reviewer's live mutant in src/BellTower/Sessions/CodexRuntimeHost.cs (server.Kill(entireProcessTree: false)). I also saw a transient difference in CodexLoopbackTransport.cs that was gone on recheck. Untracked hashes are unchanged. All my checks ran on a copy taken before those mutations and verified byte-identical to the reviewed identity.
limitations / decisions needed:
- The report write was refused; the text is inline.
- The working tree was not at the reviewed identity when I finished, because of QA's mutant. Confirm it is restored to diff=16550cc450dd before closing.
- Decide whether to fold ANALYSIS-2800 to 2803 into a fix cycle or record them as accepted limitations. None blocks 3.3.
next: the orchestrator saves the report below as reports/analysis-028.md and triages it.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-028 (recheck of DEV-040, task 3.3)

## Review target

- Reviewer / role: ANALYSIS-028 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code delegated subagent (Agent SDK). The client version is not available to the subagent. Mode: the parent's permission mode. Model: claude-opus-5-5.
- Date: 2026-11-05T18:30Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-028. This rechecks fix cycle 1 (DEV-040) for task 3.3.
- Task and requirement references:
  - [tasks.md](../tasks.md) 3.3
  - [design.md](../design.md) decision 4
  - [implementation-040](../evidence/implementation-040.md) and [implementation-039](../evidence/implementation-039.md)
  - [analysis-027](analysis-027.md) and [qa-034](qa-034.md)
- Scope: the 12 paths of the review target. Neighboring paths read:
  - `src/BellTower/Providers/CodexConnection.cs` and `src/BellTower/Providers/CodexSharedTransport.cs`
  - `src/BellTower/Sessions/CodexNameService.cs`, `src/BellTower/Sessions/CodexAccessStatus.cs` and `src/BellTower/Cli/NativeSyncCommand.cs`
  - `src/BellTower/Updates/UpdateTransaction.cs` and `src/BellTower/Updates/ReleaseClient.cs`
  - `src/BellTower.Distribution/ReleaseContract.cs`, `Installation.cs`, `CaptureCompatibility.cs` and `AttachmentHealthCompatibility.cs`
  - `src/BellTower.Launcher/Program.cs`, `src/BellTower/Program.cs` and `scripts/package.ps1` (the contract line)
  - `tests/BellTower.Tests/Testing/BuiltApplication.cs`, `TestWorkspace.cs` and `StandIns.cs`
- Baseline commit: b669cf8
- Implementation identity before the analysis (`scripts/review-identity.ps1`, at about 17:55Z):
  - HEAD: b669cf8c4aca0a54a721f8aa771c9b29775d92da
  - Manifest:
    - untracked (`?`): `evidence/implementation-039.md`, `evidence/implementation-040.md`, `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs`, `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs` and `tests/BellTower.Tests/Testing/ManagementCohort.cs`
    - modified (`M`): `docs/architecture.md`, `src/BellTower/IO/CodexRuntimeStore.cs`, `src/BellTower/Providers/CodexLoopbackTransport.cs`, `src/BellTower/Sessions/CodexRuntimeHost.cs`, `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs` and `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`
  - Tracked diff SHA-256: 16550cc450dda5e60304238310b4e37763c3e824ffb118b3ff1bab90c7ecdab8
  - Untracked file hashes:

    | File | SHA-256 prefix |
    | --- | --- |
    | `implementation-039.md` | a0934bbf |
    | `implementation-040.md` | c6efa046 |
    | `ProcessStopAuthorityTests.cs` | 6ecc3a08 |
    | `ManagementOwnershipProcessTests.cs` | 64b759e6 |
    | `ManagementCohort.cs` | b63690e8 |

  - Summary line: `head=b669cf8 baseline=b669cf8 files=12 diff=16550cc450dd untracked=5/6321761928cb`. This matches the assignment.
- Before/after identity: the concurrent QA reviewer changed the working tree during my analysis. Details are under Assessment. My checks ran on a byte-identical copy of the reviewed identity, so the changes did not affect them.
- Prior reports / rechecked findings:
  - Rechecked: [analysis-027](analysis-027.md) ANALYSIS-2700, 2701 and 2704, and the design of the fix for [qa-034](qa-034.md) QA-3401.
  - New: ANALYSIS-2800 to 2803. ANALYSIS-2804 to 2899 are unused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Isolation | I copied `src`, `tests`, `tools`, `vscode` and the root build files (without `bin` or `obj`) into the ignored folder `artifacts/analysis-028/tree` and built there in the standard layout. `BuiltApplication` and `TestWorkspace` find the copy's own `ZeusApps.BellTower.slnx`, so test processes and workspaces stay inside the copy. | `cmp` and `diff -r` of `src` and `tests` against the reviewed files: identical | The process tests can run without touching the standard layout that the QA reviewer rebuilds. |
| Build | `dotnet build ZeusApps.BellTower.slnx -c Release -p:RestoreLockedMode=true`, run in the copy | 0 warnings, 0 errors | Passed |
| Format | `dotnet format … --verify-no-changes --include <9 changed .cs>`, run in the copy | Exit code 0 | Passed |
| Targeted tests | `dotnet test … --filter ManagementOwnershipProcessTests\|ProcessStopAuthorityTests\|CodexSharedRuntimeServiceTests`, three runs | 37/37 in each run | Passed. These use stand-ins only. |
| QA-3401 mutant | In the copy only, I removed `Options = { Proxy = null }`, rebuilt, and ran `TheOwnedConnectionNeverGoesThroughAProxy`. Then I restored the file (byte-compared) and rebuilt. | `Expected: 0, Actual: 30` | Killed. This matches DEV-040. |
| Mutant: the post-readiness loop ignores the stop marker | In the copy only, I removed `&& !File.Exists(store.StopPath(attachment))` at `CodexRuntimeHost.cs:142`. Then I restored the file and rebuilt. | 24/24 passed (ownership, scan and every `CodexRuntime*` test) | Survived. See ANALYSIS-2802. |
| Readiness window | I timed three TCP connects to a loopback port that had just been freed. | Each took about 2.0 s, so each readiness attempt ends at its 1 s timeout. The 40 attempts give a window of about 44 s. | The live-backend rows have ample margin. |
| Proxy leak audit (`src` and `vscode`) | Searched for `ClientWebSocket`, `HttpClient`, `SocketsHttpHandler`, `HttpClientHandler`, `WebRequest`, `TcpClient`, `UseProxy`, `.Proxy`, `WebProxy`, `Invoke-WebRequest`, `Invoke-RestMethod`, `Net.WebClient` and `HttpListener`, and in JS for `fetch`, `http(s).request`, `net.connect` and `WebSocket`. Read every hit. | See the client list below the table. | No other client can leak a loopback credential to a proxy. |
| .NET proxy behavior | Context7 `/dotnet/runtime`. A targeted query for the `ClientWebSocket` handler setup returned nothing. | The environment proxy has no automatic loopback bypass, which matches QA-034's observed CONNECT. Only the manual WinINet settings bypass loopback. | Setting `Proxy = null` disables both the environment and the system proxy. The mutant run confirms this. |
| Docs accuracy (ANALYSIS-2700) | Compared `docs/architecture.md:28` and the guard's owner text with the code. | See Re-verification | Accurate except for one clause (ANALYSIS-2800) |
| Coverage (ANALYSIS-2701) | Read `RegisterSessions`, the tray-exit row, the update rows, `AwaitNativeObservation`, `CodexAccessStatus` and `CodexSharedTransport`. | See Re-verification | Addressed. The native assertion is weaker than its comment (ANALYSIS-2801). |
| Release-contract fixture | Compared `CreateInstallation` with `scripts/package.ps1:166`, `src/BellTower/Program.cs:23`, `HookMaintenanceTests.cs:149`, `Installation.VerifyFiles`, `UpdateTransaction.Apply` (lines 105, 107-109 and 168) and the launcher's `Program.cs:19`. | See ANALYSIS-2803 | Realistic. One row does not pin its failure reason. |
| Native check | none | The analyzer does not launch `codex` or `claude`. | Not performed. I read DEV-040's native evidence but did not reproduce it. |

Clients found by the proxy leak audit:

- `CodexLoopbackTransport` now sets `Proxy = null`.
- `CodexSharedTransport` connects over a Unix socket through `SocketsHttpHandler` with `UseProxy = false`.
- `ReleaseClient` calls the public GitHub API, so honoring the user's proxy is correct there.
- `ProviderHookProbe` uses `HttpListener`, which is a server, not a client.
- The tray opener and the host readiness probe both reach the owned endpoint only through `CodexConnection.ConnectAsync(endpoint, token)`, which constructs `CodexLoopbackTransport`.
- There are no clients in Ingress, Launcher, Distribution, Protocol, the PowerShell module or the VS Code extension.

## Findings

There are no blocking defects. The four suggestions below concern the fix itself. None affects the safety claim of task 3.3.

### ANALYSIS-2800: "Only the tray holds observer connections" is not quite true: `native-sync` also opens one

- Severity: suggestion
- Related task / requirement: 3.3. This clause came from my own ANALYSIS-2700 wording.
- Locations:
  - `docs/architecture.md:28`
  - `src/BellTower/Cli/NativeSyncCommand.cs:28`
  - `src/BellTower/Sessions/CodexNameService.cs:53`
- Observation: `bell-tower native-sync` runs one name sync in its own process. That sync opens an owned or shared observer connection through the default `CodexRuntimeOpener` and disposes it with `using`. It never stops or signals anything, so the safety claim holds. Only the "only the tray" clause is imprecise.
- Proposed improvement and benefit: name the tray (the native-name and permission readers) and, briefly, the `native-sync` command, and say that each disposes its own connection. The docs then stay exact for the next audit.
- Behavior-preservation risks: none, docs only.
- Evidence type: source observation
- Verification / measurement plan: inspection
- Relationship: ANALYSIS-2700

### ANALYSIS-2801: The native-row check accepts statuses that do not prove the backend was observed

- Severity: suggestion
- Related task / requirement: 3.3, ANALYSIS-2701
- Locations:
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:390-406` and `:428`
  - `src/BellTower/Sessions/CodexAccessStatus.cs:40-57`
- Observation:
  - The comment says the row fails for a backend that was never observed. The check is actually a denylist of three phrases.
  - These statuses also pass even though none proves a peer-verified exchange: "refused the connection", "did not answer in time", "process lookup is unsupported", "process cannot be read", "is not a codex process", "is not a shared backend", "runs elevated", "was replaced", "sent an unexpected response", and `ConnectionFailed`.
  - DEV-040's recorded status, "rejected the request", does prove observation. `CodexSharedTransport` verifies the peer in its connect callback, and `Rejected` comes only from a JSON-RPC error after that. The evidence for that run therefore stands. The guard for future reruns is weaker than its comment says.
  - Minor: the tray-exit wait `owned.Closed >= owned.Accepted` counts connections that were accepted but never upgraded, and only upgraded sessions increment `Closed`. A connection dropped during the handshake would make the wait time out spuriously. This is unlikely.
- Proposed improvement and benefit:
  - Assert an allowlist instead: `Rejected`, `Mismatch` or `Unverified`, or a confirmed name. These outcomes all require a verified peer.
  - Optionally count `Closed` for every accepted session.
- Behavior-preservation risks: none, tests only.
- Evidence type: source observation
- Verification / measurement plan: one native rerun. The recorded status must be in the allowlist.
- Relationship: ANALYSIS-2701

### ANALYSIS-2802: The production stop path after readiness is still untested, and the new log code covers only the readiness phase

- Severity: suggestion
- Related task / requirement: 3.3, QA-3400 / ANALYSIS-2702
- Locations:
  - `src/BellTower/Sessions/CodexRuntimeHost.cs:118`, `:136-137` and `:142`
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:239-280`
  - [implementation-040](../evidence/implementation-040.md), Limitations
- Observation:
  - The `.cmd` stand-in never completes the WebSocket handshake, so the host never becomes ready. The test proves the marker exit during readiness and the `finally` tree kill. That kill is shared by both phases, so QA-3400's gap is closed.
  - The wrapper's real stop request arrives after the TUI exits, which is after readiness, so it ends the loop at line 142. Removing the marker condition there survived 24/24 tests (measured). No test in the suite runs the host past readiness.
  - The log code itself is sound:
    - Precedence is backend exit, then stop requested, then timeout.
    - A marker written between the last attempt and the log call would label a timeout as a stop request. This is harmless.
    - No source, script or doc consumes `codex-runtime-timeout` or the new code.
  - The evidence's limitation does not say that the proof covers only the readiness phase.
- Proposed improvement and benefit: either state the phase limitation in the evidence, or add one row in which the backend becomes ready:
  1. The `.cmd` stand-in writes its `--listen` argument to a file, then idles.
  2. The test starts a `CodexStandInServer` on that port (this needs an optional port parameter).
  3. The test waits for the runtime record, then writes the marker.
  4. The test asserts that the manager and its descendants end and that the record and token are removed.

  The row then covers the stop that users actually hit.
- Behavior-preservation risks: none, tests only. Binding the freed port can race with another process; retry once.
- Evidence type: measured result (the mutant survived) and source observation
- Verification / measurement plan: the line-142 mutant must fail the new row.
- Relationship: QA-3400, ANALYSIS-2702

### ANALYSIS-2803: The update fixture is now realistic, but the fail-before-switch row does not check its failure reason

- Severity: suggestion
- Related task / requirement: 3.3, ANALYSIS-2705
- Locations:
  - `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs:559-582` and `:523-529`
  - `src/BellTower/Updates/UpdateTransaction.cs:105, 107-109`
- Observation:
  - The target contract now matches a real release. It declares `Capture: 1, AttachmentHealth: 1` and lists the ingress executable with its hash, which matches:
    - what `scripts/package.ps1:166` writes
    - the contract `bell-tower.exe` declares itself (`Program.cs:23`)
    - the `HookMaintenanceTests` fixture
  - So the update rows now pass through the same gates as a real update, against a data directory written by a real tray. Those gates are `CaptureCompatibility.Ensure`, `VerifyFiles` with the ingress requirement, and the launcher's `EnsureTrayInvocation`.
  - The old `Capture: 0` contract was unrealistic, and its outcome depended on timing. Whether it was refused depended on whether the tray had already written health-v1 state.
  - The fake original payload stays at contract 0/0. It is read only on rollback after the switch, which is not process-tested.
  - The fail-before-switch rows assert only `State == "failed"`. A gate refusal at line 105 runs before the intended hash check at line 107 and produces the same state.
- Proposed improvement and benefit: also assert that the message contains "Staged metadata changed". A gate that starts refusing the realistic contract then fails the row.
- Behavior-preservation risks: none, tests only.
- Evidence type: source observation
- Verification / measurement plan: run the two fail-before-switch rows. Then temporarily set `AttachmentHealth: 0` while health-v1 state is present and confirm that the row fails.
- Relationship: ANALYSIS-2705

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2700 | diff=16550cc450dd untracked=5/6321761928cb | The architecture docs now match `CodexRuntimeHost.cs:38-46` and `:147-153`, `CodexRuntimeStore.Stop` and the `ProviderHookProbe` tree kills, and the guard's owner text matches too. They describe what the stop marker does, the reaping of descendants, the PID-reuse guard, the prepare tree kill, the "handle or descendant" rule and the tripwire wording. The decision to keep reaping descendants is now covered behaviorally: every grandchild ends while a decoy of the same image survives, and a non-tree-kill mutant fails the row (DEV-040). 37/37 passed in my three runs. | Verified. One clause is imprecise (ANALYSIS-2800). |
| ANALYSIS-2701 | same | Tray exit registers four sessions: Claude direct, Codex direct, Codex shared and Codex owned. The owned observer is real: a live stand-in received `initialize`. The row asserts the bearer token, that the connection closed, and that no resume, start, archive or `turn/*` request arrived. The update rows register the same four sessions. The native home is wired to the trays and the coordinator. The recorded status "rejected the request" implies a peer-verified connection. | Verified. The native assertion is weaker than its comment (ANALYSIS-2801). I did not reproduce the native run. |
| ANALYSIS-2704 | same | The comments on `CodexAccess.Owned` and `CodexRuntimeStore.Stop` describe the real mechanism. | Verified |
| QA-3401 (fix design) | same | `CodexLoopbackTransport.cs:9` sets `Proxy = null`. The tray opener and the readiness probe share this transport, so one change covers both. The shared transport already sets `UseProxy = false`. `ReleaseClient` is the only other client and correctly honors the proxy. The test uses the real host with every proxy variable set and `NO_PROXY` removed, and my mutant failed it with 30 connections. The docs state the rule. Claude has no such transport, so there is no parity gap. | Design verified. There is no other leak in `src` or `vscode`. |

## Assessment and limitations

- Blocking defects: none. DEV-040 fixes ANALYSIS-2700, 2701 and 2704 as intended. The QA-3401 fix is correctly scoped. No regression was found in the build, the format check or 3 × 37 targeted tests.
- The new log code `codex-runtime-stop-requested` is correct and nothing consumes it. It shows only the marker exit during readiness. The stop after readiness, which is the path users hit, is untested (ANALYSIS-2802, measured). This is not a safety gap: a missed marker would leave an owned runtime running, not stop an unowned process.
- The release-contract change makes the update rows exercise the compatibility gates realistically. Checking the failure reason would complete that (ANALYSIS-2803).
- Parity: Claude direct and Codex direct, shared and owned sessions are present together in the tray-exit and update rows. The owned runtime and its proxy rule apply to Codex only, because Claude has no runtime or loopback transport. That is not a parity gap.
- Not performed: the native rerun, the full suite, and rollback after the switch (a known limitation).
- Writes:
  - The report write was refused, so this text is inline.
  - The ignored disposable folder `artifacts/analysis-028/tree` holds the copy, its build output and its test workspaces.
  - I applied mutants only inside the copy and restored each one with a byte comparison.
  - I wrote nothing in the review target.
- Identity:
  - Before: `head=b669cf8 baseline=b669cf8 files=12 diff=16550cc450dd untracked=5/6321761928cb`.
  - After (18:28Z and 18:29Z): `diff=2f491c3fb898`, untracked unchanged. The difference is the concurrent QA reviewer's live mutant in `src/BellTower/Sessions/CodexRuntimeHost.cs` (`server.Kill(entireProcessTree: false)`). I had also seen a transient change in `CodexLoopbackTransport.cs` earlier; it was gone on recheck.
  - My evidence comes from the copy, which matches the reviewed identity.
  - The orchestrator should confirm that the tree is restored to `diff=16550cc450dd`.
- Decision: fold ANALYSIS-2800 to 2803 into a fix cycle, or accept them as limitations. None blocks task 3.3.
