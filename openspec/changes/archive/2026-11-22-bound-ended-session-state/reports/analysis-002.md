> Saved verbatim by the orchestrator. Reviewer: code-analyzer subagent (ANALYSIS-002), Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`. The client refused the reviewer's native report write (known restriction, see docs/agent-workflow.md#known-client-restrictions); the reviewer returned the text inline.

# Analysis report: bound-ended-session-state / ANALYSIS-002 (recheck of fix cycle 1)

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-002) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-21T13:28Z
- Change root / assignment: `openspec/changes/bound-ended-session-state` / ANALYSIS-002 (recheck)
- Task and requirement references:
  - tasks 1.1, 1.2, 1.4, 1.5 and 2.1 in [tasks.md](../tasks.md)
  - design decisions 1, 3 and 4 in [design.md](../design.md)
  - requirement "Bounded monitoring state" in [specs/session-tracking/spec.md](../specs/session-tracking/spec.md):
    - scenario "Resumed session after compaction"
    - scenario "State at the budget recovers"
    - "without one entry per tick"
  - developer evidence [implementation-002.md](../evidence/implementation-002.md)
- Scope and relevant neighboring paths:
  - DEV-002 changes:
    - `src/BellTower/IO/RefreshHealth.cs`, `LocalLog.cs`, `StateStore.cs`, `StateSnapshotStore.cs`
    - `src/BellTower/Core/CausalSessionReducer.cs`
    - `tests/BellTower.Tests/IO/BoundedStateTests.cs` and `RefreshHealthTests.cs`
    - `docs/architecture.md` and `docs/hook-ingress.md`
  - Neighbors read:
    - `src/BellTower/Providers/CaptureConsumer.cs` (`Prepare`, `BatchSize`)
    - `src/BellTower/Core/SessionReducer.cs` (`Apply`, `ApplyUnordered` lines 37-63)
    - `StateStore.ReplayInbox` and `RetainPendingReceipts`
    - `src/BellTower/Tray/TrayApplication.cs` (timer and `Tick`)
- Baseline commit: 5e95554
- Implementation identity (before): `head=5e95554 baseline=5e95554 files=22 diff=46553ba0c8c5 untracked=7/0e4c8db2fb3f`
  - Tracked diff SHA-256: `46553ba0c8c528cd208849ccf393e753dabebe9727749cb6a1ba18e21e1c630a`
  - Untracked: `RefreshHealth.cs` 99dfa6ea…, `BoundedStateTests.cs` 184bce51…, `RefreshHealthTests.cs` bf5caea6…; the other untracked hashes are as in the manifest
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=46553ba0c8c5 untracked=7/0e4c8db2fb3f`. It is identical to the before identity and to the expected summary, so there was no unexpected mutation.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md): ANALYSIS-001, ANALYSIS-002, ANALYSIS-003 and ANALYSIS-006

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Probe setup | Product sources (`src/BellTower`, `BellTower.Protocol`, `BellTower.Distribution`, without `bin`/`obj`) copied to a scratch directory outside the repository. A scratch console referenced the copy, was built with `dotnet run -c Release` and ran against scratch data directories. | Runs the DEV-002 code itself: the real `StateStore.Drain`, `CaptureSpool`, `CaptureFactory` and `RefreshHealth.Describe`. | No repository build, no real data directory, no tray. Nothing in the probe is committed. |
| ANALYSIS-001 sanitizer | Probe of `RefreshHealth.Describe` with synthetic profiles and user names | A profile with a space becomes `'<path>'` when quoted and `<path>` when unquoted. A UNC path, `/home/<a b>` and a data-dir file are masked. A profile with an apostrophe leaks the rest of the name and path (ANALYSIS-100). | Mostly fixed; ANALYSIS-100. |
| ANALYSIS-001 over-masking | Same probe | `'<data>\state.json'` and `'<data>\captures-v1\…'` both become `'<data>'`. An unquoted path swallows the text up to the last separator on the line. A one-letter user name masks the article "a". The exception type and the .NET message frame survive in every probe. | The loss of diagnostic detail is acceptable and errs toward masking. Not a defect. |
| ANALYSIS-002 rate limit | Reading `Failed`/`Remember`; probe on a frozen fake clock, 90 ticks of 250 ms | 8 rotating reasons gave 8 lines; 9 rotating reasons gave 90 lines (one per tick). The record write is bounded to once per interval. | The map is bounded at 8 entries, but output is not bounded for every order of reasons (ANALYSIS-102). |
| ANALYSIS-003(a) predicate | Reading `HasBacklog`, `Prepare`, `ReplayInbox` | `Prepare` takes the first 128 `*.json` files of the same enumeration. It deletes each rejected file at once and each accepted file after the save. An `IOException` aborts the whole drain. `ReplayInbox` takes 2000 sorted files and either accepts or deletes each one. So "more than 128 capture files, or more than 2000 inbox files, before the drain" means exactly "this drain cannot take every file". | The predicate is correct. |
| ANALYSIS-003(a) indefinite deferral | Same reading | A backlog persists only if files are never consumed. A refused save keeps every capture, but a refusal means live sessions alone exceed the budget, which compaction cannot fix. Stuck inbox files whose deletion failed with an `IOException` would need more than 2000 of them. Meanwhile the 200 cap and the 30-day age run every drain, and the budget fallback still bounds the state. | Indefinite deferral needs an already-refused state, and the state stays bounded. But a backlog drain at the budget removes sessions that compaction would keep (ANALYSIS-103). |
| ANALYSIS-003(b) floor | Reading `Compact`, the causal floor check and `ApplyUnordered`; drain probe for both providers | A delayed earlier event is ignored on both reducer paths, unchanged from analysis-001. A stray post-end event before the first compaction: the floor is at End, and the DEV-002 test proves the session revives. A stray event after compaction: the same drain compacts a journal with no End, the floor moves to the stray event, and a resume timed before it is dropped (ANALYSIS-101). A resume delivered before the stray event: the session goes live, for both providers. | Partially fixed; ANALYSIS-101. |
| ANALYSIS-006 tests | Reading the tests | `ResumedSessionAfterCompaction…(newAttachment: true)` sends a `Register` with `attachment-resumed` to a compacted `attachment-settled` session. This takes `ApplyUnordered` lines 37-57 through the causal replay, and line 188 asserts the new attachment. `AQuietSecondDrain…` sets the write time to 2020, then asserts identical bytes and time after a third drain. It covers compacted ended and live sessions of both providers. | Verified. |
| Regression: per-tick backlog check | Reading the `Tick` cadence (`Interval = 250`, one `Drain` per tick); scratch timing of the same two enumerations (`Take(129)` / `Take(2001)`), 2000 iterations | About 70-78 µs per tick for 0-200 files and 123 µs for 3000 files, on this machine with a warm cache. That is under 0.05 % of one core at 4 ticks/s. The enumeration stops at 129 or 2001 entries, and `Prepare`, `ReplayInbox` (full sort) and `RetainPendingReceipts` already enumerate the same folders. | Negligible next to the per-tick `state.json` decode. Not measured on the user's installation or under antivirus. |
| Docs | Reading the DEV-002 additions | `docs/architecture.md` says a later resume "revives it from the compacted record and its following events are ordered and tracked normally". The tray-log paragraph says "alternating reasons are also bounded". | Both statements are contradicted, by ANALYSIS-101 and ANALYSIS-102. |
| Parity | Probes and tests | Every probe and new test ran for Claude Code and Codex. No provider branch was added. | Parity holds. All the reducer and store findings affect both providers equally. |
| Format / build / suite | Not run in the repository | Assignment constraint: QA runs the build and the suite concurrently. | Not run. |

