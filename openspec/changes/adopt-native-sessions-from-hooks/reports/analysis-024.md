<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-024 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

```
status: done
role / client / model: code-analyzer / Claude Code delegated subagent (Agent SDK; client version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.2 (DEV-036); new ANALYSIS-2400 (warning), 2401 (warning, measured), 2402, 2403, 2404, 2405, 2406 (suggestions); rechecked ANALYSIS-2201, 2203, 2205, 2300, 2302 and QA-3000: all verified (some leftover gaps are noted)
changed or report paths: reserved path D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-024.md was NOT written: the client refused the write ("Subagents should return findings as text"). The full report text is below for the orchestrator to save verbatim.
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <21 changed/new .cs> -> passed; dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-024 -p:RestoreLockedMode=true -> passed (isolated output folder that git ignores; identity confirmed unchanged right after); dotnet test <that dll> --filter CodexSharedAccessTests|CodexSharedRuntimeServiceTests|CodexTransportTests|CodexNameTests|CodexPermissionTests -> passed 103/103; disposable concurrent-open probe (outside the repo, against the isolated build) -> measured: 4 concurrent failing opens recorded 4 failures, 1 open recorded 1; full suite -> not run (developer ran it; QA was mutating sources); native Codex / elevated peer -> not run (outside analyzer scope and safety limits)
target identity: before head=a58c4e3 baseline=a58c4e3 files=28 diff=f1c1a7f1e8c2 untracked=7/55de4a6a489e (also confirmed at 16:04Z right after my build); after 16:07Z head=a58c4e3 baseline=a58c4e3 files=29 diff=f1c1a7f1e8c2 untracked=8/5fe6d008ebdb; final 16:09:56Z head=a58c4e3 baseline=a58c4e3 files=29 diff=b5786c659ca6 untracked=8/7d10f863cdfe. A concurrent QA mutation changed the identity, not this analyzer: CodexBackendCheck.cs was replaced and src/BellTower/Providers/CodexBackendCheck.cs.qa031bak was created (its hash bda17e82… equals the reviewed original), and later a tracked file changed too. My source reads, build and tests were all done at the reviewed identity. The orchestrator should confirm the identity is back to the expected value after QA restores.
limitations / decisions needed: (1) ANALYSIS-2400 needs an orchestrator decision. The fork and delegated-thread refusals do not prove the hook-to-thread mapping, and the stated reason for them is backwards; docs/architecture.md and implementation-036 claim a proof the code does not give. (2) ANALYSIS-2404: the opener's shared-backend mode gate conflicts with the 4.2 rule and leaves a gap that is not recorded; carry it into 4.2. (3) ANALYSIS-2403: implementation-036 line 68 (the other-home conversation gets HomeMismatch) is wrong by source inference, and the 4.2 task text relies on it. (4) The elevated-token readability and the other-home JSON-RPC error are untested assumptions, not results. (5) Scratchpad hygiene: I first wrote the probe into a scratchpad folder left from an earlier session (scratchpad/probe). I moved my two files to scratchpad/a024probe and removed only the bin/obj folders my build created there; the folder's earlier files are untouched. No repository source, lock file or provider setting was written.
next: orchestrator saves the report text below as reports/analysis-024.md; triages 2400 (decision) and 2401 (fix in 3.2 recommended); routes 2402-2406 as suggestions; rechecks the review identity after QA-031 restores CodexBackendCheck.cs.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-024 (DEV-036, task 3.2)

## Review target

- Reviewer / role: ANALYSIS-024 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code delegated subagent (Agent SDK); client version not available to the subagent; parent's default permission mode; claude-opus-5-5
- Date: 2026-11-05T16:10Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`; ANALYSIS-024 (independent analysis of DEV-036)
- Task and requirement references:
  - [tasks.md](../tasks.md) 3.2 (with folded ANALYSIS-2201, 2203, 2205, 2300, 2302, QA-3000) and 4.2
  - [design.md](../design.md) decisions 2, 4 and 7
  - [implementation-036](../evidence/implementation-036.md)
