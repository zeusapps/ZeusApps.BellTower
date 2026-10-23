# Findings: add-claude-repository-subagents

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

- Client / version / model: Claude Code 2.1.309 / claude-opus-5-5
- Session: Claude Code main session in `D:\Code\ZeusApps.BellTower` (apply started 2026-10-23)
- Since (UTC): 2026-10-23 (session restarted 2026-10-23 for ANALYSIS-016; same client and user)
- Previous owner: the same Claude orchestrator before the restart

## Assignments and reservations

| Assignment | Role / owner | Client / model | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- |
| DEV-001 | developer (bootstrap: Claude general-purpose subagent following `.agents/roles/developer.md`; native role unavailable until 1.1 lands and the session restarts, design D5) | Claude Code 2.1.309 / sonnet (role default, D2) | 1.1, 1.2, 2.1–2.6; baseline `b3e007e4c38a7893886e908998abbd5dfd5844a3` | handoff returned to orchestrator | none | returned 2026-10-23: 17 scoped files, `review-identity.ps1` summary `files=17 diff=cf1dd0d93984 untracked=5/3b2823125e0b`; build passed; tests 351/372 with 21 failures attributed (unmeasured) to unrelated in-flight `src/` changes; format verify fails only on unrelated files; Codex `model` key documented and applied (`gpt-6-luna` dev/qa, `gpt-6.1-sol` + high analyzer), ids not natively probed |
| QA-R1 | qa (native Claude role) | Claude Code 2.1.309 / sonnet (role default) | 3.1 review of groups 1–2 + 3.3 nesting probe; target `cf1dd0d93984` / untracked `3b2823125e0b` | `reports/qa-001.md` | QA-001 – QA-030 | running |
| AN-R1 | code-analyzer (native Claude role) | Claude Code 2.1.309 / opus (role default; resolved claude-opus-5-5) | 3.1 analysis of groups 1–2; same target | `reports/analysis-001.md` (NOT persisted: harness refused the write, see ORCH-001; full text returned in handback) | ANALYSIS-001 – ANALYSIS-030 (001–015 used) | returned |
| QA-R1 (update) | — | resolved claude-sonnet-5-5 | — | `reports/qa-001.md` persisted | QA-001 – QA-003 used | returned |
| DEV-002 | developer (native Claude role; task 3.2 evidence) | Claude Code 2.1.309 / sonnet (role default; resolved claude-sonnet-5-5) | fixes QA-001, ANALYSIS-002–006 in `scripts/review-identity.ps1` + its test + 5 usage lines | handoff | none | returned 2026-10-23: ReviewIdentity tests 6/6, build passed, full suite 21 known unrelated failures; new identity `files=17 diff=a0ed92f93e70 untracked=5/9f4f6eed3593` (pinned diff args; supersedes `cf1dd0d93984`) |
| DEV-003 | developer (native Claude role) | Claude Code 2.1.309 / sonnet (role default) | docs/adapter batch: ANALYSIS-007–010, -011 (user: shared facts file), -012, -013, -014 (user: effort high both), QA-002 labeling, QA-003 | handoff | none | returned 2026-10-23: new `.agents/roles/project-facts.md`; pure-pointer adapters; target `files=18 diff=5d47d708f074 untracked=6/c8adea8d98db`; per-launch bytes dev 5578 / qa 9051 / analyzer 8560 (up 0.6–0.9 KB vs DEV-001, about 50% below the original baseline) |
| QA-R2 | qa (native) | Claude Code 2.1.309 / sonnet (resolved claude-sonnet-5-5) | recheck QA-001–003 + verify DEV-002/003 + task 3.5 | `reports/qa-002.md` (self-written) | QA-004 – QA-030 (004 used) | returned: QA-001 and QA-003 verified, QA-002 still failing, QA-004 new; task 3.5 role files were sufficient; target unchanged `5d47d708f074` |
| AN-R1 persist | orchestrator | — | analyzer resume write refused again (2/2); `reports/analysis-001.md` saved verbatim by orchestrator on user decision | `reports/analysis-001.md` | — | done |
| DEV-004 | developer (native) | Claude Code 2.1.309 / sonnet (resolved claude-sonnet-5-5) | QA-002, QA-004, ORCH-001 fallback text in roles and workflow | handoff | none | returned 2026-10-23: target `files=18 diff=e452a79d95f4 untracked=6/7f38333b238b` |
| QA-R3 | qa (native) | Claude Code 2.1.309 / sonnet | recheck QA-002, QA-004; verify the fallback text; final QA for 5.1 on current target | `reports/qa-003.md` (self-written) | QA-005 – QA-030 (none used) | returned: QA-002, QA-004 and the fallback text verified; no regressions; no new findings; target unchanged `e452a79d95f4` |
| AN-R2 | code-analyzer (native) | Claude Code 2.1.309 / opus, effort high (role default) | recheck ANALYSIS-002–014 + DEV-004; final analysis for 5.1 | `reports/analysis-002.md` (write refused 3rd time; saved verbatim by orchestrator) | ANALYSIS-016 – ANALYSIS-030 (016–022 used) | returned: 002–011, 013, 014 verified; 012 failed (minor); 016–022 new; target unchanged `e452a79d95f4` |
| DEV-005 | developer (native) | Claude Code 2.1.309 / sonnet | ANALYSIS-012, 016 (docs), 017–022 | handoff | none | returned 2026-10-23: ReviewIdentity tests 8/8, build passed; new target `files=18 diff=de79360dc383 untracked=6/50a0657c34e7` (output format changed; earlier identities not comparable) |
| QA-R5 | qa (native) | Claude Code / haiku override (task 3.4 probe; a cheaper model is enough for a factual check) | 3.3 tool observation, 3.4 override, 3.5 + 4.4 content check after restart | `reports/qa-005.md` (saved verbatim by orchestrator) | none | returned: 3.4 override took effect (self-reported claude-haiku-5-5); 3.5/4.4 starting-context AGENTS.md matches the file on disk; role files were enough; 3.3 no Agent/Task tool; no CLAUDE.md |

