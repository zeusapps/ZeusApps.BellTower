# Implementation 007 handoff

Status: **partial.** The source correction and focused automated coverage are complete and passing. The native disposable matrix was interrupted when Claude Code stopped it because the machine ran critically low on memory. This was not a probe failure. Per that notice it was not restarted. Five of the eight native cases have recovered evidence; three were not run natively. This round implements [plan 007](implementation-007-plan.md) for canonical ANALYSIS-200 and ANALYSIS-201 (QA-250 and QA-251 are duplicates). These are fixes awaiting independent verification, not closed findings.

Model: developer role, Claude Code delegated subagent, `claude-opus-5-5` (the user-approved stronger override for round 007).

## Changes (`scripts/install-dev.ps1`)

1. **Exception-safe acquisition (ANALYSIS-200).**
   - Thin acquisition seams: `Open-DataLock`, `Open-BlockingDataLock`, `Open-RecordGuard`, `Open-LaunchGate`.
   - `Close-Resources` attempts every disposal. It closes guards in reverse order, then locks in reverse acquisition order, and collects failures.
   - `Complete-ResourceScope` rethrows the primary acquisition or validation error and reports cleanup failures as warnings. If only cleanup failed, it surfaces the first cleanup failure.
   - `Enter-MonitoringCommit` acquires every lock and guard inside one ownership scope. A transfer flag ensures that only a complete validated bundle reaches the caller. Busy, stale, throw and return paths all release first; retry delays and renewal happen with nothing held.
   - Switch's monitoring snapshot now goes through `Save-SwitchMonitoringSnapshot`. Its three blocking locks are acquired inside the same cleanup discipline, keeping the existing 5 s per-lock wait (no timeout change).
   - The monitoring commit's caller also keeps the primary error.
2. **Shared post-gate pointer guard for both directions (ANALYSIS-200).**
   - `Invoke-GuardedPointerStep` runs the existing boundary (renewal and drain) outside the gate.
   - After the potentially blocking launch-gate wait, it opens both read-only guards, enforces the 16 KiB bound, rechecks exact bytes and full tuples, checks freshness (8 min), checks the expected selection hash, then runs the short step under gate and guards.
   - Stale records release the gate and guards, then renew and retry under the existing 30 s bound.
   - Switch writes the exact recorded proposal bytes and verifies the result against `devSelectionSha256`.
   - Rollback's pointer step and its phase commit use the same helper.
3. **Write-ahead pointer intent and one completion path (ANALYSIS-201).**
   - `Get-RollbackRoute` classifies the phase, selection and intent per the plan's table *before* any renewal or takeover. Dispatch then recomputes the route and requires it to be unchanged.
   - Rollback runs one shared completion path, `Complete-RollbackPointer`, both for freshly restored monitoring and for every retry.
   - Under the gate and guards, it publishes `rollbackPointerTransition` in the existing manifest before rewriting the pointer. The intent binds:
     - operation ID and token, normalized paths, phase `Rollback` and the original target;
     - the committed plan and both marker hashes;
     - source (proposal) and destination (original snapshot) selection hashes, plus their parsed version, previous and activation;
     - the snapshot's selection hash.
   - It then atomically replaces the pointer, verifies the original bytes, and in a single manifest save sets `restored-monitoring-snapshot` and removes the intent.
   - Routes:
     - **Intent present, source bytes:** finish only the pointer step.
     - **Intent present, destination bytes:** commit only the phase, under the same guards.
     - **`restored-monitoring-candidate-pointer` without an intent:** proceeds only on the exact dev bytes.
     - **Original bytes in that phase without an intent:** refused.
   - Intent validation also requires the old owner to be proven ended.
   - A validated intent's publication binding is carried forward atomically by the existing record renewal, transition and takeover publications (`Move-PointerIntentPublication`). Its binding therefore never goes stale relative to the committed expectation. **Decision for review:** the plan allowed either keeping the old binding or carrying it forward. Carrying forward closes a crash window: a crash after takeover commits but before the pointer step would otherwise leave an intent that no longer matches the committed expectation.
   - Retry paths after monitoring restoration keep `monitoringMutationStarted` set. This includes the `restoring-monitoring-snapshot` retry. Records are therefore preserved on failure.
   - Every route after monitoring restoration refuses if any tray is running.
   - The full snapshot hash check runs before the gate. Only the two small selection files are read under the gate.
4. **Live candidate activation (ANALYSIS-201).**
   - Switch records `candidateActivation`. This is needed because a later non-recovery Rollback mints its own `coordinationId`, so the old comparison against `coordinationId` was not a valid binding for completed switches.
   - `Assert-OwnedCandidate` requires the live presence's version, data directory and activation to match the expected candidate before choosing either path.
   - In the receipt fallback, the receipt's activation must equal both the expected activation and the live one. The existing exact launcher PID, start time and image checks, tray PID and start time, and ancestry rechecks are unchanged.
   - `Stop-VerifiedTray` re-reads the owner and reruns the full candidate check immediately before `RequestExit`.
5. **Accurate phase reporting.** `Describe-OperationProgress` drives the drain-failure and busy/freshness messages. It reports that the selection is intact, that monitoring restoration has started, that monitoring was restored but the candidate pointer is still active, or that the selection was changed.
6. Disposable-only opt-in barriers (`hold-*` files, `-Disposable` only):
   - `<boundary>-before-gate`, outside the gate;
   - `candidate-before-stop`;
   - `rollback-pointer-intent-saved` and `rollback-pointer-written`, inside the gate, used only for crash tests and never for hook timing;
   - `rollback-pointer-phase-saved`.

