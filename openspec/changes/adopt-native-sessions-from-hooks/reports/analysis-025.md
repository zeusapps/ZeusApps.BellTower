<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-025 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

```
status: done
role / client / model: code-analyzer / Claude Code delegated subagent (Agent SDK; client version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.2 (DEV-037). Rechecked ANALYSIS-2400: verified, with one leftover comment that still overstates the proof. Rechecked ANALYSIS-2401: verified for the overlapping-failure case (measured), with two narrow gaps and a missing test assertion. New ANALYSIS-2500 (warning, measured): not caused by DEV-037.
changed or report paths: the client refused the write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-025.md ("Subagents should return findings as text"). The full report text is below for the orchestrator to save as is.
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <26 changed/new .cs> -> passed; dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-025 -p:RestoreLockedMode=true -> passed (output folder git ignores; identity unchanged right after); dotnet test <that dll> filtered to CodexSharedAccess|CodexSharedRuntimeService|CodexTransport|CodexName|CodexPermission|NativeName -> passed 115/115; NativeOwnedRuntimeProcessTests in the isolated layout -> not valid (the unchanged control NativeAgentResolveProcessTests also fails 28/28 there; the layout does not provide the hook executables); disposable probe on the isolated build -> measured (overlap, ABA, production caller token); full suite, native Codex -> not run
target identity: before 16:24:52Z head=a58c4e3 baseline=a58c4e3 files=29 diff=baff51d22ebb untracked=8/d363d9c1b447; after 16:31:03Z identical; no change seen from the concurrent QA reviewer
limitations / decisions needed: (1) ANALYSIS-2500: both services pass a 3 s caller timeout, the shared open's budgets are also 3 s, and the caller timeout fires first. So a stalled shared backend is never backed off and is reported as "native metadata connection failed". That undercuts the 2401 fix in production. It is a new ID in a recheck assignment; I raised it because it decides whether 2401 has any effect on stalls, and it needs triage. (2) 2401 gaps: an ABA after a success (measured: count 2 instead of 1) and a snapshot that is not taken atomically with Due (source). A per-home generation would close both. The test should assert Timeout for each open, or Accepted == 4. (3) 2400 leftover: the doc comment at ICodexTransport.cs 30-33 still says "must be proven to be the hook's own conversation". (4) The report write was refused; the text is inline.
next: the orchestrator saves the report below as reports/analysis-025.md, triages ANALYSIS-2500 and the 2401 gaps, and routes the 2400 comment fix.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-025 (recheck of DEV-037, task 3.2)

## Review target

- Reviewer / role: ANALYSIS-025 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code delegated subagent (Agent SDK); client version not available to the subagent; parent's permission mode; claude-opus-5-5
- Date: 2026-11-05T16:31Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`; ANALYSIS-025 (recheck of ANALYSIS-2400 and ANALYSIS-2401 after fix cycle 1, DEV-037, plus regression from the fix)
- Task and requirement references:
  - [tasks.md](../tasks.md) 3.2, 4.2 and 6.1 (the fork and delegated-thread hook probe)
  - [design.md](../design.md) decision 4
  - [implementation-037](../evidence/implementation-037.md), [implementation-036](../evidence/implementation-036.md)
  - [native-adoption-011](../evidence/native-adoption-011.md) lines 71-73
- Scope:
  - `src/BellTower/Providers/{CodexThreadIdentity,CodexSharedAccess,CodexConnection,CodexSharedTransport,ICodexTransport,CodexBackendCheck}.cs`
  - `src/BellTower/Sessions/{CodexAccessStatus,CodexRuntimeOpener,CodexNameService,CodexPermissionPump}.cs`
  - `src/BellTower/Tray/TrayApplication.cs` 67-70
  - `docs/architecture.md` line 30
  - `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs` and `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs` 210-231
