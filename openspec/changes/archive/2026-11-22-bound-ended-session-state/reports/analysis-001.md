> Saved verbatim by the orchestrator. Reviewer: code-analyzer subagent (ANALYSIS-001), Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`. The client refused the reviewer's native report write (known restriction, see docs/agent-workflow.md#known-client-restrictions); the reviewer returned the text inline.

# Analysis report: bound-ended-session-state / ANALYSIS-001

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-001) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-21T12:52Z
- Change root / assignment: `openspec/changes/bound-ended-session-state` / ANALYSIS-001
- Task and requirement references:
  - tasks 1.1-1.5 and 2.1-2.3 in [tasks.md](../tasks.md)
  - design decisions 1-6 in [design.md](../design.md)
  - requirement "Bounded monitoring state" in [specs/session-tracking/spec.md](../specs/session-tracking/spec.md)
  - developer evidence [implementation-001.md](../evidence/implementation-001.md)
- Scope and relevant neighboring paths:
  - DEV-001 product files:
    - `src/BellTower/Core/CausalSessionReducer.cs`
    - `src/BellTower/IO/StateStore.cs`
    - `src/BellTower/IO/StateSnapshotStore.cs`
    - `src/BellTower/IO/LocalLog.cs`
    - `src/BellTower/IO/RefreshHealth.cs`
    - `src/BellTower/IO/DataPaths.cs` (`RefreshHealth` path only)
    - `src/BellTower/Tray/TrayApplication.cs` (refresh health only)
    - `src/BellTower/Diagnostics/StateReporter.cs`
    - `src/BellTower/Diagnostics/DiagnosticsReporter.cs` (`State` field and `trayRunning` local only)
  - DEV-001 docs: `docs/architecture.md`, and the retention sentence in `docs/hook-ingress.md`.
  - DEV-001 tests: `tests/BellTower.Tests/IO/BoundedStateTests.cs`, `IO/RefreshHealthTests.cs`, `Diagnostics/StateReporterTests.cs`.
  - Neighbors read:
    - `src/BellTower/Core/SessionReducer.cs` (`Apply`, `ApplyUnordered`, `EndSession`)
    - `src/BellTower/Core/SessionState.cs`
    - `src/BellTower/Providers/CaptureConsumer.cs` (`Prepare`, `BatchSize`)
    - `StateStore.ReplayInbox`, `RetainPendingReceipts`, `ReconcileAttachments`
    - `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/IO/Json.cs`
    - `src/BellTower.Protocol/CaptureSpool.cs`
    - `TrayApplication.Tick` and its timer
    - `docs/development.md`
  - Not judged: the adopt-native-sessions-from-hooks parts (hook tray start, `HookStartReporter`, `LockName`, Exit menu item).
- Baseline commit: 5e95554
- Implementation identity (before): `head=5e95554 baseline=5e95554 files=22 diff=21b1173f10b7 untracked=7/6485f177669f`
  - Tracked diff SHA-256: `21b1173f10b731db83e74682ed9a21f8310a507f789b20857c227ef5f40bfc5f`
  - Untracked DEV-001 files:
    - `StateReporter.cs` 3ea6234c…
    - `RefreshHealth.cs` 930ae29c…
    - `StateReporterTests.cs` b09d5ec1…
    - `BoundedStateTests.cs` 5959b116…
    - `RefreshHealthTests.cs` 1063a90c…
  - Also untracked, from the other change: `HookStartReporter.cs` and `HookTrayStartTests.cs`.
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=21b1173f10b7 untracked=7/6485f177669f`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Reducer invariant: floor | Source reading of `CausalSessionReducer.Apply` and `Compact` | Every causal apply re-sorts the journal with `Compare(At, Kind, Id)`, so `CausalEvents[^1]` is the maximum and a new floor is always later than the old one. The floor check runs before the journal add, so a pre-floor event never re-grows the journal. | Correct. Residual edge in ANALYSIS-003: the floor includes replay-ignored post-end observations. |
| Reducer invariant: two paths after compaction | Reading `SessionReducer.Apply` lines 5-15 and 59-63 | After compaction the journal is empty, so a non-ingress observation takes `ApplyUnordered`. That path ignores the floor, but on an ended session it rejects every non-registration event and any registration with `At <= LastObserved`. Since `LastObserved <= CausalFloor.At`, its effective floor is weaker but still keeps the session ended. Ingress observations take the causal path, take a fresh checkpoint from the compacted session (`Ended = true`), and are ignored unless they are a later resume registration. | Both paths keep an ended session ended for earlier events. `DelayedEarlierEventAfterCompaction…` covers both paths for both providers. |
| Revival, same vs new AttachmentId | Reading `ApplyUnordered` lines 37-57 and 59-111 | New attachment: the reducer clears `CompletedOperations`/`Operations` itself, so compaction changes nothing. Same attachment: revival does not clear them. But every pre-end tool event is at or before the floor (causal path) or rejected while ended (unordered path), and the first prompt (`BeginTurn`) clears `CompletedOperations`. | Equivalent observable behavior. Only the same-attachment case is tested (ANALYSIS-006). |
| AppliedEvents / CaptureReceipts | Reading `CaptureConsumer.Prepare`, `RetainPendingReceipts` | Receipts are keyed by envelope ID; compaction and removal do not touch them. `AppliedEvents` is trimmed to pending files as before. For compacted sessions the floor replaces the journal's ID dedupe, since duplicates of journal events are at or before the floor. | No interaction defect. |
| Displayed state of an ended session | Reading `Compact`, `SessionState` | Untouched: `Key`, `Provider`, `SessionId`, `Directory`, `TurnId`, `LastObserved`, `Waits`, `AcknowledgedEpisode`, `ViewedEpisode`, `ResolvedApprovals`, `AttachmentHealth`, `AdditionalData` (extension data). Runtime-only fields are `[JsonIgnore]`. | Correct. |
| Reconcile-ended sessions | Reading `ReconcileAttachments` and the causal replay | Some sessions are ended by attachment health rather than by an observation. Pre-change, the next causal replay from the stale checkpoint un-ended such a session, and the same drain's reconcile re-ended it. After compaction the checkpoint comes from the ended session, so it simply stays ended. | Behavior change consistent with the spec; no finding. |
| Removal of live sessions / captures | Reading `PruneEndedSessions`, the `Save` fallback, `Drain` | Both removal paths filter on `Ended`, and a session revived in the same drain is live. Captures are deleted only after `snapshots.Save` returns; a refusal throws before deletion. | No live session is removed, and no capture is deleted before a successful save. |
| Per-tick saves | Reading the `Drain` gate and `Compact`/`Prune` idempotence | `Compact` returns false once a session is compacted. `Prune` returns false once within cap and age. Reconcile sets `confirmed-ended` once. A stray event for a compacted session re-creates a one-entry journal and checkpoint, and the same drain re-compacts it; that drain saves anyway for the accepted capture. | No save-every-tick loop, by reading. Not executed; ANALYSIS-006 suggests a regression test. |
| Performance | Reading `Save`, `Json.Options` (`WriteIndented = true`), the tray timer (`Interval = 250`) | See ANALYSIS-004. Per-tick retention costs one pass over sessions and one `OrderBy` over at most a few hundred ended sessions. That is negligible next to the existing per-tick whole-state decode. | Hypotheses only; nothing measured. |
| Sanitization | Probe of the exact `PathPattern` regex in PowerShell 7 (same .NET regex engine), not the product binary | `'C:\Users\First Last\.codex\…'` became `'<path> Last\.codex\…'`. `'D:\work\repo name\file.txt'` became `'<path> name\file.txt'`. | ANALYSIS-001. |
| Rate limit, atomic writes | Reading `RefreshHealth.Failed`/`Succeeded`/`Persist`, `AtomicFile.Write` | Writes are temp file plus `File.Move` with write-through, and the record is written on transitions only. The rate limit only compares with the previous reason. | ANALYSIS-002. |
| Doctor read path | Reading `StateReporter`, `RefreshHealth.Read`, `AtomicFile.Read`, the other doctor reporters | Doctor's state path takes no `DataLock`, so there is no lock contention with the tray. It does a full lock-free decode of `state.json`. | ANALYSIS-005. |
| Parity | `grep` for provider literals in the changed product files; reading the tests | No provider branch in `StateStore`, `StateSnapshotStore`, `CausalSessionReducer`, `RefreshHealth` or `StateReporter`. Doctor groups counts generically by provider. Every reducer scenario is a Claude Code/Codex theory. Cap, budget and recovery tests use mixed state, and recovery captures go through `CaptureFactory` for both providers. | Parity holds. Fixtures are synthetic observations, not recorded provider payloads, which is proportionate for provider-neutral policy. |
| Architecture | Judged against the `docs/development.md` layer list | `Compact` lives in the Core reducer, which is correct. Retention constants and policy in `StateStore` (state transactions) fit IO. `StateSnapshotStore.Save` now carries session-removal policy and mutates its argument. | ANALYSIS-004 (mutation on refusal) and ANALYSIS-007. |
| Docs | Reading the architecture and hook-ingress additions | The settle interval, cap, fallback passes, log format, record fields and doctor fields match the code. `docs/architecture.md` says compaction runs "each drain, before it saves", which is accurate. The design rationale about backlog ordering is not (ANALYSIS-003). | Matches except as noted. |
| Format / build / tests | Not run | QA-001 runs the full suite concurrently, and a build or `dotnet format` workspace load could race on `obj`/`bin` outputs. | Not run (assignment constraint). |

