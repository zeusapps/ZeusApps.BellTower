# Installation and operation

Both clients share [attachment health and bounded terminal refresh](attachment-health.md), including persisted unavailable status, the `attachmentEvidenceGraceMinutes` control and consumer compatibility requirements.

## Fast hook setup

New versioned packages include a dedicated native ingress for both providers. Setup provisions verified immutable bytes under the selected data directory and previews direct hooks. Regular hooks use async capture; SessionEnd uses fast synchronous capture. Restart an older monitoring tray with the selected version before enabling capture-v1. Agent sessions can remain open.

Before writing provider settings, setup runs an isolated native hook probe with a loopback fake model endpoint. Claude uses its configured Git Bash (`CLAUDE_CODE_GIT_BASH_PATH`) or the standard Git for Windows Bash. Codex 0.164.0 inherits the shell program selected for its turn; setup records and displays only a command form that produces a durable capture. The generated Codex PowerShell form delivered through PowerShell 7 in two tested environments. Native cmd fallback is also verified when both PowerShell directories are denied in process-local MXC with `PATH` limited to System32; the capture identifies the actual `cmd.exe` hook parent ([evidence](../openspec/changes/archive/2026-10-28-fix-codex-hook-command-shell/evidence/native-investigation-009.md)). Pinned source shows the local Windows 0.164.0 resolver selects PowerShell or cmd and has no Bash selection branch; external-host Bash and other versions/path-priority combinations remain unverified ([source review](../openspec/changes/archive/2026-10-28-fix-codex-hook-command-shell/evidence/analysis-008-source-checks.md)). The tested command-array form was rejected by Codex 0.164.0's config parser. Apply rechecks the selected form against the exact retained helper path before writing settings; a failure or shell drift leaves provider settings unchanged. Doctor repeats the isolated native check and reports whether the recorded shell still matches.

Ordinary setup preserves edits to owned handlers, including `timeout: 3` / `async: true`. Preview with `setup --migrate-edited-hooks`, then add `--apply` when ready to replace edited entries with backups. Unrelated hooks remain, and repeated migration does not duplicate handlers.

Use `doctor` for backlog, consumption lag, sanitized failure categories and per-provider hook delivery. `hookDelivery` shows configured owned hooks, last observed delivery, and `unverified` when settings exist but no delivery was observed. A known legacy quoted Codex command includes its owned count and a preview command for `setup --clients codex --migrate-edited-hooks`; review the plan and add `--apply` only to write backups and migrate. Doctor does not print hook commands or settings paths. Optional `doctor --transcript <file> --transcript-provider claude` reads a bounded tail without recording conversation content. The Codex control reports unavailable complete counts for verified 0.164.0 CLI modes, whose chat transcripts omit the tested timeout status; it keeps the common health fallback. Other version/mode pairs remain unverified. See [the protocol and evidence](hook-ingress.md).

## Install and enable

For normal desktop use, place the package in a stable directory and run one setup command from that directory:

```powershell
.\bell-tower.exe setup --desktop --apply
```

Omit `--apply` to preview the plan. Use `--clients claude` or `--clients codex` to install only that provider. Desktop setup configures provider hooks, the bundled Claude status line when selected, PowerShell session functions, profiles in detected VS Code/Windows Terminal settings, and the packaged VS Code focus bridge when a standard VS Code installation is present. Existing unowned Claude status lines and conflicting terminal profile names are reported before any files change.

For ordinary native `claude` or `codex` commands, run `setup --host-bridge --apply`. This installs the selected global hooks and requests the optional VS Code bridge without creating terminal profiles or importing shell functions. Use `--clients claude` or `--clients codex` to select one provider. Setup configures the bridge with the selected executable and data directory and records their prior settings for removal. It preserves later edits to those settings. Review and trust the installed hooks through each provider's normal trust screen. Remove one provider with `setup --clients claude --host-bridge --remove --apply` or the equivalent Codex command; the shared bridge and its settings remain while another native provider or desktop profile uses them. Removing the last consumer restores unchanged owned settings and removes only a bridge Bell Tower installed. This configures hooks and bridge transport only: it creates no profiles and does not change terminal titles or colors by itself. Codex asks to trust changed hooks again: open `/hooks` after every setup that changes them and trust the new definitions, or the Codex hooks stay inactive. See "Plain claude and codex" below for what native sessions get.

