# Implementation 025: lifecycle reduction and the persisted-semantics compatibility assessment (tasks 2.4 and 2.3)

Developer assignment DEV-025. Baseline `683ad12`, uncommitted working tree. Developer evidence awaiting independent QA and code-analyzer review; nothing here approves itself or closes a finding.

## Task 2.4: assessment and decision

**Decision: no new consumer gate is required for the semantics task 2.3 persists. `attachmentHealth` stays at 1.** The decision rests on one design property that the tests pin: the reducer never writes a legacy field that an older tray reads as an end or as liveness for a shared conversation. The gate becomes necessary under one condition in a later task (see "Constraint for task 4.2" below).

What an older (health-v1) tray reads, from `AttachmentHealth.Evaluate` and `StateStore.ReconcileAttachments`:

| Field read | Where | Effect |
| --- | --- | --- |
| `attachment.EndedAt` | `AttachmentHealth.Evaluate` | any value yields `confirmed-ended`, the session is ended and its waits cleared |
| `attachment.Process` liveness | same | `Ended` yields `confirmed-ended`, `Live` yields `verified-live` (the only state that allows reminders, escalation and host actions) |
| `attachment.Host.Shell` liveness | same | `Ended` yields `confirmed-ended` |
| `attachment.Health`, `UnverifiedSince`, `RegisteredAt` | same | with no process, `unverified` until the grace, then `unavailable`; `unavailable` is sticky |
| `attachment.Evidence` | nothing | unknown property, kept as extension data and written back unchanged |

What the new reducer persists, for a shared conversation (`Process == null`, lifecycle owner `shared-backend`):

- `evidence.terminal.binding` (`connected`, `disconnected`, `demoted`, or the default `unverified`), `source` (the reason), `client`, `orderingKey`, `observedAt`. All inside the already additive `evidence`, which an older tray never reads.
- Legacy `Health` (`unverified` or `unavailable`) and `UnverifiedSince`, with their unchanged meaning (the time of the latest evidence, then the same grace).
- Nothing else: no `Process`, no `Host.Shell`, no `EndedAt`, no new `Health` value, and no new session-level value. A terminal disconnect or latest-only demotion is not an ended session in any field.

Misread analysis:

1. Ended: an older tray ends a session only through `EndedAt`, a dead `Process` or a dead `Host.Shell`. A shared record has none of them, so a disconnected or demoted record cannot be read as ended. For directly attached processes (Claude, embedded Codex, owned runtimes) disconnect means the agent process is gone, which is the existing `confirmed-ended` rule in both trays, so the meaning is identical.
2. Live or actionable: a shared record has no process, so an older tray can never derive `verified-live` for it. Its best result is `unverified`, becoming `unavailable` after the grace. That is today's behavior for shared Codex and is protective (no queued reminder, no host action). The new tray keeps such a conversation `unverified` while it has independent evidence (a recent hook, or fresh exact-conversation runtime status showing active work); an older tray applies the old grace to the same `UnverifiedSince` and is at worst more conservative. Rollback loses attention for continuing shared work, it never fabricates it.
3. Persisted state: `state.json` keeps `attachmentHealthVersion: 1` and no new session field is written, so the existing gate (`AttachmentHealthCompatibility`, `CaptureCompatibility.EnsureTrayInvocation`, `UpdateTransaction`) already rejects a legacy consumer and accepts health-v1. The 2.1 evidence generation stamp and fail-closed encoding are unchanged: a mismatched or future-revision stamp is treated as absent and the conservative derivation is used.

Constraint for task 4.2 (gate trigger, proposed planning edit below): if the shared-Codex binder persists the bound client's shell in the legacy `Host.Shell` field of a shared conversation, an older tray would treat a closed terminal as `confirmed-ended` while the backend work continues. That would be a real misreading and would require an `attachmentHealth` 2 gate in the existing mechanism (the `Ensure` check accepts only 0 and 1 today, as do `ReleaseContract.Validate` and the `StateSnapshotStore` validity check). The binder must therefore keep the client and shell in `evidence.terminal` only, or add the gate in the same change. The new reducer already treats an exited `Host.Shell` of a shared record as a terminal disconnect (`shell-exited`) instead of an end, so the current tray is correct either way.

Verification (all in `Providers/ConversationLifecycleTests`):

