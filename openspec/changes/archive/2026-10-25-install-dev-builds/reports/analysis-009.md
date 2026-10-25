> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 009). Client: Claude Code 2.1.310, delegated subagent (auto mode), model claude-opus-5-5 (role default). The client refused the reviewer's own write with: `Subagents should return findings as text, not write report files. Include this content in your final response instead.` This is the known restriction recorded in docs/agent-workflow.md. The evidence files under `../evidence/analysis-009-*` were written by the reviewer itself.

# Analysis report: install-dev-builds / 009 (light recheck of ANALYSIS-400, ANALYSIS-401, QA-400)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310, delegated subagent (auto mode), `claude-opus-5-5` (role default)
- Date: 2026-10-25T10:54Z (start) to 2026-10-25T11:01Z
- Change root / assignment: `D:\Code\ZeusApps.BellTower\openspec\changes\install-dev-builds`, Analysis 009. This is a light recheck of ANALYSIS-400 and ANALYSIS-401 ([analysis-008](analysis-008.md)) and of the QA-400 correction ([qa-008](qa-008.md)), plus a regression check of QA-350 and ANALYSIS-200/201 ([analysis-007](analysis-007.md)). Handoff: [implementation-009](../evidence/implementation-009.md).
- Task and requirement references: task 4.1, and design decision 4 with its restart-marker handshake bullet ([design.md](../design.md) line 105)
- Scope and relevant neighboring paths:
  - `scripts/install-dev.ps1`:
    - main `try`/`finally`: lines 810, 1060 and 1061-1066
    - restart and final ownership loop: lines 1067-1088
    - `Test-TrayProcessEnded`: lines 386-402
    - `Get-InstallationTrayStarts`: lines 403-439
    - drain callers: lines 267-277 and 440-454
    - `Assert-OwnedRecordPair`: line 151
    - `Get-RollbackRoute`: line 664
  - `src/BellTower/IO/ProcessProbe.cs`
  - `src/BellTower/Updates/UpdateTransaction.cs` lines 16-33
  - `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs` lines 39-41, 241-260 and 263-289
  - `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1` lines 248-269