Open a new terminal tab and select **Claude** or **Codex**. The profiles invoke `Start-ClaudeCode` or `Start-Codex`; those functions invoke the native agent directly. Bell Tower is a separate tray/hook process and is not the agent's parent. The tray starts automatically. Set a session name with `-Name`, or edit it in the tray's Sessions window.

Generic Windows Terminal profiles explicitly start in the user home directory, avoiding the shell's System32 default. Add project profiles with one command:

Replace `<project-directory>` with the absolute path to the checkout you want to register.

```powershell
.\bell-tower.exe setup --desktop --project '<project-directory>' --apply
```

This adds **Claude (ZeusApps.BellTower)** and **Codex (ZeusApps.BellTower)** to both detected terminal hosts. The functions receive `-Directory` and the agent starts in that project; Windows Terminal also stores the explicit starting directory. Repeat with another path to add another project. Existing registrations persist across setup runs. Projects must have distinct folder names. `setup --project PATH --remove --apply` removes only the project's unchanged owned profiles and leaves provider hooks and generic profiles installed. Full `setup --remove --apply` removes all unchanged owned profiles. No files or managed instructions are written into the project itself.

Rerunning desktop setup upgrades the earlier PM/Developer/QA/APM menus to these two profiles. It removes unchanged owned role profiles, restores profiles adopted from an existing configuration, and preserves user-edited entries with a note. The old function names remain available for existing scripts and launch normally. Their role names are now ignored: Bell Tower has no session role, and they never selected a model, prompt, or agent mode. The APM label came from the original PoC launcher list and has no defined behavior here.

Functions can also be called from a shell:

```powershell
Start-ClaudeCode -Name 'session attention' -Change 'build-standalone-session-attention'
Start-Codex -Name 'codex integration' -Silent
Start-Codex -Directory '<project-directory>' -Name 'tab navigation'
# Optional native agent arguments:
Start-Codex -AgentArguments @('--sandbox', 'read-only')
```

The functions resolve agents through PowerShell's command lookup, including native executables and npm shims. `-Executable PATH` selects a specific executable or external script. New local Codex sessions use a dedicated authenticated runtime; explicit `--no-daemon` or unavailable runtime support retains standalone attachment. Explicit remote connections retain unverified terminal ownership. Installed support is based on Codex 0.163.0.

The shell module is under the chosen Bell Tower data directory. Managed imports are added to both PowerShell 7 and Windows PowerShell current-user/all-hosts `profile.ps1` files with `-NoClobber`. Existing functions are not overwritten; if personal launcher definitions take precedence, migrate those deliberately before testing the generated profiles. No execution policy or provider hook trust is bypassed. Open Codex `/hooks` to review/trust the installed definitions. Reload VS Code when convenient to activate a newly installed focus extension; setup does not restart the editor or its sessions. After updating the executable, rerun desktop setup and open a new shell tab to load the updated module/runtime integration.

Once setup is installed, normal use needs no Bell Tower commands: open the **Codex** profile and use `/resume` as usual. New terminal tabs load the integration automatically after terminal or machine restarts. The conversation's saved change/title, manual override, palette, and native synchronization ownership return without selecting the change again. Existing standalone processes need to exit before a fresh profile can supply the updated runtime; `/resume` inside such an old process does not upgrade it.

Matching existing VS Code profiles are adopted reversibly: `overrideName` is removed, and the tab-title template uses `${sequence}`. Windows Terminal profiles allow application title updates. While a managed session is live, a separate helper validates shell and agent process creation times and updates only their shared console's title from Bell Tower's identity. Renames preserve palette and attention episodes. The shell restores its original title after marking the attachment ended, preventing late title updates.

