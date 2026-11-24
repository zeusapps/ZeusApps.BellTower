<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-026 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

```
status: done
role / client / model: code-analyzer / Claude Code delegated subagent (Agent SDK; client version not available to the subagent) / claude-opus-5-5
task / finding IDs: task 3.2 (DEV-038). Rechecked: ANALYSIS-2500 verified (measured); the ANALYSIS-2401 residuals (a) ABA after a success and (b) the due check and snapshot not taken atomically, both verified; the 2401 test assertion gap, verified; the ANALYSIS-2400 comment residual, verified. No new findings and no blocking defects. ANALYSIS-2600 to 2699 are unused.
changed or report paths: the client refused the write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-026.md ("Subagents should return findings as text"). The full report text is below, inline.
checks:
- dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <26 changed/new .cs> -> passed
- dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-026 -p:RestoreLockedMode=true -> passed
- dotnet test <isolated dll>, targeted Codex/shared/native-name classes -> passed 120/120
- CodexSharedAccessTests and CodexSharedRuntimeServiceTests looped 5 times during the full suite -> passed 5 × 42/42
- full suite without the process tests, isolated layout -> 1375 passed, 42 failed. All 42 are CLI, setup or capture tests that cannot find bell-tower.exe in this layout (cause confirmed); none is in a Codex class.
- disposable timing probe with production budgets -> measured
- standard-layout full suite and native Codex -> not run
target identity: before 16:46Z and after 16:54:25Z, identical: head=a58c4e3 baseline=a58c4e3 files=30 diff=32847c0acc22 untracked=9/ea297f1a990f
limitations / decisions needed:
- None blocking.
- Non-blocking observations, not findings:
  1. Under thread-pool starvation of more than about 0.5 s at the worst-case connect deadline, a stall can still end as an unrecorded caller cancellation. This is the pre-fix behavior and fails closed; it was measured in 1 of 3 runs under heavy induced load.
  2. A read stall after a fast open is still bounded by the caller's token: generic status, and the pump retries about every 4 s.
  3. CodexTransportBudget.Default is no longer referenced anywhere.
  4. The concurrency test has the tightest window (400 ms), but it did not flake under load.
- The report write was refused; the text is inline.
next: the orchestrator saves the report below as reports/analysis-026.md and triages the rechecked IDs.
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-026 (recheck of DEV-038, task 3.2)

## Review target

- Reviewer / role: ANALYSIS-026 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code delegated subagent (Agent SDK); the client version is not available to the subagent; the parent's permission mode; claude-opus-5-5
- Date: 2026-11-05T16:54Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`. ANALYSIS-026 rechecks the following after fix cycle 2 (DEV-038), plus any regression from the fix. This is the last permitted fix cycle for 3.2.
  - ANALYSIS-2500
  - the ANALYSIS-2401 residuals (ABA after a success; the due check and snapshot not taken atomically)
  - the ANALYSIS-2400 comment residual
- Task and requirement references: [tasks.md](../tasks.md) 3.2; [design.md](../design.md) decision 4; [implementation-038](../evidence/implementation-038.md)
- Scope:
  - `src/BellTower/Providers/{CodexSharedAccess,ICodexTransport,CodexConnection,CodexSharedTransport,CodexThreadIdentity,CodexBackendCheck}.cs`
  - `src/BellTower/Sessions/{CodexRuntimeOpener,CodexNameService,CodexPermissionPump,CodexAccessStatus}.cs`
  - `src/BellTower/Tray/TrayApplication.cs` 67-70
  - `docs/architecture.md` line 30
  - `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs` 248-342
  - `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs` 313-356
- Neighbors read: `src/BellTower/Core/NativeNameSchedule.cs`, `src/BellTower/Providers/CodexLoopbackTransport.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs`
- Baseline commit: a58c4e3
- Implementation identity before the analysis (16:46Z):
  - HEAD `a58c4e37e895`; 30 files, 9 of them untracked
  - Tracked diff SHA-256: `32847c0acc22969e85cb64c161e6d2d5b400ed7794cacad1eaf2e3d5dbb94ca5`
  - Changed untracked hashes since analysis-025:
    - CodexThreadIdentity.cs `855fce5b…`
    - CodexSharedAccessTests.cs `5cff9492…`
    - CodexSharedRuntimeServiceTests.cs `22d56e2f…`
    - implementation-038.md `9e167f3a…` (new)
  - The other five untracked hashes are unchanged.
  - Summary: `head=a58c4e3 baseline=a58c4e3 files=30 diff=32847c0acc22 untracked=9/ea297f1a990f`. This matches the assignment.
