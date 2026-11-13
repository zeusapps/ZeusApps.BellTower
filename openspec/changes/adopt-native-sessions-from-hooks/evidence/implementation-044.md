# DEV-044: QA-3800 test-only fix

Awaiting independent verification.

- Race: `CodexStandInServer.Accepted` counts at TCP accept; the stand-in checks `Silent` only when it reads a message. A test that switched to `Normal` after `Accepted >= n` could get an answer on the connection meant to stay silent.
- Fix (tests only): added `CodexStandInServer.Received` (messages read, counted before the behavior check). `AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` now waits for `Received >= 1` and asserts it before switching to Normal. Sibling `AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain` had the same pattern (`Accepted < 2`) and waits for `Received >= 2` (X's and A's initialize).
- Other `Accepted` uses (CodexTransportTests, CodexSharedRuntimeServiceTests, ConcurrentFailedOpens...) only assert counts after operations complete; no mode switch, no race.
- Fixed tests alone 20x: all pass. CodexSharedAccessTests class 5x: 28/28 each.
- Mutant (generation check `== started` replaced by `true` in CodexSharedAccess.cs; restored byte-identically, git shows no src change): 3 failures (both fixed tests and ConcurrentFailedOpens...).
- Full suite: 1551 tests; run 1 had 10 unrelated load-sensitive failures that passed alone (59/59); run 2 had 4 failures: ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween (passes alone 3/3) and UpdateReplacesOnlyTheTrayAndLeavesEveryOtherProcess x3 (fails alone with "Unsupported installation" in Installation ctor, also fails with my changes reverted: environmental, unrelated).

## QA-3700 addendum: launcher not built by the test project
- Cause: `ManagementOwnershipProcessTests.CreateInstallation` copies `src/BellTower.Launcher/bin/<config>/<tfm>`, but the test project did not build the launcher, so a Debug run found an empty directory ("Unsupported installation").
- Fix (tests only): `BellTower.Tests.csproj` gains `ProjectReference` to `BellTower.Launcher` with `ReferenceOutputAssembly="false"` (same pattern as Ingress); `CreateInstallation` now throws FileNotFoundException naming the missing `bell-tower.exe` launcher path. No lock file changed; `dotnet restore --locked-mode` succeeds.
- ManagementOwnershipProcessTests: 20/20 in Debug and 20/20 in Release (including the three update rows).
- QA-3800 checks re-run: fixed tests alone 20/20, class 5x 28/28.
- Full suite (Debug): 1551 tests, 1 failure, CodexTransportTests.ExpectedIdentityThatIsAliveButNotTheListenerIsRefused, which passed alone 5/5 (load flake; not run-to-run stable in the full suite).