- Baseline commit: `b74a12b`
- Implementation identity: `Summary: head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/9452e318bad9` ([before](../evidence/analysis-009-identity-before.txt), the full manifest and hashes). This matches the assignment.
- Before/after identity: the after run is identical, with 0 differing lines ([after](../evidence/analysis-009-identity-after.txt)). No unexpected mutation.
- Prior reports / rechecked findings: ANALYSIS-400 and ANALYSIS-401 ([analysis-008](analysis-008.md)); QA-400 and the QA-350 regression ([qa-008](qa-008.md)); ANALYSIS-200 and ANALYSIS-201 regression only ([analysis-007](analysis-007.md)). `scripts/install-dev.ps1` is untracked, so there is no Git diff from round 008 to round 009. The round-009 delta was located from the handoff and the symbol references.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-400: `$reservationReleased` gate | Traced every exit of the main `try` (lines 812-1060) | `$reservationReleased = $true` (1060) is the last statement of the `try`, after the gate and mutex disposal. Any throw before it leaves the flag false. Statements outside the `try` (1067-1088) run only after the `finally`. The condition at 1065 is the round-007 condition (quoted in analysis-008) with `!$reservationReleased` added. | Cleanup now happens only on failure. Pre-mutation failures with `$coordinationPlan` set still remove the run's own records through `Remove-OwnedCoordinationRecords`, which keeps the byte and tuple check. `$recordsRemovedWithoutMutation` is gone, and 1083-1088 are unconditional again. |
| ANALYSIS-400: restart fence on `never-selected` | Followed the success path: 989-996, then 1054-1060, then the `finally` (no cleanup), then 1078 `Start-SelectedTray` (activation = plan Id, 469), then `Assert-OwnedRestart`, then 1083-1088. Checked `RestartPending` and `CanStartTray` (UpdateTransaction.cs 16-33). | `pending.json` and `restart.json` (target = original, owner = this installer) exist while the original tray starts. A tray start without the activation is therefore fenced, and the operation's own start is admitted and verified. This route now matches every other successful route. | Fence restored. On a later failure (for example in `Start-SelectedTray`), the records stay. State is `restored-monitoring-snapshot` with the original selection, so a same-manifest Rollback takes `original-retry` (line 678). Once the installer has exited, `RestartPending` is false. |
| QA-350 regression | Source trace plus the positive test (Recovery.cs 241-260) | On the success path the records reach the loop at 1083, so the "record disappeared" throw cannot fire for this route. The developer's mutation check (dropping `!$reservationReleased` fails this test) is consistent with the trace: the `finally` would delete the records and 1084 would then throw. | Still fixed in source. Tests were not run here (QA owns them). |
| ANALYSIS-401: negative test | Read Recovery.cs 263-289 against `Invoke-GuardedPointerStep` (355-385) | The refusal text `Could not find file ... restart.json` is the `FileNotFoundException` from `Open-RecordGuard` (370-371). The test also asserts that `pending.json` is kept and the dev selection is byte-identical. If `!$monitoringMutationStarted` were dropped, the `finally` would call `Remove-OwnedCoordinationRecords`, and `Assert-OwnedRecordPair` would replace the primary error with "record is missing". The message assertion catches that, which matches the developer's mutation result. | The test now pins the guarded-step refusal and record retention on a route that mutated monitoring. As analysis-008 noted, tightening the assertions is enough once the flag is gone. |
| Coverage of the other side of the gate | Searched the script tests for record-absence assertions after a refusal | No test directly asserts that a pre-mutation refusal leaves no records for this run. | This gap predates the round and was not widened. The new conjunct cannot be true on any failure path (structural, see row 1). Not raised as a finding (recheck scope). |
| QA-400: `Test-TrayProcessEnded` decision table | Inspected 386-402 against .NET `Process` behavior and probed PowerShell getter semantics ([getter probe](../evidence/analysis-009-getter-probe-results.json)) | `ArgumentException` from `GetProcessById` returns true only when the PID is absent: on an access-denied PID .NET falls back to enumeration, and the probe saw `GetProcessById` succeed for csrss, lsass, services and wininit. `HasExited` after `Refresh` is true only for a terminated process. On access denial, PowerShell turns the getter's `Win32Exception` into `$null` (falsy) rather than reaching the `catch`. `ProcessProbe.Identify` (C#) then returns null, and the helper returns false. A missing CIM `CreationDate` returns false. Any other exception returns false. | An access-denied or otherwise unreadable live process is never accepted (refuse). The only skip for a live PID is the start-time mismatch. See the next row. |
| QA-400: PID reuse and the 10-tick correlation | Source reasoning plus read-only probes: [correlation probe](../evidence/analysis-009-cim-correlation-probe.ps1) ([results](../evidence/analysis-009-cim-correlation-results.json)), [historical DMTF probe](../evidence/analysis-009-dmtf-historical-probe.ps1) ([results](../evidence/analysis-009-dmtf-historical-results.json)), [raw DMTF probe](../evidence/analysis-009-dmtf-raw-probe.ps1) ([results](../evidence/analysis-009-dmtf-raw-results.json)), [CIM_DataFile probe](../evidence/analysis-009-dst-datetime-probe.ps1) ([results](../evidence/analysis-009-dst-datetime-results.json)) | A genuinely reused PID is classified correctly. Process A's PID was held at the CIM snapshot, so a different process at that PID has a different start time and A has ended. Measured: 199 of 199 comparable processes agreed within 9 ticks; all CIM values were `Local` kind. `Win32_Process` emits a definite offset (`+060`). A historical datetime encoded with the DST bias (`Win32_OperatingSystem.InstallDate`, `+120`) converted to UTC with 0 s error. `CIM_DataFile` emits an unknown offset (`+***`) and is skewed by a constant 1 h, so it is not a valid proxy. | The same 10-tick comparison is now used as proof of reuse, not just as a refusal. A live, same-identity process whose CIM and live start times disagreed by more than 10 ticks used to be refused and is now skipped. The measurements show no such disagreement except, possibly, the unmeasured ambiguous DST fall-back hour. Recorded as ANALYSIS-450 (suggestion). |
| QA-400: harness scenarios | Read harness 248-269 and InlineData 39-41 | `reused` takes the new `catch` → helper → skip path: the live harness process, `CIM and live image differ`, CIM time 1 h earlier. `unreadable-live` takes `catch` → helper → refuse (exact time). `ended` reaches the pre-existing `catch [ArgumentException] { continue }` at 411, not the new `catch`; only the helper is called directly on the ended PID. | The scenarios pin both mutations the developer reports (always-skip fails `unreadable-live`, never-skip fails `reused`). The exit-during-inspection branch through the `catch` is covered only by calling the helper directly, which the handoff states. Adequate for a light recheck. |
| ANALYSIS-200/201 regression | Re-read the intent precondition (820-823), route classification and `monitoringMutationStarted` sites (825-829, 1034), the guarded steps (976-981, 1006-1009), `Complete-RollbackPointer` and the drain placement (267-277) | Unchanged apart from line shifts. The drain still runs outside the launch gate. The helper adds at most one `GetProcessById` and one `Identify` per failing item, with no lock held. The unconditional loop at 1083-1088 restores the round-007 behavior for every route. | No regression found |
| Parity | Shared path inspection | Installer and drain are shared by both clients. The tray restart fence (`CanStartTray` through `TrayController` and `AgentLauncher`) applies to Claude Code and Codex sessions alike. | No provider-specific behavior and no parity gap |
| Duplication / performance | Not applicable | | Not performed (light recheck) |

