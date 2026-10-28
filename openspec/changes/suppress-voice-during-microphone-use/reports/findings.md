# Findings: suppress-voice-during-microphone-use

## Current orchestrator

- Client / version: Codex CLI 0.164.0
- Session identity: codex-microphone-20261028
- Since (UTC): 2026-10-28T15:20:00Z
- Previous owner: none

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DEV-001 | developer | Codex / gpt-6-luna | none: role default | All 11 tasks; baseline 7ddef11; final diff 1a90ff0cc8f5, untracked 4/8eab2ff9c917 | Stable handoff returned; build, scoped format, 36 focused tests pass; full suite unverified | none | returned |
| QA-REVIEW-001 | QA | Codex / gpt-6-luna | none: role default | All 11 tasks; head/base 7ddef11; 15 files, diff 1a90ff0cc8f5, untracked 4/8eab2ff9c917 | reports/qa-001.md | QA-001 through QA-020 | returned |
| ANALYSIS-REVIEW-001 | code-analyzer | Codex / gpt-6.1-sol | none: role default, high reasoning | All 11 tasks; head/base 7ddef11; 15 files, diff 1a90ff0cc8f5, untracked 4/8eab2ff9c917 | reports/analysis-001.md | ANALYSIS-001 through ANALYSIS-020 | returned |
| DEV-002 | developer | Codex / gpt-6-luna | none: role default | ANALYSIS-001 through 006; QA-004 through 006; first correction round; target ec32fe6ba6f3 /19783d7998c7 | Build/format, 53 focused tests and isolated native metadata smoke pass (developer evidence) | none | returned |
| QA-RECHECK-002 | QA | Codex / gpt-6-luna | none: role default | QA-004 through 006; ANALYSIS-001 through 006 cross-cutting regression; target ec32fe6ba6f3 /19783d7998c7 | reports/qa-002.md | QA-021 through QA-040 | returned |
| ANALYSIS-RECHECK-002 | code-analyzer | Codex / gpt-6.1-sol | none: role default, high reasoning | ANALYSIS-001 through 006 plus affected regression; target ec32fe6ba6f3 /19783d7998c7 | reports/analysis-002.md | ANALYSIS-021 through ANALYSIS-040 | returned |

| DEV-003 | developer | Codex / gpt-6-luna | none: role default | QA-021 test-only; second/final correction; target0c7e59dec84c /19783d7998c7 | Build/format, 41 queue tests and diff check pass; product unchanged | none | returned |
| QA-RECHECK-003 | QA | Codex / gpt-6-luna | none: role default | QA-021 plus full regression; target0c7e59dec84c /19783d7998c7 | reports/qa-003.md | QA-041 through QA-060 (unused) | returned |
| ANALYSIS-RECHECK-003 | code-analyzer | Codex / gpt-6.1-sol | none: role default, high reasoning | QA-021 test correctness, unaffected product evidence mapping; target0c7e59dec84c /19783d7998c7 | reports/analysis-003.md | ANALYSIS-041 through ANALYSIS-060 | returned |