Orchestrator observation (2026-10-23): after DEV-001 wrote `.claude/agents/*.md`, Claude Code 2.1.309 listed `developer`, `qa`, `code-analyzer` as available agent types in the running session without a restart (task 1.1 discovery evidence; declared models confirmed when the roles run). Codex model ids accepted per D2 open question (resolved during implementation); user may override.

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 2.3 / D10 | warning | yes: documented identity invocation silently returns an empty identity | verified | — | [qa-002](qa-002.md) |
| QA-002 | [qa-001](qa-001.md), [qa-002](qa-002.md) | 1.2 / Per-role model selection | warning | yes: recheck failed, the label is general rather than next to the Codex values; clear fix is TOML comments next to the values; native evidence still task 4.1 | verified (labeling; native Codex id acceptance remains task 4.1) | — | [qa-003](qa-003.md) |
| QA-003 | [qa-001](qa-001.md) | 2.1 / Native adapters (association rule) | suggestion | no; accepted as clear fix | verified | — | [qa-002](qa-002.md) |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 2.3 / D10 | warning | duplicate of QA-001 | duplicate | — | via QA-001 |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 2.3 / D10 | warning | yes: wrong or unmatched paths silently report clean | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 2.3 / D10 | warning | yes: output can't be captured or compared, stderr noise | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 2.3 | suggestion | no; accepted (`#Requires -Version 7.0`) | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 2.3 / D10, 4.1 | suggestion | no; accepted (pin diff options; cross-client hand-off relies on it) | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 2.3 | suggestion | no; accepted (test gaps that hid QA-001) | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | 1.1, 1.2 / D1, spec "adapters shall not duplicate" | warning | yes: contradicts spec; clear fix = pure pointer, identical across clients | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | 2.2, 2.6 / D2 | warning | yes: one authoritative location | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-009 | [analysis-001](analysis-001.md) | 2.4 / D7 | suggestion | no; accepted (trim AGENTS.md workflow section; parity section untouched) | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-010 | [analysis-001](analysis-001.md) | 2.1 / spec live-instance scenario | warning | yes: safety rules already drifted between roles | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-011 | [analysis-001](analysis-001.md) | 2.1 / spec "Shared entry point" | suggestion | user decided 2026-10-23: shared `.agents/roles/project-facts.md`; spec and design updated by orchestrator | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-012 | [analysis-001](analysis-001.md) | 2.2, 2.5 | suggestion | no; accepted | verified | — | [analysis-003](analysis-003.md) |
| ANALYSIS-013 | [analysis-001](analysis-001.md) | 2.1 | suggestion | no; accepted, with task 3.5 checking no harm | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-014 | [analysis-001](analysis-001.md) | 1.1, 1.2 / D2 parity | suggestion | user decided 2026-10-23: high effort on both clients; design D2 updated | verified | — | [analysis-002](analysis-002.md) |
| ANALYSIS-015 | [analysis-001](analysis-001.md) | design D3/Risks | suggestion | no; planning artifact, orchestrator-owned | verified (design.md corrected by orchestrator 2026-10-23; planning edit, no implementation review needed) | orchestrator | design.md |
| ORCH-001 | orchestrator; [analysis-001](analysis-001.md) header | spec "Durable change-local reports" / Claude parity | warning | proven Claude restriction (2.1.309 auto mode, native analyzer `Write` refused 2/2, permission allow rule had no effect and was removed); user approved verbatim orchestrator save as fallback 2026-10-23; spec, design, and ledger updated; role and workflow text pending DEV-004 | verified (fallback exercised in AN-R2, 3/3 analyzer refusals; durable evidence home pending under ANALYSIS-021) | — | [qa-003](qa-003.md), [analysis-002](analysis-002.md) |

