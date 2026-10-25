# Implementation 008 (QA-350 fix, awaiting verification)

- scripts/install-dev.ps1: `$recordsRemovedWithoutMutation` is set when the main finally removes this run's own records in the no-mutation path. Only then the post-restart ownership loop and the final removal are skipped; every other path keeps the strict check (throws on missing or changed records).
- Tests (InstallDevScriptTests.Recovery.cs): Rollback after a Switch interrupted at switch-before-selection (-NoTray) exits 0, running-original, no records, selection unchanged; negative test: records deleted during the Rollback gate wait still refuse and do not reach running-original.
- Checks: focused tests (InstallDevScriptTests|HookMaintenanceTests|DataLockTests) 67 passed; Release build passed; scoped dotnet format verify passed. Native reproduction not run.
