# Analysis 006 evidence: target-005 fixture ownership and time

Date: 2026-10-29. Frozen [target-005](review-target-005.txt), baseline/head `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Scope and provenance

Read the current ledger, [implementation-005](implementation-005.md), target identity and prior QA-004/analysis-005. Only three test paths differ from target-004: `ReminderPlaybackTests.cs`, `WindowsUserIdleSourceTests.cs`, and new `ReminderFixtureDriver.cs`. Production/source/docs remain byte-identical. The full tracked SHA-256 is still `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`.

Factual clarification of earlier report history: the original analysis-004 interruption was an **account usage limit**, not an automatic approval rejection or evidence of a filesystem/report-write restriction. Analysis-005's approval-review phrasing was imprecise. Prior reports are preserved; this numbered report records the correction.

Inspected source while QA ran alone. Executable probes began only after the orchestrator confirmed QA's full run and isolated checks were complete. No analyzer full suite, product build, formatter or redundant documentation CLI was run. The current official .NET sources already recorded in [evidence 005](analysis-005.md#current-official-documentation-consulted-before-interruption) apply unchanged.

## Actual-helper probes

Command: `dotnet run --project artifacts/idle-analysis-006/Probe.csproj -c Release`, outside the sandbox. The ignored project links the **actual** `tests/BellTower.Tests/Tray/ReminderFixtureDriver.cs` into its scratch assembly and references existing application/protocol Release assemblies; no copied helper or product project build is used. Actual source/tray/queue paths use injected observations and silent sinks. No physical input/audio, UI constructor, native session query, microphone, real provider, production data or network send is used.

Recorded final output:

```text
PASS driver propagates original failure and joins driver
PASS dedicated consumer failures remain visible
PASS process-local controlled worker limit established
PASS actual driver reaches default80ms and eight-consumer assertions
cleanup order: held probe selfTimedOut=True, releaseCallbackSawProbeAlreadyExited=True
PASS healthy observation seeded with5s test budget
PASS actual healthy queue reached sink
controlled observation aging: reads=5, ToneCancelled=False, SpeechCancelled=True
PASS 501ms-aged speech observation reproduces cancellation despite5s read budget
```

### Assertion propagation

Run the real driver with a work delegate throwing a recognizable `InvalidOperationException`. The original exception reaches the caller, the release callback runs once, and the driver thread has stopped. Eight dedicated consumers throwing recognizable exceptions produce an aggregate containing all eight. The helper does not silently swallow those assertions or substitute success.

### Ownership-order defect

In the isolated scratch process, temporarily constrain only that process's pool to one worker. Restore its original settings in `finally`; this is a diagnostic constraint, not a proposed runner/application setting.

Use the actual helper from an asynchronous caller, with the actual source wrapper. Its dedicated work thread seeds positive input, starts the held source poll, confirms entry, checks the public default 80 ms rejection and runs eight real dedicated consumers. All policy/single-flight assertions are reached and pass. The driver then signals its completion task.

However, helper `release()` executes only in the awaiting continuation's `finally`. The held poll occupies the worker needed by that continuation. The poll therefore reaches its own three-second fixture timeout and exits **before** the release callback runs. Recorded flags are `selfTimedOut=True` and `releaseCallbackSawProbeAlreadyExited=True`.

This proves that release ownership is still coupled to shared-worker availability. It does not establish the cause of QA's particular failures. The minimal correction is to release the held work in the dedicated driver's `finally`, before publishing success/failure completion. The outer asynchronous `finally` can retain fallback release/join behavior. Preserve original exceptions and report cleanup errors without turning failed work into success.

### Independent healthy-path clock assumption

Use the actual source wrapper with the five-second test read budget, actual tray predicate and actual audio queue. Prewarm a positive idle sample. The first relevant observations are current; only the observation used during speech cancellation has a deliberately aged timestamp (501 ms old) while still reporting 20 minutes idle. The real source correctly returns unknown under its 500 ms freshness policy. The sink is reached, tone cancellation is false, and speech cancellation is true: the same assertion symptom reported by QA.

No sleep or timing benchmark is needed for this controlled age transition. The chosen aged timestamp proves the policy mechanism; it is **not** evidence that QA's real sample aged this way. QA's actual read/observation timing was not recorded.

The source read budget and observation age are independent. Waiting up to five seconds for a task does not ensure its observation is at most 500 ms old when consumed. The healthy queue fixture does not use `ReminderFixtureDriver`, and this reproduction uses normal restored pool settings. Therefore fixing driver cleanup cannot by itself remove this separate clock precondition.

## Minimal bounded correction

1. Keep release ownership on the dedicated driver and release held work before signaling completion; retain joins and exception propagation.
2. To satisfy the original deterministic clock/idle requirement while retaining actual wrapper/tray/queue coverage, use one controlled UTC clock for the source and tray reminder-validity check. The source already has an injectable clock; the tray predicate hardcodes `DateTimeOffset.UtcNow`. The smallest clean extension is an internal tray-validity clock seam whose production default remains UTC, with tests setting that seam explicitly. This is a narrow source-path scope clarification, not a product policy/control change.

The clock seam is necessary to remove the demonstrated *real-clock assumption* from this particular end-to-end positive fixture; it is not claimed necessary to repair a proven production defect or to explain QA's exact failure. A fixture that retains hardcoded real UTC in one half cannot fully control the freshness interval. Do not widen the 500 ms age limit, production 80 ms budget, generic waits or global pool settings. Keep separate explicit stale/future and timeout tests, using controlled time and the real monotonic timeout budget, so the positive test is not obtained by masking fail-closed behavior.

## Current QA results

The orchestrator reported target-005 full default result: **1,084 passed, two failed, zero skipped / 1,086**. The healthy actual-wrapper queue fixture failed at line 178 because `SpeechCancelled` was true after sink entry; its isolated rerun passed in 38 ms. A Codex PowerShell child-launch fixture had an unreadable snapshot; all eight variants then passed isolated. These are QA-owned results, not analyzer-run full-suite checks. Exact causes remain unknown. Controlled reproductions and isolated passes do not turn the full run into a pass.

## Target mapping and integrity

Both resumed before/after comparisons match the complete target-005 output exactly:

`head=47dfca9 baseline=47dfca9 files=30 diff=3c87f683b02f untracked=17/7591a4f4753a`.

Changes from target-004 are limited to these test hashes:

- `ReminderFixtureDriver.cs`: `839f5a6371d1914151a2dd267104edf1bf03bfa4981394586492f80d166b3564`.
- `ReminderPlaybackTests.cs`: `ba35a7bbddf005de1c5245eada47ac19aa455ce4a5f56f0d311b1adcc3c9fdd9`.
- `WindowsUserIdleSourceTests.cs`: `017dd5bc2fb2adf62ffd1083188b6014e6f72cf0595b9bec3317a0d1e7edb54b` (canonical target manifest).

Thus prior verified coverage carries forward explicitly: ANALYSIS-001 receipt continuity uses unchanged `ReminderPolicy.cs` (`fc9a85...`) and StateStore; ANALYSIS-002 current escalation validation uses unchanged StateStore/config/schema; ANALYSIS-003 native time bounds use unchanged `UserIdleSnapshot.cs` (`83ede6...`) and `WindowsUserIdleSource.cs` (`d5dd2c...`); ANALYSIS-005 unknown-field preservation uses unchanged settings form (`838af4...`) and ConfigStore. The same tracked implementation hash and all non-test untracked hashes preserve the prior matched Claude/Codex/mixed policy/audio checks at their recorded scope. Current affected fixtures retain real wrapper/tray/default-budget checks; no provider restriction is claimed.

Only reserved report/evidence and ignored scratch outputs were written. No implementation/test/config/task/ledger/historical report edit or production operation occurred. Own outputs were inspected for sensitive content; full regression/build/format/broad hygiene remain QA/orchestrator responsibilities.
