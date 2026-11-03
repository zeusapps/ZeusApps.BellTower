# Implementation handoff: native hook and host bridge setup

## Scope

Implemented the independent setup surface for task 5.1. The `setup --host-bridge` option installs the selected global hooks and optionally provisions the VS Code bridge without terminal profiles, PowerShell imports, or the Claude status line. It is mutually exclusive with desktop, project-profile, and status-line options.

Native bridge ownership is persisted separately from desktop profile ownership. Bridge provisioning is shared, so removing native setup retains the bridge while desktop profiles still own it. `setup --host-bridge --remove` removes the selected providers' Bell Tower hooks and releases native bridge ownership while preserving existing desktop profiles and imports. Existing desktop setup and its removal continue to use their prior profile behavior.

## Evidence

- Isolated setup preview/apply/repeat/remove passed for Claude, Codex, and both providers.
- Both installation orders passed: desktop then native bridge, and native bridge then desktop.
- Claude-only removal retained Codex hooks, desktop profile settings, and PowerShell imports.
- A legacy ownership manifest without native bridge metadata loaded and retained its desktop bridge ownership.
- CLI help exposed the new option. Invalid combinations with desktop, status-line, and project-profile options failed before writing setup state.
- Existing aliases/personal PowerShell function fixtures and unrelated provider settings remained unchanged.

## Checks

- `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~NativeHostSetupTests|FullyQualifiedName~NativeSetupCliTests" --verbosity quiet` — passed, 12 tests.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include src/BellTower/Cli/SetupCommand.cs src/BellTower/Setup/IntegrationOwnership.cs src/BellTower/Setup/IntegrationSetup.cs src/BellTower/Setup/DesktopSetup.cs tests/BellTower.Tests/Setup/NativeHostSetupTests.cs tests/BellTower.Tests/Cli/NativeSetupCliTests.cs` — passed.

## Acceptance limits

The checks used isolated settings and home directories. They did not trust the hooks in a native provider, launch the VS Code extension installer, or perform a live host trial. These remain required for task 5.1 acceptance; fixture success alone does not complete the task.