## Findings

### ANALYSIS-100: A profile or user name containing an apostrophe leaks the rest of the path

- Severity: warning
- Related task / requirement: 2.1; design decision 4; spec "a bounded, sanitized reason"; the ANALYSIS-001 fix
- Locations: `src/BellTower/IO/RefreshHealth.cs`
  - `:120`: the quoted-segment rule runs first
  - `:121-135`: the profile and user-name rules run after it
  - `:161`: `Quoted` uses `[^'\r\n]`, so a segment ends at the first apostrophe
- Observation:
  - The quoted-segment rule runs before the profile and user-name rules, and it ends a segment at the first apostrophe.
  - Probe with profile `C:\Users\O'Brien` and user name `O'Brien`:
    - input: `Access to the path 'C:\Users\O'Brien\.codex\sessions\x.jsonl' is denied.`
    - output: `IOException: Access to the path '<path>'Brien\.codex\sessions\x.jsonl' is denied.`
  - The profile and user-name rules can no longer match the split text. So part of the surname and the provider path go to `bell-tower.log`, `refresh-health.json` and the doctor output.
  - Windows account names may contain an apostrophe, and the profile folder follows the account name.
- Proposed improvement and benefit:
  - Run the exact profile rule and the user-name rule before the quoted-segment rule; the data-root rule stays first. With the profile rule first, its `PathTail` stops at the closing quote and the result is `'<path>'`.
  - Add a `RefreshHealthTests` case with a synthetic profile and user name that contain an apostrophe.
