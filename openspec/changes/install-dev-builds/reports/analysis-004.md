# Analysis report: install-dev-builds / 004

## Review target

- Reviewer / role: `analyze_dev_builds` / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 (`codex --version`), delegated subagent, `gpt-6.1-sol`, high reasoning.
- Date: 2026-10-24 UTC.
- Assignment: final recheck 2, `D:/Code/ZeusApps.BellTower/openspec/changes/install-dev-builds`.
- Requirements: [tasks](../tasks.md) 4.1, 4.3 and 6.1/6.3; [spec](../specs/development-builds/spec.md) safe installation/rollback and maintenance hook delivery; [design](../design.md) decision 7's exact-marker, pre-mutation drain and old-payload startup boundary.
- Scope: `src`, `tests`, `scripts`, the three scoped development/installation/release docs and both install-dev-build skill folders. Focused implementation paths: `scripts/install-dev.ps1`, the recovery overload in `src/BellTower/Updates/UpdateTransaction.cs`, installer tests and updated docs/skills. Neighbors: `TrayHost`, `TrayController`, `TrayApplication` exit, `CommonOptions`, process identity and old released startup ordering.
- Baseline/HEAD: `b74a12ba20eb9d3595204b41295b9dab73b7895d`.
- Before and after identity: `head=b74a12b baseline=b74a12b files=27 diff=41ba2dbe9660 untracked=10/88d345b46144`; tracked diff SHA-256 `41ba2dbe9660f50e17bdab217f947a38b493f605ef433ded47ea6e7910efecfc`. Full [before](../evidence/analysis-004-identity-before.txt) and [after](../evidence/analysis-004-identity-after.txt) manifests/hashes match; `Compare-Object` returned no differences. No unexpected implementation mutation.
- Prior evidence: [Analysis 003](analysis-003.md), [bounded startup-fence assessment](../evidence/analysis-003-startup-fence-design.md), [QA 003](qa-003.md), [Implementation 004](../evidence/implementation-004.md). Recheck canonical ANALYSIS-150 / QA-150 and relevant ANALYSIS-001/006, QA-001/005 regressions. Reserved new IDs ANALYSIS-200–249; no optional suggestions added.

## Analysis performed

| Area | Method / evidence | Result / limits |
| --- | --- | --- |
| Old-payload reservation | Inspect script `:399–424`, stop helper `:108–113`, old/current `TrayHost` constructor ordering and prior primary mutex documentation | Passed source mechanism: non-owning named-object reference is retained before stop; stop waits for exact recorded process identity rather than object disappearance. This closes ordinary new-start acquisition while reserved. Complete boundary proof fails as ANALYSIS-200 |
| Marker publication, renewal and recovery | Trace `New-ExclusiveRecord`, `Set-OwnedRecordsFresh`, recovery dispatch, final cleanup; compare actual call order with design 7 | Failed: renewal/drain is after pointer mutation; ID/token-only marker matching overwrites semantic drift; a failed Rollback leaves a target tuple its next retry rejects. See ANALYSIS-200/201 |
| Bounded process drain | Inspect local CIM query, live image comparison, timestamp correlation, argument matching and repeated `ProcessProbe` tuple | Partially passed: five-second CIM/30-second drain bounds; raw argv is kept in memory; live image and exact PID/start tuple are rechecked. Ten-tick CIM correlation is separate from exact identity. Missing image/alternate valid command forms are silently skipped; see ANALYSIS-200 |
| Stopped vs unknown owner | Trace `Read-Owner` and actual tray exit | Failed: stale ended tray presence is treated as unverifiable active ownership; see ANALYSIS-202. Empty/absent presence and actively unknown ownership are different cases |
| Launcher/hook locks | Inspect current critical sections and compare Analysis 003 target | Passed source continuity: slow copy/hash/delete, process-drain and tray-stop waits remain outside launch. Cleanup/Retire retain update serialization and short lease acquisition. No deadline increase or stable-launcher edit |
| Default updater callers | Inspect new `EnsureNoCoordinator` overload and unchanged Stage/Apply call sites | Passed source check: default one-argument overload forwards null recovery fields and retains the previous active/interrupted refusal behavior. Installer-specific recovery remains defective as ANALYSIS-201 |
| Retirement and drift regression | Inspect unchanged shared removal predicate, exact original-byte/selection diagnostics and test hashes | Carry forward Analysis 003's source checks for ANALYSIS-001/QA-001 and QA-005: shared retention, dev marker, lease/file-integrity rules and detailed drift diagnostics remain. No re-executed runtime pass claimed |
| Docs/skill parity | Read current text; SHA-256 both skill copies | Byte identity passed: `c9ada458ca27b48897054ed517f9e9a756429b080c5cb9835a691e4583474190`. Claims of refusal-before-selection and manifest-bound recovery exceed current behavior under the findings below. No provider-specific policy split found |
| Unaffected whole-target coverage | Compare current scope with Analysis 003; formatter, packaging, display and policy source/test hashes outside the focused round remain unchanged | Carry forward source coverage for tasks 1.1–3.2 and 4.2; this does not replace QA's final package/native acceptance. Both providers and mixed sessions share all affected paths |
| Whitespace | `git diff --check -- src tests scripts docs/development.md docs/releases.md docs/installation.md` | Passed, exit 0; Git line-ending notices only. Not an SDK formatter run |
| Build/tests/SDK format/native trials/benchmark | Not performed by analyzer | QA 004 exclusively owns shared outputs and runtime probes. No performance measurements or authenticated work-machine result claimed |

