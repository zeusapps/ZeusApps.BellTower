<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

# Analysis report: restore-claude-badge-identity / Analysis 004 (light recheck of user-authorized round 3)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent launched by the Claude Code main-session orchestrator, auto permission mode; model `claude-opus-5-5`
- Date: 2026-10-24 (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 004 (light recheck of Implementation 007, user-authorized fix round 3)
- Task and requirement references:
  - tasks 2.5, 3.3, 3.4, 3.5 and 6.3 in [tasks.md](../tasks.md)
  - [design.md](../design.md): decision 3 (fork/clear/resume), and "Match first-install/resume/fork/clear outcomes for both clients"
  - [session-identity delta](../specs/session-identity/spec.md): "Git branch and worktree context", "Provider conversation identity recovery", "Provider parity"
- Scope and relevant neighboring paths:
  - `src/BellTower/Core/{IdentityInheritance,HistoryState,SessionIdentity,AlertPolicy,NativeNamePolicy}.cs`
  - `src/BellTower/Sessions/IdentityHistoryReader.cs` (`Harvest.ForkBoundary`, `Refresh`, `Merge`, `ReadCodex`)
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs` (the ANALYSIS-150 block)
  - fixture `tests/BellTower.Tests/Fixtures/identity/codex-rollout-0.164.0-fork-boundary.jsonl`
  - `docs/compatibility.md` and `docs/session-identity.md` (fork rows)
  - The handoff [implementation-007](../evidence/implementation-007.md) was read as claims, not evidence.
  - Deferred suggestions (ANALYSIS-003…011 and 103) stay deferred and were not re-raised.
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src tests scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md docs/hook-ingress.md` (PowerShell 7.6.6, repository root) printed:
  - HEAD `d307c344e22999e48b3c62bc664f0e552fdef3b9`, 96 manifest entries
  - tracked diff SHA-256 `da976a89606d212c6d3bc66c9d2a9319402cee62278b20578650d5c444c23f16`, 51 untracked files
  - `Summary: head=d307c34 baseline=d307c34 files=96 diff=da976a89606d untracked=51/1c8549ea446a` (matches the assignment)
- Before/after identity: after the analysis, the same command printed byte-identical output, including the same summary line; `Compare-Object` over the full output found no difference. The review target was not changed.
- Prior reports / rechecked findings: [analysis-003.md](analysis-003.md) (ANALYSIS-150, ANALYSIS-151) and [analysis-002.md](analysis-002.md) (ANALYSIS-102); [findings.md](findings.md) read for context.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-150 re-run | Re-ran the Analysis 003 `probe-fork-floor.ps1` verbatim for both providers against the reviewed Release build. The build is newer than every `src/BellTower` source and byte-identical to the copy in the test bin | [results §1](../evidence/analysis-004-probe-results.txt) lines 6–23 | Claude: the fork child now matches the parent (`WorkFloorAt=12:00:20 ActiveWork= Subject=repo`).<br>Codex: the Analysis 003 shape has no `thread_settings_applied` event, so the child still re-derives `change-a`. This is the documented floor-only fallback for rollouts without the event (see the primary-source row). |
| ANALYSIS-150, rust-v0.164.0 `Copied` shape | New `probe-fork-boundary.ps1`, case `boundary`. The rollout holds a copied parent snapshot (parent `thread_id`), copied work restamped with the fork time, and then the fork-owned snapshot. The fork then does its own work | [§1](../evidence/analysis-004-probe-results.txt) lines 38–55 | Codex child: `ForkBoundaryOffset=976 ActiveWork= Subject=repo`. Its own work at t=60 is accepted (`change-b`, `WorkBranch=feature`).<br>Claude: the same outcome, through the inherited floor. |
| Boundary across read windows | Case `big`: the copied prefix and the own tail are each about 600 KiB, so the boundary is outside the newest window and is reached by the multi-tick backward walk.<br>Case `sweep`: the tail is sized so that the fresh window starts at the boundary line's start plus a delta (12 deltas) | [§1](../evidence/analysis-004-probe-results.txt) lines 56–75; [§2](../evidence/analysis-004-probe-results.txt) lines 159–180 | Sound:<br>- `big`: Codex finds the boundary at 624526 during the walk. The copied work near offset 400 is excluded, and later own work is accepted. Claude gives the same result.<br>- `sweep`: the newest window cut the boundary line in 7 of 12 runs, and copied work leaked in 0 of 12.<br>- By construction the boundary follows every copied call. A walk from newest to oldest therefore reads it in the same window as the copied calls or an earlier one, and `Min` persists it before `Merge` filters.<br>- So discarding `head.ForkBoundary` in the fresh head read (`IdentityHistoryReader.cs:80-85`) is safe.<br>- The Claude sweep does not apply, because Claude has no boundary line; case `big` covers its multi-window path. |
| Copied prefix and boundary in different incremental windows (the edge the developer noted) | Case `split`: the copied prefix is visible before the boundary line is written, and the boundary is appended afterwards | [§1](../evidence/analysis-004-probe-results.txt) lines 84–91 | The residual is confirmed for Codex. The copied work is applied in the first window (`ActiveWork=change-a WorkBranch=feature`) and stays after the boundary appears, because `Merge` filters calls but does not revert work already applied. Reachability is assessed in the next row. Not raised. |
| Reachability of `split` | Read the rust-v0.164.0 primary sources and Bell Tower's transcript binding | [§6](../evidence/analysis-004-probe-results.txt); `HookIngestor.cs:47`, `CaptureConsumer.cs:52` | Not reachable in normal operation:<br>- `recorder.rs` writes and flushes each line separately, so a concurrent reader could see a partial batch.<br>- But Bell Tower binds a transcript only from a hook's `transcript_path`, and Codex runs SessionStart hooks with a turn context (`hook_runtime.rs:127-131`).<br>- `record_initial_history` persists the prefix and the fork-owned snapshot in one append, then awaits `flush_rollout`.<br>- The remaining path is a rollout write failure in the middle of the batch (`write_pending_items_once` drains only the lines it wrote and retries the rest), with a hook arriving before the retry.<br>- A fresh pass, a branch change or new work clears the effect.<br>Recorded as a known residual. |
| Persistence across fresh passes, rotation and resume | Case `fresh`: the file is recreated with a new fingerprint, then truncated and regrown.<br>Case `resume`: a later fork-owned snapshot, then own work | [§1](../evidence/analysis-004-probe-results.txt) lines 92–133 | Sound:<br>- On each fresh pass, `HistoryState.Reset` sets the boundary to -1 and it is re-derived: 976 after the recreate, -1 after the truncation, 976 after the regrow. `ActiveWork` stays empty and the inherited floor survives.<br>- A later own snapshot does not move the boundary, because the first one is kept (`boundaryUnchanged=True`).<br>- Claude matches. |
| `ForkPersistence::Referenced` shape | Case `referenced`: the fork-owned snapshot comes right after `session_meta`, and nothing is copied | [§1](../evidence/analysis-004-probe-results.txt) lines 134–149 | Codex `ForkBoundaryOffset=248`, and its own work is accepted. This matches `session/mod.rs:1760-1767` (`insert(0, thread_settings_applied)`). |
| Parent unknown or not yet read | Case `orphan`: Bell Tower never saw the parent.<br>Case `unread-parent`: the parent identity exists, but its transcript has not been walked yet.<br>Both providers | [§1](../evidence/analysis-004-probe-results.txt) lines 150–158; [§3](../evidence/analysis-004-probe-results.txt) lines 181–196 | The providers diverge:<br>- Claude recovers `change-a` from the copied records.<br>- Codex excludes them unconditionally and shows `Subject=repo`. It stays that way even after the parent derives `change-a`, because inheritance happens only once.<br>Raised as ANALYSIS-200. |
| Structure of the boundary | Read `Harvest.ForkBoundary` (`:40-41`), the `Min` persist (`:125-126`), `HistoryState.Reset` (`:32`), the `Merge` exclusion (`:234-240`), `ReadCodex` (`:345-353`) and the `Relevant` filter (`:211`) | Source observation | The adapter is thin. The policy (the parent is authoritative up to the fork point) is applied in `Merge`, gated by `ForkParent`.<br>Every non-fork Codex session also persists `ForkBoundaryOffset`, set to its own first snapshot (the parent shows 223 in §1). This is harmless, because the exclusion requires `ForkParent`. Not raised. |
| One-way floor inheritance | Read `IdentityInheritance.cs:10-12`; the probes above; the `…FloorNeverLowers` test | §1 | Sound:<br>- The child gets the larger of its own and the parent's floor, so its floor is never lowered.<br>- Inheritance happens once (guarded by `ForkInheritedFrom`).<br>- A later parent switch on a shared checkout reaches the child through the child's own `ApplyBranch`.<br>- A fork into a different checkout also takes the parent's floor. This follows the rule recommended in Analysis 003; noted, not raised. |
| Primary-source basis | Fetched the openai/codex `rust-v0.164.0` sources into `artifacts/analysis-004-src` (deleted afterwards) | [§6](../evidence/analysis-004-probe-results.txt) | Confirmed:<br>- `ThreadSettingsAppliedEvent.thread_id`: "copied snapshots retain their original owner's ID".<br>- `applied_event` sets `thread_id: Some(session.thread_id())`.<br>- `Copied`: the snapshot is pushed after the copied prefix, in a single `persist_rollout_items` call.<br>- `Referenced`: the snapshot is inserted at index 0. For a paginated subagent the items are cleared first.<br>- `policy.rs:139` persists `ThreadSettingsApplied`.<br>- `EventMsg` uses `serde(tag="type", rename_all="snake_case")`, which produces `thread_settings_applied`.<br>The handoff cites `history/src/rollout_payload.rs` for the wire shape, but that file has no `thread_settings_applied` match. The shape follows from the serde attributes and the established `event_msg` envelope.<br>There is still no live capture; `Capabilities` and both docs record this honestly. |
| ANALYSIS-102 / ANALYSIS-151 | `grep -rn "PendingForkTitle\|NativeTitleAt" src tests` (`*.cs`, `*.json`, `*.psm1`, excluding bin/obj). Read `FromParent` and its callers, `AlertPolicy.cs:51` and `IdentityHistoryReader.cs:225`. Ran `probe-legacy-fields.ps1` for both providers | [§4](../evidence/analysis-004-probe-results.txt) lines 197–202 | Verified:<br>- `grep` finds no matches (exit 1).<br>- `FromParent` is `void` and has one product caller.<br>- The comment on `NativeNamePolicy.Ours` is present.<br>- `Phrase` uses `identity.Name`, and `Name` is the only `ManualName ?? NativeTitle ?? Subject` chain in `src`.<br>- Legacy `pendingForkTitle`/`nativeTitleAt` values survive a read-modify-write that includes `HistoryState.Reset`. |
| Targeted tests | `dotnet test -c Release --no-build --no-restore --filter "…IdentityRecoveryPolicyTests\|…SpeechTests\|…PresentationAgreementTests\|…WorkPolicyTests\|…IdentityHistoryTests\|…IdentityPrecedenceTests\|…LegacyImportTests"`, with TEMP and results redirected to D: | [§5](../evidence/analysis-004-probe-results.txt) | Passed 118/118. The full build, full test suite and format verification were not run (QA scope on the same target). |

Client coverage: every fork probe ran for both Claude and Codex.
- Claude: SDK `forkedFrom`, original timestamps.
- Codex: `session_meta.forked_from_id`, restamped copies, `thread_settings_applied`.
- Exceptions: `split`, `sweep` and `referenced` exercise mechanisms that only Codex has. Their Claude runs serve as controls.

## Findings

### ANALYSIS-200: The Codex fork-boundary exclusion is unconditional, so a fork with an unknown or unread parent loses all copied work evidence while Claude keeps it

- Severity: warning. It is a regression from round 2 for Codex and an unexplained parity gap on the first-install fork path.
- Related task / requirement: 2.5 (fork/clear/resume outcomes) and 3.3; the design rule "Match first-install/resume/fork/clear outcomes for both clients"; "Provider parity"; ANALYSIS-150
- Locations:
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:234-240`: the exclusion checks only `ForkParent` and `ForkBoundaryOffset`.
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:115` and `:127-128`: the parent's identity replaces the copied evidence only when `Find` returns it, and only once.
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs`, test `ACodexForkWithoutAKnownParentRetriesAndAnUnrelatedSessionMetaIsNotForkEvidence`: it asserts that an orphan fork resolves to `change-a`, but it uses the event-less `codex-rollout-0.164.0-fork.jsonl`. It does not cover the 0.164.0 `Copied` shape, which always carries the event (`session/mod.rs:1777-1780`).
- Observation (measured; [§1 orphan](../evidence/analysis-004-probe-results.txt) lines 150–158 and [§3](../evidence/analysis-004-probe-results.txt) lines 181–196):
  - Parent never seen by Bell Tower (for example, a conversation from before installation; identity files are never pruned): the Claude child shows `ActiveWork=change-a WorkSource=history:edit`, and the Codex child shows `ActiveWork= Subject=repo`.
  - Parent identity known but its transcript not yet walked (its history holds valid work and there was no switch): the Claude child shows `change-a`. The Codex child shows `Subject=repo`, and keeps it after the parent derives `change-a`, because inheritance has already run.
  - Before Implementation 007, the Codex child recovered `change-a` from the copied records, as Claude still does.
  - The exclusion replaces the copied evidence with the parent's resolved identity. That is correct only when the parent's identity is known and actually reflects that history.
- Proposed improvement and benefit:
  - Apply the boundary exclusion only when a branch invalidation is known, that is, when `WorkFloorAt` is set (inherited or the child's own).
  - This keeps ANALYSIS-150 closed. With a parent floor, inheritance runs before `Merge`, so the parent's post-switch work is copied as `fork:` work, and the restamped copies cannot bring back pre-switch work.
  - When no invalidation is known, Codex uses its copied history as Claude does. This restores parity for both the orphan and the unread-parent shapes.
  - Narrower alternative: gate on `ForkInheritedFrom == ForkParent`. This fixes only the orphan shape.
  - Optional hardening, applied equally to both providers: when a late parent is inherited, drop history-derived work that precedes the boundary (Codex) or the inherited floor (Claude).
- Behavior-preservation risks:
  - The ANALYSIS-150 tests (Claude, Codex, mixed) must stay green.
  - `ACodexForkInheritsTheParentsLiveWork…` must still inherit `fork:Write`. Without a floor, a copied record may also apply as `history:edit` for the same change; Claude behaves the same way today.
  - If a parent has a floor but its post-switch work is still unread, the child keeps the fallback name until it does its own work. This errs in the safe direction (no misname).
- Evidence type: measured result (isolated probes on the reviewed build) plus source observation
- Verification / measurement plan:
  - Add orphan and unread-parent tests with `codex-rollout-0.164.0-fork-boundary.jsonl` for Codex, a matched Claude case and a mixed pair. Both providers must resolve the copied work when no invalidation is known.
  - Rerun `probe-fork-boundary.ps1` cases `orphan`, `unread-parent`, `boundary` and `big` ([scripts](../evidence/analysis-004-probe-scripts.ps1.txt)) together with the ANALYSIS-150 tests.
- Relationship: new. Implementation 007's correction for ANALYSIS-150 introduced it. It is a regression of that fix, not a reopening of the invalidated-fork case, which is verified below.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-150 | `diff=da976a89606d untracked=51/1c8549ea446a` | The Analysis 003 probe re-run verbatim, plus the new cases (§1, §2):<br>- Claude: the invalidated fork shows `ActiveWork=` and inherits `WorkFloorAt=12:00:20`.<br>- Codex, rust-v0.164.0 `Copied` shape: `ActiveWork=`. The boundary is found in a single window (976), across a multi-window walk (624526) and in 7 straddled windows.<br>- Post-fork own work is accepted for both providers.<br>- The boundary is re-derived after a recreate and after a truncation and regrow, kept on resume, and correct for the `Referenced` shape.<br>- The floor never lowers.<br>- Codex rollouts without the event fall back to the floor only and still re-derive. This is documented as unverified, and the primary sources show that 0.164.0 `Copied` forks always append the event.<br>- The `split` residual is reachable only through a rollout write failure in the middle of the batch (see the assessment). | Verified for Claude and Codex at the reviewed identity, within the documented limitation. The fix introduces ANALYSIS-200. |
| ANALYSIS-151 | same | `AlertPolicy.cs:51` is `var subject = identity.Name;` and `SessionIdentity.cs:73` holds the only precedence chain. `SpeechTests` and `PresentationAgreementTests` are among the 118/118 that passed | Verified; both providers share `Phrase` |
| ANALYSIS-102 | same | - `grep` for `PendingForkTitle\|NativeTitleAt` in `src` and `tests` finds nothing.<br>- `FromParent` returns `void`.<br>- The comment reserving `Ours` is at `IdentityHistoryReader.cs:225`.<br>- Legacy values written by development builds survive a rewrite, for both Claude and Codex keys (§4). | Verified |

## Assessment and limitations

- Round 3 closes ANALYSIS-150 for the invalidated-fork case on both clients. It uses a shared one-way floor rule plus a thin Codex boundary adapter, whose basis I confirmed in the rust-v0.164.0 sources.
- ANALYSIS-102 and ANALYSIS-151 are verified.
- New finding ANALYSIS-200 (warning): the boundary exclusion should depend on a known invalidation, but as shipped it also fires when the parent is unknown or not yet read. This is a Codex regression and a parity gap on the first-install fork path. A small gate fixes it.
- Known residual, not raised: a Codex fork rollout read after its copied prefix but before its fork-owned snapshot applies the copied work and does not revert it.
  - Normal operation cannot reach this. Binding is hook-driven, hooks run at turn time, and the fork batch is flushed before that turn.
  - It needs a rollout write failure in the middle of the batch, plus a hook arriving before the retry.
  - It clears on a fresh pass, a branch change or new work.
  - The orchestrator may record it or leave it.
- Not performed:
  - full Release build, full test suite and format verification (QA scope)
  - any live `codex fork` or Claude fork (no live capture available, and running them against real sessions is prohibited)
  - a UI run
- Environment:
  - C: had about 113 MB free at the start.
  - All probes and fetched sources ran under the repository's ignored `artifacts/analysis-004-work` and `artifacts/analysis-004-src` on D:, with TEMP/TMP redirected there.
  - Both folders were deleted afterwards. No `analysis-004-*` remains under `artifacts`.
  - There were no disk-space failures.
- Writes:
  - Two evidence files: `evidence/analysis-004-probe-scripts.ps1.txt` and `evidence/analysis-004-probe-results.txt`.
  - The client refused the write of this report once with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." (Claude Code 2.1.310, subagent, auto mode). I did not retry, and the text is returned inline.
  - Nothing was written to sources, tests, docs, planning files, the ledger, the user profile or the default data directory.
- Decisions for the orchestrator:
  - Triage ANALYSIS-200: fix it before release or record a limitation, given the parity rule.
  - Decide whether to record the `split` residual.
