<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: rechecked ANALYSIS-200, QA-250; regression ANALYSIS-150, ANALYSIS-102, ANALYSIS-151; new ANALYSIS-250
changed or report paths: D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\evidence\analysis-005-probe-scripts.ps1.txt; D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\evidence\analysis-005-probe-results.txt; report text inline below (intended path D:\Code\ZeusApps.BellTower\openspec\changes\restore-claude-badge-identity\reports\analysis-005.md)
checks: probe-fork-boundary.ps1 (orphan, unread-parent, split, split-orphan, split-live, boundary, big, live-parent, stale-parent, weak-parent, late-parent, mixed; claude+codex) -> run, divergences recorded; probe-legacy-fields.ps1 (claude, codex) -> passed; targeted dotnet test (7 classes) -> passed 128/128; full build/suite/format -> not run (QA scope)
target identity: before and after: head=d307c34 baseline=d307c34 files=96 diff=a3ad7d8486b6 untracked=51/39bb3b437966 (match the assignment, unchanged)
limitations / decisions needed: The client refused the report write once (message above); I did not retry and the full report text follows. No live fork capture. The ANALYSIS-250 fix is reasoned from the code, not measured. Decisions: triage ANALYSIS-250; decide whether to record the late-parent asymmetry. D:\Temp\bt-a5 was removed.
next: Save the report. Triage ANALYSIS-250: drop the `fork:` clause and turn the live-parent test into a Claude-and-Codex theory, or record a limitation and correct docs/session-identity.md:85.
```

---

# Analysis report: restore-claude-badge-identity / Analysis 005 (light recheck of Implementation 008)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent launched by the Claude Code main-session orchestrator, auto permission mode; model `claude-opus-5-5`
- Date: 2026-10-24 (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 005 (light recheck of Implementation 008, user-authorized fix round 4)
- Task and requirement references:
  - tasks 2.5 and 3.3 in [tasks.md](../tasks.md)
  - [design.md](../design.md): decision 3 (fork/clear/resume) and "Match first-install/resume/fork/clear outcomes for both clients"
  - [session-identity delta](../specs/session-identity/spec.md): "Provider conversation identity recovery", "Provider parity"
- Scope and relevant neighboring paths:
  - `src/BellTower/Sessions/IdentityHistoryReader.cs` (`Merge`, lines 234-249; `Refresh`, lines 113-129)
  - `src/BellTower/Core/{WorkPolicy,IdentityInheritance,HistoryState}.cs` (read only, unchanged in this round)
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs` (lines 829-976)
  - `docs/session-identity.md:85` and `docs/compatibility.md:61` (fork wording)
  - The handoff [implementation-008](../evidence/implementation-008.md) was read as claims, not evidence.
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src tests scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md docs/hook-ingress.md` (PowerShell 7, repository root) printed:
  - HEAD `d307c344e22999e48b3c62bc664f0e552fdef3b9`, tracked diff SHA-256 `a3ad7d8486b6f14714997aba18dcf06bc3416fa369d3d1ea53d1fa8cb5689ad5`
  - `Summary: head=d307c34 baseline=d307c34 files=96 diff=a3ad7d8486b6 untracked=51/39bb3b437966` (matches the assignment)
- Before/after identity: after the analysis, the same command printed the same HEAD, diff hash and summary line. The review target was not changed.
- Prior reports / rechecked findings: [analysis-004.md](analysis-004.md) (ANALYSIS-200), [qa-004.md](qa-004.md) (QA-250), [analysis-003.md](analysis-003.md) (ANALYSIS-150, ANALYSIS-151), [analysis-002.md](analysis-002.md) (ANALYSIS-102)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build currency | Compared `bell-tower.dll` with the newest `src/BellTower` source and with the test-bin copy | this report | The DLL (20:39:26) is newer than `IdentityHistoryReader.cs` (20:39:24) and byte-identical to the test-bin copy. Probes used it read-only. |
| ANALYSIS-200 re-run | Analysis 004 `probe-fork-boundary.ps1` cases `orphan` and `unread-parent` re-run verbatim, both providers | [results §1](../evidence/analysis-005-probe-results.txt) lines 6-27 | Fixed:<br>- Orphan: Claude and Codex both show `ActiveWork=change-a WorkSource=history:edit`.<br>- Unread parent: both show `change-a` at first read and after the parent's read. |
| Mixed pair | New case `mixed`: Claude and Codex forks in one store, interleaved, shapes orphan, unread parent and QA-250 split | §1 lines 156-162 | Matched: orphan and unread give `change-a` for both providers. Split ends with no work for both, and both inherit `WorkFloorAt=12:00:20`. |
| ANALYSIS-150 regression | Cases `boundary` and `big`, both providers | §1 lines 60-97 | Still fixed. The invalidated fork has no work for both providers. The Codex boundary is found at 894 in a single window and at 624444 across a multi-window walk. Post-fork own work (`change-b`) is accepted. |
| QA-250 | Case `split`: the copied prefix is read before the boundary is written, and the parent invalidation is known. Case `split-orphan`: the same timing, with no invalidation known | §1 lines 28-59 | Codex: `change-a` shows for one tick, then is retracted to `ActiveWork=` once the boundary arrives. Claude never shows it.<br>`split-orphan`: both providers keep `change-a` and then accept their own work. This is consistent with the new no-invalidation rule. |
| The added `fork:*` clause | New cases `live-parent`, `stale-parent`, `weak-parent` and `split-live`. In each, the parent identity holds live work and its transcript is unread; no floor. Read `WorkPolicy.Apply` (`WorkPolicy.cs:40-47`) and `IdentityInheritance.FromParent` (`IdentityInheritance.cs:20-29`) | §1 lines 98-139, 163-176 | The providers diverge, and the Codex result depends on read timing. Raised as ANALYSIS-250. |
| Late parent (asymmetry of the retraction) | New case `late-parent`: the child is read while the parent is unknown, then a parent with a pre-fork floor is registered | §1 lines 140-155 | The providers diverge:<br>- Claude keeps the pre-switch `change-a` after inheriting the floor.<br>- Codex retracts it.<br>Not raised: a parent first seen after the child cannot normally hold a pre-fork floor (see the assessment). This is not a regression, because Claude's path is unchanged and Codex previously showed no work at all. |
| Shared policy / thin adapters | Source reading of `Merge` lines 234-249 | Source observation | The retraction reuses `WorkPolicy.Clear(WorkClear.History)`, the single clearing point. It touches neither live work nor the floor, and it is confined to proven Codex forks (a boundary needs `thread_settings_applied`). No new duplication.<br>The gate now carries a provider-specific precedence rule, discussed under ANALYSIS-250. |
| ANALYSIS-102 / ANALYSIS-151 | `grep -rn "PendingForkTitle\|NativeTitleAt"` over `src` and `tests` (`*.cs`, `*.json`, `*.psm1`, excluding bin/obj). Read `AlertPolicy.cs:51`, `SessionIdentity.cs:73` and the `FromParent` signature and callers. Re-ran `probe-legacy-fields.ps1` for both providers | [§2](../evidence/analysis-005-probe-results.txt) lines 178-184 | No regression:<br>- The grep has no hits (exit 1).<br>- `Phrase` uses `identity.Name`, which is the only precedence chain.<br>- `FromParent` is `void`, with one product caller (`IdentityHistoryReader.cs:128`).<br>- Legacy values survive a read-modify-write for both providers, the same as in Analysis 004. |
| Targeted tests | `dotnet test -c Release --no-build --no-restore --filter "…IdentityRecoveryPolicyTests\|…SpeechTests\|…PresentationAgreementTests\|…WorkPolicyTests\|…IdentityHistoryTests\|…IdentityPrecedenceTests\|…LegacyImportTests"`, with TEMP and results on D: | [§3](../evidence/analysis-005-probe-results.txt) | Passed 128/128 (118 in Analysis 004 plus the 10 new cases). The full build, full suite and format verification were not run (QA scope). |

Client coverage: every fork case ran for both Claude and Codex, and `mixed` ran both in one store. The boundary itself exists only in Codex; in `split`, `split-orphan` and `split-live` the Claude runs serve as controls.

## Findings

### ANALYSIS-250: The added `fork:*` clause makes Codex prefer the parent's possibly stale identity over its copied history, while Claude takes the newer evidence

- Severity: warning. It leaves a visible parity gap in the unread-parent shape that ANALYSIS-200 targets, and the docs claim parity for that shape.
- Related task / requirement: 2.5, 3.3; "Provider parity"; the design rule "Match first-install/resume/fork/clear outcomes for both clients"; ANALYSIS-200
- Locations (reviewed identity):
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:238-239`: `(value.WorkFloorAt is not null || value.WorkSource?.StartsWith("fork:", StringComparison.Ordinal) == true)`
  - `src/BellTower/Core/IdentityInheritance.cs:20-29` (the source of `fork:` work) and `src/BellTower/Core/WorkPolicy.cs:40-47` (the ordering of history against inherited work)
  - `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs:829-855`: `ACodexForkInheritsTheParentsLiveWork…` is a Codex-only `[Fact]` that asserts `WorkSource == "fork:Write"`
  - `docs/session-identity.md:85`: "with no known invalidation (parent unknown or not yet read) a Codex fork uses its copied history exactly as a Claude fork does"