- Before/after identity:
  - The summary was identical right after the isolated build.
  - It was still identical at 16:54:25Z, after the probe, the format check and the test runs.
  - No mutation by the concurrent QA reviewer was seen during this analysis.
- Prior reports / rechecked findings:
  - [analysis-025](analysis-025.md): ANALYSIS-2500 and the ANALYSIS-2401 residuals (a) and (b), plus the test assertion gap
  - [analysis-024](analysis-024.md): ANALYSIS-2400 and ANALYSIS-2401
  - [qa-032](qa-032.md): QA-3200 (context only)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 2500: budget arithmetic | Inspected `CodexTransportBudget.Shared`, `CodexConnection.ConnectAsync`/`Reason`/`Limited`, and where both services create their 3 s token | **Phases.** The connect phase (UDS connect, peer check, HTTP upgrade) is limited to 1 s. The handshake phase (initialize and initialized) is limited to 1.5 s. Each phase has a linked source started at the beginning of that phase, so the open ends by about 2.5 s after it starts.<br>**Caller token.** Both services create their 3 s source immediately before `ConnectAsync`. The pump does this after `Select` and after the semaphore wait (`CodexPermissionPump.cs` 112-114). The name service does it after `Select` (`CodexNameService.cs` 52-53). So almost none of the margin is used up before the open starts.<br>**Classification.** `Reason` maps an `OperationCanceledException` to `Timeout` only while the caller's token is not cancelled. A Timeout is an `IOException`, so `CodexSharedAccess` records it. | Margin: about 1.5 s on the common path (backend stalls after the upgrade) and about 0.5 s in the worst case (upgrade delayed close to 1 s, then silence) |
