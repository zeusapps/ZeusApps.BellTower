> Saved verbatim by the orchestrator. Reviewer: code-analyzer (Analysis 011). Client: Claude Code 2.1.310, delegated subagent (auto mode), model claude-opus-5-5 (role default). The client refused the reviewer's own write with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." This is the known restriction recorded in docs/agent-workflow.md. The evidence files under `../evidence/analysis-011-*` were written by the reviewer itself.

# Analysis report: install-dev-builds / Analysis 011

## Review target

- Reviewer / role: delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (delegated subagent, auto mode) / claude-opus-5-5
- Date: 2026-10-25T12:33Z
- Change root / assignment: `openspec/changes/install-dev-builds` / Analysis 011 (review of Implementation 011)
- Task and requirement references: task 4.1; LIVE-001, LIVE-002, LIVE-003 ([findings](findings.md)); handoff [implementation-011](../evidence/implementation-011.md); design decision 7 (startup fence and drain, `design.md:106`)
- Scope and relevant neighboring paths: `scripts/install-dev.ps1` (drain, `Start-SelectedTray`, `Restore-StoppedTrayAfterFailure`, Switch/Rollback main block and recovery classification), `tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1`, `tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs`, `docs/development.md` Recovery bullet. Neighbors read: `src/BellTower/IO/TrayInvocationCommand.cs`, `src/BellTower/Cli/*Command.cs`, `src/BellTower/Updates/UpdateTransaction.cs` (`CanStartTray`, `RestartPending`, `EnsureNoCoordinator`), `src/BellTower/Tray/TrayHost.cs`, `src/BellTower/Tray/TrayController.cs`, `src/BellTower.Launcher/Program.cs`, `src/BellTower.Distribution/Installation.cs` (`Payload`), both `install-dev-build` skill copies (identical), the native evidence scripts and results of Implementation 011.
- Baseline commit: `84dac98`
- Implementation identity: [before](../evidence/analysis-011-identity-before.txt): `head=84dac98 baseline=84dac98 files=4 diff=b9d93bc99de2 untracked=0/e3b0c44298fc` (tracked diff SHA-256 `b9d93bc99de21dac04a3e9d5d8a1f1341f19a4f73bd4ffdf3487414e9e98b933`, no untracked files in scope). Matches the expected identity.
- Before/after identity: [after](../evidence/analysis-011-identity-after.txt): identical summary line and diff hash. No unexpected mutation.
- Prior reports / rechecked findings: [Analysis 007](analysis-007.md), [Analysis 009](analysis-009.md) (ANALYSIS-450); regression view of ANALYSIS-200/201. New findings ANALYSIS-550 to ANALYSIS-555.