- Observation (measured; [§1](../evidence/analysis-005-probe-results.txt) lines 98-139 and 163-176). In each case the parent identity holds live work, its transcript (which has `change-a` work at t=10) is unread, and there is no floor:

  | Case | Parent live work | Claude child | Codex child | Parent after its own read |
  | --- | --- | --- | --- | --- |
  | `live-parent` | `change-a` Write at t=5 | `change-a`, `history:edit` | `change-a`, `fork:Write` | not read |
  | `stale-parent` | `change-b` Write at t=5 | `change-a` | `change-b` (`fork:Write`), kept permanently | `change-a` |
  | `weak-parent` | weak `change-b` skill at t=30 | `change-a` | weak `change-b` (`fork:skill`), kept permanently | `change-a` |
  | `split-live` (as `live-parent`, prefix read before the boundary) | `change-a` Write at t=5 | `history:edit` | `history:edit` | not read |

  - The clause therefore does more than keep a source label: it changes the precedence rule for Codex only.
    - Claude orders copied records against inherited work by timestamp, so a newer copy wins. This is the shared `WorkPolicy` rule.
    - Codex lets inherited work block all copies.
  - The developer's rationale was that restamped copies would replace `fork:Write` with `history:edit`. Claude does exactly that in the same shape (`live-parent`). The rationale protects a Codex-only assertion on an internal source string, not a user-visible outcome.
  - The clause does not even hold its own goal reliably. With split timing (`split-live`), the copies apply before the boundary exists, and the gate is false from then on because `WorkSource` is now `history:edit`. So the Codex outcome depends on read timing. In `stale-parent`, a whole-file read gives `change-b`, while a split read would give `change-a`.
  - Reachability: the same class as the ANALYSIS-200 unread-parent shape. It needs a parent identity whose live work lags its own transcript when the child is first read, for example a parent seen before a Bell Tower restart or a missed hook, and a parent that is closed or not yet refreshed. It also covers a parent with only weak live evidence (an explore skill) whose strong history is still unwalked.
  - Before Implementation 008, Codex excluded the copies unconditionally, so this sub-shape already diverged. It is an incomplete correction of ANALYSIS-200, not a regression of ANALYSIS-150.