| DEV-004 | developer | Codex / gpt-6-luna | none: role default | User-authorized extra round; QA-021; target38fed194f59c /a12d62f18a64 | Only native source clock seam and queue tests changed; build/format/4 targeted checks pass | none | returned |
| QA-RECHECK-004 | QA | Codex / gpt-6-luna | none: role default | QA-021 and REGRESSION-001 plus affected regression; target38fed194f59c /a12d62f18a64 | reports/qa-004.md | QA-061 through QA-080 | returned |
| ANALYSIS-RECHECK-004 | code-analyzer | Codex / gpt-6.1-sol | none: role default, high reasoning | QA-021 clock semantics and deterministic fixtures plus affected regression; target38fed194f59c /a12d62f18a64 | reports/analysis-004.md | ANALYSIS-061 through ANALYSIS-080 | returned |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ANALYSIS-001 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 1.2, 1.3, 4.1 | critical | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| ANALYSIS-002 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 1.2, 1.3, 4.1 | critical | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| ANALYSIS-003 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 1.2, 1.3, 2.2, 4.1 | critical | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| ANALYSIS-004 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 1.3, 4.2 | warning | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| ANALYSIS-005 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 2.3 | warning | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| ANALYSIS-006 | [Analysis 001](analysis-001.md), [002](analysis-002.md) | 1.3 | warning | resolved by independent evidence | verified | orchestrator | analysis-002.md on ec32fe6ba6f3 /19783d7998c7 |
| QA-004 | [QA 001](qa-001.md), [002](qa-002.md) | 2.2 | warning | resolved by independent evidence | verified | orchestrator | qa-002.md on ec32fe6ba6f3 /19783d7998c7 |
| QA-005 | [QA 001](qa-001.md), [002](qa-002.md) | 2.3 | warning | resolved by independent evidence | verified | orchestrator | qa-002.md on ec32fe6ba6f3 /19783d7998c7 |
| QA-006 | [QA 001](qa-001.md), [002](qa-002.md) | 2.4 | warning | resolved by independent evidence | verified | orchestrator | qa-002.md on ec32fe6ba6f3 /19783d7998c7 |
| QA-001 | [QA 001](qa-001.md) | 1.3 | critical | canonical ANALYSIS-001 verified | duplicate | orchestrator | linked to ANALYSIS-001 |
| QA-002 | [QA 001](qa-001.md) | 1.3 | critical | canonical ANALYSIS-002 verified | duplicate | orchestrator | linked to ANALYSIS-002 |
| QA-003 | [QA 001](qa-001.md) | 1.3 | critical | canonical ANALYSIS-003 verified | duplicate | orchestrator | linked to ANALYSIS-003 |

| QA-021 | [QA 002](qa-002.md), [Analysis 003](analysis-003.md), [QA 004](qa-004.md), [Analysis 004](analysis-004.md) | 1.3, 3.2 | warning | resolved by independent evidence | verified | orchestrator | qa-004 and analysis-004 on38fed194f59c /a12d62f18a64 |
| REGRESSION-001 | [QA 003](qa-003.md), [QA 004](qa-004.md); orchestrator triage ID | 3.2 / required default regression | warning | resolved by clean full run | verified | orchestrator | 1045 passed, zero failed/skipped on38fed194f59c /a12d62f18a64 |

## Finding details and history

### ANALYSIS-001: Native collection IID mismatch

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-001.
- Required behavior: tasks 1.2, 1.3, 4.1; Use exact WinSDK IID; audit handwritten COM identities and add executable native-contract assertions.
- Triage: blocking critical; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Use exact WinSDK IID; audit handwritten COM identities and add executable native-contract assertions.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### ANALYSIS-002: Session-manager COM method slots mismatch

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-002.
- Required behavior: tasks 1.2, 1.3, 4.1; Restore complete inherited/derived vtable order and signatures from official headers; validate before native metadata smoke.
- Triage: blocking critical; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Restore complete inherited/derived vtable order and signatures from official headers; validate before native metadata smoke.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### ANALYSIS-003: Native activity values reversed

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-003.
- Required behavior: tasks 1.2, 1.3, 2.2, 4.1; Use explicit SDK enum values and exercise raw 0/1/2 and invalid-state conversion through production mapping.
- Triage: blocking critical; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Use explicit SDK enum values and exercise raw 0/1/2 and invalid-state conversion through production mapping.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### ANALYSIS-004: Cached snapshot can exceed observation freshness

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-004.
- Required behavior: tasks 1.3, 4.2; Expire cache by original observation age; never refresh old evidence by collection time; verify source/doctor Unknown and recovery.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Expire cache by original observation age; never refresh old evidence by collection time; verify source/doctor Unknown and recovery.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### ANALYSIS-005: Missing immediate guard before native speech

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-005.
- Required behavior: tasks 2.3; Recheck mute/stop and speech guard after voice initialization immediately before Speak; test silently with injected initialization.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Recheck mute/stop and speech guard after voice initialization immediately before Speak; test silently with injected initialization.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### ANALYSIS-006: Unbounded callback-session admission