- Behavior-preservation risks: none for the existing cases. The profile rule is exact and bounded to whole path components, so the existing 12 cases keep their expected output.
- Evidence type: measured result (scratch probe of the DEV-002 `Describe`)
- Verification / measurement plan: the unit case above, asserting that no backslash and no fragment of the name survives.
- Relationship: residual of ANALYSIS-001

### ANALYSIS-101: Re-compaction after a stray post-end event moves the floor past End and drops an out-of-order resume

- Severity: warning
- Related task / requirement: 1.1, 1.2; spec scenario "Resumed session after compaction" and "SHALL NOT … prevent a resumed session from being tracked"; the ANALYSIS-003(b) fix
- Locations:
  - `src/BellTower/Core/CausalSessionReducer.cs:95-96`: with no End in the journal, the floor is the latest observation
  - `src/BellTower/Core/CausalSessionReducer.cs:18`: the floor check
  - `src/BellTower/Core/SessionReducer.cs:37-63`: an ended session ignores non-registration events without advancing `LastObserved`
  - `src/BellTower/IO/StateStore.cs:98` and `:251`: the same drain compacts again, because `LastObserved` is still the old End time
- Observation:
  - Take a compacted session: empty journal, floor at End. The resumed client's first event S (for example the prompt) is drained one tick before its resume registration R, with `R.At < S.At`:
    1. S enters a new one-entry journal and is ignored, because the session is ended.
    2. `LastObserved` stays at the old End, so the same drain compacts again.
    3. That journal has no End, so the floor becomes S. R is then at or before the floor and is dropped.
  - Every later event of the resumed session passes the floor, is ignored, and moves the floor forward again.
  - Probe, both providers, real `Drain` with captures:
    - S then R: `ended=True` after every drain. The floor walked from `End` to `Prompt@S` to `Prompt@later`, and the later prompt was not tracked.
    - R then S: live, with `turn-later` tracked.
  - This needs no long delay. R's capture only has to land after S's across a 250 ms drain boundary. The causal reducer exists for exactly this reordering, and a prompt passed on the resume command line makes the two hooks nearly simultaneous.
  - The baseline, which has no compaction, replays R before S and revives the session. DEV-001 had the same defect; DEV-002 fixed only the first compaction.
- Proposed improvement and benefit:
  - In `Compact`, keep the existing floor when the journal has no End and the session was already ended before it (its checkpoint is ended, so the replay applied nothing).
  - Equivalently: set the floor to the latest journal observation with `At <= LastObserved`, or keep the existing floor when there is none. This also covers sessions ended by attachment evidence.
  - Add a theory for both providers: compact, drain S, then drain R with `R.At < S.At`. Assert the session is live and a later prompt is tracked.
  - Correct the `docs/architecture.md` sentence once fixed.