- Neighbors read: `src/BellTower/Core/NativeNameSchedule.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs`
- Baseline commit: a58c4e3
- Implementation identity before the analysis (16:24:52Z):
  - HEAD `a58c4e37e895`; 29 files, 8 of them untracked.
  - Tracked diff SHA-256: `baff51d22ebb3b7c416c4f348790d35212916f80c094a5f19a08dedaf252f746`.
  - Untracked file hashes:
    - implementation-036.md `b0f866fe…`
    - implementation-037.md `9c724c9c…`
    - CodexBackendCheck.cs `bda17e82…`
    - CodexThreadIdentity.cs `5d8ed5cd…`
    - CodexAccessStatus.cs `1918788f…`
    - CodexRuntimeOpener.cs `e29066df…`
    - CodexSharedAccessTests.cs `8cdef1bc…`
    - CodexSharedRuntimeServiceTests.cs `265c20fc…`
  - Summary: `head=a58c4e3 baseline=a58c4e3 files=29 diff=baff51d22ebb untracked=8/d363d9c1b447`. This matches the assignment.
- Before/after identity:
  - Right after my isolated build, the summary was unchanged.
  - At 16:31:03Z, after the probe and the format check, it was still identical.
  - I saw no transient change from the concurrent QA reviewer during this analysis.
- Prior reports / rechecked findings:
  - [analysis-024](analysis-024.md): ANALYSIS-2400 and ANALYSIS-2401
  - [qa-031](qa-031.md): QA-3101 (context only)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 2400: code | Inspected `CodexThreadIdentity.Require` and the `CodexTransportFailure` docs | Only a true mismatch gives `ThreadUnmapped`: no thread, a different id, or a missing id on an unowned runtime. Policy refusals give `ThreadUnverified`: a `sessionId` other than the id, or a non-null `parentThreadId`, `forkedFromId`, `agentRole` or `agentNickname`. The owned path is unchanged: it refuses only a present, different id. | Matches the orchestrator decision |