Line numbers below refer to the working tree at the reviewed identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| (a) Drain re-scan | Traced `Get-InstallationTrayScan` (404-444) and `Wait-InstallationTrayStartsDrained` (446-466) for each transition: new, persistent and churning unclassified processes; verified start that persists; verified start that turns unclassifiable | The early return still requires a fully empty scan (455). A verified start that never drains still refuses at 466, unchanged. A persistent unclassifiable identity refuses at 465 after the deadline. A process first seen in the final scan is dropped. So is a process that was a verified start in the last in-loop scan and is unclassifiable in the final one, because `$previous` holds only unclassified keys. | First-seen-at-final is equivalent to a start just after an empty scan, so it is accepted by design (the marker and reservation fence it). The start-to-unclassified transition loses continuity: ANALYSIS-554. |
| (a) Root cause of LIVE-001 | Read-only probe [analysis-011-classifier-probe.ps1](../evidence/analysis-011-classifier-probe.ps1) ([results](../evidence/analysis-011-classifier-probe-results.json)): classifies synthetic command lines with the Release `TrayInvocationCommand` and compares its `NonTrayCommands` with the CLI root command names | `statusline` throws `unknown Bell Tower command` and `status-line` returns `NotTray`. The CLI registers `statusline` (`StatusLineCommand.cs:8`), but the classifier lists `status-line` (`TrayInvocationCommand.cs:16`). Every live Claude status-line payload process is therefore unclassifiable for its whole lifetime, not just while its command line is empty. | ANALYSIS-550 |
| (a) ANALYSIS-450 | Compared the skip path before and after | `Test-TrayProcessEnded` and the `CIM start identity differs` → helper → skip route are unchanged. Re-scanning evaluates the same CIM value and live start again, so the deterministic result does not change. The new key uses the same CIM tick conversion. | Not made worse. ANALYSIS-450 is unchanged. |
| (b) Restart path and fence | Traced `Restore-StoppedTrayAfterFailure` (715-759) → `Start-SelectedTray` (476-500) → `Assert-OwnedRestart` (501-514). Checked `CanStartTray`/`RestartPending` (`UpdateTransaction.cs:16-33`). | The restart uses the same launcher, the coordination plan ID as activation, the verified ancestry and the version check. The fence holds while the installer (plan owner) is alive and the plan is under 10 minutes old. Restore does not renew the plan or recheck the selection bytes before launching. | Same verified path. Bounded gaps: ANALYSIS-555. |
| (b) Record cleanup and failure branches | Traced every branch of 723-758 against the `finally` (1137-1143), which now skips cleanup when `$restartStoppedTray` is set | The live-owner branches (726-728) neither save nor remove records. The failure branch removes records only when the manifest has no `restartLauncher`. A post-launch failure keeps Switch-phase records, and Rollback recovery then hashes `versions/<candidate>/release.json` (919), which does not exist before the payload copy. | ANALYSIS-551, ANALYSIS-552 |
| (b) Reset to `prepared` and retry | Traced the retry Switch (876-879, 1019-1031) and Rollback routes (697) after each Restore outcome | The retry rechecks the owner (`Assert-SameOwner` against the restarted `originalTray`), the selection hash (1024) and compatibility (1025), so foreign owners and selection drift are refused. `$untouched` ignores a partly created `before-monitoring` folder. After a non-retryable restart the state stays `original-stopped`/`ready-to-select` with a live tray, yet the early hint (877) points to Rollback. The never-selected Rollback then counts that live tray as an undrained start (the `expectedStoppedTray` loop body at 457-461 has no effect; this predates the round) and refuses after 30 s. | ANALYSIS-553 |
| (b) Rollback candidate receipt | Traced 732-735 against `Assert-OwnedCandidate` (523-551) | `candidateTray` and `candidateActivation` are taken only from an owner that passed `Assert-OwnedRestart`: the activation equals this plan ID and the tray descends from this operation's launcher. A foreign tray that knows the plan ID from the marker fails the ancestry check. A retried Rollback then requires an exact PID/ticks/executable match and the same activation. | No foreign-owner acceptance found |
| (b) ANALYSIS-200/201 and the write-ahead intent | Checked where `monitoringMutationStarted` and `selectionMutated` are set (900, 1052, 1073, 1079, 1109, `Complete-RollbackPointer` 664), and the routes | Restore runs only when neither flag is set. Every pointer-intent route sets `monitoringMutationStarted` before any work, so Restore never runs with a `rollbackPointerTransition` present. In the restore-monitoring route the only write before the flag is the private `savedDevMonitoring` aside. | No regression found |
| (c) Shell-execute launch | Read 476-500 and compared with `TrayController.Start` and the launcher | It launches the stable launcher with the same arguments. The activation is set for the single `Process.Start` call and restored in `finally`, including on exception, and nothing else in the single-threaded installer starts a process inside that window. `Assert-OwnedRestart` is unchanged and the ancestry still holds (ShellExecuteEx returns the launcher process, which waits for the payload). The native run closed every captured pipe. Trade-off: the launcher's own failure text (`bell-tower launcher: …` on stderr) now goes to a hidden console instead of the installer output. | No leak or race found. The diagnostic trade-off is noted only. |
| (d) 3 s hook budget | Checked for new work under `Open-LaunchGate` | `Invoke-GuardedPointerStep` is unchanged. The longer drain still runs in `Assert-OperationBoundary`, outside the launch gate. Restore runs after `finally` has released both gates. | No new work under the launch gate |
| (e) Parity | Shared path inspection plus the probe | One installer and one classifier serve both clients. The Codex commands (`event`, `prepare-codex-runtime`, `codex-runtime-host`, `stop-codex-runtime`) all classify. Only Claude's `statusline` is missing, so the LIVE-001 trigger is a Claude-only failure inside a shared policy. | ANALYSIS-550 (parity gap in shared data) |
| (f) Proportionality and tests | Read harness 249-312 and InlineData | The size is proportional (about 115 script lines). The harness covers transient, churn and persistent unclassified processes, plus the Switch restart success and pre-launch failure. Not covered: the live-owner branches, a post-launch restart failure, the Rollback candidate branch in the harness (native case C only), start-to-unclassified continuity, and a `before-monitoring` leftover. `$script:trayDrainSeconds` is an ungated test seam; it is harmless because script scope is not inherited. Native fault case B injects only before launch (`fail-start-<version>`). | Gaps are listed under each finding |
| Build, tests, format | Not run | QA owns them | Not performed |