- Proposed improvement and benefit:
  - Drop the `fork:` clause and gate the exclusion and retraction on `WorkFloorAt is not null` only. That is the rule ANALYSIS-200 proposed, and it is what the docs already describe.
  - Reasoning from `WorkPolicy.Apply`:
    - Restamped Codex copies (fork time) are newer than any pre-fork `SelectionAt`. So in `live-parent`, `stale-parent` and `weak-parent` the Codex child resolves `change-a`, the same as Claude.
    - Inherited work stamped after the fork still beats the copies for both providers.
  - Change `ACodexForkInheritsTheParentsLiveWork…` to assert `ActiveWork == "change-a"` rather than the source string, and make it a Claude-and-Codex theory.
  - Benefit: one precedence rule for both providers, a simpler gate (no string-prefix test on provenance), timing-independent results, and docs that are accurate as written.
- Behavior-preservation risks:
  - The ANALYSIS-150 tests and the QA-250 growth tests use an inherited floor, so they are unaffected.
  - A Codex child with inherited live work and no floor will show `history:edit` instead of `fork:Write`. That is a label difference only, and it matches Claude.
  - Residual divergence after the change: parent live work that is newer than every copied record, on a different change, with no transcript record of its own. Claude would keep the inherited work and Codex would take the newest copy. I know of no live-only evidence source that produces this; verify while making the change.