## Findings

### ANALYSIS-450: The reuse proof in `Test-TrayProcessEnded` shares the CIM start-time correlation that the acceptance check relies on, so a disagreement now skips a live process instead of refusing

- Severity: suggestion
- Related task / requirement: 4.1, QA-400 correction ("skip only when it is proven to have ended; refuse otherwise", line 433)
- Locations: `scripts/install-dev.ps1` lines 398-399 (helper), 420-422 (acceptance check) and 432-436 (the `catch` that calls the helper); `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1` lines 252-253 (`reused` fakes reuse with a live process and a CIM time one hour earlier)
- Observation: Suppose a live process at the enumerated PID has the same identity as the CIM item, but its live start time and CIM `CreationDate` differ by more than 10 ticks. Then:
  1. The main path throws `CIM start identity differs` (422).
  2. The helper finds the PID alive, not exited, and the same live start time.
  3. The helper returns true at 399, and the process is skipped.

  Before QA-400, this case refused. The helper cannot tell "PID reused" from "same process, timestamps disagree". The `reused` harness scenario is exactly that case: a live process skipped because of a faked 1 h skew. The skip is sound only while CIM and `Process.StartTime` agree within 10 ticks for the same process.

  Measured on this machine: 199 of 199 processes agree within 9 ticks. A DST-era `Win32_OperatingSystem` datetime converts with 0 s error. So the residual exposure is the one ambiguous fall-back hour per year. It applies only if MMI builds the `Local` value from the wall clock without the ambiguity flag. That is a hypothesis; it could not be measured, because this machine booted after today's CET transition.

  Impact is low:
  - While the drain runs, the tray-instance mutex is held.
  - After release, the restart fence, now present on every route after ANALYSIS-400, and `Assert-OwnedRestart` still exclude an unactivated tray.
- Proposed improvement and benefit: Make the reuse proof independent of datetime conversion. Capture `[DateTime]::UtcNow.Ticks` immediately before the `Get-CimInstance` call in `Get-InstallationTrayStarts` and pass it to the helper. At line 399, accept reuse only when the live start time is later than that captured time (as well as differing by more than 10 ticks). This works because any process that took a PID which was held at the snapshot must have been created after the enumeration began. A long-lived same-identity process with a skewed CIM time then refuses again, as it did before QA-400. Genuine reuse during the drain is still skipped. Accepting the current fix as is is also reasonable, given the measurements.
- Behavior-preservation risks:
  - A backward step of the system clock between the capture and a reuse would refuse instead of skip. That is the safe direction and can at worst bring back a QA-400-style refusal.
  - The `reused` harness scenario would need a process started after the faked enumeration. For example, the fake `Get-CimInstance` can start a short-lived child and report its PID with an earlier `CreationDate`.
  - No change to the main acceptance path.
- Evidence type: source observation, measured correlation (current processes and a historical DST datetime), and a performance-independent correctness hypothesis for the ambiguous hour
- Verification / measurement plan:
  - If adopted, add a harness case: a live process with a skewed CIM time that started before the captured enumeration time must refuse. Keep the existing reuse case with a child started after capture.
  - Either way, a check of the ambiguous-hour behavior needs a disposable VM or a machine with a process created during a fall-back hour. Run [analysis-009-cim-correlation-probe.ps1](../evidence/analysis-009-cim-correlation-probe.ps1) there. Do not change the maintainer's time zone.