## Findings

### ANALYSIS-550: The tray-start classifier does not know the `statusline` command, so every live Claude status-line process stays unclassifiable

- Severity: warning
- Related task / requirement: 4.1; LIVE-001; design 7 (refuse only on unknown startup children)
- Locations: `src/BellTower/IO/TrayInvocationCommand.cs:16` (`"status-line"` in `NonTrayCommands`), `src/BellTower/Cli/StatusLineCommand.cs:8` (`base("statusline", …)`), `src/BellTower/Setup/ProviderSettingsEditor.cs:168` (Claude status line built as `statusline`), consumed by `scripts/install-dev.ps1:427`
- Observation: The probe shows that `Classify("<payload> statusline --provider claude --data-dir <d>")` throws `InvalidDataException: Process command line has an unknown Bell Tower command.`, while `status-line` returns `NotTray`. The `statusline` command is the only CLI root command the classifier does not know. So a Claude status-line payload process is unclassifiable for its entire lifetime, not just while CIM reports an empty command line. The handoff and the docs give that empty-command-line window as the cause.

  The re-scan masks this as long as some scan within 30 s is empty. That holds for the measured churn (one process every ~200 ms, Switch ~5 s). Two cases still refuse with `stayed unclassifiable until the drain deadline`:
  - Several busy Claude sessions keep at least one status-line process alive in every scan for 30 s. This is a performance hypothesis and was not measured.
  - A single status-line process outlives the last two scans.

  Every drain at an operation boundary that reaches the deadline also costs the full 30 s while the tray is stopped. Codex commands all classify, so this Claude-only failure mode sits in shared policy data.
- Proposed improvement and benefit: Add `statusline` to `NonTrayCommands`. Keep `status-line` unless it is proven unused by any retained payload. Add a test that derives the CLI root command names (as the probe does, or from the root command builder) and asserts that each one except `tray` is classified as `NotTray`, so a renamed or new command cannot reopen the gap. Status-line processes then classify immediately, and the re-scan remains as the guard for the genuinely transient empty-command-line case.
- Behavior-preservation risks: Only `NotTray` classification widens, and only for an exact verb the CLI defines. Tray detection and `--data-dir` parsing are unchanged. This needs a `src/` change, and the installer loads the classifier from the Release build.
- Evidence type: measured result (read-only probe), plus a performance hypothesis for the multi-session case
- Verification / measurement plan: a unit test in `TrayInvocationCommandTests` for `statusline`; the command-coverage test; rerun the native churn of `implementation-011-native.ps1` and record the drain durations (expect immediate returns)
- Relationship: root cause of LIVE-001, which is a co-cause with the empty-command-line window; independent of ANALYSIS-554

### ANALYSIS-551: When a live owner is found after the failure, the restore path leaves the coordination records behind

- Severity: warning
- Related task / requirement: LIVE-002; the "clears its own records" contract in `docs/development.md`
- Locations: `scripts/install-dev.ps1:726-728` (live-owner branches), 1141-1142 (`finally` skips cleanup when `$restartStoppedTray`), 131 (`stoppedTray` set before `RequestExit`)
- Observation: `$script:stoppedTray` is set before the exit wait. A `Verified tray did not exit` timeout therefore also reaches `Restore-StoppedTrayAfterFailure`, as does the `Another tray … now owns monitoring` case. In both branches the function throws its note without calling `Remove-OwnedCoordinationRecords`, and the `finally` has already skipped the removal. Before this round the `finally` removed them.

  After the installer exits, `updates/pending.json` and the restart marker remain with a dead owner:
  - The product's stable update refuses with `An interrupted update needs inspection` (`EnsureNoCoordinator`, `UpdateTransaction.cs:337`).
  - The next installer run refuses at 906-907 or 960.
  - A same-manifest Rollback takes the recovery path with phase `Switch`. Before the payload copy it then fails on the missing candidate `release.json` (see ANALYSIS-552).

  Manual record removal is the only exit.
