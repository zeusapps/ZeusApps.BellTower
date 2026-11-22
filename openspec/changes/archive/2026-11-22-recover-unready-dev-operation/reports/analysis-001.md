> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-001); client: Claude Code 2.1.330; mode: subagent (non-interactive); model: claude-opus-5-5.
> Refusal: the client refused the reviewer's write of this reserved path; the reviewer made one attempt and returned the full text inline. Exact refusal message was not quoted in the hand-back.

# Analysis report: recover-unready-dev-operation / ANALYSIS-001

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-001) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent (non-interactive), model `claude-opus-5-5`
- Date: 2026-11-22T10:20Z
- Change root / assignment: `openspec/changes/recover-unready-dev-operation` / ANALYSIS-001
- Task and requirement references: tasks 1.1, 2.1-2.4, 3.1 ([tasks](../tasks.md)); requirement "Supported dev installation with rollback" ([spec](../specs/development-builds/spec.md)); design decisions 2-5 ([design](../design.md)); developer evidence [implementation-001](../evidence/implementation-001.md)
- Scope and relevant neighboring paths:
  - In scope: `scripts/install-dev.ps1` (Finalize, not-ready diagnostics, readiness wait, `Start-TrayLauncher`, `trayRestartNote`, finalized-manifest refusal); `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Finalize.cs`, `InstallDevScriptTests.Recovery.cs`, `InstallDevFunctionHarness.ps1`, `InstallDevBuildSkillTests.cs`; `docs/development.md`, `docs/releases.md`; `.claude/skills/install-dev-build/SKILL.md`, `.agents/skills/install-dev-build/SKILL.md`.
  - Neighbors read: `src/BellTower/Updates/UpdateTransaction.cs` (`EnsureNoCoordinator`, `RestartPending`, `CanStartTray`, `Stage`, `Apply`), `src/BellTower.Launcher/Program.cs`, `src/BellTower.Distribution/InstallationGate.cs`, `src/BellTower.Protocol/HookTrayStart.cs`, `src/BellTower/IO/LocalLog.cs`, `src/BellTower/IO/RefreshHealth.cs`.
- Baseline commit: `f4dfa4e`
- Implementation identity (before):
  - HEAD `f4dfa4e90009b90d0fa5e906289a541ba5789417`
  - Manifest: `?` `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Finalize.cs`; `M` both SKILL.md copies, `docs/development.md`, `docs/releases.md`, `scripts/install-dev.ps1`, `InstallDevBuildSkillTests.cs`, `InstallDevFunctionHarness.ps1`, `InstallDevScriptTests.Recovery.cs`
  - Tracked diff SHA-256 `43216f4f40a325dd6aefae5820cb5ad904f09d2f16c3b19c119912201534adf4`
  - Untracked `c090b615daa8adeaf64c46efb50ab37b27ef88dec50773e5de6ba03b625cc367` (Finalize tests)
  - Summary `head=f4dfa4e baseline=f4dfa4e files=9 diff=43216f4f40a3 untracked=1/681383ff9c34`
