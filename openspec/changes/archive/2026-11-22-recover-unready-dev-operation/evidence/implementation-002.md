# Implementation evidence 002 (DEV-002, fix cycle 1)

Baseline `f4dfa4e`. Fixes for QA-001 (ANALYSIS-002 is its duplicate), ANALYSIS-001, -003, -005, -008, -009 and -010. Each fix awaits independent verification. No commit made. Not touched: ANALYSIS-004, -006, -007, -011, QA-002.

## Fixes
- **QA-001 / ANALYSIS-002.** On a `finalized-without-tray` manifest the retry removes any single remaining record, under the gates, only when that path's hash equals the recorded one and the record parses to the manifest's coordination plan (`Assert-PresentOwnedRecords`, `Remove-PresentOwnedRecords`). A foreign or changed lone record is refused, as is any lone record while the manifest is not finalized. `removedRecords` reports the real count.
- **ANALYSIS-001.** `Assert-FinalizeOwnerEnded`: Finalize refuses unless `ProcessProbe.Inspect(owner)` is `Ended` (`Unverified` refuses); the message names the installer PID and says to wait. Under the launch gate, immediately before the first write, `Assert-FinalizePreconditionsHold` re-checks the owner, the `current.json` hash against `originalSelectionSha256`, the manifest bytes (read once as bytes, hash kept in `$script:manifestReadSha256`), the records and the original bytes. New disposable barrier `rollback-before-tray-restart` (after the reservation release).
- **ANALYSIS-003.** The drain runs under the update gate and the tray-instance reservation only. The launch gate is taken only for the short effect. Refusal PIDs are computed once by `Get-FinalizeTrayRefusal`, after every gate is released.
- **ANALYSIS-005.** `Get-OperationDisposition` (finalizable, rollback, terminal, finalized, foreign) is the single source of the next step for Finalize, the Switch and Rollback entry checks and `New-NotReadyFailure`. A terminal Switch with `trayRestartedAfterFailure` now makes Finalize say "run Preflight again". An inner Rollback restart miss names Rollback, not Finalize.
- **ANALYSIS-008.** The harness search uses JSON-escaped backslashes (`Test-SerializedPath`) over the serialized facts only. A new check, `pathDetectorCatchesAnInjectedPath`, proves an injected path fails the detector.
- **ANALYSIS-009.** `Format-RecordPath` prints the operation record relative to the repository when inside it; a workspace outside the repository prints as given (documented exception). The not-ready message says the facts "could not be stored" when the save fails. Harness assertions updated.
- **ANALYSIS-010.** `Find-OwningOperation` returns a workspace `operation.json` only when both `coordinationId` and token match the records. Preflight's unknown-owner message and the interrupted-update refusal of a Switch (`Assert-NoCoordinator`) name it with its disposition and next step.

## Tests added
`tests/BellTower.Tests/Scripts/`:
- `InstallDevScriptTests.Finalize.cs`:
  - `FinalizeRetryRemovesTheSingleRecordAnInterruptedRemovalLeftAndANewSwitchFollows` (restart-only, pending-only, then Preflight and Switch)
  - `FinalizeRetryRefusesALoneRecordThatIsForeignOrChanged` (4 cases)
  - `FinalizeRefusesWhileTheInstallerThatOwnsTheRecordsIsStillWaitingForItsTray` (live Rollback at the new barrier; manifest and records byte-identical; Finalize succeeds once the owner is killed)
  - `FinalizeDoesNotHoldTheLaunchGateWhileItDrainsTrayStarts`
  - `FinalizeOfATerminalSwitchSaysPreflightAgainAndNeverNamesRollback`
  - `PreflightAndSwitchNameTheOperationRecordThatOwnsTheLeftoverRecords`
- Harness scenarios (`InstallDevFunctionHarness.ps1`, listed in `InstallDevScriptTests.Recovery.cs`): `not-ready-save-fails`, `not-ready-incomplete-rollback`, `owner-unknown-names-owning-operation` (includes a half-matching record that must never be named). Existing `not-ready-*` scenarios gained relative-path, facts-stored and injected-path checks.
- Helpers: `RunningInstaller.Id`, `HoldTrayInstanceObject()`.

## Docs
`docs/development.md`, `docs/releases.md` and both skill copies (byte-identical, verified with `fc /b`) describe the owner rule, the single-record retry, the disposition rule, the relative record path and the owning-record hint.

## Results
- `dotnet build -c Release --no-restore`: passed, 0 warnings, 0 errors.
- `InstallDevScriptTests` and `InstallDevBuildSkillTests`: 121 passed, 1 failed (122). The failure was `InstallerFunctionsReleaseEveryResourceAndKeepThePrimaryError(pointer-guard-throws)`, an IOException reading the harness `result.json` while the harness held it (read race, unrelated scenario). A rerun of that theory: 42 passed, 0 failed.
- Standalone, `--no-build --no-restore`, filtered: `FinalizeRefusesWhileTheInstallerThatOwnsTheRecordsIsStillWaitingForItsTray` and `FinalizeDoesNotHoldTheLaunchGateWhileItDrainsTrayStarts`: 2 passed, 0 failed (14 s).
- Full suite: 1824 passed, 13 failed. The failures are `HookTrayStartTests` (the known 11; 10 were visible in my filtered output), `ProcessStopAuthorityTests.OnlyHandlesCreatedByTheOperationAreEverTerminated`, and `CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens(owned: True)`. The last is not on the known list; it passed on rerun (2 passed).
- `dotnet format --verify-no-changes` on the three changed test `.cs` files: exit 0.
- `openspec validate recover-unready-dev-operation --strict`: valid.
- `dotnet build-server shutdown`: done; no test or build processes of mine remain.

## Limitations
- Task 4.3 (live recovery) is not performed; nothing ran against the real installation or data directory.
- Rollback's own two-step record removal has no retry window (QA-001 marked it out of scope).
- The launch-gate test samples the gate about every 50 ms during Finalize's drain and asserts the slowest acquire is under 1.5 s. It is timing-based.
- Parity: one shared script, nothing provider-specific; identical for Claude Code and Codex.

## Final identity
`head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596` (taken before this evidence file was written; the file is outside the scoped paths).
