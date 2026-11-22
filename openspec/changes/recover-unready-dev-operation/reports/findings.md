# Findings: recover-unready-dev-operation

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code (Opus 5.5)
- Session identity: claude-recover-unready-20261122
- Since (UTC): 2026-11-22T09:45Z
- Previous owner: none

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DEV-001 | developer | Claude Code / role default | none: role default | 1.1, 2.1-2.4, 3.1, 4.1 (build and tests); baseline f4dfa4e, pre-assignment scoped identity `head=f4dfa4e baseline=f4dfa4e files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc` | evidence/implementation-001.md | none | returned; done |
| QA-001 | qa | Claude Code / role default | none: role default | 1.1, 2.1-2.4, 3.1 on DEV-001 target `head=f4dfa4e baseline=f4dfa4e files=9 diff=43216f4f40a3 untracked=1/681383ff9c34` | reports/qa-001.md | QA-001 through QA-099 | returned |
| ANALYSIS-001 | code-analyzer | Claude Code / role default | none: role default | 1.1, 2.1-2.4, 3.1 on the same DEV-001 target | reports/analysis-001.md (saved verbatim by the orchestrator after a refused write) | ANALYSIS-001 through ANALYSIS-099 | returned |
| DEV-002 | developer | Claude Code / role default | none: role default | Fix cycle 1: QA-001 (with duplicate ANALYSIS-002), ANALYSIS-001, ANALYSIS-003, ANALYSIS-005, ANALYSIS-008, ANALYSIS-009, ANALYSIS-010 on DEV-001 target `head=f4dfa4e baseline=f4dfa4e files=9 diff=43216f4f40a3 untracked=1/681383ff9c34` | evidence/implementation-002.md | none | returned; done |
| QA-002 | qa | Claude Code / role default | none: role default | Recheck QA-001 (with ANALYSIS-002), ANALYSIS-005, ANALYSIS-008, ANALYSIS-009, ANALYSIS-010 plus regression on DEV-002 target `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596` | reports/qa-002.md | QA-100 through QA-199 (unused); no suggestions in recheck | returned |
| ANALYSIS-002 | code-analyzer | Claude Code / role default | none: role default | Recheck ANALYSIS-001, -002, -003, -005, -008, -009, -010 plus regression on the same DEV-002 target | reports/analysis-002.md (saved verbatim by the orchestrator after a refused write) | ANALYSIS-100 through ANALYSIS-199; no suggestions in recheck | returned |
| DEV-003 | developer | Claude Code / role default | none: role default | Fix cycle 2 (last permitted): ANALYSIS-100, ANALYSIS-101 on DEV-002 target `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596` | evidence/implementation-003.md | none | returned; done |
| QA-003 | qa | Claude Code / role default | none: role default | Final recheck ANALYSIS-100, ANALYSIS-101 plus regression on DEV-003 target `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832` | reports/qa-003.md | QA-200 through QA-299 (unused); no suggestions | returned |
| ANALYSIS-003 | code-analyzer | Claude Code / role default | none: role default | Final recheck ANALYSIS-100, ANALYSIS-101 (and ANALYSIS-009 residual) plus regression on the same target | reports/analysis-003.md (saved verbatim by the orchestrator after a refused write) | ANALYSIS-200 through ANALYSIS-299; no suggestions | returned |
| DEV-004 | developer | Claude Code / role default | none: role default | User-authorized docs-only fix: ANALYSIS-200 on DEV-003 target `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832` | evidence/implementation-004.md | none | returned; done |
| QA-004 | qa | Claude Code / role default | none: role default | Light docs-only recheck of ANALYSIS-200 on DEV-004 target `head=f4dfa4e baseline=f4dfa4e files=10 diff=4995f1ed6ca6 untracked=1/943d3bdda832` | reports/qa-004.md | QA-300 through QA-399 (unused) | returned |
| ANALYSIS-004 | code-analyzer | Claude Code / claude-sonnet-5-5 | docs-only target: cost-efficient default per workflow | Light docs-only recheck of ANALYSIS-200 on the same target | reports/analysis-004.md (saved verbatim by the orchestrator after a refused write) | ANALYSIS-300 through ANALYSIS-399 (unused) | returned |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 2.3; Finalize scenario | warning | yes: breaks the agreed interrupted-finalize retry and recreates the deadlock | verified | DEV-002 | [qa-002](qa-002.md), [analysis-002](analysis-002.md) |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 2.3 | warning | duplicate of QA-001 | duplicate | DEV-002 (via QA-001) | [analysis-002](analysis-002.md) |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 2.1, 2.2 | warning | yes: Finalize can remove records of a live same-manifest Rollback (spec: only records the operation owns, safely) | verified | DEV-002 | [analysis-002](analysis-002.md) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 2.1, 2.2; sessions not disturbed | warning | yes: holding the launch gate across CIM drains can fail Claude Code and Codex hooks | verified | DEV-002 | [analysis-002](analysis-002.md) |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 2.2; spec "names the supported next step" | suggestion | yes (raised by orchestrator): contradictory next steps break required behavior | verified | DEV-002 | [qa-002](qa-002.md), [analysis-002](analysis-002.md) (regression ANALYSIS-100 tracked separately) |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | 1.1 verification | suggestion | yes (raised): the required path-free verification is vacuous | verified | DEV-002 | [qa-002](qa-002.md), [analysis-002](analysis-002.md) |
| ANALYSIS-009 | [analysis-001](analysis-001.md) | 1.1 "never contains the profile path" | suggestion | yes (raised): breaks task 1.1 acceptance and docs claim | verified | DEV-002, DEV-003 | [analysis-003](analysis-003.md) |
| ANALYSIS-010 | [analysis-001](analysis-001.md) | spec: next step with the operation record path | suggestion | yes (raised): the field case Preflight/Switch never leads to Finalize | verified | DEV-002 | [qa-002](qa-002.md), [analysis-002](analysis-002.md) |
| ANALYSIS-100 | [analysis-002](analysis-002.md) | 2.2; spec next-step rule | warning | yes: regression of ANALYSIS-005; following the hint stops a healthy tray | verified | DEV-003 | [qa-003](qa-003.md), [analysis-003](analysis-003.md) |
| ANALYSIS-101 | [analysis-002](analysis-002.md) | 1.1 path-free messages | warning | yes: residual of blocking ANALYSIS-009 | verified | DEV-003 | [qa-003](qa-003.md), [analysis-003](analysis-003.md) |
| ANALYSIS-200 | [analysis-003](analysis-003.md) | 3.1 docs | warning | yes (user decision 2026-11-22: one-word fix with light review) | verified | DEV-004 | [qa-004](qa-004.md), [analysis-004](analysis-004.md) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 1.1 | suggestion | no | deferred | none | n/a |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 2.1, 3.1 | suggestion | no: field record keeps its package; docs order nuance | deferred | none | n/a |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | 1.1, 2.2 | suggestion | no | deferred | none | n/a |
| ANALYSIS-011 | [analysis-001](analysis-001.md) | 1.1 | suggestion | no: needs product code outside the change's impact | deferred | none | n/a |
| QA-002 | [qa-001](qa-001.md) | 2.4, 1.1 | suggestion | no: fixture state matches a real rollback; live readiness timeout is task 4.3 | deferred | none | n/a |

