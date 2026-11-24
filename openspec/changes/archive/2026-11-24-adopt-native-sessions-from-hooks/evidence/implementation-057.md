# Implementation DEV-057 (task 4.5 acceptance)

- Verification of the claim: `TerminateProcess` appears in `src/BellTower.Protocol/TrayProcess.cs` at the extern declaration and at one call in `StopSuspended`. `StopSuspended` is called only after `CreateProcessW` or `CreateProcessWithTokenW` (flags include `CreateSuspended`) succeeded, with that call's own `process.Process`, on the failure paths of `IsProcessInJob`, the breakaway check and `ResumeThread`. It never takes a PID, name or record. Production code unchanged. Shared by Claude Code and Codex hook starts.
- Fix: added the `BellTower.Protocol/TrayProcess.cs` entry (2 sites) to `Allowed` in `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs`.
- Docs: added a `recover-unready-dev-operation` section to `docs/follow-ups.md` (QA-002, ANALYSIS-004, -006, -007, -011).
- Checks: `dotnet build -c Release` 0 warnings, 0 errors. `ProcessStopAuthorityTests` 2/2 passed. `dotnet format --verify-no-changes` on the test file passed (file kept CRLF in the working copy, as the other tests are).
- Full suite: 1839 total, 1827 passed, 12 failed. 11 are the known `HookTrayStartTests` (ordinary tray start outside the test host's job). 1 is the known flaky `CodexSharedRuntimeServiceTests.ElevatedSharedBackendIsRefusedWithItsOwnStatusAndNothingIsSent`. The guard test passes.