- Scope:
  - `src/BellTower/Sessions/{CodexRuntimeOpener,CodexAccessStatus,CodexNameService,CodexPermissionPump,NativeNamePump}.cs`
  - `src/BellTower/Providers/{CodexThreadIdentity,CodexBackendCheck,CodexConnection,CodexSharedAccess,CodexSharedEndpoint,CodexSharedPeer,CodexSharedTransport,CodexLoopbackTransport,ICodexTransport,ICodexNameClient,ICodexAttentionClient}.cs`
  - `src/BellTower/IO/ProcessProbe.cs`, `src/BellTower/Tray/TrayApplication.cs`, `docs/architecture.md`, and the changed tests
- Neighbors read:
  - `src/BellTower/Core/{NativeAdoption,NativeNameSchedule,ConversationLifecycle,AttachmentEvidence}.cs`
  - `src/BellTower/IO/LocalLog.cs`, `src/BellTower/Cli/NativeSyncCommand.cs`
  - [native-adoption-011](../evidence/native-adoption-011.md), [native-adoption-001](../evidence/native-adoption-001.md)
- Baseline commit: a58c4e3
- Implementation identity before the analysis:
  - HEAD `a58c4e37e895`; 28 files, 7 of them untracked.
  - Tracked diff SHA-256: `f1c1a7f1e8c252c07ea7194aa31738d210b1e85ac3f2bcd1130cffb70cea6d31`.
  - Untracked file hashes:
    - implementation-036.md `b0f866fe…`
    - CodexBackendCheck.cs `bda17e82…`
    - CodexThreadIdentity.cs `c7a480c5…`
    - CodexAccessStatus.cs `aa467d31…`
    - CodexRuntimeOpener.cs `e29066df…`
    - CodexSharedAccessTests.cs `dc305872…`
    - CodexSharedRuntimeServiceTests.cs `be8ded91…`
  - Summary: `head=a58c4e3 baseline=a58c4e3 files=28 diff=f1c1a7f1e8c2 untracked=7/55de4a6a489e`. This matches the assignment.
- Before/after identity:
  - 16:04Z, right after my isolated build: the summary was unchanged.
  - 16:07Z: `files=29 diff=f1c1a7f1e8c2 untracked=8/5fe6d008ebdb`. A concurrent QA mutation (not mine) replaced `src/BellTower/Providers/CodexBackendCheck.cs` (now `59085419…`) and left `CodexBackendCheck.cs.qa031bak`, whose hash equals the reviewed original (`bda17e82…`).
  - 16:09:56Z: `files=29 diff=b5786c659ca6 untracked=8/7d10f863cdfe`. QA mutation still in progress.
  - I read `CodexBackendCheck.cs` at the start, before the mutation. My build and targeted tests ran at the reviewed identity.
