> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 012). Client: Claude Code 2.1.310, delegated subagent (auto mode), model claude-opus-5-5 (role default). The client refused the reviewer's own write with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." This is the known restriction recorded in docs/agent-workflow.md. The evidence files under `../evidence/analysis-012-*` were written by the reviewer itself.

# Analysis report: install-dev-builds / Analysis 012

## Review target

- Reviewer / role: delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (delegated subagent, auto mode; this subagent cannot see the version string; round 011 recorded 2.1.310) / claude-opus-5-5 (role default)
- Date: 2026-10-25 (UTC)
- Change root / assignment: `openspec/changes/install-dev-builds` / Analysis 012, a recheck of Implementation 012
- Task and requirement references: task 4.1; LIVE-001 and LIVE-002 ([findings](findings.md)); handoff [implementation-012](../evidence/implementation-012.md); rechecked [Analysis 011](analysis-011.md) ANALYSIS-550 to ANALYSIS-553
- Scope and relevant neighboring paths:
  - Changed:
    - `src/BellTower/IO/TrayInvocationCommand.cs`
    - `tests/BellTower.Tests/IO/TrayInvocationCommandTests.cs`
    - `scripts/install-dev.ps1`, specifically `Stop-VerifiedTray`, the drain, `Start-SelectedTray`, `Get-RecoveryExpectedContractHash`, `Restore-StoppedTrayAfterFailure`, the early Switch and Rollback refusals, the recovery classification and the `finally`
    - `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1`
    - `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs`
    - the Recovery bullet in `docs/development.md`
  - Neighbors read:
    - `src/BellTower/Cli/CliApplication.cs` (the root registry) and every `src/BellTower/Cli/*Command.cs` constructor
    - `src/BellTower/Updates/UpdateTransaction.cs` (`RestartPending`, `CanStartTray`)
    - `src/BellTower.Distribution/DistributionJson.cs` (the share mode of `Read`)
    - `src/BellTower/Tray/TrayHost.cs`
    - both `install-dev-build` skill copies (identical and unchanged)
- Baseline commit: `84dac98`
- Implementation identity ([before](../evidence/analysis-012-identity-before.txt)): `head=84dac98 baseline=84dac98 files=6 diff=42f119930b2d untracked=0/e3b0c44298fc`. The tracked diff SHA-256 is `42f119930b2d377ea9c87a09dd957d5baf0fceae2125f17e4f6dfd707ea17bce`. This matches the expected identity.
- Before/after identity ([after](../evidence/analysis-012-identity-after.txt)): the summary line and diff hash are identical. Nothing changed unexpectedly.
- Prior reports and rechecked findings:
  - [Analysis 011](analysis-011.md): ANALYSIS-550, ANALYSIS-551, ANALYSIS-552 and ANALYSIS-553.
  - ANALYSIS-554 and ANALYSIS-555 are deferred and were not made worse (see the assessment).
  - Regression view of ANALYSIS-200/201.
  - One new regression finding: ANALYSIS-600.

