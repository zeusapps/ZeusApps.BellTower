> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-002); client: Claude Code 2.1.330; mode: subagent (non-interactive); model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: recover-unready-dev-operation / ANALYSIS-002

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-002, recheck) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent (non-interactive), model `claude-opus-5-5`
- Date: 2026-11-22 (UTC)
- Change root / assignment: `openspec/changes/recover-unready-dev-operation` / ANALYSIS-002 (recheck after DEV-002, fix cycle 1)
- Task and requirement references: tasks 1.1, 2.1-2.3, 3.1 ([tasks](../tasks.md)); requirement "Supported dev installation with rollback" ([spec](../specs/development-builds/spec.md)); design decisions 2-5 ([design](../design.md)); developer evidence [implementation-002](../evidence/implementation-002.md)
- Scope and relevant neighboring paths:
  - Rechecked in `scripts/install-dev.ps1` at the reviewed identity: `Format-RecordPath`, `Get-OperationDisposition`, `Find-OwningOperation`, `Get-OwningOperationHint`, `Assert-NoCoordinator`, `Add-NotReadyDiagnostics`, `New-NotReadyFailure`, `Assert-FinalizeOwnerEnded`, `Assert-PresentOwnedRecords`, `Remove-PresentOwnedRecords`, `Assert-FinalizePreconditionsHold` and `Invoke-Finalize`. Also the manifest byte read, the Switch and Rollback entry checks, and the new `-before-tray-restart` barrier.
  - Tests read: `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Finalize.cs` (the new DEV-002 tests); `InstallDevFunctionHarness.ps1` (`Test-SerializedPath`, the `not-ready-*` and `owner-unknown-*` scenarios); `InstallDevScriptTests.Recovery.cs` (the terminal-flag tests).
  - Docs and skills: `docs/development.md`, `docs/releases.md` and both `install-dev-build` skill copies.
  - Neighbors read: `Replace-OwnedRecordPair`, `Set-OwnedRecordsFresh`, `Invoke-GuardedPointerStep`, `Assert-OriginalBytes`, `Restore-StoppedTrayAfterFailure`, the Rollback recovery tuple and the record creation in the main flow.
- Baseline commit: `f4dfa4e`
- Implementation identity (before):
  - HEAD `f4dfa4e90009b90d0fa5e906289a541ba5789417`
  - Manifest:
    - `?` (untracked): `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Finalize.cs`
    - `M` (modified): both SKILL.md copies, `docs/development.md`, `docs/releases.md`, `scripts/install-dev.ps1`, `InstallDevBuildSkillTests.cs`, `InstallDevFunctionHarness.ps1`, `InstallDevScriptTests.Recovery.cs`, `InstallDevScriptTests.cs`
  - Tracked diff SHA-256 `74e04e2276e2c80409bf4d8010c5649ef684c8c71e08b4bc2bb4cae2381e07ad`
  - Untracked `bfba4fb70b0e0bbf2109c2dac9fdb51471f6940dded8c850596e0b2998a08427` (Finalize tests)
  - Summary `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596`