| 2500: measured timing | Disposable probe on the isolated build: a raw UDS backend with a configurable upgrade delay that then stays silent; `CodexSharedAccess` with production budgets; a 3 s caller token created just before `OpenAsync`, as the services do; 24 logical CPUs | **Common path, delay 0:** `Timeout` after 1506-1555 ms in 3 of 3 runs; failure count 1.<br>**Worst case, delay 950 ms:** `Timeout` after 2465-2505 ms in 3 of 3 runs; failure count 1. | Measured. A stall is recorded as `Timeout` before the 3 s caller token on both paths. |
| 2500: scheduler jitter (measured) | Same probe. Thread-pool starvation was injected just before the budget deadline by queuing N work items that each block for M ms. | **Common path:**<br>- 48 items × 300 ms: 3 of 3 runs `Timeout`.<br>- 72 items × 600 ms: 3 of 3 `Timeout` (2.0-2.5 s).<br>- 96 items × 1000 ms: 3 of 3 `Timeout`.<br>**Worst case:**<br>- 48 items × 150 ms: 3 of 3 `Timeout`.<br>- 48 items × 300 ms: 3 of 3 `Timeout` (about 2.7 s).<br>- 72 items × 600 ms: 2 `Timeout` (about 2.9 s) and 1 caller cancellation (3.04 s, failure count 0). | The fix tolerates realistic jitter. A fallback appears only when the continuation is delayed by more than about 0.5 s, and only on the worst-case path. The result is then the pre-fix behavior: nothing recorded, no backoff, generic status. That is benign and fails closed. Not blocking. |
| 2500: stalled read after a fast open | Source reasoning | **Timing.** `Shared.Read` (3 s) starts after the open, so the services' 3 s token always fires first. A read stall therefore ends as caller cancellation (a raw `OperationCanceledException`) and is outside `CodexSharedAccess`. The open succeeded, so the home count is reset, as it should be.<br>**Name service.** Status "native metadata connection failed", plus its per-session `NativeNameSchedule` backoff (2, 5, 15 … s).<br>**Permission pump.** A local-log line for each check. The next poll comes at least 1 s after the previous refresh ends (the `running` flag), so there are about 4 s between attempts, with at most 4 connections at once, each held for at most 3 s.<br>**Owned path.** Behaves identically. | Acceptable and bounded. This is not part of 2500, which concerns stalls during the open, and implementation-038 states it. The only cost is a less specific status text and some log volume while a backend that accepts the handshake is stuck. Not blocking. |
| 2401: per-home generation | Inspected `CodexSharedAccess.OpenAsync` 34-78 and `NativeNameSchedule` | **Due and snapshot.** `Due` and the snapshot are now taken in one `gate` section (38-46). A `BackedOff` result throws inside it, before any snapshot.<br>**Failure.** A failure is recorded only when the generation is unchanged. Recording it bumps the generation under the same gate (58-65).<br>**Success.** A success resets the schedule and bumps the generation under the gate (70-77).<br>**Caller cancellation** touches neither.<br>**Lock order** is always access gate, then schedule gate. The schedule is private to the access in production, so no inverse order exists.<br>**Size.** The dictionary is bounded by the number of distinct homes (one in practice). | Correct: it closes both (a) and (b) |
| 2401: semantics at the edges | Reasoning over interleavings | **(1) A failure that started before a success** is not recorded, by design. If the backend really broke after that success, the next attempt is due immediately (count 0) and records the failure, so the cost is at most one extra attempt.<br>**(2) A success that started before a recorded failure** resets the count. The backend answered, so this is correct.<br>**(3) Overlapping failures in one window** count once.<br>**(4) Failures in later windows** advance the count (1, 2, 3). | Correct |
| 2401: one opener for both services | `TrayApplication.cs` 67-70; `NativeNamePump` passes the opener through | One `CodexRuntimeOpener`, so one `CodexSharedAccess`: one schedule and one generation for each home, shared by the name service and the permission pump | Correct |
| Regression: owned path | Searched `src` for budget use; inspected `CodexLoopbackTransport` | The owned transport still has no budget (`Budget => null`), so only the caller's token bounds it. Its exception types are unchanged. `CodexTransportBudget.Default` is now referenced nowhere in `src` or `tests`. | No regression |
| Regression: concurrency | Pump semaphore (4) and the `running` flag; the name service's synchronous `GetResult` | The budgets get shorter, but no new blocking or shared state is added. The generation gate keeps critical sections to a few instructions and never awaits under a lock. | No regression |
| 2400: comment residual | Read `ICodexTransport.cs` 30-34 and `CodexThreadIdentity.cs` 5-17 | **`RequiresProvenThreads`** now says that the exact requested id is the proof, on a peer-verified backend, and that the fork and agent refusal is a conservative policy.<br>**`CodexThreadIdentity`** says that a missing id is refused and that absent fork and session fields read as a root, which matches `Require`.<br>**`docs/architecture.md` line 30** describes the generation and the 1 s and 1.5 s budgets. | Verified |
| Tests: meaningfulness | Inspected the new tests; reasoning against analysis-025's measured Default-budget result (caller cancellation, count 0) | **Service tests** (`AStalledSharedBackend…`) use production budgets (`null` gives `Shared`) and a stand-in that is silent after the upgrade (the 1.5 s path).<br>- With the old 3 s budgets the caller's token fires first. The status would then be "connection failed", the count 0, and the second Sync or poll would connect again. Each assertion would fail, so these tests guard 2500.<br>- The pump test also covers a second conversation of the same home.<br>**ABA and before-success tests:** each reproduces the exact interleaving, with a 3 s handshake budget as the window.<br>**Concurrency test:** now asserts `Timeout` for each open and `Accepted == 4`.<br>**`SequentialFailuresInLaterWindowsAdvanceTheBackoff`:** covers QA-3200. | Meaningful |
| Tests: timing sensitivity | Margins by reading the source, then the load runs below | **Service tests:** 1.5 s of margin, and the second Sync or poll needs less than 2 s of real-clock delay to stay inside the backoff.<br>**ABA and before-success tests:** about 3 s of margin for the C and D steps.<br>**Concurrency test:** the tightest. All four `Task.Run` opens must pass `Due` within about 400 ms of the first open's handshake (the `Quick` budget). Under extreme starvation a late open would get `BackedOff` and fail the new `Timeout`/`Accepted == 4` assertion. That is the price of the assertion analysis-025 asked for.<br>**Load result:** no flake observed. | Acceptable. A residual flake risk exists only for the concurrency test under starvation of several hundred ms. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <26 changed or new .cs under src and tests>` | exit 0 | Passed |
| Targeted tests | `dotnet build tests/BellTower.Tests -c Release --artifacts-path artifacts/analysis-026 -p:RestoreLockedMode=true` (git-ignored output), then `dotnet test <that dll>` filtered to CodexSharedAccess, CodexSharedRuntimeService, CodexTransport, CodexName, CodexPermission and NativeName | 120/120 passed | Passed |
| Under load | Ran CodexSharedAccessTests and CodexSharedRuntimeServiceTests 5 times in a row, at the same time as the full suite (below) | 5 × 42/42 passed | No timing flake observed under full-suite load |
| Full suite (isolated layout) | `dotnet test <that dll> --filter FullyQualifiedName!~ProcessTests` (3 min 11 s) | **Result:** 1375 passed, 42 failed.<br>**Where the failures are:** CliTests 24, NativeSetupCliTests 4, IdentityControlCliTests 2, CaptureTests 2, PowerShellModuleTests 8, PrelaunchPresentationTests 2.<br>**Cause, sampled on one test:** a `Win32Exception`. The executable `bell-tower.exe` is looked for under `src/BellTower/bin/...` and is absent in the isolated artifacts layout. | Layout-only failures, the same limitation analysis-025 recorded. None is in a Codex, shared-access or native-name class. The full suite was not run in the standard layout (implementation-038 reports 1515/1515 there). |
| Parity | Provider gates | The shared-backend path remains Codex-only (design decision 4). Claude paths are untouched by DEV-038. | No regression. These checks cover Codex only. |