- Prior reports / rechecked findings:
  - [analysis-022](analysis-022.md): ANALYSIS-2201, 2203, 2205
  - [analysis-023](analysis-023.md): ANALYSIS-2300, 2302
  - [qa-030](qa-030.md): QA-3000

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture: one selection rule | Inspected `CodexRuntimeOpener`, both services, `NativeNamePump`, `TrayApplication` and `NativeSyncCommand` | Selection lives only in `CodexRuntimeOpener.Select` (Sessions). `ConnectAsync` was removed from both client interfaces. The tray builds one opener and gives it to both services. The CLI builds its own default opener. | Conforms to ANALYSIS-2201. Layering follows docs/development.md. No duplicated selection logic remains. The `connect is null ? opener.ConnectAsync : connect` seam appears in both services; that is acceptable because their client interfaces differ. |
| Thread identity proof | Inspected `CodexThreadIdentity`, the thread fields in native-adoption-011 and the shared tests | `id == requested` is the only check that links a thread to the request. The other field checks look only at the thread the backend returned. | ANALYSIS-2400 |
| Elevation check | Inspected `ProcessProbe.IsElevated` and the check order in `CodexBackendCheck` | Elevated means `TokenElevation`, or an integrity level of High or above. The token is read with `PROCESS_QUERY_LIMITED_INFORMATION` plus `TOKEN_QUERY`. An unreadable token gives `PeerUnreadable`. Checks run in this order: live, image, command line, elevation. | Matches design decision 4 (always refuse; accepted by the orchestrator). Not measured: whether a medium-integrity caller can open an elevated peer's token (hypothesis, ANALYSIS-2403). |
| Refusal reasons / error mapping | Inspected `CodexConnection.Reason`, `CodexAccessStatus`, `CodexSharedPeer.Identify` and `CodexSharedEndpoint.HomeKey` | The handshake and requests share one mapping. Caller cancellation passes through. A close frame is now an `IOException`. | Conforms to ANALYSIS-2203. Wrong labels are covered in ANALYSIS-2403. |
| ANALYSIS-2300 | Inspected `CodexSharedTransport.EnsureCurrent` and `ConnectAsync`; test `EachOpenMakesOneBackendConnectionAndRequestsMakeNone` | The listener probe and its blocking 1 s wait are gone. `EnsureCurrent` is now `check.IsLive(Backend)`. | Verified |
| Shared-opener concurrency and backoff | Inspected `CodexSharedAccess.OpenAsync`, `NativeNameSchedule` and the pump's `SemaphoreSlim(4)`; ran a disposable probe | Every concurrent open that fails records its own failure. | ANALYSIS-2401 (measured) |
| Backoff across both services | Inspected the `CodexNameService` catch block and both pump intervals (permission pump every 1 s, name pump every 2 s) | `BackedOff` changes neither the name status nor the name schedule. | ANALYSIS-2402 |
| Selection gate and 4.2 | Inspected `Select`, `NativeAdoption.CrossCheck` and the `AttachmentEvidence` merge (line 169: an unknown stored mode can be upgraded, a known one is kept) | Shared access requires `Mode == shared-backend`. Task 4.2 says the binder must not select by that mode. | ANALYSIS-2404 |
| Field minimization | Inspected `ReadThreadAsync`, `ExchangeAsync`, `LocalLog` and the status strings | Every read sends `includeTurns:false`. The `result` is cloned, used briefly, and never logged or stored. Logs and statuses use fixed codes and strings. The token is read only when connecting. | No finding |
| Parity | Inspected `Select` (`Provider != "codex"` returns null) and `NativeNamePump` (Codex only); Claude tests are unchanged | Claude behavior is unchanged. There is no Claude shared backend in this change (design decision 4; task 3.4). | No finding. Checks cover Codex only; Claude is covered by its unchanged existing tests. |
| Performance | Inspected the per-poll open path | Each waiting conversation gets one full shared open per second. Refusals that will always repeat are retried and logged every second. | ANALYSIS-2405 (hypothesis with a measurement plan) |
| Test quality | Inspected the new and changed tests | Supersession is simulated, elevation is injected, sharing of one opener in the tray is untested, and success paths use 400 ms read budgets. | ANALYSIS-2406 |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <21 changed/new .cs files>` | exit 0 | Passed |
| Targeted tests (own build) | Built with `dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-024 -p:RestoreLockedMode=true` into a folder git ignores, confirmed the identity right after, then ran `dotnet test <that dll> --filter CodexSharedAccessTests\|CodexSharedRuntimeServiceTests\|CodexTransportTests\|CodexNameTests\|CodexPermissionTests` | 103/103 passed in 3 s | Passed. Full suite not run: the developer ran it, and QA was mutating sources at the same time. |
| Concurrency probe (measured) | Disposable console project outside the repository, referencing the isolated build. A Unix socket listener accepts and never answers. `CodexSharedAccess` uses a frozen clock, `CodexBackendCheck.Accepting()` and a 500 ms handshake budget. | 4 concurrent opens: all `Timeout`, failure count 4. 1 open: failure count 1. | Supports ANALYSIS-2401 |
| Native Codex, elevated peer | Not performed | Outside analyzer scope and safety limits | Related hypotheses are labeled as such |

## Findings

### ANALYSIS-2400: The thread-identity checks beyond id equality do not prove the hook-to-thread mapping, and the fork refusal rationale is backwards

- Severity: warning
- Related task / requirement: task 3.2 (exact-conversation reads and writes; concurrent conversations cannot read or write each other's state); design decision 4 ("Map hook session identity to protocol thread identity explicitly"); design decision 7 (self-checks)
- Locations:
  - `src/BellTower/Providers/CodexThreadIdentity.cs` 14-45
  - `src/BellTower/Providers/CodexConnection.cs` (`ReadThreadAsync`)
  - `docs/architecture.md` 30 ("the thread read must prove it is the hook's own root conversation")
  - [implementation-036](../evidence/implementation-036.md) 20 ("Decision for the orchestrator … a wrong mapping would write another conversation's name")
  - `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs` 210-219
- Observation:
  - Bell Tower requests `thread/read` with the hook's session id. The `id == requested` check therefore proves only that the backend returned the thread Bell Tower asked for. It cannot show that the hook's session id names the conversation the hook came from.
  - The other checks look only at that returned thread: `sessionId == id`, and no `parentThreadId`, `forkedFromId`, `agentRole` or `agentNickname`.
  - They refuse exactly the consistent case: a hook that names a fork or a delegated thread by its own id. In that case every read and write goes to that same thread, never to another conversation.
  - They miss the case the rationale worries about. If a fork's or delegated thread's hook carried its parent's or its session root's id, the returned thread is the parent or root. That thread has no fork or agent fields, so it passes every check, and the write goes to the parent.
  - Native evidence 011 (lines 71-72) shows that a fork has `sessionId == id`. So the `sessionId` check cannot tell a fork from a root at all. Only `forkedFromId` can.
  - Effect: `/fork` is a common user action. On a shared backend, forked conversations lose native names and runtime permission confirmation (they fall back to the silent "possibly waiting" chip), and the refusal protects nothing.
  - `docs/architecture.md` describes a proof the code does not provide.
- Proposed improvement and benefit:
  - State the actual proof honestly: id equality plus a backend verified as the socket's peer process.
  - Either drop the fork and agent refusals, or keep them as an openly conservative choice and label the status "fork mapping unverified" instead of "not proven to match".
  - Optionally add a check that can actually detect a wrong mapping: compare the returned thread's `cwd` (a field present in evidence 011) with the session's hook directory.
  - Keep the developer's proposed fork hook probe, but change its question to: which id does a fork's or a delegated thread's hook carry?
  - Correct `docs/architecture.md` 30 and implementation-036 line 20.
- Behavior-preservation risks:
  - Removing the refusals allows writes to forks that the hook names by their own id, which is the consistent case.
  - A `cwd` check could refuse a conversation whose working directory changed during the session. Compare against the latest hook directory, and refuse only on a mismatch.
- Evidence type: source observation, plus existing native evidence 011. Nobody has observed which id a fork's or a delegated thread's hook carries.
- Verification / measurement plan:
  - Run the fork hook probe: record the hook `session_id` and the `thread/read` fields for a `/fork`, and for a delegated thread if one can be produced.
  - If a `cwd` check is added, add a stand-in test where the returned root thread's `cwd` differs from the session directory, and assert that no `thread/name/set` is sent.
- Relationship: new. Corresponds to the decision for the orchestrator in implementation-036.

### ANALYSIS-2401: Concurrent failed opens multiply the shared backoff for the whole Codex home

- Severity: warning
- Related task / requirement: task 3.2 ("one normalized home key for backoff"); design decision 4 ("existing backoff")
- Locations:
  - `src/BellTower/Providers/CodexSharedAccess.cs` 34, 49, 53
  - `src/BellTower/Core/NativeNameSchedule.cs` 8, 30-36
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 51 (`SemaphoreSlim(4)`)
  - `src/BellTower/Tray/TrayApplication.cs` (one opener shared by both services)
- Observation:
  - `OpenAsync` checks `Due` before an attempt and calls `Failed` after it fails. It does not track which backoff window an attempt belongs to, so every overlapping failure calls `Failed`. Each call moves the backoff one step along 2, 5, 15, 30, 60 s.
  - The permission pump verifies up to four waiting conversations at once, and the name service uses the same `CodexSharedAccess`.
  - Opens overlap whenever the failure comes after an await: handshake or read timeout, refused upgrade, refused peer, or a handshake refusal. A missing socket file fails synchronously and does not overlap.
  - Measured with a disposable probe against the isolated build: 4 concurrent opens against a listener that never answers gave 4 timeouts and a failure count of 4. That means a 30 s backoff after a single short stall; the next failure gives 60 s. A single open gave a failure count of 1 (2 s).
  - During the backoff, every shared conversation of that home loses runtime permission confirmation, because `BackedOff` removes the verification. A real approval wait falls back to the silent unverified chip, and names stop updating for the same period.
- Proposed improvement and benefit:
  - Count at most one failure per backoff window per home. For example, record the failure count or the window's `Next` when checking `Due`, and call `Failed` only if no failure has been recorded since then.
  - Or make opens single-flight per home key, so concurrent callers wait on one attempt.
  - Benefit: a short backend stall costs 2 s, as the backoff design intends, instead of 30 to 60 s for every conversation in the home.
- Behavior-preservation risks: the per-key steps and the meaning of `BackedOff` stay the same. With single-flight, each caller must still get its own connection after a success.
- Evidence type: measured result (disposable probe on the isolated build), plus source observation of the pump's concurrency.
- Verification / measurement plan: a stand-in test with a silent listener that starts four `OpenAsync` calls through `Task.WhenAll` and asserts a failure count of 1, and a second test where a later success resets the count.
- Relationship: new. The issue can occur because of the 3.2 wiring (one shared opener plus pump concurrency); `CodexSharedAccess` itself came from 3.1. Related to ANALYSIS-2204 (the "concurrent" test runs sequentially).

### ANALYSIS-2402: A backoff started by one service leaves the other service's native-name status stale

- Severity: suggestion
- Related task / requirement: task 3.2; 5.x diagnostics (honest per-surface status)
- Locations:
  - `src/BellTower/Sessions/CodexNameService.cs` 79-85 (`BackedOff` returns without changing the status or the schedule)
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 33, 146-153
  - `src/BellTower/Sessions/NativeNamePump.cs` 34
- Observation:
  - The permission pump runs every 1 s; the name service runs every 2 s. When the pump records the shared failure first, the name service sees only `BackedOff` and keeps its previous status (for example `synchronized`) for the whole window.
  - The name service records no schedule entry on `BackedOff`. It repeats `Select` and identity reads every 2 s and can lose the race to the pump again in the next window.
  - So the name status shows the real reason only after the name service happens to make the first attempt once a backoff expires.
  - The comment in the code ("nothing was attempted, so the previous status stays") holds for the name service's own backoff, not for a backoff the pump started.
- Proposed improvement and benefit:
  - Have `CodexSharedAccess` remember the last failure reason per home key and attach it to the `BackedOff` exception (as the inner exception or a `LastFailure` property), so `CodexAccessStatus.For` reports it instead of the stale status.
  - Or record the name schedule's own backoff on `BackedOff` without changing the status.
- Behavior-preservation risks: `MissingBackendReportsItsReasonAndDoesNotRetryDuringBackoff` still holds, because the reason stays the same.
- Evidence type: source observation.
- Verification / measurement plan: a stand-in test where the pump fails first against a missing backend, then the name service syncs inside the backoff window; assert the status names the missing backend.
- Relationship: new; related to ANALYSIS-2401.

### ANALYSIS-2403: Several refusal labels, and one evidence statement, do not match the code path

- Severity: suggestion
- Related task / requirement: task 3.2 (distinct refusal reasons, ANALYSIS-2302); task 4.2 (per-conversation home); 5.2 diagnostics
- Locations:
  - [implementation-036](../evidence/implementation-036.md) 68
  - `src/BellTower/Providers/CodexSharedTransport.cs` 124-128
  - `src/BellTower/Providers/CodexBackendCheck.cs` (`CodexImage` catch; reviewed hash `bda17e82…`)
  - `src/BellTower/Providers/CodexSharedEndpoint.cs` 21-34
  - `src/BellTower/Sessions/CodexAccessStatus.cs`
  - `src/BellTower/IO/ProcessProbe.cs` (`IsElevated`)
- Observation:
  - **Conversation from another Codex home.** Implementation-036 line 68 says this case is refused by the initialize home check (`HomeMismatch`). That cannot happen: the socket path comes from the tray's own home, so Bell Tower connects to the tray-home backend, which reports that same home, and the handshake passes. The read then goes to a backend that does not host the thread.
    - By source inference, the backend answers with a JSON-RPC error, and the status becomes "shared backend rejected the request". If no tray-home backend is running, the status is "shared backend is not running". Neither is `HomeMismatch`.
    - Task 4.2's folded text ("refused as a home mismatch") relies on that statement.
  - **Ended backend.** `EnsureCurrent` reports a verified backend that ended or restarted as `EndpointReplaced` ("was replaced"), although no other process was seen.
  - **Exit race.** If the peer exits between the liveness check and the image check, `CodexImage` returns false and the refusal is reported as `ImageMismatch`, not as ended.
  - **Relative `CODEX_HOME`.** `HomeKey` throws `EndpointMissing`, which is shown as "is not running".
  - **Elevated peer (hypothesis).** If a medium-integrity Bell Tower cannot open an elevated peer's token for `TOKEN_QUERY`, the label is "process cannot be read" instead of "runs elevated". The refusal still fails closed. Tests inject `isElevated`.
- Proposed improvement and benefit:
  - Correct implementation-036 line 68 and the wording in task 4.2.
  - Report a verified backend that is no longer live as `BackendNotVerified` (ended) rather than `EndpointReplaced`.
  - Recheck liveness when the image read fails, or report the failed image read separately.
  - Give an unusable `CODEX_HOME` its own reason (`HomeMismatch`, or a new "home not resolved").
  - Before 5.2 diagnostics rely on the elevated label, check on a test machine whether the elevated peer's token can be read.
  - Benefit: these are the per-surface diagnostics that design decision 2 and task 5.2 require.
- Behavior-preservation risks: labels only; every path still refuses.
- Evidence type: source observation. The other-home answer and the elevated-token access are hypotheses.
- Verification / measurement plan:
  - A stand-in test with a thread the backend does not host, asserting the resulting status.
  - On a test machine (not the user's install), run an elevated stand-in listener and record which label appears.
- Relationship: new; completes ANALYSIS-2302.

### ANALYSIS-2404: Shared access requires the stored `shared-backend` mode, which task 4.2 rules out for the binder, and the resulting gap is not documented

- Severity: suggestion
- Related task / requirement: task 3.2; task 4.2 ("Select shared conversations by shared-runtime ownership and the binder's own listener and connection evidence, never by `Mode == shared-backend`"); AGENTS.md (honest capability reporting)
- Locations:
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs` 57-59
  - `src/BellTower/Core/NativeAdoption.cs` (`CrossCheck`: a backend without a live client parent becomes `unknown`)
  - `src/BellTower/Core/AttachmentEvidence.cs` 169 (an unknown stored mode can be upgraded; a known one is kept)
  - [implementation-036](../evidence/implementation-036.md) 11