- Proposed improvement and benefit: Restore launched nothing in these branches, so remove the owned records there (`Remove-OwnedCoordinationRecords`, under the same `Assert-OwnedRecordPair`). This restores the pre-round cleanup. Alternatively, set `stoppedTray` only after the exit is confirmed, which sends the stop timeout back through the ordinary cleanup.
- Behavior-preservation risks: The records protect only the restart fence. No tray was launched by Restore in these branches, and record removal still verifies the exact bytes and tuple.
- Evidence type: source observation
- Verification / measurement plan: harness scenarios `stopped-tray-still-running` and `stopped-tray-foreign-owner` (stub `Read-Owner` returning the stopped or another owner) that assert both records are absent and the message is kept; a mutation that drops the removal must fail
- Relationship: new in this round (regression of the pre-round failure cleanup)

### ANALYSIS-552: A post-launch restart failure keeps Switch-phase records that the recommended Rollback cannot recover before the payload copy, and the message says monitoring is stopped although a tray may run

- Severity: warning
- Related task / requirement: LIVE-002; the "Rollback recovers it" fallback in `docs/development.md`
- Locations: `scripts/install-dev.ps1:750-756` (failure branch keyed on the `restartLauncher` property), 919 (recovery hashes `versions/<phaseTarget>/release.json`), 981 (Switch computed the plan's contract hash from `preparedPayload/release.json`), `src/BellTower.Distribution/Installation.cs:28-34` (`Payload` reads `release.json`)
- Observation: Suppose `Start-SelectedTray` records `restartLauncher` and then fails, either because the tray did not confirm within 30 s or because `Assert-OwnedRestart` refused. The records are kept and the message says `Monitoring is stopped. Recover it with -Action Rollback`. The likeliest failure point, the drain refusal (LIVE-001), happens before the payload copy. The same-manifest Rollback then:
  1. reaches the pending-record branch with phase `Switch`;
  2. computes `Hash (Join-Path ($installation.Payload('<candidate>')) 'release.json')`, which throws because `versions/<candidate>` does not exist;
  3. ends with an opaque file-not-found error and the records still in place (`coordinationPlan` is not yet set, so the `finally` does not clean up).

  Two related defects:
  - The message is wrong when a tray was launched but not verified: it may be running.
  - In Rollback, `restartLauncher` already exists from the earlier Switch, so the property test cannot tell whether this Restore call launched anything. A pre-launch failure there keeps records unnecessarily. That case is recoverable, because a Rollback-phase target payload exists.

  The harness and native case B inject only before launch, so neither covers this path.
- Proposed improvement and benefit:
  1. In recovery, derive the Switch-phase expected contract from `$manifest.preparedPayload/release.json`, exactly as Switch computed it at 981, instead of from the possibly absent installed payload.
  2. Decide record retention from whether this call reached `Process.Start` (for example, compare the `restartLauncher` value before and after), not from property presence.
  3. When a launch happened, say that a tray may be running unverified, rather than `Monitoring is stopped`.

  The documented fallback then works for the most likely failure point.
- Behavior-preservation risks: The contract check stays exact: the prepared payload is inside the private workspace and is the verified source of the copied payload. The recovery tuple's other checks are unchanged.
- Evidence type: source observation
- Verification / measurement plan: a harness or native case that fails after the launch (for example, an injected `Assert-OwnedRestart` refusal) with no candidate payload, then a same-manifest Rollback that must finish with no records; a message assertion for the launched case
- Relationship: new in this round; it makes the ANALYSIS-551 stuck state recoverable too

### ANALYSIS-553: After a non-retryable restart the manifest state and the Switch hint point to a Rollback that refuses, and `$untouched` misses a partial snapshot folder

- Severity: suggestion
- Related task / requirement: LIVE-002 message accuracy
- Locations: `scripts/install-dev.ps1:739-745` (`$untouched` and its notes), 877 (early Switch hint), 697 (never-selected route), 457-461 (the `expectedStoppedTray` skip has no effect on the count; pre-existing), `Backup-Monitoring` 552-560 (`New-Item` without `-Force`)
- Observation:
  - In the restarted-but-not-retryable case, the state stays `original-stopped` or `ready-to-select` while the original tray runs. A Switch retry prints `It stopped the original tray … recover with -Action Rollback`. Rollback takes the never-selected route with that live tray. Its first boundary drain classifies the tray as a start for this data directory and refuses after 30 s with `A tray startup did not drain`. That refusal is safe, but it is not a recovery.
  - `$untouched` checks only `originalMonitoringFiles` and the candidate folder. If `Backup-Monitoring` created `before-monitoring` and then failed, the state is reset to `prepared` and reported as retryable. Each retry then stops the tray again and fails at `New-Item`, the restore restarts it and reports it as retryable again.
- Proposed improvement and benefit: When the tray was restarted and the operation is not retryable, record that outcome (for example, a terminal state that Rollback's route table rejects at once, or a `trayRestartedAfterFailure` flag) and make the 877 hint say `run Preflight again`. Add `!(Test-Path (Join-Path $manifest.operationDirectory 'before-monitoring'))` to `$untouched`.
- Behavior-preservation risks: Messages and state labels only. The retryable `prepared` path is unchanged.
- Evidence type: source observation
- Verification / measurement plan: harness checks for both notes; one native case with a failure injected after the snapshot, followed by the hinted command
- Relationship: related to ANALYSIS-552; the drain's live-tray behavior predates this round

### ANALYSIS-554: At the deadline the drain drops a process that was a verified tray start in the last in-loop scan and is unclassifiable in the final scan

- Severity: suggestion
- Related task / requirement: design 7 (refuse on non-exiting startup children); LIVE-001
- Locations: `scripts/install-dev.ps1:452-454` (`$previous` holds only unclassified keys), 463-466
- Observation: Take a non-exiting tray start for this data directory that lasted the whole drain. If its classification fails transiently in the final scan (for example `command line unavailable` or a `MainModule` failure while it is still alive), its key is not in `$previous`, so neither throw fires and the drain returns. Before the round, any unclassifiable live process refused. Two things soften the impact. The trigger needs a transition at exactly the final scan. And a later stray owner fails `Assert-OwnedRestart` on activation or ancestry, which keeps the records.
- Proposed improvement and benefit: Build `$previous` from both lists, recording the CIM key of verified starts as well, so continuity holds across start → unclassified. Keep dropping only identities first seen in the final scan, which matches the accepted window after an empty scan.
- Behavior-preservation risks: It can only add refusals, and only for an identity live through both final scans.
- Evidence type: source observation
- Verification / measurement plan: a harness scenario whose fake CIM returns a classifiable tray command line, then an empty one for the same PID and start; it must refuse
- Relationship: independent of ANALYSIS-550 and ANALYSIS-450

### ANALYSIS-555: The automatic restart neither renews the fence nor rechecks the selection bytes before launching through the stable launcher

- Severity: suggestion
- Related task / requirement: LIVE-002 (same fenced, verified path)
- Locations: `scripts/install-dev.ps1:723-731`; compare the success path's `Assert-OperationBoundary 'reservation-release'` at 1130; `UpdateTransaction.cs:23` (10-minute fence)
- Observation: The stable launcher starts whatever `current.json` selects. Restore launches it without checking that the selection still has the recorded bytes (`originalSelectionSha256` for Switch, `devSelectionSha256` for a Rollback candidate) and without renewing the plan. Drift is improbable while the records block other installers and the stable updater, but nothing prevents a manual edit. Plan age exceeds 10 minutes only after a slow phase without a boundary. Either way, the result is a refused verification (version or activation mismatch), not acceptance. That then lands in the ANALYSIS-552 branch, with its inaccurate `Monitoring is stopped`, while a different or hook-started tray may be monitoring.
- Proposed improvement and benefit: Before the launch, compare `Hash $installation.SelectionPath` with the expected bytes and skip the launch with an accurate message on mismatch. Call `Set-OwnedRecordsFresh` when the plan is older than the existing 8-minute bound. Both are cheap and reuse existing helpers.
- Behavior-preservation risks: Neither runs under the launch gate. The renewal uses the existing verified replace path.
- Evidence type: source observation
- Verification / measurement plan: a harness scenario with a changed selection hash that asserts no `Start-SelectedTray` call and an accurate message
- Relationship: related to ANALYSIS-552

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-450 | identity above | The skip logic is unchanged. Re-scans repeat the same deterministic comparison. | Unchanged, not made worse |
| ANALYSIS-200 / ANALYSIS-201 (regression) | identity above | The routes, intent checks, mutation flags and guarded steps are unchanged. Restore is excluded whenever either mutation flag is set, which includes every pointer-intent route. No work was added under the launch gate. | No regression found |
| LIVE-001 / LIVE-002 / LIVE-003 | identity above | Static review only. LIVE-003 is sound in source. LIVE-001 is mitigated, but its root cause is ANALYSIS-550. LIVE-002 works for the tested paths, with the gaps in ANALYSIS-551 to ANALYSIS-553 and ANALYSIS-555. | Not closed by this role; for orchestrator triage |

## Assessment and limitations

- Covered:
  - the full Implementation 011 delta in the script, harness, tests and the Recovery bullet;
  - the neighboring classifier, CLI command names, restart fence, launcher and recovery classification;
  - Claude and Codex parity, both through the shared installer path.
- Findings:
  - LIVE-003 is correct.
  - The drain change is safe for verified starts and for persistent unknowns. Its LIVE-001 premise is incomplete: the `statusline` classifier gap (ANALYSIS-550, warning) is a Claude-only cause in shared data, and a one-word fix plus a coverage test removes it.
  - The automatic restart uses the verified restart path and cannot accept a foreign tray. Two failure branches regress or break the documented recovery: records left behind (ANALYSIS-551, warning) and post-launch failure before the payload copy (ANALYSIS-552, warning).
  - The rest are message, state or hardening suggestions (ANALYSIS-553, ANALYSIS-554, ANALYSIS-555).
  - The `docs/development.md` Recovery bullet ("clears its own records", "Rollback recovers it") holds only once ANALYSIS-551 and ANALYSIS-552 are fixed. Its "just-started process" wording should mention the `statusline` fix if ANALYSIS-550 is adopted.
- Not performed: build, tests, format and native runs (QA owns them), and any multi-session status-line measurement (ANALYSIS-550 is a hypothesis in that respect).
- Writes:
  - `evidence/analysis-011-identity-{before,after}.txt`
  - `evidence/analysis-011-classifier-probe.ps1` and `evidence/analysis-011-classifier-probe-results.json`. The probe only classifies synthetic strings and reflects over a static field; it inspects no process.
  - the report write to `reports/analysis-011.md` was refused by the client, so this text is inline

  No other files were touched, and the review identity is unchanged.
- Decision for the orchestrator: whether to fix ANALYSIS-550 (a `src/` change outside Implementation 011's "no `src/` change") and ANALYSIS-551 and ANALYSIS-552 before the live install.

```text
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (delegated subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 011 (Implementation 011: LIVE-001/002/003); new ANALYSIS-550 (warning), ANALYSIS-551 (warning), ANALYSIS-552 (warning), ANALYSIS-553 (suggestion), ANALYSIS-554 (suggestion), ANALYSIS-555 (suggestion); rechecked ANALYSIS-450 (unchanged), ANALYSIS-200/201 (no regression)
changed or report paths: evidence/analysis-011-identity-{before,after}.txt; evidence/analysis-011-classifier-probe.ps1 + evidence/analysis-011-classifier-probe-results.json; reports/analysis-011.md: write refused, text inline
checks: scripts/review-identity.ps1 before/after -> passed (identical); read-only classifier probe -> run (statusline throws, status-line NotTray); build/test/format/native -> not run (QA owns them)
target identity: before: head=84dac98 baseline=84dac98 files=4 diff=b9d93bc99de2 untracked=0/e3b0c44298fc; after: identical
limitations / decisions needed: the client refused the report write; the report text is inline for the orchestrator to save. Static analysis only; the multi-session status-line exposure is a hypothesis. Decide whether ANALYSIS-550 (src change) and ANALYSIS-551/552 are fixed before the live install.
next: orchestrator saves the report and triages ANALYSIS-550–555 alongside QA 011; a developer correction round if adopted, then QA and analysis rechecks
```
