# Analysis report: install-dev-builds / 003

## Review target

- Reviewer / role: `analyze_dev_builds` / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 (`codex --version`), delegated subagent, `gpt-6.1-sol`, high reasoning.
- Date: 2026-10-24 UTC.
- Change root / assignment: `D:/Code/ZeusApps.BellTower/openspec/changes/install-dev-builds` / Analysis 003, resumed first recheck.
- Requirements: [tasks 1.1–6.3](../tasks.md), especially 4.1–4.3 and 5.2; [supported installation, retirement and hook-delivery requirements](../specs/development-builds/spec.md); [design decisions 4 and 7](../design.md).
- Scope: `src`, `tests`, `scripts`, `docs/development.md`, `docs/releases.md`, `docs/installation.md`, and both `install-dev-build` skill directories. Neighbors inspected: launcher selection/lease startup, `TrayHost`, `TrayController`, `BellTower.psm1`, immutable ingress provisioning and shared data locks. No implementation was edited.
- Baseline: `b74a12ba20eb9d3595204b41295b9dab73b7895d`.
- Before identity: `head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768`; tracked diff SHA-256 `5b2efc8567add9e68e2d8812949bb970fed4496692f8a923328bcc44f6e821b7`. Full manifest and untracked hashes are in [identity evidence](../evidence/analysis-003-identity.txt).
- After identity: the same command at the end of inspection returned the identical summary, tracked hash and untracked hashes; `Compare-Object` returned no differences. The [after identity](../evidence/analysis-003-identity-after.txt) retains that output. No unexpected target mutation was observed. Only this report and assigned identity evidence were written.
- Prior reports: [Analysis 001](analysis-001.md), [retention map](../evidence/analysis-001-retention-map.md), [QA 001](qa-001.md). Rechecked IDs: ANALYSIS-001, ANALYSIS-002 documentation, ANALYSIS-006 and QA-005 regression. New allocation: ANALYSIS-150–199. Optional prior suggestions were not reopened.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared retention and removal | Source inspection of `UpdateTransaction.cs:188–281` and installer `:207–222`; comparison with baseline diff and Analysis 001's rule map | Cleanup and Retire share `RetainedVersions` and `TryRemovePayload`; Retire adds the required dev-marker filter. Selected, previous, recovery-retained and leased versions remain protected. Owned-file inventory and file hashes remain prerequisites for removal | Passed source recheck, both providers and mixed sessions through one shared implementation. Runtime refusals and native retirement are QA's checks |
| Lock lifetime for cleanup | Trace update gate → short launch gate → exclusive lease → release launch → scan/hash/delete | `TryRemovePayload` acquires the exclusive lease under launch at `:233–246`, then scans, invokes the internal checkpoint, verifies and deletes at `:250–265`. The launcher already acquires its shared selected-version lease under launch | Passed source recheck. Slow removal no longer owns the launch gate; no timing measurements performed here |
| Installer hook continuity | Trace script Switch/Rollback and compare with design 7 | Outer gate is now `update`; payload copy/hash and tray waits are outside launch/data locks. Pointer compare/write alone is under launch. The update gate is released before restart, avoiding startup-cleanup lock inversion | Slow-launch-gate defect is addressed in source, but new tray ownership can appear during the open launch window: ANALYSIS-150 |
| Pending update refusal | Inspect `EnsureNoCoordinator` and installer call before branch-specific stop/write | Active and interrupted `updates/pending.json` both refuse while preserving recovery files. Script uses the updater's shared policy | Passed source recheck; active/stale functional cases are delegated to QA |
| Rollback and drift diagnostics | Inspect snapshot scope, hash comparisons and owner descriptions | Selection and original payload refusals name the affected file and expected/actual hash. `Assert-SameOwner` names PID, creation ticks and executable. Rollback restores only the specified monitoring/selection files | QA-005 source correction present. Snapshot semantics are preserved; late-owner protection needs ANALYSIS-150 |
| Test seams | Inspect internal checkpoint, IVT and disposable script barrier | Callback is internal, scoped to exact installation/version by its sole test and reset in `finally`. Script barriers require `-Disposable` and paths below `artifacts`; no environment/production switch enables the callback | Passed source review. Existing test directly exercises Cleanup; Switch/Rollback/Retire native evidence still belongs to QA |
| Documentation correction | Read both skill copies and development/release docs | `-Legacy` is gone; unmarked payloads are explicitly refused by Retire and left to automatic cleanup; rollback is described as one level | ANALYSIS-002 assigned documentation portion passed. Optional Preflight removal listing remains deferred |
| Whole-target version/update coherence | Read current contract, formatter and all changed callers; compare baseline diffs | Shared effective-version helper is used for discovery/check/staging; one display formatter feeds version surfaces; stable offers reject dev versions; old strict launcher format remains compatible | No new regression found in these unaffected parts. Carry forward Analysis 001's architectural coverage for tasks 1.1–3.2; this is source continuity, not re-executed acceptance |
| Skill parity | `Get-FileHash` SHA-256 on both copies | Both are `e3e2bfdf78d61419410131db69b76acf994d83d0e7da002d9e374a49ba240846`; same shared scripts and policy | Passed byte-parity inspection for Claude Code/Codex. Discovery observations are supplied by the orchestrator's evidence; no provider CLI/model session was launched here |
| Whitespace regression | `git diff --check -- src tests scripts docs/development.md docs/releases.md docs/installation.md` | Exit 0; only Git line-ending notices | Passed for tracked diff. This is not an SDK format verification |
| Build, tests, SDK format, package, native cycle and benchmark | Not run | QA exclusively owns shared build/package outputs for this round | Not performed to avoid concurrent output mutation. No runtime or field performance pass claimed |

