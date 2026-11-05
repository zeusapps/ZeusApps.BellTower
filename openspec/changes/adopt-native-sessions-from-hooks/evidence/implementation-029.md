# Implementation 029: fix cycle 1 for DEV-028 (task 2.5)

Developer assignment DEV-029 on top of `93e3534` with the DEV-028 work. Fixes await independent verification; nothing here closes a finding or ticks a task. The 5.2 reason code is deferred and not implemented.

## ANALYSIS-1502 (blocker): owned-runtime Codex sessions bound again

Before 2.2 (`2f1de7f`): a hook inheriting `BELLTOWER_DEDICATED=1` was dedicated, so a managed `Start-Codex` backend (parent: the `bell-tower codex-runtime-host` manager) kept the managed attachment ID, its own process and a host shell, and `CodexNameService` and `CodexPermissionPump` found the runtime. After 2.2 the backend was `shared-backend` (after 2.5, `unknown`), so it ended in the shared shape: no process, no host, `SharedId`; both services reported no verified runtime.

Reproduced first (failing test, real stand-ins: manager image `bell-tower.exe`, backend `codex.exe app-server` child, a written `CodexRuntime` record, hook through `event` and `bell-tower-ingress.exe`): only the two positive rows failed (`attachments.Get(managedId)` was null); all negative and Claude rows already passed.

Restored in `NativeAdoption.Reconcile` (provisional path). `AttachmentStore.Adopt` reads the live `CodexRuntime` record for the captured ID (only for a provisional capture) and passes `NativeAdoption.OwnedRuntime(attachment, manager, shell)`; Core stays pure. Accepted only when the same agent as any previous record, the record is for this attachment, its manager equals the hook's captured direct parent (PID and creation time), and manager and shell are live. The host shell comes from the record (`ShellSource` `launch-record`), not `BELLTOWER_SHELL_PID`. `CrossCheck` does not accept any launcher by image name; the backend stays `unknown`.

Tests: `NativeOwnedRuntimeProcessTests` (real processes, 15 rows): valid record gives the managed ID, the backend as process, `Host.Shell == record.Shell` (differs from the inherited shell variable), lifecycle `bell-tower`, `CodexNameService` writes the name; and the same `runtimes.Get` plus `Host.Shell == runtime.Shell` lookup the permission pump uses (pump itself not run). Negative (shared, mode unknown, name path unavailable, nothing claimed): no record, wrong manager PID, wrong creation time, dead manager (backend outlives it), dead shell, record for another attachment, `node.exe` and `powershell.exe` between manager and backend. Independently started backend under a Codex client with a live record stays `shared-backend`. Claude managed launch with and without a record stays direct under the managed ID (unchanged). Positive and Claude rows run both entry points; negatives alternate. `OwnedRuntimeReconcileTests` (10 policy rows) pin each condition in Core.

## QA-2300: Neighbors guards

`ProcessTableTests` (7 tests, real stand-ins): parent/child found; parent younger than the agent rejected; child older than the agent rejected; image filter; children not read when not requested; 8 children complete, 9 incomplete with 8 listed in pid order.

## ANALYSIS-1500: Core decides the needed relations

`NativeAdoption.Relation` flags and `Needs(mode, mayShareBackend)`; `CrossCheck(mode, mayShare, Func<Relation, ProcessRelations?>)` calls the delegate at most once and only when needed. Hosts has no gate left (only the agent-present check) and passes `AgentModeClassifier.MayShareBackend(provider)`; a shared backend reads the parent only, a Claude hook reads nothing. R5 asymmetry kept. Tests: `Needs` table for both providers and delegate-request rows in `ModeCrossCheckTests`.

## ANALYSIS-1501: one process table per hook

New `IO/ProcessTable.cs` (linked into the NativeAOT ingress): one Toolhelp snapshot answers `Ancestors` and `Neighbors`; children are filtered before sorting. `Resolve` takes the table (overload keeps the old signature); the ingress takes one table and uses it for resolve and the envelope ancestors; `Capture` reuses it for the shell's window ancestry. `ProcessProbe.Ancestors(pid)` remains for `VsCodeWindowOwnership` and `PrepareSessionCommand`, unchanged. Snapshots per Codex hook: 1 (was up to 4 at DEV-028). Not measured with the packaged benchmark (needs a package build); the single-snapshot property has no test.

## ANALYSIS-1505: relationship tests trimmed

Each row runs under one entry (alternating); both entries for R1, R2, R3, the Claude nested and Claude held representatives, and the mixed row. The two Claude-under-Codex nested rows are dropped (covered by the Resolve theory). Relationship process rows: 50 runs down to 28.

## Mutation checks (each restored; failures in the focused 7-class set of 238 tests)

| Mutation | Failures |
| --- | --- |
| owned: manager equality removed | 6 |
| owned: manager liveness removed | 1 |
| owned: shell liveness removed | 1 |
| owned: attachment equality removed | 1 |
| owned: host shell not from record | 3 |
| owned: same-agent guard removed | 1 |
| store passes no runtime | 2 |
| Neighbors parent creation check removed | 1 |
| Neighbors child creation check removed | 1 |
| Neighbors child bound raised | 1 |
| Neighbors children flag ignored | 1 |
| Neighbors sort removed | 2 |
| Needs ignores provider | 13 |
| shared backend also needs children | 1 |
| unjudged modes need relations | 4 |
| R1 backend always accepted | 20 |
| R1 parent mode check removed | 5 |
| R2 parent ignored | 12 |
| R3 children ignored | 8 |
| incomplete-children check removed | 1 |
| provider gate removed at Hosts call | 7 |
| Neighbors queried with non-matching image | 16 |

All killed. Mutations of the Core liveness/attachment checks are killed by the policy rows (the store filters the same facts first).

## Results

Release build 0 warnings, 0 errors. Full suite once: 1397 passed, 2 failed (`WindowsUserIdleSourceTests.PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation`, `ConsoleTitleProcessTests.HelperChangesOnlyADisposablePrivateConsoleAndRejectsDetachedAttachments(codex, background)`); both classes passed alone on rerun (13/13), unrelated load-sensitive paths. `dotnet format --verify-no-changes`: clean (my new files were formatted with `--include`). Build server shut down; no dotnet, testhost or stand-in processes of mine remain (an unrelated user client process was left untouched).

## Limits

- Permission pump lookup asserted by its identical predicate, not by running the pump.
- npm/shim installs (parent `node.exe` or `powershell.exe`) stay shared by design; a bounded near-ancestor manager check was not added.
- The dead-manager row relies on the orphaned backend's missing parent; PID reuse of the manager is covered by the wrong-creation-time row and the policy rows.
- No real `claude`/`codex`, live instance, packaged ingress benchmark.

## Identity

`./scripts/review-identity.ps1 -Baseline 93e3534 -Paths <all DEV-028 and DEV-029 source and test paths plus implementation-028>`:
`head=93e3534 baseline=93e3534 files=16 diff=19d91ea1ab74 untracked=8/c3787e2144d7`
(this file contains its own identity and is excluded from it).
