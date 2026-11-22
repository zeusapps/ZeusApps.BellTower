# Implementation 003: bound-ended-session-state fix cycle 2 (DEV-003)

Fixes awaiting independent verification: ANALYSIS-100, ANALYSIS-101, ANALYSIS-102, ANALYSIS-103 (QA-100 duplicate). Deferred and untouched: ANALYSIS-004, ANALYSIS-005, ANALYSIS-007, QA-004, QA-005.

## Changes

### ANALYSIS-101: compaction folds only what the session applied (`src/BellTower/Core/CausalSessionReducer.cs`)
- `Compact` (ended sessions only) folds the journal observations with `At <= LastObserved`. The journal is sorted by `Compare`, which orders by time first, so these are a prefix. The floor moves to the latest folded observation and never backward (kept when `Compare` says the existing floor is later, or when nothing is folded).
- Later observations, which the ended session ignored (a resumed client's prompt delivered before its resume registration), stay in the journal, bounded by the 256 window. `CausalCheckpoint` is a copy of the compacted session (operation memory cleared), so checkpoint plus kept journal replays to the stored session. With none left the journal is empty and the checkpoint null.
- The "floor at last End" special case is removed; the new rule covers it (End is the latest folded observation) and sessions ended by attachment evidence.
- A second call with nothing new to fold changes nothing (no journal, checkpoint or floor write), so a drain after a stray does not re-save.

### ANALYSIS-103 / QA-100: compaction before removal in a backlog drain (`StateStore.cs`, `StateSnapshotStore.cs`)
- `StateSnapshotStore.Save(state, relieve)` takes an optional relief step. When the first encode is over the budget, `relieve` runs and the state is encoded once more; only if it still does not fit does the existing oldest-first removal run (live sessions never removed, the live-only refusal unchanged, `fallbackPasses` unchanged).
- `Drain` passes `CompactSettledSessions` as the relief step only when a backlog skipped compaction. Without a backlog compaction already ran.

### ANALYSIS-100: apostrophes in profile or user name (`RefreshHealth.cs`)
- `Describe` order is now: data directory and tail, exact user-profile rule, exact user-name rule, quoted-segment rule, drive/UNC/Unix path rule. The user name is replaced by a private-use marker (U+E000) until the end, then by `<user>`, so the later path rules still see one unbroken segment.

### ANALYSIS-102: rate limit (`RefreshHealth.cs`)
- `Remember` evicts only entries older than the repeat interval. With 8 fresh entries a new reason is not logged; one `tray-refresh-failed: further reasons suppressed` line is written at most once per interval (reset on recovery). Output is at most 8 reasons plus one notice per interval. The record write rule is unchanged.

### Docs
- `docs/architecture.md`: the compaction paragraph (fold rule, kept strays, late resume sorts ahead of them, budget exception to the backlog deferral) and the tray-log sentence (bounded reasons and one notice) replace the two flagged sentences. `docs/hook-ingress.md`: the DEV-001 sentence now states the fold rule and the budget exception.

## Tests (`tests/BellTower.Tests/IO`; Claude Code and Codex theories)

- `BoundedStateTests`:
  - (a) `AResumeDrainedAfterAStrayPromptWithAnEarlierHookTimeStillRevives...`: real capture drains. End, drain (compacted), stray prompt, drain (still ended, one journal entry, floor still End), resume with `R.At < S.At`, drain: live, stray's turn tracked, a later prompt tracked.
  - (b) the DEV-002 floor tests and the backlog theory are unchanged and pass (floor at End with a late resume; no-End floor at the latest observation).
  - (c) `ADelayedEarlierEventStaysIgnored...`: two compaction rounds with a stray kept; an earlier permission stays ignored, no waits or attention, floor unchanged.
  - (d) `RepeatedStraysOnACompactedSession...`: 300 strays, journal at most the window, a quiet drain leaves `state.json` bytes and write time unchanged.
  - `CompactionFoldsOnlyWhatTheSessionApplied...`: kept observations, checkpoint copy, displayed state unchanged, second call false, floor never moved back.
  - ANALYSIS-103 `ABacklogDrainOverTheBudgetCompactsSettledSessionsBeforeItRemovesAnyOfThem`: 130 queued captures from both providers, 12 settled ended sessions, budget from a reference drain compacted plus 8 KB (precondition: the uncompacted state does not fit); no ended session removed, all compacted, live sessions kept, captures beyond the batch stay queued.
  - Inbox half of the predicate: 2001 inbox files defer compaction for the drain, 2000 do not; both compact on the next drain.
- `RefreshHealthTests`: the old "40 distinct reasons all log" test now asserts 8 entries plus one notice; 9, 12 and 100 rotating or ever-changing reasons over 100 ticks within 10 minutes give at most 9 entries; entries older than the interval are evicted and the notice repeats once per interval; four apostrophe cases (profile in a quoted path, user name in a quoted path, bare name, unquoted profile path) assert exact output with no backslash, name fragment or path tail. The existing 13 sanitizer cases keep their outputs.

## Mutants (applied against BoundedStateTests and RefreshHealthTests, then reverted)

| Mutant | Result |
| --- | --- |
| Fold the whole journal and floor at the latest observation (the superseded rule) | killed: 10 tests, including both (a) theories, (c), (d), the fold unit test and the DEV-002 floor-at-End test |
| `Drain` passes no relief step to `Save` | killed: the backlog budget test |
| Inbox clause removed from `HasBacklog` | killed: the 2001-file case |
| Quoted-segment rule moved back before the profile rule | killed: 2 apostrophe cases |
| Eviction ignores age (all entries evicted) | killed: 5 rate-limit tests |

The `Compact`-never-returns-false-on-repeat property (save loop) is covered by (d) and the fold unit test; the working tree was restored after each mutant (builds clean, tests below).

## Checks

- `dotnet build -c Release --no-restore`: succeeded, 0 warnings, 0 errors.
- Focused (BoundedStateTests, RefreshHealthTests, Causal, SessionReducer, StateReporter): 77 of 77 passed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include <6 changed files>`: no diagnostics.
- Full suite: 1741 total, 1729 passed, 12 failed, all known and unrelated: 11 `HookTrayStartTests` and `ProcessStopAuthorityTests.OnlyHandlesCreatedByTheOperationAreEverTerminated`. No other failures.

## Limits

- Folding uses `At <= LastObserved`. An observation with the same `At` as the last applied one but a later `Kind` is folded as well; it can only be an ignored stray at the same instant.
- Compaction in a backlog drain over the budget gives up the backlog protection for that drain (the design accepts this); a late resume can then only be tracked if it sorts after the floor.
- Not run: the installed check, the real data directory and the real tray.

## Review identity (after)

`head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/5e050d0888dd`