## Findings

### ANALYSIS-150: A new or delayed tray can take ownership during the shortened installer window, after the last owner check and before selection changes

- Severity: warning; breaks the agreed owner-drift/safe-switch acceptance.
- Related requirements: task 4.1, task 4.3; "Unexpected owner or changed state"; design 7 requires preserving verified ownership while freeing the hook launch path.
- Locations at this reviewed identity: `scripts/install-dev.ps1:301–336`, `:350–379` and `:386–395`; `src/BellTower/Tray/TrayController.cs:20–43`; `src/BellTower/Tray/TrayHost.cs:9–16`; `src/BellTower/Integration/BellTower.psm1:89–91`.
- Observation: Switch checks/stops the recorded owner at `:301`, then copies/verifies the candidate with the launcher open, and finally changes `current.json` at `:335`. It checks the selection hash before that write but never checks whether another tray appeared. Rollback similarly stops the candidate, restores monitoring and later writes the original selection without rechecking ownership. A normal new agent launch invokes `start` from the integration module. `TrayController.Start` does not acquire the installer's update gate; it checks only the restart marker and tray-instance mutex. The installer writes no restart marker. Thus a new old-version tray can start during the deliberately slow phase and become the monitoring owner; the installer still changes selection and only discovers the mismatch when its subsequent restart waits for the requested version. It can leave a selected candidate with another tray still monitoring, or restore monitoring under a newly started tray during Rollback.
- Delayed-start variant: a launcher child can have been started before the final short launch section but publish the tray-instance/presence afterward. A single `Read-Owner` check immediately before the pointer write cannot cover that in-flight child by itself. Specifically, current and v0.1.8 `TrayHost.Run` call `CanStartTray` before creating the tray-instance mutex and have no second marker check: a child can pass the check, pause, then resume after marker creation and an empty-owner check to acquire ownership anyway. `UpdateTransaction.Apply` writes its restart marker before stopping the tray, but it does not separately drain or fence that pre-marker in-flight startup, so the updater's sequence alone does not prove this edge safe.
- Reproduction: on a fresh disposable installation, Preflight with an original tray; start Switch with `-TestBarrierDirectory`; when `switch-before-selection.entered` appears, start another original-version tray via that installation's stable launcher (or pause an already-started child before tray-instance acquisition); wait for its ownership, then release the barrier. By the inspected control flow, Switch still writes the candidate pointer instead of refusing before selection mutation. Repeat around `rollback-before-selection` for rollback. Native execution of this scenario was requested from QA and is not asserted as performed in this report.
- Proposed correction and benefit: establish a fenced/atomic tray-start ownership boundary across the operation, using the existing restart/activation identity where applicable, and verify that only the operation's recorded/authorized tray can own monitoring before selecting or restoring. Cover delayed already-launched children as well as newly requested starts; marker presence plus a final owner read is insufficient by itself. Old payloads' single-check startup requires explicit safe handling without rewriting their bytes. Preserve existing marker bytes/ownership and recovery, and leave unrelated/newly discovered trays running rather than stopping them. Do not restore the long launch-gate hold or increase hook deadlines.
- Compatibility evidence: `git show v0.1.8:src/BellTower/Tray/TrayHost.cs` and the same tag's `UpdateTransaction.cs` show `CanStartTray`/`RestartPending` already exist in the original released source. This supplies a compatible coordination path for investigation; it is source inspection, not a run of that old binary.
- Behavior-preservation risks: marker identity, process creation evidence, stale-marker handling, owned restart allowance and failure cleanup must remain safe. Marker protection must cover the stop/selection/restart interval without blocking event ingress. Preserve the user's running sessions and personal provider settings.
- Verification plan: deterministic disposable Switch/Rollback barriers with a new tray-start request and a delayed child paused after a successful `CanStartTray` check but before tray-instance acquisition; assert refusal before selection/state mutation or suppression of the unowned start, correct operation-owned restart, unchanged unrelated owner, marker byte/owner preservation, and successful Claude/Codex/mixed legacy and AOT hooks while the phase remains blocked. Existing drift, pending-update, snapshot and lease cases must continue passing.
- Evidence type: concrete source/control-flow regression; not a measured field failure.
- Relationship: regression introduced while fixing ANALYSIS-006; does not reopen other deferred suggestions.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 / QA-001 root cause | `5b2efc8567ad`, untracked `10/3e9c4782a768` | One shared C# retention/removal path; no `-Legacy`; validated dev marker required for explicit Retire | Source correction verified; runtime recheck belongs to QA |
| ANALYSIS-002 documentation portion | Same | Development/release docs and byte-identical skills explain automatic unmarked cleanup and one-level rollback | Verified by inspection; optional Preflight listing not rechecked because deferred |
| QA-005 | Same | Named payload/selection and expected/actual hashes; owner description includes PID, creation and executable | Source correction verified; functional diagnostics recheck belongs to QA |
| ANALYSIS-006 | Same | Slow payload work outside launch; update serialization and short exclusive-lease acquisition present | Original slow-gate mechanism corrected in source; owner-safety regression ANALYSIS-150 blocks acceptance; four-phase/benchmark evidence still pending QA |

