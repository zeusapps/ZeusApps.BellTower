> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 013). Client: Claude Code 2.1.310, delegated subagent (auto mode), model claude-opus-5-5 (role default). The client refused the reviewer's own write with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." This is the known restriction recorded in docs/agent-workflow.md. The evidence files under `../evidence/analysis-013-*` were written by the reviewer itself.

# Analysis report: install-dev-builds / Analysis 013

## Review target

- Reviewer / role: delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (from `claude --version`), delegated subagent, auto mode / claude-opus-5-5 (role default)
- Date: 2026-10-25T13:44Z (UTC)
- Change root / assignment: `openspec/changes/install-dev-builds` / Analysis 013, a light recheck of Implementation 013
- Task and requirement references: task 4.1; LIVE-002 ([findings](findings.md)); handoff [implementation-013](../evidence/implementation-013.md); rechecked [Analysis 012](analysis-012.md) ANALYSIS-600 and the ANALYSIS-552 point-2 residual
- Scope and relevant neighboring paths:
  - Delta:
    - `scripts/install-dev.ps1`: `Restore-StoppedTrayAfterFailure` (725-781) and the early Rollback and Switch refusals (897-904)
    - `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1`: scenarios at 373-406
    - `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs`: lines 50-51 and 302-340
  - Neighbors read again for regression:
    - `Get-RecoveryRoute`, the ANALYSIS-200/201 routes (690-706)
    - `Start-SelectedTray` and `Assert-OwnedRestart` (476-519)
    - the Switch flow (1044-1075)
    - the `finally` and Restore precondition (1162-1171)
- Baseline commit: `84dac98`
- Implementation identity ([before](../evidence/analysis-013-identity-before.txt)): `head=84dac98 baseline=84dac98 files=6 diff=19aea83cdd82 untracked=0/e3b0c44298fc`. The tracked diff SHA-256 is `19aea83cdd826af4408e8c621a8dfe9c10b06086e82add55fe49964d13e4c491`, which matches the expected identity.
- Before/after identity ([after](../evidence/analysis-013-identity-after.txt)): byte-identical to before (`Compare-Object` found no differences). Nothing changed unexpectedly.
- Prior reports and rechecked findings: [Analysis 012](analysis-012.md) ANALYSIS-600 and the ANALYSIS-552 point-2 residual; a regression view of ANALYSIS-200/201, the restart fence, the launch-gate budget and parity. ANALYSIS-554/555 stay deferred.

