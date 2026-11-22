# Implementation 002: bound-ended-session-state fix cycle 1 (DEV-002)

Fixes awaiting independent verification: ANALYSIS-001 (QA-001 duplicate), ANALYSIS-002, ANALYSIS-003, ANALYSIS-006, QA-002, QA-003. Deferred and untouched: ANALYSIS-004, ANALYSIS-005, ANALYSIS-007, QA-004, QA-005.

## Changes

### ANALYSIS-001 / QA-001: sanitizer (`src/BellTower/IO/RefreshHealth.cs`, `LocalLog.cs`)
- `Describe` now masks, in order: the data directory (exact text, both separator styles) and whatever follows it (`<data>`); every single-quoted segment that holds a path separator (`'<path>'`, or `'<data>'` when it starts with the data directory); the user-profile directory (both separator styles, case-insensitive, as a whole path component) with its tail; drive, UNC and Unix paths; and the current user name as a whole token (`<user>`).
- A path segment before a separator may contain spaces; the last segment ends at whitespace. Unix roots include `/root`, `/mnt`, `/home`, `/Users`, `/tmp`, `/var`, `/opt`, `/etc`, `/usr`, `/srv`, `/media`, `/private`, `/Volumes`, `/proc`, `/run`, `/dev`, `/snap`, `/Library`, plus any `/<dir>/...` path with at least two segments. The exception type and non-path text are kept. The message is capped at 2000 characters before matching and the reason at 200 after.
- `Describe` takes optional `profile` and `userName` parameters (default: the current user's) so tests use synthetic values.
- `LocalLog.cs` comment now says a reason is accepted only after the caller sanitized it.
- Known trade-off: two paths in one message with only spaces between them can be masked as one `<path>`; it errs on the side of masking.

### ANALYSIS-002: per-reason rate limit (`RefreshHealth.cs`)
- A bounded map (8 reasons, oldest evicted) holds the last logged time per reason. A new reason logs at once; a known one at most once per 10 minutes, whatever the order.
- `refresh-health.json` is written only when the failure window starts, or when the reason differs from the recorded one and 10 minutes have passed since the last write. A repeat of the same reason never rewrites it. Recovery clears the map and writes as before.

### ANALYSIS-003: compaction versus backlog (`StateStore.cs`, `CausalSessionReducer.cs`)
- (a) `Drain` computes `HasBacklog()` before applying anything: more than `CaptureConsumer.BatchSize` capture files, or more than 2000 inbox files (the replay limit). While a backlog remains, `CompactSettledSessions` is skipped. The 200 cap, the 30-day age rule and the save budget fallback are unchanged.
- (b) `Compact` sets the floor to the last End observation in the journal (the latest observation not later than it, since the journal is sorted); with no End observation (for example a session ended by attachment evidence) the rule is unchanged: the latest journal observation.
- `docs/architecture.md` and the DEV-001 sentence in `docs/hook-ingress.md` describe the floor, the deferral, the per-reason limit and the sanitizer.

### Test seam (`StateSnapshotStore.cs`)
- New optional constructor argument `fallbackPasses` (default 4, the existing constant). Production behavior is unchanged. It lets a test run the last, remove-all pass as the first one (QA-003). No new `state.json` field.

## Tests (all in `tests/BellTower.Tests/IO`)

- `RefreshHealthTests`:
  - 12 sanitizer cases (profile with a space quoted and unquoted, forward-slash profile, repository with spaces quoted and unquoted, UNC with spaces, `/root`, `/mnt/c/...` with a space, `/home/<name with space>`, bare `/root`, the profile itself, user-name token not matching a longer word). Each asserts the exact masked reason and that no backslash and no path segment survive.
  - data directory and its files, and the current machine profile and user name.
  - ANALYSIS-002: 100 alternating failures of two reasons within 10 minutes give exactly one entry per reason, and the record is written once (a marker written into the record file proves no later write); after 11 minutes a persisted reason change is written once; further changes within the interval are not. 40 distinct reasons all log once (bounded map).
  - QA-002: the same failure repeated after more than 10 minutes logs again and does not rewrite the record (marker).
- `BoundedStateTests`:
  - ANALYSIS-006: the resume-after-compaction theory now runs Claude Code and Codex, each with the same and with a NEW `AttachmentId`, tracking an out-of-order approval, a pending approval and its resolution.
  - ANALYSIS-006: a quiet drain leaves `state.json` bytes and last write time unchanged (write time pre-set to 2020 so a write is detectable), with ended and live sessions of both providers.
  - ANALYSIS-003 (b): `Compact` floors at the End; a late resume between End and a stray post-end event revives the session; without an End the floor is the latest observation (both providers).
  - ANALYSIS-003 (a): 130 queued captures (128 first batch, including an ended session with a stray post-end event, then 125 filler sessions of both providers; the second batch holds an out-of-order resume registration and a later prompt). After the first drain the session is ended and uncompacted; after the second it is live with the prompt tracked, for both providers.
  - QA-003: with `fallbackPasses: 1` the single pass removes every ended session in two encodes, while the normal pass count removes only a subset (kills the "remove-all pass removed" mutant). A recovery fixture with 16 settled and 16 recent ended sessions, live sessions and six queued captures from both providers, a budget of half the compacted size (precondition asserted: compaction alone does not fit): one drain fits the budget, consumes the captures and keeps live and queued sessions.

## Mutation probes (applied, run against BoundedStateTests and RefreshHealthTests, reverted)

| Mutant | Result |
| --- | --- |
| No backlog guard before compaction | killed (2: backlog theory, both providers) |
| Floor = latest journal observation again | killed (2: floor theory, both providers) |
| Record written on every logged failure | killed (2: alternating and repeat-after-interval) |
| Fallback's remove-all pass never reached | killed (1: single-pass test) |

## Checks

- `dotnet build -c Release --no-restore`: succeeded, 0 warnings, 0 errors.
- Focused (BoundedStateTests, RefreshHealthTests, StateReporterTests): 53 of 53 passed. Causal/reducer tests ran in the full suite.
- `dotnet format ... --include <my changed files>`: ran with no diagnostics (it normalized `RefreshHealthTests.cs`).
- Full suite: 1722 total, 1709 passed, 13 failed, all known and not caused by this cycle:
  - 11 `HookTrayStartTests` (the other change's job environment);
  - `ProcessStopAuthorityTests.OnlyHandlesCreatedByTheOperationAreEverTerminated` (the other change's `TrayProcess.cs`);
  - `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks` (a load-only failure in the known list).
- Every earlier DEV-001 test and the causal ordering tests passed.

## Limits

- Not run: the installed check (3.3), the real data directory and the real tray (never touched or stopped).
- The `TrayApplication` wiring of `Failed`/`Succeeded` is unchanged and still has no seam (QA-004 deferred).
- The sanitizer is regex based; a path form outside the listed shapes with an unusual root and spaces could still keep its tail. The quoted-segment rule covers the usual .NET message shape regardless of root.
- Compaction waits only while a backlog remains. A permanent unreadable capture file would defer it; the cap, age and budget fallback still bound the state.

## Review identity (after)

`head=5e95554 baseline=5e95554 files=22 diff=46553ba0c8c5 untracked=7/0e4c8db2fb3f`

## Return block

- Status: complete, awaiting independent verification of ANALYSIS-001, ANALYSIS-002, ANALYSIS-003, ANALYSIS-006, QA-001, QA-002, QA-003.
- Changed: `src/BellTower/IO/RefreshHealth.cs`, `LocalLog.cs`, `StateStore.cs`, `StateSnapshotStore.cs`, `src/BellTower/Core/CausalSessionReducer.cs`, `tests/BellTower.Tests/IO/BoundedStateTests.cs`, `RefreshHealthTests.cs`, `docs/architecture.md`, `docs/hook-ingress.md`.
- Deferred, untouched: ANALYSIS-004, ANALYSIS-005, ANALYSIS-007, QA-004, QA-005.