- Before/after identity: after the analysis the same command printed `head=f4dfa4e baseline=f4dfa4e files=9 diff=43216f4f40a3 untracked=1/681383ff9c34`. Identical, so nothing changed unexpectedly.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| State machine: ownership | Compared `Invoke-Finalize` with the Rollback recovery tuple and `EnsureNoCoordinator` | Finalize checks ID, token, bytes and tuple (`Assert-OwnedRecordPair`). It never checks that the plan owner has ended, which Rollback recovery requires (install-dev.ps1 1213, 1231; UpdateTransaction.cs 332-334) | ANALYSIS-001 |
| State machine: concurrency | Walked the gates held during the readiness wait | The update gate and reservation are released at 1406-1409, before `Start-SelectedTray` (1434). The records stay owned by the live installer | ANALYSIS-001 |
| State machine: crash safety | Traced manifest-first ordering, atomic `Save-Json` and the two separate deletes in `Remove-OwnedCoordinationRecords` | A crash before or after the manifest write is retried correctly. A crash between the two deletes is not (716) | ANALYSIS-002 |
| Refusals: Switch and incomplete routes | Compared the predicates at 698-706 with `Get-RollbackRoute` and the main-flow checks at 1144-1153 | Every Switch phase and every incomplete route (`coordinationTransition`, `rollbackPointerTransition`, `restored-monitoring-candidate-pointer`, `restoring-monitoring-snapshot`) is refused. One terminal-Switch message disagrees with Rollback | Refusals hold. Message drift is ANALYSIS-005 |
| Refusals: running or starting tray | Compared the order of reservation, drain and launch gate with Switch's `Invoke-GuardedPointerStep` and with the launcher | Safe and fails closed. It holds the hook launch gate across CIM drains, which Switch deliberately avoids | ANALYSIS-003 |
| Refusals: product update and hooks | Read the gating in `Stage`/`Apply` and `HookTrayStart.UpdatePending` | `Stage` refuses while our `pending.json` exists and while the update gate is held. `Apply` needs its own pending ID. A hook-started tray loses to the reservation or is caught by the drain | No race apart from ANALYSIS-001 |
| Order against design decision 2 | Read the prelude before the Finalize dispatch (1129-1143) | The package, descriptor and original-bytes checks run before Finalize's own ordered preconditions | ANALYSIS-006 |
| Diagnostics: bounded and path-free | Read 105-142. Probe: `'IOException: The file exceeds the size budget.' -match '[A-Za-z]:\|\\\|%\|~'` returned True | The read is bounded to 64 KiB, skips the first partial line and swallows errors. The reason filter rejects every product-format reason (`RefreshHealth.cs` 200: `<Type>: <message>`) | Bounded: yes. Facts path-free: yes. Reason quoting never fires in practice (ANALYSIS-004) |
| Diagnostics: message privacy | Read 206 and 209-217 against the docs claim | The facts carry no paths. The absolute manifest path is printed | ANALYSIS-009 |
| Diagnostics: performance | Read the readiness loop (635-646) and `briefOwnerErrors` | The CIM-based diagnostic is computed once per failure, not on every 100 ms poll. The poll cadence is unchanged | No issue. Hypothesis only, nothing measured |
| Duplication | Compared `Get-TrayHolderPids` (664-679) with `Get-NotReadyFacts` (144-172), and the budget literals | The presence read and the tray scan are duplicated. The 16 MiB budget appears as a literal in the script and in 3 product sites | ANALYSIS-007, ANALYSIS-011 |
| Developer additions | Read `Start-TrayLauncher` (605-621), `trayRestartNote` (1448) and the finalized-manifest refusal (1148-1150) | The seam is minimal, has one call site and is stubbed by the harness. The note is accurate; its last `else` is unreachable but harmless. The refusal prevents a misleading "Rollback to revert it" hint | Sound and in scope. No finding |
| Tests | Read the new Finalize class, the harness scenarios and the skill test. Probe: `ConvertTo-Json` escapes `\` as `\\` | Coverage of 2.1-2.4 is good for dead-owner fixtures. One harness assertion is vacuous. There is no fixture for a live owner, a half-removed pair or a product-format reason | ANALYSIS-008, plus notes in 001, 002 and 004 |
| Field applicability | Read-only probe of non-path fields of the field operation record under `artifacts/dev-builds/operations/` (no installation or data access) | state=restored-monitoring-snapshot, coordinationPhase=Rollback, all coordination fields present, no transition or intent, no trayNotReady, package present | Finalize precondition 1 and the manifest part of precondition 2 hold for task 4.3. The reason will be `operator-finalize`. Records and tray not inspected |
| Docs and parity | `cmp` of the two skill copies; read the docs diffs against the script help | The skill copies are identical. One shared script, no provider branch. The launch gate affects Claude Code and Codex hooks equally. The docs list the same preconditions as the help | Parity holds. The docs order claim is part of 006, the privacy claim is 009 |
| Preflight unknown-owner next step | Read `Add-NotReadyDiagnostics` for Preflight, where no manifest exists | It says only "run again" and never names Finalize or the record | ANALYSIS-010 |

Not performed:
- Builds, tests and format checks: QA runs them in this round.
- Live installation checks: forbidden.
- Timing measurements: every performance statement here is a hypothesis.

## Findings

### ANALYSIS-001: Finalize does not require the records' owner to have ended, so it can race a live same-manifest Rollback

- Severity: warning
- Related task / requirement: 2.1, 2.2; design 2.2-2.3; spec "remove only the coordination records that the operation owns"
- Locations: `scripts/install-dev.ps1` 707-723 (checks before the gates), 730-752 (effect under the gates). Compare 1213 and 1231: Rollback recovery requires `ProcessLiveness.Ended` for the plan owner. At 1406-1409 Switch and Rollback release the update gate and the reservation before `Start-SelectedTray` (1434). Also `src/BellTower/Updates/UpdateTransaction.cs` 332-334.
- Observation:
  - The records' `Owner` is the installer process that created or renewed them (1269, 1275, kept by `Set-OwnedRecordsFresh`). During a Rollback's readiness wait of up to 30 s, that installer is alive and holds no gate.
  - At that point the manifest is already `restored-monitoring-snapshot` and the record hashes match it. A Finalize started in this window passes every precondition.
  - While Finalize holds the tray-instance object, the tray that Rollback launched either exits (older payload, `createdNew=false`) or its launcher waits behind Finalize's launch gate. Finalize then removes the records.
  - The still-running Rollback then saves its stale in-memory manifest: either `trayNotReady` (212), or `running-original` (1438) if the late tray started. Either save overwrites `finalized-without-tray`. Rollback then throws "record disappeared".
  - The result is a manifest whose state contradicts the records, which are gone. Finalize refuses it as "record missing", while Rollback re-runs `original-retry`. This is not destructive, but it loses the terminal state and cuts short a live operation.
- Proposed improvement and benefit:
  - After `ConvertTo-UpdatePlan` (719), refuse unless `[BellTower.IO.ProcessProbe]::Inspect($script:coordinationPlan.Owner) -eq [BellTower.IO.ProcessLiveness]::Ended`. The message should name the live installer PID and say to wait for it.
  - Repeat the check under the gates (next to 741).
  - Under the gates, also re-hash `current.json` against `originalSelectionSha256`; today that check runs only before any gate (707). Also re-read the manifest bytes.
  - About 4 lines, reusing the existing ended-owner rule.
- Behavior-preservation risks: the field case and all fixtures with a dead owner still pass. `Unverified` liveness must refuse too (fail closed, as at 1213).
- Evidence type: source observation
- Verification / measurement plan: a CLI fixture holding a Rollback at a new disposable barrier after the reservation release and before `Start-SelectedTray`. Finalize must refuse and name the PID, and the manifest and records must stay byte-identical. The dead-owner tests stay green.
- Relationship: none

### ANALYSIS-002: an interruption between the two record deletes cannot be recovered by the Finalize retry

- Severity: warning
- Related task / requirement: 2.3; design decision 3
- Locations: `scripts/install-dev.ps1` 597-601 (two separate `Remove-Item` calls), 690-696 (retry detection), 716 (refusal when a record is missing), 1235 (Switch refuses a lone `restart.json`); `UpdateTransaction.cs` 326-337 (a lone `pending.json` blocks every operation)
- Observation:
  - Manifest-first ordering is crash-safe around the manifest write, because `Save-Json` is atomic.
  - A crash after `pending.json` is deleted but before `restart.json` is deleted leaves the manifest `finalized-without-tray` with one owned record.
  - Every retry then refuses at 716 ("record is missing ... docs/releases.md").
  - A lone `restart.json` blocks every later dev Switch (1235), and `docs/releases.md` has no procedure for it.
  - The retry test (Finalize.cs 213-243) restores both records only.
- Proposed improvement and benefit:
  - In the retry state only, accept one remaining record when its hash matches that path's recorded hash and it parses to `coordinationPlan`. Remove it under the same gates, using a per-path variant of `Assert-OwnedRecordPair`.
  - Optionally delete `restart.json` first.
- Behavior-preservation risks: keep refusing a foreign or changed lone record, and any lone record when the manifest is not finalized.
- Evidence type: source observation
- Verification / measurement plan: after a successful Finalize, restore only `restart.json` (and, separately, only `pending.json`). The retry must remove it. A foreign lone record must still be refused.
- Relationship: the same window existed before this change at the end of Switch and Rollback (1444). This fix gives it a supported exit.

### ANALYSIS-003: Finalize holds the hook launch gate during CIM drains, blocking every launcher call from either provider

- Severity: warning
- Related task / requirement: 2.1, 2.2; design 2.3 ("exactly as Switch does"); the rule that agent sessions are not disturbed
- Locations: `scripts/install-dev.ps1`:
  - 734-740: takes the launch gate, then runs the 5 s drain, then on refusal calls `Get-TrayHolderPids` twice (738 and inside `Get-FinalizeTrayRefusal`).
  - 486-491: Switch runs its drain outside the launch gate.
  - 537: each scan can take up to the 5 s CIM timeout.
  - Related: `src/BellTower.Launcher/Program.cs` 15; `InstallationGate.cs` (waits 10 s by default, then fails with "busy").
- Observation:
  - Every stable-launcher call, including Claude Code and Codex hooks, waits up to 10 s for the launch gate.
  - Finalize holds the gate for at least 5 s whenever a tray start is in progress, plus up to 3 more CIM scans on refusal. With slow WMI the hold can pass 10 s, and hooks then fail.
  - Under the reservation the gate adds nothing to the drain.
- Proposed improvement and benefit:
  - Run the drain under the update gate and the reservation only.
  - Take the launch gate only for the short effect: the record-pair check, the selection re-check, `Assert-OriginalBytes`, the manifest write and the deletes. This is the `Invoke-GuardedPointerStep` pattern.
  - Compute the refusal PIDs once, after releasing the gates.
- Behavior-preservation risks: the refusal must still come before any write. The reservation stays held throughout.
- Evidence type: source observation. The hook delay is a hypothesis and was not measured.
- Verification / measurement plan: the start-in-progress test stays green. Optionally, a second process must acquire `InstallationGate(root)` within 1 s while Finalize drains.
- Relationship: none

### ANALYSIS-004: the sanitized-reason filter rejects every reason the product writes

- Severity: suggestion
- Related task / requirement: 1.1; design decision 4
- Locations: `scripts/install-dev.ps1` 109 (`'[A-Za-z]:|\\|%|~'`), 119 (`FileShare.ReadWrite`); `RefreshHealth.cs` 200; `LocalLog.cs` 17-19; harness line 428
- Observation:
  - Product reasons are always `<TypeName>: <message>`. The `[A-Za-z]:` branch matches the `n:` of `IOException:` (probe returned True), so `lastReason` is always null except for `further reasons suppressed`.
  - The test fixture uses a reason without a colon, so it cannot catch this.
  - The log is opened without `FileShare.Delete`. A concurrent `LocalLog` rotation then fails silently once, and the log grows past its cap.
- Proposed improvement and benefit:
  - Match real path shapes only, for example `(?<![A-Za-z])[A-Za-z]:[\\/]|\\\\|%[A-Za-z_]+%|~[\\/]`, and keep the data-directory and `Hide-Paths` checks.
  - Add a fixture line in the product's format.
  - Open the log with `ReadWrite -bor Delete`.
- Behavior-preservation risks: a reason containing a drive path must still be dropped; the existing fixture covers that.
- Evidence type: source observation plus a regex probe (measured for the regex only)
- Verification / measurement plan: the harness line `tray-refresh-failed: IOException: State file exceeds the budget.` must be quoted, and the path case must still be dropped.
- Relationship: none

### ANALYSIS-005: the next steps for Finalize, Rollback and readiness failures come from separate predicates that disagree

- Severity: suggestion
- Related task / requirement: 2.2; spec "names ... the supported next step"
- Locations: `scripts/install-dev.ps1` 698 vs 1144-1147; 213-215 (next step chosen from `$Action` only); 989 and 1016
- Observation:
  - (a) A Switch that failed in `ready-to-select` and whose original tray was restarted carries `trayRestartedAfterFailure` and `devSelectionSha256`. Finalize says "Recover with -Action Rollback", but Rollback says "already ended ... run Preflight again". The two messages send the maintainer in a loop.
  - (b) When a Rollback restarts the candidate it stopped and that restart misses the deadline, the inner text says "run -Action Finalize", which Finalize will refuse. The outer text says Rollback.
- Proposed improvement and benefit: one `Get-OperationDisposition` helper that returns finalizable, rollback, terminal or foreign. Use it in `Invoke-Finalize`, in the main-flow checks and in `New-NotReadyFailure`.
- Behavior-preservation risks: messages only. Keep the substrings the tests assert.
- Evidence type: source observation
- Verification / measurement plan: a Finalize CLI case for that manifest expecting "Run Preflight again", and a harness case for the inner next step.
- Relationship: none

### ANALYSIS-006: Finalize inherits the Switch/Rollback prelude before its own ordered preconditions

- Severity: suggestion
- Related task / requirement: 2.1, 2.2, 3.1; design decision 2; `docs/development.md` ("checked in this order")
- Locations: `scripts/install-dev.ps1` 1135-1141 (package hash, descriptor, `preparedPayload`, `Assert-OriginalBytes`) before the dispatch at 1143
- Observation:
  - If the dev zip or its descriptor was removed or rebuilt, Finalize refuses with "The package differs from the reviewed hash." That message names no next step, and Finalize never uses the package.
  - The bytes check runs first instead of fourth, against the documented order.
  - The field record still has its package (probe), so task 4.3 is unaffected.
- Proposed improvement and benefit: dispatch Finalize right after the manifest path, path-equality and tray-mode checks (1133-1136). Keep its gated `Assert-OriginalBytes` as step 4.
- Behavior-preservation risks: Switch and Rollback keep the full prelude.
- Evidence type: source observation
- Verification / measurement plan: Finalize with the zip deleted must succeed. Either the docs order holds, or the docs are corrected.
- Relationship: none

### ANALYSIS-007: presence reading and the tray scan are duplicated between the refusal and diagnostic helpers

- Severity: suggestion
- Related task / requirement: 1.1, 2.2
- Locations: `scripts/install-dev.ps1` 664-679 (`Get-TrayHolderPids`), 144-172 (`Get-NotReadyFacts`), 680-684, 738
- Observation: both helpers parse `tray.json`, probe liveness and run `Get-InstallationTrayScan`. The refusal path also scans twice more.
- Proposed improvement and benefit: build the Finalize refusal from `Get-NotReadyFacts`, adding the live presence PID. That leaves one rule, and the refusal carries the same facts as the other messages.
- Behavior-preservation risks: keep "PID <n>" and "tray menu", which tests assert.
- Evidence type: source observation
- Verification / measurement plan: the existing refusal tests.
- Relationship: ANALYSIS-003 also notes the duplicate scans.

### ANALYSIS-008: the harness check that the saved manifest has no data-directory path is vacuous

- Severity: suggestion
- Related task / requirement: 1.1
- Locations: `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1` 445
- Observation: `$DataDirectory.Replace('\', '\')` does nothing, so the check searches for a path with single backslashes. JSON escapes backslashes (probe), so the search never matches and the check always passes.
- Proposed improvement and benefit: serialize only `trayNotReady` and search it for the path with backslashes escaped (`Replace('\', '\\')`), or assert that no fact value is a rooted path.
- Behavior-preservation risks: none
- Evidence type: source observation plus an escaping probe
- Verification / measurement plan: inject a path into the facts in a scratch run; the corrected check must fail.
- Relationship: none

### ANALYSIS-009: not-ready messages print the absolute record path, and claim the facts were stored even when the save failed

- Severity: suggestion
- Related task / requirement: 1.1; `docs/development.md` ("Nothing prints the data-directory or profile path")
- Locations: `scripts/install-dev.ps1` 206, 213-216 (absolute `$OperationManifest`), 212 (`catch { }`, followed by "the facts are stored")
- Observation:
  - The default workspace is inside the repository. If the clone lives under `%USERPROFILE%`, every message contains the profile path.
  - A failed save is swallowed, but the message still says the facts were stored.
- Proposed improvement and benefit:
  - Print the record path relative to the repository (`artifacts/dev-builds/operations/<id>/operation.json`, runnable from the repository root as the skill requires). Otherwise keep it absolute and document that exception.
  - Say "could not be stored" when the save fails.
- Behavior-preservation risks: harness line 444 matches the absolute substring and needs updating.
- Evidence type: source observation
- Verification / measurement plan: a harness assertion that no profile or workspace prefix appears when the workspace is under the repository.
- Relationship: none

### ANALYSIS-010: Preflight's unknown-owner refusal does not lead to Finalize or the record

- Severity: suggestion
- Related task / requirement: 1.1; spec "SHALL also give the supported next step with the operation record path"
- Locations: `scripts/install-dev.ps1` 203-208, 1116, 1181, 1234
- Observation:
  - In the field state, Preflight says to quit the tray and run again.
  - After that, Switch gets the product's "An interrupted update needs inspection".
  - Neither message names Finalize or the record.
- Proposed improvement and benefit: when the records exist, find the workspace `operation.json` whose `coordinationId` and token match the records. Name it, with the disposition from ANALYSIS-005, in Preflight's unknown-owner message and before `EnsureNoCoordinator` refuses a Switch.
- Behavior-preservation risks: read-only. Never name a manifest unless both ID and token match.
- Evidence type: source observation
- Verification / measurement plan: a stuck rollback, then Preflight and Switch. The refusal must name `-Action Finalize -OperationManifest <record>`.
- Relationship: builds on ANALYSIS-005

### ANALYSIS-011: the state budget is a duplicated literal rather than a product constant

- Severity: suggestion
- Related task / requirement: 1.1; design decision 4
- Locations: `scripts/install-dev.ps1` 96; `src/BellTower/IO/AtomicFile.cs` 46; `src/BellTower/Updates/UpdateTransaction.cs` 287; `src/BellTower.Distribution/AttachmentHealthCompatibility.cs` 19
- Observation: the product has no named budget, so the script restates `16 * 1024 * 1024`. If the product changes the budget, the diagnostic silently drifts.
- Proposed improvement and benefit: add a `public const long MaxReadBytes` on `AtomicFile`, used by the product sites and read by the script from the already loaded `bell-tower.dll`.
- Behavior-preservation risks: none, since the value is unchanged. It touches product code outside the stated impact, so the orchestrator may defer it.
- Evidence type: source observation
- Verification / measurement plan: the existing harness checks for 16777216.
- Relationship: none

## Re-verification

None: this is the first round.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| n/a | n/a | n/a | not performed |

## Assessment and limitations

Covered: the whole Finalize path and how it integrates with records, reservation, launch gate, rollback routes and manifest states; the not-ready helper and its 3 call sites; the developer's additions; the tests; both docs; both skill copies.

Answers to the focus questions:
- **Records it does not own:** Finalize never removes them.
- **Running or starting tray:** it never runs while one is running or starting.
- **Product updates and hook-started trays:** it cannot race a product update, and a hook-started tray is caught.
- **Live owner:** it can act while the installer that owns the records is still alive (ANALYSIS-001).
- **Crash safety:** manifest-first ordering is crash-safe and retries correctly around the manifest write. A crash between the two deletes cannot be recovered (ANALYSIS-002).
- **Switch and incomplete routes:** every Switch and every incomplete route is refused. One terminal-Switch message conflicts with Rollback's (ANALYSIS-005).
- **Diagnostics:** the log tail is bounded and the stored facts are path-free. Quoting the reason never fires (ANALYSIS-004), and the record path can carry a profile prefix (ANALYSIS-009).
- **Developer additions:** `trayRestartNote`, the "run Preflight again" refusal for a finalized manifest and the `Start-TrayLauncher` seam are sound and in scope.
- **Parity:** one shared script, identical skill copies, no provider branch. ANALYSIS-003 affects both providers' hooks equally.

Recommendation: fix ANALYSIS-001, -002 and -003, which are cheap and local, before the installed recovery (task 4.3). The suggestions can be deferred.

Not performed: builds, tests and format checks (QA owns them); timing measurements; any access to the live installation, data directory or records. The field operation record was read for non-path fields only.

Writes: none. The client refused the write of the reserved report path, so this text is returned inline. The target identity was unchanged before and after.
