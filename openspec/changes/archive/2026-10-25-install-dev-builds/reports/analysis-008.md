> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 008). Client: Claude Code 2.1.310, delegated subagent (auto mode), model claude-opus-5-5 (role default). The client refused the reviewer's own write with: `Subagents should return findings as text, not write report files. Include this content in your final response instead.` This is the known restriction recorded in docs/agent-workflow.md. The evidence files `../evidence/analysis-008-identity-{before,after}.txt` were written by the reviewer itself.

# Analysis report: install-dev-builds / 008 (QA-350 fix light recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310, delegated subagent (auto mode), `claude-opus-5-5`
- Date: 2026-10-25T10:29:48Z (start)
- Change root / assignment: `D:\Code\ZeusApps.BellTower\openspec\changes\install-dev-builds`, Analysis 008 (light regression recheck of the QA-350 fix)
- Task and requirement references: task 4.1, design decision 4 and the restart-marker handshake bullet ([design.md](../design.md) line 105), [QA-350](qa-007.md), [implementation 008](../evidence/implementation-008.md)
- Scope and relevant neighboring paths: `scripts/install-dev.ps1` (flag at lines 790, 1044, 1062-1068; neighboring try/finally at 789-1045, `Get-RollbackRoute`, `Complete-RollbackPointer`, `Remove-OwnedCoordinationRecords`, `Start-SelectedTray`); `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs` lines 237-282; restart-fence consumers `src/BellTower/Updates/UpdateTransaction.cs` lines 16-33, `src/BellTower/Tray/TrayController.cs` line 21, `src/BellTower/Tray/TrayHost.cs` line 9, `src/BellTower/Sessions/AgentLauncher.cs` line 21
- Baseline commit: `b74a12b`
- Implementation identity: `Summary: head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/914583263da8` ([before](../evidence/analysis-008-identity-before.txt)); matches the assignment
- Before/after identity: after run identical ([after](../evidence/analysis-008-identity-after.txt)); no unexpected mutation
- Prior reports / rechecked findings: [analysis-007](analysis-007.md) (ANALYSIS-200, ANALYSIS-201 as functionally verified in [qa-007](qa-007.md)); QA-350 target. `scripts/install-dev.ps1` is untracked, so the 007-to-008 delta was located from the implementation note and the flag references, not from a Git diff.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Where the flag can be set | Traced every route that can finish the main `try` with both `selectionMutated` and `monitoringMutationStarted` false | Switch sets `selectionMutated` inside the guarded pointer step (958) before any successful exit. Rollback: `original-retry` (979) and `pointer-intent-destination` (985) set `selectionMutated`; `pointer-only` and `pointer-intent-source` set `monitoringMutationStarted` before takeover (805-808) and `Complete-RollbackPointer` sets `selectionMutated` (634); `restore-monitoring` sets `monitoringMutationStarted` at 1014 or earlier at 805-808 | On a successful run the flag is set only for the `never-selected` route. On a failure run it is also set, but the exception carries on out of the `finally`, so lines 1046-1073 never run. When `Remove-OwnedCoordinationRecords` itself fails in the `finally`, the flag stays false and the records are kept. This is correct for the QA-350 symptom. See ANALYSIS-400 for the record lifetime it accepts. |
| Ownership refusal on the flagged route | Inspected the order of the gate release (1036), the `finally` cleanup (1044) and the restart (1057) | `Remove-OwnedCoordinationRecords` → `Assert-OwnedRecordPair` still checks bytes and the tuple once after the gate is released, then deletes. The original tray is restarted afterwards with no `restart.json`. | The byte and tuple refusal is kept. The restart fence and the post-restart ownership check are absent on this route only: ANALYSIS-400 |
| ANALYSIS-200/201 interaction | Checked the intent precondition (800-803), route classification order, `monitoringMutationStarted` initialisation (791) and the `selectionMutated` sites | `Get-RollbackRoute` returns the `pointer-intent-*` routes before `never-selected` (649-656), so the flagged route never carries a `rollbackPointerTransition`. The flag is never read inside the `try` or by the failure messages (`Describe-OperationProgress` line 308). | No adverse interaction. The refusals and retry routes verified in analysis-007 and qa-007 are unchanged. |
| Minimality | Compared with existing state | `$reservationReleased` (789, 1040) is written and never read, and it already separates success from failure | The change is small but adds a second state variable. A one-condition alternative exists (ANALYSIS-400). |
| Tests | Read both new tests and the harness | `Run` and `StartRun` always pass `-NoTray` (InstallDevScriptTests.cs:428, Recovery.cs:459). The negative test uses the `restore-monitoring` route and fails inside the guarded pointer step. | Positive test: adequate for the exit-code symptom. Negative test: does not exercise the flag-gated lines: ANALYSIS-401 |
| Parity | Shared path inspection | Installer path shared by both clients. Tray auto-start through `AgentLauncher` applies to Claude Code and Codex sessions alike. | No provider-specific behavior |
| Performance / duplication | Not applicable | | Not performed (light recheck) |

