# Compatibility and evidence

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
| Codex footer/title | Native `/statusline` supports Git/directory fields; no documented arbitrary custom colored footer renderer was verified. Managed titles show Bell Tower name plus branch/worktree | User confirmed Windows Terminal tab targeting. Native Codex `/rename` and Bell Tower identity remain independent |
| Session background | New managed launches apply colors before the agent starts; hooks carry the startup palette into new session identities. Resumes reapply saved palettes. Wrapper resets configured profile colors on exit; `-NoBackground` opts out | User confirmed immediate startup color for both Claude and Codex, including before Codex's first response |
| VS Code navigation | Registered terminal selection, instance isolation, expiry, creation-time check, explicit owning-window focus, active-terminal confirmation and acknowledgment | User confirmed correct window foregrounding and terminal selection after the fix. Duplicate-name/multiple-window edge cases are not separately specified |
| Windows Terminal navigation | Fresh console-title marker and UI Automation select a unique live tab, with verified-window fallback. Focus requests restore minimized windows only | User confirmed correct live tab selection, fullscreen preservation, reordered tabs and multiple windows. Exact split-pane focus is unavailable |
| Tray lifecycle/UI | Real silent tray process, detached-start/exit test, in-place timer updates, font/DPI layout checks, actual strip rendering, position clamp tests | User confirmed no flicker, timer hiding, three-dot dragging, tray survival after the starting tab closes, right-click dismissal and separate chips for parallel waits. Display/RDP resizing is explicitly deferred as nonblocking |
| Desktop setup | Reversible Claude/Codex and optional project profiles, explicit Windows Terminal home/project starting directories, migration from role profiles, PowerShell 5/7 module/imports, dynamic-title settings and packaged bridge | User confirmed startup, hook review, completion and removal of flicker; project-menu launches and the complete provider/host matrix remain open |
| Sound | Four bundled default event tones, optional classic chime/custom PCM WAV, bounded conversion, reason-aware serialized queue and silent sink | Bundled mapping and imports verified without playback; physical sound only when explicitly requested |

Hook test inputs are documented-contract fixtures, not recorded authenticated agent sessions. Native CLI tests exercise the actual built executable and isolated filesystem state. Node tests use a simulated VS Code API around the real bridge service. No test grants agent trust or changes personal client configuration.

PermissionRequest indicates that an approval flow is starting. Correlated operation resumption/tool output, a new prompt, or completion clears it. For Bash and apply_patch, command fingerprints omit approval-only descriptions and execution options that vary between hook stages; arbitrary tools still hash their complete arguments. Distinct newly observed blockers at the visible priority create fresh notification episodes even while another wait remains. A denial that produces no subsequent observed transition is not yet independently observable on every provider path. Question tools are observed through their local tool hooks; unsupported hosted/specialized paths are not inferred from chat prose.

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

Context7's requested outside-sandbox execution was unavailable under this session's policy; official documentation was used instead.


Shared Codex background-server launcher metadata is not considered a verified terminal binding. An explicit launch with --no-daemon enables per-session registration; ordinary shared-server hooks use conversation identity with unverified process/host attachment. Existing sessions are never restarted to change this mode. Windows Terminal window activation is enabled only when the owning process has one verified visible window; multiple or moved-window cases are unavailable until revalidated.

Desktop role functions add --no-daemon for local Codex automatically. Users select a terminal profile or invoke a PowerShell role function; bell-tower.exe is not required in the agent's parent chain. An already open agent or remote/shared-backend conversation does not acquire managed terminal presentation just because desktop setup ran.