## Findings

### ANALYSIS-001: Refresh reason sanitization leaks the rest of any path that contains a space

- Severity: warning
- Related task / requirement: 2.1; design decision 4 ("The message contains no path"); spec "A refresh failure SHALL be logged with a bounded, sanitized reason"
- Locations: `src/BellTower/IO/RefreshHealth.cs:84-96` (`Describe`), `:110` (`PathPattern`); `src/BellTower/IO/LocalLog.cs:15` and `:22`, in the working tree on top of 5e95554
- Observation:
  - `Describe` replaces the data root exactly, then masks other paths with `(?:[A-Za-z]:[\\/]|\\\\|/(?:home|Users|tmp|var)/)[^\s'"<>|]*`. The match stops at the first whitespace, so any path outside the data root that contains a space keeps its tail.
  - Windows account folders with a space (for example a first and last name) and repository folders with spaces are common.
  - The tray `Tick` catch covers more than the drain. The overview, titles, native names and Git context reads touch provider and project paths.
  - Probe with the same regex: `Access to the path 'C:\Users\First Last\.codex\sessions\x.jsonl' is denied.` became `Access to the path '<path> Last\.codex\sessions\x.jsonl' is denied.`
  - The leaked text goes to `bell-tower.log` and `refresh-health.json`, and doctor prints it.
  - Separately, `LocalLog.cs:15` still says "Deliberately accept codes, never exceptions/messages", which the new overload contradicts. Anyone reading that rule will assume the log holds no message text.