## Findings

### ANALYSIS-400: The never-selected Rollback restarts the original tray after its restart marker was removed; the flag makes this the accepted success path

- Severity: warning
- Related task / requirement: 4.1; design.md line 105 ("Coordinate tray startup with the existing updater restart-marker/activation handshake while the launch gate is free ... only clear operation-owned markers")
- Locations: `scripts/install-dev.ps1` lines 789-790, 1036-1044, 1057, 1062-1068 (reviewed identity above); `src/BellTower/Updates/UpdateTransaction.cs` lines 16-33; `src/BellTower/Tray/TrayController.cs` line 21; `src/BellTower/Sessions/AgentLauncher.cs` line 21
- Observation: On the `never-selected` route (QA-350's case) the `finally` deletes `pending.json` and `restart.json` at line 1044, after the gate was released at 1036 and before `Start-SelectedTray` at 1057. Every other successful route keeps both records through the restart. While they exist, `RestartPending` stops `TrayController.Start` (used by `bell-tower start` and the agent launcher) and `CanStartTray` admits only the tray that carries this operation's activation. At the end, the Id/Token loop (1063-1066) and the final owned removal (1068) run. With the fix, this route restarts the tray with no marker and skips the post-restart ownership check. Pre-fix, the restart ordering was the same and the run exited 1. The fix turns the unfenced restart into a success outcome instead of moving the record removal. Consequence: an agent launched through Bell Tower (either client) in that window can start a tray without the activation. The operation's own child then loses the tray-instance mutex, and `Assert-OwnedRestart` refuses with an activation mismatch. The run exits 1 with no records and state `restored-monitoring-snapshot`. Selection and data stay intact. A retry takes the `original-retry` route and meets the same foreign tray. The window is short, so the impact is low: a confusing failure, not data loss.
- Proposed improvement and benefit: Limit the no-mutation cleanup to the failure path and drop the new flag:
  `if (!$reservationReleased -and !$script:selectionMutated -and !$script:monitoringMutationStarted -and $script:coordinationPlan) { Remove-OwnedCoordinationRecords }`
  Then restore lines 1063 and 1068 to their unconditional form. A successful `never-selected` run then keeps its records through the restart and goes through the same Id/Token check and owned removal as every other route. QA-350 is still fixed, because the records exist when the final loop runs. The change also gives the existing dead `$reservationReleased` variable a use, which is smaller than adding a new flag. Pre-mutation refusals still clean up their records as they do now: `$reservationReleased` is false on every exception path, because nothing after line 1040 can throw inside the `try`.
- Behavior-preservation risks: If `Start-SelectedTray` fails on this route, the records are now kept. The manifest is already `restored-monitoring-snapshot` with the original selection, so a same-manifest Rollback takes `original-retry` and finishes, as for the other routes. After an installer crash, `RestartPending` is false (owner ended), so the kept records do not block a manual tray start. Switch is unaffected because it never succeeds without `selectionMutated`.
- Evidence type: source observation (not reproduced natively)
- Verification / measurement plan: Rerun the two new tests and the `InstallDevScriptTests` and `HookMaintenanceTests` filters. Natively, rerun `qa-007-native.ps1 -Cases rollback-never-selected` and additionally check that `updates/restart.json` exists while the original tray reports presence with this operation's activation, and that the run exits 0 with no records afterwards.
- Relationship: follow-up to QA-350. Not a regression of ANALYSIS-200/201.

### ANALYSIS-401: The negative test does not exercise the flag-gated record check

- Severity: suggestion
- Related task / requirement: 4.1, QA-350 regression coverage
- Locations: `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs` lines 259-282 (and 136-176 for comparison)
- Observation: `RollbackStillRefusesWhenItsRecordsDisappearOnAPathThatKeepsThem` runs the `restore-monitoring` route and deletes `restart.json` during the pointer-step gate wait. The refusal comes from `Assert-OwnedRecordPair` inside `Invoke-GuardedPointerStep` (about line 372), not from the post-restart loop at 1063-1064, which is the only code the flag changes. The test would still pass if the flag were set on every route. It asserts only `!Succeeded` and `state != running-original`: no message, no check that `pending.json` and the dev selection are preserved. Its scenario mostly repeats `RollbackRevalidatesRecordsAfterTheGateWaitAndARetryFinishesOnlyThePointer`, which is stricter. The positive test runs `-NoTray`, so neither test covers the restart window in ANALYSIS-400.
- Proposed improvement and benefit: Either assert the expected refusal text, the preserved `pending.json` and the unchanged dev selection, so the test pins a specific guard, or replace it with a case that reaches line 1063 on a non-flagged route, for example through a disposable barrier after the reservation release. If ANALYSIS-400 is adopted the flag disappears, and tightening the assertions is enough.
- Behavior-preservation risks: none (test only)
- Evidence type: source observation
- Verification / measurement plan: mutation check by inspection or a temporary local edit: force the flag to `$true` and confirm that the improved test fails.
- Relationship: related to ANALYSIS-400

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-350 (symptom: exit 1 after a correct never-selected recovery) | identity above | Source trace: on a successful `never-selected` run the flag is set at 1044 and lines 1063-1068 are skipped. The new positive test (Recovery.cs 237-257) asserts exit 0, `running-original`, no records and an unchanged selection under `-NoTray`. Tests were not run here (QA owns them). | Fixed by inspection, with ANALYSIS-400 open on how. Native recheck not performed. |
| ANALYSIS-200 / ANALYSIS-201 (regression only) | identity above | Flag unreachable on any intent, restored-monitoring or pointer route. Failure paths unchanged. | No regression found |

## Assessment and limitations

- Scope: the QA-350 delta and its interactions only. The flag cannot hide a missing or changed record on any route other than `never-selected`, and refusals verified in analysis-007 and qa-007 are unchanged. On the `never-selected` route, the fix accepts a tray restart without the restart marker and without the post-restart ownership check. That conflicts with design.md line 105, and ANALYSIS-400 gives a smaller alternative using the existing `$reservationReleased`. The negative test does not guard the changed lines (ANALYSIS-401).
- Not performed: build, tests and native reproduction (QA owns them). No 007-to-008 Git diff exists for the untracked script.
- Writes: `evidence/analysis-008-identity-before.txt` and `evidence/analysis-008-identity-after.txt` only. The write of `reports/analysis-008.md` was refused by the client, and this text is returned inline for the orchestrator to save. The review identity did not change.
- Decision for the orchestrator: whether to adopt ANALYSIS-400 before the maintainer Switch or accept the current fix as is.

```text
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (delegated subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 008; new ANALYSIS-400, ANALYSIS-401; rechecked QA-350, ANALYSIS-200, ANALYSIS-201
changed or report paths: evidence/analysis-008-identity-before.txt, evidence/analysis-008-identity-after.txt (written); reports/analysis-008.md (write refused, text inline)
checks: scripts/review-identity.ps1 before/after -> passed (identical); build/test -> not run (QA owns them); native reproduction -> not run (light recheck)
target identity: before: head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/914583263da8; after: identical
limitations / decisions needed: report write refused; the before-identity evidence file records only the summary line; scripts/install-dev.ps1 is untracked so there is no 007-to-008 Git diff; decision: adopt ANALYSIS-400 before the maintainer Switch, or accept the current fix.
next: orchestrator saves the report and triages ANALYSIS-400 and ANALYSIS-401.
```
