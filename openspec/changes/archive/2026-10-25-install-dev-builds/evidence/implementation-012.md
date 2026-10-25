# Implementation 012 handoff: install-dev-builds

Fixes awaiting independent verification: ANALYSIS-550, ANALYSIS-551, ANALYSIS-552, ANALYSIS-553 (QA-550 is a duplicate of ANALYSIS-550). ANALYSIS-554 and ANALYSIS-555 are deferred and not implemented.

Identity (scoped paths, baseline 84dac98): `head=84dac98 baseline=84dac98 files=6 diff=42f119930b2d untracked=0/e3b0c44298fc`

## Changes

- ANALYSIS-550 (`src/BellTower/IO/TrayInvocationCommand.cs`): `statusline` added to `NonTrayCommands`; `status-line` removed.
  - Justification: `git log -S'"status-line"'` shows the string entered only with 84dac98 (this change). No CLI verb was ever named `status-line`; the only `status-line` spelling in the product is the `setup --status-line` option, which is never a root verb. `ProviderSettingsEditor` writes `statusline`. No retained payload or shipped config can emit the verb, so keeping it would only accept a command the CLI rejects.
  - Tests (`TrayInvocationCommandTests`): `ClassifiesStatusLineAsNotTrayForEveryProvider` (claude, codex); `ClassifiesEveryRegisteredCliCommandExceptTrayAsNotTray`.
  - The drift test derives names by instantiating every `Command` type in `BellTower.Cli` through its single constructor (the root builder is private to `CliApplication`, which is outside this assignment's ownership), asserts `tray` and `statusline` are present, `tray` is detected and every other name is `NotTray`. Mutation check: reverting to `status-line` fails all three new tests.
  - `docs/development.md` Recovery bullet rewritten to name the real cause and the new behaviors.
- ANALYSIS-551 (`scripts/install-dev.ps1`): chosen option is to remove records in the live-owner branches (`Remove-OwnedCoordinationRecords`, exact byte and tuple check kept, message kept). Reason: `stoppedTray` is deliberately set before the exit request so a stop timeout reaches the restore path; moving it would change which failures restore. Nothing is launched in those branches, so the records protect nothing.
- ANALYSIS-552:
  - New `Get-RecoveryExpectedContractHash`: the Switch phase hashes `$manifest.preparedPayload/release.json` (as Switch computed it); Rollback hashes the installed original payload.
  - New `$script:restartLaunched`, set only when `Process.Start` returned in `Start-SelectedTray` and reset at the start of each Restore call. Retention is decided by it, not by the `restartLauncher` property.
  - Launched but unverified: records kept and the message says a tray may be running unverified, with the Rollback command for when none is.
  - Disposable-only injection `fail-after-launch-<version>` (needs `-TestBarrierDirectory`, which requires `-Disposable`) refuses after the launch and the manifest save.
- ANALYSIS-553:
  - `before-monitoring` folder check added to `$untouched`.
  - Terminal outcome: `trayRestartedAfterFailure` is saved with the manifest after a non-retryable restart. The Switch hint then says to run Preflight again, and Rollback refuses at once (before any gate or drain) with "Nothing to roll back; run Preflight again".
- Launch gate: nothing added under it (the new checks run before the gate or in the post-gate restore).

## Tests added (all mutation-checked)

Harness scenarios: `stopped-tray-still-running`, `stopped-tray-foreign-owner`, `stopped-tray-launched-unverified`, `stopped-tray-partial-snapshot`, `recovery-contract-without-candidate-payload`. End-to-end: `RollbackOfASwitchInterruptedBeforeThePayloadCopyDerivesTheContractFromThePreparedPayload`, `ATerminalRestartedOperationSaysPreflightAgainAndRollbackRefusesAtOnce`. The existing restart harness fixtures gained `operationDirectory`.

Mutations (script reverted afterwards, byte-identical): remove live-branch removal -> still-running and foreign-owner fail; ignore the launched flag -> launched-unverified fails; drop the `before-monitoring` check -> partial-snapshot fails; drop the flag write -> partial-snapshot fails; drop the Rollback refusal -> terminal e2e fails; drop the Switch hint branch -> terminal e2e fails; use the installed payload hash -> contract harness and e2e both fail.

## Checks

- `dotnet build -c Release --no-restore` -> passed (0 warnings, 0 errors; rebuilt after the last source edit).
- Focused (TrayInvocationCommandTests|InstallDevScriptTests|HookMaintenanceTests|DataLockTests) -> passed (101 tests, before the harness fix noted below; the one failure was my harness scenario using `Installation.Payload` on an absent payload, fixed and rerun in the full suite).
- Full suite once -> 833 passed, 1 failed: `StripRenderingTests.IncreasingTheInheritedFontRefitsTheChipAndItsStrip`. It passed on rerun in isolation (10/10) and no tray rendering code changed, so it is unrelated and load-sensitive.
- `./scripts/format.ps1 -Verify` -> passed.
- Native churn `evidence/implementation-012-native.ps1` (results `implementation-012-native-results.json`): 6 Switch to Rollback cycles with statusline churn about every 200 ms, all passed, pipes closed, original restored. Direct drain durations under the same churn (real `Wait-InstallationTrayStartsDrained`, no tray running), 5 runs: 0.30, 0.20, 0.15, 0.16, 0.15 s, none refused. Switch totals 4.9-5.4 s and Rollback 3.2-3.7 s, the same as Implementation 011 (those totals are dominated by tray stop and start, not the drain).
- Native faults `evidence/implementation-012-native-faults.ps1` (results `implementation-012-native-faults-results.json`): A/B/C unchanged and passing. New D: refusal after the tray stop, then launch, then injected refusal after launch with no candidate payload: message says a tray may be running unverified, records kept, tray came up (so the message is accurate); after ending that disposable tray, the same-manifest Rollback exited 0 with no records and the original tray running.
- Observation from the first D run: the launched tray comes up after the installer exits, and a Rollback run while it is alive refuses safely at the drain ("A tray startup did not drain"), also removing its records. This is the existing safe behavior and is why the message tells the user to check for a tray first.
- Cleanup: no disposable process remains (checked by command line and image path); disposable directories stay under `artifacts/implementation-012/`.

## Limitations

- The drift test uses reflection over command types, not the live root builder, because `CliApplication.Run` builds the root privately and `src/BellTower/Cli` is outside the assigned paths. A type that exists but is not registered would still be tested (harmlessly).
- The live-owner branches and the partial-snapshot branch are covered by harness scenarios only (not injectable natively).
- Skill copies unchanged.