- Proposed improvement and benefit:
  - Before the regex, replace each single-quoted segment that contains a separator with `<path>`; .NET IO exceptions quote paths as `'…'`.
  - Replace `%USERPROFILE%` (both separator styles, case-insensitive) and the current user name as whole tokens.
  - Update the `LocalLog` comment to say reasons are accepted only after caller sanitization.
  - Add `RefreshHealthTests` cases for a user-profile path with a space and a repository path with a space.
- Behavior-preservation risks: stronger masking can hide a useful file name. Keep the exception type and the non-path text.
- Evidence type: measured result (regex probe outside the product); source observation for the reach of the `Tick` catch
- Verification / measurement plan: unit test `Describe(new IOException("Access to the path 'C:\\Users\\A B\\x' is denied."), root)` and assert the result contains neither `A`/`B` path segment nor any backslash.
- Relationship: none

### ANALYSIS-002: Alternating failure reasons bypass the rate limit and rewrite the health record every tick

- Severity: suggestion
- Related task / requirement: 2.1; spec "without one entry per tick"; design decision 4 ("On each transition, never per tick")
- Locations: `src/BellTower/IO/RefreshHealth.cs:36-59`; tray timer `src/BellTower/Tray/TrayApplication.cs:21` (`Interval = 250`); log rotation `src/BellTower/IO/LocalLog.cs:17-20`
- Observation:
  - `changed` compares only with the immediately previous reason. If two reasons alternate, every tick logs an entry and persists `refresh-health.json` with write-through. An example is a sharing violation on one tick and the budget refusal or a different IO error on the next.
  - At a 250 ms tick that is up to 4 log lines and 4 flushed writes per second. The 250 KB log is then truncated within minutes, which erases the other diagnostics.
  - Tests cover only identical reasons followed by a changed one.
