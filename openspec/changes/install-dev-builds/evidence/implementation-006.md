# Implementation 006 handoff

Status: source changes are ready for independent QA and code analysis. This round fixes only ANALYSIS-200/201 and the related QA-250/251 observations, plus the unresolved-root parser behavior and DataLock coverage requested in the handoff.

## Changes

- `DataLock.TryAcquire` performs a zero-timeout acquisition and disposes an unowned handle when busy. Existing `DataLock` construction still waits up to five seconds. Normal Rollback now retries the setup/state/presentation bundle without waiting while holding a partial bundle; every retry releases in reverse order, then repeats record renewal and tray-start draining outside data locks.
- Before the first monitoring-state write, Rollback retains read-only sharing guards for both exact owned records, bounds record sizes to 16 KiB, and revalidates their byte hashes and full typed tuples. Record freshness is renewed only outside the lock bundle. The launch-gate selection path validates the pair after the gate opens and holds the same read guards through pointer replacement.
- Rollback persists `restoring-monitoring-snapshot` before changing monitoring state and `restored-monitoring-candidate-pointer` after verifying the restored files. Retry from the intermediate phase skips the saved development snapshot and repeats only unfinished work. Failure cleanup keeps records once monitoring restoration has started. The busy-drain message no longer claims monitoring is unchanged after a mutation.
- Recovery record replacement now persists old/new phase and target alongside old/new plans and byte hashes. Interrupted recovery accepts only old/old, new-pending/old-restart, or new/new with the exact recorded plan bytes and ended owners; it completes only that transition and commits phase/target with the pair. Foreign record changes are rejected and preserved.
- An interrupted candidate start uses the actual persisted `restartLauncher` receipt. Candidate version, activation, launcher PID/start/image, tray PID/start, data directory, and live ancestry are checked with identity rechecks. A disposable-only opt-in barrier exercises the interval after candidate presence appears but before `candidateTray` is saved.
- `TrayInvocationCommand` rejects relative, drive-relative and current-drive-root-relative data roots instead of resolving them against installer context. Fully qualified roots retain normal path normalization.

## Checks completed

| Check | Result |
| --- | --- |
| `dotnet format ZeusApps.BellTower.slnx --no-restore --include` the four changed C# files | passed |
| `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~TrayInvocationCommandTests|FullyQualifiedName~DataLockTests|FullyQualifiedName~InstallDevScriptTests"` | passed, 41/41 (includes final Release build) |
| Disposable native lock-wait drift probe | passed: changed restart-marker bytes were refused before monitoring restoration; selection and `state.json.bak` hashes were unchanged; both markers remained and the manifest stayed `running-candidate`. See [measured results](implementation-006-lockwait-drift-results.json) and [probe](implementation-006-rollback-drift.ps1). |
| Disposable native nested recovery | passed: failed candidate restart, failed original restart during recovery, then same-manifest retry. The failed recovery saved phase `Rollback`, target `0.1.9003`, state `restored-monitoring-snapshot`; retry restarted original and removed both records. See [measured results](implementation-006-nested-recovery-results.json) and [probe](implementation-006-nested-recovery.ps1). |
| Disposable native candidate receipt interruption | passed: installer was ended at the presence-before-receipt barrier; `candidateTray` was absent while `restartLauncher` matched candidate version/activation and live lineage; same-manifest Rollback restored original. See [measured results](implementation-006-candidate-receipt-results.json) and [probe](implementation-006-candidate-receipt.ps1). |
| Disposable native transition interruption matrix | passed, six cases: exact old/old, new-pending/old-restart, and new/new recovery; byte drift, unknown-field drift, and tuple/byte drift at those same stages were each rejected and preserved. See [measured results](implementation-006-transition-results.json) and [probe](implementation-006-transition-recovery.ps1). |
| Scoped `dotnet format ... --verify-no-changes` | passed |

All installer/native probes used distinct disposable installations and data directories. No live tray, user settings, provider configuration, or real Switch operation was touched. Earlier QA-005 output established the live hook migration separately; it is outside this source handoff.

## Remaining acceptance

The following are intentionally left to QA006 or remain unverified; this handoff does not claim they passed:

- Full Release suite, fresh package build, package identity/benchmark and full Claude/Codex/mixed hook matrix.
- A direct mutation attempt while the short record guards are held, and injected teardown failures while releasing each partial lock/guard.
- Relative-root process-drain runtime fixture; parser unit tests cover `../data`, `D:data`, and `\data` plus existing fully qualified positive cases.
- Negative persisted-launcher receipt matrix (wrong version, activation, PID/start, root, image, dead/reused launcher, and same-version foreign tray); only the decisive valid interrupted receipt was run natively here.
- Full process-classification matrix for inaccessible/unknown child images, PID reuse, expiry/freshness, and all marker fields. Existing byte/semantic/unknown-field transition drift probes cover only the listed recovery states.
- Authenticated work-machine timeout causation remains unverified. Successful disposable native checks demonstrate the corrected paths locally, not the cause of the released 0.1.8 symptoms.

## Review target

- Baseline: `b74a12ba20eb9d3595204b41295b9dab73b7895d`.
- Review paths: `src`, `tests`, `scripts`, `docs/development.md`, `docs/releases.md`, `docs/installation.md`, `.claude/skills/install-dev-build`, `.agents/skills/install-dev-build`, and this round's evidence files.
- Stable review identity: `head=b74a12b baseline=b74a12b files=32 diff=fb2c83821602 untracked=14/62017675a458`.
- Source is frozen for independent QA006 and analysis006. No OpenSpec planning, task ledger, or reviewer report files were edited.
