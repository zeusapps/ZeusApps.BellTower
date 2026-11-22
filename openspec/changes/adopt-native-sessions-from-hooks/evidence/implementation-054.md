# Implementation 054: hook starts a stopped tray (task 4.5)

Developer evidence for design decision 9. Baseline 5e95554. Both providers share one implementation (the ingress); test rows run for `claude` and `codex` wherever behavior could differ.

## What changed

- `src/BellTower.Protocol/HookTrayStart.cs` (shared, linked by the ingress, tray and doctor): mutex name (`DataPaths.LockName` now delegates to it, so there is one computation), tray-running check, launcher record and validation, user-stop record keyed by the sign-in, cheap update-pending check, per-interval attempt claim, last-attempt record.
- `src/BellTower.Protocol/TrayProcess.cs`: `CreateProcessW` with no handle inheritance, `CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP | CREATE_BREAKAWAY_FROM_JOB` (retry inside the job on access denied), the launcher folder as working directory, `CreateEnvironmentBlock(token, inherit: false)`; sign-in token from `GetTokenInformation(TokenStatistics)` plus `LsaGetLogonSessionData` logon time. Plain P/Invoke, no reflection.
- `src/BellTower.Ingress/Program.cs`: after the capture is saved (or a benign event needs none) and `{}` is written, `HookTrayStart.TryStart`. Never after a rejection or argument error.
- Tray: the **Exit** menu item records the stop; `TrayController.RequestUserExit` (used only by `bell-tower exit`) records it; `RequestExit` (update, recovery, dev switch in `install-dev.ps1`) does not. `TrayHost.Run` clears the stop and records the stable launcher (only when run from an installation payload). `IngressProvision.Apply` (setup) and `IntegrationRefresh.Apply` (update refresh) record the launcher.
- `src/BellTower/Diagnostics/HookStartReporter.cs`, wired into doctor as `hookStart`.
- Docs: `docs/hook-ingress.md` (new section), `docs/installation.md`, `docs/compatibility.md`.
- Tests: `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs` (26 tests) and a stand-in launcher project `tests/BellTower.Tests.FakeLauncher` (added to the solution and referenced by the test project).

## Decisions taken (flag if wrong)

- Attempt after a durable capture write or a benign no-envelope event. Not after a rejected capture or an argument error (nothing is known to be queued, and a malformed payload should not start a tray).
- Sign-in identity: the token's logon session LUID plus that session's `LogonTime` (LsaGetLogonSessionData). LUIDs restart per boot and could repeat; the logon time makes a record from another boot or sign-in stale. If the logon time is unreadable the token is the LUID alone; if the LUID is unreadable no stop is recorded (fails open: the tray may restart after Exit).
- Interval 30 s: a tray that is still starting is not restarted by every hook of a busy session, and a failed start is retried within a minute. Fixed-window exclusive create `attempt-<n>.lock`, plus yielding when the previous window's record is younger than the interval. A sub-millisecond race at a window boundary can still allow two attempts; the tray mutex keeps one tray.
- Update/switch: the ingress treats `updates/restart.json` younger than 10 minutes as pending (the age the update transaction uses; it does not check the owner process). The tray's `CanStartTray` stays authoritative.
- `--silent` is not passed (normal tray arguments).
- The last-attempt diagnostic is written only for real attempts, not for refusals.

## Verification