- Sources and evidence: [analysis-001.md](analysis-001.md), finding ANALYSIS-006.
- Required behavior: tasks 1.3; Cap retained callback controls at admission; release overflow safely, mark scan incomplete/Unknown, test concurrency and cleanup.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Cap retained callback controls at admission; release overflow safely, mark scan incomplete/Unknown, test concurrency and cleanup.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [analysis-002.md](analysis-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent analysis-002.md evidence verifies correction on unchanged target. |

### QA-004: Incomplete Voice/Mute/opt-out matrix

- Sources and evidence: [qa-001.md](qa-001.md), finding QA-004.
- Required behavior: tasks 2.2; Exercise all eight preference combinations for inactive/active/unknown with recording sinks and preserved preference values.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Exercise all eight preference combinations for inactive/active/unknown with recording sinks and preserved preference values.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [qa-002.md](qa-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent qa-002.md evidence verifies correction on unchanged target. |

### QA-005: Missing transition after speech begins

- Sources and evidence: [qa-001.md](qa-001.md), finding QA-005.
- Required behavior: tasks 2.3; Use silent recording sink to start speech, activate probe, poll guard and cancel remaining speech without catch-up.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Use silent recording sink to start speech, activate probe, poll guard and cancel remaining speech without catch-up.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [qa-002.md](qa-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent qa-002.md evidence verifies correction on unchanged target. |

### QA-006: Missing reminder-like aggregate fixture

- Sources and evidence: [qa-001.md](qa-001.md), finding QA-006.
- Required behavior: tasks 2.4; Exercise reminder-like and normal AudioItems through the shared aggregate boundary for both providers.
- Triage: blocking warning; violates agreed acceptance, within authorized scope.
- Action / assignment: developer DEV-002, first correction round; Exercise reminder-like and normal AudioItems through the shared aggregate boundary for both providers.
- Duplicate of: none.
- Deferral: none.
- User decision: no new scope choice required; accepted best-effort limitations do not waive this behavior.
- Verification: independently verified in [qa-002.md](qa-002.md) on target ec32fe6ba6f3 /19783d7998c7. QA also confirms functional correction; QA-021 tracks a separate scheduling-dependent regression fixture.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted first-report evidence; assigned DEV-002 with independent QA/analysis recheck required. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-002 | Corrected target ec32fe6ba6f3 /19783d7998c7; build/format and 53 focused fixtures pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Independent qa-002.md evidence verifies correction on unchanged target. |

### QA-001: Duplicate native contract finding

- Sources and evidence: [qa-001.md](qa-001.md), QA-001; [analysis-001.md](analysis-001.md), ANALYSIS-001.
- Required behavior: task 1.3, correct native observation contracts.
- Triage: critical duplicate; canonical ANALYSIS-001 independently verified by analysis-002 and carried forward in analysis-004.
- Action / assignment: included in DEV-002 under the canonical ID.
- Duplicate of: ANALYSIS-001.
- Deferral: none.
- User decision: not needed.
- Verification: follows canonical correction and independent recheck.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | duplicate | orchestrator | Independent QA confirms the same native defect; retain both reports. |

### QA-002: Duplicate native contract finding

- Sources and evidence: [qa-001.md](qa-001.md), QA-002; [analysis-001.md](analysis-001.md), ANALYSIS-002.
- Required behavior: task 1.3, correct native observation contracts.
- Triage: critical duplicate; canonical ANALYSIS-002 independently verified by analysis-002 and carried forward in analysis-004.
- Action / assignment: included in DEV-002 under the canonical ID.
- Duplicate of: ANALYSIS-002.
- Deferral: none.
- User decision: not needed.
- Verification: follows canonical correction and independent recheck.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | duplicate | orchestrator | Independent QA confirms the same native defect; retain both reports. |

### QA-003: Duplicate native contract finding

- Sources and evidence: [qa-001.md](qa-001.md), QA-003; [analysis-001.md](analysis-001.md), ANALYSIS-003.
- Required behavior: task 1.3, correct native observation contracts.
- Triage: critical duplicate; canonical ANALYSIS-003 independently verified by analysis-002 and carried forward in analysis-004.
- Action / assignment: included in DEV-002 under the canonical ID.
- Duplicate of: ANALYSIS-003.
- Deferral: none.
- User decision: not needed.
- Verification: follows canonical correction and independent recheck.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | duplicate | orchestrator | Independent QA confirms the same native defect; retain both reports. |


### QA-021: Scheduling-dependent cache recovery fixture

- Sources and evidence: [qa-002.md](qa-002.md), full default suite 1044 passed and one failed; exact fixture passed five isolated rechecks.
- Required behavior: deterministic verification of cache freshness/recovery; full required regression acceptance.
- Triage: blocking warning; fixture assumes background ThreadPool work starts within a fixed sleep. Pending work returns Unknown safely; no stale Inactive product output observed.
- Action / assignment: DEV-003, second and final correction round. Synchronize scan start/completion explicitly in the test; preserve original age assertions and coverage. No production changes authorized for this fixture correction.
- Duplicate of: none; distinct from corrected ANALYSIS-004.
- Deferral: none.
- User decision: on 2026-10-28 the user replied "okay, go ahead" to the explicit request for one additional focused clock/test correction and review round. DEV-004 may add a narrow injectable clock and synchronize both delayed-scan fixtures; required assertions and runtime behavior stay in scope.
- Verification: [analysis-004.md](analysis-004.md) independently verifies clock coverage, four compiled timing tests and the prior delayed-consumer case under fake time. [qa-004.md](qa-004.md) verifies four targeted checks plus the complete 1045/1045 default suite on38fed194f59c /a12d62f18a64. No remaining timing finding.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | assigned | orchestrator | Accepted QA-002 isolated-vs-full evidence; DEV-003 test-only synchronization correction. |
| 2026-10-28 | assigned | awaiting-verification | developer DEV-003 | Test-only start barriers; 41 queue tests/build/format pass; target0c7e59dec84c /19783d7998c7. |
| 2026-10-28 | awaiting-verification | reported | orchestrator | Analysis-003 controlled delayed-consumer fixture reproduces a remaining near-expiry assertion race. |
| 2026-10-28 | reported | awaiting-user-decision | orchestrator | Repository two-cycle limit reached; requested one additional narrow correction round. No third implementation assigned. |
| 2026-10-28T17:49Z | awaiting-user-decision | assigned | orchestrator | User explicitly authorized one extra round; assigned DEV-004 clock seam and deterministic fixture correction. |
| 2026-10-28T17:53Z | assigned | awaiting-verification | developer DEV-004 | Default runtime clock unchanged; explicit fake-time tests replace sleeps, build/format and four focused checks pass. |
| 2026-10-28 | awaiting-verification | verified | orchestrator | Accepted qa-004 and analysis-004 on unchanged38fed194f59c /a12d62f18a64; deterministic boundary/recovery and full suite pass. |

### REGRESSION-001: Three full-suite file/index failures remain unclassified

- Sources and evidence: [qa-003.md](qa-003.md), full default run outside sandbox: 1041 passed, four failed. Three failures concern palette allocation AtomicFile.Move, VS Code navigation AtomicFile.Move, and status-line fallback Git index writing. The fourth is the delayed-scan fixture tracked in QA-021.
- Required behavior: successful required regression checks on the reviewed implementation.
- Triage: blocking warning for acceptance, not proof of a microphone product defect. Exact expanded five-case isolated rerun passed 5/5 outside sandbox; no supported environment or product root cause established.
- Action / assignment: no further implementation or broad retest assigned after the two-cycle limit. Proposed next step is the requested narrow timing-fixture round followed by one full run; if file/index failures recur, investigate against baseline before proposing unrelated code changes.
- Duplicate of: none; distinct from cache timing QA-021.
- Deferral: none approved.
- User decision: user approved one extra focused correction/review round on 2026-10-28. QA will rerun the full suite and investigate recurring file/index failures against baseline; no unrelated source changes are authorized merely to obtain a pass.
- Verification: [qa-004.md](qa-004.md) establishes a clean full default run outside the sandbox: 1045 passed, zero failed/skipped, on38fed194f59c /a12d62f18a64. Earlier file/index failures did not recur; no environment or product root cause is inferred and no unrelated sources were changed.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | reported | awaiting-user-decision | orchestrator | Assigned orchestration triage ID to explicit QA-003 failures; no unsupported root-cause attribution. |
| 2026-10-28T17:49Z | awaiting-user-decision | assigned | orchestrator | User authorized focused timing correction followed by full regression and bounded baseline investigation if failures recur. |
| 2026-10-28 | assigned | verified | orchestrator | QA-004 full suite1045/1045 passes; previous file/index failures did not recur. Baseline investigation not needed. |

## Completion assessment

- Current QA / analysis reports: [qa-004.md](qa-004.md) and [analysis-004.md](analysis-004.md) on accepted target38fed194f59c /a12d62f18a64. Both before/after identities match. Prior native ABI, metadata smoke, provider parity and policy evidence is explicitly mapped forward in those reports.
- Required acceptance: all11 tasks and all five delta requirements satisfied by the evidence below. Full default regression1045 passed, zero failed/skipped; targeted freshness/single-flight4/4 and ABI metadata1/1 pass independently. Developer Release build has zero warnings/errors; scoped formatting and whitespace pass.
- Findings: 11 canonical findings verified, three duplicates linked; zero unresolved blocking findings or unapproved deferrals. No optional suggestions or follow-ups recorded.
- Hygiene: QA-004 passed the required change-root/documentation scan. The orchestrator repeated the scan over all30 scoped committable files after task/ledger edits; no sensitive content was found. One broad assignment-pattern match was checked as the existing local COM voice-token variable, with a precise expression-only classification rather than a credential exception.
- Evidence gaps / provider restrictions: shared best-effort capture-session observation is the user's accepted scope. Exclusive-mode capture may be missed; RDP/probe failures yield Unknown. No provider exception claimed. Physical microphone activity, sound, RDP and installed-build/live provider trials were not performed or claimed passed; acceptance uses silent fixtures, primary API contracts and prior read-only native metadata observations as specified.
- Commit: accepted implementation ready for scoped local commit on suppress-voice-during-microphone-use; SHA will be recorded after commit. No push, archive or installation authorized/performed.
- OpenSpec task reconciliation:11/11 complete. The user expressly authorized DEV-004 past the normal two-cycle limit; its corrected target received both required independent reviews.

### Verification dimensions

| Dimension | Evidence and result |
| --- | --- |
| Completeness | 11/11 tasks and five delta requirements mapped to implementation and independent checks |
| Correctness | QA-004 full default suite1045/1045; clock/age/future/single-flight fixtures4/4; SDK metadata1/1; existing matched Claude/Codex, mixed aggregates, controls, tone/speech transitions and reminder-like fixtures rerun |
| Coherence | Analysis-004 verifies default UTC/runtime limits, deterministic clock seam and preserved shared policy; previous ABI, callback bounds, immediate speech guard and no-catch-up evidence carried forward with explicit target mapping |

### Acceptance mapping

| Task | Implementation / evidence |
| --- | --- |
| 1.1 | Shared AudioQueue/sink/config boundaries; source review and analysis-002/004 |
| 1.2 | Primary Microsoft contracts in design/analysis-001, SDK metadata/raw-state fixtures and accepted best-effort scope |
| 1.3 | WindowsMicrophoneActivitySource; bounded/error/partial/overflow/future/stale/single-flight fixtures, native metadata smoke analysis-002, deterministic clock rechecks QA-004/analysis-004 |
| 2.1 | AppConfig, ConfigStore compatibility, tray control, diagnostics; config round-trip/extension-data tests and QA-002/004 |
| 2.2 | Shared AudioQueue speech guard; all Voice/Mute/opt-out combinations under active/inactive/unknown, QA-002/004 |
| 2.3 | Separate tone/speech cancellation, immediate pre-Speak guard and discarded phrases; initialization/mid-speech/no-replay fixtures, analysis-002/004 and QA-002/004 |
| 2.4 | Shared normal/reminder-like mixed aggregate fixture, QA-002/004 |
| 3.1 | Fake probes and silent/recording sinks; no stream or physical sound in automated checks |
| 3.2 | README/compatibility documentation, Release build, scoped format, whitespace and full1045/1045 regression |
| 4.1 | Matched provider fixtures and mixed simultaneous-session cases through shared policy, QA-002/004 |
| 4.2 | Shared defaults/config/control/diagnostics and explicit accepted platform scope, independent QA/analysis evidence |

## Platform investigation checkpoint

- Microsoft documents that [IAudioSessionControl](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessioncontrol) covers shared-mode streams and does not work with exclusive-mode streams.
- [GetState](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol-getstate) reports running streams within that audio session, not global microphone idle.
- Developer investigation found no primary contract for CapabilityAccessManager registry activity timestamps; this is unverified coverage, not proof that all alternative detection is impossible.
- The user selected best-effort detection: resume speech when no observable capture stream is active, accepting that exclusive-mode use may be missed; RDP and probe failures remain Unknown. Native implementation resumed under this explicit choice.
