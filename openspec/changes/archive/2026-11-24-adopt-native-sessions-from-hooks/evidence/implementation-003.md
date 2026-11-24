# Implementation handoff: shared bridge ownership corrections

## Corrections

- QA-001: VS Code bridge settings now manage `bellTower.executablePath` and `bellTower.dataDirectory` independently of terminal profile edits. Both native and desktop setup use the same reversible settings ownership policy. When both edit `settings.json` in one plan, their edits are merged into one file change.
- QA-002: Native bridge ownership is tracked per provider. Selected-provider and generic removal release only the selected provider's ownership; the shared settings and extension remain while a native provider or desktop profile still requests them. Old manifests without native ownership metadata remain readable, and unknown metadata is retained.
- ANALYSIS-001/002: Added coverage for custom executable/data settings, installation in both orders, provider-specific and generic removal, last-consumer cleanup, preserving user edits and previous values, failed-action retry, and legacy ownership.

## CLI and documentation

`setup --host-bridge --remove --apply` removes the selected providers' hooks and native bridge ownership while preserving desktop profiles and PowerShell imports. `setup --remove --apply` also releases native bridge ownership for selected providers. The shared bridge is removed only when no native or desktop consumer remains. See [installation.md](../../../../docs/installation.md).

The native setup documentation describes hook and bridge configuration only. It does not claim terminal binding, presentation, or complete native adoption.

## Checks

- `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~NativeHostSetupTests|FullyQualifiedName~NativeSetupCliTests|FullyQualifiedName~DesktopSetupTests|FullyQualifiedName~SetupTests|FullyQualifiedName~ProfileMigrationTests|FullyQualifiedName~IntegrationRefresh" --verbosity minimal` — passed, 52 tests.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include <assigned source, test, and documentation files>` — passed.

## Acceptance limits

Tests used isolated fixture homes. No provider hook trust action, VS Code extension install/uninstall, or live host trial was performed. Those remain outstanding for task 5.1 acceptance.
