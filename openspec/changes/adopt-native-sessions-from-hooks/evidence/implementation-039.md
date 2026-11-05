# Implementation 039: management actions dispose observers and stop only owned helpers (task 3.3)

## Finding
No defect. Every terminating call in `src` is a process handle Bell Tower created in the same operation (probe and helper children, the runtime-host manager and the backend that manager started). Owned-runtime shutdown is a marker file (`CodexRuntimeStore.Stop`) read by the manager; it sends no signal. Detach, setup removal, tray exit and update never signal anything. The work made this explicit and tested it.

## Changes
- `CodexRuntimeOpener.cs`, `CodexRuntimeStore.cs`: documentation of the ownership rule only (`CodexAccess.Owned` is the only record that names a stoppable runtime; `Shared` carries a Codex home only). No behavior change.
- `docs/architecture.md`: shutdown-authority paragraph.
- Tests: `ManagementCohort` (stand-in independent shell, Claude client, Codex client, shared and private backends, plus an owned manager honoring the stop marker), `ManagementOwnershipProcessTests` (19 cases), `ProcessStopAuthorityTests` (source scan pinning every terminating call site, and a record-shape test), and an observer-disposal test in `CodexSharedRuntimeServiceTests`.

## Coverage (real bell-tower processes, isolated data directory and home)
Detach (Claude direct, Codex direct, shared, owned); `stop-codex-runtime` (owned ends only its manager; non-owned ids are a no-op); stale, reused-PID and exact-identity records naming a backend, the shell or a client (no process terminated); the real `codex-runtime-host` with a stand-in image that never becomes ready (stops only the backend it started); `setup --remove --apply` on an isolated home; real tray exit with Claude, Codex-direct and Codex-owned sessions registered; the real packaged `__apply-update` coordinator and launcher (tray running, no tray, failure before switch); shared and owned observer connection disposal. Claude and Codex are both covered, with mixed sessions in the tray and update rows.

## Checks (final tree)
- Build Release: 0 errors, 0 warnings. Format run on changed files.
- Targeted: ManagementOwnershipProcessTests 19 passed; ProcessStopAuthorityTests plus CodexSharedRuntimeServiceTests 17 passed.
- Full suite: an earlier run before the last edit failed 2 (`CodexTransportTests.DefaultProvenanceRequiresTheCodexImageEvenWithASharedBackendCommandLine`, `VsCodeWindowNavigationTests.MultipleWindowsGuardReadsAsReloadRecoveryForClaudeAndCodexInNavigationAndDoctor`); the final-tree run failed 1 (`CodexTransportTests.DisposingTheObserverLeavesTheBackendServing`), 1536 passed. All three pass when re-run alone (3 of 3), so they are timing-sensitive under parallel load and not caused by this change; not otherwise investigated.

## Mutants (each killed, then reverted)
1. `StopAndWait` kills the record's manager by PID: 5 failures (four stale-record rows and the stop-site guard).
2. `Detach` also calls `CodexRuntimeStore.Stop`: Codex owned detach row failed.
3. Tray exit writes an owned-runtime stop marker: tray exit test failed.

## Native check
Performed (no quota). Installed Codex 0.166.0 `app-server --listen unix:// --managed-daemon` in a fresh short home under the drive root, dummy key, all proxy variables set to a loopback proxy answering 403. No turns or model requests were made by the harness; the proxy logged 6 denied requests from the backend (egress refused, origin not isolated). With the backend's identity passed to the tests, detach, stop, setup removal (isolated home), tray exit and the update coordinator all ran against isolated data directories and the 19 tests passed. Backend PID and creation ticks were identical before and after. The installed Bell Tower, its data and real homes were not touched (tray mutex names derive from the isolated data path). The harness stopped only its own backend and proxy and deleted the home. No test-started processes remained afterwards.

## Limitations
- Rollback after the switch is not process-tested; it uses the same `RequestExit` and `StartTray` primitives, and the failure-before-switch variant is covered.
- `CodexRuntimeHost.RunAsync` was run only with a stand-in image that never becomes ready, not a ready runtime.
- Detach was run through an attachment registry, not a live provider hook.
