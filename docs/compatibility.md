# Compatibility and evidence

Fast hook follow-up (2026-10-23): both packaged providers now use a shared NativeAOT capture protocol and bounded background consumer. The [capture contract and measured evidence](hook-ingress.md) distinguish generated-shell trials from authenticated provider captures. Installed local versions were Claude 2.1.306, Codex 0.164.0 and Git Bash 5.2.37. At eight concurrent invocations, the first valid native trial measured medians of 115 ms for Claude and 83 ms for Codex; all 242 captures persisted and all completed below three seconds.

An isolated Claude 2.1.306 SDK CLI with an unreachable loopback model endpoint produced actual `attachment/hook_cancelled` timeout records and custom-main-agent input (`agent_type: pm`, no `agent_id`). The optional bounded timeout reader uses that retained schema. Codex 0.164.0 local exec and no-daemon terminal probes verified SessionStart timeout execution and a successful control, but the complete chat transcripts lack structured hook-result records. Doctor reports complete historical counts as unavailable for those verified version/source pairs, with the shared capture-health/native-status fallback. This narrow restriction, retained evidence and regression coverage are documented in the [probe record](../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/provider-timeout-probes.md); other modes/versions remain unverified.

The packaged lifecycle trial under `artifacts/update-acceptance/run-5422462fc6d943a3b06c8e6437082182` passed mixed fast capture during tray restart, version selection, immutable helper refresh, stopped-tray ingestion and compatible rollback. Its 0.1.7 baseline is current source compiled with a fixture version, not the original released 0.1.7. It used a disposable agent/backend and authenticated loopback runtime, with no real model connection or production configuration writes.

Recorded on 2026-10-21. An installed version is not a claim that all interactive scenarios have been acceptance-tested.

The user's testing in this conversation supplies live acceptance evidence; see [the reconciled record](live-acceptance.md). Missing formal fixtures or unchecked broad tasks do not erase confirmed startup, completion, badge/background, flicker and tab-navigation results.

| Component | Observed version | Evidence |
| --- | --- | --- |
| .NET SDK | 10.0.113 | dotnet --info; target net10.0-windows; build and tests executed |
| Claude Code | 2.1.289 | claude --version |
| Codex CLI | 0.163.0 | Bundled native executable --version |
| VS Code | 1.141.1 | code --version |
| Windows Terminal | 1.24.2607.10001 | File version of installed running WindowsTerminal executable |
| Git | 2.51.0.windows.2 | git --version; isolated repository/worktree tests |

## Coverage and fallbacks

| Surface | Implemented coverage | Remaining live evidence |
| --- | --- | --- |
| Claude events | Start/prompt/tool/permission/stop/failure/end, notification reasons, subagent observations and background snapshots | Real hook captures, approval denial/cancellation, and terminal interaction |
| Codex events | Documented lifecycle/prompt/permission/tool/subagent/interruption contracts | Real hook captures and installed-version question/denial/failure paths |
| Claude footer | Bundled ANSI name/accent chip with branch and worktree context | User confirmed the name badge and branch appear; linked-worktree rendering still needs live acceptance |
| Codex footer/title | Explicit setup adds native `thread-name`/`git-branch` reversibly. New local launchers use dedicated authenticated runtimes for live names | Isolated native TUI verified switches, native rename pause/resume, parallel sessions and a real worktree label/branch. Authenticated hook/full desktop matrix remain open; explicit standalone/remote sessions without owned runtimes cannot sync names |
| Session background | New managed launches apply colors before the agent starts; hooks carry the startup palette into new session identities. Resumes reapply saved palettes. Wrapper resets configured profile colors on exit; `-NoBackground` opts out | User confirmed immediate startup color for both Claude and Codex, including before Codex's first response |
| VS Code navigation | Registered terminal selection, instance isolation, expiry, creation-time check, explicit owning-window focus, active-terminal confirmation and acknowledgment | User confirmed correct window foregrounding and terminal selection after the fix. Duplicate-name/multiple-window edge cases are not separately specified |
| Windows Terminal navigation | Fresh console-title marker and UI Automation select a unique live tab, with verified-window fallback. Focus requests restore minimized windows only | User confirmed correct live tab selection, fullscreen preservation, reordered tabs and multiple windows. Exact split-pane focus is unavailable |
| Tray lifecycle/UI | Real silent tray process, detached-start/exit test, in-place timer updates, font/DPI layout checks, actual strip rendering, position clamp tests | User confirmed no flicker, timer hiding, three-dot dragging, tray survival after the starting tab closes, right-click dismissal and separate chips for parallel waits. Display/RDP resizing is explicitly deferred as nonblocking |
| Desktop setup | Reversible Claude/Codex and optional project profiles, explicit Windows Terminal home/project starting directories, migration from role profiles, PowerShell 5/7 module/imports, dynamic-title settings and packaged bridge | User confirmed startup, hook review, completion and removal of flicker; project-menu launches and the complete provider/host matrix remain open |
| Sound | Four bundled default event tones, optional classic chime/custom PCM WAV, bounded conversion, reason-aware serialized queue and silent sink | Bundled mapping and imports verified without playback; physical sound only when explicitly requested |

Hook test inputs are documented-contract fixtures, not recorded authenticated agent sessions. Native CLI tests exercise the actual built executable and isolated filesystem state. Node tests use a simulated VS Code API around the real bridge service. No test grants agent trust or changes personal client configuration.

