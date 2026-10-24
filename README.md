# Bell Tower

A personal Windows tray app that alerts when Claude Code or Codex needs attention in VS Code terminals or Windows Terminal. Session names and colors connect the terminal badge, attention strip, and session overview. It runs independently of WorkLog, OpenSpec, and personal Claude mods.

## Build and run

Requires the SDK in global.json for development. Packaged Windows x64 releases include the runtime.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
node --test vscode/service.test.js vscode/focus.test.js
./scripts/format.ps1 -Verify
./scripts/package.ps1
```

Download a [Windows release](https://github.com/zeusapps/ZeusApps.BellTower/releases), or run the root executable in artifacts/releases/bell-tower-0.1.8-win-x64 after packaging:

```powershell
./bell-tower.exe setup --desktop --apply
```

Then open a new **Claude** or **Codex** profile from VS Code or Windows Terminal's menu. PowerShell invokes the agent directly and starts Bell Tower separately. You can also run `Start-Codex -Name 'codex support' -Silent` from a new shell. Omit `--apply` from setup to review the files and extension action first.

Add profiles for a project with `./bell-tower.exe setup --desktop --project 'D:\Code\ZeusApps.BellTower' --apply`. This adds **Claude (ZeusApps.BellTower)** and **Codex (ZeusApps.BellTower)** with that working directory. Generic Windows Terminal profiles start in your home folder.

Session functions accept `-Executable PATH` and `-AgentArguments @(...)`. The optional `bell-tower launch` command remains available for custom workflows. Setup preserves unrelated hooks and refuses to replace an existing unowned Claude status line. New agent sessions need their normal trust flow; Codex requires reviewing /hooks. No personal configuration is installed by the build or tests.

See [installation](docs/installation.md), [architecture](docs/architecture.md), and [observed compatibility](docs/compatibility.md). See the [validation record](docs/validation.md) for completed checks and the remaining live agent/host acceptance work.

Code organization and formatting follow the [development conventions](docs/development.md), using WorkLog's command-per-class approach with System.CommandLine.

## Attention behavior

- Completion waits 20 seconds. Managed Codex permissions use verified human-approval status with a 5-second grace; other permission paths use a 15-second debounce. Questions and terminal failures remain immediate.
- Background subagent/workflow/monitor activity suppresses completion, while foreground questions remain visible.
- Left-click requests focus; successful chip navigation hides only its timer for the current wait. Right-click dismisses the chip.
- The tray Sessions overview includes working sessions and allows manual name and change association editing.
- Successful edits, patches and literal new-change commands update each Claude Code or Codex session's title independently; read-only OpenSpec inspection never renames a session. Bell Tower titles and native user titles take precedence. [Session identity](docs/session-identity.md) covers evidence tiers, history recovery, checkout, color/reset and legacy import, native names (Codex sync shipped; Claude's native title write is unfinished, deferred to `sync-native-session-titles`) and provider limits; the [capability matrix](docs/compatibility.md#session-identity-capability-matrix) states what is supported, restricted or unverified per client.
- Branch and linked-worktree names are supplementary context, shown in detail and tooltips.
- Chips show `name · reason · HH:mm:ss` in one row. Time is elapsed waiting for that reason, including the completion grace.
- Bundled default tones distinguish permission, question, completion and error. Choose a custom WAV or the classic chime from the tray, or restore event tones with `bell-tower tone --reset`.
- Startup restores existing waits silently. Voice can be disabled independently of chimes; Mute suppresses all alert audio.

Audio tests use silent substitutes. Test sound only plays when explicitly invoked in the running application.

## Useful commands

```powershell
bell-tower doctor
bell-tower list
bell-tower rename --session "codex:SESSION_ID" --name "working name" --change "change-name"
bell-tower focus --session "codex:SESSION_ID"
bell-tower toggle-voice
bell-tower exit
```

Use --data-dir PATH or BELLTOWER_DATA_DIR for an independent data directory. The default is %LOCALAPPDATA%/ZeusApps.BellTower. Keep data separate from the executable installation.

The installed tray checks for stable releases automatically. Use its **Install update** action or `bell-tower update`; check the eventual outcome with `bell-tower update --status`. Compatible updates restart the tray and reconnect to running sessions while agents and their existing runtime processes continue. See [release/update operations](docs/releases.md) for verification, rollback, and the one-time legacy migration.

MCP identity tools, WorkLog integration, and per-repository instruction setup remain deferred in the archived change's [handoff](openspec/changes/archive/2026-10-21-build-standalone-session-attention/handoff.md).