- Before/after identity: after the analysis, the same command printed `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596`. It is identical, so nothing changed unexpectedly.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md) (ANALYSIS-001, -002, -003, -005, -008, -009, -010); triage in [findings](findings.md). ANALYSIS-002 duplicates QA-001 ([qa-001](qa-001.md)).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Owner rule and re-checks under the gates (ANALYSIS-001) | Read `Assert-FinalizeOwnerEnded` (787-794), `Assert-FinalizePreconditionsHold` (814-824), `Invoke-Finalize` (825-902) and the manifest read (1268-1272). Compared with how records are renewed (1408-1410, 1413-1415). | <ul><li>The owner is checked before the gates (856) and again under the launch gate (816). An `Unverified` owner refuses.</li><li>Every Rollback, fresh or recovering, sets the plan owner to its own process (`Identify($PID)`). A live Rollback retry on the same manifest is therefore caught too.</li><li>Before the first write, under the gates, it re-checks `current.json`, the manifest bytes (against the hash of the one read), the records and the original payload bytes.</li></ul> | Verified |
| Single-record retry (ANALYSIS-002 / QA-001) | Read `Assert-PresentOwnedRecords` and `Remove-PresentOwnedRecords` (795-813), the retry branch (828-855) and the retry tests (Finalize.cs 245-298). | <ul><li>Each remaining record must have its path's recorded hash, and its parsed plan must equal the recorded plan.</li><li>A lone record is refused while the manifest is not finalized (848).</li><li>A foreign or changed lone record is refused and its bytes are kept. Tests cover both paths and both kinds.</li></ul> | Verified |
| Launch gate held only for the short effect (ANALYSIS-003) | Read 867-895 and compared with `Invoke-GuardedPointerStep` (585-615). Timed `Get-FileHash` over a disposable 0.1.7 payload under `artifacts/` (16 files, 176 MB, read-only). | <ul><li>The drain runs under the update gate and the reservation only. `Open-LaunchGate` (877) comes after the drain.</li><li>The PIDs for a refusal are scanned only after every gate is released (895).</li><li>The launch gate covers the re-checks, including `Assert-OriginalBytes`, as ANALYSIS-001 recommended.</li><li>Hashing measured about 3.2 s cold and about 150 ms warm. Here the cache is warm, because the prelude (1282) has just hashed the same files.</li></ul> | Verified. The warm hold time was measured; hook waits under real contention were not. |
| Disposition rule (ANALYSIS-005) | Read `Get-OperationDisposition` (216-256), its four callers (837, 1286-1294, 279, 306) and the old predicates in the baseline diff. Probe: extracted the function through the PowerShell AST and ran it on a completed-Rollback manifest, with the records absent and paths under a nonexistent `artifacts/` folder. | <ul><li>Finalize, the Rollback entry and the readiness failure now agree.</li><li>A terminal Switch with `trayRestartedAfterFailure` says to run Preflight again (Finalize.cs 376-395).</li><li>An inner restart miss for a candidate names Rollback (harness `not-ready-incomplete-rollback`).</li><li>The probe classifies a completed Rollback as `finalizable`.</li></ul> | Verified for the two reported cases. Regression ANALYSIS-100 |
| Path detector (ANALYSIS-008) | Read `Test-SerializedPath` (harness 49-52) and its uses (459, 462). | It searches the JSON-escaped and forward-slash forms of the path, over the serialized facts only. An injected path is detected; a path-free reason is not. | Verified |
| Relative record path and "could not be stored" (ANALYSIS-009) | Read `Format-RecordPath` and its callers. Grepped every message that embeds `$OperationManifest`. | <ul><li>The readiness, unknown-owner, disposition and Switch-hint messages print the record path relative to the repository.</li><li>A failed save says the facts "could not be stored" (`not-ready-save-fails`).</li><li>`Restore-StoppedTrayAfterFailure` (1113) still embeds the absolute path. That message wraps a readiness failure when its own restart misses the deadline (1151-1153).</li></ul> | Verified for the helpers. Residual ANALYSIS-101 |
| `Find-OwningOperation` (ANALYSIS-010) | Read 257-298, the harness `owner-unknown-names-owning-operation` scenario and Finalize.cs 397-429. | <ul><li>It needs both records, with equal ID and token, plus a workspace record whose `coordinationId` and `coordinationToken` both equal theirs.</li><li>It is read-only and never throws.</li><li>The half-match case is exercised by the second harness check (`namesNothingWhenNoRecordMatches`), where the stale record is the only one with a matching ID. The first check alone could not exercise it, because `owning` sorts before `wrong-token`.</li></ul> | Verified |
| Regression: accepting or naming records the operation does not own | Considered foreign, product-update, changed, half-matching and renewed records, and two concurrent Finalize runs. | <ul><li>The retry needs exact bytes per path plus the full tuple.</li><li>Naming needs both ID and token.</li><li>A second concurrent Finalize fails the manifest-hash re-check under the gates; the update gate serializes the two runs.</li></ul> | No issue |
| Regression: every write comes after the re-checks under the gates | Listed the writes in `Invoke-Finalize`. | <ul><li>There are two writes: `Save-Json` (885) and the deletes (887). Both follow `Assert-FinalizePreconditionsHold` under all three gates, and the deletes re-assert each path.</li><li>The retry does not write the manifest.</li><li>The new hint code for Switch and Rollback writes nothing.</li></ul> | No issue |
| Regression: Switch and Rollback routes and refusals | Diffed the old entry checks against 1286-1294. | <ul><li>The terminal predicate for Rollback is unchanged; only the text changed, and the tests still match "Nothing to roll back".</li><li>Switch now also refuses `terminal`. The only newly refused input is a `prepared` manifest that carries the flag, and the flag's own note already says "cannot be retried".</li><li>The `finalized` refusal is unchanged since DEV-001. No route or record handling changed.</li><li>The Switch hint for `finalizable` is wrong for a completed Rollback (ANALYSIS-100).</li></ul> | Refusals preserved. One message regression |
| Regression: `rollback-before-tray-restart` barrier | Read `Wait-OptionalTestBarrier` (73-75) and the guards for disposable runs (1170-1176). | <ul><li>The barrier needs `-Disposable`, `-TestBarrierDirectory` and a `hold-` file.</li><li>`-TestBarrierDirectory` without `-Disposable` throws, and `-Disposable` requires every root to be under `artifacts/`.</li><li>`switch-before-tray-restart` is created by the same call and has the same guard.</li></ul> | Cannot trigger on a real installation |
| Parity | `cmp` of the two skill copies; read the docs diffs. | The skill copies are identical. One shared script, with no provider branch. The launch-gate change benefits Claude Code and Codex hooks equally. | Parity holds |