## Finding details and history

### QA-001: Interrupted Finalize between the two record deletions cannot be retried

- Sources and evidence: [qa-001](qa-001.md), source reading only (no reproducing fixture)
- Required behavior: task 2.3, design decision 3: a retry on a `finalized-without-tray` manifest finishes the record removal
- Triage: blocking warning with a clear in-scope fix
- Action / assignment: pending; developer to remove each record still present after verifying it against the manifest, with a half-removed-pair retry test
- Verification: pending

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-11-22 | none | reported | orchestrator | QA-001 report; routing waits for ANALYSIS-001 so the target stays stable |
| 2026-11-22 | reported | assigned | orchestrator | DEV-002 fix cycle 1, with duplicate ANALYSIS-002 |

### QA-002: End-to-end test does not run a real readiness timeout

- Sources and evidence: [qa-001](qa-001.md)
- Triage: suggestion; deferred under the severity gate
- Deferral: the live readiness timeout and Finalize run are covered by task 4.3 on the installed build; reconsider if 4.3 shows a gap

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-11-22 | none | deferred | orchestrator | severity gate |

## History

- 2026-11-22: Change proposed after the installed switch to 0.1.9026 deadlocked (rollback of the 2026-11-21 operation restored 0.1.9022 but its tray never confirmed readiness because `state.json` is at the budget). User authorized apply. Assigned DEV-001 at baseline f4dfa4e.
- 2026-11-22: DEV-001 returned on `head=f4dfa4e baseline=f4dfa4e files=9 diff=43216f4f40a3 untracked=1/681383ff9c34` (confirmed by the orchestrator). Release build clean; install-dev and skill tests 109/109; full suite 1810 passed, 14 failed (the 13 known plus AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor, which passed alone). The not-ready deadline path is covered in the function harness with stubbed launcher, scan and owner read; the stuck-rollback fixture kills the installer after the pointer phase. Developer additions beyond the task list for review: `trayRestartNote` in Switch and Rollback results, finalized manifests make Switch and Rollback say run Preflight again, and the extracted `Start-TrayLauncher` seam. Assigned QA-001 and ANALYSIS-001.
- 2026-11-22: QA-001 returned (identity unchanged): 109/109 install-dev and skill tests, hygiene passed; QA-001 warning, QA-002 suggestion. ANALYSIS-001 returned (identity unchanged), report saved verbatim after a refused write: three warnings, eight suggestions. Triage recorded above; DEV-002 assigned as fix cycle 1.
- 2026-11-22: DEV-002 returned on `head=f4dfa4e baseline=f4dfa4e files=10 diff=74e04e2276e2 untracked=1/026df138b596` (confirmed by the orchestrator; evidence written in a follow-up without source changes). Install-dev and skill tests 122 passed (one harness result-file read race passed 42/42 on rerun); full suite 1824 passed, 13 failed (known HookTrayStart and ProcessStopAuthority; CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens failed once, passed on rerun); the owner-live and launch-gate tests passed standalone. All fixed findings awaiting verification. Assigned QA-002 and ANALYSIS-002 (fix cycle 1 of 2).
- 2026-11-22: QA-002 returned (identity unchanged, 122/122, hygiene passed): QA-001, ANALYSIS-005, -008, -009, -010 verified functionally; no new findings. ANALYSIS-002 returned (identity unchanged; report saved verbatim after a refused write): ANALYSIS-001, -002, -003, -008, -010 verified; ANALYSIS-005 verified for its cases with regression ANALYSIS-100; ANALYSIS-009 partially verified with residual ANALYSIS-101. Both new findings are blocking with clear message-only fixes. Assigned DEV-003 as fix cycle 2, the last permitted; any finding left after its recheck goes to the user.
- 2026-11-22: DEV-003 returned on `head=f4dfa4e baseline=f4dfa4e files=10 diff=103d8365aee3 untracked=1/943d3bdda832` (confirmed by the orchestrator): new `completed` disposition for a finished Rollback without records; `Restore-StoppedTrayAfterFailure` uses the relative record path; one CLI test and one harness scenario added; docs and skills unchanged. Release build clean; install-dev and skill tests 124/124; format and strict validation passed; full suite not run. Assigned final rechecks QA-003 and ANALYSIS-003.
- 2026-11-22: QA-003 returned (identity unchanged; 124/124; format on all four test files and hygiene passed; full suite 1826 passed, 13 failed: the 12 known plus WindowsUserIdleSourceTests.QueryFailureSettlesToUnknownWithoutEscapingTheObserver, 9/9 on class rerun). ANALYSIS-003 returned (identity unchanged; saved verbatim after a refused write): ANALYSIS-100, -101 and the ANALYSIS-009 residual verified; new ANALYSIS-200, a docs-only omission of the `completed` kind in docs/development.md. Both fix cycles are used, so ANALYSIS-200 is escalated to the user with a recommendation.
- 2026-11-22: User decisions: fix ANALYSIS-200 as a one-word docs change with a light docs-only review by both roles (beyond the two fix cycles, by explicit user authorization); and authorize all installed-recovery steps of task 4.3 (Finalize on the 2026-11-21 manifest, Preflight, Switch to a new dev build from the commit, tray start). Assigned DEV-004.
- 2026-11-22: DEV-004 returned on `head=f4dfa4e baseline=f4dfa4e files=10 diff=4995f1ed6ca6 untracked=1/943d3bdda832` (docs/development.md line 49 only). QA-004 and ANALYSIS-004 (identity unchanged; analyzer report saved verbatim after a refused write) verified ANALYSIS-200 with no new findings. No blocking finding remains. Tasks 1.1, 2.1-2.4, 3.1, 4.1 and 4.2 checked; 4.3 (installed recovery) remains and is user-authorized.