PermissionRequest indicates that an approval flow is starting. Correlated operation resumption/tool output, a new prompt, or completion clears it. For Bash and apply_patch, command fingerprints omit approval-only descriptions and execution options that vary between hook stages; arbitrary tools still hash their complete arguments. Distinct newly observed blockers at the visible priority create fresh notification episodes even while another wait remains. A denial that produces no subsequent observed transition is not yet independently observable on every provider path. Question tools are observed through their local tool hooks; unsupported hosted/specialized paths are not inferred from chat prose.

Codex's permission hook precedes automatic review and does not prove that a human is needed. Managed Codex now checks the verified owning runtime's `waitingOnApproval` status; a fresh negative status suppresses stale hook waits even while a command is still executing. Human-confirmed approval has five-second grace, while unavailable/stale runtime verification and Claude use a fifteen-second hook fallback. The fallback can still alert during unusually slow automatic review or delayed results. Runtime status tests cover exact-thread scoping, initial-check flicker, expiry, failure, and silent restart restoration. The current real resumed runtime returned no human approval pending after automatic review. A native human-prompt end-to-end trial and Claude auto-review timing remain separate acceptance checks.

The original PoC source and badge implementation were unavailable on this machine. They are not dependencies.

## Documentation consulted

- [OpenAI hooks](https://learn.chatgpt.com/docs/hooks): hook input and lifecycle contracts; review/trust and asynchronous-delivery limits.
- [Codex commands](https://learn.chatgpt.com/docs/developer-commands): native footer/title customization.
- [Claude hooks](https://code.claude.com/docs/en/hooks): notification, tool-failure and background-task observations.
- [Claude status line](https://code.claude.com/docs/en/statusline): JSON stdin and ANSI output.
- [VS Code Terminal API](https://code.visualstudio.com/api/references/vscode-api#Terminal): shell process ID and terminal reveal.
- [Windows Terminal CLI](https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments): window and index-based targeting limitations.
- [UI Automation SelectionItem](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingselectionitem): selectable tab control pattern.
- [Windows Terminal general profile settings](https://learn.microsoft.com/en-us/windows/terminal/customize-settings/profile-general): explicit starting directories.
- [xterm.js input handler](https://github.com/xtermjs/xterm.js/blob/master/src/common/InputHandler.ts): OSC 10/11 color application and OSC 110/111 reset behavior; live-host validation remains separate.
- [SAPI Speak](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms723609(v=vs.85)): speech playback API.

The initial change used official documentation when outside-sandbox Context7 execution was unavailable. The session-title follow-up successfully consulted Context7 `/openai/codex` for hook response/native transport contracts and `/xoofx/tomlyn` for lossless TOML editing.

## Session-title follow-up evidence

The trial build was installed into the existing stable release directory on 2026-10-22, desktop setup refreshed the owned native footer and shell module, and the restarted production tray reported running. The current real Codex conversation's literal agent-run OpenSpec status operation selected its change through the installed hooks. This confirms the successful selection path; it does not complete the failed/background/provider capture matrix.

A subsequent isolated restart trial used a copy of the installed module with only its data root redirected. After the client and dedicated runtime exited, a fresh shell/profile runtime opened Codex and the native `/resume` picker restored `restart-retained-change · master` without another selection. An explicit metadata fixture supplied the initial association. A disposable user turn against an unreachable loopback-only model endpoint made the thread eligible for the picker and was interrupted; no real model service or personal credentials were used. The empty-thread trials showed that threads without user turns are omitted by the native picker. All disposable clients/runtimes and the isolated tray were closed afterward.

Supported literal OpenSpec selection forms and native footer setup have automated regression coverage. Provider hook examples remain documented-contract fixtures, not authenticated captures. Codex Bash `tool_response` may be a plain output string without an exit code; selection therefore requires matching successful OpenSpec JSON or a local create-success marker plus scaffold, rather than assuming every PostToolUse succeeded. Matching pre/post operation IDs isolate sessions and reject delayed older completions and replay.

The initial standalone probe did not establish an external name-update endpoint. After runtime expansion was approved, a dedicated authenticated loopback app-server and native TUI demonstrated live updates without restart. The production launcher/name service displayed `trial-change-one · master`, switched to `trial-change-two`, paused after an external native rename, and resumed on explicit takeover. A second managed session in the same project displayed `parallel-change` independently. A real fixture worktree displayed `worktree-change | worktree: worktree-footer · feature/footer`. A console-membership probe verified that the dedicated native backend and shell share a console.

These trials used isolated `CODEX_HOME`, dummy credentials, and disposable data/repositories under ignored artifacts. There were no model turns or authenticated tool-hook captures. Bell Tower registration/identity changes were supplied explicitly by the controller; the actual native TUI and production WebSocket/name service were used. No personal configuration or existing user session was changed. This does not establish the full Claude/VS Code/Windows Terminal matrix or authenticated OpenSpec tool-hook acceptance.

Final verification on 2026-10-22: Release build, formatting verification, all 242 .NET tests, all 8 VS Code bridge tests, and strict OpenSpec validation pass. The final managed-launch exit trial removed its dedicated runtime record/token before the shell returned. Normal Bell Tower detach is separately tested to leave a native runtime running.


Shared Codex background-server launcher metadata is not considered a verified terminal binding. An explicit launch with --no-daemon enables per-session registration; ordinary shared-server hooks use conversation identity with unverified process/host attachment. Existing sessions are never restarted to change this mode. Windows Terminal window activation is enabled only when the owning process has one verified visible window; multiple or moved-window cases are unavailable until revalidated.

Updated desktop functions use dedicated authenticated runtimes for new local Codex sessions and retain --no-daemon as an explicit/failure fallback. The interactive client remains a direct shell child. Existing agents and explicit remote/shared-backend conversations do not acquire managed presentation merely because desktop setup ran.
