# Analysis report: suppress-voice-during-microphone-use / 003

## Review target

- Reviewer / role: microphone_analysis / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / delegated default-mode recheck / gpt-6.1-sol, high reasoning (role default).
- Date: 2026-10-28.
- Change root / assignment: `openspec/changes/suppress-voice-during-microphone-use` / ANALYSIS-RECHECK-003.
- Task and requirement references: QA-021; task 1.3 freshness verification and task 3.2 required regressions.
- Scope: test-only synchronization correction in `tests/BellTower.Tests/Tray/AudioQueueTests.cs`, specifically CachedInactiveExpiresFromOriginalObservationTimeAndFreshScanRecovers at lines 573-607. No new suggestions or broader implementation review.
- Baseline / HEAD: `7ddef11527676676b3808fb23adf28a68c9930b4`.
- Prior reports: [analysis-002.md](analysis-002.md), [qa-002.md](qa-002.md).
- Identity command before and after: `./scripts/review-identity.ps1 -Baseline 7ddef11 -Paths src tests README.md docs/compatibility.md`.
- Before/after identity: identical; no implementation mutations by this reviewer.

```text
Summary: head=7ddef11 baseline=7ddef11 files=15 diff=0c7e59dec84c untracked=4/19783d7998c7
Tracked diff SHA-256: 0c7e59dec84c5860cd3aec9a2d7cf359c2c0d41c1e0b6b84689da9cc094de054
AudioQueueTests.cs SHA-256: C35C5C7CD50F8E7E12C6FBA25CC7C038E8F5206B85A2260C8ACF22772D62AFF7
```

The 15-file manifest is unchanged from the earlier reports. All four untracked hashes remain those in analysis-002, including native source `872ce2daf1cbb7bc0d421d0154f0628368d202c28596c0bfcd2968983d39fa73`. The orchestrator's scoped handoff identifies only the above test file as changed since target `ec32fe6ba6f3` / `19783d7998c7`.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Exact target | Review identity before/after; SHA-256 of AudioQueueTests.cs | Matched assigned manifest and test hash | Passed |
| Prior QA-021 count race | Inspect new firstStarted/secondStarted barriers and call-count assertion | Replacement probe signals secondStarted after incrementing calls; the test waits for that signal before asserting calls=2 | Corrected for the originally observed calls=1 assertion |
| Initial freshness window | Inspect ordering of initial GetSnapshot, scan timestamp, firstStarted, 800 ms sleep and first Inactive assertion | The initial GetSnapshot can consume 150 ms of the one-second freshness interval before returning. Then the test sleeps another 800 ms | Still scheduling-sensitive; only roughly 50 ms remain for publication and consumption |
| Controlled delayed-consumer reproduction | Reflection fixture over the existing built WindowsMicrophoneActivitySource; inject first and replacement incomplete tasks, publish a complete Inactive result with its original timestamp; preserve initial 150 ms bounded wait and 800 ms sleep, then delay the consumer by 100 ms | Initial Unknown; result consumed at 1091 ms age; source returned Unknown with stale-evidence reason. A subsequent 2045 ms polling interval with replacement probe held never observed Inactive | Failed the test's intended first-Inactive condition while production behavior remained correct. This is a controlled scheduling model using the actual source, not a claim that the unmodified xUnit test failed again in a default suite |
| Product behavior regression | Re-read unchanged cache freshness branch; map unchanged product source to analysis-002 evidence | Returning Unknown for the expired result is the required behavior; the test correction did not change it | Passed source review; six earlier product fixes retain prior evidence |
| Diff cleanliness | `git diff --check` | No whitespace errors | Passed |
| Full suite / focused tests / build / formatter | Not repeated | QA owns the full default suite; developer reports 41 queue tests, build and formatting passed | Not performed by analyzer; reported successes do not prove absence of scheduling sensitivity |
| Hardware or installation | No native metadata probe, microphone stream, physical sound or installed-state operation repeated | Prior safe metadata smoke is carried forward only for unchanged source | Not performed in this round |

## Findings

No new finding IDs allocated. The existing QA-021 is only partially corrected.