Not performed:
- Builds, tests and format checks: QA runs them in this round.
- Access to the live installation or data directory: forbidden.
- Timing hook waits under contention.

## Findings

### ANALYSIS-100: a completed Rollback is classified as finalizable, so Switch tells the maintainer to quit a healthy tray and run Finalize

- Severity: warning. It is a regression introduced by the ANALYSIS-005 fix, and the same class of problem as the blocking ANALYSIS-005.
- Related task / requirement: 2.2; spec "names ... the supported next step"; design "Messages that let a maintainer tell a stuck tray from ..."
- Locations: `scripts/install-dev.ps1`:
  - 243-255: `finalizable` is returned for any Rollback in `restored-monitoring-snapshot` or `running-original`, whether or not the records exist.
  - 1290: the Switch hint.
  - 848: Finalize then refuses.
- Observation:
  - A successful Rollback ends in `state = running-original` (1577-1580) and removes its records (1586). The manifest keeps `coordinationPhase = Rollback` and every ownership field.
  - The probe shows `Get-OperationDisposition` returns `kind: finalizable` for that manifest, with next step "Quit any tray from its tray menu, then run -Action Finalize -OperationManifest '...'".
  - Switch with that manifest now prints this hint. Before the change it printed "Run Preflight again for a new operation, or Rollback to revert it."
  - A maintainer who follows the hint quits a healthy tray, so monitoring stops. Finalize then refuses at 848 with "an operation coordination record is missing ... inspect them under docs/releases.md (interrupted update)", which describes a problem that does not exist.
  - Nothing is corrupted. Still, the two messages contradict each other and the first leads to an unneeded tray stop, which is what ANALYSIS-005 set out to remove.
- Proposed improvement and benefit:
  - In `Get-OperationDisposition`, return `finalizable` only while at least one owned coordination record is present.
  - A Rollback in `running-original` with neither record is complete. Classify it as `terminal`, or as a new `completed` kind, with the next step "Run Preflight again for a new operation."
  - `Invoke-Finalize` then refuses through the same rule and text, before it reaches the record-missing check.
- Behavior-preservation risks:
  - The field case (`restored-monitoring-snapshot` with both records) stays `finalizable`.
  - A crash after the `running-original` save but before record removal leaves records behind, so it stays `finalizable`.
  - The Switch and Rollback entry gate must not start refusing a Rollback retry of `restored-monitoring-snapshot`. Keep the entry refusal for `terminal` limited to the existing predicate, or use the new kind only in the Switch hint and in Finalize.
- Evidence type: source observation, plus a probe of the AST-extracted function (the classification was measured).
- Verification / measurement plan:
  - Add a CLI case: a successful Switch, then Rollback, then Switch again with the same manifest.
  - That Switch must say to run Preflight again and must not mention Finalize or quitting the tray.
  - Finalize on the same manifest must refuse with the same next step.
  - The existing Finalize theory for `running-original` with records must stay green.
- Relationship: regression of the ANALYSIS-005 correction

### ANALYSIS-101: one readiness-failure route still prints the absolute operation record path

- Severity: warning. It is a residual of ANALYSIS-009, whose acceptance the orchestrator triaged as blocking. The fix is one line.
- Related task / requirement:
  - Task 1.1: "The message never contains the data-directory or profile path".
  - `docs/development.md`: "Messages print the operation record relative to the repository ... so no profile or workspace prefix appears".
- Locations: `scripts/install-dev.ps1`:
  - 1113: `$recovery = "-Action Rollback -OperationManifest '$OperationManifest'"`
  - 1151-1153 and 1159: the messages that embed it.
- Observation:
  - Switch or Rollback can restart the tray it stopped. If that restart misses the readiness deadline, `Start-SelectedTray` throws the `New-NotReadyFailure` text, which uses the relative path.
  - `Restore-StoppedTrayAfterFailure` wraps that text ("a restart was launched but could not be verified (...). ... recover with ...") and appends `$recovery`, which is still absolute.
  - The final readiness message on this route therefore contains the repository prefix, which includes the profile path when the clone is under `%USERPROFILE%`.
  - The line itself is unchanged from the baseline. The change now sends a readiness failure through it, and the docs claim it does not print such a path.
  - No harness scenario covers this route: `not-ready-incomplete-rollback` calls `New-NotReadyFailure` directly.
