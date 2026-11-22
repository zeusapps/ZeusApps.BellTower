> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-003); client: Claude Code (version not re-probed; 2.1.330 recorded in ANALYSIS-002); mode: subagent (non-interactive); model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: recover-unready-dev-operation / ANALYSIS-003

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-003, final recheck) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not re-probed in this round; 2.1.330 was recorded in ANALYSIS-002), subagent (non-interactive), model `claude-opus-5-5`
- Date: 2026-11-22T11:06Z (UTC)
- Change root / assignment: `openspec/changes/recover-unready-dev-operation` / ANALYSIS-003 (final recheck after DEV-003, fix cycle 2)
- Task and requirement references: tasks 1.1 and 2.2 ([tasks](../tasks.md)); requirement "Supported dev installation with rollback" ([spec](../specs/development-builds/spec.md)); developer evidence [implementation-003](../evidence/implementation-003.md)
- Scope and relevant neighboring paths:
  - Rechecked in `scripts/install-dev.ps1` at the reviewed identity:
    - `Get-OperationDisposition` (216-262)
    - its callers: `Get-OwningOperationHint` (285), `New-NotReadyFailure` (312), `Invoke-Finalize` (843-844) and the Switch and Rollback entry (1292-1300)
    - `Restore-StoppedTrayAfterFailure` (1114-1167)
    - the end of the main flow (1583-1593)
  - Neighbors read: `Format-RecordPath`, `Find-OwningOperation`, `Assert-OwnedRecordPair`, `Remove-OwnedCoordinationRecords`, `Assert-PresentOwnedRecords`, `Remove-PresentOwnedRecords`, `Assert-FinalizePreconditionsHold` and the route selection (1090).
  - Tests read:
    - `InstallDevScriptTests.Finalize.cs`, the test `ASwitchOrFinalizeOfACompletedRollbackSaysPreflightAgainAndNeverNamesFinalizeOrTheTray` (398-420)
    - the harness scenario `stopped-tray-readiness-failure-prints-relative-recovery` (`InstallDevFunctionHarness.ps1` 517-533)
    - its registration in `InstallDevScriptTests.Recovery.cs` (60)
  - Docs and skills: `docs/development.md` (45, 49), `docs/releases.md` (51) and both `install-dev-build` skill copies.
- Baseline commit: `f4dfa4e`
- Implementation identity (before):
  - HEAD `f4dfa4e90009b90d0fa5e906289a541ba5789417`
  - Manifest:
    - `?` (untracked): `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Finalize.cs`
    - `M` (modified): both SKILL.md copies, `docs/development.md`, `docs/releases.md`, `scripts/install-dev.ps1`, `InstallDevBuildSkillTests.cs`, `InstallDevFunctionHarness.ps1`, `InstallDevScriptTests.Recovery.cs`, `InstallDevScriptTests.cs`
  - Tracked diff SHA-256 `103d8365aee37ae00ab48794776ad1c8c61ba7603d6ce3dbf81b16dbcaf1a6e0`
  - Untracked `555f9f5e61bedbefb0c66b6f0eb6bd60aecdf5deb5b40a964896cb4a917169db` (the Finalize tests)
  - Summary `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832`
