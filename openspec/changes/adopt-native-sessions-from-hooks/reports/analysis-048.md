> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-048); client: Claude Code, Agent SDK subagent (client version not observed); mode: non-interactive; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-048 (recheck of the DEV-060 corrections)

## Review target

- **Reviewer / role:** independent code analyzer / code-analyzer
- **Client / version / mode / model (resolved ID):** Claude Code, Agent SDK subagent, non-interactive (client version not observed in this session) / `claude-opus-5-5`
- **Date:** 2026-11-23T09:14Z (UTC)
- **Change root / assignment:** `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-048 (recheck)
- **Task and requirement references:**
  - [tasks](../tasks.md) 4.6 to 4.9; [design](../design.md) decisions 9 to 12
  - [session-identity](../specs/session-identity/spec.md) "Provider-owned terminal titles" (updated per-session rule); [session-tracking](../specs/session-tracking/spec.md)
  - developer evidence [implementation-060](../evidence/implementation-060.md); triage at the end of [findings](findings.md)
- **Scope:** the DEV-060 diff. Files inspected:
  - `src/BellTower.Protocol/`: `HookTrayStart.cs`, `TrayProcess.cs`, `HookStartTask.cs`, `DesktopShell.cs`
  - `src/BellTower/Tray/`: `TrayJobGuard.cs`, `TrayHost.cs`; `src/BellTower/Cli/TrayCommand.cs`
  - `src/BellTower/IO/ProviderTitleState.cs`
  - `src/BellTower/Hosts/`: `TerminalTitleService.cs`, `TerminalPresentationResolver.cs`, `WindowsTerminalFocusService.cs`, `WindowsTerminalNavigator.cs`, `AttachedConsoleTitleWriter.cs`
  - `src/BellTower/Core/`: `NativePresentation.cs`, `TitleRestore.cs`
  - `src/BellTower/Setup/`: `IntegrationSetup.cs`, `HookStartTaskSetup.cs`, `SetupAction.cs`, `SetupPlan.cs`, `ProviderTitleSettings.cs`
  - `src/BellTower/Providers/`: `ClaudeDeclineWatcher.cs`, `TranscriptEvidence.cs`; `src/BellTower/Sessions/CodexPermissionPump.cs`
  - `src/BellTower/Diagnostics/`: `ConfirmationCoverageReporter.cs`, `HookStartReporter.cs`, `DiagnosticsReporter.cs`
  - `docs/hook-ingress.md`
- **Neighbors read:** `src/BellTower.Launcher/Program.cs`, `src/BellTower/Program.cs`, `src/BellTower/Diagnostics/HookDeliveryReporter.cs`, `tests/BellTower.Tests/Testing/ProviderTitleFixture.cs`, `tests/BellTower.Tests/Cli/CliTests.cs`, `docs/development.md`
- **Baseline commit:** `2e7f318`
- **Implementation identity (before):** `./scripts/review-identity.ps1 -Baseline 2e7f318 -Paths src tests docs vscode`
  - HEAD `2e7f3182b6b61d9e5e570a20289b915889df4da1`
  - 40 tracked modified and 21 untracked paths (manifest as printed by the script)
  - tracked diff SHA-256 `39d1ed6bed0e2818b1ed8448b1a52ef1140d20b1152d3d61c05e47c24abb705a`
  - untracked hashes include `81be7c9e…` ProviderTitleState.cs, `2580aa07…` HookStartTask.cs, `211701e8…` HookStartTaskSetup.cs, `b629bb51…` TrayJobGuard.cs, `e4f033d5…` ClaudeDeclineWatcher.cs, `24fc3683…` TranscriptEvidence.cs
  - summary `head=2e7f318 baseline=2e7f318 files=61 diff=39d1ed6bed0e untracked=21/16fc0834ca4b`
- **Before/after identity:** after the analysis, the same command printed `head=2e7f318 baseline=2e7f318 files=61 diff=39d1ed6bed0e untracked=21/16fc0834ca4b`, with the same diff SHA-256. Identical; no unexpected mutation.
- **Prior reports / rechecked findings:** [analysis-047](analysis-047.md): ANALYSIS-4700, 4701, 4702, 4703, 4704, 4705, 4706, 4708, 4709. ANALYSIS-4707 and 4710 are deferred and were not rechecked.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Marker: where it is built | Source, `TrayProcess.Start` and `WithVariable` | `BELLTOWER_HOOK_START=1` is added only to a copy of the block that `CreateEnvironmentBlock(shellToken, false)` built for the shell's token. That copy, not the hook's environment, is passed to `CreateProcessExW` with `CREATE_UNICODE_ENVIRONMENT`. An existing entry with the same name is dropped, entries are sorted by name ignoring case, and the copy is double-terminated and freed in the outer `finally`. The task route (`HookStartTask.Run`) and the task action (`HookStartTaskSetup.RegisterScript`) carry no marker | Built only into the tray's block |
| Marker: removal | Source, `TrayCommand`, `TrayJobGuard.TakeMarker`, `TrayHost.Run`, launcher `Program` | `TakeMarker()` is an argument of `Run`, so it runs before the update gate, the job check and anything the tray launches. It calls `Environment.SetEnvironmentVariable(name, null)`, which updates the process block that `Process.Start` and native children inherit. The launcher keeps the variable in its own block but starts only the payload | Removed before anything the tray launches |
| Marker: spoofing | Source and reasoning | A user-level variable reaches every Explorer-started tray. Those trays run in the session job, which has no kill-on-close, so none is refused. A provider `env` entry reaches only trays started by hand inside that provider's process tree. If such a tray is in a kill-on-close job, it is refused instead of dying when the session ends. Its refusal record then sends hooks to the task for at most ten minutes, or until a tray takes the instance mutex. No privilege boundary is crossed, and the harm is a bounded route choice | Does not matter; no finding |
| Marker: older payload | Source | An older tray does not read the variable, so the start works across a rollback; DEV-060 has a stand-in test that accepts exactly the old arguments. An older tray also does not remove the variable, so its children inherit it. The worst case is a newer tray started by hand inside a kill-on-close job being refused, which is the same benign case as above | Ignored by an older payload as intended |
| 250 ms bound | Source, `HookStartTask.Run` | No handles are inherited and `CREATE_NO_WINDOW` is set. The process and thread handles are closed in `finally` on every return, so a still-running `schtasks` continues as an independent process with no handle open. Results: `WaitForSingleObject != 0` gives `task-run-pending` with outcome `Started`; exit 0 gives `task-run-requested`; any other exit gives `task-run-exit:<code>`. Pending is honest: the detail says so, and the 30 s interval means a request that later fails is retried from the shell route. Cosmetic edges only: `WAIT_FAILED` is also counted as pending, and a failed `GetExitCodeProcess` reads as `task-run-exit:0` | No handle leak; states honest |
| Refusal routing | Source, `HookTrayStart.TryStart:126-136`, `TrayHost.Run`, `HookStartReporter` | The refusal is checked only after the attempt is claimed, so the steady-state hook pays nothing extra. The first hook after a refusal is routed, because the refusal is newer than the attempt that started the refused tray. Later hooks within ten minutes are routed too; after that the shell route is tried again. The record is cleared once a tray holds the instance mutex. A task-started or hand-started tray is never refused. With no task, the result is `shell-route-refused`, recorded as `Failed` | Matches [hook-ingress](../../../../docs/hook-ingress.md) and the DEV-060 note |
| Removal ordering | Source, `IntegrationSetup.PlanProviderTitles`, task removal, `SetupPlan.Apply`, against `docs/development.md` (removal ownership committed after actions succeed) | Provider titles: the manifest change is the plan's `commitAfterActions` and is skipped in the file loop, so ownership is written only after the settings restore. Task: the unregister is an action, `OnSuccess` deletes the record, and the manifest commit comes after the actions. A thrown action leaves both the record and ownership in place. An invalid record is deleted as a file change and touches no task | Ordering conforms. Failure detection inside the script: ANALYSIS-4801 |
| `ProviderTitleState` | Source, all callers | See ANALYSIS-4800 and the 4704 row | Rule implemented; the effective-time record has residual defects |
| Files read by `ProviderTitleState` | Source, `SettingsFile`, `ProviderTitleFixture` | It reads only two files, read-only and at most 1 MiB each: `<CLAUDE_CONFIG_DIR or %USERPROFILE%\.claude>\settings.json` (`env` block) and `<CODEX_HOME or %USERPROFILE%\.codex>\config.toml` (`[tui]`). It never reads a path recorded in the ownership manifest or a `--home` given to setup. In-process tests are redirected by a module initializer and an `AsyncLocal` seam. Out-of-process `doctor` runs in `CliTests` (`:165`, `:259`, `:280`) inherit the test runner's profile and read these files read-only. `HookDeliveryReporter` already does the same for `settings.json` and `hooks.json` at baseline. So the evidence's claim "no test reads a real profile" holds only in process. This is not a new kind of read, and nothing is edited | Reads only the resolved provider file. The record is keyed by provider rather than file: ANALYSIS-4800 |
| Concurrency (ticks, clicks, helper, doctor) | Source | The parse cache is a static `ConcurrentDictionary`. `provider-titles.json` is read, modified and written without a lock by the tray, the presentation helper and doctor. A lost update drops one provider's entry, and the next call records it again from the file's current write time. That direction is conservative: it can only withhold a title. `WindowsTerminalFocusService` and `TerminalTitleService.Apply` evaluate under the presentation lock; the resolver does not | No blocking race. Doctor's effect across processes: ANALYSIS-4800 |
| Watcher exactly-once and exceptions | Source, `ClaudeDeclineWatcher`, `ClaudeDeclineEvidence.DeclinedAt` | Snapshots are taken on the caller's thread, and the transcript is scanned newest first. Without identifiers, a rejection must be at or after the wait's start; with a matched identifier, a 5 s skew is allowed. `published` is keyed by session and decline time and updated only after `Publish` succeeds, so a failed publish is retried. The observation ID comes from the same key, so a republish after a tray restart uses the same inbox name. `InvalidDataException`, `JsonException` and `ArgumentException` are now caught and logged once per session, and `running` is always reset. Doctor's `LastReadFailure` is never cleared after later successes, which is cosmetic | Verified |
| Duplication and complexity introduced | Source | DEV-060 adds a fifth copy of provider-home resolution (`ProviderTitleState.ProviderHome`, alongside `IntegrationSetup.SettingsPath`, `HookDeliveryReporter` and `CodexSharedEndpoint`) and a second `CLAUDE_CODE_DISABLE_TERMINAL_TITLE` constant. For a default run, setup writes and the state reads the same resolved path, so nothing diverges today. `ProviderTitleControl` was removed, so Hosts no longer depend on Setup. The new code is proportionate | Folds into deferred ANALYSIS-4710; no new finding |
| Claude Code and Codex parity | Source and the DEV-060 test list | One `ProviderTitleState` rule, with provider-specific parsing only. Same title step for both. Declines: the Claude transcript adapter and the Codex `Interrupt` hook feed one reducer rule. Doctor: the Codex transcript count now comes from real reads, and Claude has a matching `decline` block. Tray start, marker and refusal routing do not depend on the provider. ANALYSIS-4800 and 4801 affect both providers equally | No parity gap found |
| Builds / tests / format | Not run | Assignment: QA runs them | Not performed |

## Findings

### ANALYSIS-4800: The effective-time record can grant a title to a session whose provider still writes its own

- **Severity:** warning
- **Related task / requirement:** 4.7; session-identity "Provider-owned terminal titles" (scenario "Setting reset or applied while a session runs"); `docs/development.md` (Diagnostics are read-only reports)
- **Locations:**
  - `src/BellTower/IO/ProviderTitleState.cs:28-36`: the file is resolved from the calling process's environment
  - `:73-76`: the parse result, failures included, is cached by stamp
  - `:78-81` and `:95-102`: `Forget`
  - `:83-92`: the record is keyed by `provider` only
  - `:146-149`: a read or parse failure returns `false`, meaning "absent"
  - `src/BellTower/Diagnostics/DiagnosticsReporter.cs:90-92`: doctor calls `Report`, which goes through `Observe` and can write or forget the record
- **Observation:** the per-session rule is implemented, but the persisted effective time has three gaps. The first two can fail in the unsafe direction, which is the T-5 title fight ANALYSIS-4704 was raised for.
  1. **The record is keyed by provider, not by file.** `provider-titles.json` uses only `"claude"` or `"codex"` as keys, but `SettingsFile` depends on the environment of whichever process calls it (`CLAUDE_CONFIG_DIR`, `CODEX_HOME`). A `doctor` run from a terminal with a different home writes under the same key the tray uses for the default file. That can go two ways:
     - If the setting is present in the other home, doctor records that file's first-sighting time. The tray then uses the earlier time and titles sessions that started before the setting reached the default file.
     - If it is absent there, doctor erases the record. The tray re-sights the setting at the file's current write time, and sessions that were titled lose the title mid-run.

     Either way, a diagnostic is changing runtime state.
  2. **A removal and re-add between two observations is missed.** If the setting is removed and re-added between two `Observe` calls (for example while the tray is stopped), the old `since` survives because nothing saw the setting absent. A session started in that gap read a configuration without the setting, yet `DisabledFor` returns true for it.
  3. **A transient failure counts as "absent" (conservative direction).** Examples are a sharing violation while the provider rewrites its file, or a partly written file that fails to parse. The failure is cached as absent for that stamp and erases the record, so titles stop until the file changes again. The setting is then re-sighted at a later time, and sessions started before the failure stay color-only.
- **Proposed improvement and benefit:**
  - Key each record by the resolved file path (or provider plus path), and store the stamp it was last observed at.
  - Make `Report` read-only: compute from the record without writing or forgetting it.
  - Add a third state, "unknown", for an I/O or parse failure. Do not cache it or call `Forget` on it, and answer from the existing record (false if there is none).
  - Optionally, have the tray stat both files on its existing tick, so an absence is seen while the tray runs. That narrows gap 2 to tray downtime, which can be documented as a limitation.

  With these changes the answer stays conservative in every case except the documented downtime gap.
- **Behavior-preservation risks:** the record format changes, so an older provider-keyed record should be read as unknown and re-sighted. Doctor stops creating the record. `ProviderTitleStateTests` gain cases.
- **Evidence type:** source observation
- **Verification / measurement plan:** unit tests through the `ScopedHome` seam, for Claude Code, Codex and a mixed pair:
  - (a) doctor resolving another home leaves the tray's answer and the record unchanged;
  - (b) the setting is removed and re-added with no observation in between, and a session starts in the gap. Expected: no title once the tick observation exists; otherwise a documented limitation;
  - (c) a locked or partly written settings file neither drops the record nor withdraws a granted title.
- **Relationship:** residual of ANALYSIS-4704 (and QA-5400)

### ANALYSIS-4801: The unregister script counts any lookup failure as "task already gone"

- **Severity:** warning
- **Related task / requirement:** 4.6 (setup removal deletes the task); `docs/development.md` (failed removals can be retried); [hook-ingress](../../../../docs/hook-ingress.md) ("a missing task counts as removed … so a failed removal can be retried")
- **Locations:** `src/BellTower/Setup/HookStartTaskSetup.cs`:
  - `:47`: `Get-ScheduledTask … -ErrorAction SilentlyContinue`
  - `:48`
  - `:52-53`: `exit 0`
  - `:37-38`: the doc comment says "any other failure stops the script"
  - `:65`: `OnSuccess` deletes the record
- **Observation:** `-ErrorAction SilentlyContinue` on the lookup suppresses every error, not only "not found". Suppose the Task Scheduler service or its CIM provider is unavailable, or access is refused:
  1. `$task` is `$null`, so the script skips `Unregister-ScheduledTask` and exits 0.
  2. `OnSuccess` deletes `hook-start/task.txt`.
  3. The manifest commit drops ownership.

  The task stays registered, and neither doctor nor a retry has a record to find it by. This is the orphan case ANALYSIS-4703 asked to close, and it contradicts both the method's comment and the documentation. The impact is bounded: the leftover task has no trigger and never runs by itself. It does stay in the user's Task Scheduler with an action that points at the launcher.
- **Proposed improvement and benefit:** look the task up with `-ErrorAction Stop` inside `try`/`catch`. Treat only the not-found error as removed (the CIM `ObjectNotFound` category, or `MSFT_ScheduledTask` reporting no objects). Rethrow anything else, so the script exits non-zero, the action fails, and the record and ownership are kept. A failed removal then really can be retried.
- **Behavior-preservation risks:** none for the normal and not-found paths. The test that counts a missing task as removed must still pass.
- **Evidence type:** source observation. A per-call `-ErrorAction` overrides `$ErrorActionPreference` (PowerShell common-parameter semantics).
- **Verification / measurement plan:**
  - Run `UnregisterScript` with a stand-in executable, or assert on the script text, to show that a lookup failure other than not-found exits non-zero and leaves `task.txt` in place.
  - Confirm the existing missing-task test still exits 0.
  - Repeat one Claude-only, one Codex-only and one mixed hook removal, as DEV-060 did.
- **Relationship:** residual of ANALYSIS-4703

## Re-verification

All rows were checked against `head=2e7f318 … untracked=21/16fc0834ca4b`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-4700 | The bound is 250 ms (`HookStartTask.cs:20`, `:85`). Handles are closed on every path, and a slow `schtasks` keeps running with no handle held. `task-run-pending` is `Started`, not a failure, and a refusal inside the bound is `task-run-exit:<code>`. DEV-060 measured about 27 ms p50 for the fallback with a real task. That is consistent with the code; not re-measured here | verified (source) |
| ANALYSIS-4701 | `IsBellTowerTask` requires a leading `\`, a single folder of up to 64 `[A-Za-z0-9._-]` characters, and this data directory's exact `HookStart-<hash>`, with at most 128 characters in total. It rejects a `"`, a space, a switch, a second folder level, and another directory's hash. `Run` and removal use only `Recorded`, so an invalid record never reaches a command line, and removal then deletes only the record. Doctor reports `invalid` | verified (source) |
| ANALYSIS-4702 | The arguments are exactly `tray --data-dir <root>`. The marker travels only in the tray's environment block, and `TakeMarker` removes it before anything is launched. An older payload ignores it. The `--hook-start` option and its classification are gone | verified (source) |
| ANALYSIS-4703 | Provider titles: the manifest change is the plan's `commitAfterActions`. Task: the unregister is its own action (own wording, no Electron variable), `OnSuccess` deletes the record, and the manifest is committed after the actions. This conforms to `docs/development.md`. The script's lookup still swallows failures other than not-found | ordering verified; residual raised as ANALYSIS-4801 |
| ANALYSIS-4704 (+QA-5400) | `ProviderTitleState` (IO) replaces `ProviderTitleControl`, so Hosts no longer depend on Setup. The decision reads the actual settings file, ignores the manifest, and compares the agent process's start time with the effective time. The resolver, title writer, Windows Terminal focus service and navigator, and doctor all use this one call. An entry for another home no longer counts | rule verified (source); effective-time record defects raised as ANALYSIS-4800 |
| ANALYSIS-4705 | A color-only record stores `Previous = null`, and the next titled write reads the real title (`TerminalTitleService.cs`: `previous = existing?.Previous ?? (colorOnly ? null : ReadTitle)`). A color-only write after a titled one keeps `Written`. Restore runs only when `Written` is non-empty and `Previous` is not null, so a blank title is never written back. `AttachedConsoleTitleWriter` skips `SetTitle` for an empty title | verified (source) |
| ANALYSIS-4706 | Newest-first scan. A rejection without identifiers must be at or after the wait's start; the skew applies only to a matched identifier. `published` is keyed by session and decline time and updated after a successful publish. The `Snapshot` is taken on the caller's thread. The extra exception types are caught and logged once per session | verified (source) |
| ANALYSIS-4708 | A refusal newer than the last attempt, or younger than ten minutes, sends the next hook to the recorded task, or to `shell-route-refused` when none exists. The record is cleared when a tray takes the instance mutex, and doctor shows it only while it exists. Steady-state cost is unchanged | verified (source) |
| ANALYSIS-4709 | The Codex `WithTranscriptEvidence` count runs `CodexApprovalEvidence.HumanMustDecide` over the same bounded read, so a path with no record is not counted. Claude has a `Decline` block with counts only: known transcripts, declines published, last read failure. A failure to open the shell is reported as `shell-open:<error>` (`DesktopShell.cs:42`). `LastReadFailure` is never cleared after later successes, which is cosmetic | verified (source) |