New managed sessions prepare a palette and apply a random dark background with a readable light foreground before invoking the native agent. The first accepted hook transfers that palette to a new conversation's identity. Resumed conversations retain their saved palette, which the normal presentation helper reapplies after registration. This avoids waiting for Codex's first activity before coloring the terminal. Bell Tower's brighter chip accent comes from the same palette. On agent exit, the wrapper resets this terminal's foreground/background to its configured profile defaults with OSC 110/111. It does not capture arbitrary preexisting runtime color overrides: use `-NoBackground` when those must be preserved. Existing sessions launched with older modules do not acquire background ownership retroactively. Global terminal themes and other consoles are not edited.

The task name uses **Bell Tower title > native user title > pinned change, active work, branch or ticket subject > directory** for both Claude Code and Codex (see [session identity](session-identity.md)). Successful edits, patches and literal new-change commands update only the originating session, including later change switches; read-only inspection never renames it. Codex's chat name is synchronized as before; Claude Code's native `/resume` title is read (an explicit `/rename` wins) but not written, which is unfinished work deferred to `sync-native-session-titles` and not a provider restriction. `Use Bell Tower name` (Sessions, or `rename --use-bell-tower-name`) returns either client from a native title to the automatic name. Edit **Sessions > Name** for a manual override; clear it to reveal the latest association. Chips use one compact horizontal row: **name · reason · HH:mm:ss**. Branch/worktree details remain in the tooltip and overview; managed tab titles and Claude's badge include available Git context. Color and attention episodes stay unchanged when these fields update. See [session titles](session-titles.md) for supported operations and current Codex limitations.

The time measures elapsed waiting for the displayed reason, starting when that wait begins. It is not session runtime or time since clicking the chip. Completion starts its clock once foreground/background work is eligible to finish; its chip appears after the 20-second grace and initially shows about `00:00:20`. A different higher-priority reason uses its own wait start. Naming or Git metadata updates do not reset the timer.

Permission requests are provisional until a supported signal shows that a person is needed. Claude's `permission_prompt` notification confirms a request and is eligible on arrival (the provider already waited about 6 seconds as documented, which counts toward the 5-second human-wait grace); managed Codex sessions confirm through the verified owning runtime's human-approval status, with the 5-second grace from the first report. Automatic review, already-running commands and requests that resolve automatically stay silent however long they take, and there is no timing-based confirmation: requests without a supported signal (for example Codex sessions without a verified owning runtime or a failed status read, or Claude sessions in which no permission prompt notification has been observed, as when `CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS` turns them off) are never confirmed and raise no sound, reminder or escalation, but a request pending 15 seconds shows a visibly unverified \"possibly waiting for approval\" chip until it resolves, is dismissed, or the turn stops or ends; `bell-tower doctor` (`confirmationCoverage`) lists such sessions as unavailable and reports a hook ingress image older than this behavior (rerun setup). Question dialogs stay questions for both clients. Duplicate events do not restart grace, questions and errors remain immediate, and already-due waits restore silently after tray restart.

After a chip click successfully navigates to its terminal (or the supported window fallback), the chip stays visible with its name and reason, while its timer and trailing separator disappear. Failed navigation leaves the timer visible. This presentation preference persists for that wait across tray restarts; a new attention episode gets a timer again. It does not resolve the provider's wait, dismiss the chip, or change its underlying timestamps. Navigation from the overview or CLI does not hide chip timers. Right-click still dismisses the current chip.

A distinct new blocker at the displayed priority starts a fresh notification episode, even if an older wait has not been correlated with its resolution. This restores its timer and permits a new sound; repeated observations of the same unresolved blocker retain the episode and remain quiet. Notification eligibility uses the new episode's timestamp independently of older pending waits, while already-due episodes still restore silently. The three vertically centered dots at the strip's left are its drag handle.