- Proposed improvement and benefit:
  - Rate-limit per reason, using a small map from reason to last-logged time bounded to a few entries.
  - Persist the record only when `failingSince` starts, or when the reason differs from the persisted one for longer than one tick, or at most once per interval.
  - This keeps "a new reason logs immediately" and bounds output whatever the order of reasons.
- Behavior-preservation risks: a reason seen earlier in the same failure window would no longer log immediately when it returns. That is acceptable because its earlier entry is still in the log.
- Evidence type: source observation; the rate figures are a hypothesis derived from the timer interval
- Verification / measurement plan: a `RefreshHealthTests` case that alternates two exceptions 100 times within 10 minutes on a fake clock, asserting at most two `tray-refresh-failed` entries and a bounded number of record writes.
- Relationship: none

### ANALYSIS-003: Compaction can run before a queued backlog is fully applied, and the floor includes ignored post-end observations

- Severity: suggestion
- Related task / requirement: 1.1, 1.2, 1.5; design decision 1 (rationale "Events queued while the tray was stopped are drained before compaction runs in the same drain"); spec "SHALL NOT … prevent a resumed session from being tracked"
- Locations: `src/BellTower/IO/StateStore.cs:91-96`; `src/BellTower/Providers/CaptureConsumer.cs:9` (`BatchSize = 128`) and `:35`; `src/BellTower/Core/CausalSessionReducer.cs:92-96`
- Observation:
  - (a) Captures in later batches are applied after their ended sessions were compacted.
    - `Prepare` takes at most 128 captures per drain, and compaction runs in that same drain. The field state had 134 captures and 43 inbox items queued.
    - So the design rationale holds only for a backlog of up to 128.
    - For an ended session the displayed result is the same, because an earlier event cannot change it. This is not a spec violation today.
  - (b) `Compact` sets the floor to the latest journal observation, including observations the replay ignored because they came after `End`.
    - Before compaction, a resume registration timed between `End` and such a stray event is sorted in and revives the session.
    - After compaction it is at or before the floor and is dropped. The session stays ended, and the resumed session's later events are ignored.
    - This needs a stray post-end event that is later than the resume, with the resume delivered more than 10 minutes late (for example in a backlog). The probability is low.
- Proposed improvement and benefit:
  - Skip `CompactSettledSessions` in a drain whose capture batch was full (`Prepare` returned `BatchSize` entries) or whose capture inbox still has files.
  - Retention then runs only once the backlog is consumed. This makes the design rationale exact and closes most of the (b) window.
  - Optionally, when the session has an `End` observation, set the floor to the latest journal observation not later than that `End`.
- Behavior-preservation risks: compaction is deferred while a backlog persists. During that time the existing budget fallback still bounds the state.
- Evidence type: source observation
- Verification / measurement plan: a `BoundedStateTests` case with more than `CaptureConsumer.BatchSize` queued captures for an ended session of each provider, including an out-of-order resume registration in the second batch, asserting the session is live after the second drain.
- Relationship: none

### ANALYSIS-004: A refused save mutates its input and repeats whole-state encodes on every tick

