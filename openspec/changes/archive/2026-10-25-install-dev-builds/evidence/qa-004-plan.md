# QA 004 acceptance harness plan

Preparation only: none of the harnesses in this round were run. Wait for the orchestrator to assign the stable fix-round target, record that target identity, then run all write-producing actions in a new `artifacts/qa-004-*` tree. The previously tested `0.1.9002` package may be reused only if final source remains identical to the QA 003 package inputs; a source fix requires a new package and Release build before the benchmark.

## Prepared scripts

- [prepare-hooks](qa-004-prepare-hooks.ps1) takes an extracted package/install root and a new scenario path under `artifacts/qa-004-*`. It writes only a synthetic provider home and data directory there, runs `IntegrationSetup` for both clients in that fake home, provisions verified immutable ingress bytes, and records generated launcher-based and native commands plus the ingress digest in `commands.json`.
- [hook-matrix](qa-004-hook-matrix.ps1) starts real Bash/Windows-command-shell processes using those generated commands. It submits only synthetic Stop events, checks neutral output and silent native ingress, enforces a three-second budget, counts new durable captures/inbox records, and records whether a supplied phase barrier remains unreleased. `-Channel Legacy` and `-Channel Native` give separate results; `Both` starts Claude/Codex legacy and native commands together. `-HoldLaunchGate` is for the baseline mechanism and native-under-gate control. It kills only the process trees it created.
- [QA 003 race reproduction](qa-003-owner-race.ps1) remains immutable history. It failed against the pre-fix target. Prepare an equivalent fresh-switch trial after the fix and require no tray presence/owner to survive the phase or be substituted under selection.

The matrix's current gate-holder path intentionally creates the `InstallationGate` for the installation root in the same PowerShell thread that disposes it. Named Mutex thread affinity requires both operations on the owning thread. Run `-Channel Native -HoldLaunchGate` first to verify AOT capture bypasses the gate and records durable envelopes. Run `-Channel Legacy -HoldLaunchGate` separately as a controlled historical failure: require timeout beyond the three-second provider budget, kill the QA-owned shell tree, and label this as mechanism reproduction. It is not evidence of the work machine's cause. During maintenance barriers, the expected acceptance is `-Channel Both` success with the barrier still blocked.

## Disposable lifecycle and phases

Use a fresh installation clone per lifecycle. Start with the archived disposable stable 0.1.8 payload and launcher at `artifacts/dev-cycle/20261024T200826Z/install`; point every data and workspace parameter to the new QA root, never the normal profile. Run Preflight, provision synthetic dual-provider hooks, Switch, verify selected/running state, Rollback, then retire only a generated unselected dev payload. No user tray or provider session is involved. Use `-NoTray` if a readytray trial isn't essential; cover readytray ownership separately with a fake disposable tray or the installed fixture.

For each revised installer barrier named by final `implementation-004.md`:

1. Wait for its `.entered` signal. Confirm its restart marker or reservation is present and record the selected pointer, marker and tray-instance owner before opening the gate.
2. Start the `Both` hook matrix. All eight shell invocations must finish before three seconds while `.release` is absent; verify neutral responses and durable output while maintenance is still blocked.
3. Attempt to start the old selected tray with the actual stable launcher and the disposable data directory. The owning protocol must reject or defer this start; record its process outcome and check that no old-version owner appears after Switch/Rollback commits.
4. For delayed-child drain, use the developer's explicit test seam if delivered. Pause a disposable tray child after it has claimed its instance object but before it publishes Ready. Switch/Rollback must wait for or safely drain only that child before changing selection. Verify delayed readiness cannot publish an old-version owner afterward.
5. Release the barrier, wait for the installer outcome, inspect `current.json`, `updates/restart.json`, marker ownership, tray presence version/activation and operation manifest state. Verify successful paths clear only their marker, and refusal/recovery paths preserve their documented recovery material without leaving a stale block.

Switch and Rollback each get their own fresh operation; repeated phases must not reuse `.entered`/`.release` files. Retire/Cleanup share `UpdateTransaction.PayloadRemovalTestCheckpoint`. For an evidence-only C# harness, reflect the internal static checkpoint from `BellTower.Updates.UpdateTransaction`, install an `Action<string,string>` callback filtered to the exact fixture root/version, and block on `ManualResetEventSlim` while a task invokes `Cleanup` or `Retire`. From the main script thread, run `qa-004-hook-matrix.ps1` using the disposable launcher's generated commands; release the checkpoint in `finally`. Assert the lease-retained target remains present while verification is blocked, then that Retire removes only a marked, unretained dev payload after release. Restore the callback to null. Use separate process or PowerShell runspace only when the `InstallationGate` is acquired and released synchronously on one owner thread.

## Packaged NativeAOT benchmark

After the fix passes lifecycle barriers, run the existing benchmark against the final extracted package and a new output root:

```powershell
dotnet run --project tools/BellTower.Ingress.Bench -c Release --no-build -- <extracted-package-root> artifacts/qa-004-benchmark
```

It creates `.claude` and `.codex` settings only under the new fixture home, provisions verified ingress, runs generated Bash and `cmd.exe` commands, checks neutral responses and durable capture totals, records first/median/p95/p99/max per provider, rejects native median at or above 300 ms or any sample at or above three seconds, drains the captures and checks replay. Preserve `results.json`. Separately run Native-only mixed hooks during the explicit launcher-gate hold, while the maintenance barrier run covers both channels. Generated-shell/native evidence stays distinct from any authenticated field observation.

## Outstanding setup at stable handoff

- Confirm final marker filename/format, the disposable tray start gate, and any delayed-child barrier API from the stable handoff. Do not guess these contracts from changing source.
- Map each exact marker file/phase name and expected owner/reservation state to the final test entry points.
- Record all commands and per-phase outputs in the assigned QA 004 evidence/report. Report a missing seam or fixture as not performed and route any source/test change through findings.