| 2400: status texts | Inspected `CodexAccessStatus.For`; tests `ForkedOrDelegatedThreadIsRefusedByPolicy…` and `ADifferentThreadIdIsAMismatch…` | The policy status is "fork or delegated thread mapping unverified". The mismatch status is "runtime thread does not match the conversation". Both tests assert the exact text and that no `thread/name/set` is sent. | Honest. One small label point: a missing id is a "mismatch" text, not "unverified". It still fails closed, and the enum doc covers it. |
| 2400: docs and evidence | Read `docs/architecture.md` line 30, implementation-037 and `tasks.md` 6.1 | The architecture text names the actual proof (exact id plus a peer-verified backend). It calls the fork and agent refusal "an explicitly conservative policy … not a proof" and links the 6.1 probe. Implementation-037 explicitly corrects the overstated sentences in implementation-036, which stays unchanged as history. Task 6.1 now holds the probe (ANALYSIS-2400, QA-3101). | Honest, with one leftover overstatement in a code comment (see Re-verification) |
| 2400: `sessionId == id` | Compared the check with evidence 011 lines 71-73 | Roots and forks both report `sessionId` equal to the thread id. What delegated threads report is not known. So the check never tells a fork from a root (the comment says this), but it does refuse a thread that names another session as its session root. That is the same "not a root of its own session" category the policy already refuses. It costs nothing and is labeled unverified, not mismatch. | Keeping it is justified as documented |
| 2401: snapshot logic | Inspected `CodexSharedAccess.OpenAsync` 34-60 and `NativeNameSchedule` (which locks on every call) | When an attempt starts, it records the home's failure count. When it fails, it calls `Failed` only if the count, rechecked under the access gate, still equals that snapshot. `BackedOff` attempts take no snapshot and count nothing. Caller cancellation counts nothing. `Succeeded` resets the count to 0 outside the gate. | Correct for overlapping failures; two narrow gaps (see Re-verification) |
| 2401: one opener for both services | `TrayApplication.cs` 67-70; `CodexRuntimeOpener` holds one `CodexSharedAccess` | The name service and the permission pump use the same access object, so they share one schedule and one snapshot. | Coalescing works across both services. In production, however, a stall is not counted at all: ANALYSIS-2500. |
| 2401: the new test | Inspected `ConcurrentFailedOpensCountOneBackoffFailureAndALaterSuccessResets`; reproduced its shape in a probe five times | Measured in every run: all four opens failed with `Timeout`, the stand-in accepted 4 connections, and the failure count was 1. The opens really do overlap: each takes 400 ms, and `Task.Run` starts them within a few ms. The test, however, asserts only `CodexTransportException`, so a late open that got `BackedOff` would still pass. | The overlap is real, but the test does not assert it |
| Regression: ABA after a success (measured) | Disposable probe on the isolated build. Frozen clock; a toggle predicate in `CodexBackendCheck.Accepting`; 2 s handshake budget. | 1. Open X fails: count 1. 2. Window expires; A starts (snapshot 1) and is silent. 3. C succeeds: count 0. 4. D fails fast (`BackendNotVerified`): count 1. 5. A times out: count 2, although D and A failed in the same window. | Residual of the 2401 remedy (see Re-verification) |
| Production caller token (measured) | Same probe: default budgets (`CodexTransportBudget.Default`, 3 s each), a 3 s caller `CancellationTokenSource` as both services use, silent stand-in, two opens | Both opens: `TaskCanceledException` after 3.0 s; failure count 0; the second open made a new connection (accepted 2) | ANALYSIS-2500 |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <26 changed/new .cs under src, tests>` | exit 0 | Passed |
| Targeted tests | Built with `dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-025 -p:RestoreLockedMode=true` (git-ignored output; identity confirmed unchanged right after). Ran `dotnet test <that dll>` filtered to `CodexSharedAccessTests\|CodexSharedRuntimeServiceTests\|CodexTransportTests\|CodexNameTests\|CodexPermissionTests\|NativeNameTests`. | 115/115 passed | Passed |
| Process tests in the isolated layout | Same dll; `NativeOwnedRuntimeProcessTests`, plus the unchanged `NativeAgentResolveProcessTests` as a control | 36 failures in `NativeOwnedRuntimeProcessTests`. The unchanged control class also failed 28/28 (`KeyNotFoundException` for the session key: the hook executables are not found where this layout puts them). | Not valid in this layout; not attributed to the fix. QA-031 ran them in the standard layout. |
| Parity | Inspected provider gates (unchanged) | Shared access is still Codex-only (design decision 4). Claude paths were not touched by DEV-037. | No regression. Checks cover Codex only. |

## Findings

### ANALYSIS-2500: The services' 3 s caller timeout fires before the shared open's 3 s budgets, so a stalled shared backend is never backed off and is reported as a generic connection failure

- Severity: warning
- Related task / requirement:
  - task 3.2 ("one normalized home key for backoff"; distinct refusal reasons)
  - design decision 4 ("existing backoff")
  - the `CodexSharedAccess` contract ("a missing, refusing or slow backend is not retried in a loop")
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 112-114
  - `src/BellTower/Sessions/CodexNameService.cs` 52-53
  - `src/BellTower/Providers/ICodexTransport.cs` 52 (`CodexTransportBudget.Default`: 3 s connect, 3 s handshake, 3 s read)
  - `src/BellTower/Providers/CodexConnection.cs` (`Reason`: a cancellation from the caller's token passes through unchanged)
  - `src/BellTower/Providers/CodexSharedAccess.cs` 49 (no failure is recorded when the caller's token was cancelled)
- Observation:
  - Both services create a 3 s `CancellationTokenSource` before they call `opener.ConnectAsync`, and pass that token to the open.
  - The shared transport's connect budget and handshake budget are also 3 s, but each starts later, inside the open. The caller's token therefore always fires first when the backend accepts the socket and then stays silent.
  - The open then ends with a raw `OperationCanceledException` while the caller's token is cancelled. `CodexSharedAccess` treats that as caller cancellation and records no failure.
  - Measured on the isolated build: two opens in a row against a silent stand-in, each with a 3 s caller token, both threw `TaskCanceledException` after 3.0 s. The failure count stayed 0, and the second open connected again (the stand-in accepted 2 connections).
  - Effects:
    - **Permission pump: no home backoff for a stall.** Every 1 s poll reopens a connection for each waiting conversation, up to four at once, and holds each for up to 3 s.
    - **Name service: wrong status text.** It gets the raw exception, so `CodexAccessStatus.For` reports "native metadata connection failed" instead of "shared backend did not answer in time". Its own per-session schedule still backs off.
    - **The 2401 fix does not cover stalls.** The coalescing now works only for failures the transport reports before the caller's token fires: refusals, drops, malformed responses and peer-verification refusals. The slow-backend case measured in ANALYSIS-2401 is not counted at all in production.
  - The stand-in tests do not show this because they use short shared budgets (`Quick`, 400 ms), below the callers' 3 s.
- Proposed improvement and benefit:
  - Make the shared open's connect and handshake budget clearly shorter than the services' operation timeout. For example, the opener could use its own shared budget, or the services could pass their shutdown token to the open and apply the 3 s timeout only to the reads.
  - Benefit: a stall is then recorded once as `Timeout`, backs off for the whole home in both services, and is shown with its own reason.
- Behavior-preservation risks:
  - The owned path uses the same caller token. Keep its exception types as they are.
  - A stop or dispose cancellation must still not count as a failure.
- Evidence type: measured result (disposable probe on the isolated build) and source observation.
- Verification / measurement plan:
  - A service-level stand-in test with default budgets and a silent shared backend. Assert a failure count of 1, the status "shared backend did not answer in time", and that a second pump poll inside the window makes no new connection (the stand-in's `Accepted` count is unchanged).
- Relationship:
  - New. Found while verifying ANALYSIS-2401's interaction with both services.
  - Not caused by DEV-037: it comes from the DEV-036 wiring (3.2) combined with the caller-cancellation exclusion from 3.1.
  - Related to ANALYSIS-2401 and ANALYSIS-2405.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2400 | `head=a58c4e3 baseline=a58c4e3 files=29 diff=baff51d22ebb untracked=8/d363d9c1b447` | **Code and statuses.** `ThreadUnverified` and `ThreadUnmapped` are separate kinds with separate status texts. The theory test covers six thread shapes, each with its expected kind. Two service tests assert the exact status texts and that nothing is written.<br>**Docs and evidence.** `docs/architecture.md` line 30 now states the actual proof (exact id plus a peer-verified backend) and labels the fork, parent, agent and session refusal as an explicitly conservative policy that waits for the 6.1 probe. The `CodexThreadIdentity` summary says the same. Implementation-037 corrects implementation-036's "must prove" and "would write another conversation's name" sentences. Task 6.1 holds the probe.<br>**`sessionId == id`.** Keeping it is justified as documented: it cannot tell a fork from a root, the code says so, and it refuses only threads that name another session root. That is the same category the policy refuses, under the honest "unverified" label.<br>**Leftover overstatement.** The doc comment on `ICodexTransport.RequiresProvenThreads` (`src/BellTower/Providers/ICodexTransport.cs` 30-33) still says the named thread "must be proven to be the hook's own conversation … rather than assumed from an identifier that merely looks equal". That contradicts the corrected statement, which says id equality is the proof. Minor: the `CodexThreadIdentity` sentence "A missing field is refused instead of guessed" applies only to `id`, because absent fork and session fields are accepted as a root. | Verified for behavior, status texts, docs/architecture.md and evidence. One comment-only residual (`ICodexTransport.cs` 30-33) should be reworded to match. |
| ANALYSIS-2401 | same | **Common case (measured).** Four overlapping failed opens give a failure count of 1 in five of five runs; every open was a real attempt (`Timeout`; the stand-in accepted 4). A later success resets the count to 0. `BackedOff` attempts take no snapshot and count nothing. Because the tray shares one opener (`TrayApplication.cs` 67-70), the name service and the permission pump coalesce through the same count.<br>**Gap (a), ABA after a success (measured).** An attempt that started when the count was 1 and fails after a success (count 0) and a newer failure (count 1 again) passes the equality check and counts again: the count became 2 instead of 1. The extra cost is bounded by the number of attempts in flight (at most 5 today: 4 permission-pump verifications plus 1 name sync). It needs a success during a failing attempt, so it is rare.<br>**Gap (b), non-atomic snapshot.** `Due` (line 34) and the snapshot (line 39) take the schedule lock separately. If another attempt fails between them, this attempt's snapshot already includes that failure, and its own failure counts again. The window is a few instructions.<br>**Fix for both.** A per-home generation, increased under the access gate on every recorded failure and every success and recorded as the snapshot, would close (a) and (b). For (b) alone, taking the snapshot before `Due` is enough.<br>**Test.** The test does overlap in practice, but it asserts only a `CodexTransportException`. Asserting `Timeout` for each open, or `server.Accepted == 4`, would stop it passing without real overlap (for example, under thread-pool starvation where late opens get `BackedOff`).<br>**Production.** Stalls are not counted at all (ANALYSIS-2500). The docs sentence "Concurrent failed opens count one backoff failure per home per window" is true only for failures that are recorded. | Verified for the overlapping-failure case it targets (measured). Two narrow gaps, (a) measured and (b) by source; a test assertion gap; and the production stall gap tracked as ANALYSIS-2500. |

## Assessment and limitations

- Scope covered: the ANALYSIS-2400 and ANALYSIS-2401 fixes in the review target, plus regression from them. DEV-037 introduced no behavior regression for owned runtimes, Claude, or the shared read and write path. Targeted tests pass (115/115), and formatting passes.
- ANALYSIS-2400: the code, the status texts, `docs/architecture.md` and implementation-037 now state the real proof honestly and match the orchestrator's conservative-policy decision. Only the `RequiresProvenThreads` doc comment still overstates it. The fork and delegated-thread refusal now depends on the 6.1 probe. The capability-matrix and compatibility docs do not yet describe shared-backend access at all (5.x work); QA-3101 asked for a row there, and I did not check beyond noting its absence.
- ANALYSIS-2401: the snapshot fix works for the targeted case, measured. Two narrow residual gaps (ABA and the non-atomic snapshot) and the test's missing overlap assertion are recorded for the orchestrator to accept or route.
- ANALYSIS-2500 (warning, measured) is a new ID in a recheck assignment. I raised it because it decides whether the 2401 remedy has any effect on stalls in production. It is not a suggestion and was not caused by DEV-037. The orchestrator owns its triage.
- Not performed:
  - full test suite;
  - process tests in the standard layout (the isolated layout breaks them; the unchanged control class fails the same way);
  - a native Codex run;
  - a test of mutants that revert the fix;
  - measuring the cost of the pump's reopen loop during a stall.
- Writes:
  - the isolated build in `artifacts/analysis-025/` (git ignores it; no lock file changed);
  - a disposable probe project and its build in the session scratchpad, outside the repository (a new empty folder `a025probe`);
  - temporary stand-in homes under the system temp area, created by the probe.
- The client refused the reserved report write; this text is returned inline.
- No repository source, provider setting or user process was touched.
- Identity: unchanged from start to finish (`head=a58c4e3 baseline=a58c4e3 files=29 diff=baff51d22ebb untracked=8/d363d9c1b447`).
- The orchestrator owns triage and ledger status.