### ANALYSIS-001 to ANALYSIS-011

Details are in [analysis-001](analysis-001.md). Triage 2026-11-22: ANALYSIS-002 is a duplicate of QA-001 (canonical QA-001, both reports kept). ANALYSIS-001 and ANALYSIS-003 are gated warnings with clear in-scope fixes. ANALYSIS-005, -008, -009 and -010 are reported as suggestions but each breaks agreed acceptance (spec next-step rule, task 1.1 path-free verification and message, spec record path), so they are blocking and included in DEV-002. ANALYSIS-004, -006, -007 and -011 are deferred under the severity gate; follow-up destination `docs/follow-ups.md` at completion; ANALYSIS-011 needs product code outside this change.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-11-22 | none | assigned / duplicate / deferred | orchestrator | as above; DEV-002 fix cycle 1 |

## Completion assessment

- Current QA / analysis reports: [qa-003](qa-003.md) and [analysis-003](analysis-003.md) on the DEV-003 target; [qa-004](qa-004.md) and [analysis-004](analysis-004.md) on the final docs-only target `head=f4dfa4e baseline=f4dfa4e files=10 diff=4995f1ed6ca6 untracked=1/943d3bdda832`
- Required acceptance: fixture acceptance for tasks 1.1, 2.1-2.4 and 3.1 satisfied; Release build clean; install-dev and skill tests 124/124; full suite failures only known or rerun-passing flaky tests; strict validation passed. Task 4.3 (installed recovery on the maintainer's installation) remains.
- Findings: 10 verified (QA-001, ANALYSIS-001, -003, -005, -008, -009, -010, -100, -101, -200), 1 duplicate (ANALYSIS-002 of QA-001), 5 deferred (QA-002, ANALYSIS-004, -006, -007, -011); none blocking open.
- Nonblocking follow-ups: QA-002 (real readiness timeout covered by task 4.3), ANALYSIS-004 (reason quote filter and log share mode), ANALYSIS-006 (Finalize prelude order), ANALYSIS-007 (duplicated presence and scan), ANALYSIS-011 (budget constant in product code); destination docs/follow-ups.md at archive.
- Evidence gaps / provider restrictions: the not-ready deadline path is fixture- and harness-proven only; a live run is task 4.3. No provider restriction claimed.
- Commit: recorded below after the commit.
- OpenSpec task reconciliation: 8/9 checked; 4.3 open.
