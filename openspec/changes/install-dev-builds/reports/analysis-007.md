> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 007). Client: Claude Code 2.1.310, delegated subagent mode, model claude-opus-5-5 (role default). The client refused the reviewer's own write with: `Subagents should return findings as text, not write report files. Include this content in your final response instead.` This is the known restriction recorded in docs/agent-workflow.md (Known client restrictions). The evidence files under `../evidence/analysis-007-*` were written by the reviewer itself.

# Analysis report: install-dev-builds / 007

## Review target

- Reviewer / role: delegated code-analyzer subagent, Analysis 007 / code-analyzer.
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), delegated subagent (Agent SDK), role default model, resolved `claude-opus-5-5`.
- Date: 2026-10-25T09:55Z (UTC).
- Change root / assignment: `D:/Code/ZeusApps.BellTower/openspec/changes/install-dev-builds`. Analysis 007 rechecks canonical ANALYSIS-200 and ANALYSIS-201 (QA-250 and QA-251 are duplicates) and runs a regression analysis of the round-007 correction.
- Task and requirement references:
  - [tasks](../tasks.md) 4.1, 4.3, 6.1 and 6.3
  - [design](../design.md) decisions 4 and 7
  - [development-builds spec](../specs/development-builds/spec.md)
  - [bounded plan 007](../evidence/implementation-007-plan.md)
  - [developer handoff 007](../evidence/implementation-007.md)
- Scope and relevant neighboring paths:
  - Focus: `scripts/install-dev.ps1`, `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1`, `tests/BellTower.Tests/Scripts/InstallDevScriptTests.cs`, `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs`, and the Recovery bullet in `docs/development.md`.
  - Neighbors read: `src/BellTower/IO/DataLock.cs` and `src/BellTower.Distribution/InstallationGate.cs`.
  - Checked by hash only: `tests/BellTower.Tests/Updates/HookMaintenanceTests.cs` and both `install-dev-build` skill copies.
- Baseline commit: `b74a12ba20eb9d3595204b41295b9dab73b7895d`.
- Implementation identity before analysis: `head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/0717273118a2`. This matches the expected line.
  - Tracked diff SHA-256: `1eaa17c65cb603c8d531200a603a27b762e3e716e3f921f5dfc2bc02a71ccf1b`.
  - `scripts/install-dev.ps1`: `9ba060a5…22a0`. Harness: `b00dcbcc…b200`.
  - Full manifest: [before](../evidence/analysis-007-identity-before.txt).
- Before/after identity: the [after](../evidence/analysis-007-identity-after.txt) output is byte-identical to the before output; `diff` reported no differences. There were no unexpected mutations. My evidence files lie outside the identity scope.
- Prior reports / rechecked findings: [Analysis 006](analysis-006.md), for ANALYSIS-200 and ANALYSIS-201 (QA-250 and QA-251).
- What changed since round 006, from per-file hashes compared with the [Analysis 006 after-identity](../evidence/analysis-006-identity-after.txt):
  - Changed untracked files: `scripts/install-dev.ps1` and `InstallDevScriptTests.cs`.
  - New files: `InstallDevFunctionHarness.ps1` and `InstallDevScriptTests.Recovery.cs`.
  - Unchanged: every other untracked hash, including `HookMaintenanceTests.cs` and both skills. The two skill copies are identical (`c9ada458…4190`).
  - Tracked files: the same 18 files. Their diff hash changed, which the handoff attributes to `docs/development.md` only. Without a per-file tracked baseline from round 006, I cannot confirm that no other tracked file changed.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| (a) Cleanup after partial acquisition or disposal failure | Traced `Close-Resources :284–293`, `Complete-ResourceScope :294–305`, `Enter-MonitoringCommit :319–354`, `Invoke-GuardedPointerStep :355–385`, `Save-SwitchMonitoringSnapshot :543–559` and the commit caller `:999–1025`. Checked them against `DataLock` and `InstallationGate` disposal. | Every lock, gate and guard is acquired inside a `try`. Its `finally` releases everything acquired unless ownership was transferred. The transfer flag `$transferred` (`:343`) is set only just before the complete bundle is returned. Each `Dispose` has its own catch, so a failed release does not skip the rest. Guards are released in reverse order, then locks in reverse order. The original error is rethrown and cleanup failures become warnings. If only cleanup fails, its first failure is raised. Busy, stale and timed-out paths release everything before `Start-Sleep` and before renewal. Switch's three blocking 5 s locks now follow the same pattern. The caller assigns `$commit` and enters its own `try` with nothing in between that can fail. | Corrected |