- Relationship: residual assumption of the QA-400 correction. Not a regression of ANALYSIS-200/201 or ANALYSIS-400.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-400 | identity above | `$recordsRemovedWithoutMutation` removed. The cleanup at 1065 is gated on `!$reservationReleased`, which is false on every exception path and true only on success. The never-selected success path keeps both records through `Start-SelectedTray` (1078). The ownership loop and owned removal (1083-1088) are unconditional again. The restart fence (`RestartPending`/`CanStartTray`) is therefore active on this route. | Verified in source. A native check that `restart.json` exists while the original tray restarts was not performed. |
| ANALYSIS-401 | identity above | Recovery.cs 263-289 now asserts the guarded-step refusal text, the kept `pending.json` and the unchanged dev selection. The flag it could not exercise no longer exists. | Verified in source. Not run here. |
| QA-400 | identity above | The helper accepts only an absent PID, an exited process, or a live start time differing by more than 10 ticks from CIM. Access-denied and unreadable live processes refuse (getter probe and source). The harness pins both directions. | Correction verified, with the residual assumption recorded as ANALYSIS-450. Root cause of the original flake not reproduced (not attempted). |
| QA-350 (regression) | identity above | The success path reaches 1083 with both records present. The positive test plus the developer's mutation check are consistent with the trace. | Still fixed in source |
| ANALYSIS-200 / ANALYSIS-201 (regression) | identity above | Routes, intent checks, `monitoringMutationStarted` sites, guarded steps and drain placement are unchanged. No new work runs under the launch gate. | No regression found |

## Assessment and limitations

- Scope: only the round-009 delta and its interactions. ANALYSIS-400 is implemented as recommended: the never-selected Rollback keeps its records and the restart fence through the tray restart, and every pre-mutation failure still cleans up its own records. ANALYSIS-401 is satisfied by the tightened assertions. The QA-400 skip cannot accept an access-denied or unreadable live process, and it classifies a reused PID correctly. One residual dependency remains: CIM and live start-time agreement, now used in the fail-open direction (ANALYSIS-450, suggestion, low impact). Its exposure is limited to a narrow, unmeasured DST edge case.
- Not performed:
  - build and tests (QA owns them)
  - native tray restart
  - reproduction of the original QA-400 flake
  - an ambiguous-DST-hour measurement (no such process exists on this machine)
- Writes:
  - `evidence/analysis-009-identity-{before,after}.txt`
  - four read-only probe scripts and their result files under `evidence/analysis-009-*`; they record aggregate counts and offsets only, with no process names, paths or command lines
  - this report, which the client refused to write and which is returned inline

  No other files were touched. The review identity is unchanged.
- Decision for the orchestrator: adopt the ANALYSIS-450 hardening, or accept the current QA-400 correction on the measured evidence.

```text
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (delegated subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 009; rechecked ANALYSIS-400, ANALYSIS-401, QA-400 (verified in source), QA-350, ANALYSIS-200, ANALYSIS-201 (no regression); new ANALYSIS-450 (suggestion)
changed or report paths: evidence/analysis-009-identity-{before,after}.txt; evidence/analysis-009-cim-correlation-probe.ps1 + results; evidence/analysis-009-dst-datetime-probe.ps1 + results; evidence/analysis-009-dmtf-raw-probe.ps1 + results; evidence/analysis-009-dmtf-historical-probe.ps1 + results; evidence/analysis-009-getter-probe-results.json; reports/analysis-009.md: write refused, text inline
checks: scripts/review-identity.ps1 before/after -> passed (identical, 0 differing lines); read-only CIM/process probes -> run (results linked in the report); build/test -> not run (QA owns them); native tray restart -> not run (light recheck)
target identity: before: head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/9452e318bad9; after: identical
limitations / decisions needed: client refused the report write with "Subagents should return findings as text, not write report files. Include this content in your final response instead."; the report text is inline for the orchestrator to save. Decision: adopt the ANALYSIS-450 hardening or accept the current QA-400 correction on the measured evidence. The ambiguous-DST-hour case was not measured.
next: orchestrator saves the report and triages ANALYSIS-450; QA's functional recheck of round 009 covers the tests.
```