Line numbers refer to the working tree at the reviewed identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-550: the `statusline` fix | Reran the round-011 classifier probe against the current Release build ([results](../evidence/analysis-012-classifier-probe-results.json)) | `statusline` and `event` now classify as `NotTray`, and `tray` as `ThisDataDirectory`. `status-line` now throws `unknown Bell Tower command`. Every CLI command is known to the classifier, and every classifier verb has a CLI command. | Fixed |
| Removing `status-line` | Ran `git log --all -S'"status-line"'` and `git log -G'base\("' -- src/BellTower/Cli`, then read each commit's verb diff | The `"status-line"` literal appears only in 84dac98, and only in the classifier. `statusline` has been the verb since `6591ad0` (initial setup). The CLI history only adds verbs (`4db8dda`, `ed16b9f`) or edits descriptions (`b74a12b`). It never removes or renames a verb, so no retained older payload can run a verb outside the current set. `setup --status-line` is an option of `setup`, and `setup` already classifies as NotTray. | Removal is justified. Keeping the entry would accept a verb the CLI rejects. |
| Drift test design | Compared `RegisteredCommandNames()` (`TrayInvocationCommandTests.cs:73-88`) with the root registry (`CliApplication.cs:18-43`). Searched for `Aliases` and for `: Command` outside `BellTower.Cli`. | The 26 sealed `Command` types in `BellTower.Cli` match the 26 `root.Subcommands.Add` calls one to one. There are no aliases and no inline commands.<br><br>The test fails on a renamed verb, because it reads the name from the instance. It fails on a new type in the namespace, and loudly on an unsupported constructor shape (`Single()` or the parameter-type `throw`).<br><br>It does not see three cases: a command that is registered but defined in another namespace, an inline `new Command(...)`, or an added alias. It also tests unregistered types, but that can only fail toward over-inclusion. | Accepted, not a finding. None of the blind spots exist in the codebase today. Closing them needs a seam in `CliApplication`, which is outside the paths of Implementation 012. |
| ANALYSIS-551: the live-owner branches | Read 736-741. Ran the harness scenarios in the [retention probe](../evidence/analysis-012-retention-mutation-probe.ps1) ([results](../evidence/analysis-012-retention-mutation-results.json)). | Both branches now call `Remove-OwnedCoordinationRecords` and keep their note. The removal still checks the exact bytes and tuple (`Assert-OwnedRecordPair`). Nothing is launched in these branches. `stopped-tray-still-running` passes with 0 records left. | Fixed. A minor message residual is noted under Re-verification. |
| ANALYSIS-552 (1): the Switch-phase contract | Compared `Get-RecoveryExpectedContractHash` (718-724) with the Switch plan's contract (1004-1006) | Both hash `$manifest.preparedPayload/release.json`, and `preparedPayload` is confined to the operation directory (888). After the payload copy, the installed `release.json` is byte-identical (`VerifyFiles` at 1062 and 1064). So the expected hash is the same in every Switch-phase state. If the prepared payload is missing, the hash throws inside the `try` while `coordinationPlan` is unset, so the records are preserved. | Fixed. The e2e test `RollbackOfASwitchInterruptedBeforeThePayloadCopyDerivesTheContractFromThePreparedPayload` covers it. |
| ANALYSIS-552 (2): retention decided by `Process.Start` | Read 488-492 and 733-779. The retention probe ran the existing harness scenarios against the reviewed script and against a scratch mutant that restores the round-011 property-presence test. | In source, the flag is set only after `Process.Start` returns (inside the activation `try`), and it is reset at the start of each Restore call. All four scenarios pass on both the reviewed script and the mutant. So no current test tells "this call launched" apart from "a `restartLauncher` property is left over from an earlier phase", which is the Rollback case ANALYSIS-552 named. | Correct in source, but no regression test pins it. See the residual under Re-verification. |
| ANALYSIS-552 (3): the launched-unverified message | Read 766-770. Checked the harness scenario `stopped-tray-launched-unverified` and native case D in [implementation-012](../evidence/implementation-012.md). | The records are kept. The message says a tray may be running unverified, and it no longer says "Monitoring is stopped". | Fixed |
| ANALYSIS-553: `$untouched` and the terminal outcome | Read 748-761 and 897-903. Ran the [prepared-restart probe](../evidence/analysis-012-prepared-restart-probe.ps1) ([results](../evidence/analysis-012-prepared-restart-results.json)). | The `before-monitoring` check was added. The flag is saved, the Switch hint says to run Preflight again, and Rollback refuses before the gate.<br><br>However, if Restore runs while the state is still `prepared`, the flag is saved with state `prepared`. Line 900 checks only the state, so that manifest can still be switched. | Fixed for the reported cases. New regression: ANALYSIS-600. |
| Rollback's early refusal versus the never-selected route | Read the never-selected route (1087-1094) and the post-gate restart (1183) | With the flag set, the never-selected route would only drain, recheck the owner and restart the original tray, which is already running. That route never removes the copied candidate payload (Retire does that). So refusing loses no work. | Correct for never-selected manifests. The problem case is ANALYSIS-600. |
| ANALYSIS-200/201 machinery | Checked whether the flag can coexist with `rollbackPointerTransition` or with a mutation flag | Restore still runs only when `!selectionMutated -and !monitoringMutationStarted` (1165). The early refusal throws before any record, intent or route work. Every pointer-intent route still sets `monitoringMutationStarted` first (921-925). | No regression |
| Restart fence and `Assert-OwnedRestart` | Diffed 476-519 | `Assert-OwnedRestart` is unchanged. During the Restore launch, the records stay in place until the restart is verified. They are removed only where nothing was launched, or after verification. | No regression |
| 3 s hook budget | Searched the diff for changes to `Open-LaunchGate` and `Invoke-GuardedPointerStep` | The new checks run either before the gate (897-903) or after `finally` released it (1168-1170). `Invoke-DisposableFailure` runs outside the launch gate. | Nothing new under the launch gate |
| Test-only injections | Read 474, 477, 498 and 793 | `fail-after-launch-*` and `fail-start-*` need `-TestBarrierDirectory`, and line 793 allows that only with `-Disposable`. `Invoke-DisposableFailure` checks both itself. | Safe |
| Claude/Codex parity | Inspected the shared paths; ran the probe; read `ClassifiesStatusLineAsNotTrayForEveryProvider` | One classifier and one installer serve both clients. The `statusline` test covers `--provider claude` and `--provider codex`. The drift test covers the Codex verbs `event`, `prepare-codex-runtime`, `codex-runtime-host`, `stop-codex-runtime` and `native-sync`. The two skill copies are identical. | Parity gap closed. No provider-specific policy was added. |
| Format (scoped) | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include` on the three changed `.cs` files | Exit code 0 | Passed |
| Build, full tests, native runs | Not run | QA owns these. The probes reused the existing `tests/BellTower.Tests/bin/Release` output, whose timestamp matches the changed source. | Not performed |

## Findings

### ANALYSIS-600: The terminal flag can be saved while the state is still `prepared`, so a Switch retry succeeds and a later Rollback of that switch then refuses at once

- Severity: warning. The trigger is narrow, but the saved state disables the rollback safety net.
- Related task / requirement: LIVE-002; the ANALYSIS-553 correction; the "`Rollback` reverts a switch" contract in `docs/development.md`
- Locations in `scripts/install-dev.ps1`:
  - 754-761: the flag is set in the `elseif ($Action -eq 'Switch')` branch for every state other than an untouched `original-stopped`.
  - 897-899: Rollback refuses on the flag alone.
  - 900: the Switch refusal checks only `state -ne 'prepared'`.
  - 1050-1053: `original-stopped` is saved only after `Stop-VerifiedTray` returns.
  - 131-134: `stoppedTray` is set before the exit wait.
- Observation:
  - How it happens:
    - The trigger is the `Verified tray did not exit` timeout. When it fires, the Switch state is still `prepared`, but Restore runs because `stoppedTray` is set.
    - The tray process may end between the 30 s deadline and Restore's `Read-Owner`. `Read-Owner` then finds no owner, and the tray is restarted and verified.
    - The `elseif` then saves `trayRestartedAfterFailure` with state `prepared`, with the note "cannot be retried; run Preflight again".
    - The probe reproduces this saved state exactly: `savedState: prepared`, `savedTerminalFlag: true`, records removed.
  - The note is wrong: nothing was touched, so the operation can be retried.
  - Nothing blocks the retry, and its result can block the rollback:
    - A Switch with that manifest passes 900 and 1044, because the state is `prepared`, and it can complete.
    - Nothing ever clears the flag. A later `Rollback -OperationManifest` of the now-selected dev build therefore refuses at 897 with "Nothing to roll back; run Preflight again".
    - The documented one-level rollback is then unavailable, and recovery means editing `current.json` by hand.
  - Likelihood: the trigger window is milliseconds after a 30 s exit hang, and someone must also retry against the note. The chance is low, but the saved state disables the main recovery path.
- Proposed improvement and benefit: two independent guards; either one removes the defect.
  1. Treat `prepared` like an untouched `original-stopped`, so `$manifest.state -in @('prepared', 'original-stopped') -and $untouched` counts as retryable. Set the flag only in the other cases.
  2. Apply the 897 refusal only to a never-selected manifest: the selection hash equals `originalSelectionSha256` and the state is in the never-selected set. In every other case, continue to the normal route table, which already handles a selected dev build.

  The second guard also keeps a stale flag from hiding any future state.
- Behavior-preservation risks: guard 1 changes only the note and the state in a case where nothing was touched. Guard 2 keeps the immediate refusal for every manifest the flag was meant for: selection still original, and state `original-stopped`, `ready-to-select` or `prepared`.
- Evidence type: measured result (a harness probe of the saved state), plus source reading for the retry and Rollback consequences
- Verification / measurement plan:
  - A harness scenario like the probe's (state `prepared`, verified restart) that asserts the operation stays retryable and no flag is saved.
  - An e2e test with a manifest that has the flag, a selected dev pointer and a matching `devSelectionSha256`. Rollback must not refuse at 897.
  - A mutation that drops either guard must fail.
- Relationship: a regression introduced by the ANALYSIS-553 correction

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-550 | identity above | The classifier probe gives `statusline` → NotTray and `status-line` → unknown command, and no CLI verb is unknown to the classifier. History shows `status-line` was never a verb. The drift test covers renames and new types in `BellTower.Cli`. It does not cover other namespaces, inline commands or aliases, and none of those exist today. The scoped format check passed. | Verified |
| ANALYSIS-551 | identity above | Lines 736-741 remove the records under the exact pair check and keep the note. The harness scenario `still-running` leaves 0 records, and the developer's mutation that drops the removal fails both scenarios.<br><br>Residual: the removal can throw, but only when the record bytes are no longer ours. In that case the catch at 771-776 reports "could not be restarted automatically … Monitoring is stopped" even though a live tray was found. The records are correctly kept, so the only problem is the message, on a foreign-records path. | Verified, with a minor message residual |
| ANALYSIS-552 | identity above | Point 1: the contract matches the one Switch computes, and the handoff reports the e2e test passing.<br><br>Point 2: retention follows `$script:restartLaunched`, which is correct in source. However, the retention probe shows the existing harness scenarios also pass with the round-011 property-presence test. So no test pins the case the finding named: a Rollback with an existing `restartLauncher` and a failure before launch.<br><br>Point 3: the message is fixed, as the harness and native case D show.<br><br>Residual: a failure after a verified restart (for example in `Save-Json` or record removal) also gets the "launched but could not be verified" message. That is conservative but inaccurate. | Verified in source. Point 2 has no regression test; I suggest a harness scenario with an existing `restartLauncher` and a pre-launch failure that asserts the records were removed. |
| ANALYSIS-553 | identity above | The harness scenario `partial-snapshot` passes for the `before-monitoring` check. The e2e test passes for the flag, the Switch hint and the immediate Rollback refusal, with state `original-stopped`. The flag is also saved with state `prepared`, which is ANALYSIS-600. | Verified for the reported cases; the regression is ANALYSIS-600 |
| ANALYSIS-200 / ANALYSIS-201 (regression) | identity above | The Restore precondition, intent routes, mutation flags and guarded pointer steps are unchanged. The new refusals throw before any record or intent work. | No regression |

## Assessment and limitations

- Covered:
  - The full Implementation 012 delta:
    - the classifier and its tests;
    - the Restore retention and messages;
    - the recovery contract hash;
    - the terminal flag and its two refusals;
    - the moved `original-stopped` save;
    - the disposable injections.
  - Regression checks: the drain, the restart fence, `Assert-OwnedRestart`, the launch-gate budget, ANALYSIS-200/201 and Claude/Codex parity. Every check covers both clients, because they share the installer and the classifier.
- Developer decisions:
  - Removing `status-line`: correct, and the history supports it.
  - The reflection-based drift test: acceptable. It catches renamed verbs and new types in `BellTower.Cli`. It misses commands defined in another namespace, inline commands and aliases. None of those exist today, and a seam for the real registry would need a change to `CliApplication`.
  - Removing the records in the live-owner branches: correct.
  - Deciding retention by whether this call reached `Process.Start`: correct in source, but no test tells it apart from the old property check.
  - Deriving the Switch-phase contract from `preparedPayload`: correct, and equivalent in every Switch-phase state.
  - `trayRestartedAfterFailure` and the immediate Rollback refusal: correct for never-selected manifests, but they need the state and selection guards described in ANALYSIS-600.
- Deferred findings:
  - ANALYSIS-554 is unchanged: `$previous` still holds only unclassified keys.
  - ANALYSIS-555 is unchanged: Restore still neither renews the plan nor rechecks the selection before it launches. The new `restartLaunched` routing actually sends that case to the more accurate "may be running unverified" message.
  - Neither was made worse.
- Not performed: build, full test suite and native runs (QA owns them). I did not measure how likely the ANALYSIS-600 trigger window is.
- Writes:
  - `evidence/analysis-012-identity-before.txt` and `evidence/analysis-012-identity-after.txt`
  - `evidence/analysis-012-classifier-probe-results.json`, produced by the unchanged round-011 probe script
  - `evidence/analysis-012-retention-mutation-probe.ps1` and `evidence/analysis-012-retention-mutation-results.json`
  - `evidence/analysis-012-prepared-restart-probe.ps1` and `evidence/analysis-012-prepared-restart-results.json`
  - `reports/analysis-012.md` was refused by the client, so this report text is inline.

  The probes kept their mutant script, harness copy and fixture roots only in the session scratchpad. They started only harness `pwsh` processes, which exited when released, and touched no tray, provider or user data directory. The review identity is unchanged.
- Decision for the orchestrator: whether to fix ANALYSIS-600 and add the missing ANALYSIS-552 point-2 test before the live install.

```
status: done
role / client / model: code-analyzer / Claude Code (delegated subagent, auto mode; version not exposed to the subagent) / claude-opus-5-5
task / finding IDs: Analysis 012 recheck of Implementation 012; ANALYSIS-550 verified; ANALYSIS-551 verified (minor message residual); ANALYSIS-552 verified in source, but point 2 has no distinguishing regression test; ANALYSIS-553 verified for the reported cases; new ANALYSIS-600 (warning, regression from the 553 fix); ANALYSIS-200/201 no regression; ANALYSIS-554/555 deferred, not made worse
changed or report paths: evidence/analysis-012-identity-before.txt, evidence/analysis-012-identity-after.txt, evidence/analysis-012-classifier-probe-results.json, evidence/analysis-012-retention-mutation-probe.ps1, evidence/analysis-012-retention-mutation-results.json, evidence/analysis-012-prepared-restart-probe.ps1, evidence/analysis-012-prepared-restart-results.json; reports/analysis-012.md: write refused, text inline
checks: scripts/review-identity.ps1 before/after -> passed (identical); classifier probe -> run (statusline NotTray, no unknown verbs); retention mutation probe -> run (harness scenarios pass on the reviewed script and on the round-011 property-presence mutant); prepared-restart probe -> run (flag saved with state prepared); scoped dotnet format --verify-no-changes -> passed; build/full test/native -> not run (QA owns them)
target identity: before: head=84dac98 baseline=84dac98 files=6 diff=42f119930b2d untracked=0/e3b0c44298fc; after: identical
limitations / decisions needed: the client refused the report write, so the orchestrator should save the text above; static analysis plus harness-level probes only; the likelihood of the ANALYSIS-600 trigger was not measured; decide whether to fix ANALYSIS-600 and add the ANALYSIS-552 point-2 test before the live install
next: orchestrator saves the report and triages ANALYSIS-600 with QA 012; if adopted, a developer correction, then QA and analysis rechecks
```