- Evidence type: measured result (isolated probes on the reviewed build) plus source observation. The effect of the proposed change is reasoned, not measured.
- Verification / measurement plan:
  - Add matched Claude and Codex tests plus a mixed pair for `stale-parent` and `weak-parent`. Both providers must resolve the parent's newer transcript work (`change-a`).
  - Keep the live-parent test as a theory over both providers.
  - Re-run cases `live-parent`, `stale-parent`, `weak-parent`, `split-live`, `boundary`, `big`, `split` and `mixed` from [the scripts](../evidence/analysis-005-probe-scripts.ps1.txt).
- Relationship: residual of ANALYSIS-200, specifically the sub-shape where the unread parent already holds live work.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-200 | `diff=a3ad7d8486b6 untracked=51/39bb3b437966` | `orphan`, `unread-parent` and the `mixed` orphan/unread pair: Claude and Codex both resolve `change-a`, and Codex keeps it after the parent's read (§1 lines 6-27, 156-162). The 10 new tests pass | Verified for the probed orphan and unread-parent shapes (parent identity without live work), for both clients and the mixed pair. The sub-shape where the unread parent holds live work still diverges: ANALYSIS-250 |
| QA-250 | same | Codex `split`: transient `change-a` for one tick, then `ActiveWork=` with `ForkBoundaryOffset=894` and the floor intact. Claude never shows it. Mixed split: no work for both. Own work is accepted afterwards (§1 lines 28-43, 156-162) | Verified for the invalidated-parent split. The retraction is correctly limited to `history:*` work below the boundary. One transient tick remains, which is inherent in the retract option QA-250 offered |
| ANALYSIS-150 | same | `boundary` and `big` for both providers: no pre-switch work, the boundary found in one window and across a multi-window walk, own work accepted (§1 lines 60-97) | No regression |
| ANALYSIS-102 | same | The grep has no hits. The legacy round-trip passes for both providers (§2) | No regression |
| ANALYSIS-151 | same | `AlertPolicy.cs:51` is `var subject = identity.Name;`. `SessionIdentity.cs:73` holds the only chain. `SpeechTests` and `PresentationAgreementTests` are among the 128/128 that passed | No regression |

## Assessment and limitations

- Implementation 008 closes the ANALYSIS-200 orphan and unread-parent shapes and QA-250 for both clients. It does so with a small change that reuses the single clearing point (`WorkPolicy.Clear`) and adds no duplication.
- The developer's extra `fork:*` condition is not principled; it is a test-fitting special case:
  - It keeps a Codex-only source-string assertion green.
  - In exchange, it introduces a Codex-only precedence rule (inherited identity over copied history) that Claude does not apply.
  - It produces timing-dependent results, and it contradicts `docs/session-identity.md:85`.
  - Raised as ANALYSIS-250 (warning), with a one-clause fix.
  - Without that clause, the two clients would share one policy: the shared floor rule plus a thin Codex boundary adapter.
- The developer's handoff says that for Claude "nothing is applied early". That holds only when the floor is known at the child's first read. In `late-parent` (§1 lines 140-155), a Claude orphan fork keeps pre-switch copied work after a later-inherited floor, while Codex retracts it.
  - Not raised: a parent identity first registered after the child's read cannot normally hold a floor from before the fork, because floors come only from switches Bell Tower observes. Inheritance also runs once, when the parent first appears.
  - Claude's behavior here is unchanged from earlier rounds.
  - The orchestrator may record it with the Analysis 004 `split` residual.
- Not performed:
  - full build, full suite and format verification (QA scope on the same target)
  - a live `codex fork` or Claude fork (no capture available, and running them against real sessions is prohibited)
  - a UI run
  - a measurement of the proposed ANALYSIS-250 change (reasoned only)
- Environment:
  - Probes ran against isolated data under `D:\Temp\bt-a5`, with TEMP/TMP redirected there. The folder was removed at the end.
  - The default data directory, `~/.claude`, `~/.codex` and the tray app were not touched, and no audio was played.
- Writes:
  - Two evidence files: `evidence/analysis-005-probe-scripts.ps1.txt` and `evidence/analysis-005-probe-results.txt`.
  - The client refused the write of this report once with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." (Claude Code 2.1.310, subagent, auto mode). I did not retry, and the text is returned inline.
  - Nothing was written to product files, tests, docs, planning files, the ledger or the user profile.
- Decisions for the orchestrator:
  - Triage ANALYSIS-250: drop the clause and parameterize the test, or record the sub-shape as a parity limitation with the docs corrected.
  - Decide whether to record the `late-parent` asymmetry.
