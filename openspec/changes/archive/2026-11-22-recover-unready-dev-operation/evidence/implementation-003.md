# Implementation 003 (DEV-003, fix cycle 2)

Fixes awaiting independent verification: ANALYSIS-100, ANALYSIS-101.

## ANALYSIS-100

- `scripts/install-dev.ps1`, `Get-OperationDisposition`: a Rollback in `running-original` with neither coordination record now returns kind `completed`, next step "Run Preflight again for a new operation." (no Finalize, no tray quit). It is checked after the Rollback phase test and before the ownership-field check. `restored-monitoring-snapshot` with records, and `running-original` with one or both records left, stay `finalizable`.
- Finalize refuses `completed` through its existing `kind -ne 'finalizable'` rule, so the same text appears before the record-missing check.
- The entry gate still refuses only `terminal` and `finalized`, so a Rollback retry is not newly refused. The Switch hint uses the `completed` next step alone.
- Test: `ASwitchOrFinalizeOfACompletedRollbackSaysPreflightAgainAndNeverNamesFinalizeOrTheTray` (Finalize.cs): Switch, Rollback, Switch again, then Finalize. The existing `running-original`-with-records theory stays green.

## ANALYSIS-101

- `Restore-StoppedTrayAfterFailure` builds `$recovery` with `Format-RecordPath`.
- Test: harness scenario `stopped-tray-readiness-failure-prints-relative-recovery` (stubbed `Start-SelectedTray` throwing `New-NotReadyFailure` after a launch), registered in the `InstallerFunctionsReleaseEveryResourceAndKeepThePrimaryError` theory. It asserts the relative `-Action Rollback -OperationManifest '<relative>'` and no repository prefix.

## Docs and skills

No quoted message changed; none edited. The skill copies are untouched and stay identical.

## Checks

- Release build: passed (0 warnings, 0 errors).
- `InstallDevScriptTests` and `InstallDevBuildSkillTests` together: 124 passed, 0 failed (includes both new tests).
- `dotnet format --verify-no-changes` on the changed C# files: passed.
- `openspec validate recover-unready-dev-operation --strict`: valid.
- Full suite: not run (optional).