| Check | Result |
| --- | --- |
| No tray: exactly one start that consumes the queue (claude, codex) | passed (stand-in launcher; the queue drains into a session of that provider) |
| Tray running: no start | passed (both providers; no attempt record) |
| User stop in the current sign-in: no start; clearing it lets the next hook start | passed (both) |
| Stale record (other LUID; same LUID with another logon time): ignored, start | passed |
| Manual start clears the record; tray records the stable launcher; dev/test tray records nothing | passed (`TrayHost.RegisterStart` against a synthetic installation; the real tray is never run) |
| Only the `exit` command records a stop; `RequestExit` (update, dev switch) does not; `exit` with no tray records nothing | passed |
| Concurrent hooks of both providers (12, 6 each) | passed: one stand-in, 12 captures, all `{}` and exit 0; 32 concurrent claims have one winner; window-boundary behavior unit-tested |
| Missing, relative, wrong-name, directory and reparse-point (junction) launcher record, and no record | passed: nothing starts, capture kept, doctor `Available:false`; the tray also refuses to record a reparse-point path |
| Pending update marker: no start; marker older than 10 minutes: start | passed |
| Hook exits and its stdout reaches EOF while the started child keeps running | passed (EOF asserted while the stand-in pid is alive) |
| Child environment lacks provider and BELLTOWER variables | passed: CLAUDECODE, CLAUDE_CODE_ENTRYPOINT, CODEX_THREAD_ID, CODEX_HOME, BELLTOWER_HOOK_LEAK, BELLTOWER_DATA_DIR and an arbitrary hook-only marker are absent; USERPROFILE, LOCALAPPDATA, PATH present; no standard handle of the child is a pipe |
| Failed start recorded, hook unchanged | passed (non-executable named bell-tower.exe: `Failed create-process:<code>`, `{}`, exit 0, capture kept) |
| Rejected capture and argument error start nothing; a benign no-envelope event starts | passed |
| Doctor reports availability, last attempt, user-stop state | passed |
| Mutant: user-stop check disabled | intended tests fail (2, both providers); restored |
| Mutant: burst guard disabled | intended test fails (`ConcurrentHooksOfBothProvidersStartOneTray`); restored |
| Mutant: handles inherited | 4 tests fail (the hook's stdout never closes); restored |
| Mutant: provider environment inherited | 2 tests fail (both providers); restored |
| `dotnet build -c Release --no-restore --no-incremental` | passed, 0 warnings, 0 errors |
| Full suite `dotnet test -c Release --no-build --no-restore` | passed: 1647 of 1647, no load-only failures this run |
| Native AOT publish of the ingress | passed, no trim or AOT warnings |
| Format verify | exit 0 (see note 1) |
| Real tray, real data directory, installed build | not performed by design; no test starts a real tray; only stand-ins a test started are released or killed |
| Hook start in real Windows Terminal or VS Code after a restart | not performed (task 6.1 user trial) |

### Hook latency (ingress wall time)

Method: PowerShell `Stopwatch` around process start through exit with stdin, stdout and stderr redirected, a real Claude SessionStart fixture payload, 3 warm-up then 50 samples per row, the published Native AOT ingress (before: baseline source, after: this change), a disposable data directory, a held named mutex for "running". Rounds were interleaved. Noise is about 8 to 15 ms (process start and timer granularity) on a non-idle machine. Milliseconds.

| Row | median | p95 | max |
| --- | --- | --- | --- |
| before, tray running (r1 / r2) | 45.3 / 38.0 | 55.7 / 40.5 | 59.1 / 43.0 |
| after, tray running (r1 / r2) | 39.0 / 39.1 | 54.9 / 51.2 | 57.1 / 54.9 |
| before, tray stopped (r1 / r2) | 37.9 / 46.2 | 54.9 / 56.4 | 57.4 / 57.8 |
| after, tray stopped, no launcher record (r1 / r2) | 46.5 / 40.8 | 55.9 / 52.7 | 57.2 / 55.5 |
| after, stopped, user stop active | 42.6 | 58.1 | 60.1 |
| after, stopped, launcher valid, attempt already claimed this interval | 39.3 | 43.3 | 194.2 (one outlier) |
| after, stopped, a real attempt starts the stand-in every sample | 52.4 | 65.7 | 71.5 |

Reading: with the tray running or any refusal the added cost is within noise. A start attempt (at most once per 30 s) costs roughly 10 to 14 ms more at the median. These are measurements of this machine, not an acceptance threshold.

### Job object evidence

- Claude Code 2.1.325, print mode, isolated settings (`--setting-sources project` and a temporary settings file; the user's hooks and settings were not used): a SessionStart hook ran in a job object with `LimitFlags=0x1800` (breakaway allowed, silent breakaway allowed, no kill-on-close). This held also when claude itself was started outside any job through `CREATE_BREAKAWAY_FROM_JOB`, so the job comes from Claude Code's own process spawning, not only the harness. A session started from Windows Terminal or VS Code was not probed.
- Codex 0.167.2: not probed (needs an authenticated or fake-endpoint session; not run to avoid touching real configuration). If its job refuses breakaway the start retries inside the job, doctor shows `StartedWithoutBreakaway`, and the tray could end with that job.
- The breakaway-refused fallback path is not covered by an automated test: not performed (no harness assigns the ingress to a no-breakaway job); left for review.

## Notes for the orchestrator

1. The pinned SDK 10.0.114 (`global.json`, rollForward disabled) is not installed here; only 10.0.115 and 9.0.321. I built, tested and published from a directory outside the repository so `global.json` did not apply, with `vswhere` on PATH, using `--no-restore`. A restore with 10.0.115 rewrote four `packages.lock.json` files (ILCompiler 10.0.14 to 10.0.15); I reverted them. `scripts/format.ps1 -Verify` cannot run in the repository with this SDK, so I ran `dotnet format` and then `--verify-no-changes` on the whole solution (exit 0, no change to any of my files) in a restored copy of the tree without `global.json`. The new `tests/BellTower.Tests.FakeLauncher/packages.lock.json` came from that restore (project reference only).
2. The launcher is validated and started without re-validation between check and use; a swap in that window is out of scope (the path must already lie in a plain tree).
3. Orphaned `artifacts/unit-tests/*` folders from earlier runs exist and are untouched; the two folders my aborted mutant run left were removed. No process I did not start was stopped.
