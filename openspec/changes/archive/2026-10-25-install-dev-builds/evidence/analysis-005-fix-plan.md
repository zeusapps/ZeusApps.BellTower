# Focused fix plan after Analysis/QA 005

Read-only follow-up for **only the four remaining gaps under ANALYSIS-200/201**. This is a recommendation, not implementation or an executed acceptance report. Source remains `head=b74a12b baseline=b74a12b files=30 diff=a3393fb5519a untracked=13/74f58a81dccb`; [Analysis 005](../reports/analysis-005.md) is immutable. QA independently reproduced the normal-Rollback lock-wait drift and nested-recovery failures, as communicated by the orchestrator. No source or report was edited for this plan.

## 1. Normal Rollback: protect the actual first state write, including lock waiting

Current `DataLock(paths, area)` (`src/BellTower/IO/DataLock.cs`) always waits up to five seconds. Taking setup, then state, then presentation therefore permits waiting while an earlier data lock is held. Adding `Assert-OperationBoundary` before those constructors **does not fix** drift during their wait. The accepted final check must occur after successful acquisition and before monitoring changes.

Use the existing three named locks and current lock order. Add only a narrow nonblocking acquisition path to `DataLock` (for example `TryAcquire(paths, area)` returning no lock when busy), preserving the existing constructor's five-second behavior for all existing callers. `WaitOne(0)` performs the try; an abandoned mutex is acquired, as existing code already handles. Dispose unacquired handles, and release only owned mutexes on the acquiring thread. No asynchronous/background lock holder or general lock framework is needed.

Recommended sequence for normal Rollback:

1. Validate prior coordinator/owner, reserve the existing tray-instance object, and stop only the verified tray, retaining update serialization.
2. Run the full `Assert-OperationBoundary` renewal/drain **outside** all data/launch locks.
3. Try setup, state and presentation locks with zero timeout. If any is busy, release every acquired lock in reverse order. Only then sleep/retry with a bounded overall busy window, rerunning the outside boundary before the next acquisition attempt. Never enumerate processes, drain, renew records, sleep or perform another blocking lock acquisition while any data/launch lock is held.
4. Once all three are acquired, perform a final short **read-only** exact-record/freshness validation. Open both marker files for read with `FileShare.Read` only (no Write/Delete sharing), fail closed on sharing/identity/drift errors, cap metadata reads at the existing 16 KiB limit, and compare both byte hashes and full tuples against the private manifest expectation. A too-old record requires releasing guards and all locks before returning to the outside renewal/drain; do not renew under the locks. Do not call `ProcessProbe`/CIM here.
5. Hold those two short read guards through the monitoring commit and its durable manifest phase. They allow ordinary marker readers but prevent write/replace/delete between the last check and the state writes. Do not overwrite the guarded markers yourself. Release guards and data locks before the slow barrier, process work, pointer boundary or marker renewal.

**TOCTOU assessment:** checking only before acquisition misses the QA-reproduced wait interval. Checking after acquisition without retaining marker read guards still leaves a marker-check/state-write window. Validating under guards closes that file-content window using existing Windows sharing semantics. It does not claim an OS-enforced boundary against administrative tampering; the operation still fails closed on inability to acquire/validate both guards. The tray reservation and fresh pre-acquisition drain cover startup ownership while the short lock bundle is acquired; the final step does not repeat slow process scans under locks.

Use the same short final-check pattern after any potentially blocking launch-gate acquisition before a pointer write, or acquire that gate nonblocking and retry outside it. This prevents moving the same wait/check defect to the pointer boundary; keep payload verification and process work outside launch.

**Persist the actual phase:** before the first monitoring-file replacement, durably record a restore-started state/intent. After the required monitoring files are replaced and verified, durably record a phase that explicitly means **monitoring restored, candidate pointer still selected**, before entering `rollback-before-selection`. After original-pointer restoration, record the existing original-pointer/restored phase. Retry dispatch must recognize the intermediate phase: retain the original snapshot and saved dev snapshot, avoid a second dev snapshot/monitoring restore when restoration already completed, and finish only the remaining pointer/restart work. A crash during the restore-started interval must be distinguishable from no mutation and can complete restoration from the already validated original snapshot. Do not label a partial restore as complete.

Track monitoring mutation separately from `$selectionMutated` in failure cleanup. Once monitoring restoration starts, keep the matching recovery records even if the pointer is unchanged. Before-state refusals may remove only demonstrably unchanged own records; drifted records must remain. Replace the drain's unconditional “monitoring state left intact” claim with phase-accurate reporting. No new lifecycle framework is required: these are bounded states in the existing operation manifest.

Decisive regressions:

- Hold each of setup/state/presentation in turn, begin normal Rollback, change one owned marker while the installer is contending, then release. It must refuse with monitoring **and** pointer hashes unchanged, preserving drifted marker bytes. Include a content/unknown-field-only change and an expired/freshness case.
- At a disposable pause after lock acquisition but before first state write, marker tampering must either be rejected by the read guards or detected before mutation; no state write may precede successful validation.
- After monitoring restoration but before pointer restoration, inject marker drift or an undrained child. Candidate pointer must remain; the manifest must truthfully record restored monitoring. Resume from that phase without replacing the saved dev snapshot or restoring monitoring a second time.
- Verify contention retries release all partial locks, and hooks/data readers remain able to run while the installer is waiting outside its commit. Preserve the native/legacy three-second gates and no-long-launch-lock policy.

