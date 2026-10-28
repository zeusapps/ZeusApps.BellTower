# Proposal

## Why

Codex hook events may never reach Bell Tower on Windows. During the 2026-10-27 live-trial harness work (archived change `2026-10-27-confirm-claude-human-permission-waits`, evidence `implementation-011-live-harness.md`, H-1), Codex CLI 0.164.0 ran hook commands through PowerShell 7. The command that setup generates for Codex, `"<ingress>" --provider codex --data-dir "<dir>"`, is quoted for the Windows command shell and is a PowerShell parse error, so in two isolated runs it delivered no captures, while the PowerShell call form `& '<ingress>' ...` delivered them. The user reports missing Codex notifications in daily use, which is consistent with this but not yet verified against their real configuration. If confirmed, Codex sessions silently lose all hook-driven attention, a release-blocking parity gap.

The same harness observed one more delivery problem (H-2): with the installed asynchronous Codex hook registration, `PermissionRequest` reached ingress about 20 ms before its `PreToolUse` once, and the wait was treated as already resolved.

## What Changes

- Establish the exact rule Codex 0.164.0 on Windows uses to choose the hook shell (and whether a shell-independent argument-vector form exists), with a reproducible probe, before changing the command.
- Generate Codex hook commands that run correctly in the shell Codex actually uses, keeping spaces, apostrophes and Unicode in paths, the direct fast ingress and the existing latency goal.
- Verify generated commands during setup through the provider's real hook shell, not a substituted one, so a command that cannot run is reported instead of silently installed.
- Detect existing owned Codex hook entries with the broken form and replace them only through the existing previewed, explicit migration, preserving user edits and unrelated hooks.
- Diagnostics report Codex hook delivery as unverified when hooks are configured but no Codex capture has ever arrived, instead of implying it works.
- Reproduce H-2 deliberately. If confirmed, a permission request delivered before its own tool start must not be resolved by that start; the wait stays pending until its correlated resolution.
- Refresh `docs/compatibility.md`, `docs/follow-ups.md` and the doctor wording that still say live auto-versus-human acceptance was not performed, citing the user-operated native trials of 2026-10-27 and their scope (isolated harness sessions).
- Parity: Claude's hook command path and diagnostics receive the same real-shell verification and delivery-unverified reporting. Claude's command form changes only if the same check fails.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `standalone-setup`: Direct capability-aware provider hooks (commands must run in each provider's actual Windows hook shell, verified through that shell); Actionable provider hook diagnostics (configured-but-never-delivered reporting).
- `session-tracking`: Causally ordered provider observations (an approval request delivered before its own tool start is not resolved by that start).

## Impact

- `src/BellTower/Setup/HookCommandBuilder.cs`, `ProviderSettingsEditor.cs` and the setup preview/migration and provisioning probe paths.
- Reducer correlation for out-of-order approval and tool-start delivery (`src/BellTower/Core`), if H-2 is confirmed.
- Doctor/capability matrix output (`src/BellTower/Diagnostics`), with a documented output change if fields are added.
- Docs: `docs/hook-ingress.md`, `docs/compatibility.md`, `docs/follow-ups.md`, `docs/installation.md`.
- Users: installed Codex hooks need a previewed migration after upgrade. No provider settings are changed without that explicit action.