## Findings

### ANALYSIS-200: Startup safety is checked after mutation, and the drain can omit valid in-flight tray commands

- Severity: warning; blocks task 4.3 and the safe owner/unknown-startup acceptance in 4.1.
- Locations: `scripts/install-dev.ps1:134–178`, `:430–465`, `:483–521`, `:189–207`.
- Expected / actual: design 7 requires renewal and all-child drain before shared-state/pointer mutation and before releasing the reservation. Switch drains at `:431`, then copies/verifies and waits at the slow barrier, writes the candidate pointer at `:465`, and only renews/drains at `:518–521`. Rollback drains at `:484`, restores monitoring at `:499`, waits at its barrier and changes selection at `:509`; its final renewal/drain is likewise afterward. Marker drift/expiry or a newly unknown/undrained child discovered after the slow phase therefore fails after mutation, rather than refusing safely beforehand.
- Drain completeness: `:139` silently skips a missing CIM `ExecutablePath`. `:152` treats any raw argument text not beginning `tray` as a harmless non-tray invocation. The actual CLI supports recursive `--data-dir` options (`CommonOptions`) and quoted command tokens; an accepted `bell-tower.exe --data-dir <root> tray` or `bell-tower.exe "tray" --data-dir <root>` can be omitted from the drain. A child already past its old startup check must not be classified as safe merely by those raw-string omissions.
- Restart validation: the launched child receives an activation environment value, but `Read-Owner` drops activation and `Start-SelectedTray` polls only version. Candidate ancestry is checked, but the original-version Rollback restart does not receive that lineage check. This does not complete the required activation/process verification if an omitted child acquires ownership after reservation release.
- Reproduction / evidence: source ordering is definitive. In a disposable Switch/Rollback barrier, expire or semantically drift the marker and introduce a paused/unknown old-order child during the slow phase, then release. Observe selection/state mutation before the final check can reject it. For completeness, use a valid alternate CLI spelling for a child paused after successful `CanStartTray`. These runtime probes were requested from QA; this report does not assert them executed.
- Recommendation: perform ownership/freshness renewal and a complete, conservative startup classification/drain before each specified mutation boundary and again before release. Refuse unknown images/ambiguous accepted argument forms; use actual argument semantics rather than assuming a nonmatching prefix is harmless. Verify exact operation-owned restart identity and activation for both directions. Keep waits outside launcher/data gates and never kill an unknown process.
- Risks / verification: preserve old 0.1.8 startup semantics, normal event/helper traffic and mixed sessions. Check pre-mutation unchanged selection/state on expiry/drift/undrained refusal, alternate valid tray argv, and authorized old/dev restarts; keep all delayed-phase hook/durability tests passing.
- Evidence type: source/control-flow observation. Relationship: incomplete correction of canonical ANALYSIS-150 / QA-150 and ANALYSIS-006.

### ANALYSIS-201: Recovery records are incompletely bound, and a failed Rollback cannot recover its own target tuple

- Severity: warning; blocks marker preservation and supported manifest-bound rollback recovery.
- Locations: `scripts/install-dev.ps1:122–132`, `:180–186`, `:379–415`, `:545–550`; `UpdateTransaction.EnsureNoCoordinator` recovery overload.
- Expected / actual: pending recovery validates root, data directory, original/target versions and ended prior owner. Restart recovery checks only ID/token at `:391`. `Set-OwnedRecordsFresh` also checks only ID/token and then replaces every other field. A restart record retaining ID/token but changed root/data/versions/owner is accepted, overwritten and ultimately deleted, contradicting exact owned-record drift preservation. The captured pending/restart hashes are not used to enforce these boundaries.
- Separate own-recovery failure: every retry passes `manifest.candidateVersion` as recovery target at `:381/:386`. However, a Rollback's new coordination plan sets `Target=manifest.originalVersion` at `:412`. If Rollback restores the original pointer and then its restart fails, the retained records have that legitimate original target. A subsequent Rollback with the same manifest rejects those records before recovery as an interrupted update. The supplied recovery test fabricates the candidate target, covering a Switch-failure shape only.
- Reproduction / evidence: modify only a matching restart record's root/data/owner with ID/token unchanged, then invoke matching dead-owner recovery; current code rewrites it. Separately, fail authorized restart after Rollback restores selection, let its coordinator end and retry the same manifest; the recovery target comparison fails. These are source-derived probes, not analyzer-executed native trials.
- Recommendation: validate both records against the complete expected operation/phase identity and prior owner before renewal/deletion; preserve mismatched bytes. Record or derive the correct phase-specific recovery target so a failed Switch and a failed Rollback can each recover their own retained records without admitting unrelated plans. Keep the ordinary updater overload unchanged.
- Verification: matching ID/token with each semantic field independently drifted must refuse without rewriting either record or changing selection/state; unchanged matching dead-owner records must recover after both Switch and Rollback restart failures. Never reclaim a live or unverified owner.
- Evidence type: concrete source/tuple mismatch. Relationship: regression in the ANALYSIS-150 fix's added coordination recovery, not an optional hardening suggestion.

