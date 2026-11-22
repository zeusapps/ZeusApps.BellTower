# Implementation evidence 001 (DEV-001)

Baseline `f4dfa4e`. Tasks 1.1, 2.1-2.4, 3.1 implemented; 4.1 build and test checks run. No commit made.

## Changes
- `scripts/install-dev.ps1`: `Describe-NotReadyTray` (facts: tray process alive, presence, `state.json` size against the 16 MiB budget, count and span of `tray-refresh-failed` lines in the last 64 KiB of the log, sanitized reason only when it holds no path). Used by the readiness-deadline failure (`New-NotReadyFailure`, stores `trayNotReady` in the manifest) and by both `Read-Owner` unknown-owner refusals. New `-Action Finalize` (`Invoke-Finalize`) with the preconditions and effect of design decisions 2 and 3, under the update gate, tray-instance reservation and launch gate, with a 5 s drain. `Start-TrayLauncher` is a seam extracted from `Start-SelectedTray`. Switch and Rollback results gain `trayRestartNote`; a finalized manifest is refused by Switch and Rollback with "run Preflight again".
- Tests in `tests/BellTower.Tests/Scripts/`: new `InstallDevScriptTests.Finalize.cs`, four harness scenarios (`not-ready-over-budget`, `not-ready-small-state`, `owner-unknown-no-presence`, `owner-unknown-ended-presence`) and the skill test now requires `-Action Finalize`.
- Docs: `docs/development.md`, `docs/releases.md`, both skill copies (byte-identical).

## Fixture method and limits
- The stuck-rollback state (original selection, restored monitoring, `restored-monitoring-snapshot`, both owned records kept) is reached by ending the installer after the original-pointer phase was saved. A real tray cannot be started in a fixture, so the readiness-deadline failure itself is covered in the function harness (stubbed launcher, tray scan and owner read), not by a CLI run. The live installation was not touched; Finalize 4.3 is Not performed.
- Tray start in progress is tested with a copy of the system command interpreter placed as a payload image of the disposable installation and started with the tray command line; it is killed by the test.

## Results
- `dotnet build -c Release`: passed, 0 warnings.
- InstallDev script and skill test classes: 109 passed, 0 failed.
- Full suite: 1810 passed, 14 failed. The 14 are the 11 `HookTrayStartTests`, `ProcessStopAuthorityTests.OnlyHandlesCreatedByTheOperationAreEverTerminated`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks` (passes alone, known flaky) and `AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor` (not on the known list; unrelated audio-timeout test, passes in isolation, flaky under load). `CodexSharedRuntimeServiceTests.ExplicitDisableAndEnablePersistAcrossServiceInstances` passed this run.
- `openspec validate recover-unready-dev-operation --strict`: valid.
- `dotnet format --verify-no-changes` on the changed C# files: clean.
- Parity: one shared script; skill copies identical (sync test passes). No provider-specific path added.
