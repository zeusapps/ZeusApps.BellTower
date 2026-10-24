# Implementation 007 (fix round 3: ANALYSIS-150, ANALYSIS-102, ANALYSIS-151)

All items are awaiting independent verification; none is closed here.

## ANALYSIS-150 (Claude, Codex, mixed)
- Shared rule: `IdentityInheritance.FromParent` sets child `WorkFloorAt = max(child, parent)` (one-way, never lowers). Sufficient for Claude (SDK forks keep original record timestamps).
- Codex boundary verified from primary sources (openai/codex tag rust-v0.164.0):
  - `rollout/src/recorder.rs` `JsonlWriter::write_rollout_item` stamps every line with `now_utc()`, so copied items carry the fork time.
  - `core/src/session/mod.rs` (`InitialHistory::Forked`, `ForkPersistence::Copied`): copied items plus one `thread_settings::applied_event` are appended in a single `persist_rollout_items` call, so the fork's own snapshot is the last record of the batch.
  - `protocol/src/protocol.rs` `ThreadSettingsAppliedEvent.thread_id`: "copied snapshots retain their original owner's ID"; `rollout/src/policy.rs` persists `ThreadSettingsApplied`. Wire shape: `{"type":"event_msg","payload":{"type":"thread_settings_applied","thread_id":...}}` (`history/src/rollout_payload.rs`).
  - Boundary = offset of the first `thread_settings_applied` event whose `thread_id` is this session. `forked_from_ordinal_exclusive` exists only for paginated history and is not used.
- Thin adapter: `IdentityHistoryReader` records `Harvest.ForkBoundary` (Codex), persists `SessionIdentity.ForkBoundaryOffset` (-1 = unknown, reset with `HistoryState.Reset`), and `Merge` drops calls before it for a proven fork. The fork's own first turn follows the boundary and is kept.
- Remaining unverified (not a restriction): no live `codex fork` capture; a rollout without the event (older versions, reference-backed forks) uses the floor only. Closing evidence: a real `codex fork` rollout at 0.164.0 showing the copied prefix followed by the fork-owned event. Recorded in `Capabilities`, `docs/compatibility.md` and `docs/session-identity.md`.
- Fixture `tests/BellTower.Tests/Fixtures/identity/codex-rollout-0.164.0-fork-boundary.jsonl` (+ provenance). Tests in `IdentityRecoveryPolicyTests`: invalidated fork (Claude, Codex) gets `ActiveWork == null` and accepts post-fork work; mixed pair; ordinary Codex inheritance unchanged and floor max (never lowers). Disabling the boundary filter makes the Codex and mixed tests fail (mutation check).

## ANALYSIS-102
Removed `PendingForkTitle` and `NativeTitleAt` (`SessionIdentity`, `HistoryState`, `NativeNamePolicy`); `FromParent` returns `void`; comment on the `NativeNamePolicy.Ours` branch in `IdentityHistoryReader`. Old files keep the values through `JsonExtensionData`. `grep` finds no references in `src`/`tests`.

## ANALYSIS-151
`AlertPolicy.Phrase` uses `identity.Name`.

## Checks
- `dotnet build -c Release --no-restore` -> passed, 0 warnings
- `dotnet test -c Release --no-build --no-restore` -> passed 670/670
- `dotnet format ... --include <changed C# files>` apply then `--verify-no-changes` -> passed
- `node --test vscode/service.test.js vscode/focus.test.js` -> passed 8/8
- Temp sources fetched under `artifacts/impl-007-src` were deleted.