- `AnOlderConsumerNeverReadsADisconnectedOrDemotedSharedRecordAsEndedOrLive`: for owner-killed idle, owner-killed active, demoted and never-bound records, asserts the invariants above, decodes each record through an older-shaped type with no `evidence` property (the evidence survives as extension data and round-trips), and runs the unchanged legacy evaluator over the record: `unverified` within the grace and `unavailable` after, never `confirmed-ended` or `verified-live`.
- `TheLifecycleReducerNeverWritesALegacyEndOrLivenessFieldForASharedConversation`: end-to-end `StateStore.Drain`; the stored record has no process, shell or end; `attachmentHealthVersion` stays 1; `AttachmentHealthCompatibility.Ensure` accepts consumer 1 and still rejects consumer 0.
- `RollbackToALegacyConsumerIsStillRejectedAndSharedAndManagedSessionsArePreserved`: with a persisted disconnected shared session and a managed Claude session, `UpdateTransaction.Stage` to a legacy-contract release is rejected with the health-v1 message; selection, `state.json` and the shared attachment file are byte-identical; a health-v1 successor is accepted and reads both sessions.
- `LegacySharedRecordsWithoutEvidenceKeepTheirBehaviorAndGainOnlyAdditiveEvidence`: a pre-evidence shared record with an unknown field keeps its meaning (`unavailable` past the grace), keeps the unknown field and gains only additive evidence.

## Task 2.3: what is implemented

One shared policy, `ConversationLifecycle` (`src/BellTower/Core/ConversationLifecycle.cs`), with thin provider adapters behind `IConversationEvidenceSource`:

- Directly attached processes (Claude, embedded Codex, owned runtimes, profiles, launch functions): `ConversationLifecycle.Evaluate` delegates unchanged to `AttachmentHealth`. Exit and idle rules, grace and stickiness are the existing ones.
- Shared conversations (`Process == null`, lifecycle owner `shared-backend`):
  - Terminal side from `SharedTerminalFacts` (owner client PID plus creation time, `codex_tui` listener present, backend connection present, latest-only demotion, ordering key, observation time). Disconnected when the owner client identity is gone (`owner-exited`, checked by the reducer itself so it also works from the stored client after a Bell Tower restart with no new facts), the listener is gone (`listener-gone`), the backend connection is gone (`backend-disconnected`) or the recorded shell exited (`shell-exited`); demoted by the latest-only rule (`latest-only`); connected only when the owner is live and both listener and backend connection are present. An unreadable owner or incomplete facts change nothing. `ReportedRuntimeStatus` is carried for diagnostics and never read.
  - Facts older than the recorded `observedAt` are ignored, so delayed or out-of-order observations cannot overwrite newer state; a newer live-owner observation reattaches.
  - Never-bound conversations (no owner, no stored client) stay `unverified`, never disconnected.
  - Work side from `RuntimeWorkFacts` (exact-conversation status only) and `HookActivity` (from the reduced session). Runtime status older than 2 minutes, or from the future, is not evidence. The newest fresh signal decides, ties favor the runtime. Only fresh active runtime status extends the conversation's evidence time; an idle or merely loaded thread extends nothing. Hooks extend it through the existing conversation observation time.
  - Result: the legacy `Health` is `unverified` while evidence is within the grace and `unavailable` after (no longer sticky: fresh evidence reinstates `unverified`), `UnverifiedSince` is the latest evidence time. A disconnect, demotion or loss of evidence never sets `EndedAt` or ends the session; completion remains only an explicit end event or, for direct processes, the process exit.
- Wiring: `StateStore` takes an optional `IConversationEvidenceSource` (default `NoConversationEvidence`, which makes the binder and transport inputs absent) and an optional process liveness delegate. `ReconcileAttachments` calls `ConversationLifecycle.Evaluate` in place of `new AttachmentHealth().Evaluate`; nothing else in the tray changed. Host navigation and title writers already require a verified process, so a shared conversation has no host action whether connected or not.
- `EvidenceStatus` gained `Connected`, `Disconnected` and `Demoted`.
- Deliberately not implemented: the shared-Codex binder (4.2), the non-owning transport (3.x), runtime-status polling and any provider adapter that produces the facts. The interface and records are the seam; the binder and transport supply `SharedTerminalFacts` and `RuntimeWorkFacts` later.

## Files changed

