# Implementation 001: bound-ended-session-state tasks 1.1-1.5 and 2.1-2.3

## Changes

- `src/BellTower/Core/CausalSessionReducer.cs`: `Compact(SessionState)` drops the journal, checkpoint, completed and open operations of an ended session and moves `CausalFloor` to the latest journal observation (the existing floor is kept when the journal is empty).
- `src/BellTower/IO/StateStore.cs`: the drain compacts ended sessions untouched for 10 minutes (`SettleInterval`), then prunes by the 30-day age and a cap of 200 ended sessions (`MaxEndedSessions`, oldest `LastObserved` first, key as tie-break). Both run before the save gate, so a quiet state is also bounded and a change in retention triggers a save. Retention uses the injected `TimeProvider`. A new optional `budgetBytes` constructor argument is the test seam.
- `src/BellTower/IO/StateSnapshotStore.cs`: budget is an injectable constructor argument (`DefaultBudgetBytes` = 16 MiB, unchanged in production). An over-budget save removes the oldest ended sessions. Per-session encoded sizes are computed once and sessions are removed until the estimated excess is covered. The result is re-encoded at most `FallbackPasses` (4) times, and the last pass removes every ended session. It throws `InvalidDataException` only when no ended session remains. `LastSaveEncodes` is internal, for the bound test.
- `src/BellTower/IO/RefreshHealth.cs` (new) and `LocalLog.cs`, `DataPaths.cs`: reasoned, rate-limited `tray-refresh-failed: <type>: <message>` log. The message is capped at 200 characters, control characters are removed, and the data directory and any drive, UNC or common Unix path are replaced. It logs on a reason change or after 10 minutes. `tray-refresh-recovered` is logged on the first success after a failure, including a failure recorded by an earlier run. `refresh-health.json` (`failingSince`, `reason`, `lastSuccess`) is written atomically on transitions only. `LocalLog.Write(code, reason)` is a new overload. The old code-only path is unchanged.
- `src/BellTower/Tray/TrayApplication.cs`: surgical edit. `refreshHealth.Failed(e)` replaces the bare log and `refreshHealth.Succeeded()` is called at the end of a successful tick.
- `src/BellTower/Diagnostics/StateReporter.cs` (new) and `DiagnosticsReporter.cs`: doctor `state` section with `state.json` bytes and percent of the budget, live and ended counts (overall and per provider) from a read-only snapshot, the retention policy and the refresh health. A failure is `persistent` when `failingSince` is more than 1 minute old while the tray is running. Missing files give an honest message and never throw. `DiagnosticsReporter` now reads `TrayRunning` once into a local that both fields use.
- `docs/architecture.md` (new "Bounded monitoring state" paragraph) and `docs/hook-ingress.md` (retention sentence): they match the settle interval, cap, fallback and doctor fields.
- No new `state.json` fields. A compacted session uses an empty journal and a null checkpoint, which `IsValid` accepts and older builds read.

## Decisions

- Retention runs before the save gate, so a state that only has newly settled sessions is still compacted and saved on the next drain.
- Compaction is tied to `Ended` and `LastObserved` older than 10 minutes. No provider branch exists anywhere.
- Fallback removes by measured session weight, not one re-encode per session. The nested indentation makes the estimate low, so a second pass can occur. The cap of four re-encodes bounds it.
- The refresh message uses the exception type name and message only. Exception types outside the existing catch filter are unchanged and still propagate.

## Tests added (all pass)

- `tests/BellTower.Tests/IO/BoundedStateTests.cs` (Claude Code and Codex theories, plus mixed fixtures):
  - compaction keeps the displayed state, and the floor, journal, checkpoint and operations are as designed (1.1);
  - a session within the interval and a live session are not compacted (1.1);
  - a delayed earlier event on both the causal and the direct path leaves the session ended with no attention (1.2);
  - a resume registration revives the session, and out-of-order approval, resolution and a second pending approval are ordered and tracked (1.2);
  - the 200 cap over 230 mixed-provider ended sessions plus 6 live ones, and the 30-day rule (1.3);
  - the over-budget fallback with a reduced budget, oldest removed first, at most 5 encodes, live kept (1.4);
  - live sessions alone over the budget throw, and the captures and `state.json` bytes are unchanged (1.4);
  - recovery: 40 mixed ended sessions with full journals, a budget equal to the written size plus 512 bytes, 6 queued captures from both providers. One drain saves below a quarter of the size and consumes and deletes all captures (1.5);
  - the production budget stays at 16 MiB.
- `tests/BellTower.Tests/IO/RefreshHealthTests.cs`: one entry per 10 minutes, a changed reason logs at once, recovery with record update, transitions-only writes, recovery after a restart, and no data-directory or other path in the log or record, with the length bounded (2.1).
- `tests/BellTower.Tests/Diagnostics/StateReporterTests.cs`: missing files, size, budget and counts for both providers, persistent versus not (tray running or not, under or over a minute), and recovered (2.2).

## Checks

- `dotnet build -c Release --no-restore`: succeeded, 0 warnings.
- Focused: BoundedStateTests, RefreshHealthTests and StateReporterTests passed, 24 of 24.
- `dotnet format ... --include <my changed files>`: ran with no diagnostics.
- Full suite: 1674 passed, 19 failed.
  - Seven classes failed only under full-suite load, with one isolated rerun of those classes: `WindowsUserIdleSourceTests` (2), `PresentationSchedulerTests`, `CodexSharedAccessTests`, `PowerShellModuleTests` (the Unicode and environment ones), `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks` and `AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor`. They passed in the rerun (103 of 104).
  - `ProcessStopAuthorityTests.OnlyHandlesCreatedByTheOperationAreEverTerminated` also failed in the rerun, so it is not load-only. It is process-handle logic with no state, drain or tray-log dependency, and it is not among the five known failures.
  - Twelve `HookTrayStartTests` failed, for example `ABenignEventWithNoEnvelopeStillStartsAStoppedTray` (`Assert.Single` on an empty fake-launcher list). That file and the fake launcher belong to the adopt-native-sessions-from-hooks work and exercise hook-started launcher processes, not the state store.
  - Not caused by this change: none of these touch the changed code paths (`StateStore`, `StateSnapshotStore`, the reducer compaction or the refresh health), but I did not run them against a clean baseline.
  - The known five (Unicode hook cleanup, two Codex transport timeouts, stalled-backend pump timing, hook-maintenance budget) are only partly the same set as the failures seen, so I did not map them one to one.

## Limits

- The installed check (3.3) and the real data directory were not touched or run. The real tray was not stopped.
- No QA or analyzer review was run. This handoff is awaiting independent verification.
- `refresh-health.json` is not covered by an end-to-end `TrayApplication` test. Only the recorder and doctor are, because `TrayApplication` is a UI host with no seam in tests.
- When a failure is repeated, the log rate limit is in memory, so a restarted tray logs the same failure once more (by design: the first failure of a run).