- Observation:
  - Consider a shared conversation whose first hook arrives after the backend's launching client exited (for example, another TUI joined, or a cold resume happened later). Its mode is stored as `unknown`, and nothing later sets it to `shared-backend`.
  - The opener then gives it no access: no native names and no runtime permission confirmation. Its status reads "no verified dedicated runtime", which also misdescribes a shared conversation.
  - Task 4.2 calls this situation the normal binder case.
  - The gate does protect something. The shared transport does not check that the backend actually hosts the thread, so the mode check is what keeps a same-home embedded-runtime conversation away from the shared backend.
  - The notes in implementation-036 do not mention this gap.
- Proposed improvement and benefit:
  - Record the gap in implementation-036 and in the capability matrix: for a shared conversation adopted after its launcher exited, native names and runtime confirmation are unverified.
  - Carry into task 4.2: move the opener's gate to the binder's ownership and hosting evidence, so both use one rule. For example, allow access to a shared-runtime conversation when binder or `thread/loaded/list` evidence shows the verified backend hosts it.
- Behavior-preservation risks: none for 3.2 if this is only documented. In 4.2, widening the gate without a hosting check would let a same-home embedded conversation be read or written through the shared backend.
- Evidence type: source observation.
- Verification / measurement plan: a 4.2 test row where the launching client exits before the conversation's first hook; assert that access is selected only when hosting evidence exists.
- Relationship: new; affects task 4.2 (DEV-036 decisions 3 and 4 as folded there).