- Before/after identity: after the analysis, the same command printed `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832`. It is identical, so nothing changed unexpectedly.
- Prior reports / rechecked findings:
  - [analysis-002](analysis-002.md): ANALYSIS-100 and ANALYSIS-101.
  - [analysis-001](analysis-001.md): ANALYSIS-009 (its residual).
  - Triage is in [findings](findings.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| When `completed` is chosen (ANALYSIS-100) | Read 241-262. Probe: extracted `Get-OperationDisposition` and `Get-MissingOwnershipFields` through the PowerShell AST, stubbed `Format-RecordPath`, and pointed the record paths at a disposable scratch folder outside the repository (deleted afterwards). | <ul><li>`completed` is reached only after the `finalized`, `terminal` and `rollback` returns. It also needs `coordinationPhase = Rollback`, state `running-original` and neither record present (250).</li><li>Probe results:<ul><li>`running-original` with no records -> `completed`.</li><li>`running-original` with both records, or with only the restart record -> `finalizable`.</li><li>`restored-monitoring-snapshot` with both records -> `finalizable`.</li><li>A Switch in `running-candidate` -> `rollback`.</li><li>A legacy `running-original` without ownership fields gives `completed` with no records and `foreign` with both.</li></ul></li></ul> | Verified. Chosen only for a Rollback in `running-original` with neither record |
| Field case and leftover-record case stay finalizable | Same probe. Read the end of the main flow (1584-1593) and `Remove-OwnedCoordinationRecords` (703-707). | <ul><li>The field case (`restored-monitoring-snapshot`, both records) is `finalizable`.</li><li>A crash after the `running-original` save (1587) but before the deletes (705-706) leaves records behind. That case stays `finalizable`, whether one record or both remain.</li></ul> | Verified |
| Switch hint and Finalize refusal agree | Read 1296-1300 and 843-844, and the CLI test at Finalize.cs 398-420. | <ul><li>For `completed`, Switch prints "... not in the prepared state (state running-original). Run Preflight again for a new operation."</li><li>Finalize prints "Finalize refused: This operation already ended: ... Run Preflight again for a new operation."</li><li>Both come from the same `next` text. Finalize refuses at 844, before the record-missing check (854).</li><li>The test asserts that neither message contains "Finalize", "tray menu" or "coordination record is missing".</li></ul> | Verified |
| Switch and Rollback entry gate unchanged | Read 1292-1293. Compared the line offsets with the DEV-002 lines cited in analysis-002. | <ul><li>The gate still refuses only `terminal` and `finalized`.</li><li>A Rollback with a `completed` manifest passes the gate and takes the `original-retry` route (1090), as it did in DEV-002, where the same manifest was `finalizable`.</li><li>A Rollback retry of `restored-monitoring-snapshot` is not newly refused.</li></ul> | Verified |
| `$recovery` path (ANALYSIS-101) | Read 1119, 1159 and 1165, and the harness scenario 517-533. | <ul><li>`$recovery` is now `"-Action Rollback -OperationManifest '$(Format-RecordPath $OperationManifest)'"`.</li><li>The scenario stubs `Start-SelectedTray`, marks the restart as launched and throws `New-NotReadyFailure`. It asserts three things: the wrapped text, the exact relative recovery command, and that neither the backslash nor the forward-slash form of the repository prefix appears.</li><li>The absolute path in the fixture contains the repository prefix, so the check is not vacuous.</li></ul> | Verified |
| Remaining messages with the absolute `$OperationManifest` (ANALYSIS-009 residual) | Grepped every use of `OperationManifest` in the script, plus the patterns `$OperationManifest'`, `$($OperationManifest)` and string concatenation. | <ul><li>Every message that names the record goes through `Format-RecordPath`: 227, 302, 310, 1119 and 1295-1298.</li><li>The remaining uses are file I/O (`Save-Json`, `Hash`, `ReadAllBytes`) and the `operationManifest` field of the JSON results (838, 903, 1264, 1595). Those are structured result data, not messages, and are unchanged from the baseline.</li><li>No thrown or printed message embeds the absolute record.</li></ul> | Verified |
| Regression: scope of the DEV-003 script diff | Compared function and line offsets with every DEV-002 line cited in analysis-002. | <ul><li>Everything after line 250 moved by exactly +6. For example, `Invoke-Finalize` moved from 825 to 831, the record-missing refusal from 848 to 854, the launch gate from 877 to 883, `$recovery` from 1113 to 1119 and the entry from 1286 to 1292.</li><li>Everything after the Switch hint moved by +7: the `running-original` assignment is now at 1584 and the record removal at 1593.</li><li>That matches only two additions: the 5-line `completed` branch plus 1 comment line, and the 1-line hint branch. `$recovery` was edited in place.</li></ul> | No unrelated change found |
| Regression: ownership | Read `Find-OwningOperation`, the `completed` branch and the Finalize record checks. | <ul><li>The new branch only calls `Test-Path`.</li><li>`Get-OwningOperationHint` can never get `completed`, because `Find-OwningOperation` needs both records.</li><li>The retry and pair checks for exact bytes and the full plan are unchanged.</li></ul> | No issue |
| Regression: every write comes after the re-checks under the gates | Listed the writes in `Invoke-Finalize` and the new code. | <ul><li>The Finalize writes are still only `Save-Json` (891) and `Remove-PresentOwnedRecords` (893). Both follow `Assert-FinalizePreconditionsHold` (884) under the update gate, the reservation and the launch gate.</li><li>The `completed` refusal happens before any gate or write.</li><li>The hint and the `$recovery` change write nothing.</li></ul> | No issue |
| Regression: routes and barriers | Read 1090, 1292-1301 and 1570-1571. | <ul><li>No route predicate changed.</li><li>The `-before-tray-restart` barrier is unchanged and still needs `-Disposable` and `-TestBarrierDirectory`.</li></ul> | No issue |
| Parity | Hashed both skill copies; read the docs. | <ul><li>The skill copies are identical (same SHA-256).</li><li>One shared script with no provider branch, so the change applies equally to Claude Code and Codex installations and hooks.</li></ul> | Parity holds |
| Docs agree with the new kind | Grepped `docs/`, the skills and the tests for the list of disposition kinds. | <ul><li>`docs/development.md` (49) still says "One disposition rule (finalizable, rollback, terminal, finalized or foreign)". `completed` is missing.</li><li>No test pins that sentence.</li><li>The skill's general "Preflight again for an operation that already ended" still holds.</li></ul> | Regression ANALYSIS-200 (docs only) |

Not performed:
- Builds, tests and format checks: QA-003 runs them concurrently.
- Access to the live installation, the data directory or any coordination records: forbidden.
- The probe used only AST-extracted functions and a disposable scratch folder.

## Findings

### ANALYSIS-200: the architecture doc's list of disposition kinds omits the new `completed` kind

- Severity: warning (documentation only, no runtime effect). It is a regression introduced by DEV-003: the reference doc reviewers judge against now contradicts the code.
- Related task / requirement: 3.1 (docs); the ANALYSIS-100 correction
- Locations:
  - `docs/development.md` line 49, the sentence "One disposition rule (finalizable, rollback, terminal, finalized or foreign) gives the next step ..."
  - `scripts/install-dev.ps1` 223 and 250-254 (the `completed` kind)
- Observation:
  - DEV-003 added a sixth kind. Its evidence says "No quoted message changed; none edited" for docs, but this sentence lists the kinds, not a message.
  - A reader of the doc would still conclude that a finished Rollback in `running-original` is `finalizable`. That is exactly what ANALYSIS-100 removed.
- Proposed improvement and benefit:
  - Add `completed` to the list, for example "(finalizable, rollback, terminal, finalized, completed or foreign)".
  - Optionally add the clause "a Rollback that already finished says to run Preflight again".
  - This is a one-line edit.
- Behavior-preservation risks: none. No test matches the sentence (grepped `tests/`).
- Evidence type: source observation
- Verification / measurement plan: re-read line 49 against `Get-OperationDisposition` 219-224.
- Relationship: follows from the ANALYSIS-100 correction

## Re-verification

All rows were checked against the corrected target `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832`.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-100 | DEV-003 identity above | <ul><li>`completed` is returned only for a Rollback in `running-original` with neither record (250; probe).</li><li>The field case and the leftover-record cases stay `finalizable` (probe).</li><li>The Switch hint (1296) and the Finalize refusal (844) give the same "Run Preflight again for a new operation." text, and neither names Finalize or the tray (Finalize.cs 398-420).</li><li>The entry gate still refuses only `terminal` and `finalized` (1293).</li></ul> | verified (doc enumeration regression raised as ANALYSIS-200) |
| ANALYSIS-101 | same | <ul><li>`$recovery` uses `Format-RecordPath` (1119).</li><li>The harness scenario `stopped-tray-readiness-failure-prints-relative-recovery` asserts the relative command and that no repository prefix appears in either slash form.</li></ul> | verified |
| ANALYSIS-009 (residual) | same | A full grep finds no thrown or printed message that embeds the absolute `$OperationManifest`. The only absolute values left are the `operationManifest` JSON result fields, which are structured data and unchanged from the baseline. | verified |

## Assessment and limitations

Answers to the regression questions:
- **Ownership:** unchanged. The new branch is read-only, and record acceptance and naming still need exact bytes plus the full plan, or both ID and token.
- **Writes:** all Finalize writes still follow the re-checks under the three gates, and DEV-003 adds no write.
- **Routes and refusals:** unchanged. The script diff from DEV-002 is limited to the `completed` branch, the hint branch and the `$recovery` line, as the uniform line offsets show.
- **Barriers:** unchanged.
- **Parity:** one shared script and identical skill copies.

ANALYSIS-100 and ANALYSIS-101 are fixed in code, and the ANALYSIS-009 residual is closed. The only new item is ANALYSIS-200, a one-word documentation gap in `docs/development.md`. It has no runtime effect, so the orchestrator decides whether to fix it now, during reconciliation or after this last fix cycle.

The orchestrator may want to know about one narrow case. It is unchanged from DEV-002 and is not a regression, so it gets no finding ID:
- A Rollback can crash between the two record deletions (705-706), after saving `running-original`. One record is then left, and the disposition is `finalizable`.
- The Switch hint then says to quit the tray and run Finalize, and Finalize refuses at 854 ("an operation coordination record is missing ... inspect them under docs/releases.md").
- This is the documented rule that a lone record is refused while the manifest is not finalized, and it was accepted under QA-001. It needs a crash between two consecutive `Remove-Item` calls.

Not performed: builds, tests and format checks (QA owns them); any access to the live installation, data directory or records.

Writes: none. The client refused the write of the reserved report path, so the text is returned inline. The target identity was unchanged before and after.