Codex's native footer supports `thread-name` and `git-branch`. Explicit `setup --status-line` or desktop setup adds those items to `config.toml`, preserving existing items and unrelated TOML text; removal restores unchanged owned values and preserves later user edits. A sealed inline `tui` table without a footer key must have one added first. Native styling is retained. New local **Start-Codex** launches use dedicated authenticated runtimes for live names. Existing sessions, explicit remote connections, and explicit `--no-daemon` launches retain prior behavior. Native `/rename` pauses automatic writes; **Use name in Codex** resumes explicitly. `list` includes the derived name and actual sync state. See [session titles](session-titles.md) for lifetime/fallback details. [OpenAI commands documentation](https://learn.chatgpt.com/docs/developer-commands)

`setup --remove --apply` removes the selected owned hooks, badge, imports, and unchanged generated profiles, restores adopted entries and settings, and removes a focus extension installed by this setup. User-edited terminal profiles/settings are preserved and reported; helpers needed by retained profiles remain available. Private backups and ownership metadata are stored under the Bell Tower data root.

## Plain claude and codex

After `setup --host-bridge --apply` (and trusting the hooks in Codex through `/hooks`), start `claude` or `codex --no-daemon` in a Windows Terminal tab or a VS Code terminal as usual. Bell Tower adopts the session from its first hook with no wrapper and no launch flag; keep the tray running. Hook delivery and trust still decide whether a session appears: run `bell-tower doctor` to see whether hooks have been observed.

