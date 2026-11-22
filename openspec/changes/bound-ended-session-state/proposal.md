# Proposal

## Why

Monitoring state grows without a bound until it reaches the 16 MiB snapshot budget, and then the tray stops working. Every ended session keeps its full ordering journal (up to 256 events), a checkpoint copy and its completed-operation set for 30 days, about 300 KB each. On 2026-11-15 the maintainer's state reached the budget with 93 ended sessions: every later save was refused, the tray logged a bare `tray-refresh-failed` on every tick, and it processed no hook from Claude Code or Codex for six days. Nothing reported the failure. A newly started tray could never complete its first refresh, so it never confirmed ownership and the dev-build switch failed.

## What Changes

- An ended session that has stayed quiet for a short settle interval is compacted. Its ordering journal is folded into its current state, its journal, checkpoint and completed-operation memory are dropped, and an ordering floor is kept so that delayed earlier events stay ignored.
- Ended sessions are capped by count as well as by the existing 30-day age, with the oldest removed first.
- A save that would exceed the budget removes the oldest ended sessions until it fits, so it no longer fails forever. Monitoring refuses a save only when live sessions alone exceed the budget, and then reports that clearly.
- Existing oversized state recovers automatically on the first drain after upgrade. Queued captures are consumed and no manual edit is needed.
- Refresh failures are logged with a bounded, sanitized reason, rate-limited instead of logged on every tick. Doctor reports state size against the budget, live and ended session counts, and a persistent refresh failure.
- The behavior is identical for Claude Code and Codex because both use the shared state store.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-tracking`: adds a requirement that monitoring state stays within its budget and that ended-session retention never stops event processing, with diagnostics for both providers.

## Impact

- Code: the shared state store and its snapshot save path (`src/BellTower/IO/StateStore.cs`, `StateSnapshotStore.cs`), the causal session reducer (`src/BellTower/Core/CausalSessionReducer.cs`) for compaction, the tray refresh error path (`src/BellTower/Tray/TrayApplication.cs`), and the doctor report (`src/BellTower/Diagnostics/DiagnosticsReporter.cs`).
- Data: the `state.json` schema is unchanged. Compacted sessions use existing fields, so older builds can still read the state. A dev or stable rollback stays compatible.
- Docs: diagnostics and architecture notes on state retention.
- No change to hook ingress, capture format, provider integrations or installation.
