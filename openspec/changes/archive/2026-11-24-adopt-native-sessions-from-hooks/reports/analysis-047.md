> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-047); client: Claude Code, Agent SDK subagent (client version not observed); mode: non-interactive; model: claude-opus-5-5. The run was interrupted by a usage limit and resumed on the same target.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-047 (DEV-059, tasks 4.6 to 4.10)

## Review target

- Reviewer / role: independent code analyzer / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code, Agent SDK subagent, non-interactive (client version not observed in this session) / `claude-opus-5-5`
- Date: 2026-11-23 (UTC); the run was interrupted by a usage limit and resumed on the same target
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-047
- Task and requirement references: [tasks](../tasks.md) 4.6 to 4.10; [design](../design.md) decisions 9 to 13; delta specs [session-tracking](../specs/session-tracking/spec.md) ("Hook tray start from the user's shell" with the narrowed self-check wording, "Confirmed waits from provider evidence"), [session-identity](../specs/session-identity/spec.md) ("Provider-owned terminal titles"), [standalone-setup](../specs/standalone-setup/spec.md) ("Opt-in provider title control"), [terminal-navigation](../specs/terminal-navigation/spec.md); developer evidence [implementation-059](../evidence/implementation-059.md)
- Scope: `src/BellTower.Protocol/DesktopShell.cs`, `TrayProcess.cs`, `HookTrayStart.cs`, `HookStartTask.cs`; `src/BellTower/Setup/HookStartTaskSetup.cs`, `IngressProvision.cs`, `IntegrationSetup.cs`, `IntegrationOwnership.cs`, `ProviderTitleSettings.cs`, `ProviderTitleControl.cs`, `CodexFooterSettings.cs`; `src/BellTower/Cli/SetupCommand.cs`, `TrayCommand.cs`; `src/BellTower/Tray/TrayJobGuard.cs`, `TrayHost.cs`, `TrayApplication.cs`, `NavigationNotice.cs`, `NoticeForm.cs`; `src/BellTower/IO/TrayInvocationCommand.cs`, `ConfirmationEvidence.cs`; `src/BellTower/Providers/TranscriptEvidence.cs`, `ClaudeDeclineWatcher.cs`; `src/BellTower/Sessions/CodexPermissionPump.cs`; `src/BellTower/Core/NativePresentation.cs`; `src/BellTower/Hosts/TerminalTitleService.cs`, `TerminalPresentationResolver.cs`, `AttachedConsoleTitleWriter.cs`, `WindowsTerminalFocusService.cs`, `WindowsTerminalNavigator.cs`, `FocusResult.cs`; `src/BellTower/Diagnostics/HookStartReporter.cs`, `ConfirmationCoverageReporter.cs`, `CapabilityMatrix.cs`; `docs/compatibility.md`. Neighbors read: `src/BellTower.Ingress/Program.cs`, `src/BellTower.Launcher/Program.cs`, `src/BellTower.Distribution/CaptureCompatibility.cs`, `src/BellTower/Setup/SetupPlan.cs`, `SetupAction.cs`, `IntegrationRefresh.cs`, `src/BellTower/Core/SessionReducer.cs`, `src/BellTower/IO/EventStore.cs`, `StateStore.cs`, `StateSnapshotStore.cs`, `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs`, `tests/BellTower.Tests.FakeLauncher/Program.cs`, `docs/development.md`, `docs/installation.md`
- Baseline commit: `2e7f318`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2e7f318 -Paths src tests docs vscode` -> HEAD `2e7f3182b6b61d9e5e570a20289b915889df4da1`; 39 tracked modified and 20 untracked paths (manifest as printed by the script); tracked diff SHA-256 `4d73eda7e119c444f251980e084faee1b9a7096b4f3bc0dfb4a47145b7aad9f7`; untracked hashes include `2b42a0a8…` DesktopShell.cs, `b48b9eb4…` HookStartTask.cs, `6e74e264…` HookStartTaskSetup.cs, `7923a91a…` ProviderTitleControl.cs, `1931dcdb…` ProviderTitleSettings.cs, `05e1c37e…` TrayJobGuard.cs, `ec2cb722…` TranscriptEvidence.cs, `c0129c29…` ClaudeDeclineWatcher.cs, `a9105822…` NavigationNotice.cs, `1e4296a3…` NoticeForm.cs; summary `head=2e7f318 baseline=2e7f318 files=59 diff=4d73eda7e119 untracked=20/232a0d9e80a8`
- Before/after identity: rechecked after the interruption and at the end: `head=2e7f318 baseline=2e7f318 files=59 diff=4d73eda7e119 untracked=20/232a0d9e80a8` each time. Identical; no unexpected mutation.
- Prior reports / rechecked findings: [analysis-046](analysis-046.md) ANALYSIS-4600, ANALYSIS-4601, ANALYSIS-4602, ANALYSIS-4603

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Tray start: parent attribute and handle hygiene | Source, `TrayProcess.Start` | `bInheritHandles = false`; no `STARTF_USESTDHANDLES`; `CREATE_NO_WINDOW` gives the console child its own console. Attribute list: sized, initialized (freed and zeroed if initialization fails), `DeleteProcThreadAttributeList` runs before the value buffer is freed, so the value outlives the list as required. On every return path the `finally` closes the shell process handle, the shell token, the environment block, the command buffer and the attribute list; the child process, thread and token handles are closed in their own `try/finally`. `StartupInfo`/`StartupInfoEx` layouts and `cb` match the Win32 definitions for x64. `lpApplicationName` is the validated absolute launcher, so no search path is used | Sound. No leak found |
| Shell verification | Source, `DesktopShell` | PID reuse: the PID from `GetShellWindow` is opened, then `GetShellWindow` is read again and must give the same PID. The open handle pins the PID, so a match proves the opened process owns the shell window. This is equivalent to the creation-time check the design describes. Also checked: the image equals `%WINDIR%\explorer.exe`, the token user SID equals the hook's, and the session ID equals the hook's. Other-user and other-session shells fail closed. A failed `OpenProcess` is reported as `shell-image-unverified` rather than as an open failure (folded into ANALYSIS-4709) | Sound |
| Environment | Source | `CreateEnvironmentBlock(shellToken, bInherit: false)` builds the user's default environment from the shell's token, so nothing comes from the hook (`CLAUDECODE`, `CODEX_*`, `BELLTOWER_*`). This is the user's registry environment, not Explorer's live block. It matches decision 9 ("the user's default environment"); the spec text's "the shell's … environment" is a looser wording, which causes no defect | Sound |
| Elevation guard | Source | The child is created suspended. Its `TokenElevation` is read and it is resumed only if it is not elevated while the shell is not. An unreadable child token also stops it. When UAC is off and the shell is elevated, the tray is allowed: no worse than the shell. `ResumeThread` must return 1 | Sound by source; the elevated-hook path is unverified live (as DEV-059 states) |
| Command lines | Source | Shell route: `Quote(launcher) tray --data-dir Quote(root) --hook-start`. A Windows path cannot contain `"`, and the launcher must exist under a validated path, so the quoting cannot be broken there. A root that contains `"` cannot hold a launcher record, so `NoLauncher` returns first. Task registration: PowerShell single-quote escaping inside `-EncodedCommand`; `schtasks /query` uses `ArgumentList`. `schtasks /run /tn "<task>"` takes the task path from an unvalidated record: ANALYSIS-4701 | See ANALYSIS-4701 |
| Scheduled-task definition | Source, `HookStartTaskSetup.RegisterScript` | No trigger; `-LogonType Interactive -RunLevel Limited` for the current user; `ExecutionTimeLimit` zero; both battery switches set; `MultipleInstances IgnoreNew`; the task name is unique per data directory; the action is the launcher with `tray --data-dir`, with no `--hook-start` marker | Matches decision 10. Ownership and removal: ANALYSIS-4703 |
| Hook-path latency | Source, `Ingress/Program.cs`, `HookTrayStart.TryStart`, `HookStartTask.Run` | Steady state is still one mutex open. `TryStart` runs after `{}` is written but before the process exits, so its time is on the hook's critical path. Shell route: process open, token queries, `CreateEnvironmentBlock` (registry reads), suspended create, at most once per 30 s. Task route: `WaitForSingleObject(schtasks, 3 s)` | ANALYSIS-4700. Not measured |
| Process-stop guard | `ProcessStopAuthorityTests` allow-list and a grep of `Kill(`/`TerminateProcess` | The only new sites are `TrayProcess.StopSuspended` (the suspended child it created) and `HookStartTaskSetup.Run` (its own PowerShell child after 60 s). `HookStartTask.Run` leaves a slow `schtasks` running rather than killing it. `TrayJobGuard` exits its own process before taking the mutex | Guard holds |
| Version skew of the marker | Source, launcher, `CaptureCompatibility.StartsTray`, `IntegrationRefresh` | The launcher forwards every argument to the selected payload. `StartsTray` still classifies `tray … --hook-start` as a tray, so the capture gate applies. A tray older than DEV-059 does not know `--hook-start` | ANALYSIS-4702 |
| Tray self-check | Source, `TrayJobGuard`, `TrayHost`, `TrayCommand`, `TrayInvocationCommand` | Only `--hook-start` checks, matching the narrowed spec. The flags offset (16) and the 144-byte buffer match `JOBOBJECT_EXTENDED_LIMIT_INFORMATION` on x64. No job, or a failed read, never refuses. The guard runs after the update gate and before the mutex. The marker is accepted by the tray-invocation classifier | Diagnostic-only as required, but a refusal is never routed to the fallback and never cleared: ANALYSIS-4708 |
| Provider title step | Source, `ProviderTitleSettings`, `IntegrationSetup.PlanProviderTitles`, `SetupPlan` | One key per provider. Ownership records the previous value and whether setup created the container. A repeat changes nothing, a user edit is kept, and a hand-made value stays unowned. Invalid JSON/TOML and a non-object `env` or non-table `tui` throw before any write. The edited file is backed up through `SetupPlan`. TOML edits are text splices located by the Tomlyn syntax tree (the existing footer helper); appending `[tui]` at the end, or a root dotted key when only sub-tables exist, is valid TOML. Removal of an inline-table `tui` key is a no-op (apply refuses to create one, so only a user-converted file reaches it) | Edits are safe. The removal commit order and the runtime use of ownership: ANALYSIS-4703, ANALYSIS-4704, ANALYSIS-4705 |
| Wait evidence: bounded reads and path safety | Source, `TranscriptTail` | Fully qualified, not UNC, not device-prefixed, not a directory, device or reparse point; `FileShare.ReadWrite or Delete`; last 1 MiB; partial first and last lines dropped; lines over 256 K characters skipped; only booleans and times leave the adapters. Path components are not checked for reparse points (the launcher check does this), which is acceptable for a read whose results carry no content | Sound. Cost: ANALYSIS-4707 |
| Wait evidence: format drift | Source, both adapters | Unknown or damaged lines are skipped. A missing `approvals_reviewer` gives `null` (stays provisional); a renamed `toolDenialKind` means no clear (the old behavior). Both fail safe, but drift is invisible in doctor for Claude: ANALYSIS-4709 | Safe |
| Wait evidence: timing never confirms | Source, `CodexPermissionPump.Record`, reducer | Confirmation comes only from `waitingOnApproval` or `approvals_reviewer == "user"`. The 5 s `since` carry-over and the 15 s chip are pre-existing grace/visibility rules and never confirm. Automatic review records `Pending = false` | Holds for both providers |
| Wait evidence: watcher lifetime, cadence, threading, exactly-once | Source, `ClaudeDeclineWatcher`, `TrayApplication.Tick`, `StateStore.Drain` | Created with the tray and disposed in `ExitThreadCore`. At most one check runs at a time (CAS), with ticks gated to 1 s. Each tick works on freshly deserialized state, and the UI thread later writes only a scalar to those objects. The observation `Id` is derived from session plus wait start, so a republish after a restart rewrites the same inbox name. The reducer drops an interrupt older than `TurnStarted`. Exactly-once can be lost through a stale rejection inside the skew window; failures outside `IOException`/`UnauthorizedAccessException` fault the task silently every second | ANALYSIS-4706 |
| Delivery (4.10) recheck | Source | See Re-verification | 4600 to 4603 verified by source |
| Duplication | Source | The `IntegrationOwnership` copy block exists in `Plan` and `PlanProviderTitles`. `StartupInfo`, `ProcessInformation`, `CreateProcessW` and `CloseHandle` are declared in both `TrayProcess` and `HookStartTask` (and `CloseHandle` again in `DesktopShell`). `TrayProcess` keeps dead constants. `"integration.json"` is now hard-coded in a fourth place | ANALYSIS-4710 (and ANALYSIS-4704 for the path) |
| Architecture | Against `docs/development.md` | Hosts now depend on `Setup` (`ProviderTitleControl`) at runtime. Removal ordering conflicts with the rule that removal ownership is committed after actions succeed (`docs/development.md` line 29). Diagnostics depend on `Tray.TrayJobGuard` for a record path | ANALYSIS-4703, ANALYSIS-4704 |
| Claude Code and Codex parity | Source and tests listed in DEV-059 | Tray start: one shared ingress path, provider-independent. Title step: both providers, same ownership model. Wait rule: one reducer rule. Codex confirms from the transcript reviewer and clears through its `Interrupt` hook; Claude confirms through the existing `permission_prompt` and clears from the transcript decline. Navigation and notice: shared. Remaining gap: diagnostics for the Claude decline adapter and an overstated Codex transcript count | ANALYSIS-4709 |
| Builds / tests / format | Not run | Assignment: QA runs them | Not performed |

## Findings

### ANALYSIS-4700: The task fallback blocks the hook for up to 3 seconds

- Severity: warning
- Related task / requirement: 4.6; design decisions 9 and 10 ("never waits for the tray, never delays or fails the hook"); hook latency budget
- Locations: `src/BellTower.Protocol/HookStartTask.cs:16`, `:56-69`; `src/BellTower.Protocol/HookTrayStart.cs:106-111`; `src/BellTower.Ingress/Program.cs:59-66`
- Observation: `TryStart` runs in the hook process after `{}` is written but before the process exits, and providers wait for the hook process to finish. When the shell start fails and a task is recorded, `HookStartTask.Run` waits up to 3 s for `schtasks /run`, and it reports `task-run-timeout` as a failure although the run may still be accepted. This happens at most once per 30 s burst interval. It hits precisely the restart and degraded-shell cases the fallback exists for, so the first hook after a sign-in can be delayed. DEV-059 did not re-measure hook latency.
- Proposed improvement and benefit: do not wait. `schtasks` is already created without inherited handles and with its own console, so the hook can close its handles at once and record `task-run-requested`. If an exit code is needed for diagnostics, run the hook with a short bound (about 250 ms) and record `task-run-pending` on timeout, not a failure. The design's "never delays the hook" then holds on both routes.
- Behavior-preservation risks: the `last-attempt` detail changes from `started-via-task` to a requested state when the exit code is unknown. Doctor wording and `HookTrayStartTests` assertions on the route change accordingly.
- Evidence type: source observation; latency not measured
- Verification / measurement plan: measure hook wall time with the tray stopped and the shell start forced to fail (injected `shellStart`), and with a real recorded task, for one Claude Code hook and one Codex hook. Compare with the tray-running baseline.
- Relationship: none

### ANALYSIS-4701: The recorded task path is not validated before it is used in command lines

- Severity: warning
- Related task / requirement: 4.6 (injection-safe command line, launcher-record discipline of decision 9)
- Locations: `src/BellTower.Protocol/HookStartTask.cs:25` (`Recorded`), `:52` (`/run /tn "` + task + `"`); `src/BellTower/Setup/IntegrationSetup.cs:136-141` (removal uses the recorded value); `src/BellTower/Setup/HookStartTaskSetup.cs:36-40`
- Observation: `Recorded` accepts any record text that starts with `\` and is at most 256 characters, and the hook splices it inside quotes into the `schtasks` command line. A `"` in the record ends the quoted argument and adds options to `/run` (for example another `/tn`, or `/s`/`/u`/`/p` for a remote run). Removal passes the same value to `Unregister-ScheduledTask`, which is quoted safely but can name any other task of the user. The record lives in the user-writable data directory, the same trust level as the launcher record. The launcher record, however, gets a strict shape check (absolute, expected name, regular file, no reparse point), and the task record gets none. The value is fully determined by `TaskPath(root, folder)`, so nothing needs to be free text.
- Proposed improvement and benefit: accept a record only when it matches `^\\[A-Za-z0-9._-]{1,64}\\HookStart-[0-9A-Za-z]{16}$`, or simply equals `TaskPath(root)` for the default folder (tests can pass their folder). Treat anything else as `no-task-recorded` and report it in doctor. This closes the argument injection and keeps removal from touching a task Bell Tower did not create.
- Behavior-preservation risks: a test that records a custom folder must still match the pattern; doctor gains one state ("recorded task is not Bell Tower's").
- Evidence type: source observation
- Verification / measurement plan: unit tests for `Recorded` with a valid path, an embedded `"`, extra switches and another folder or name. Assert that `Run` never starts `schtasks` for an invalid record and that removal plans no action for one.
- Relationship: none

### ANALYSIS-4702: `--hook-start` makes a hook start fail silently against an older payload

- Severity: warning
- Related task / requirement: 4.6; decision 9 ("the tray's own start gate remains authoritative"), decision 10 fallback; rollback compatibility
- Locations: `src/BellTower.Protocol/TrayProcess.cs:245`; `src/BellTower.Launcher/Program.cs:23-26` (forwards all arguments to the selected payload); `src/BellTower/Cli/TrayCommand.cs:16-21`; `src/BellTower/Setup/IntegrationRefresh.cs:12` (a payload switch refreshes only the launcher record, not the immutable ingress)
- Observation: the hooks call the immutable ingress that setup provisioned, and the launcher runs whichever payload is selected. After a rollback or dev-build switch to a payload older than DEV-059, the newer ingress still appends `--hook-start`. The older tray's command parser does not know the option and, with System.CommandLine's default handling of unmatched tokens, exits with a parse error. The hook has already recorded `Started`/`started-via-shell`, because the process was created and resumed. Because of that recorded success the task fallback never runs, and every following hook repeats the same failing start every 30 s. The result is the T-2 symptom (no tray, sessions missing) with doctor reporting success.
- Proposed improvement and benefit: either have the launcher drop `--hook-start` when the selected payload's release contract does not declare it (the launcher already reads `release.json` for the capture gate), or carry the marker in a way an older tray ignores. An example of the second option is a launcher-consumed argument the launcher strips, or a marker file next to the attempt record that the tray consumes. Either keeps hook start working across a supported rollback.
- Behavior-preservation risks: the release contract gains one capability field; the fake launcher and `HookTrayStartTests` that assert the exact argument list change.
- Evidence type: source observation plus a hypothesis about the old parser's exit; not reproduced
- Verification / measurement plan: a packaged test that selects a payload built from `2e7f318` (or a fake payload that rejects unknown options), runs one Claude and one Codex hook with no tray, and asserts that a tray starts or that doctor reports the failure.
- Relationship: none

### ANALYSIS-4703: Removal drops ownership before the removal succeeds

- Severity: warning
- Related task / requirement: 4.6 (setup removal deletes the task), 4.7 (removable step, ownership rules); `docs/development.md` line 29 ("removal ownership is committed after extension actions succeed so failed removals can be retried")
- Locations: `src/BellTower/Setup/IntegrationSetup.cs:207-213` (`PlanProviderTitles` inserts the manifest change at index 0 and passes no `commitAfterActions`), `:136-141` (task record deleted as a file change); `src/BellTower/Setup/SetupPlan.cs:27-52` (file changes, then actions, then the commit); `src/BellTower/Setup/HookStartTaskSetup.cs:39` (`-ErrorAction SilentlyContinue`), `:42`; `src/BellTower/Setup/SetupAction.cs:24-33`
- Observation:
  - Provider titles: on `--provider-titles --remove`, the manifest without the entry is written first, and the settings file is restored afterwards. If that second atomic write fails (the provider rewriting `settings.json` or `config.toml`, or a lock), the value `1` or `[]` stays in the profile and is now unowned. A retry then reports the setting as user-made and leaves it, with no setup path to remove it.
  - Task: the record `hook-start/task.txt` is deleted in the file-change loop before the unregister action runs. The unregister script suppresses its own errors, so a failure is invisible, and the task is orphaned with no record that doctor or a later removal could find. A failed action would also surface through `SetupAction`'s VS Code-specific messages.
  - Unregistering leaves the empty `\ZeusApps.BellTower\` Task Scheduler folder behind.
- Proposed improvement and benefit:
  - For removal, pass the manifest change as `commitAfterActions`, as `Plan` does.
  - Unregister the task first, with an error check (`-ErrorAction Stop`, treating "not found" as success), and delete the record only after that succeeds.
  - Give the task its own action wording rather than the VS Code installer's.
  - Optionally remove the folder when it is empty.

  Failed removals become retryable, which the repository rule requires.
- Behavior-preservation risks: apply order is unchanged (ownership is still recorded before adding). `ProviderTitleStepTests` and the setup preview text change slightly.
- Evidence type: source observation
- Verification / measurement plan: a test with disposable settings in which the settings write fails during removal (a read-only file). Assert that ownership is retained and that a rerun removes the setting. A test in which the unregister action fails, asserting that the record is kept. For both providers.
- Relationship: none

### ANALYSIS-4704: The title decision trusts ownership alone, for every session at once

- Severity: warning
- Related task / requirement: 4.7; session-identity "Provider-owned terminal titles"; decision 11; capability-matrix honesty
- Locations: `src/BellTower/Setup/ProviderTitleControl.cs:11-22`; callers `src/BellTower/Hosts/TerminalTitleService.cs:28`, `:102`, `TerminalPresentationResolver.cs:16`, `WindowsTerminalFocusService.cs:16`, `WindowsTerminalNavigator.cs:14`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs:91-92`
- Observation: `Disabled(paths, provider)` is true whenever the manifest has any `ProviderTitles` entry for the provider.
  - It stays true after the user sets the key back or the provider rewrites its file. Removal itself then says "Kept user-edited … setting", yet Bell Tower keeps writing titles and claiming tab navigation.
  - It turns true immediately for sessions that were already running when the step was applied. Their provider read its configuration at start and keeps writing its own title, so the T-5 fight returns, and Windows Terminal navigation reports "tab or window" through a title that is being overwritten.
  - It ignores which file the entry names, so an entry for a `--home` other than the real profile counts.
  - It decodes `integration.json` on every title write, resolution and click.
  - Architecturally, Hosts now depend on `Setup`, and the manifest path is hard-coded a fourth time.
- Proposed improvement and benefit: make the fact "this session's provider title is disabled" a small IO/Core service, not a `Setup` call. Grant it only when three things hold:
  1. Ownership exists.
  2. The owned file at the recorded path still holds the value (one bounded read, cached by file write time).
  3. The session's process started after the step was applied. Record an `AppliedAt` in `OwnedProviderTitle` and compare it with the attachment's process start or `RegisteredAt`.

  Navigation, presentation and doctor then agree with what the provider actually does, per session.
- Behavior-preservation risks: the manifest schema gains an optional field (older manifests have none, so treat them as applied at an unknown time and grant only to sessions registered after the tray read them, or require one re-apply). Presentation fixtures that set the step need the value present in their disposable settings.
- Evidence type: source observation
- Verification / measurement plan: tests for (a) the step applied while a session runs (no title for it, a title for a new session), (b) the user resets the key (no title, window-only navigation, doctor says provider-owned), (c) an entry for another home; for Claude Code and Codex, and a mixed case.
- Relationship: ANALYSIS-4705 is the related restore-record defect

### ANALYSIS-4705: A color-only restore record later restores a blank title

- Severity: suggestion
- Related task / requirement: 4.7; session-identity restoration ("restored to its prior value")
- Locations: `src/BellTower/Hosts/TerminalTitleService.cs:47-57` (`previous = existing?.Previous ?? (colorOnly ? "" : ReadTitle)`); `src/BellTower/Hosts/AttachedConsoleTitleWriter.cs:31-39`
- Observation: a color-only write stores `Previous = ""` as a placeholder. If the step later becomes effective for the same live session (see ANALYSIS-4704), the first title write finds `existing.Previous` non-null (`""`) and does not read the real title. Restore then compares the current title with the one Bell Tower wrote and sets the console title to `""`, leaving the tab blank instead of giving back the shell's title.
- Proposed improvement and benefit: store `Previous` as null for a color-only record (or keep a separate "title captured" flag), and read the real title on the first titled write. Restoration then follows the spec in this transition.
- Behavior-preservation risks: the `TitleRestore` record format (keep a null `Previous` readable).
- Evidence type: source observation
- Verification / measurement plan: a fake-writer test: a color-only write, then the step becomes effective, a titled write, then restore. Assert that the original title comes back. One case per provider.
- Relationship: ANALYSIS-4704

### ANALYSIS-4706: The Claude decline watcher can lose its one publication

- Severity: suggestion
- Related task / requirement: 4.9 ("declined prompt clears the wait"; exactly-once); session-tracking "Declined Claude prompt"
- Locations: `src/BellTower/Providers/TranscriptEvidence.cs:118`, `:128-163` (scans oldest first, 5 s skew); `src/BellTower/Providers/ClaudeDeclineWatcher.cs:29-39`, `:46-83`; `src/BellTower/Core/SessionReducer.cs:63-66` (drops observations older than `TurnStarted`); `src/BellTower/IO/EventStore.cs:13-26` (`InvalidDataException`)
- Observation:
  - When a wait has no tool-use IDs, a rejection of the previous wait recorded within 5 s before the new wait's start matches. `DeclinedAt` returns the oldest match.
  - The watcher publishes it at that earlier time, and the reducer drops it as older than the turn. The watcher has nevertheless added the new wait's key to `published`, so the user's real decline of the new wait is never published. The wait then stays until the next prompt, which is the original T-10 symptom.
  - The background check enumerates the tick's `SessionState` objects directly. The Codex pump instead snapshots its checks on the UI thread in `BeginChecks`.
  - Only `IOException` and `UnauthorizedAccessException` are caught. `InvalidDataException` from `EventStore.Validate`, or `JsonException`, faults the task without a log, and it repeats every second.
- Proposed improvement and benefit:
  - Scan newest first.
  - Accept a rejection only at or after the wait's start (keep the skew only for matched tool-use IDs).
  - Key `published` by the decline time actually published rather than by the wait alone, so a newer rejection can still be published.
  - Capture the session fields needed on the UI thread, as the Codex pump does.
  - Catch and log the remaining expected exception types once per session.

  The publication then becomes reliably exactly-once for the wait the user actually declined.
- Behavior-preservation risks: a decline whose transcript timestamp is slightly earlier than the hook's wait time (clock skew without IDs) no longer clears. This is rare, because Claude Code usually supplies `tool_use_id`.
- Evidence type: source observation
- Verification / measurement plan: fixture test with two waits within 5 s, no IDs, and the first declined. Assert that the second decline still clears the second wait once. Add a test that a validation failure is logged and does not repeat unboundedly. Run a mixed Claude/Codex case.
- Relationship: none

### ANALYSIS-4707: Transcript tails are re-read in full every second, and long Codex turns lose their record

- Severity: suggestion
- Related task / requirement: 4.9; design decision 12 (bounded reads); performance
- Locations: `src/BellTower/Providers/TranscriptEvidence.cs:14`, `:27-47`; `src/BellTower/Providers/ClaudeDeclineWatcher.cs:14`, `:59`; `src/BellTower/Sessions/CodexPermissionPump.cs:28-33`, `:180-181`
- Observation: for every waiting Claude session (watcher) and every waiting Codex session without a runtime (pump), each 1 s tick reads up to 1 MiB, decodes it to a string of up to 2 MiB, and splits it. That is several large-object-heap allocations per waiting session per second while a person decides, which can take minutes. Most ticks see an unchanged file. Separately, Codex writes `turn_context` at the start of the turn. In a long agentic turn, the tool outputs before the approval can push that record past the last 1 MiB, so `HumanMustDecide` returns null and the wait stays provisional in exactly the turns where waiting matters.
- Proposed improvement and benefit:
  - Skip the read when the transcript's length and last write time are unchanged since the previous tick.
  - Cache the Codex result per session and turn once a record is found; the reviewer does not change within a turn.
  - Optionally, for the first check of a turn only, search backwards in bounded chunks (for example up to 8 MiB) for that turn's `turn_context`.

  This lowers steady cost to a metadata check and makes confirmation independent of turn length.
- Behavior-preservation risks: the cache must be keyed by turn ID and invalidated when the turn changes; reads stay bounded.
- Evidence type: performance hypothesis (allocation volume follows from the code; the `turn_context` distance in real rollouts is not measured)
- Verification / measurement plan: (a) count allocations or bytes read over 60 s with one Claude and one Codex session waiting, before and after the change; (b) measure the byte distance from `turn_context` to the `PermissionRequest` time in a few real long Codex 0.171.0 rollouts under isolated homes.
- Relationship: none

### ANALYSIS-4708: A self-check refusal keeps the tray down until someone runs doctor

- Severity: suggestion
- Related task / requirement: 4.6; design decision 10 ("diagnostic first, so a false trigger is visible rather than silently keeping the tray down"); spec "Shell start unavailable"
- Locations: `src/BellTower/Tray/TrayJobGuard.cs:38-58`; `src/BellTower/Protocol/HookTrayStart.cs:106-111`; `src/BellTower/Diagnostics/HookStartReporter.cs:16`, `:25-28`
- Observation: when a hook-started tray refuses its job, the hook has already recorded `started-via-shell`. The fallback therefore never runs, and every hook 30 s later repeats the same shell start and the same refusal, even when a working task is registered. The user sees no tray. Only `doctor` and the local log show it, and doctor shows "LatestRoute: started-via-shell" next to the refusal. The refusal record is never cleared, so an old refusal stays in doctor after later successful starts.
- Proposed improvement and benefit: in `TryStart`, when the refusal record is newer than the last attempt, go straight to the recorded task, or fail with `shell-route-refused` when none exists. Clear the refusal record when a tray takes the instance mutex, or show it in doctor only when it is newer than the latest started tray. A false trigger then cannot keep the tray down while a fallback exists, and doctor stays current.
- Behavior-preservation risks: none for the normal path; adds one record read on the start path (only after the mutex check fails).
- Evidence type: source observation
- Verification / measurement plan: tests with an injected kill-on-close flag. The first hook records the refusal; the next hook after the interval uses the task route. A later successful start clears doctor's refusal field. Run with a Claude hook and a Codex hook.
- Relationship: none

### ANALYSIS-4709: Doctor overstates Codex transcript evidence and has no Claude decline counterpart

- Severity: suggestion
- Related task / requirement: 4.9; AGENTS.md "keep the capability matrix and diagnostics honest"; parity
- Locations: `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs` (`transcript` count, `UnavailableWithoutRuntime`); `src/BellTower/Providers/ClaudeDeclineWatcher.cs:79-82`; `src/BellTower/Protocol/DesktopShell.cs:85-89`
- Observation:
  - `WithTranscriptEvidence` counts runtime-less Codex sessions whose identity has a transcript path, not sessions whose transcript produced a reviewer record. A missing, unreadable, drifted or out-of-window transcript is still counted as covered and subtracted from `UnavailableWithoutRuntime`, although the status text says unavailable applies to sessions with "no readable transcript".
  - The Claude decline adapter has no doctor field at all, so format drift in `toolDenialKind` would go unnoticed for Claude, while Codex has a counter.
  - A failed `OpenProcess` on the shell is reported as `shell-image-unverified`, which hides an access failure.
- Proposed improvement and benefit:
  - Count Codex coverage from the last read's result (record found, reviewer, or none), which the cache in ANALYSIS-4707 would hold.
  - Add a Claude field: sessions whose transcript is known, declines published, the last read failure code.
  - Report `shell-open:<error>` for an open failure.

  Doctor then states verified and unverified evidence the same way for both providers.
- Behavior-preservation risks: diagnostics schema only; `ConfirmationCoverageTests` change.
- Evidence type: source observation
- Verification / measurement plan: doctor tests for a Codex session with a path but no record (unavailable), with a record (covered), and a Claude session with a decline published; a shell-open failure test through the injectable reader.
- Relationship: none

### ANALYSIS-4710: Duplicated ownership copy and leftover interop declarations

- Severity: suggestion
- Related task / requirement: 4.6, 4.7; duplication
- Locations: `src/BellTower/Setup/IntegrationSetup.cs:37-47` and `:173-183` (identical `IntegrationOwnership` copy blocks); `src/BellTower.Protocol/TrayProcess.cs:19-21` (`TokenTypePrimary`, `SecurityImpersonation`, `TokenAssignPrimary`, each referenced only by its declaration); `src/BellTower.Protocol/TrayProcess.cs:30-70` and `src/BellTower.Protocol/HookStartTask.cs:82-118` (the same `StartupInfo`, `ProcessInformation`, `CreateProcessW` and `CloseHandle` declarations)
- Observation: the ownership copy is the one with a concrete risk. Every new ownership field must be added to both copies, or one planner silently drops that ownership on its next run. DEV-059 had to add `ProviderTitles` to both. The interop duplication is low-risk but already diverges in naming (`CreateProcessExW` with `EntryPoint = "CreateProcessW"`).
- Proposed improvement and benefit: add `IntegrationOwnership.Copy()` (or a `with`-based deep copy) and use it in both planners. Remove the three dead constants. Optionally move the shared process-creation interop into one internal file in `BellTower.Protocol`.
- Behavior-preservation risks: none if the copy stays deep for `Desktop` (currently copied through JSON round-trip).
- Evidence type: source observation
- Verification / measurement plan: existing setup tests; one test that a field added to the manifest survives both planners (round-trip through `Plan` and `PlanProviderTitles`).
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4600 | `head=2e7f318 … untracked=20/232a0d9e80a8` | `NavigationNotice.ShowsNotice` is `NavigationOutcome.NeedsNotice`. `TrayApplication.Navigate` shows `NoticeForm` for every such result, anchored to the strip or the Sessions window. The balloon, `SHQueryUserNotificationState` and `Decide` are gone | verified (source) |
| ANALYSIS-4601 | same | `NoticeForm.CreateParams` adds `WS_EX_TOPMOST`, `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE`, the `TopMost` property is no longer set, and `ShowWithoutActivation` is true. This avoids the framework's `SetWindowPos` without `SWP_NOACTIVATE` and the `FocusActiveControlInternal` path for `TopMost` forms | verified (source); the live probe that the focused terminal keeps the keyboard is still open in trial 6.1 |
| ANALYSIS-4602 | same | A missing key logs `navigation-session-missing` and shows the reason. `NavigationNotice.RunAsync` catches `IOException`, `UnauthorizedAccessException`, `Win32Exception`, `InvalidOperationException`, `JsonException` and `InvalidDataException`, then logs `navigation-failed` and states the reason. An exception type outside that list still escapes the `async void` handler, which is acceptable for unexpected faults. `FocusResult.Normalized()` (QA-5304) is applied to every result, and the log code accepts only known precisions | verified (source) |
| ANALYSIS-4603 | same | `CapabilityMatrix.cs:103` and `docs/compatibility.md:79` (Codex column) both read "the same stated reason and delivery as Claude Code for a partial result" | verified (source) |

## Assessment and limitations

Scope covered: the DEV-059 implementation of tasks 4.6 to 4.10 listed above, for Claude Code and Codex, by source inspection.

- The core of the tray start is sound: the parent-process start's handle hygiene, the shell verification against PID reuse and against other users and sessions, the environment built from the shell token, the elevation guard, and the process-stop guard.
- The remaining tray-start risks are at the edges:
  - the fallback blocks the hook (ANALYSIS-4700);
  - the task record is not validated (ANALYSIS-4701);
  - the new marker breaks hook start after a rollback, and the failure is recorded as a success (ANALYSIS-4702);
  - a self-check refusal is never routed to the fallback (ANALYSIS-4708).
- The provider title edits are safe and limited to one key. Two issues remain:
  - the removal order breaks the repository's retry rule (ANALYSIS-4703);
  - the runtime decision trusts ownership alone, so it can claim titles and tab navigation that the provider is still overwriting (ANALYSIS-4704, with ANALYSIS-4705).
- The wait evidence fails safe on drift, and timing never confirms. Exactly-once publication and per-tick cost need tightening (ANALYSIS-4706, ANALYSIS-4707), and doctor coverage is uneven between the providers (ANALYSIS-4709).
- The 4.10 corrections (ANALYSIS-4600 to 4603) are verified by source.

Not performed: builds, tests and format verification (QA owns them); no hook latency measurement; no elevated-hook, VS Code, Native AOT or other-build check; no real scheduled task registration; no live notice-focus probe; no reproduction of the version-skew case. The attempt to write the reserved report was refused; no files were written.

Decisions needed (orchestrator): triage of ANALYSIS-4700 to ANALYSIS-4710. For ANALYSIS-4704, decide whether "disabled through the step" means ownership only (current spec text) or ownership plus the setting being in effect for the session.