- Proposed improvement and benefit:
  - Build `$recovery` with `Format-RecordPath $OperationManifest`.
  - Optionally, extend `not-ready-incomplete-rollback` or add a scenario that goes through `Restore-StoppedTrayAfterFailure` with a stubbed launcher, and assert that no repository prefix appears.
- Behavior-preservation risks: message text only. Check for any test that matches the recovery command with an absolute path.
- Evidence type: source observation
- Verification / measurement plan: a harness assertion that the wrapped message contains `-Action Rollback -OperationManifest '<relative>'` and does not contain `$repo`.
- Relationship: residual of ANALYSIS-009

## Re-verification

All rows were checked against the corrected target `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596`.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | DEV-002 identity above | <ul><li>The owner is checked before the gates (856) and under the launch gate (816).</li><li>Every Rollback sets the plan owner to its own process (1409, 1415).</li><li>Before the first write, under the gates, it re-hashes `current.json`, the manifest bytes (the exact hash of the one read, 1270 and 821), the records and the original bytes.</li><li>The test at Finalize.cs 300-329 holds a live Rollback at the new barrier.</li></ul> | verified |
| ANALYSIS-002 (duplicate of QA-001) | same | <ul><li>Each path's hash and plan must match (795-805).</li><li>A lone record is refused while the manifest is not finalized (848).</li><li>Tests cover a retry with only the restart record and with only the pending record, foreign and changed lone records, and a following Preflight and Switch.</li></ul> | verified |
| ANALYSIS-003 | same | <ul><li>The drain runs without the launch gate (868-875).</li><li>The gate opens at 877, only for the re-checks and the effect.</li><li>Refusal scans run after the gates are released (895).</li><li>The warm payload hash measured about 150 ms.</li><li>A sampling test asserts the slowest gate acquire is under 1.5 s.</li></ul> | verified |
| ANALYSIS-005 | same | <ul><li>Both contradictions it reported are gone (Finalize.cs 376-395; harness `not-ready-incomplete-rollback`).</li><li>The new rule misclassifies a completed Rollback (ANALYSIS-100).</li></ul> | verified for the reported cases; regression raised as ANALYSIS-100 |
| ANALYSIS-008 | same | `Test-SerializedPath` searches the escaped form. `pathDetectorCatchesAnInjectedPath` proves the check is not vacuous. | verified |
| ANALYSIS-009 | same | <ul><li>`Format-RecordPath` is used in all not-ready, unknown-owner and disposition messages.</li><li>"could not be stored" appears when the save fails (`not-ready-save-fails`).</li><li>One wrapping route still embeds the absolute path (ANALYSIS-101).</li></ul> | partially verified; residual ANALYSIS-101 |
| ANALYSIS-010 | same | <ul><li>It names a record only when both records match on ID and token. It is read-only and swallows its own errors.</li><li>Preflight and Switch name `-Action Finalize -OperationManifest '<relative>'` (Finalize.cs 397-429).</li><li>The harness's no-match check exercises the half match.</li></ul> | verified |

## Assessment and limitations

Answers to the regression questions:
- **Ownership:** the single-record retry and `Find-OwningOperation` never accept or name a record the operation does not own. The retry needs exact bytes per path plus the full plan; naming needs both ID and token on both records.
- **Writes:** every Finalize write comes after the re-checks, made under the update gate, the reservation and the launch gate.
- **Switch and Rollback routes:** the disposition helper changes no route, and every existing refusal still holds. Switch now also refuses a `prepared` manifest flagged terminal, which matches the flag's own note. One Switch hint regressed (ANALYSIS-100).
- **Barrier:** `rollback-before-tray-restart` and `switch-before-tray-restart` cannot trigger on a real installation, because they need `-Disposable` and roots under `artifacts/`.
- **Parity:** one shared script, identical skill copies, and the launch-gate fix benefits both providers' hooks equally.

Recommendation: fix ANALYSIS-100 and ANALYSIS-101 before task 4.3. Both are small, local, message-only changes. ANALYSIS-100 has the larger effect, because following its message stops a healthy tray.

Not performed: builds, tests and format checks (QA owns them); timing hook waits under contention; any access to the live installation, data directory or records. The probes used only an AST-extracted function with paths under a nonexistent `artifacts/` folder, and a read-only hash timing of a disposable payload under `artifacts/`.

Writes: none. The client refused the write of the reserved report path, so the text is returned inline. The target identity was unchanged before and after.