- **Click navigation.** In Windows Terminal a click brings the owning window forward (window only) and shows a short balloon saying the tab could not be identified while the native-presentation setting is off (with it on, which is the default, the click selects the session's tab); in VS Code it reveals the registered terminal. When the window cannot be verified, for example when one Terminal process owns several windows, the balloon states why.
- **Tab title and color.** The tray menu item **Title and color native sessions** is on by default (switch it off in the tray) and applies to all adopted direct sessions of both providers (plain `claude`, `codex --no-daemon`). While it is on, which is the default, the session title and its identity palette color are written after the first accepted hook, which can be at the first prompt rather than before the client starts, and a Windows Terminal click selects the session's tab. It applies to already running sessions without a restart. Turning it off, or the session ending, gives back the prior title and resets the colors to the terminal default (OSC 110/111) on the recorded shell only; that reset is assumed to work in both hosts until the user-operated trial confirms it. A nested agent started inside a live Bell Tower managed launch keeps the launch's presentation or opt-out: it gets no native title or colors in the launch's shell and resets nothing when it exits. There is no pre-launch coloring for native sessions: that remains a feature of Bell Tower launches (`Start-ClaudeCode`, `Start-Codex`), which also keep their own opt-outs.
- **Plain default `codex`** (the shared backend) is tracked and alerts, its names sync and its permission status is read, but it gets no navigation, title or color, and the setting does not apply. That is an unfinished item by user scope decision 2026-11-13, not a provider restriction. For navigation and color with Codex use either the Codex profile (`Start-Codex`) or `codex --no-daemon`.
- `bell-tower doctor` reports the setting state and the navigation precision per provider under `nativeSessions`.

The commands below remain available for hook-only/custom installations and troubleshooting; the normal profile workflow does not require `bell-tower launch`.

1. Build/package using scripts/package.ps1, or extract a prepared Windows x64 package into a stable directory such as %LOCALAPPDATA%/Programs/ZeusApps.BellTower.
2. Add the executable directory to your user PATH, or invoke its full path. Data stays under %LOCALAPPDATA%/ZeusApps.BellTower by default.
3. Run bell-tower setup --clients both --status-line to preview affected paths. Select --clients claude or codex when only one agent is wanted.
4. Run the same command with --apply to install the hooks and optional Claude badge. An existing unowned status line is a conflict; omit --status-line to keep it.
5. Restart the agent clients and review their normal trust prompts. In Codex inspect /hooks and trust the exact generated hook definitions.
6. For VS Code, install the bundled VSIX using code --install-extension PATH_TO_VSIX. Set bellTower.executablePath to the executable's stable path and reload the window. With a custom data root, set bellTower.dataDirectory too.
7. For a custom hook-only installation, start the tray with bell-tower start. The optional bell-tower launch command is also available for explicit process/host registration; desktop role functions provide registration without using that executable as the agent launcher.

For Codex per-session attachment, pass its explicit --no-daemon option after --. Shared-daemon hook delivery (plain default `codex`) is tracked by conversation ID without trusting inherited launcher metadata or terminal ownership; navigation, title and color are not provided in that mode (unfinished by user scope decision 2026-11-13, not a provider restriction), so use the Codex profile or `--no-daemon` when you want them.

Normal provider launches are observed once installed hooks emit events. Launch registration improves process-lifetime tracking. Exact host navigation requires a verified host registration; it remains unavailable when ancestry cannot establish ownership.

Use bell-tower doctor to see limitations and whether hooks have been observed. A historical observation does not prove that every hook remains trusted after later edits. See compatibility.md for the difference between implemented contracts and live acceptance.

After updating the VSIX, run **Developer: Reload Window** in VS Code when ready so the new bridge is loaded; setup never restarts VS Code or an agent. A bridge that predates window verification stays loaded until then and its chips read `terminal only`: the terminal is revealed but the window is not claimed as brought forward. The new bridge records which top-level window is its own only when its window gains focus (the registration keeps `window: null` until then), so focus each VS Code window once after reloading. A window that is already focused when its bridge loads becomes verifiable on its first keyboard or mouse interaction in it (the initial focused flag alone is never trusted, because a new window can report focus before Windows makes it the foreground window). A window in which more than one top-level window has been observed for the same editor instance, such as a floating editor window, publishes no window record and reads `terminal only`. This state is permanent for that editor window: nothing re-enables it, the bridge publishes the reason (`windowUnavailable: multiple`), and the chip tooltip, failed-click message and `doctor` tell you to run **Developer: Reload Window** to re-verify. A registration without that reason is simply not yet observed (focus the window once). Floating-window focus behavior is unverified and left to the live Windows 11/RDP check. `bell-tower doctor` lists each registered window and each live VS Code session as verified, partial (older bridge, the same state the setup note calls terminal only) or unverified with the reason, identically for Claude Code and Codex sessions. Sessions whose shell or agent process has ended are reported as `session process has ended`. A chip click then restores a minimized owner only (visible maximized and fullscreen windows keep their state), requests the foreground, and succeeds only when Windows confirms that exact window is in the foreground and the owning bridge confirms the exact terminal reveal (panel or editor area). If Windows refuses the foreground request, or the window cannot be verified (older bridge, never-focused window, two bridges claiming one window, changed owner), navigation reports failure and the chip's wait and timer stay unchanged. Editor active-terminal state and window focus do not prove keyboard input focus, which the public VS Code API cannot observe.

Windows Terminal chip navigation first focuses the verified owning window while preserving its visible show state. A disposable helper validates the live agent and shell, temporarily assigns their console a random title marker, and uses UI Automation to select the unique tab showing that marker. The original title is restored in a finally block. No stored tab number, friendly session name, keyboard input, or `wt` command is used. Reordered tabs are resolved again on each click; missing, ambiguous, inaccessible or hidden tab headers fall back to the verified window. If a different split pane supplies the tab title, the marker may not be visible and this fallback applies. Exact split-pane focus and moved/multiple-window ownership remain unavailable. A timed-out UI Automation helper is stopped and title repair is scheduled; the tray remains responsive. Only minimized windows receive a restore request. Actual Windows Terminal fullscreen and tab behavior still needs desktop acceptance.

## Private state and configuration

config.json contains voice and muted booleans, nativePresentation (default true; a saved false is kept; the tray item "Title and color native sessions") and updates.enabled (default true). Changes are reloaded live. Disabling updates.enabled suppresses automatic network checks/notices; explicit update checks remain available. Position and identity files persist independently of the executable directory. Events, state, attachments, host registrations, and focus requests are local to the chosen data root. Logs are capped below 256 KB and contain diagnostic codes, not prompts/transcripts/tool payloads.

The default notification tones are bundled inside the app: doorbell for permission, magic ring for questions, interface sound for completion, and fail sound for errors. No Downloads directory is needed. More than two queued notifications use the highest-priority reason's tone and the aggregate waiting phrase. Voice remains independently configurable.

**Notification tone > Choose WAV…** selects one custom tone for all reasons. Selection copies a normalized WAV into Bell Tower's private data and reloads it live; it does not play a preview. **Default event tones** restores the bundled mapping; **Classic chime** selects the original 880 Hz, 400 ms chime. Existing custom selections remain selected across upgrades. Use **Test sound** only when ready to listen; with defaults it uses the completion tone.

The CLI offers the same selection without playback:

```powershell
.\bell-tower.exe tone --file (Join-Path $env:USERPROFILE 'Downloads/tones/mixkit-doorbell-single-press-333.wav')
.\bell-tower.exe tone --reset
.\bell-tower.exe tone --chime
```

Mono/stereo PCM WAV files in 8/16/24/32-bit format are accepted and normalized to 16-bit PCM, including extensible PCM WAVs. Source and converted files must each fit within 16 MB; duration is limited to 30 seconds. A file that would exceed the limit after conversion is rejected before saving the selection. Missing or invalid custom tones fall back to the bundled tone for that reason. Downloads are not needed after a tone is copied, and source files are not modified. See assets.md for bundled filenames and icon generation details.

Every CLI command accepts --data-dir PATH; BELLTOWER_DATA_DIR supplies the same override. Use the same data directory for tray, setup, hooks, launchers, and VS Code.

Setup plans all selected client changes before writing, checks concurrent edits, backs up original files under integration-backups, and records owned hook commands in integration.json. Backups can contain private user settings; keep them local. Rerun setup to refresh owned entries. Removal is `bell-tower setup --clients both --remove --apply`; selected-provider removal also releases that provider's native bridge ownership while retaining the shared bridge for any remaining native provider or desktop profiles. Unrelated hooks and edited settings are preserved. An interrupted multi-file setup can be rerun; inspect the manifest and backups if a write fails.

## Legacy import

Import a known badge file explicitly, binding it to the intended provider/session ID:

```powershell
bell-tower import --badge PATH_TO_BADGE.json --provider claude --session SESSION_ID
bell-tower import --config PATH_TO_OLD_CONFIG.json --position PATH_TO_OLD_POSITION.json
```

Original files are read only. There is no background scan or permanent dependency on ~/.claude/ or TEMP badge directories.

## Manual update and rollback

For a versioned installation use **Check for updates / Install update** in the tray or `bell-tower update --check`, `bell-tower update`, then `bell-tower update --status` after the coordinator finishes. The root launcher stays at its registered stable path; a new payload is selected without overwriting files used by active runtimes. The tray reconnects to the same data directory while agents, runtimes, and terminal hosts continue. Source builds and legacy executable-only installations receive manual migration guidance.

Failed activation attempts restore the prior selection and monitoring when possible; data is not rolled back. Keep recovery metadata and inspect status when recovery is incomplete. Migrating a legacy executable at the same path, or replacing the stable launcher itself, may require a one-time maintenance window after sessions finish naturally. See [release/update operations](releases.md) for migration alternatives, retained versions, integration refresh, compatibility, and manual recovery. Never terminate agents or terminal hosts to update Bell Tower. Maintainers who test unreleased local builds use the separate, marked dev channel described in [development builds](development.md#development-builds).

## Verification

Use bell-tower start --silent with an isolated --data-dir for automated/manual fixtures. The CLI preview command renders the real strip into a PNG without sound. Test sound is an explicit action in the tray or bell-tower test-sound; normal validation uses a silent sink. Do not run physical sound tests until requested.


Offline validation can use dotnet restore --locked-mode --ignore-failed-sources -p:NuGetAudit=false and scripts/package.ps1 -Offline. This uses cached dependencies and does not perform a vulnerability audit; normal network-enabled restore keeps the default auditing behavior.
