# Implementation handoff: retain shared desktop tab title

## Correction

QA-100 and ANALYSIS-100 identified that removing one desktop provider restored `terminal.integrated.tabs.title` even while another provider or project profile remained. The VS Code profile editor now retains the managed title while desktop provider/project ownership or a retained user-edited VS Code profile remains. It restores the prior unchanged value after the last desktop consumer leaves. Native-only setup still does not manage this presentation setting.

## Regression coverage

- Claude-first and Codex-first desktop removal retain the title until the second provider leaves, then restore the prior personal value.
- A later user edit to the title survives partial and final provider removal.
- A user-edited profile retained during removal keeps the tab-title setting available.
- Removing one project retains the title while another project and generic profiles remain; final desktop removal restores the prior value.
- Native-only bridge setup continues to assert that no tab-title setting is added.

## Checks

- `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~DesktopSetupTests|FullyQualifiedName~ProjectProfileTests|FullyQualifiedName~NativeHostSetupTests" --verbosity minimal` — passed, 28 tests.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include src/BellTower/Setup/TerminalProfilesEditor.cs tests/BellTower.Tests/Setup/DesktopSetupTests.cs tests/BellTower.Tests/Setup/ProjectProfileTests.cs` — passed.

QA-100 and ANALYSIS-100 await independent verification. No provider trust, extension action, or live host operation was performed.
