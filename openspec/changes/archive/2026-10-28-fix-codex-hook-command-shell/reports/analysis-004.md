# Analysis report: fix-codex-hook-command-shell / 004

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 local tooling; delegated Default-mode recheck; role-configured `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`; resumed final recheck 004. Report 003 was not produced because of the prior usage quota interruption. Reserved new regression IDs: ANALYSIS-150 through ANALYSIS-199; none used.
- Requirements: tasks 4.1/4.2 and relevant 2.1/2.2/3.2 safety regressions; [tasks](../tasks.md), [ordering delta](../specs/session-tracking/spec.md), [design](../design.md).
- Scope: ANALYSIS-050 and directly related correlation/replay regressions, retaining the specific ANALYSIS-001/002/003 correction mapping. No new suggestions or performance/architecture investigation.
- Reviewed neighbors: shared Core reducer and scope identity; affected Core and provider test fixtures; unchanged native probe and delivery reporter. Full identity scope includes `src`, `tests`, `scripts`, `tools/BellTower.Ingress.Bench` and the four assigned documentation files.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior immutable reports: [analysis-001](analysis-001.md), [analysis-002](analysis-002.md). Handoff: [implementation-003](../evidence/implementation-003.md). Native evidence: [investigation 001](../evidence/native-investigation-001.md), [investigation 002](../evidence/native-investigation-002.md).

Identity command before and after analysis:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests scripts tools/BellTower.Ingress.Bench docs/hook-ingress.md docs/installation.md docs/compatibility.md docs/follow-ups.md
```

Manifest:

```text
? src/BellTower/Diagnostics/HookDeliveryReporter.cs
? src/BellTower/Setup/ProviderHookProbe.cs
? tests/BellTower.Tests/Diagnostics/HookDeliveryReporterTests.cs
M docs/compatibility.md
M docs/follow-ups.md
M docs/hook-ingress.md
M docs/installation.md
M src/BellTower/Core/SessionReducer.cs
M src/BellTower/Diagnostics/CapabilityMatrix.cs
M src/BellTower/Diagnostics/DiagnosticsReporter.cs
M src/BellTower/Setup/HookCommandBuilder.cs
M src/BellTower/Setup/IngressProvision.cs
M src/BellTower/Setup/IntegrationSetup.cs
M tests/BellTower.Tests/Core/PermissionNotificationTests.cs
M tests/BellTower.Tests/Core/ReducerTests.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Providers/CodexPermissionTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `262b223e8a797fac30321c9ebbd0462b0f2bdb35e6b2f965a2772a2eef8e46e5`.
- Untracked SHA-256: reporter `fd6b6933dd692beaaf33cbb741c10e84fd296d5d8b529c78baf915d87008004d`; native probe `8eb90d45ead52c7caa8e94c4fa33db5fb48d7ee366a8f4062fa1f2cb17656f24`; reporter tests `e0b800afc786482cffbd4acbb6419c324924201fee5a4ad451b52c37829481fb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=19 diff=262b223e8a79 untracked=3/1b89a893996b`; matched. These three untracked files are byte-identical to analysis 002. Report/evidence writes are outside the implementation identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correlation | Inspect `SessionReducer.cs:218`, `:243`, `:369`; rerun independent disposable Core probe | Four subagent and eight unrelated-main combinations preserve the wait; captured Codex wait survives the causal window plus 40 starts | Passed; [executed evidence](../evidence/analysis-004-reproductions.md) |
| Correlated completion and scope | Inspect candidate pruning and permission-scope comparison; execute affected test classes | Candidate starts keep membership/revision; results narrow membership and invalidate old runtime reads; every candidate must complete | Passed |
| Fixture meaning | Compare old/new expectations against the accepted ordering delta | Matching starts retain permission and separate Question; grace remains silent without confirmation; correlated results resolve; backdated restoration remains silent and actionable | Passed; changes preserve the independent safety assertions rather than accepting unrelated starts as resumption |
| Provider parity and replay | Execute `CaptureTests`, `PermissionNotificationTests`, `HumanWaitCorrectionTests` and related reducer/provider cases | Both providers, normalized/captured paths, candidate/direct waits, shuffled/duplicated delivery and mixed bounded replay | Passed within fixtures; not user-operated native acceptance |
| Receipt safety | Execute `HookDeliveryReporterTests` and independent wrong-kind calls | Matched-provider malformed receipts remain unverified, bytes preserved, private marker/command omitted; valid `at` and `At` remain supported | Passed |
| Native config encoding | Verify unchanged probe hash and inspect Tomlyn table construction | Same fully serialized special-character project key correction verified in analysis 002 | Passed carry-forward mapping; native parser/delivery not rerun by analyzer this round |
| Focused tests | Six affected class predicates, Release, `--no-build --no-restore` | 181 passed, zero failed/skipped outside sandbox | Passed; initial sandbox attempt stalled and was canceled without a diagnostic, so no new IPC cause is asserted |
| Package integrity | `Get-FileHash` for ignored 0.1.9015 ZIP | `B6F4A1C1184190C4527C3423DF3146E4EA4335AE17CFC25C88971C0601A12488` | Passed against handoff; package build source binding is developer-recorded, no installation |
| Whitespace / output hygiene / identity | `git diff --check`; scan assigned report/evidence; before/after identity command | No whitespace errors, no personal/secret patterns found, matching implementation identity | Passed |
| Build / formatter / full suite / native probe / benchmark | Not repeated by analyzer | Developer evidence and independent QA cover broader execution; no performance code changed in this correction | Not performed here; results are not presented as analyzer-executed acceptance |

