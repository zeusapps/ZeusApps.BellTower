# Analysis 003 evidence: final playback freshness recheck

Date: 2026-10-28. Frozen [target 003](review-target-003.txt), baseline/head `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Execution and separation from QA

Inspected source while QA ran its default suite. Did not build or execute scratch probes until the orchestrator relayed that the full suite and isolated rerun had finished. Then ran `dotnet run --project artifacts/idle-analysis-003/Probe.csproj -c Release` outside the sandbox. The ignored scratch project references the existing application/protocol Release assemblies and does not build product projects.

The probe uses the actual `WindowsUserIdleSource`, its injected low-level observer, manual time/synchronization, and the actual tray validity predicate without running the tray constructor. Audio checks use silent sinks. No physical input, native session query, microphone, UI, authenticated provider, installation, network send or production data is used.

Two bounded runs passed. Final run output:

```text
PASS fresh positive idle available
PASS preinput cache cannot authorize fresh read at100ms
PASS older poll started
PASS prerequest poll requires second observation
PASS timeout fixture has prior positive cache
PASS default-budget timeout withholds positive cache
PASS blocked refresh started
PASS timeout call remains bounded with scheduler tolerance
PASS concurrent timeout requests keep one worker
Measured default timeout call: 82.8ms
PASS fresh path rejects future observation
PASS fresh path settles failure as unknown
PASS future postwrap tick remains rejected
PASS short wrap remains supported
PASS healthy queue completed claude
PASS healthy reminder audible policy claude
PASS healthy queue completed codex
PASS healthy reminder audible policy codex
PASS mixed queue barrier started
PASS mixed batch completed
PASS recent input cancels mixed speech while ordinary tone survives
PASS isolated worker-availability fixture configured
PASS sole worker occupied
PASS queued reminder validity completes with worker unavailable
PASS fast observer still withheld when worker cannot start within80ms
```

The first run's default timeout call measured 79.7 ms. These are two observations of the nominal 80 ms budget with scheduling/timer overhead, not a benchmark or hard wall-clock guarantee. The probe uses a 500 ms generous assertion bound solely to catch unbounded waiting; the product's remaining wait is computed from a monotonic 80 ms budget.

## Assigned finding reproduction

The original ANALYSIS-004 reproduction is adapted to call `GetFreshSnapshot`, which the actual tray now calls. An initial 20-minute idle observation is followed by a zero-idle low-level result at manual time T+100 ms. The fresh call performs a second observation and returns zero, instead of granting eligibility from the cached sample. A background poll already in flight before a fresh request is released with an old positive result; a distinct second observation is required, and its recent-input result is returned.

A blocked probe with a previously positive cache returns unknown within the fixed budget. Six concurrent requests time out without creating another in-flight observer. A failed observer and future timestamp yield unknown. The post-wrap future tick still yields null; a legitimate short wrap remains 21 ms.

For both Claude and Codex, a real source wrapper with a successful current-idle observer passes through `AudioQueue` to the silent sink, preserving phrase, tone permission and speech permission. In a synchronized mixed batch, the low-level source switches to recent input during the tone. Reminder speech is canceled while the ordinary alert preserves the tone. This uses actual wrapper refresh and tray validity, rather than only a mutable fake `IUserIdleSource`.

## Worker-availability assessment for REGRESSION-001

The orchestrator relayed QA's target-003 full result: 1,085 passed, one failed, zero skipped. The sole failure was `ActualWindowsSourceAllowsHealthyReminderThroughQueue` at its three-second sink wait; its isolated rerun passed. Those remain QA observations, not analyzer-run full-suite results.

The new method schedules its low-level observer with `Task.Run` and includes time waiting for that task to start within the fresh-read budget. The successful-playback fixture supplies a fast observer, but does not guarantee a worker can start it within 80 ms. Once the queue's first validity check returns false after timeout, the item is dropped; increasing the later sink wait does not retry it.

To assess that assumption independently, constrained only the scratch process's managed thread pool to one worker and occupied that worker behind a bounded event. Enqueued a reminder whose low-level observer would immediately return current positive idle. The audio queue has its own thread, so its validity check completed normally but returned false after the budget, the observer read count remained zero, and the sink was not entered. Released the event and restored the scratch process's original thread-pool settings in `finally`.

This demonstrates that a fast observer alone does not establish the successful-playback fixture's scheduling precondition. It also demonstrates the intended conservative product result when a fresh observation cannot run. It does **not** identify the cause of QA's particular full-suite failure: that run did not record observer start/completion timing. A deterministic positive fixture would need controlled observer availability or an appropriate test seam, while a separate default-budget fixture continues to prove timeout/unknown behavior. The default safety budget must not be weakened merely to satisfy an uncontrolled scheduling assertion. Any further correction is an orchestrator/user decision after the two allowed rounds.

QA-003 retains full ANALYSIS-004 acceptance and REGRESSION-001 as open. These independent bounded probes establish correction of the stale-cache safety defect, not a clean full-suite result or final acceptance of the flaky healthy-wrapper scenario.

## Target continuity and carry-forward

Full before/after identity exactly matches [target 003](review-target-003.txt): `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/cbe46cdb0a67`.

Correction 003 changes only the six paths listed in [implementation-003](implementation-003.md): reminder docs, idle-source interface/implementation, tray fresh-read call, and the two idle/playback test files. The following target-002 evidence therefore carries forward with explicit scope:

- ANALYSIS-001: retained-episode policy and `StateStore` transaction are unchanged; `ReminderPolicy.cs` retains hash `fc9a85bcc27b16b14ace8e4817c108b1e48ae682b822b585dd041fa32bd14698`. Both-provider capped serialized restart evidence in analysis-002 remains applicable.
- ANALYSIS-002: `StateStore.Escalations`/current-actionable helper, configuration reader, receipt/candidate types and schema fields are unchanged. Source reinspection confirms the same current-projection/config gate. Candidate API, confirmation/disable/Mute probes from analysis-002 remain applicable.
- ANALYSIS-003: `UserIdleSnapshot.cs` retains hash `83ede6e05ce9f118fb9653437f07f3aff2b06ff9c52afaaa624ac8a6d344183d`. The elapsed half-uint guard is unchanged inside the refactored source. Affected future/failed-read and tick-wrap cases were also rerun independently in this round.
- ANALYSIS-005: `ReminderSettingsForm.cs` retains hash `838af4c6017047a9d903c686ba6e459caf7b5528f55d62dd9599aad5971c6c03`; `ConfigStore` and the actual Save mapping are outside correction 003. Executed preservation checks from analysis-002 remain applicable.

Only this reserved evidence/report and ignored scratch artifacts were written by the analyzer. Prior reports are unchanged. Full regression/build/format and broad hygiene remain QA/orchestrator responsibilities.