There is no new dependency, timeout increase, stable-launcher change or provider-specific policy. Claude Code and Codex hooks share the one unchanged path. `docs/development.md` Recovery gained a short description of same-manifest resume, the intent and the activation recheck. Both skills are unchanged and byte-identical.

## Tests

- `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1` (new) is the extracted-function harness. It defines the installer's functions without running its program and injects faults by replacing only the `Open-*` seams, `Read-Owner`, the controller and the boundary. On the same, still-alive thread it proves that no data lock or launch gate is owned (`ReleaseMutex` probe) and that no guard is open. The test process then acquires every mutex and opens each record exclusively while the harness is still alive.
  - A mutation check confirmed it detects a real leak: with the cleanup `finally` removed, it reported `setup,state` held.
- `InstallDevScriptTests.Recovery.cs` (new; the class is now `partial`):
  - 21 harness scenarios:
    - second or third lock throws, guard open throws, validation fails after a complete bundle, disposal failures with the primary error kept, disposal failure only;
    - busy and stale retries with nothing held at renewal;
    - Switch snapshot lock throw and backup failure;
    - pointer step: stale retry with renewal outside the gate, write/delete/replace denied under guards, drift after the boundary, guard throw, gate and guard disposal failures;
    - candidate: stop-time activation change (no signal) plus a positive control, live activation, receipt activation, data directory, unrecorded activation.
  - Script-level cases (real script, real gate and mutexes, `-NoTray`):
    - post-gate Switch drift (restart bytes; pending unknown field), with the test holding the launch gate;
    - post-gate Rollback drift, then an exact-restore retry that finishes only the pointer;
    - actual intermediate retry after interruption at `rollback-before-selection`;
    - three-point pointer publication crash matrix;
    - 12 negative intent fixtures: missing intent; source, destination and snapshot hash; operation; phase; source activation; destination previous; publication; live owner; unrelated exact-version selection; marker bytes. Each asserts that manifest, selection and both marker bytes are unchanged.
  - Mutation checks: removing the post-gate record check failed all three gate tests, and skipping pointer completion on retry failed the retry tests. The script bytes were restored afterwards (hash verified).

## Checks

| Check | Result |
| --- | --- |
| `dotnet build -c Release --no-restore` | passed |
| Focused tests: `InstallDevScriptTests\|HookMaintenanceTests\|DataLockTests\|TrayInvocationCommandTests\|InstallDevBuildSkillTests` | passed, 85/85 (installer class 63/63) |
| Existing `HookMaintenanceTests` (Claude, Codex and mixed generated hooks under 3 s while slow cleanup is blocked) | passed |
| `dotnet format ... --verify-no-changes --include` the two changed C# test files | passed |
| Skill copies byte-identical | passed |
| Native disposable matrix ([probe](implementation-007-native.ps1), [recovered results](implementation-007-native-partial-results.json)) | **partial**; see below |
| Full Release suite, package build, NativeAOT benchmark, maintainer read-only Preflight | not run (outside this focused round; QA owns final acceptance; a full suite was avoided under memory pressure) |
| Known STA font/render test | not run here; out of scope, status unchanged |

Native results recovered before the memory stop:

- Clean Preflight → Switch → Rollback with a real tray passed twice: the candidate tray's activation was bound, the original tray restarted with the operation activation, and the records were removed. After Rollback, the restarted tray's own automatic cleanup retired 0.1.9001 under the shared retention rule.
- The explicit Retire step failed **because of a probe fixture bug, not the product.** The probe copied 0.1.9002 from an unpacked build folder that contains `bell-tower.pdb`. The shared owned-files rule correctly refused it. The probe now extracts the payload from the package zip. A clean native Retire is therefore **not yet re-proven** this round; QA006 previously recorded one.
- Native restored-monitoring retry and the pointer-intent-saved and pointer-written crash cases: in each, the installer was interrupted by exact identity and a same-manifest retry reached `running-original`. The original was selected, the original tray was restarted, no intent or records remained, and exactly one dev snapshot existed (none saved twice). Per-interruption intermediate state was asserted in the -NoTray tests; the native run's in-memory records were lost with the stopped process.
- **Not run natively:** the pointer-phase-saved crash case, the live-activation negatives at the presence-before-receipt interval (live activation, receipt activation, receipt PID), the activation change before the stop, and the native hook timing during the new pre-gate barriers. The same refusals are covered by the harness and `-NoTray` tests, but natively they are unverified.

## Cleanup

- No disposable process remains. Every run's process scan under its root found none, and a final scan found only the maintainer's three pre-existing processes (PIDs 16508, 2908, 18412), which were never touched.
- All `artifacts/implementation-007-native-*` roots and harness smoke roots were removed. Unit tests clean their own workspaces.
- The maintainer's installation, selection, tray, settings and provider configuration were not touched. No real Switch was run, and no maintainer Preflight was run this round.

## Residual concerns

- Barriers inside the gate are opt-in and disposable-only. Hooks must never be timed against them.
- The 8-minute post-gate freshness branch is effectively unreachable in practice (renewal immediately precedes a gate wait of at most 10 s). It is exercised only through the harness.
- The native rerun is still needed for the three cases above (pointer-phase-saved crash, the activation/receipt negatives and the activation change before the stop), the hook-timing case and the clean explicit Retire, when memory allows.

## Review target

Baseline `b74a12b`; scoped paths as assigned. `head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/0717273118a2`.
