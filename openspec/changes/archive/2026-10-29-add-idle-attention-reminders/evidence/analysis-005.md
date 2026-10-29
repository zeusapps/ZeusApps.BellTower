# Analysis 005 evidence: resumed target-004 fixture review

Saved 2026-10-29 after the user's continuation request. This completes the review interrupted on 2026-10-28 by an account usage limit before report 004 could be written. Its reservation remains consumed; no report/evidence 004 was saved. Existing adequate probes were not repeated after resumption.

## Provenance and unchanged target

Read the resumed ledger marker/reservations, [QA-004](../reports/qa-004.md), [implementation-004](implementation-004.md), and [target-004](review-target-004.txt). The ignored executable probe is preserved at `artifacts/idle-analysis-004/Program.cs` and its project at `Probe.csproj`. Its recorded execution/output below comes from 2026-10-28, after the orchestrator confirmed all QA executions were complete. On resumption both files remain present and the implementation still exactly matches target-004.

Full before/after identity: `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/7001ad675d0d`. Baseline/HEAD are `47dfca9fd46b309017d4d785a1f91fd249d42041`. Comparison used the complete output of:

```powershell
./scripts/review-identity.ps1 -Baseline 47dfca9 -Paths src/BellTower tests/BellTower.Tests README.md docs/compatibility.md docs/attention-reminders.md
```

The tracked diff SHA-256 remains `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296` from target-003. Comparing snapshots 003 and 004 changes only these untracked test hashes and the aggregate summary:

- `ReminderPlaybackTests.cs`: `a6578e6adc88f339de0becb70a09adae05e192602090a9954014c5865966eb2b`.
- `WindowsUserIdleSourceTests.cs`: `d5d2eeffdf68cca83863297e48b15bc4fff732692ce43d71ef278ccd49e0452f`.

All product/source/docs and other test hashes are unchanged.

## Current official documentation consulted before interruption

Applied the previously read find-docs skill. Outside the sandbox, successfully ran `npx ctx7@latest library ".NET" ...`, selected the official `/dotnet/docs` repository, then ran `npx ctx7@latest docs /dotnet/docs ...` for Task.Run queueing, blocked workers, and asynchronous coordination. Two commands were used; no quota or documentation lookup error occurred.

Primary sources returned:

- [.NET async scenarios](https://github.com/dotnet/docs/blob/main/docs/csharp/asynchronous-programming/async-scenarios.md): ThreadPool offloading includes queueing overhead.
- [Synchronous wrappers for asynchronous methods](https://github.com/dotnet/docs/blob/main/docs/standard/asynchronous-programming-patterns/synchronous-wrappers-for-asynchronous-methods.md): blocking pool workers can delay work needed for completion; eventual pool growth does not prevent every starvation delay.
- [Debug ThreadPool starvation](https://github.com/dotnet/docs/blob/main/docs/core/diagnostics/debug-threadpool-starvation.md): asynchronous waiting frees workers for other queued work.

Inference: an observer that executes quickly once started does not guarantee startup within a synchronous fixture gate's timeout. The concrete fixture mechanism was also reproduced independently below; neither documentation nor that reproduction establishes the cause of QA's particular full-run failures.

## Recorded bounded probes

Command: `dotnet run --project artifacts/idle-analysis-004/Probe.csproj -c Release`, outside the sandbox after QA finished. The scratch project references existing application/protocol assemblies without building product projects. It uses injected low-level observations, synthetic sessions, the actual source wrapper/tray validity predicate without a tray constructor, and silent audio sinks. No physical input/audio, native session query, UI, microphone, real provider, installation, production data or network send was used.

Recorded final output:

```text
PASS healthy observer precondition claude
PASS healthy real-wrapper queue claude
PASS current input observed after healthy prewarm claude
PASS healthy observer precondition codex
PASS healthy real-wrapper queue codex
PASS current input observed after healthy prewarm codex
PASS default-budget fixture seeded positive
PASS default-budget probe running before timeout check
PASS public production budget rejects old positive cache
PASS production timeout remains bounded with scheduler tolerance
Observed public default timeout: 85.2ms
PASS actual tray with public default budget withholds reminder
PASS synchronized default timeout stays single-flight
PASS setup-pressure fixture seeded positive
PASS controlled process-only scheduling limit established
PASS blocking fixture setup can prevent its own observer from starting
PASS async fixture with dedicated driver avoids startup starvation and verifies eight concurrent consumers
PASS dedicated driver cleaned up
```

An earlier bounded run observed 88.8 ms for the nominal default timeout. These observations include scheduling/timer overhead and are not a benchmark or hard wall-clock guarantee. A 500 ms generous probe assertion only detects unbounded waiting; product code retains the monotonic 80 ms budget.

Healthy probes use the actual wrapper with the explicit five-second test budget, assert a successful prewarm, and deliver through the actual predicate/queue for both providers. New input after prewarm causes a new observation and invalidation. Separate probes use a default-budget source with an already-entered held poll: public fresh reads return unknown, the real tray predicate rejects the prior positive cache, and concurrent reads retain one in-flight observation.

## Remaining setup mechanism and concrete alternative

At `ReminderPlaybackTests.cs:230`, the current fixture synchronously waits two seconds for a ThreadPool observer to start before reaching the default-budget rejection assertion. The source's probe is queued through Task.Run. In a controlled scratch process with its own pool limited to one worker, seed the positive sample, execute the fixture driver on the sole worker, queue the next observer, and call the same entered wait. The wait returns false with the read counter still at the seed value: the observer has not started, and the later 80 ms assertion has not run. Original scratch-process pool settings were restored in `finally`. This constraint is diagnostic only, not a proposed global configuration change.

Under the same constraint, an asynchronous fixture awaits completion from a dedicated test-owned coordinator. The coordinator seeds and starts the actual source, waits for its entered signal, invokes the actual tray predicate with the public default budget, and releases the held source before completing. Eight dedicated test-owned consumer threads independently call the same source; all return unknown and the low-level counter stays at seed plus one held observation. The coordinator joins consumers and releases held work before signaling completion. This passed without changing product code or default timeout.

The current concurrency fixture at `WindowsUserIdleSourceTests.cs:145` instead uses eight Task.Run drivers, each synchronously waiting in fresh reads on the same shared pool while a probe is held. That can consume workers needed by concurrently running tests. The dedicated-driver alternative isolates the intended concurrency contract. No measurement attributes any actual QA failure to this pattern.

Recommended required correction: make fixture setup nonblocking for the xUnit worker, use a dedicated test-owned coordinator and test-owned concurrent callers, and await completion asynchronously. Release/join owned work in `finally`. Retain the real wrapper and tray predicate, public production 80 ms timeout assertion, rejection of old positive cache, and single-flight checks. Do not loosen generic timeouts, add retries, tune global ThreadPool settings, or change product policy. The recommendation is proved in scratch only; this reviewer made no implementation/test edit.

## QA evidence remains independent

As recorded by [QA-004](../reports/qa-004.md):

| Run | Result | Distinguishing facts |
| --- | --- | --- |
| Target-004 default full 1 | 1,084 passed, two failed, zero skipped / 1,086 | Corrected healthy reminder passed. Unchanged audio-doctor diagnostic and hook-maintenance launch checks failed, then passed once isolated. |
| Unchanged default full 2 | 1,083 passed, three failed, zero skipped / 1,086 | Strip font refit, CLI hook timeout and reminder timeout fixture's line-230 setup failed. All passed once isolated; reminder isolate took 99 ms. Healthy reminder passed again. |

Exact causes of these failures remain unknown. Isolated passes and controlled mechanisms do not convert either failed full run into a pass. ANALYSIS-004 full acceptance and REGRESSION-001 remain open.

## Carry-forward and ownership

Because product/source/docs are byte-identical to target-003, the [analysis-003 explicit coverage mapping](analysis-003.md#target-continuity-and-carry-forward) for receipt continuity, current escalation validation, native tick/freshness bounds, unknown-field preservation and matched/mixed provider behavior remains applicable. Current probes also support healthy provider paths, post-prewarm input invalidation, default timeout and single-flight. No provider restriction is claimed.

Only reserved report/evidence 005 were saved after resumption. No broad run, source/test/config/task/ledger edit, historical-report change, commit or production operation was performed. Own outputs were inspected for sensitive content; comprehensive hygiene/build/format/full regression remain QA/orchestrator responsibilities.