### ANALYSIS-202: An ended tray's stale presence file blocks normal Preflight and failed-restart Rollback

- Severity: warning; blocks required stopped-tray and recovery behavior.
- Locations: `scripts/install-dev.ps1:86–98`, `:349`, `:396`; `src/BellTower/Tray/TrayHost.cs`; `TrayApplication.ExitThreadCore`.
- Expected / actual: an ended known tray must be distinguished from actively unverified ownership. Previously `Read-Owner` returned null when the tray-instance object was absent. It now always reads an existing `tray.json` and throws if its recorded PID/start identity has exited. Actual tray shutdown does not delete `tray.json`. Therefore starting and cleanly stopping a disposable tray leaves a file that makes subsequent Preflight refuse, despite no live owner. More critically, a candidate restart failure after the original was stopped leaves stale original presence; Rollback reads it at entry and refuses before recovering selection/monitoring.
- Reproduction: start and exit only a disposable tray, retain its ordinary `tray.json`, confirm its exact process identity ended and no tray object remains, then run Preflight. For recovery, cause a candidate restart failure after selection and use its matching operation manifest for Rollback without deleting the ended original tray's presence file. Current code throws "Tray presence ownership cannot be verified." rather than treating that ended record as no live owner. QA is probing the native cases independently.
- Recommendation: retain the reservation-aware distinction between named-object existence and actual monitoring ownership, while classifying proven-ended presence as stale/non-running. Keep unknown/access-denied or live mismatched ownership fail-closed; do not delete/ignore an active unverified owner merely to pass recovery.
- Verification: absent presence, proven-ended stale presence, live verified ownership, PID reuse and actively unverified presence require distinct assertions. Native stopped-tray Preflight and failed-restart Rollback must work without manual file deletion; unrelated owner/processes remain untouched.
- Evidence type: source regression against actual exit behavior. Relationship: introduced by the ANALYSIS-150 reservation-aware owner helper change.

## Re-verification

| Finding | Current target result |
| --- | --- |
| ANALYSIS-150 / QA-150 | Ordinary acquisition is fenced by the named-object reservation, but complete safe boundary/recovery acceptance remains failed as ANALYSIS-200/201/202 |
| ANALYSIS-001 / QA-001 | Prior shared retirement source correction retained; no new regression found in this recheck |
| ANALYSIS-006 | Slow launch-gate work remains removed; owner/failure safety prevents overall acceptance |
| QA-005 | Named selection/payload and expected/actual diagnostics retained; source correction carried forward |
| ANALYSIS-002 docs | Automatic cleanup and one-level rollback text retained; new fence/recovery claims need the required behavior above |

## Assessment and limitations

The final target is not ready for completion. The reservation direction is compatible with old payloads, but three concrete startup/recovery blockers remain. No new optional suggestions were raised, no original deferred suggestion was reopened, and no implementation was edited. The orchestrator owns triage and the workflow's final-fix-round decision.

All affected behavior is shared by Claude Code, Codex and simultaneous sessions. NativeAOT capture bypasses launcher contention, but neither source review nor package selection proves that the user's existing hooks were migrated or reproduces the reported field cause. Runtime/native timing, durable-count and lifecycle evidence remain QA 004's responsibility. No user tray, agent, personal provider configuration or live installation was touched by this reviewer.

```text
status: done
role / client / model: code-analyzer / Codex CLI 0.164.0 delegated subagent / gpt-6.1-sol
task / finding IDs: ANALYSIS-150/QA-150; regression ANALYSIS-001/006, QA-001/005; new ANALYSIS-200, ANALYSIS-201, ANALYSIS-202
changed or report paths: reports/analysis-004.md; evidence/analysis-004-identity-before.txt; evidence/analysis-004-identity-after.txt
checks: source recheck -> failed safety acceptance (three blockers); git diff --check -> passed; skill byte hashes -> identical; build/tests/SDK format/native/benchmark -> not performed (QA owns outputs)
target identity: before/after head=b74a12b baseline=b74a12b files=27 diff=41ba2dbe9660 untracked=10/88d345b46144
limitations / decisions needed: startup/recovery blockers remain after final allowed fix/recheck round; runtime probes belong to QA; field cause unverified
next: orchestrator triages current reports and obtains the required user decision; do not silently approve or start a third fix round
```