## Assessment and limitations

Scope covered: the DEV-060 corrections for ANALYSIS-4700 to 4706, 4708 and 4709, and the regression surface of the DEV-060 diff, for Claude Code and Codex, by source inspection.

- **Verified by source:** ANALYSIS-4700, 4701, 4702, 4705, 4706, 4708 and 4709.
- **Marker:** built only into the tray's environment block and removed before the tray launches anything. An older payload ignores it, and spoofing it has no meaningful effect.
- **250 ms bound:** leaves no handle open, and its pending and requested states are honest.
- **Removal ordering:** now conforms to `docs/development.md`.
- **Rest of the diff:** no regression or parity gap found.

Two residuals of assigned corrections remain:

- **ANALYSIS-4800 (residual of 4704):** the effective-time record has four gaps:
  - it is keyed by provider rather than by file;
  - doctor writes it;
  - a removal and re-add that nobody observed is missed;
  - a transient read failure counts as absence.

  The first and third gaps can title a session whose provider still writes its own title.
- **ANALYSIS-4801 (residual of 4703):** the unregister script treats any lookup failure as "already removed", so the task can be orphaned without a record.

Not performed:

- builds, tests and format verification (QA owns them);
- no hook latency re-measurement and no real scheduled task registration or removal;
- no elevated-hook, VS Code, NativeAOT or other-build check;
- no live title or notice probe.

My Write of this report to the reserved path was refused with this message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." No files were written.

Decisions needed (orchestrator): triage ANALYSIS-4800 and ANALYSIS-4801. For ANALYSIS-4800, choose between having the tray check the settings files on its tick, which narrows the unobserved removal-and-re-add gap to tray downtime, and documenting that gap as a limitation.
