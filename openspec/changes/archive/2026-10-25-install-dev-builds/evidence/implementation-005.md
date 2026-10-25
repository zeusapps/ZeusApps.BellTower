# Implementation handoff: install-dev-builds / 005

## Target and scope

Developer / Codex delegated role / gpt-6-luna, continuing the user-authorized focused fix round for tasks 4.1, 4.3 and their acceptance prerequisites in 6.1/6.3. Corrected ANALYSIS-200/201/202 and QA-200; QA-201/202/203 are overlapping reports. The implementation scope remains scripts, source/tests, docs and the two skill copies.

Baseline and HEAD remain b74a12ba20eb9d3595204b41295b9dab73b7895d. The frozen implementation identity is:

head=b74a12b baseline=b74a12b files=30 diff=a3393fb5519a untracked=13/74f58a81dccb

Tracked diff SHA-256: a3393fb5519ad0493cb228120d7ecb8a164152bec0343e6ad1c8a9f0134f0966.

The unrelated staged restore-claude-badge-identity/reports/findings.md and unrelated untracked changes remain untouched. No commits, staging, branch changes or real installation operations were performed by this developer.

## Changes

- Added final marker refresh and conservative tray-start drains immediately before Switch selection, Rollback monitoring restoration, Rollback pointer restoration, and reservation release. All process scans and waits remain outside launch/data locks.
- Connected complete Windows argv parsing to the installation tray drain. The classifier verifies argv[0] against the live image, accepts quoted/recursive --data-dir forms, distinguishes another explicit data root, and fails closed on ambiguous tray command forms.
- Recovery checks both pending and restart records against the complete operation tuple and the manifest's last expected byte hashes before changing either. Phase targets distinguish Switch and Rollback. The Rollback retry path resumes after the original pointer and monitoring snapshot have already been restored without applying that snapshot twice.
- Treats a proven-ended stale tray presence as stopped when no competing tray-instance object exists; live or ambiguous ownership still refuses.
- Every authorized restart records the launcher PID/start tuple and verifies the tray's version, data root, activation ID and process ancestry in both directions. PowerShell reads C# ValueTuple ancestry PIDs from Item1.
- Made the Cleanup hook test run Claude individually, Codex individually, then a mixed pair while hashing remains blocked. Its finally releases the barrier and joins Cleanup before workspace/event disposal, retaining both primary and teardown errors. A timed-out test-owned hook shell is killed as a process tree, waited to exit, and its output tasks are drained.
- Added disposable-only restart-failure injection used by the native recovery smoke. It refuses immediately before restart launch and does not alter production settings or user data.

## Verification

- dotnet build -c Release --no-restore — passed, zero warnings/errors.
- Focused suite — passed 38/38: HookMaintenanceTests, TrayInvocationCommandTests, and InstallDevScriptTests.
- Scoped dotnet format verify-no-changes — passed after formatting the five changed C# files.
- Real disposable native Switch/Rollback smoke — passed, results saved at [implementation-005-native-restart-results.json](implementation-005-native-restart-results.json). It completed an injected candidate-restart failure followed by same-manifest Rollback recovery; a regular Switch; an injected Rollback restart failure after the original pointer/state had been restored; and idempotent Rollback recovery. Tray receipts matched version, activation and data root in both directions. It also shut down the disposable tray, retained its ended tray.json, and passed read-only Preflight with trayRunning=false. Automatic Cleanup removed the unselected 0.1.9001 payload before the smoke's final Retire step, so this trial did not independently execute native Retire.
- The smoke's initial output-pipe hang was a harness issue: the child tray inherited redirected pipe handles. The final harness redirects installer output to files, preserves the tray during subsequent phases, and completes without waiting for pipe EOF.

## Coverage still required from QA

- The native smoke covers successful restarts, both interrupted restart recovery phases, activation/ancestry checks, and ended stale presence. It does not replace the full disposable Retire lifecycle.
- Focused classifier tests cover quoted/recursive options, equals syntax, Unicode/spaced paths, other data roots, helpers, malformed/unknown commands and argv[0] mismatch. Runtime CIM access-denied, missing-image, PID-reuse and delayed old-order child cases were not exercised in this developer round.
- The installer compares both complete records and their byte hashes before renewal. The focused suite verifies a reconstructed changed recovery tuple is refused and both record byte streams are preserved. It does not independently perturb every field and every record, unknown JSON fields, or byte drift on each record separately.
- Focused hook evidence covers the generated legacy launcher for Claude, Codex and mixed sessions during the blocked Cleanup hash phase. It does not cover all four Switch/Rollback/Cleanup/Retire phases with delayed hooks, injected test assertion/timeout teardown, or the final package NativeAOT benchmark. Those remain for QA's acceptance pass.
- Only the focused 38-test suite was rerun here. The full suite, final package build/benchmark, read-only maintainer Preflight, full disposable lifecycle and final mixed slow-maintenance matrix remain QA work. Prior full-suite result was 754/757 with two individually passing flaky failures and the Cleanup teardown issue addressed here; no new full-suite claim is made.
- The reported work-machine timeout cause remains unverified. The root separately confirmed that existing Claude/Codex hooks used the legacy desktop launcher before the user's authorized migration; native-package creation by itself did not update hook commands.

status: done
role / client / model: developer / Codex CLI delegated role / gpt-6-luna
task / finding IDs: 4.1, 4.3; ANALYSIS-200/201/202; QA-200
changed or report paths: scripts/install-dev.ps1; src/BellTower/IO/WindowsCommandLine.cs; src/BellTower/IO/TrayInvocationCommand.cs; src/BellTower/Updates/UpdateTransaction.cs; tests/BellTower.Tests/IO/TrayInvocationCommandTests.cs; tests/BellTower.Tests/Scripts/InstallDevScriptTests.cs; tests/BellTower.Tests/Updates/HookMaintenanceTests.cs; evidence/implementation-005.md; evidence/implementation-005-native-restart.ps1; evidence/implementation-005-native-restart-results.json
checks: Release build passed; focused tests passed 38/38; scoped format verify passed; disposable native restart/recovery smoke passed with limits above
target identity: head=b74a12b baseline=b74a12b files=30 diff=a3393fb5519a untracked=13/74f58a81dccb
limitations / decisions needed: final full suite, final package/benchmark, full lifecycle including native Retire, delayed maintenance matrix and per-field marker/process-negative matrix are assigned to independent QA
next: independent QA and code analysis on this frozen identity