## Findings

**No new findings.** No IDs in ANALYSIS-150 through ANALYSIS-199 are assigned. The remaining release evidence gaps below retain their existing ownership and are not new code defects.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-050 | This report's identity | Eight original unrelated-main cases now retain the wait for both providers, both reducer paths and equal/different fingerprints. Correlated result/candidate completion, stale runtime evidence, replay-window preservation and silent restoration pass the focused tests | Verified correction; orchestrator owns status transition |
| ANALYSIS-001 | This report's identity | Immediate main-thread guard remains; original four subagent reproductions pass; direct/candidate provider fixtures now also preserve waits on unrelated foreground starts | Verified specific subagent correction, with A050 regression corrected |
| ANALYSIS-002 | Unchanged probe SHA-256 mapped above | Analysis 002 native parser acceptance plus unchanged Tomlyn construction and special-character probe root inspected here | Verified by explicit carry-forward; no new native delivery run claimed |
| ANALYSIS-003 | Unchanged reporter/tests SHA-256 mapped above | Wrong-kind calls return safely; current matched-provider receipt tests verify unchanged bytes, sanitized output and timestamp compatibility | Verified with current executed regression coverage |

## Assessment and limitations

The final bounded correction addresses ANALYSIS-050 in shared policy. ToolStart does not establish approval of an attributed or candidate wait. The existing later foreground-activity fallback remains for a wait with no operation ID, candidates or fingerprint. ToolEnd supplies correlation, and candidate membership changes participate in approval revision/scope so stale native-status evidence cannot confirm the narrower wait. Prompt/stop/end behavior remains covered by the focused fixture classes. No extra provider policy branch or abstraction was introduced.

The previously obsolete fixture expectations were corrected on requirement grounds: preserve the permission and unrelated Question through a start; keep unconfirmed grace-period requests silent; require every ambiguous candidate's result; and preserve unrelated-identity and backdated restoration safety checks. The delayed native-status fixture retains rejection of the old status read and acceptance of a fresh scoped read after a result changes membership.

The reviewed native evidence now establishes an actual Claude Code 2.1.310 SessionStart delivery through configured Git Bash, and Codex 0.164.0 rejection of the tested command-array shape. Version-pinned source explains inherited turn-shell execution and Windows PowerShell/default-to-cmd fallback, with COMSPEC used only when no shell program is supplied. Exact executable resolution/fallback branches and other exec shapes remain unverified. These evidence records are reviewed source/probe reports, not analyzer-executed native checks, and prove no universal provider restriction. The tested product PowerShell path is supported; native verification blocks settings writes on a failing selected shell.

Release acceptance remains incomplete: task 2.3's recorded Codex median is 385.4529 ms against the unwaived below-300 ms gate, and task 5.2's packaged user-operated post-migration/mixed-session/trust acceptance is pending. The no-op comparison suggests startup contribution but does not measure per-call components. No new benchmark, budget exception, migration, installation or user acceptance was performed or authorized by this report. These remaining blockers must stay visible even though the assigned code corrections and focused tests pass.

Only these assigned report/evidence files and ignored disposable analysis outputs were written. The source target stayed frozen. The orchestrator owns triage, ledger status and the final completion decision.