- `src/BellTower/Core/ConversationLifecycle.cs` (new), `src/BellTower/Core/AttachmentEvidence.cs` (three status constants), `src/BellTower/IO/StateStore.cs` (optional source and liveness delegate, one call site).
- `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs` (new).
- No docs were changed: the assessment found no compatibility text that needed an edit (`docs/attachment-health.md` Data compatibility remains accurate: the state schema is unchanged and the new evidence is additive).

## Tests added (20 cases; fakes only, no provider launched)

| Test | Proves |
| --- | --- |
| `ADirectAgentCrashEndsTheSessionThroughTheExistingRule` (claude, codex) | direct crash ends the session as before; terminal and conversation end together |
| `AQuietLiveProcessStaysVerifiedLiveWithoutHooks` (claude, codex) | hours without hooks do not end a live process |
| `ASharedOwnerKilledWhileIdle...` | disconnected, retained, never ended, later only `unavailable` |
| `ASharedOwnerKilledWhileActive...` | disconnected, runtime-confirmed work keeps it `unverified` past the hook grace |
| `AConnectedStatusWithoutAListenerOrBackendConnectionIsStillDisconnected` (2 rows) | `runtimeStatus: connected` is ignored; listener or backend loss disconnects; both present connects |
| `LatestOnlyDemotionIsNotCompletionAndWorkContinues` | demoted, not ended, work continues |
| `ANeverBoundSharedConversation...` | stays terminal-unverified; attention continues with fresh work evidence; expires only without evidence |
| `StaleRuntimeEvidenceIsNotWork` | stale and future runtime status are not work |
| `ARetainedIdleBackendThreadIsNeitherLiveNorConnected` | an idle loaded thread extends nothing and proves no terminal |
| `DelayedAndOutOfOrderFactsNeverOverwriteNewerState` | late disconnect or connect facts are ignored; newer ones reattach |
| `AnOwnerWithAReusedPidIsGoneAndAnUnreadableOwnerIsNotClaimedDisconnected` | PID plus creation time with the real probe; unreadable owner unchanged |
| `BellTowerRestartKeepsDisconnectedState...` | restart keeps state, episode, palette; reattachment; owner death noticed from the stored client |
| `MixedSimultaneousClaudeAndCodexSessionsAreDecidedIndependently` | live Claude, crashed embedded Codex and a disconnected-but-working shared Codex in one drain |
| `LegacySharedRecordsWithoutEvidence...` | pre-evidence records unchanged in meaning |
| four 2.4 tests | listed above |

Mutation checks: skipping the listener rule and widening the runtime freshness made four tests fail (stale runtime, out-of-order, listener, restart); restored afterwards.

## Commands and results

- `dotnet build -c Release --no-restore` -> 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore` -> 1252 passed, 0 failed (4 m 7 s). This includes the 20 new cases.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` -> clean (the formatter was first run limited to my four files to normalize line endings).
- No real `claude` or `codex` and no model service was contacted; no tray, desktop setup or user data directory was touched. All processes the tests start are fakes or short-lived and disposed.

## Review identity

See the handoff block for the `./scripts/review-identity.ps1 -Baseline 683ad12` summary line.

## Known limits

- The binder and runtime transport do not exist, so no live system produces `SharedTerminalFacts` or `RuntimeWorkFacts`; behavior with real providers is untested and listener, backend-connection and demotion semantics rest on the fakes plus decision 2/3 and probe 011.
- A quiet but live shared conversation with a long human wait and no runtime evidence still becomes `unavailable` after the grace (the existing behavior). Keeping it actionable needs the runtime transport (3.x).
- Reconciliation runs on drain; there is no independent timer, so a disconnect is noticed at the next drain.
- Hook-derived activity (`HookActivity`) only reports work status; it extends visibility through the conversation observation time, not through a separate rule.
- Diagnostics and the capability matrix do not yet show the new terminal states (task 5.2).

## Proposed planning edits (for the orchestrator)

1. Task 2.4: mark complete with this assessment (decision: no gate for 2.3's records).
2. Task 4.2: add "persist the owner client and shell only in `evidence.terminal`; writing a shared conversation's `Host.Shell` (or any other legacy end or liveness field) requires an `attachmentHealth` 2 gate in the existing mechanism first, verified by an older-consumer test".
3. Design Risks: the "older tray misclassifies shared conversations" risk can reference this assessment as resolved for 2.3's records and open only for the 4.2 constraint.