| (b) Revalidation after the launch-gate wait | Traced `Invoke-GuardedPointerStep` and its three callers: Switch `:955–960`, Rollback `Complete-RollbackPointer :626–640`, and the `pointer-intent-destination` route `:985–988`. | The helper runs these steps in order: (1) the outside boundary (renewal and drain); (2) the optional test barrier, still outside the gate; (3) the launch gate; (4) both read guards (`FileShare.Read`); (5) the 16 KiB size limit; (6) exact bytes and full tuple (`Assert-OwnedRecordPair`); (7) the 8-minute freshness check; (8) the expected selection hash; (9) the step itself. The gate and guards stay held through the pointer write and the manifest save. If the records are stale, everything is released and the loop renews outside the gate. No data lock is held while waiting for the gate. | Corrected in both Switch and Rollback |
| (c) Rollback route table and write-ahead intent | Compared `Get-RollbackRoute :643–663`, the classification before takeover `:797–809`, the reclassification after takeover `:965–966`, `Complete-RollbackPointer`, `Save-OriginalPointerPhase :618–623` and `Get-RollbackPointerBinding :564–587` with the plan's table. | Each row of the plan's table maps to one route. The crash-window walk-through is under ANALYSIS-201 below. Original pointer bytes with `restored-monitoring-candidate-pointer` and no intent are refused at `:659`. An intent whose records are missing is refused at `:799–802`. The intent and the original-pointer phase change in one atomic manifest write, so no saved state can contain both. | Corrected |
| (d) Carrying the intent's binding forward | Traced `Move-PointerIntentPublication :607–617`, called from `Replace-OwnedRecordPair :197` and `Resolve-InterruptedRecordTransition :257`. Checked the `publication` and `owner` conditions at `:601–602`. | Sound, and it accepts nothing new. Reasoning is under ANALYSIS-201 below. | Design decision accepted |
| (e) Live activation, launcher receipt and recheck just before the stop | `Get-ExpectedCandidateActivation :476–481`, `Assert-OwnedCandidate :482–508`, `Stop-VerifiedTray :125–134`, Switch's `candidateActivation` (set at `:929`, saved at `:936` before any stop), and the Rollback calls at `:872` and `:995–998`. | Before either ownership path is chosen, the live tray's version, data directory and activation must equal the activation recorded at Switch. On the receipt path, the receipt's activation must equal both the expected and the live activation. The existing checks on launcher PID, start time and image, tray identity, and ancestry are kept. `Stop-VerifiedTray` re-reads the owner and runs `Assert-SameOwner` and then the full candidate check just before `RequestExit`. `Read-Owner` is unchanged, so foreign or unverifiable owners are still refused. | Corrected |
| (f) Accuracy of phase messages | `Describe-OperationProgress :306–314` and its three uses (`:353`, `:384`, `:432`), cross-checked against where the flags are set (`:807`, `:957`, `:978`, `:984`, `:1013`, `:635`). | Every reachable combination of flags and state gives a true message: selection intact, monitoring restoration started, monitoring restored with the candidate pointer still active, or selection changed. The old drain message that claimed monitoring was left intact after restoration is gone. | Corrected |
| (g) 3 s hook budget and provider parity | Static review: `Open-LaunchGate` has one caller (`:369`), and I listed all work done under the gate. Searched the installer for provider names. Timed the under-gate Rollback step with a [proxy probe](../evidence/analysis-007-gate-work-probe.ps1) that runs the installer's real `Hash`, `Save-Json` and `Get-SelectionFields` on scratch files. | Under the gate there is only: selection bindings from two small files, three record or selection hashes, two manifest saves, one atomic small-file replace and one `ProcessProbe.Inspect`. The CIM drain, renewal, sleeps, snapshot hashing (`Assert-Snapshot :648`) and payload work all happen outside the gate. [Measured](../evidence/analysis-007-gate-work-results.json) over 25 runs with 60 payload entries: median 30.7 ms, first (cold) run 189.9 ms, which was also the maximum. The probe leaves out guard opens and `Inspect`, both well under a millisecond. The installer contains no provider name: Claude Code, Codex and mixed hooks all go through the same unchanged launcher path. `HookMaintenanceTests` is byte-for-byte unchanged. | Holds, by source review and the proxy measurement. Native hook timing during the new pre-gate barrier was not run (QA). |
| (h) Complexity and duplication | Inspection | About four lines (opening the guards, the size limit, the tuple check and the freshness check) appear in both `Enter-MonitoringCommit` and `Invoke-GuardedPointerStep`. `Get-RollbackPointerBinding` and `Assert-RollbackPointerIntent` run up to four times per invocation: before takeover, after takeover, and under the gate. Each repeat protects a different wait or takeover point, and the measured cost is tens of milliseconds. The fix adds no framework, dependency or new journal: just one manifest field and thin `Open-*` seams. Disposable barriers run only with `-Disposable` and a hold file. | Proportionate. Not a finding. |
| Constraints | Inspected timeouts, the manifest and the launcher. | No new assembly or package. Timeouts are unchanged: `Wait-Until` and the commit and pointer loops 30 s, `DataLock` 5 s, gate 10 s, freshness 8 min. No change to `src/BellTower.Launcher`, and `Assert-OriginalBytes` is unchanged. No provider-specific branch. | All kept |
| Harness and test quality | Read the harness and `Recovery.cs` in full. | The harness extracts function definitions from the real script's syntax tree and replaces only the seams. On the same thread, it checks ownership by calling `ReleaseMutex` and re-acquiring. The C# test then acquires every mutex and opens every record exclusively while the harness is still running, which proves the resources were released rather than abandoned. Every boundary observation asserts that nothing is held. The gate name, which the harness derives itself, is validated by `pointer-stale-retry`, which requires the gate to be held inside the step. The `-NoTray` script tests cover drift after the gate wait for both Switch and Rollback, a real interrupted-then-resumed retry, the three-point crash matrix, and 12 negative intent cases with byte-equality checks. | Adequate for source acceptance. The `candidate-*` harness cases use made-up owners, so the native presence-before-receipt negatives are still QA work. |
| Docs | Read the Recovery bullet in the `docs/development.md` diff. | It matches the behavior: resuming with the same manifest, no second monitoring restore once that step is recorded, the `rollbackPointerTransition` hashes, refusal of any other combination, and the live activation check both before and immediately before the stop. | Accurate |
| Read-only checks | `git diff --check` on the scoped tracked paths; PowerShell `Parser.ParseFile` on the installer and the harness; a search for provider names; identity before and after. | Exit code 0; no parse errors in either script; no provider names found; identity unchanged. | Passed |
| Build, tests, format, native trials | Not run | The host is low on memory, QA owns shared build outputs, and reviewers do not repeat the developer's runs. | Not performed |