### ANALYSIS-2405: Each waiting conversation costs one full shared open per second, even when the refusal will always repeat

- Severity: suggestion
- Related task / requirement: task 3.2; design decision 4 (bounded access, minimal interaction with a backend Bell Tower does not own)
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 33, 51, 105-153
  - `src/BellTower/IO/LocalLog.cs` 14-19
- Observation:
  - For every Codex conversation with a permission or question wait, the pump opens a new connection each second. Each open does:
    - a Unix socket connect and the peer ioctl;
    - about 4 process queries (identity, liveness, image, command line, token);
    - a WebSocket upgrade, then `initialize` and `initialized`;
    - one or two `thread/read` calls.
  - Per-thread refusals (`ThreadUnmapped`, `Rejected`, a timeout after the handshake) never enter a backoff. A forked or other-home conversation that waits for minutes is reopened, refused and logged (`permission-verification-unavailable`) every second.
  - The log file is deleted once it passes 250 KB, so after about an hour per such conversation these lines can push out other diagnostic codes.
  - Client-side cost per open is probably a few milliseconds (hypothesis: the earlier probe measured about 0.1 ms per ioctl connect in analysis-023; the process queries were not measured).
  - Whether a new `initialize` every second costs the shared backend anything is unknown. QA-030 saw no backend log line for a bare connection natively, but did not test `initialize`.