Line numbers refer to the working tree at the reviewed identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Delta confinement | Compared file modification times with `evidence/analysis-012-identity-after.txt` (14:14:07) | Only `scripts/install-dev.ps1` (14:30:14), the harness (14:30:33) and `InstallDevScriptTests.Recovery.cs` (14:30:41) are newer. `TrayInvocationCommand.cs`, its tests and `docs/development.md` predate Analysis 012. | Matches the handoff |
| Guard 1 (ANALYSIS-600) | Read 752-761 | The retry condition is now `$manifest.state -in @('prepared', 'original-stopped') -and $untouched`. That branch saves `prepared` with the "can be retried" note. The flag is saved only in the `elseif ($Action -eq 'Switch')` branch. | Implemented as proposed |
| Guard 2 (ANALYSIS-600) | Read 897-900 and compared them with the `never-selected` route at 700 | The refusal now needs all of: the flag, state in `prepared`/`original-stopped`/`ready-to-select`, a `current.json` that exists, and a hash equal to `originalSelectionSha256`. That is the `never-selected` predicate. In every other case the normal route table runs. A selected dev pointer with `devSelectionSha256` reaches `restore-monitoring` (703-706). | Implemented as proposed |
| Never-selected refusal still immediate | Read 897-905; ran `ATerminalRestartedOperationSaysPreflightAgainAndRollbackRefusesAtOnce` | The refusal still throws before the `InstallationGate` is created (905), and before any record, intent or route work. The e2e test (flag, `original-stopped`, original selection) still refuses with "Nothing to roll back", leaves the installation and data snapshot unchanged and leaves no records. | Still immediate |
| Tests pin guard 1 and point 2 | Ran the [guard mutation probe](../evidence/analysis-013-guard-mutation-probe.ps1) ([results](../evidence/analysis-013-guard-mutation-results.json)). It runs seven harness restore scenarios against the reviewed script and two scratch mutants. | Reviewed script: all 7 pass.<br><br>`mutant-600-guard1` (the round-012 condition, `original-stopped` only): only `stopped-tray-prepared-retryable` fails (`stillRetryable`, `noTerminalFlag`).<br><br>`mutant-552-retention` (retention by `restartLauncher` property presence): only `stopped-tray-leftover-launcher-property` fails (`saysStopped`, `recordsRemoved`). | Each is pinned by its own scenario |
| Test pins guard 2 | Read `RollbackOfASelectedSwitchIgnoresAStaleTerminalFlag` (321-340) and ran it | The test does a real Switch, adds the flag and runs Rollback. It asserts success, `running-original` and the original selection bytes. A flag-only refusal (the round-012 form) would refuse here, which the developer's mutation run also reports. In this fixture both the state and the selection are outside the never-selected set, so the test pins guard 2 as a whole, not each part separately. | Pinned as a whole. I did not mutate this e2e test myself, because its script path is fixed to the repository. |
| Focused tests | `dotnet test -c Release --no-build --no-restore --filter` for the two e2e tests and the `InstallerFunctionsReleaseEveryResourceAndKeepThePrimaryError` theory ([output](../evidence/analysis-013-focused-tests.txt)) | 37 passed, 0 failed. The test assembly (14:30:46) is newer than every changed source file. | Passed |
| ANALYSIS-200/201 routes | Read 690-706 and 1162-1171 | `Get-RecoveryRoute` is unchanged. Restore still runs only when `!selectionMutated -and !monitoringMutationStarted` (1166). Both pointer-intent routes still set `monitoringMutationStarted` (925, 1134). The changed refusal throws before any intent work. | No regression |
| Restart fence | Read 476-519 | `Start-SelectedTray` still sets `restartLaunched` only after `Process.Start` returns, and `Assert-OwnedRestart` is unchanged. | No regression |
| 3 s launch-gate budget | Located the changed lines relative to `Open-LaunchGate` and `Invoke-GuardedPointerStep` | Both changes run outside the launch gate. Guard 2 runs before the update gate (897), and guard 1 runs in Restore, after `finally` released both gates (1164, 1170). | Nothing new under the launch gate |
| Claude/Codex parity | Inspection | The changes are in the shared installer, which serves both clients. They add no provider-specific branch or policy, and the skill copies are outside the delta. | No parity change |
| Fixture bytes | `grep -naP '[\x00-\x08\x0B\x0C\x0E-\x1F]'` on the three changed files, and the same check on the harness at `84dac98` | Harness lines 377, 383 and 396 contain U+000C and U+0008 where `\f` and `\b` were interpreted. The baseline harness has none, and `file` reports "with overstriking". | ANALYSIS-650 |
| Format (scoped) | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include` on the three `.cs` files in the target | Exit code 0 | Passed |
| Build, full suite, native runs | Not run | QA owns these, and the handoff reports a Release build with 837 tests passing. The launch and drain code is unchanged. | Not performed |

## Findings

### ANALYSIS-650: The new harness scenarios contain control characters in the fixture executable path

- Severity: suggestion
- Related task / requirement: the regression coverage for ANALYSIS-600 and ANALYSIS-552 added in Implementation 013
- Locations: `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1` lines 377, 383 and 396, at the reviewed identity
- Observation:
  - The three new `executable = '...'` literals contain the bytes `C:` U+000C `ixture` U+0008 `ell-tower.exe` instead of `C:\fixture\bell-tower.exe`. This is the form every older scenario in the same file uses.
  - The likely cause is that an escape-processing write turned `\f` and `\b` into control characters.
  - The baseline file has no such bytes, and `file` now reports the harness as "ASCII text, with overstriking".
  - The value is inert in these scenarios: `Restore-StoppedTrayAfterFailure` never reads `executable`, and `Start-SelectedTray` is stubbed. The probe confirms that all checks pass on the reviewed script.
  - However, the file now holds unintended raw control bytes. They show up as invisible or garbled text in diffs and editors, and a later scenario copied from these lines would inherit a bad path.
- Proposed improvement and benefit: replace the three literals with `'C:\fixture\bell-tower.exe'`, matching the neighboring scenarios. This restores a plain-text test file.
- Behavior-preservation risks: none. These are fixture data the code under test does not read.
- Evidence type: measured result (a byte scan)
- Verification / measurement plan: rerun the `grep -naP '[\x00-\x08\x0B\x0C\x0E-\x1F]'` scan on the harness and expect no output, then rerun the two affected harness scenarios.
- Relationship: new in the Implementation 013 delta. It does not reopen ANALYSIS-600 or ANALYSIS-552.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-600 | identity above | **Guard 1:** matches the proposal. The probe shows `stopped-tray-prepared-retryable` passes on the reviewed script and fails on the round-012 condition.<br><br>**Guard 2:** matches the proposal. The refusal now uses the never-selected predicate, and `RollbackOfASelectedSwitchIgnoresAStaleTerminalFlag` passes. The immediate refusal for a never-selected manifest still happens before the gate, and its e2e test passes.<br><br>**Residual inside the proposal:** guard 1 still saves the flag with state `prepared` when `$untouched` is false. In state `prepared` that can only happen if a foreign actor created `versions/<candidate>` after Preflight, because the snapshot and the copy happen only after `original-stopped`, and Preflight refuses an existing target (856). The trigger also needs the stop timeout and the exit race. Guard 2 removes the consequence Analysis 012 reported, because a later Rollback of a selected switch now proceeds. One harmful path remains: a second Switch that also fails and whose restart fails. It reports "Recover with Rollback", but Rollback then refuses as "Nothing to roll back" while the tray is stopped. That needs four coincident faults plus foreign interference. It is recorded for triage, not as a new finding. | Verified for the reported trigger and its consequence. A narrow residual is noted. |
| ANALYSIS-552 point 2 | identity above | `stopped-tray-leftover-launcher-property` runs a Rollback with a `restartLauncher` left over from an earlier phase and a failure before launch. It passes on the reviewed script: records removed, "Monitoring is stopped", no "may be running unverified". It fails on the property-presence mutant (`saysStopped`, `recordsRemoved`). This is the distinguishing test Analysis 012 asked for. | Verified |
| ANALYSIS-200 / ANALYSIS-201 (regression) | identity above | Routes, mutation flags, the Restore precondition and the guarded pointer steps are unchanged. The changed refusal throws before any record or intent work, and when it does not throw, control goes to the same route table. | No regression |

## Assessment and limitations

- Covered:
  - the full Implementation 013 delta: both ANALYSIS-600 guards, the two new harness scenarios and the new e2e test;
  - a regression view of the ANALYSIS-200/201 routes, the restart fence, the launch-gate budget and Claude/Codex parity.

  Every check covers both clients, because they share the installer.
- Results:
  - Both guards are implemented as proposed, and the never-selected terminal refusal is still immediate.
  - Guard 1 and ANALYSIS-552 point 2 are each pinned by a harness scenario that fails on the matching mutant.
  - Guard 2 is pinned as a whole by the e2e test. Its state and selection parts are not pinned separately.
- New finding: ANALYSIS-650 (suggestion). It is a fixture byte defect with no behavioral effect.
- Residual for the orchestrator: the ANALYSIS-600 corner described under Re-verification, where a foreign `versions/<candidate>` exists while the state is `prepared`. It lies inside the guard I proposed, not outside the implementation. It needs four coincident faults plus foreign interference. Whether it warrants a change, such as treating `prepared` as retryable regardless of `$untouched`, is a triage decision. I do not raise it as a new suggestion in this recheck.
- Deferred findings: ANALYSIS-554 and ANALYSIS-555 were not touched by this delta and were not made worse.
- Not performed: build, full suite and native runs (QA owns them, and the handoff reports them). I did not mutate the guard 2 e2e test myself.
- Writes:
  - `evidence/analysis-013-identity-before.txt` and `evidence/analysis-013-identity-after.txt`
  - `evidence/analysis-013-guard-mutation-probe.ps1` and `evidence/analysis-013-guard-mutation-results.json`
  - `evidence/analysis-013-focused-tests.txt`
  - `reports/analysis-013.md` was refused by the client, so this report text is inline.

  The probe kept its mutants and harness roots only in the session scratchpad. It started only harness `pwsh` processes, which exited when released. The focused tests used their own disposable fixtures. No tray, provider settings or user data directory was touched, and the review identity is unchanged.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (delegated subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 013 recheck of Implementation 013; ANALYSIS-600 verified for the reported trigger (narrow residual noted for triage); ANALYSIS-552 point 2 verified; ANALYSIS-200/201 no regression; new ANALYSIS-650 (suggestion, control bytes in harness fixtures); ANALYSIS-554/555 deferred, not made worse
changed or report paths: evidence/analysis-013-identity-before.txt, evidence/analysis-013-identity-after.txt, evidence/analysis-013-guard-mutation-probe.ps1, evidence/analysis-013-guard-mutation-results.json, evidence/analysis-013-focused-tests.txt; reports/analysis-013.md: write refused, text inline
checks: scripts/review-identity.ps1 before/after -> passed (identical); guard mutation probe -> run (reviewed 7/7 pass; guard-1 mutant fails only prepared-retryable; retention mutant fails only leftover-launcher-property); focused dotnet test (2 e2e + harness theory) -> passed (37/37); scoped dotnet format --verify-no-changes -> passed; control-byte scan -> 3 lines flagged (ANALYSIS-650); build/full suite/native -> not run (QA owns them)
target identity: before: head=84dac98 baseline=84dac98 files=6 diff=19aea83cdd82 untracked=0/e3b0c44298fc; after: identical
limitations / decisions needed: the client refused the report write, so the orchestrator should save the text above; guard 2 is pinned as a whole, not per part; I did not mutate the guard 2 e2e test myself; triage the ANALYSIS-600 foreign-candidate-directory residual and ANALYSIS-650
next: orchestrator saves the report and triages ANALYSIS-650 and the residual with QA 013
```