## Findings

**No findings.** There are no new findings in ANALYSIS-350–399. The regression analysis found no defect in the correction and no complexity worth changing.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-200 (QA-250) | `diff=1eaa17c65cb6`, `untracked=16/0717273118a2` | See ANALYSIS-200 below | Verified in source. Native hook timing under the new pre-gate barrier is still QA acceptance. |
| ANALYSIS-201 (QA-251) | Same | See ANALYSIS-201 below | Verified in source. Not yet verified natively: the crash after the phase save, the live and receipt activation negatives, and the activation change before the stop (QA). |
| ANALYSIS-202, QA-200, ANALYSIS-001/QA-001, ANALYSIS-006/QA-150, QA-005 | Same | See retained regressions below | Corrections retained |

### ANALYSIS-200 (QA-250)

- Switch now writes the pointer only through `Invoke-GuardedPointerStep` (`:955`). After the possibly long gate wait, the step rechecks the exact record pair, freshness and the original selection hash. It holds the read guards through the write.
- In `Enter-MonitoringCommit` and `Save-SwitchMonitoringSnapshot`, every acquisition is inside the ownership scope. Cleanup tries to release every handle and keeps the original error.
- The drain, busy and freshness messages now match the actual phase.
- The harness shows that nothing stays held, both on the same live thread and from another process. It covers a throw from the second or third lock, a failed guard open, a failed validation and failed disposals.
- The developer also checked that the harness catches a real leak: with the `finally` removed, `setup,state` stayed held.

### ANALYSIS-201 (QA-251)

The fresh path (`:1028`) and the `pointer-only` and `pointer-intent-source` retries (`:990–993`) all go through `Complete-RollbackPointer`, which writes the pointer. Monitoring is not restored a second time, and the dev snapshot is not saved twice (the `savedDevMonitoring` check at `:1003`).

Crash windows and the route each one resumes through:

- **Before the intent is saved:** `pointer-only`.
- **After the intent is saved, before the pointer is written:** `pointer-intent-source`, which repeats only the guarded write.
- **After the atomic write, before the phase is saved:** `pointer-intent-destination`, which only commits the phase, under the gate and guards.
- **After the phase is saved:** `original-retry`.

Refusals:

- Original pointer bytes with no intent are refused.
- An intent whose records are missing is refused.
- `monitoringMutationStarted` is set on every route after monitoring restoration (`:804–808`), so failure cleanup keeps the records.

The live tray's activation is now compared with `candidateActivation`. Switch has to save this value because a later Rollback creates its own `coordinationId`. The comparison runs before the ownership path is chosen and again just before the stop. The `-NoTray` tests cover all four saved states and the 12 negative intent cases, each checking that bytes are preserved.

#### Decision (d): carrying the intent's binding forward

I find it sound, and it accepts nothing new. Five reasons:

1. **Only from a proven pair.** The binding moves only in `Move-PointerIntentPublication`, which first requires the intent to match the old plan and both old hashes exactly. That function runs only after the record pair has been proven, either by `Assert-OwnedRecordPair` in `Replace-OwnedRecordPair` or by the exact-state check in `Resolve-InterruptedRecordTransition`.
2. **Saved atomically with the new expectation.** The moved binding is written in the same atomic manifest save that commits the new record expectation. No saved manifest can hold an intent bound to records other than the committed pair or the recorded old pair of an interrupted transition.
3. **Nothing important changes.** Only the plan's `Created` and `Owner` fields can change:
   - Id and Token never change.
   - The target stays the original version, because the intent requires the Rollback phase.
   - The contract hash and `HadTray` are recomputed to the same values.

   The operation, path, phase, source, destination and snapshot bindings never change, and every check re-derives them from the manifest and files.
4. **A live owner is accepted only if it is this run's own plan.** The `owner` exception (`:602`) accepts a live owner only when it equals this run's own `$script:coordinationPlan`, which exists only after a validated takeover or a fresh creation. At the check before takeover (`:803`) that variable is null, so the old owner must be proven ended. The `live-owner` negative test confirms this.
5. **Needed for recovery, not just convenient.** Without carrying the binding forward, a failure after takeover and before the step re-saves the intent would leave the intent permanently out of step with the committed expectation. The check at `:803` would then refuse every later retry. That is broader than one crash window.

### Retained regressions

These are unchanged since Analysis 006 and still hold:

- ANALYSIS-202: `Read-Owner` treats presence as live, ended or unverifiable.
- QA-200: `HookMaintenanceTests`.
- ANALYSIS-001 and QA-001: the shared retention rule and the skill copies.
- ANALYSIS-006 and QA-150: short launch critical sections.
- QA-005: detailed drift messages.
- The coordination-transition machinery, which now also carries the intent.

## Assessment and limitations

Within this static scope, the round-007 correction closes the source gaps that Analysis 006 left open for ANALYSIS-200 and ANALYSIS-201. It does so without a new dependency, a timeout increase, a launcher edit or a separate provider policy. Slow work stays off the launch gate: the proxy measured about 31 ms median and 190 ms on a cold run. Claude Code, Codex and mixed sessions therefore keep one shared policy within the 3 s budget. This conclusion comes from source review plus a proxy measurement, not from a native hook trial.

Not performed by this analyzer, and remaining QA acceptance work (I do not claim any of it as passing):

- Release build, full or focused test suite, format verification, package and NativeAOT benchmark. These were avoided because of memory pressure, and QA owns them.
- The native disposable matrix, including the three cases the developer could not run natively:
  - the crash after the phase save;
  - the live and receipt activation negatives at the presence-before-receipt interval;
  - the activation change just before the stop.
- Native hook timing while the new `<boundary>-before-gate` barrier is held.
- A clean native explicit Retire.

The report write was refused by the client, so the text is inline. Evidence written:

- `evidence/analysis-007-identity-before.txt`
- `evidence/analysis-007-identity-after.txt`
- `evidence/analysis-007-gate-work-probe.ps1`
- `evidence/analysis-007-gate-work-results.json`

The probe touched only a scratchpad directory, and the source identity is unchanged. I did not touch the maintainer's installation, tray, data directory, provider settings or any user process, and I left the unrelated memory-heavy application alone. The orchestrator owns triage and ledger status; this report does not mark any finding closed.

```text
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 delegated subagent / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-200 (QA-250), ANALYSIS-201 (QA-251); regression of 4.1/4.3/6.1/6.3; no new ANALYSIS-350–399 findings
changed or report paths: reports/analysis-007.md (write refused by client; full text inline above); evidence/analysis-007-identity-before.txt; evidence/analysis-007-identity-after.txt; evidence/analysis-007-gate-work-probe.ps1; evidence/analysis-007-gate-work-results.json
checks: source recheck (a)–(h) -> passed (both canonical IDs verified in source); git diff --check -> passed; PowerShell parse of installer and harness -> passed; provider-name search -> passed (none); gate-work proxy probe -> median 30.7 ms, cold max 189.9 ms; build/tests/format/native -> not run (memory pressure; QA owns)
target identity: before and after head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/0717273118a2 (byte-identical)
limitations / decisions needed: report write refused ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), so the orchestrator should save the inline text to reports/analysis-007.md; native crash-after-phase-save, live and receipt activation negatives, activation change before the stop, native hook timing under the pre-gate barrier and a clean Retire are still unverified natively (QA); tracked-file changes since round 006 beyond docs/development.md cannot be independently confirmed
next: orchestrator saves the report and triages with QA; QA runs the outstanding native cases and the final acceptance suite when memory allows
```
