<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code (2.1.310 per Analysis 005), subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

```
status: done
role / client / model: code-analyzer / Claude Code (subagent, auto mode; version not re-queried, 2.1.310 in Analysis 005) / claude-opus-5-5
task / finding IDs: rechecked ANALYSIS-250, QA-300 (as reported); regression ANALYSIS-150, ANALYSIS-102, ANALYSIS-151; new ANALYSIS-300, ANALYSIS-301
changed or report paths: D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\evidence\analysis-006-probe-scripts.ps1.txt; D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\evidence\analysis-006-probe-results.txt; report text is inline below (intended path D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\reports\analysis-006.md)
checks: probe-fork-boundary.ps1 (orphan, unread-parent, split, boundary, big, live-parent, stale-parent, weak-parent, split-live, mixed, plus new gap-parent, split-gap, newer-parent, mixed-gap, gap-twin; claude and codex) -> run, one residual divergence recorded; targeted dotnet test (7 classes) -> passed 140/140; full build, suite and format -> not run (QA scope)
target identity: before and after: head=d307c34 baseline=d307c34 files=96 diff=69c7b4fc67e6 untracked=51/a216cafdd3d1 (matches the assignment, unchanged)
limitations / decisions needed: The client refused the report write once with "Subagents should return findings as text, not write report files. Include this content in your final response instead." I did not retry; the full report text follows. There is no live fork capture. Recoverability of the Codex stamps and the earlier-turn fork route come from source reading, not measurement. Decisions: ANALYSIS-300 (choose option 1, 2 or 3) and ANALYSIS-301 (correct and classify the docs). D:\Temp\bt-a6 was removed.
next: Save the report. Triage ANALYSIS-300 and ANALYSIS-301.
```

---