- Behavior-preservation risks:
  - The floor never moves backward, so delayed earlier events stay ignored.
  - A duplicate stray event that passes the kept floor is ignored again and then compacted again. This is bounded and causes no save loop, because compaction only follows a drain that accepted the event.
- Evidence type: measured result (scratch probe of the DEV-002 code)
- Verification / measurement plan: the theory above, plus a mutant that restores "latest observation" to show the theory kills it.
- Relationship: residual of ANALYSIS-003(b)

### ANALYSIS-102: Nine or more rotating, or ever-changing, failure reasons still log one entry per tick

- Severity: warning (low likelihood)
- Related task / requirement: 2.1; spec "without one entry per tick"; design decision 4; the ANALYSIS-002 fix
- Locations: `src/BellTower/IO/RefreshHealth.cs:54-59` and `:92-99` (`Remember` evicts the oldest entry whatever its age)
- Observation:
  - The map is bounded at 8 entries, but eviction ignores age. With 9 reasons in rotation, each new reason evicts the one that returns next, so every failure logs.
  - Probe: 90 lines in 90 ticks (22.5 s) with 9 reasons, against 8 lines with 8 reasons.
  - The same happens for any reason whose text changes on each failure, such as a count, identifier or position outside a path. The sanitizer collapses paths, not those values.
  - The record write is correctly bounded to at most once per interval.
  - The DEV-002 test `ManyDistinctReasons…` asserts that each distinct reason logs. It does not bound the total.
- Proposed improvement and benefit:
  - Evict only entries older than `RepeatInterval`. When the map holds 8 fresh entries, either suppress the new reason or log one `tray-refresh-failed: further reasons suppressed` line per interval.
  - This bounds output to about 9 lines per 10 minutes for any stream of reasons.
  - Correct the "alternating reasons are also bounded" sentence in the docs to match.
- Behavior-preservation risks: a ninth distinct reason within one window would not log at once. Its type still appears in later windows and in the record.
- Evidence type: measured result (fake-clock probe)
- Verification / measurement plan: extend the alternating test to 9 or more reasons over 100 ticks within 10 minutes, asserting at most 9 entries.
- Relationship: residual of ANALYSIS-002

### ANALYSIS-103: In a backlog drain at the budget, the fallback removes ended sessions that compaction would have kept

- Severity: warning
- Related task / requirement:
  - tasks 1.4, 1.5
  - spec scenario "State at the budget recovers" ("the first drain compacts and, if needed, removes")
  - design risk: the budget fallback "happens only when the state would otherwise stop all processing"
  - the ANALYSIS-003(a) fix
- Locations:
  - `src/BellTower/IO/StateStore.cs:93` and `:98` (no compaction while there is a backlog), `:107`
  - `src/BellTower/IO/StateSnapshotStore.cs:47-74`
- Observation:
  - In a drain with more than 128 queued captures, compaction is skipped. An over-budget save therefore goes straight to the removal fallback, against uncompacted ended sessions.
  - The field case had 134 queued captures, so its first drain takes this path, not the "compact first" path the scenario describes.
  - Probe: 20 settled ended sessions with full journals, a budget of the stored size plus 20 KB, and filler captures from both providers.
    - 100 queued: all 20 sessions kept and compacted.
    - 130 queued: 1 ended session removed and none compacted. The next drain compacted the remaining 19.
  - The loss is bounded by the excess, but it removes history that the 200 cap and the 30-day rule would keep.
  - Every backlog drain over the budget also repeats the fallback's extra encodes.
  - No test combines a backlog with the budget; the recovery fixture queues only 6 captures.