## Findings

**No findings.** No blocking defect was found. No new suggestion is raised, as a recheck requires.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2500 | `head=a58c4e3 baseline=a58c4e3 files=30 diff=32847c0acc22 untracked=9/ea297f1a990f` | **Production budgets (measured).** A stall becomes a recorded `Timeout` after about 1.5 s (stall after the upgrade) or about 2.5 s (worst case), before the services' 3 s token. The failure count is 1. The service tests assert the status "shared backend did not answer in time" and that the backoff applies to the whole home in both services.<br>**Jitter.** Up to about 0.5 s of continuation delay at the worst-case deadline is absorbed (about 1.5 s on the common path). Under heavier induced starvation the worst-case path fell back to an unrecorded caller cancellation in 1 of 3 runs. That is the pre-fix behavior, not worse, and it fails closed.<br>**Stalled read after a fast open.** Still bounded by the caller's token, with a generic status. Bounded and acceptable, and outside this finding's scope. | Verified (measured). A non-blocking residual under severe thread-pool starvation. |
| ANALYSIS-2401 residual (a): ABA after a success | same | A per-home generation is bumped on every success and every recorded failure, and a failure is recorded only when the generation is unchanged. `AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain` reproduces the analysis-025 sequence and asserts a count of 1. `AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` asserts a count of 0. | Verified |
| ANALYSIS-2401 residual (b): due check and snapshot not atomic | same | `Due` and the generation snapshot are taken in one gate section (`CodexSharedAccess.cs` 38-46). The window is closed by construction; deterministic testing is not practical. | Verified (by source) |
| ANALYSIS-2401 test assertion gap | same | The concurrency test asserts `Timeout` for each open and `Accepted == 4`. It passed 6 times, 5 of them under full-suite load. | Verified |
| ANALYSIS-2400 comment residual | same | `ICodexTransport.RequiresProvenThreads` and the `CodexThreadIdentity` summary now match the corrected statement and the code. | Verified |

## Assessment and limitations

- **Verdict:** the DEV-038 fixes are correct, and I found no blocking defect.
  - ANALYSIS-2500 is fixed in production: a stall now records a timeout and backs off both services.
  - The 2401 residuals are closed by the per-home generation.
  - The 2400 comment now matches the code.
  - I found no regression for the owned path, for concurrency, or for Claude.
- **Non-blocking observations (not findings, for the orchestrator's information on this last cycle):**
  1. Under thread-pool starvation of more than about 0.5 s at the worst-case connect-phase deadline, a stall can still be classified as caller cancellation and go unrecorded (measured in 1 of 3 runs under heavy induced load).
  2. A read stall after a successful open shows the generic "connection failed" status, and the pump retries it about every 4 s.
  3. `CodexTransportBudget.Default` is no longer referenced anywhere.
  4. The concurrency test is the most timing-sensitive test (a 400 ms window), but it did not flake under load.
  None of these affects correctness or safety.
- **Not performed:**
  - the full suite and the process tests in the standard layout: the isolated layout lacks the product executable, a cause I confirmed;
  - a native Codex run;
  - source mutants: I may not edit sources in the shared tree, so the Default-budget mutant is covered by analysis-025's measurement and the reasoning above.
- **Writes:**
  - the isolated build in `artifacts/analysis-026/` (git-ignored; the identity was unchanged afterwards);
  - a disposable probe project and its build in the session scratchpad, outside the repository;
  - temporary socket homes under the system temp area, deleted by the probe.
- The client refused the reserved report write; this text is returned inline.
- No repository source, lock file, provider setting or user process was touched.
- **Identity:** unchanged from start to finish (`head=a58c4e3 baseline=a58c4e3 files=30 diff=32847c0acc22 untracked=9/ea297f1a990f`).
- The orchestrator owns triage and ledger status.
