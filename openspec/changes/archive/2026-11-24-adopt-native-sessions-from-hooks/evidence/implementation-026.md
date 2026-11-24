# Implementation 026: fix cycle 1 for DEV-025 (tasks 2.3 and 2.4)

Developer assignment DEV-026. Baseline `683ad12`, uncommitted working tree. Fixes awaiting independent verification; nothing here closes a finding.

## Per-finding fix and test

All tests are in `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs` (27 cases now, up from 20). Claude and Codex direct rows, shared Codex and a mixed simultaneous drain are all still covered.

| Finding | Fix | Test |
| --- | --- | --- |
| ANALYSIS-1205 | `AttachmentHealth` moved from `IO` to `Core` and is the one evaluator, with injected liveness only (no default probe, so no `Core` to `IO` call). Non-shared records take the original code path unchanged. A shared record without `EndedAt` goes to `ConversationLifecycle.Reduce`, which no longer calls `AttachmentHealth` (one dependency direction). `IConversationEvidenceSource`, `NoConversationEvidence`, `HookActivity` and `LifecycleResult` are gone. | `AttachmentHealthTests` and the legacy cases pass unchanged; the two `new AttachmentHealth()` calls in `AttachmentEvidenceTests` now pass `ProcessProbe.Inspect` explicitly. |
| ANALYSIS-1200 | The seam is a read-only data snapshot `ConversationEvidenceSnapshot` (session key to `ConversationFacts(Terminal, Work)`), passed as `StateStore.Drain(snapshot)` and consulted by `facts.Find(session.Key)`. Nothing in the drain or the policy can block; the caller fills it before the locks. No pump, binder or transport was built. A missing entry changes nothing. | Every StateStore test builds a `FakeEvidence.Snapshot()`; drains without a snapshot (restart rows) leave stored state unchanged. |
| ANALYSIS-1201 | `SharedTerminalFacts` now carries `Outcome` (connected, disconnected, demoted, unverified) and `Reason`, plus `Shell`. `FromEndpoint` is the shared mapping from listener, backend and demotion observations; an incomplete endpoint maps to unverified `incomplete-endpoint`, never to disconnected. A stored `connected` ages to unverified `stale-connection` after `TerminalFreshness` (5 min) without a new observation (observation time unchanged). Owner PID plus creation death is still detected with no facts. | `AnIncompleteEndpointObservationIsUnverifiedNotDisconnected`, `AStoredConnectionAgesToUnverifiedWithoutFactsAndOwnerDeathIsStillDetected` |
| ANALYSIS-1202 | The shell-exit rule reads `evidence.terminal.Shell` only, applied after any outcome; `Host.Shell` of a shared record is never read. Facts carry a shell; a different owner replaces or clears it, the same owner keeps it unless the facts name one. | `AnExitedEvidenceShellDisconnectsTheTerminalAndHostShellIsNotRead`, `AnOwnerChangeReplacesOrClearsTheRecordedShell` |
| ANALYSIS-1203 / QA-2000 | The evidence time is the latest of `ConversationObservedAt`, `UnverifiedSince`, `RegisteredAt` and fresh active runtime time (clamped to now), so it only moves forward and runtime-confirmed work gets the configured grace. `StateStore` takes an optional `TimeProvider` (default system) used by the reconcile step only, so the two-drain test is deterministic. | `RuntimeConfirmedWorkKeepsTheConfiguredGraceWhenFactsPauseAndTheEvidenceTimeNeverMovesBack` (active at t0, no facts at t0+3 min with the default 10 min grace is `unverified`, `UnverifiedSince` not earlier, `unavailable` at t0+11); `ANeverBoundSharedConversation...` rewritten to the same shape with a 1 min grace |
| ANALYSIS-1206 | Revival is the behavior (an exact-conversation hook moves `ConversationObservedAt` forward, the reducer is not sticky). Documented in `docs/attachment-health.md` (new "Shared conversations" section, including that rollback to an older tray restores its permanent `unavailable` for shared conversations). Direct and other unknown-owner records keep the existing no-revival rule. | `AnExactConversationHookRevivesAnUnavailableSharedConversationToUnverifiedNeverVerifiedLive` (through `AttachmentStore.Adopt` plus `StateStore.Drain`; also asserts a direct unknown-owner `unavailable` record stays unavailable after the same hook) |
| ANALYSIS-1204 / QA-2003 | One hook-freshness input (`ConversationObservedAt`). `HookActivity`, the unused work result and the direct-record terminal mapping are removed; tests assert stored state. The runtime work signal is used only as "fresh active" evidence time. Task 5.2 adds diagnostics later. | all tests assert stored attachments or session health |
| ANALYSIS-1207 / QA-2001 | `AttachmentEvidence.IsUnusableRevision`: when the stored evidence has a revision other than the current one, the drain updates only `Health` and `UnverifiedSince`, and `AttachmentStore.Adopt` keeps the stored `Evidence` object as is. | `StoredEvidenceWithANewerRevisionKeepsItsBytesThroughADrainAndAHookMerge` (revision 2 with a future facet, drain with no facts and a hook merge; evidence JSON byte-identical) |
| QA-2002 | New tests for shell-exited (evidence shell), revival, aging and the others above. The tie-break between runtime and hook no longer exists after ANALYSIS-1204, so no tie test. | see mutation checks |
| QA-2004 | Full suite run on an otherwise idle machine, see Commands. Neither flaky test reproduced. | n/a |