- Proposed improvement and benefit:
  - When a save is over the budget, compact settled sessions first, even in a backlog drain, and remove sessions only if the state still does not fit.
  - Compacting during a backlog only risks the narrow event-replay window, while removal drops the whole session.
  - Add a recovery test with more than `BatchSize` captures at the budget, asserting that no ended session is removed when compaction alone fits.
- Behavior-preservation risks:
  - In that over-budget backlog drain, the ANALYSIS-003(a) protection gives way to staying within the budget. The design already accepts this trade-off for the fallback.
  - The refusal when live sessions alone exceed the budget is unchanged.
- Evidence type: measured result (scratch probe of the DEV-002 code)
- Verification / measurement plan: the test above, for both providers.
- Relationship: regression introduced by the ANALYSIS-003(a) fix

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 (and duplicate QA-001) | `diff=46553ba0c8c5 untracked=7/0e4c8db2fb3f` | Probe: quoted and unquoted profiles with spaces, a repository path with spaces, UNC, `/home/<a b>`, data-dir files and the user-name token are all masked. The exception type and message frame are kept, and the `LocalLog` comment was corrected. A profile or user name with an apostrophe still leaks (ANALYSIS-100). An unquoted final path segment with a space keeps its last word; this is the documented trade-off, and the profile itself is masked exactly. | still failing (narrow case; ANALYSIS-100) |
| ANALYSIS-002 | same | The map is bounded at 8. Alternating up to 8 reasons is bounded, and the record write is bounded to once per interval. Nine or more rotating, or ever-changing, reasons log every tick (ANALYSIS-102). | still failing (low likelihood; ANALYSIS-102) |
| ANALYSIS-003 (a) | same | The predicate matches `Prepare` and `ReplayInbox` exactly. Deferral cannot last indefinitely unless the state is already refused, and the cap, age and fallback still bound the state. But a backlog drain at the budget removes ended sessions instead of compacting them (ANALYSIS-103). | verified, with a regression (ANALYSIS-103) |
| ANALYSIS-003 (b) | same | The first compaction floors at End, and a late resume before a stray event revives the session (test and reading). A stray event after compaction triggers a second compaction whose floor passes End, after which an out-of-order resume is dropped; reproduced for both providers (ANALYSIS-101). Delayed earlier events stay ignored on both reducer paths. | still failing (ANALYSIS-101) |
| ANALYSIS-006 | same | The new-attachment theory takes the `ApplyUnordered` attachment-change branch through the causal replay and asserts the new attachment. The quiet-drain test detects any write on the third drain, for both providers. | verified |

## Assessment and limitations

- **Verdicts:**
  - ANALYSIS-006 is verified.
  - ANALYSIS-003(a): the predicate is verified, but the skip introduces ANALYSIS-103.
  - ANALYSIS-001, ANALYSIS-002 and ANALYSIS-003(b) still fail in residual cases: ANALYSIS-100, ANALYSIS-102 and ANALYSIS-101 respectively.
- **Severity order:**
  - ANALYSIS-101 matters most. It breaks the "Resumed session after compaction" scenario under ordinary hook reordering, for both providers.
  - ANALYSIS-100 is a narrow content-safety leak.
  - ANALYSIS-103 loses some ended-session history in the same shape as the field recovery case (more than one batch queued at the budget).
  - ANALYSIS-102 is low likelihood.
- **Docs:** two DEV-002 doc statements overstate the behavior, as noted in ANALYSIS-101 and ANALYSIS-102.
- **Per-tick cost:** the backlog check costs about 0.07-0.12 ms per tick on this machine, measured in scratch. That is negligible, so there is no finding.
- **Not performed:**
  - No repository build, test run or format check (assignment constraint).
  - No timing on the user's installation.
  - No live tray and no real data directory.
- **Writes:**
  - The client refused the report write, so this text is returned inline.
  - The probe sources and data are in the session scratch directory, outside the repository.
  - Target identity was unchanged before and after.
- **Decision for the orchestrator:** whether ANALYSIS-100 through ANALYSIS-103 block this cycle.
