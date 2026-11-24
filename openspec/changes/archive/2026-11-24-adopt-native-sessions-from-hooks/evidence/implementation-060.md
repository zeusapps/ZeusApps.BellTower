# DEV-060: fix round on the DEV-059 target (QA-054, ANALYSIS-047)

Baseline `2e7f318`, on top of the uncommitted DEV-058 and DEV-059 work. Same constraints as DEV-059: isolated provider homes, disposable data directories under the ignored `artifacts/`, a disposable task folder (deleted), no real settings, tray or installation, no commit. ANALYSIS-4707 and ANALYSIS-4710 are deferred and unchanged.

## Per finding

| Finding | Status | What changed |
| --- | --- | --- |
| ANALYSIS-4700, QA-5401 | fixed | `HookStartTask.Run` waits at most 250 ms for `schtasks /run`. An exit 0 inside the bound is `task-run-requested`, a still-running request is `task-run-pending` (outcome Started, not a failure), a refusal inside the bound is `task-run-exit:<code>`. Latency below. |
| ANALYSIS-4701 | fixed | A task record is accepted only as `\<folder of letters, digits . - _ up to 64>\HookStart-<hash of this data directory>`. Anything else is not used: no `schtasks`, no removal action (the record alone is deleted with a note), and doctor says "recorded path is not a Bell Tower task". `HookStartTask.RecordState` is none, valid or invalid. |
| ANALYSIS-4702 | fixed | The marker is the environment variable `BELLTOWER_HOOK_START=1`, added to the environment block the hook builds for the shell's token (sorted, double-terminated); the arguments are exactly `tray --data-dir <root>`, which every payload accepts. The tray reads and removes the variable at start (`TrayJobGuard.TakeMarker`), so nothing it launches inherits it, and an older tray ignores it. The `--hook-start` option and its classification change are removed. |
| ANALYSIS-4703 | fixed | `--provider-titles --remove` passes the manifest change as `commitAfterActions`, so ownership is kept until the settings were restored. The task removal is its own `SetupAction` (own failure wording, no Electron environment, `OnSuccess` deletes the record), a PowerShell script with `ErrorActionPreference Stop` that treats a missing task as removed, then removes the empty task folder. The record is deleted only after success. `IntegrationSetup.RemoveTaskAction` is the test seam. |
| ANALYSIS-4704, QA-5400 | fixed (orchestrator decision) | New `ProviderTitleState` (IO): the title is in effect disabled for a session when the disabling setting is present in the provider's actual settings file (`CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1` in the `env` block, or `[tui] terminal_title = []`) whoever set it, and the session's process started after the setting took effect. The effective time is the file write time at first sighting (kept in `provider-titles.json` while the setting stays present, cleared when it goes), the answer is cached by file write time, and the manifest is no longer consulted for the decision. `ProviderTitleControl` (Setup) is removed; the resolver, writer, Windows Terminal navigation and doctor use the one service. A hand-set value counts; the step still leaves it unowned. |
| ANALYSIS-4705 | fixed | A color-only restore record stores `Previous = null`; the first titled write in the shell reads the real title; restore gives back that title (and never writes a blank one). |
| ANALYSIS-4706 | fixed | `DeclinedAt` scans newest first; a rejection counts only at or after the wait's start, except for a matched tool use identifier (5 s skew); "published" is keyed by session and decline time; session fields are copied on the caller's thread (`Snapshot`); `InvalidDataException`, `JsonException` and `ArgumentException` are caught and logged once per session. |
| ANALYSIS-4708 | fixed | A refusal record newer than the last attempt (or younger than ten minutes) sends the next hook to the recorded task, or fails with `shell-route-refused`; the record is cleared when a tray takes the instance mutex, and doctor shows it only while it exists. |
| ANALYSIS-4709 | fixed | Codex coverage counts sessions whose transcript produced a turn record now (the pump's own read); a Claude `decline` field reports sessions with a known transcript, declines published and the last read failure (`decline-evidence.json`, counts only); an open failure on the shell is `shell-open:<error>`. |

## Hook latency (ANALYSIS-4700 measurement plan)

Method: a harness (`artifacts/t12-probe/harness`) loads the built assemblies, uses a disposable data directory and a real recorded per-user task under the disposable folder `BellTowerTest-DEV060M` (action: a harmless system executable), and deleted the task afterwards (none remains). The managed ingress binary was timed as a process, since the NativeAOT image cannot be built here.

| Row | Result |
| --- | --- |
| Hook process wall time, tray running (baseline, managed ingress, 27 samples after 3 warm-ups) | p50 41 ms, p95 42 ms, max 43 ms |
| `TryStart` with the tray running (in process, 40 samples) | p50 0.00 ms, p95 0.01 ms, max 0.65 ms |
| Fallback after a failed shell start: `HookStartTask.Run` with a real task (25 samples) | p50 26.7 ms, p95 37.8 ms, max 40.0 ms; all 25 were `Started/task-run-requested` |
| Worst case with a `schtasks` that does not return (stand-in, test) | returns in under 1.5 s with `task-run-pending` (bound 250 ms) |

So a hook that takes the fallback costs about 27 ms (up to 250 ms) more than the baseline, against up to 3 s before. The shell start failing was injected in process (`TryStart` with a failing shell stand-in), because a separate ingress process cannot be made to fail shell verification without changing the real desktop; the end-to-end figure is therefore baseline plus the measured delta, not a single measured wall time. The shell route itself (a real start) was not re-timed.

## Tests added or changed

- `HookTrayStartTests` (77): the non-blocking and pending task request with a stand-in `schtasks` (fast, slow, failing); invalid records (embedded quote, extra switches, another folder, another data directory's hash, relative, empty) never start `schtasks`, are reported by doctor and plan no removal; a hook against a payload that rejects unknown options (the stand-in accepts exactly the old arguments) starts for Claude and Codex; the environment block copy; the tray removes the marker; the refusal routing (task, `shell-route-refused`, cleared) and a real hook after a refusal for both providers; the shell open failure. The two marker-as-argument assertions became environment assertions.
- `ProviderTitleStepTests` (26): a failed settings write during removal keeps ownership and a retry removes the setting, for Claude and Codex; a failed unregister keeps the record and a retry succeeds; no task action for no record or an invalid one; removing a missing task counts as removed; the step applied while a session runs titles only later sessions; a color-only write followed by a titled one restores the real title, for both providers.
- `ProviderTitleStateTests` (12): applied while a session runs, user reset and re-apply, hand-set value, an ownership entry for another home, mixed providers, presentation and Windows Terminal navigation following the per-session answer, doctor.
- `WaitEvidenceTests` (14): two waits within 5 s with no identifiers and the first declined, matched identifiers with skew, a refused publication logged once, doctor counts for both providers.
- The presentation, click and setup fixtures now write the setting into a disposable provider home instead of the manifest; the test assembly defaults the title state to an empty home, so no test reads a real profile.

## Verification

`dotnet build -c Release --no-restore`: 0 warnings, 0 errors. Full suite: 1951 passed, 2 failed of 1953; both failures are load-bound (`CodexTransportTests.EachOpenMakesOneBackendConnectionAndRequestsMakeNone`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`, the latter the known 15 to 25 s timeout) and both passed together when rerun alone. `HookTrayStartTests` 77/77. `dotnet format --verify-no-changes` on the changed C# files passed; `openspec validate adopt-native-sessions-from-hooks --strict` passed. `vscode/` is unchanged, so the node bridge tests were not run.

## Notes for the orchestrator

- ANALYSIS-4702 changed the mechanism the planning text calls "the tray's own marker": the spec text (a hook-started tray checks its job) is unchanged; only its carrier changed from an argument to an environment variable. `docs/hook-ingress.md` says so.
- A refusal binds for ten minutes even after a later attempt, so a false trigger with no task keeps failing with `shell-route-refused` (visible in doctor) rather than repeating the shell start; starting the tray by hand clears it.
- The effective time of a hand-set setting is the file's write time when Bell Tower first saw it present, so it can be later than the true time (a session started in between gets color only until it restarts); it never errs toward titling a session whose provider still writes its own title.
- Not verified: an elevated hook, VS Code hosts, other Windows builds, NativeAOT publish, a real end-to-end hook wall time with a failed shell start.