## Mutation checks (single-line mutation, run `ConversationLifecycleTests`, restored afterwards)

| Mutation | Killed by |
| --- | --- |
| shell-exited rule disabled | `AnExitedEvidenceShellDisconnects...` |
| `unavailable` made sticky for shared records | `AnExactConversationHookRevives...` |
| `UnverifiedSince` dropped from the evidence time (can move back) | `RuntimeConfirmedWorkKeeps...`, `ANeverBoundShared...` |
| stored-connection aging disabled | `AStoredConnectionAgesToUnverified...` |
| owner change keeps the former shell | `AnOwnerChangeReplacesOrClearsTheRecordedShell` |
| unverified outcome mapped to disconnected | `AnIncompleteEndpointObservationIs...` |
| drain rewrites unusable-revision evidence | `StoredEvidenceWithANewerRevision...` |
| `AttachmentStore.Adopt` merges unusable-revision evidence | `StoredEvidenceWithANewerRevision...` |

All eight mutations failed at least one test; originals restored (build and full suite run afterwards).

## Commands and results

- `dotnet build -c Release --no-restore` -> 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore` (idle machine, nothing else of mine running) -> 1259 passed, 0 failed, 4 m 14 s. `AudioQueueTests` and `HookMaintenanceTests` passed; QA-2004 did not reproduce.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` -> exit 0.
- Build servers shut down at the end; no `dotnet` or `testhost` process of this session remains. No real `claude` or `codex` and no model service was contacted; no user data directory or tray was touched.

## Files changed in DEV-025 and DEV-026

- `src/BellTower/Core/AttachmentHealth.cs` (moved from `src/BellTower/IO/AttachmentHealth.cs`), `src/BellTower/Core/ConversationLifecycle.cs`, `src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/IO/StateStore.cs`, `src/BellTower/IO/AttachmentStore.cs`.
- `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs`, `tests/BellTower.Tests/Providers/AttachmentEvidenceTests.cs`.
- `docs/attachment-health.md`.

## Review identity

`./scripts/review-identity.ps1 -Baseline 683ad12 -Paths` over the nine paths above (including the deleted `src/BellTower/IO/AttachmentHealth.cs`):
`head=683ad12 baseline=683ad12 files=9 diff=c3e23e41ad34 untracked=3/9f93fda55b44`

## Limits and decisions needed

- `ProcessLiveness` was NOT moved into Core. `scripts/install-dev.ps1` (lines 98, 99, 229 and others) uses the type name `[BellTower.IO.ProcessLiveness]`, and a first attempt to move it failed 21 script tests. `scripts/` is outside this assignment, so the enum stays in `BellTower.IO`; `Core/AttachmentHealth.cs` and `ConversationLifecycle.cs` import `BellTower.IO` only for that enum. Finishing ANALYSIS-1205 fully needs an authorized edit of the script references (and the Ingress project link, `src/BellTower.Ingress/BellTower.Ingress.csproj`, which compiles `ProcessLiveness.cs` by path), or acceptance of the enum staying in `IO`.
- Rollback to an older tray: it never revives `unavailable` for shared conversations (permanent), so a conversation that a hook revived in the new tray reverts to `unavailable` there after the grace. It never becomes live or ended.
- The snapshot is keyed by session key; the real binder and transport (tasks 3.x and 4.2) will fill it. Behavior with real providers remains untested.
- Task 5.2 diagnostics for the terminal facet are not done.
- Reconciliation still runs on drain only; no independent timer.