- Proposed improvement and benefit:
  - Add a short per-session negative cache for refusals that will repeat. For example, remember `ThreadUnmapped` until the attachment or the backend identity changes.
  - Back off per session for per-thread failures.
  - Log once per reason per session instead of once per poll.
  - Optionally, reuse one connection per poll for several conversations of the same home.
- Behavior-preservation risks: the negative cache must expire when the backend changes (`Replaced`) or the scope changes, so a later valid mapping is not missed.
- Evidence type: performance hypothesis. The rate comes from source observation; the cost was not measured.
- Verification / measurement plan:
  - A stand-in run with one waiting forked conversation for 60 s, counting `Accepted` connections and log lines (about 60 of each are expected today).
  - Optionally, in a native no-quota run, count backend log rows per `initialize`.
- Relationship: new; builds on the per-open model accepted in ANALYSIS-2300.

### ANALYSIS-2406: Some 3.2 claims are tested only through injection or not at all

- Severity: suggestion
- Related task / requirement: task 3.2 acceptance (one opener shared by owned and shared access, shared backoff, refusal reasons)
- Locations:
  - `src/BellTower/Tray/TrayApplication.cs` 67-70
  - `tests/BellTower.Tests/Providers/CodexTransportTests.cs` (`ASupersedingListenerIsSeenByTheNextOpenAndTheVerifiedConnectionKeepsItsBackend`)
  - `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs` 222-233
  - `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs` 15, 97-101
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs` 11-26 (`CodexAccess.Key`)
- Observation:
  - Nothing tests that the tray gives both services the same opener. A regression to two openers, with separate backoff and replacement tracking, would pass. `SelectionRuleIsOneForBothServicesAndFailsClosed` tests only `Select`.
  - The supersession test only changes the injected `peerOf`. It never starts a different listener at the path, and its `File.Exists(path)` assertion is trivially true.
  - `MissingBackendReportsItsReasonAndDoesNotRetryDuringBackoff` uses the real clock. Its "does not retry" claim holds only if the second call lands within 2 s, and the test asserts the same status in either case.
  - Elevation is exercised only through an injected `isElevated`. `ProcessElevationCanBeReadForTheCurrentProcess…` passes whatever value it reads.
  - `CodexSharedAccessTests` uses a 400 ms read budget on success paths (ANALYSIS-2204, deferred).
  - `CodexAccess.Key` is used only by test fakes. `Shared.Key` returns the raw home rather than `HomeKey`, which contradicts its own comment ("identifies the runtime the connection reaches").
- Proposed improvement and benefit:
  - Make the opener visible from both pumps, or build both services through one factory a test can check.
  - Freeze the clock in the backoff test (inject a `CodexSharedAccess` with a clock) and assert the attempt count.
  - Rename the supersession test to say what it proves, or add a variant that uses a real `ChildListener`.
  - Remove `CodexAccess.Key` (fakes can switch on the record type), or define it as `HomeKey` for shared access.
- Behavior-preservation risks: tests only, plus one internal member.
- Evidence type: source observation.
- Verification / measurement plan: after the change, a mutant that gives the permission pump its own opener in `TrayApplication` must fail a test.
- Relationship: related to ANALYSIS-2204 (budgets, sequential "concurrent" test) and ANALYSIS-2301 (child listeners).

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2201 | `head=a58c4e3 … untracked=7/55de4a6a489e` | The client interfaces no longer declare `ConnectAsync`. `CodexRuntimeOpener.Select`/`ConnectAsync` is the only place that selects and connects. Both services take a `Func<CodexAccess,…>` and the tray shares one opener. `NativeOwnedRuntimeProcessTests` and `CodexSharedRuntimeServiceTests` reach shared reads and writes through the services. Targeted tests passed 103/103. | Verified (remaining test gap: ANALYSIS-2406) |
| ANALYSIS-2203 | same | `CodexConnection.Reason` is used by the handshake, `CallAsync` and `ReadThreadAsync` on strict transports. Tests for a silent, dropped, malformed or rejecting backend after the handshake pass, as does caller cancellation. The owned (non-strict) path keeps its previous exception types. | Verified |
| ANALYSIS-2205 | same | `HomeKey` (full path, trailing separator trimmed, upper-cased on Windows) keys both `schedule` and `verified`. The containment check was removed. The spelling and drive-root tests pass. | Verified |
| ANALYSIS-2300 | same | The listener probe is gone, `EnsureCurrent` checks liveness only, and `ConnectAsync` no longer calls it. `Accepted == 1` after 3 requests. Supersession is seen as `Replaced` on the next open (tested only with an injected peer, ANALYSIS-2406). | Verified |
| ANALYSIS-2302 | same | Distinct `ImageMismatch`, `CommandLineMismatch`, `PeerUnreadable`, `PeerUnsupported` and `ElevatedPeer` reasons; checks run in order; each reason has its own status text; the evidence statement was corrected; elevated peers are refused. | Verified for the reasons and the policy. Some labels are still wrong (ANALYSIS-2403); an elevated peer was not exercised natively. |
| QA-3000 | same | The real ioctl on a TCP socket returns null and gives `PeerUnsupported`. A disposed socket returns null. A nonexistent PID gives `BackendNotVerified`. An injected unreadable identity gives `PeerUnreadable`. | Verified |

## Assessment and limitations

- Scope covered:
  - DEV-036 sources, tests and docs in the review target, plus the neighbors listed above.
  - Architecture: the opener is the single place that selects and connects, in the right layer, with no duplicated policy left.
  - Field minimization holds.
  - Parity: Claude is unchanged and selection is gated by provider. Only Codex scenarios exist; the reason is documented (design decision 4, task 3.4).
  - All six folded findings are implemented.
- Two warnings:
  - ANALYSIS-2400: the thread-identity "proof" is overstated, and the fork refusal protects nothing. Needs an orchestrator decision.
  - ANALYSIS-2401: concurrent failures multiply the home's backoff. Measured.
- Four suggestions: ANALYSIS-2402 to ANALYSIS-2406.
- Accepted by the orchestrator and not raised again: an elevated Bell Tower, or a system where every process is elevated (UAC off), refuses every shared backend as "runs elevated". The capability matrix should say so.
- Not performed:
  - full test suite;
  - native Codex runs;
  - elevated-peer behavior;
  - measuring the per-open cost or the backend effect of repeated `initialize`;
  - re-running the developer's mutants.
- Writes:
  - The isolated build output went to `artifacts/analysis-024/`, which git ignores.
  - The probe project and its build went to the session scratchpad, outside the repository. I first wrote the probe into a scratchpad folder left from an earlier session (`scratchpad/probe`). I then moved my two files out and removed only the `bin` and `obj` folders my build had created there; that folder's earlier files are untouched.
  - No repository source, lock file or provider setting was written. A lock-file status check after the build showed no changes.
  - The reserved report write was refused by the client; this text is returned inline.
- Identity:
  - Stable from the start through my build at 16:04Z.
  - From 16:07Z a concurrent QA mutation (`CodexBackendCheck.cs` replaced, `.qa031bak` backup equal to the reviewed file, and later a tracked-file change) produced `files=29 … untracked=8/5fe6d008ebdb`, then `diff=b5786c659ca6 untracked=8/7d10f863cdfe`. This analyzer did not cause it.
  - The orchestrator should confirm the identity returns to `files=28 diff=f1c1a7f1e8c2 untracked=7/55de4a6a489e` after QA restores the file.
- The orchestrator owns triage and ledger status.
