# Bell Tower

A personal Windows tray app that alerts when Claude Code or Codex needs attention in VS Code terminals or Windows Terminal. Session names and colors connect the terminal badge, attention strip, and session overview. It runs independently of WorkLog, OpenSpec, and personal Claude mods.

## Build and run

Requires the SDK in global.json for development. Packaged Windows x64 releases include the runtime.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
node --test vscode/service.test.js
./scripts/package.ps1
```

Run the executable in artifacts/releases/bell-tower-0.1.0-win-x64:

```powershell
./bell-tower.exe setup --clients both --status-line
# Review the displayed paths, then explicitly apply:
./bell-tower.exe setup --clients both --status-line --apply
./bell-tower.exe start
./bell-tower.exe launch codex --name "codex support" --role DEV -- --no-daemon
```

The launch command can use --executable PATH when an agent is not on PATH. Put agent arguments after --. Setup preserves unrelated hooks and refuses to replace an existing unowned Claude status line. Restart agents after setup and complete their normal trust flow; Codex requires reviewing /hooks. No personal configuration is installed by the build or tests.

See [installation](docs/installation.md), [architecture](docs/architecture.md), and [observed compatibility](docs/compatibility.md). Live agent/host acceptance is tracked separately from automated contract tests.

## Attention behavior

- Completion waits 20 seconds; observed permission, question, and terminal failure waits are actionable immediately.
- Background subagent/workflow/monitor activity suppresses completion, while foreground questions remain visible.
- Left-click requests focus. Right-click dismisses the current wait episode.
- The tray Sessions overview includes working sessions and allows manual name, change association, and role editing.
- Branch and linked-worktree names are supplementary context, shown in detail and tooltips.
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

MCP identity tools, WorkLog integration, per-repository instruction setup, and release/update automation are deferred in the change's [handoff](openspec/changes/build-standalone-session-attention/handoff.md).