## 2. Relative or drive-relative data roots: refuse unresolved process context

`TrayInvocationCommand` must not pass a non-fully-qualified `--data-dir` through installer-context `Path.GetFullPath` and declare `OtherDataDirectory`. The target tray's working-directory/per-drive context is not available to this helper.

Smallest correction: require a fully qualified data-root argument before comparing it. Throw the existing sanitized classification error for a relative, drive-relative, or current-drive-root-relative value, unless exact target context has separately been proven. The existing script catch then refuses the operation without adopting or killing that process. Fully qualified paths containing `.`/`..` can still normalize normally. Keep the current native Unicode argv parser and supported rooted quoted/recursive/equals forms; normal known event/session helpers remain excluded.

Tests: `../data`, `D:data`, and `\data` with installer and child working directories deliberately different must be unknown/refused, never a harmless “other” result. Keep positive fully qualified own/other roots, spaces/apostrophes/Unicode, trailing separators and helper-command controls. Run an old-order delayed-child fixture with a relative root to show it cannot evade the drain. Do not attempt to infer the target cwd from installation layout.

## 3. Recovery phase and both records must transition together

Validate the interrupted operation against its **old** recorded phase/target and full byte expectations first. Then define the current Rollback phase, original target, contract binding, current coordinator identity and creation time as the **new** expected transition. Do not preserve `coordinationPhase='Switch'` while publishing original-target Rollback plans.

The installer already writes `coordinationTransition` containing old/new plans and hashes before renewing two files. Extend that existing intent with old/new phase and phase-target fields; do not add another journal. Keep the old committed expectation authoritative until the complete new pair is verified, then atomically persist the new phase/target/plan/hashes together and clear the intent. A crash between files cannot be solved by writing the manifest phase first or last alone.

On recovery with an outstanding intent, validate both actual records against the exact private intent and permitted publication order (old/old, new-pending/old-restart, new/new for the current writer order), including complete tuples/hashes and proven-ended relevant coordinator identities. Complete or reconcile only that fully bound own transition, then publish the current takeover. Missing partners, unknown byte/semantic changes, live/unverified owner or an unrecorded combination still refuse and preserve both files. Do not broadly accept either version as a valid target.

Decisive native regression: fail candidate restart after Switch; invoke its recovery Rollback while failing original restart; let that coordinator end; retry the same manifest. It must select/restart original and remove only exact owned records, without a second monitoring restore. Retain fresh-Switch failure and fresh-Rollback failure cases already passed. Add interruption probes before first record replacement, between replacements, and after pair verification/before manifest commit; inject foreign field/byte drift in each to prove it is never reconstructed as an owned transition.

## 4. Interrupted candidate receipt: use the actual persisted launcher

`Start-SelectedTray` persists `restartLauncher`; `Assert-OwnedCandidate` must not require the unwritten `candidateLauncher`. Reuse the persisted receipt only when it explicitly names the expected candidate version and the interrupted operation activation. Require exact live launcher PID/start, candidate tray PID/start, installation image, explicit data root, activation and live ancestry. Recheck launcher and tray identity around the ancestry snapshot. The full coordinator/recovery-record validation still precedes takeover. Existing `candidateTray` receipts remain usable under their exact owner checks; a foreign or unverifiable tray is never signalled or adopted.

Add a disposable-only barrier after the candidate publishes valid presence but before `candidateTray` is persisted. End only that test-owned installer process, leaving its operation-owned tray and recorded stable launcher alive. Same-manifest Rollback must verify that lineage, stop only that exact disposable tray, and recover. Negative cases: changed receipt version/activation/PID/start/data root, reused or ended launcher, and a same-version foreign tray without that ancestry all refuse with the foreign process untouched. Do not use pre-launch failure injection as a substitute for this interval.

## Validation and bounds

These are corrections within the existing agreed behavior, with no required product decision or provider exception. The orchestrator owns authorization for another source round. This plan itself authorizes no implementation. Keep unrelated work, immutable reports, real settings and user processes unchanged. All new probes use disposable data/installations and captured exact process identities. QA remains responsible for final full/native acceptance on the next stable handoff; successful source planning is not a pass.

Primary documentation checked through Context7: [.NET mutex ownership/thread affinity](https://github.com/dotnet/docs/blob/main/docs/standard/threading/mutexes.md), [FileShare semantics](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0), and [.NET Windows path qualification](https://github.com/dotnet/docs/blob/main/docs/standard/io/file-path-formats.md). Existing `DataLock` and `InstallationGate` source establish their current bounded waits and abandonment handling. No build, test, native operation or performance measurement was run for this plan.

Reviewer: code-analyzer / Codex CLI 0.164.0 / gpt-6.1-sol, role default.