| QA-004 | [qa-002](qa-002.md) | 2.1 / project-facts reviewer format check | suggestion | no; accepted: add a scoped `dotnet format --verify-no-changes --include <files>` option for dirty trees | verified | — | [qa-003](qa-003.md) |
| ANALYSIS-016 | [analysis-002](analysis-002.md) | 2.4, 3.5, 4.4, 5.1 | warning | yes: subagents in this session see the AGENTS.md text from session start; docs note added; 3.5 and 4.4 to be redone after restart | verified (docs; restart + 3.5/4.4 rerun tracked in tasks) | — | [analysis-003](analysis-003.md) |
| ANALYSIS-017 | [analysis-002](analysis-002.md) | 2.3 / D10 | warning | yes: wrong-case path gave a wrong identity | verified | — | [qa-004](qa-004.md), [analysis-003](analysis-003.md) |
| ANALYSIS-018 | [analysis-002](analysis-002.md) | 2.1 / safety | warning | yes: developer formatter would rewrite unrelated in-flight files | verified | — | [qa-004](qa-004.md), [analysis-003](analysis-003.md) |
| ANALYSIS-019 | [analysis-002](analysis-002.md) | 2.3 / D10 | suggestion | no; fixed in DEV-005 (pinned diff defaults, label); structural identity deferred | verified | — | [qa-004](qa-004.md), [analysis-003](analysis-003.md) |
| ANALYSIS-020 | [analysis-002](analysis-002.md) | 2.1 | suggestion | no; fixed in DEV-005 | verified | — | [analysis-003](analysis-003.md) |
| ANALYSIS-021 | [analysis-002](analysis-002.md) | 4.1, 5.4 | suggestion | no; fixed in DEV-005 (durable "Known client restrictions" section); markers cleared in task 4.1 | verified | — | [analysis-003](analysis-003.md) |
| ANALYSIS-022 | [analysis-002](analysis-002.md) | 2.2 | suggestion | no; fixed in DEV-005 | verified | — | [analysis-003](analysis-003.md) |

