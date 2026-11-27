# Implementation 013 (DEV-011): tasks 3.3 and 3.4

Scope: user decision 2026-11-26 (release essentials). One pass of each check, no repeat runs, no field-harness scenarios. Raw logs: `artifacts/dev013/` (ignored).

## 3.3 SHA-256 update verification: proof map

Mechanism: `UpdateTransaction.Stage` calls `ReleasePackage.Extract`, which runs `offer.Validate()`, the archive hash check (`Release package checksum mismatch.`) and the `.sha256` sidecar check before anything is moved to `versions/` or selected.

| Point | Proof |
| --- | --- |
| Mismatched archive hash rejected, selection unchanged | `DistributionTests.SafeExtractionChecksSidecarAndDoesNotOverwriteInstalledSelection` (wrong hash throws `InvalidDataException`, `current.json` still 0.1.7). Also new test below. |
| Modified archive rejected | New `DistributionTests.ModifiedMismatchedAndInvalidOffersAreRejectedWithoutChangingSelectionOrSessions`: an entry added after the offer was hashed; `Stage` throws `Release package checksum mismatch.`; the candidate contract is a health-v1 consumer (`AttachmentHealth = 1`) so the attachment-health gate passes first (correction, ANALYSIS-301). |
| Mismatched sidecar rejected | Same new test (sidecar digest differs from the offer); asserts `Checksum file mismatch.`. |
| Invalid offer rejected | Same new test (hash `not-a-digest`); asserts `Invalid release hash.`; `DistributionTests.MutationAndLaunchGates...` rejects a missing archive with an offer; `DevBuildTests` (`EnsureStable`, offer validation); `AttachmentHealthCompatibilityTests.CommittedHealthRequirementRejectsLegacySelection...` rejects an incompatible offer before the archive is read. |
| No selection or session change | New test compares `current.json` and `state.json` bytes (after a drained permission session) and checks no `versions/0.1.8` exists after each of the three rejections. `AttachmentHealthCompatibilityTests` makes the same byte comparison for the incompatible-offer rejection. |
| Unsafe archive contents rejected | `DistributionTests.UnsafeArchivesNeverEscapeStaging` (3 cases). |
| Candidate coexists with the real 0.1.9 baseline | Direct check in 3.4 below: real 0.1.9 zip (`artifacts/field-acceptance/baseline`, sha256 `2739659e...f062`, matches its `.sha256`) updated to the 0.1.11 fixture candidate by `tools/BellTower.Smoke`: PACKAGED UPDATE PASSED and PACKAGED ROLLBACK PASSED. |

Gap closed: the only missing proof was a `Stage`-level test with a modified archive and an invalid offer that also asserts sessions. The one new test covers it. Changed file: `tests/BellTower.Tests/Updates/DistributionTests.cs` (CRLF).

Deferred by the 2026-11-22 decision: publisher signing and the managed check control. No signing scenario ran or counts as passed. The verification above is SHA-256 only.

## 3.4 single pass

| Check | Result |
| --- | --- |
| `dotnet format ZeusApps.BellTower.slnx --verify-no-changes` | exit 0 (run before the final edit; rerun once after the CRLF conversion, see the end) |
| Clean Release build (`dotnet clean` then `dotnet build -c Release`) | succeeded, 0 warnings, 0 errors. The first build failed on a locked file: an orphaned unit-test tray (PID 47964, `artifacts/unit-tests/6bf9...`, parent gone) held `bell-tower.exe`; stopped, build then succeeded. |
| Complete .NET suite (`dotnet test -c Release --no-build`) | 2245/2247. Failures: `AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor` and `ConsoleTitleProcessTests.HelperChangesOnlyADisposablePrivateConsoleAndRejectsDetachedAttachments(codex, background)`. Each passed alone (1/1 and 4/4). No Ingress.Bench count mismatch (QA-165) appeared. |
| Node suite (`node --test vscode/service.test.js vscode/focus.test.js`) | exit 0 (`vscode` has no `npm test` script; command from docs/development.md) |
| `openspec validate validate-next-release-field-acceptance --strict` | valid. Also ran `--all --strict` by mistake of scope: 13/14, the failure is `sync-native-session-titles` (no delta specs; untouched by this change). |
| `scripts/update-acceptance.ps1` default mode | FAILED, once, not repeated. Baseline 0.1.7 fixture to 0.1.9 candidate. `Smoke` threw `InvalidOperationException: Bell Tower is not running for this data directory` at `Program.cs:210`, the `finally` of the stopped-installation section (`TrayController.RequestExit` right after `start --silent`), after the 12 stopped-tray captures were already in `state.json`. A tray (PID 17652, started via hook, `started-via-shell`) was running after the smoke exited; it and its launcher were an orphan of this run under `artifacts/update-acceptance/run-7f3c...` and were stopped. The `finally` exception masks any earlier error. See decision note. |
| Direct real-baseline check: `package.ps1 -FixtureVersion 0.1.11 -OutputDirectory artifacts/update-acceptance/packages`, then `BellTower.Smoke` with the real 0.1.9 zip | package ok; smoke exit 0: PACKAGED ROLLBACK PASSED and PACKAGED UPDATE PASSED (0.1.9 to 0.1.11, baseline fast ingress, stopped-tray section included). Evidence run `artifacts/update-acceptance/run-22f7be76...`. |

Environment: D: 105 GB free, about 6 GB free memory before the update acceptance run. No `~/.claude`, `~/.codex`, real installation or user tray (8060, 28388) was touched.

## Decision note

The default-mode failure is in the old-baseline (0.1.7 fixture) stopped-tray section; the same section passed against the real 0.1.9 baseline. It looks like a race between the hook-started tray and `start --silent` registration (tray.json.starting present), with the smoke's own `RequestExit` in a `finally` turning it into a failure. Not investigated further under the one-run rule.
