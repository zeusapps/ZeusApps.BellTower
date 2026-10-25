# Findings: fix-vscode-window-activation

## Current orchestrator

- Client: Claude Code (Opus 5.5, `claude-opus-5-5`), main session
- Since: 2026-10-25 (UTC)
- Previous owner: none (first apply)

Orchestrator-owned ledger. Baseline: `9b26088`. User authorized apply on 2026-10-25 ("yes, let's go with vs code"). Planning home and implementation root resolved by OpenSpec: `D:/Code/ZeusApps.BellTower`; change root: `openspec/changes/fix-vscode-window-activation`. Baseline identity for `vscode src tests`: `head=9b26088 baseline=9b26088 files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc`.

## Assignments and reservations

| Assignment | Role / owner | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- |
| Implementation 001 | developer | 1.1, 1.2, 3.1, 3.2; then 2.1–2.4, 3.3, 4.1, 4.3, 5.1, 5.2 if 1.2 proves an instance-bound mapping; baseline `9b26088` | `../evidence/implementation-001.md`, `../evidence/prototype-001-*`, scoped source/tests/docs | None | handed off 2026-10-25 (partial: 4.2 not delegated; developer limitations 2 lone-claim mapping risk, 6 doctor per-window gap); identity `head=9b26088 baseline=9b26088 files=27 diff=6146e35f8ede untracked=6/4ffcd8e5d034` (paths `vscode src tests docs/compatibility.md docs/installation.md docs/architecture.md`) confirmed by orchestrator |
| QA 001 | qa (Claude Code, `claude-sonnet-5-5`) | Target identity above | `qa-001.md`, `../evidence/qa-001-*`, optional `../screenshots/qa-001-*` | QA-001–QA-099 | completed 2026-10-25 (identity unchanged; QA-001…QA-004) |
| Analysis 001 | code-analyzer (Claude Code 2.1.310, `claude-opus-5-5`) | Same target as QA 001 | `analysis-001.md`, `../evidence/analysis-001-*` | ANALYSIS-001–ANALYSIS-099 | completed 2026-10-25 (identity unchanged; ANALYSIS-001…005; orchestrator saved text under write fallback) |
| Implementation 002 (fix round 1) | developer | ANALYSIS-001…005, QA-001…QA-004; target `diff=6146e35f8ede untracked=6/4ffcd8e5d034` | `../evidence/implementation-002.md`, scoped source/tests/docs | None | handed off 2026-10-25 (resumed once after a usage-limit stop); identity `head=8167c5d baseline=9b26088 files=32 diff=5443a2b9d46e untracked=9/80744c697558` confirmed by orchestrator (HEAD moved only by user commit 8167c5d touching another change's ledger) |
| QA 002 / Analysis 002 (recheck 1) | qa (`claude-sonnet-5-5`) / code-analyzer (Claude Code 2.1.310, `claude-opus-5-5`) | Implementation 002 handoff | `qa-002.md`, `analysis-002.md`, `../evidence/{qa,analysis}-002-*` | QA-100–QA-149, ANALYSIS-100–ANALYSIS-149 | completed 2026-10-25 on `diff=5443a2b9d46e untracked=9/80744c697558` (identity unchanged both; analyzer text saved by orchestrator under write fallback) |
| Implementation 003 (fix round 2, final cycle) | developer | ANALYSIS-100; user-included ANALYSIS-101, ANALYSIS-102, ANALYSIS-005 residual (ENOENT unlink log), QA-100; target `diff=5443a2b9d46e untracked=9/80744c697558` | `../evidence/implementation-003.md` | None | handed off 2026-10-25; identity `head=8167c5d baseline=9b26088 files=33 diff=3a6df874e2c9 untracked=9/7e8916580128` confirmed by orchestrator |
| QA 003 / Analysis 003 (recheck 2, final) | qa / code-analyzer | Implementation 003 handoff | `qa-003.md`, `analysis-003.md`, `../evidence/{qa,analysis}-003-*` | QA-150–QA-199, ANALYSIS-150–ANALYSIS-199 | completed 2026-10-25 on `diff=3a6df874e2c9 untracked=9/7e8916580128` (identity unchanged both; no new QA findings; ANALYSIS-150; analyzer text saved by orchestrator under write fallback) |
| QA 004 (task 3.3 check) | qa | Isolated VSIX install on `diff=3a6df874e2c9 untracked=9/7e8916580128` | `qa-004.md`, `../evidence/qa-004-*` | QA-200–QA-249 | completed 2026-10-25 (identity unchanged; no findings; isolated setup and direct CLI install verified) |

Task 4.2 (live Windows 11/RDP acceptance) needs the user's real editor windows and is not delegated; it stays unverified until performed. Its checklist now includes: window behind another app; minimized; visible maximized/fullscreen; two windows with duplicate terminal names; a floating (auxiliary) editor window and its doctor state (ANALYSIS-003, ANALYSIS-100); and whether `WindowState.active` can rise on a background window via scroll/hover (ANALYSIS-103). No publication, installed-app or VS Code extension changes on the user's install, production provider writes, audio playback or process termination is authorized by this apply. Untracked handoffs and other queued changes predate apply and must be preserved.

## Summary

Recheck 2 (final cycle) completed 2026-10-25. All fix-round findings verified in fixtures by independent reports; ANALYSIS-003 live consequence and ANALYSIS-103 remain with task 4.2. Fix-and-recheck cycle count: 2 of 2 (no further fix rounds without user direction).

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 1.2, 2.1, 2.3; Exact VS Code terminal selection | warning | blocking | verified (residual event lag >~30 ms by design; then ANALYSIS-100 applies) | orchestrator | Analysis 002 probes A, C, E |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 1.2, 3.2 | suggestion | user asked to include (2026-10-25) | verified | orchestrator | Analysis 002 + QA 002 |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 1.2, 2.3, 4.2 | warning (hypothesis) | blocking pending live probe | deferred (fail-safe guard verified in fixtures; floating-window consequence not separately checked live; user accepted 4.2) | — | Analysis 002 probe D; QA 002 |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 3.1, 3.2 | suggestion | user asked to include (2026-10-25) | verified (share mode does not prevent Node rename EPERM; snapshot is the mitigation; no rename retry recommended) | orchestrator | Analysis 002 probe set 2 |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 3.1, 3.2 | suggestion | user asked to include (2026-10-25) | verified (ENOENT residual fixed in Impl 003) | orchestrator | QA 003 + Analysis 003 |
| QA-001 | [qa-001](qa-001.md) | 4.1 | warning | blocking | verified (real foreground grant substituted by recorded denial, honestly labeled; real grant → 4.2) | orchestrator | QA 002 |
| QA-002 | [qa-001](qa-001.md) | 2.4 | warning | blocking | verified (production-branch duplication tracked as ANALYSIS-101) | orchestrator | QA 002 |
| QA-003 | [qa-001](qa-001.md) | 1.2 | suggestion | user asked to include (2026-10-25) | verified in fixtures (activity semantics tracked as ANALYSIS-103) | orchestrator | QA 002 + Analysis 002 probes F, G |
| QA-004 | [qa-001](qa-001.md) | 5.2 | warning | blocking | verified (dishonest reason for guard state tracked as ANALYSIS-100) | orchestrator | QA 002 |
| ANALYSIS-100 | [analysis-002](analysis-002.md) | 1.2, 5.2 | warning | blocking (diagnostics give an unworkable remedy) | verified | orchestrator | QA 003 + Analysis 003 |
| ANALYSIS-101 | [analysis-002](analysis-002.md) | 2.4 | suggestion | user asked to include (2026-10-25) | verified | orchestrator | QA 003 + Analysis 003 |
| ANALYSIS-102 | [analysis-002](analysis-002.md) | 5.2 | suggestion | user asked to include (2026-10-25) | verified | orchestrator | QA 003 + Analysis 003 |
| ANALYSIS-103 | [analysis-002](analysis-002.md) | 1.2, 4.2 | suggestion (hypothesis) | non-blocking; probe added to 4.2 checklist | deferred (reconsider if 4.2 shows `active` rising without foreground input) | user (4.2) | — |
| QA-100 | [qa-002](qa-002.md) | 5.2 | suggestion | user asked to include (2026-10-25) | verified | orchestrator | QA 003 + Analysis 003 |
| ANALYSIS-150 | [analysis-003](analysis-003.md) | 5.2 | suggestion | non-blocking | deferred (follow-up: reuse `AttachmentHealth` confirmed-ended rule in the doctor reporter; no runtime effect today) | — | — |
| LIVE-001 | [live-4.2](../evidence/live-4.2.md) | 1.2 (QA-003) | warning | non-blocking per user acceptance | deferred (follow-up: identify a window focused at reload without a later focus change) | — | — |
| LIVE-002 | [live-4.2](../evidence/live-4.2.md) | 3.2 | suggestion | non-blocking | deferred (bounded by sweep; investigate why overlapping-click receipts were not consumed) | — | — |
| LIVE-003 | [live-4.2](../evidence/live-4.2.md) | 5.2 | suggestion | non-blocking | deferred (follow-up: per-click navigation diagnostics codes) | — | — |

## History

- 2026-10-25: Ledger created; Implementation 001 assigned.
- 2026-10-25: QA 001 / Analysis 001 triaged. User asked to include ANALYSIS-002, -004, -005 and QA-003 in fix round 1. Implementation 002 assigned; resumed once after a usage-limit stop.
- 2026-10-25: QA 002 / Analysis 002 triaged; round-1 findings verified except ANALYSIS-003's live consequence. ANALYSIS-100 gated for fix round 2 (final cycle).
- 2026-10-25: User asked to include ANALYSIS-101, ANALYSIS-102, the ANALYSIS-005 ENOENT log residual and QA-100 in fix round 2. Implementation 003 assigned.
- 2026-10-25: QA 003 / Analysis 003 (final recheck) triaged: ANALYSIS-100, -101, -102, the ANALYSIS-005 residual and QA-100 verified; no regressions. ANALYSIS-150 deferred. Task 3.3 has only developer evidence (QA 001 did not re-execute the VSIX install), so QA 004 is assigned for that check alone; it is not a fix round.
- 2026-10-25: QA 004 verified task 3.3 independently (isolated `setup --desktop --apply` with disposable home and data dir, plus a direct CLI install; bridge files hash-identical to source; ownership rule kept an unowned extension; reload notice present; no process started, stopped or restarted). The full NativeAOT `package.ps1` was not run.

## Completion assessment (2026-10-25)

- Tasks: 14/14. Task 4.2 accepted by the user on 2026-10-25 from a user-performed live run (`../evidence/live-4.2.md`); minimized, fullscreen, duplicate-name, floating-window and activity sub-checks were not separately reported and remain unverified live.
- No open blocking finding apart from ANALYSIS-003's live consequence. Deferred suggestions: ANALYSIS-103 (pending 4.2), ANALYSIS-150.
- 2026-10-25: Dev build 0.1.9011 switched and setup applied with user authorization. User performed live 4.2 and accepted it; LIVE-001..003 recorded as follow-ups. Change complete for commit.
- Commit: `54f0ab3` on `add-claude-repository-subagents` (implementation identity `diff=3a6df874e2c9 untracked=9/7e8916580128` at commit time). Not pushed or archived.