## Deferred follow-ups (D12)

- Restriction scope update (QA-R5, after the restart): this session's QA subagent was told up front not to write report files and did not try, so the restriction is not analyzer-only. The docs/agent-workflow.md "Known client restrictions" wording ("QA writes succeed") is now too narrow. Update it together with the deferred Codex verification. The fallback already covers any reviewer.
- Task 3.3 result: before the restart, the native QA role listed an Agent tool. After the restart, QA had no Agent/Task tool. Nesting is prevented natively in the current session; the earlier exposure is recorded as observed and was never exercised.

- User decision 2026-10-23 (after the session restart): Codex verification is deferred, to be done by the user later. It covers task 1.2 native Codex role discovery, 4.1 Codex hand-off/native round/model-id acceptance/marker cleanup, 4.2 concurrent Claude+Codex, and 4.3 same-change conflict across clients. These remain unverified, not passed and not restricted.
 | qa (native) | Claude Code / **haiku override** (rationale: task 3.4 probe; a cheaper model suffices for a factual check) | 3.3 tool observation, 3.4 override, 3.5 + 4.4 content check | `reports/qa-005.md` (saved verbatim by orchestrator) | none | returned: 3.4 override took effect (self-reported claude-haiku-5-5); 3.5/4.4 starting-context AGENTS.md matches the file on disk; role files were enough; 3.3 no Agent/Task tool; no CLAUDE.md |


- Observation, QA-R4: QA ran the mutating scoped `dotnet format --include` on one test file to check the developer command, which the reviewer role forbids. The tree was verified unchanged, so no fix round. If this recurs, consider giving reviewers a disposable copy for command checks.

- ANALYSIS-019 option: structural (diff-setting-independent) identity. Reconsider if cross-machine identity comparison is needed.

## Finding details and history

### QA-001: documented `-Paths` form of review-identity.ps1 is broken

- Sources and evidence: [qa-001](qa-001.md). The orchestrator also observed the comma form returning `files=0` before review; this was deliberately withheld from reviewers to test independent detection.
- Required behavior: D10. The documented invocation produces the scoped identity.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-23 | — | reported | QA-R1 | qa-001 |

### QA-002: Codex model ids lack native evidence

- Sources and evidence: [qa-001](qa-001.md). Docs-only evidence in the DEV-001 handoff.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-23 | — | reported | QA-R1 | qa-001 |

### QA-003: main-session association rule absent from role files

- Sources and evidence: [qa-001](qa-001.md)

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-23 | — | reported | QA-R1 | qa-001 |

## Other evidence

- 21 `dotnet test` failures (Providers.OpenSpecSelectionTests, Providers.CaptureTests): QA measured that both classes pass 50/50 at baseline `b3e007e` in a disposable worktree, so the failures belong to unrelated in-flight `src/` changes. Not tracked as findings of this change.
- Task 3.3 probe (partial): the Agent tool is exposed to the native Claude `qa` role on 2.1.309, so nested delegation is not prevented by tool absence. No launch was attempted. The actual outcome needs a dedicated disposable probe.

## Completion assessment

- Current QA / analysis reports: [qa-004](qa-004.md) and [analysis-003](analysis-003.md) on target `files=18 diff=6c9019099702 untracked=6/50a0657c34e7`; post-restart probe [qa-005](qa-005.md).
- Required acceptance: Claude groups 1-3 and 4.4 satisfied. Codex parts (1.2 native discovery, 4.1-4.3) deferred by explicit user decision on 2026-10-23 and still unverified.
- Findings: every finding is verified, duplicate, or deferred. No blocking finding is open.
- Provider restrictions: ORCH-001 (Claude Code report-write refusal) has evidence and a user-approved fallback. No Codex restriction is claimed.