- Severity: suggestion
- Related task / requirement: 1.4; design decision 3; the non-goal on encoding cost
- Locations: `src/BellTower/IO/StateSnapshotStore.cs:42-81` (per-session encodes at `:51`, passes `:52-74`, refusal `:75-78`); `src/BellTower/Tray/TrayApplication.cs:21`, `:259`
- Observation:
  - Refusal cost:
    - When live sessions alone exceed the budget, `Save` does the following, then throws. Nothing is persisted.
      1. Encodes the whole state.
      2. Encodes each ended session to weigh it.
      3. Removes ended sessions from the caller's `state`.
      4. Re-encodes at least once, up to four times.
    - So every 250 ms tick on the UI thread repeats the read and decode of a roughly 16 MiB snapshot plus at least two whole-state encodes. Before the change it was one encode.
    - Because the stored file is within the read limit, the ended sessions are usually small in this case. Pass 1 removes them all, so the cost is about two encodes plus the weights rather than five.
  - Mutation:
    - `Save` removes sessions from its argument even when it then refuses.
    - It is also reached from `AdvanceReminders`, `Acknowledge` and `MarkViewed`.
    - Today every caller discards the state on the exception, so this is a latent contract hazard, not a defect.
- Proposed improvement and benefit:
  - Before any mutation, encode the live-only projection once. If it exceeds the budget, throw at once without touching `state`. Otherwise run the existing weighted passes, which then always succeed.
  - This bounds the refusal to two encodes and makes `Save` non-mutating on failure. It also removes the need for the `pass >= FallbackPasses` special case in the refusal path.
  - A per-session estimate cannot replace the exact live-only encode. With `WriteIndented = true`, standalone session encodings underestimate their nested size. So `size − Σweights` overestimates the live size and could refuse a save that would fit.
- Behavior-preservation risks: the refusal message, the retained captures and the successful fallback path are unchanged.
- Evidence type: source observation; the CPU cost on the UI thread is a performance hypothesis (not measured)
- Verification / measurement plan:
  - In `LiveSessionsAloneOverTheBudget…`, assert `LastSaveEncodes <= 2` and an unchanged `state.Sessions` count.
  - For a bounded measurement, time one `Json.Encode` of a synthetic 16 MiB state in an uncommitted scratch benchmark to size the per-tick cost.
- Relationship: ANALYSIS-007 (same method)

### ANALYSIS-005: Doctor's state section adds another full lock-free decode of the state file

- Severity: suggestion
- Related task / requirement: 2.2; design decision 5
- Locations:
  - `src/BellTower/Diagnostics/StateReporter.cs:18-35`
  - other readers: `src/BellTower/Diagnostics/CaptureHealthReporter.cs:54`, `:59`; `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs:98`
  - `src/BellTower/IO/AtomicFile.cs:38-52`; `src/BellTower/IO/StateSnapshotStore.cs:18-35`
- Observation:
  - Doctor takes no lock, so it cannot contend with the tray's `DataLock` (focus question 4).
  - It now decodes `state.json` at least four times: twice in `CaptureHealthReporter`, once in `ConfirmationCoverageReporter`, and once in the new reporter.
  - `File.ReadAllText` opens with `FileShare.Read`, without delete sharing. A tray `File.Replace` that overlaps one of these reads can fail with a sharing violation and record a transient `tray-refresh-failed`. This pattern predates the change; the new reader adds exposure.
  - `StateSnapshotStore.Read` silently falls back to `state.json.bak` or an empty state, and logs `state-invalid` from doctor. `StateReporter` then shows counts that do not belong to the file whose `Bytes` it reports, without saying so.
- Proposed improvement and benefit:
  - Read the snapshot once in `DiagnosticsReporter` and pass it to the reporters that need sessions.
  - Have `StateReporter` report the counts as unavailable when the primary file did not decode.
  - Optionally, read state with `FileShare.ReadWrite | FileShare.Delete` in a doctor-only path.
- Behavior-preservation risks: the reporters' JSON shapes are unchanged; only where they get the snapshot changes.
- Evidence type: source observation; the sharing-violation interaction is a platform-behavior hypothesis, not reproduced
- Verification / measurement plan: a `StateReporterTests` case with an invalid `state.json` and a valid `.bak`, asserting the counts are not reported as the primary file's.
- Relationship: none

### ANALYSIS-006: No regression tests for new-attachment revival or for quiet drains leaving state.json untouched