## Assessment and limitations

The retirement policy is shared and the explicit dev-only boundary is now coherent with docs and both clients. The architecture for letting hooks launch during maintenance is sound for payload cleanup, but installer ownership must be coordinated through the now-open startup interval. ANALYSIS-150 needs correction before acceptance. There are no new optional suggestions in this recheck.

Provider policy remains shared: the defect and its required fix affect Claude Code, Codex and simultaneous sessions equally. NativeAOT ingress bypasses the launcher; successful isolated captures do not prove a live machine's existing hooks were migrated. No authenticated work-machine reproduction, native operation, performance benchmark, build or test was performed by this reviewer. QA retains those checks and must label them separately. No personal settings, user tray, provider processes or implementation files were changed.

```text
status: done
role / client / model: code-analyzer / Codex CLI 0.164.0 delegated subagent / gpt-6.1-sol
task / finding IDs: 4.1–4.3, 5.2, 6.3; ANALYSIS-001, ANALYSIS-002 docs, ANALYSIS-006, QA-005; new ANALYSIS-150
changed or report paths: reports/analysis-003.md; evidence/analysis-003-identity.txt; evidence/analysis-003-identity-after.txt
checks: source/control-flow rechecks -> passed except ANALYSIS-150; git diff --check -> passed; byte hashes -> identical; build/tests/SDK format/native trials -> not performed (QA owns outputs)
target identity: before and after head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768
limitations / decisions needed: ANALYSIS-150 blocks installer owner safety; runtime hook/benchmark/field evidence not performed here
next: orchestrator triages ANALYSIS-150 and routes a bounded developer fix, followed by independent recheck
```
