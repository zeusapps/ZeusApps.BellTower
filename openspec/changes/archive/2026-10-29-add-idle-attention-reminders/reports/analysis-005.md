# Analysis report: add-idle-attention-reminders / 005

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / resumed delegated review / `gpt-6.1-sol` (role default).
- Date: 2026-10-29; completes the interrupted 2026-10-28 target-004 review under a new reservation.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-005.
- Task and requirement references: ANALYSIS-004 / REGRESSION-001 affected fixture/scheduling acceptance only. No new optional scope.
- Scope: the two changed tray test files and unchanged source/queue neighbors used in prior bounded probes; complete 29-file manifest/hashes in [target-004](../evidence/review-target-004.txt).
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked SHA-256 `3c87f683b02f39b577fe5baf3bcba839737f38372acea7e56ab82daf171b3296`; 16 untracked files, aggregate `7001ad675d0d`.
- Before/after identity: exact full-output matches; `head=47dfca9 baseline=47dfca9 files=29 diff=3c87f683b02f untracked=16/7001ad675d0d`.
- Prior reports: [analysis-003](analysis-003.md), [QA-004](qa-004.md), [implementation-004](../evidence/implementation-004.md). Report 004 was not saved when automatic approval review could not complete because of the account usage limit; that consumed reservation is not reused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Scope and identity | Full target-003/004 and resumed before/after comparisons | [Evidence 005](../evidence/analysis-005.md) | Passed: only two test hashes differ; production/source/docs unchanged. |
| Healthy actual-wrapper paths | Prior adequate scratch probes: explicit test budget/prewarm, actual source/tray/queue, Claude and Codex | Same evidence; preserved ignored probe source | Passed; QA also records the corrected healthy fixture passing both full runs. No repeat after resumption. |
| Public default-budget safety | Prior synchronized held probe, old positive cache, actual source and tray predicate | Same evidence | Passed in bounded probes: unknown on timeout, old cache rejected, single-flight retained. Production budget remains 80 ms. |
| Official .NET contract | Successful find-docs/Context7 resolve and official `/dotnet/docs` query before interruption | [Primary links/provenance](../evidence/analysis-005.md#current-official-documentation-consulted-before-interruption) | Queueing/blocking-worker mechanism documented; no inference about exact QA-run causes. |
| Remaining setup coupling | Controlled process-local single-worker reproduction of synchronous entered wait | [Recorded probe](../evidence/analysis-005.md#remaining-setup-mechanism-and-concrete-alternative) | Reproduced: observer cannot start before setup gate fails; actual rejection assertion not reached. |
| Minimal required alternative | Async fixture completion, dedicated coordinator and eight test-owned consumers under the same constraint | Same evidence | Passed in scratch; not applied to repository tests. No proposed product or global pool tuning. |
| Broad regression | Read immutable QA-004, retaining both full failures and all isolated passes | [QA-004](qa-004.md) | Still failed: 1,084/1,086 then 1,083/1,086. Causes remain unknown. No analyzer full run. |
| Earlier verified requirements | Exact unchanged product identity and previous coverage mapping | [Carry-forward](../evidence/analysis-005.md#carry-forward-and-ownership) | Prior receipt/escalation/native/config/provider checks retained at their established scopes. |
| Integrity and own hygiene | Exact final identity comparison and report/evidence inspection | Reserved target and own outputs | Passed for analyzer scope. Broad hygiene/build/format remain QA-owned. |

## Findings

No new finding IDs allocated. The original stale-cache defect remains corrected. Healthy real-wrapper fixtures retain meaningful behavior with an explicit test-only budget and positive precondition, and separate checks still exercise the production public 80 ms rejection behavior.

ANALYSIS-004 full acceptance remains open: `ReminderPlaybackTests.cs:230` failed its synchronous observer-start gate in the second full run before reaching the stale-positive rejection assertion. Its isolated pass does not establish reliable full-suite coverage. The controlled reproduction establishes that such setup can occupy a worker needed by its own queued observer. The eight blocking Task.Run consumers at `WindowsUserIdleSourceTests.cs:145` also share pool resources with the held source probe. These are demonstrated/source-confirmed coupling mechanisms, not proof of the causes of QA's actual failures.

**Concrete required correction:** make setup nonblocking for the xUnit worker, coordinate the held real source on a dedicated test-owned driver, use test-owned concurrent consumers, and await fixture completion asynchronously. Release/join owned work before completing, including failure paths. Preserve the actual wrapper/tray path, public production 80 ms assertion, old-positive rejection and single-flight checks. Do not loosen generic waits, add retries, tune global ThreadPool settings or change production policy. The alternative passed under the same controlled constraint in ignored scratch code; the reviewer has not edited repository tests.

REGRESSION-001 remains open. QA-004's first full run failed two unchanged checks while the corrected healthy reminder passed; the second failed three checks including setup of the new timeout fixture. All failed methods passed once isolated. No exact full-run cause is established, and neither failed result is overruled by these bounded probes.

## Re-verification

| Finding ID | Target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-004 | `3c87f683b02f` / `7001ad675d0d` | Original safety remains corrected; healthy source/queue and synchronized default-budget probes pass; full-run setup gate remains unreliable | Still open for required fixture acceptance. |
| REGRESSION-001 | Same target | QA full results 1,084 pass/two fail and 1,083 pass/three fail; exact isolates pass | Still open; clean default suite not established. |
| Unaffected ANALYSIS-001/002/003/005 and matched/mixed policy cases | Product byte-identical to target-003 | Explicit unchanged-hash mapping and prior independent evidence | Prior verification retained at its documented scope. |

## Assessment and limitations

The next bounded step is a test-only coordination correction, followed by independent review and a clean QA default run. The user's continuation authorizes the orchestrator to route that work; this reviewer performs no implementation or orchestration.

Adequate earlier probes and documentation were preserved; after resumption only read-only checks and the newly reserved report/evidence writes were needed. No physical input/audio, native/RDP trial, real provider session, installation, network send, process control, broad rerun, commit or publication was performed. Full acceptance and completion remain with the orchestrator using the retained QA evidence.