- Severity: suggestion
- Related task / requirement: 1.1, 1.2; focus question 3 (save on every tick)
- Locations: `tests/BellTower.Tests/IO/BoundedStateTests.cs:167-199` (revival only with the same `AttachmentId`); `src/BellTower/IO/StateStore.cs:97`
- Observation:
  - The revival test uses the session's original attachment. So the new-attachment branch (`SessionReducer.cs:37-57`, which clears operations itself) is not exercised after compaction.
  - No test asserts that a second drain leaves `state.json` untouched when there are no captures and nothing newly settled.
  - By reading, `Compact` and `Prune` are idempotent, so there is no per-tick save today. The new `retained` gate makes this an easy regression to introduce.
- Proposed improvement and benefit:
  - Add a Claude Code/Codex theory that revives a compacted session with a new `AttachmentId` and tracks an approval.
  - Add a test that drains twice and asserts the file's last write time and bytes are unchanged after the second drain.
- Behavior-preservation risks: none (tests only).
- Evidence type: source observation
- Verification / measurement plan: the tests themselves.
- Relationship: none

### ANALYSIS-007: The oldest-ended ordering is duplicated and the removal policy is split across two classes

- Severity: suggestion
- Related task / requirement: 1.3, 1.4; `docs/development.md` IO responsibilities ("`StateStore` coordinates replay and state transactions; `StateSnapshotStore` reads/writes snapshots")
- Locations: `src/BellTower/IO/StateStore.cs:248-260`; `src/BellTower/IO/StateSnapshotStore.cs:49-50`
- Observation:
  - Both classes decide removal by ordering ended sessions by `LastObserved`, then by `Key` with ordinal comparison.
  - The settle interval, cap and age live in `StateStore`. The budget removal lives in the snapshot store, which now changes domain state rather than only reading and writing snapshots.
  - If one ordering later changes (for example to prefer uncompacted sessions or to use the end time), the cap and the budget fallback will diverge.
- Proposed improvement and benefit:
  - Add one internal helper for the order in which ended sessions are removed, for example `StateStore.EndedOldestFirst(AppState)` or a small static in Core next to `Compact`. Use it from both places.
  - Keep the fallback in `Save`, where the encoded size is known, but document it as part of the state transaction.
  - Together with ANALYSIS-004, `Save` would then mutate state only on success.
- Behavior-preservation risks: none if the ordering is preserved exactly (ordinal tie-break on `Key`).
- Evidence type: source observation
- Verification / measurement plan: the existing cap and budget tests already assert oldest-first, contiguous removal.
- Relationship: ANALYSIS-004

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | not applicable | not applicable | not applicable |

## Assessment and limitations

- **Invariants:** the causal-ordering invariants hold for the cases the spec names.
  - Compaction keeps every displayed field.
  - The floor, plus the ended-session guard in `ApplyUnordered`, keeps a compacted session ended on both reducer paths for causally earlier events.
  - Revival works with the same or a new attachment. Dropping operation memory is not observable because of the floor and `BeginTurn`.
  - Retention and the budget fallback never remove a live session, and captures are deleted only after a successful save.
  - By reading, the new `retained` gate does not cause a save on every tick.
- **Findings:**
  - One warning (ANALYSIS-001): the refresh reason can leak the tail of a path with a space, including part of a user-profile folder name, and the `LocalLog` rule comment now contradicts the code.
  - Six suggestions:
    - 002: rate-limit robustness.
    - 003: compaction timing against a backlog larger than one capture batch, plus a narrow floor edge.
    - 004: refusal cost and mutation.
    - 005: doctor read duplication.
    - 006: test gaps.
    - 007: duplicated ordering.
- **Parity:** shared policy, no provider branches, and Claude Code, Codex and mixed fixtures throughout.
- **Not performed:**
  - No build, test, format verification or timing measurement: QA-001 was running the full suite concurrently, and the assignment asked to avoid racing builds.
  - All performance statements are hypotheses with a measurement plan.
  - The installed check (3.3) and the real data directory were not touched.
- **Writes:** none; the client refused the report write. Target identity was unchanged before and after.
- **Decisions for the orchestrator:**
  - Whether ANALYSIS-001 is blocking, since it concerns the sanitized-reason requirement.
  - Whether to fix ANALYSIS-003(a) now: the field backlog of 134 captures already exceeded one batch.