# Analysis report: restore-claude-badge-identity / Analysis 006 (light recheck of Implementation 009)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code subagent launched by the Claude Code main-session orchestrator, auto permission mode. Version not re-queried this round (Analysis 005 recorded 2.1.310). Model `claude-opus-5-5`.
- Date: 2026-10-24 (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 006 (light recheck of Implementation 009)
- Task and requirement references:
  - tasks 2.5 and 3.3 in [tasks.md](../tasks.md)
  - [design.md](../design.md): decision 3, and "Match first-install/resume/fork/clear outcomes for both clients"
  - [session-identity delta](../specs/session-identity/spec.md): "Provider conversation identity recovery" and "Provider parity"
- Scope and relevant neighboring paths:
  - `src/BellTower/Sessions/IdentityHistoryReader.cs` (`Merge`, lines 234-248)
  - `src/BellTower/Core/{WorkPolicy,IdentityInheritance,SessionIdentity}.cs` (read only)
  - `src/BellTower/Providers/OpenSpecSelectionObserver.cs` (the live evidence path)
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs` (lines 828-1069)
  - `docs/session-identity.md:85` and `docs/compatibility.md:59-61,70-77`
  - the fixture `codex-rollout-0.164.0-fork-boundary.jsonl` and `provenance.json`
  - The handoff [implementation-009](../evidence/implementation-009.md) was read as claims, not evidence.
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, this command ran from the repository root in PowerShell 7: `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src,tests,scripts,README.md,.gitignore,docs/compatibility.md,docs/development.md,docs/installation.md,docs/releases.md,docs/session-titles.md,docs/session-identity.md,docs/hook-ingress.md`. It printed:
  - HEAD `d307c344e22999e48b3c62bc664f0e552fdef3b9`
  - tracked diff SHA-256 `69c7b4fc67e6940045e34b6caaba28a9987eaffe7d949f89194bd550ecfe10cf`
  - 51 untracked files
  - `Summary: head=d307c34 baseline=d307c34 files=96 diff=69c7b4fc67e6 untracked=51/a216cafdd3d1`, which matches the assignment
- Before/after identity: after the analysis, the same command printed the same diff hash and summary line. The target was not changed.
- Prior reports / rechecked findings:
  - [analysis-005.md](analysis-005.md): ANALYSIS-250
  - [analysis-003.md](analysis-003.md): ANALYSIS-150, ANALYSIS-151
  - [analysis-002.md](analysis-002.md): ANALYSIS-102
  - QA-300, as described in the handoff

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build currency | Compared timestamps:<br>- `bell-tower.dll` (20:54:11) against the newest `src/BellTower` source, `IdentityHistoryReader.cs` (20:53:50), and against the test-bin copy<br>- `BellTower.Tests.dll` (20:56:31) against `IdentityRecoveryPolicyTests.cs` (20:56:18) | this report | Both builds are current. The product DLL is byte-identical to the test-bin copy. Probes ran against a copy under `D:\Temp\bt-a6\bin`. |
| ANALYSIS-250 fix | Read `IdentityHistoryReader.cs:234-248` | Source observation | The gate is now `value.ForkParent is not null && value.ForkBoundaryOffset >= 0 && value.WorkFloorAt is not null`. No `fork:` clause remains. Retraction still goes only through `WorkPolicy.Clear(WorkClear.History)`. |
| Analysis 005 probes, both clients | Re-ran `probe-fork-boundary.ps1` unchanged for `orphan`, `unread-parent`, `split`, `boundary`, `big`, `live-parent`, `stale-parent`, `weak-parent`, `split-live` and `mixed` | [results](../evidence/analysis-006-probe-results.txt) lines 4-135 and 178-185 | Claude and Codex now match in every case.<br>- `live-parent`, `stale-parent`, `weak-parent` and `split-live` all resolve `change-a` for both clients.<br>- The timing dependence seen in `split-live` is gone. |
| Residual (QA-300 shape) | Added five cases (see the case list below the table) | [scripts](../evidence/analysis-006-probe-scripts.ps1.txt); results lines 136-199 | - `gap-parent`, `split-gap` and `mixed-gap`: Claude resolves `change-b` (`fork:Write`), Codex resolves `change-a` (`history:edit`). Each client's result is independent of read timing.<br>- `newer-parent` and `gap-twin`: both clients resolve `change-b`.<br>- Both clients converge once the fork does its own work (t=80).<br>See ANALYSIS-300. |
| Recoverability of Codex stamps | Read the fixture `codex-rollout-0.164.0-fork-boundary.jsonl` and `provenance.json`, which cites rust-v0.164.0 `JsonlWriter.write_rollout_item` using `now_utc`. Looked up the Agent SDK sessions docs with `ctx7`. | Source observation | - The child rollout has no per-record original time. The copied `session_meta.timestamp` is the session start, not a record time.<br>- Copied payloads keep their `call_id` (`call_copied_patch`), so the parent rollout still holds the original stamps.<br>- There is no live `codex fork` capture; this comes from sources only. |
| Reachability | Read `OpenSpecSelectionObserver.cs:26-97`, `IdentityHistoryReader.cs:418`, `WorkPolicy.cs:13-63` and `SessionIdentity.cs:71-73` | Source observation plus the `gap-twin` probe | Live and history evidence share `OpenSpecSelectionReader`. A strong live observation therefore normally has a transcript twin, and a full-history fork copies that twin. With the twin present, both clients agree. See ANALYSIS-300. |
| Docs honesty | Read `docs/session-identity.md:85` and `docs/compatibility.md:59,61,70-77` | Source observation | The wording is imprecise, and the residual sits outside the matrix categories. Raised as ANALYSIS-301. |
| Provider theories | Listed the `[Fact]` and `[Theory]` attributes in `IdentityRecoveryPolicyTests.cs` | Lines 828-1069 | - Every ANALYSIS-250 and QA-300 test is a Claude-and-Codex theory (lines 828, 881, 931, 991, 1030, 1056).<br>- Mixed pairs are at lines 902, 956 and 1008.<br>- Lines 1030-1053 pin the residual per client: `expected = provider == "claude" ? "change-b" : "change-a"`.<br>- No mixed-pair test covers the residual shape itself; the `mixed-gap` probe does. |
| ANALYSIS-102 / ANALYSIS-151 | Searched `src` and `tests` (`*.cs`, `*.json`, `*.psm1`, excluding bin/obj) for `PendingForkTitle\|NativeTitleAt`. Read `AlertPolicy.cs:51` and `SessionIdentity.cs:73`. Listed the callers of `FromParent`. | Results §2 | - The search has 0 hits.<br>- `AlertPolicy.cs:51` is `var subject = identity.Name;`.<br>- `SessionIdentity.cs:73` is `Name => ManualName ?? NativeTitle ?? Subject`.<br>- `FromParent` has a single caller, `IdentityHistoryReader.cs:128`. |
| Targeted tests | `dotnet test -c Release --no-build --no-restore --filter` over IdentityRecoveryPolicyTests, SpeechTests, PresentationAgreementTests, WorkPolicyTests, IdentityHistoryTests, IdentityPrecedenceTests and LegacyImportTests. TEMP, results and `BELLTOWER_DATA_DIR` were on D:. | Results §3 | Passed 140/140. The full build, full suite and format check were not run (QA scope). |

The five residual cases added in this round:

- `gap-parent`: the parent holds live strong work on `change-b` at t=20. That is newer than Claude's original copy stamps (t=10) and older than the fork time (t=40).
- `split-gap`: the same shape, with the copied prefix read before the boundary is written.
- `newer-parent`: the parent's live work is at t=90, newer than every copied record.
- `mixed-gap`: Claude and Codex `gap-parent` forks in one store.
- `gap-twin`: the parent's t=20 call is also in the copied prefix.

Client coverage: every probe ran for both Claude and Codex, and `mixed` and `mixed-gap` ran both clients in one store. The fork boundary exists only in Codex, so the Claude runs of the `split*` cases serve as controls.

## Findings

### ANALYSIS-300: The fork-time residual is a recoverable data difference, not a proven hard restriction. It is reachable only when the parent's live work has no copied twin.

- Severity: suggestion (decision needed). Reachability is narrow, but the parity gap is visible when it is reached.
- Related task / requirement: 2.5, 3.3; "Provider parity"; the AGENTS.md exception rule; QA-300
- Locations:
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:234-238`: no exclusion without a floor
  - `src/BellTower/Core/WorkPolicy.cs:40-47`: history is ordered against inherited work by `At`
  - `src/BellTower/Core/IdentityInheritance.cs:20-28`: the inherited `SelectionAt`
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs:1026-1053`
- Observation (measured; results lines 136-199):

  | Case | Claude fork | Codex fork |
  | --- | --- | --- |
  | `gap-parent` (parent live `change-b` at t=20, no twin in the copies) | `change-b`, `fork:Write` | `change-a`, `history:edit` |
  | `split-gap` (same, prefix read before the boundary) | `change-b` | `change-a` |
  | `mixed-gap` (both in one store) | `change-b` | `change-a` |
  | `gap-twin` (the parent's t=20 call is also copied) | `change-b` | `change-b` |
  | `newer-parent` (t=90) | `change-b` | `change-b` |

- Is it a hard data difference? Only within the child rollout.
  - Codex copies carry the fork time (source: rust-v0.164.0 `now_utc` and a synthetic fixture; no live capture). The child file alone therefore cannot give the original record times.
  - It is still not a hard restriction of the capability, for two reasons:
    - Copied payloads keep their `call_id`. The residual needs a known parent identity with live work, and in that case Bell Tower usually has the parent's bound `TranscriptPath`, which holds the original stamps.
    - A provider-neutral rule would also make the clients agree: order inherited parent work against the fork point instead of against each copy's stamp.
  - So the handoff's statement that equal results "would need provider-specific precedence" is not established.
- Is it reachable? Rarely.
  - `OpenSpecSelectionObserver` and the history reader share `OpenSpecSelectionReader`, so a strong live observation normally has a transcript twin. A full-history fork copies that twin, and `gap-twin` shows that both clients then agree.
  - Weak live evidence never blocks strong copies (`weak-parent`).
  - The residual needs the twin to be missing from the copied prefix. Possible routes:
    - A fork taken from an earlier turn: a Codex backtrack-style fork, or an SDK fork resumed at an earlier message. These are hypotheses, not verified for either client here.
    - A twin that the history reader does not classify.
- Is it visible? Yes, when reached.
  - `WorkSubject` feeds `Subject` and `Name` (`SessionIdentity.cs:71,73`). The badge, tab and tray subject therefore differ (`Subject=change-b` versus `change-a`).
  - The difference lasts until the fork does its own work, or permanently if it never does.
  - A manual name, native title or pin hides it.
- Which client is right depends on the route. In the earlier-turn fork route, the copied history is the true state at the fork point. There, the Codex result is the accurate one, and Claude shows parent work the fork never did. Recovering the Codex stamps from the parent would make Codex match Claude's less accurate result.
- Proposed improvement and benefit (the orchestrator chooses):
  1. Record the residual as unverified or proven-restricted, following AGENTS.md: the exact operation, rust-v0.164.0, the source citation, the fallback, and the line-1030 test as regression coverage. Add the closing evidence to `docs/compatibility.md` (see ANALYSIS-301). This is the cheapest option.
  2. Shared fork-point rule: for both clients, compare inherited parent work with the fork point. For Codex that is the copy stamp; for Claude it is the fork's first own record or the file start. This makes the clients agree without provider-specific precedence and fixes the Claude earlier-turn route. It changes Claude's `gap-parent` result to `change-a`.
  3. The Codex adapter recovers original stamps from the parent rollout by `call_id`. This makes Codex match Claude's behavior. It adds a cross-file read and needs the parent rollout to be reachable.
- Behavior-preservation risks:
  - Option 1 does not affect the ANALYSIS-150 floor cases.
  - Option 2 changes the shared rule. Floor filtering must keep using Claude's original stamps.
  - Option 3 adds I/O on the read path and a dependency on the parent's path.
- Evidence type: measured result (isolated probes on the reviewed build) plus source observation. Recoverability and the earlier-turn route are hypotheses drawn from sources.
- Verification / measurement plan:
  - Option 1: docs review only.
  - Options 2 and 3:
    - Change `ALateBoundaryDoesNotChangeTheOutcome...` to assert a single expected value.
    - Add a mixed pair for the gap shape.
    - Re-run `gap-parent`, `split-gap`, `gap-twin`, `newer-parent`, `stale-parent`, `boundary`, `big` and `mixed` from [the scripts](../evidence/analysis-006-probe-scripts.ps1.txt).
- Relationship: residual of ANALYSIS-250 / QA-300.

### ANALYSIS-301: The docs misstate which parent work Claude keeps, and they record the residual outside the matrix categories

- Severity: warning. AGENTS.md requires the capability matrix to be honest about supported, proven-restricted and unverified behavior.
- Related task / requirement: 3.3; "Provider parity"; AGENTS.md
- Locations:
  - `docs/session-identity.md:85`
  - `docs/compatibility.md:59,61,70-77`
  - the test comment at `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs:1029`
- Observation (source observation, checked against measured behavior):
  - `session-identity.md:85` says: "a Codex fork prefers its copied history over older inherited parent work, where a Claude fork's original stamps keep that older parent work."
    - Claude does not keep parent work that is older than the copied records' original stamps. `stale-parent` gives `change-a` on both clients (results lines 94-107), and the test at line 991 asserts that.
    - Claude keeps parent work only when it is newer than the copied records' original stamps and older than the fork time.
  - `compatibility.md:61` calls the residual "a documented difference". However:
    - Line 59 says no surface is a proven restriction.
    - The "Evidence that closes the unverified rows" list (lines 70-77) has no entry for it.
    - So the residual is classified as neither proven-restricted nor unverified, and the AGENTS.md exception elements (fallback, regression coverage, recoverability) are not stated.
  - The test comment at line 1029 calls it "a documented restamp limitation", which is the same unclassified wording.
- Proposed improvement and benefit:
  - State the exact window: parent work newer than the copied records' original stamps, older than the fork time, and with no copied twin.
  - State that the residual requires the copied twin to be missing.
  - Classify it: either unverified, with closing evidence, or proven-restricted, with the AGENTS.md elements. Add it to the evidence list.
  - Users and reviewers then get an accurate parity statement.
- Behavior-preservation risks: docs only.
- Evidence type: source observation, checked against measured probes.
- Verification / measurement plan: review the docs against the `stale-parent` and `gap-parent` results and against the tests at lines 991 and 1030.
- Relationship: documentation side of ANALYSIS-300.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-250 | `diff=69c7b4fc67e6 untracked=51/a216cafdd3d1` | - The gate is `WorkFloorAt is not null` only (`IdentityHistoryReader.cs:238`).<br>- `live-parent`, `stale-parent`, `weak-parent` and `split-live` resolve `change-a` for Claude and Codex, independent of timing (results lines 80-135).<br>- The theories at lines 828, 991, 1030 and 1056 and the mixed pairs at lines 902, 956 and 1008 pass. | Verified. The residual is raised as ANALYSIS-300 and ANALYSIS-301. |
| QA-300 (as reported in the handoff) | same | `split-gap` and `gap-parent` give each client the same value before and after the boundary arrives: Claude `change-b`, Codex `change-a`. | The timing dependence is verified gone. The value still differs by client (ANALYSIS-300). |
| ANALYSIS-150 | same | - `boundary`, `big`, `split` and the mixed split show no pre-switch work for either client.<br>- The Codex boundary is found at 894, and at 624444 across a multi-window walk.<br>- Own work is accepted afterwards (results lines 26-79 and 178-185). | No regression |
| ANALYSIS-102 | same | The search for `PendingForkTitle\|NativeTitleAt` has 0 hits. | No regression |
| ANALYSIS-151 | same | - `AlertPolicy.cs:51` uses `identity.Name`.<br>- `SessionIdentity.cs:73` holds the only precedence chain.<br>- `FromParent` has one caller.<br>- SpeechTests and PresentationAgreementTests are among the 140/140 that passed. | No regression |

## Assessment and limitations

- Implementation 009 applies the ANALYSIS-250 fix as proposed:
  - Claude and Codex now share one precedence rule.
  - They agree in every Analysis 005 shape, including the mixed store.
  - The tests are provider theories with mixed pairs.
- One Claude/Codex difference remains: the fork-time window.
  - It follows from Codex restamping copies with the fork time.
  - It is not proven hard: the original stamps can be recovered from the parent rollout, or a shared fork-point rule can be used.
  - It is reachable only when the parent's live work has no copied twin.
  - When it is reached, the subject shown to the user differs between the clients.
- Decisions for the orchestrator:
  - ANALYSIS-300: choose option 1, 2 or 3.
  - ANALYSIS-301: correct and classify the docs.
- Not performed:
  - the full build, full suite and format verification (QA scope)
  - a live `codex fork`, Claude fork or earlier-turn fork capture (none is available, and running them against real sessions is prohibited)
  - a UI run
  - a measurement of options 2 and 3
- Environment:
  - Probes and tests used isolated data under `D:\Temp\bt-a6`. TEMP/TMP, the npm cache and `BELLTOWER_DATA_DIR` were redirected there.
  - The folder was removed at the end.
  - The default data directory, `~/.claude`, `~/.codex` and the tray app were not touched, and no audio was played.
- Writes:
  - Two evidence files: `evidence/analysis-006-probe-scripts.ps1.txt` and `evidence/analysis-006-probe-results.txt`.
  - The client refused the write of this report once with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." I did not retry, and the text is returned inline.
  - Nothing was written to product files, tests, docs, planning files, the ledger or the user profile.