### QA-021 recheck: first-Inactive timing race remains

- Severity / disposition reference: existing blocking warning; see [QA-002](qa-002.md) and the orchestrator ledger.
- Related tasks: 1.3 and 3.2.
- Location: `tests/BellTower.Tests/Tray/AudioQueueTests.cs:597`, especially the 800 ms sleep at 599 and first Inactive polling assertion at 601.
- Corrected portion: explicit start events remove the assumption that the replacement ThreadPool delegate has run before the call-count assertion at 605.
- Remaining observation: firstStarted is set inside the delegate immediately after recording ObservedAt, but the test waits for it only after the initial GetSnapshot finishes its bounded wait. When the delegate starts promptly, that initial call uses approximately 150 ms of the one-second validity period. The subsequent 800 ms sleep leaves approximately 50 ms to release the scan and observe Inactive. A later firstStarted wait cannot restore that elapsed validity time. A modest consumer scheduling delay can expire the result before the first Inactive assertion observes it.
- Executed evidence: a reflection fixture reproducing that ordering against the actual source, with a controlled additional 100 ms consumer delay, observed the result at age 1091 ms. The source correctly returned Unknown/stale. Holding the replacement probe, as this test does until its later phase, kept the first-Inactive condition false for 2045 ms. No stale Inactive product output was observed.
- Impact: the new barrier fixes one manifestation of nondeterminism but does not make the test's positive near-expiry observation robust under load. A passing focused or full run remains useful evidence of that run, not proof that this scheduling defect is removed.
- Recommendation for an explicitly authorized further correction: use a narrow injectable clock with production UTC as its default, synchronize probe starts/completions, and advance fixture time explicitly for fresh/stale/recovery phases. This also avoids depending on elapsed sleeps in the delayed-scan companion test. Such a change expands the latest test-only implementation assignment and requires the orchestrator's/user's decision and affected reviews.
- Minimal alternatives and risks: a test-only reflection fixture can seed the cache and controlled query state and directly assert the fresh/expired branches, preserving public production behavior but coupling the test to private field names. Merely shortening the 800 ms sleep widens the margin but retains load-sensitive wall-clock assumptions. Accepting a successful suite while retaining this known sensitivity is an explicit deferral decision, not verification that QA-021 is fixed.
- Evidence type: source ordering plus executed controlled scheduling fixture. No real-world scheduling frequency or performance benchmark is claimed.

## Re-verification

| Finding ID | Corrected target / mapping | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-021 | New target `0c7e59dec84c` / `19783d7998c7` | New barriers fix calls=1 race; controlled delayed-consumer reproduction still prevents first fresh-Inactive observation | Partially corrected; scheduling defect remains |
| ANALYSIS-001 through ANALYSIS-003 | Product source unchanged from `ec32fe6ba6f3` / `19783d7998c7` to current test-only target | Carry forward analysis-002 compiled ABI/raw-state assertions and successful safe outside-sandbox metadata enumeration | Prior product corrections remain verified; not re-executed here |
| ANALYSIS-004 | Same unchanged native source hash | Carry forward analysis-002 stale-cache reproduction; this round's controlled expired result again returns Unknown correctly | Product correction remains verified; QA-021 is a distinct fixture issue |
| ANALYSIS-005 | Unchanged WindowsAudioSink in scoped handoff | Carry forward analysis-002 production helper inspection and silent initialization-transition zero-Speak fixtures | Prior correction remains verified; not re-executed here |
| ANALYSIS-006 | Unchanged native source hash | Carry forward analysis-002 1024-control admission/release fixture and overflow-to-Unknown inspection | Prior correction remains verified; not re-executed here |

## Assessment and limitations

The final test-only patch addresses the reported replacement-start count assertion, but a concrete first-observation timing race remains under QA-021. This report does not reopen the six independently verified product fixes or infer a production freshness failure. The orchestrator must decide how to handle the remaining fixture issue after the two authorized correction rounds; no implementation or additional correction was performed by this reviewer.

Independent QA owns the full-suite outcome on this exact target. No competing build/test suite or hardware probe was run, and no source, task, ledger or historical report was edited.
