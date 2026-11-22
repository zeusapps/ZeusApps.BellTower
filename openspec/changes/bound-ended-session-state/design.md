# Design

## Context

See proposal.md for why this change is needed. Relevant current behavior:

- `StateStore.Drain` reads the snapshot, applies accepted captures and inbox replays, runs `PruneEndedSessions` (which removes ended sessions older than 30 days) and saves. `StateSnapshotStore.Save` throws `InvalidDataException` when the encoded state exceeds 16 MiB. Captures are deleted only after a successful save, so they stay queued.
- `CausalSessionReducer` keeps a per-session journal of up to 256 observations (`CausalEvents`), a `CausalCheckpoint` copy of the session taken before the journal, and a `CausalFloor`: an observation at or before which new observations are ignored. Each new observation re-sorts the journal and replays it from the checkpoint.
- `SessionReducer.Apply` uses the causal path for ingress observations or when the journal is non-empty. Otherwise it applies directly. Both paths ignore non-registration events for an ended session, and a later registration with a new attachment clears `CompletedOperations` and `Operations`.
- Measured on the maintainer's state: 95 sessions, 93 ended. `CausalEvents` accounts for 11.4 of 12.0 million characters, `CausalCheckpoint` for 0.26 million and `CompletedOperations` for 0.14 million.
- `TrayApplication.Tick` writes tray presence only after its first successful refresh, and on a failed refresh logs the bare text `tray-refresh-failed`.

## Goals / Non-Goals

**Goals:**
- State stays under the budget in normal use, and event processing never stops while ended sessions could be removed instead.
- Existing state at the budget recovers on the first drain.
- A failing refresh is visible in the log and in doctor.

**Non-Goals:**
- Changing the budget, the snapshot format, the journal window or the save frequency.
- Changing when the tray writes its presence record. A tray that cannot refresh is correctly not ready.
- Reducing the cost of encoding the whole state on every save, beyond what compaction gives.

## Decisions

1. **Compact ended sessions after a 10-minute settle interval, inside the drain before save.** *Revised 2026-11-21 after ANALYSIS-101 and ANALYSIS-103:* compaction folds only the journal observations at or before the session's `LastObserved` (its last applied observation), and the floor moves to the latest folded one, never backward. Later observations that the ended session ignored, for example a resumed client's prompt delivered before its resume registration, stay in the journal (still bounded by the 256 window) against a checkpoint copied from the compacted session. When there are none, the journal is empty and the checkpoint null. A late resume registration then sorts ahead of them and is replayed with them. Compaction is skipped while a capture or inbox backlog remains, except when the save would exceed the budget: then settled sessions are compacted before any removal. The original decision text follows. A session is compacted when `Ended` is true and `LastObserved` is more than 10 minutes before now. Compaction sets `CausalFloor` to the latest journal observation (keeping the existing floor when the journal is empty), clears `CausalEvents`, sets `CausalCheckpoint` to null, and clears `CompletedOperations` and `Operations`. The materialized session already equals the journal's replay result, so the displayed state does not change. Later observations behave correctly: an earlier one falls at or before the floor and is ignored, which keeps the session ended. A later resume registration passes the floor, and the reducer takes a fresh checkpoint from the compacted session, which is the existing empty-journal path. Completed-operation memory is safe to drop because both reducer paths ignore non-registration events for an ended session, and revival clears that memory. *Alternatives:* compacting at the end event loses the reordering of events delivered seconds late. Keeping the journal and pruning harder still lets about 55 ended sessions fill the budget. Ten minutes is well beyond normal hook delivery delay. Events queued while the tray was stopped are drained before compaction runs in the same drain, and even a later batch can only contain causally earlier events, which leave the session ended.

2. **Retain at most 200 ended sessions, alongside the 30-day age.** When there are more, remove the ended sessions with the oldest `LastObserved` first. A compacted ended session is a few kilobytes, so 200 of them stay far below the budget. The age rule is unchanged.

3. **Budget fallback instead of a permanent failure.** If the encoded state still exceeds the budget, remove ended sessions oldest first and re-encode. Use a bounded number of passes, for example removing a growing fraction each pass, rather than one re-encode per session. Throw the existing `InvalidDataException` only when no ended session remains. Removed sessions' receipts follow the existing retention rules. *Alternative:* raising the budget only postpones the failure.

4. **Rate-limited reasoned refresh log and a refresh-health record.** On a failed refresh the tray logs `tray-refresh-failed: <exception type>: <sanitized message, at most 200 characters>` when the reason changes, and otherwise at most once every 10 minutes. On the first success after a logged failure it logs `tray-refresh-recovered`. A recovery does not reset the limit (revised 2026-11-21, ANALYSIS-201): a failure within 10 minutes of the last logged entry for its reason is not logged again, so any mix of failures and successes stays within the failure-line bound of a continuous failure (at most nine failure or suppression lines per 10 minutes), plus at most one recovery line per logged failure. A first failure after a healthy period longer than the repeat interval still logs at once. On a transition, never per tick, the tray atomically writes a small `refresh-health.json` in the data directory with `failingSince`, `reason` and `lastSuccess`. A failure is recorded when it is logged, and also, even when its log line is suppressed, once the current failure window has lasted 30 seconds (revised 2026-11-21, ANALYSIS-301), so doctor's one-minute persistent rule (decision 5) sees every lasting failure. A recovery is recorded whenever the record shows a failure, so doctor never reports a failure that has ended. The message contains no path, prompt or token: only the exception type and its bounded message, with every path replaced, including a path whose names contain an apostrophe or a space in any position. A quoted segment that starts at a path root (a drive, UNC or Unix root, the data directory, or the profile, which is first replaced by a root marker) is masked through the last closing quote on its line (revised 2026-11-21, ANALYSIS-300 and ANALYSIS-400): this may also mask text between two quoted paths, which is accepted, because over-masking loses only diagnostic detail while a partial match leaks names. Sanitizing a message has a bounded cost: its patterns use a match timeout, and a timeout replaces the message with the exception type alone.

5. **Doctor `state` section.** Doctor reports the `state.json` byte size, the budget, live and ended session counts read from a read-only snapshot, and the refresh-health record together with whether the tray is running. A failure is reported as persistent when `failingSince` is more than 1 minute old while the tray is running.

6. **Shared policy for both providers.** All of this lives in the shared store, reducer and tray, with no provider-specific branches. Tests use Claude Code and Codex observation fixtures and a mixed state.

## Risks / Trade-offs

- [A delayed earlier event arriving more than 10 minutes after a session ended is ignored] → The spec already requires an ended session to stay ended. Such an event could only have changed intermediate state that is no longer displayed.
- [The budget fallback removes ended sessions sooner than 30 days] → This happens only when the state would otherwise stop all processing, and live sessions are never removed.
- [An older build reads compacted state] → It uses only existing fields with valid values (an empty journal, a null checkpoint), which the current validity check accepts. Rollback stays compatible.
- [The first drain after upgrade encodes a large state several times] → It is bounded by decision 3 and happens once. Compaction usually makes the first save fit.

## Migration Plan

No migration step is needed. The first drain of the upgraded tray compacts and saves. Rolling back to an earlier build keeps reading the compacted state. That build can grow its state again, but it is not wedged at once.
