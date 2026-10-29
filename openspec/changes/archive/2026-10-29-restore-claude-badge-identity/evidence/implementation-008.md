# Implementation 008 (user-authorized tiny fix round 4)

Fixes awaiting independent verification: ANALYSIS-200, QA-250. Baseline d307c34.

## Changed files
- `src/BellTower/Sessions/IdentityHistoryReader.cs` (Merge)
- `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs`
- `docs/session-identity.md`, `docs/compatibility.md` (fork wording only)
- this handoff

## ANALYSIS-200
The Codex fork-boundary exclusion in `Merge` now applies only when a branch invalidation is known (`WorkFloorAt` set, inherited or own). It also applies when the fork's work already came from parent inheritance (`WorkSource` starts with `fork:`): without that, restamped copies replaced the inherited `fork:Write` work with `history:edit`, so the existing test `ACodexForkInheritsTheParentsLiveWork...` (kept green, still `fork:Write`) required it. With no invalidation and no inherited work (orphan, unread parent), a Codex fork uses its copied history as a Claude fork does.

## QA-250
When the exclusion applies and applied work has `history:*` source with `HistoryWorkOffset` below the boundary, it is cleared through `WorkPolicy.Clear(WorkClear.History)` (live work and the floor are untouched). Claude has no analogous path: it has no boundary and its copied records keep their original stamps, so the floor alone filters them and nothing is applied early.

## Tests added (all green)
- `AForkWithNoKnownInvalidationUsesItsCopiedWorkForBothClients` (orphan and unread-parent, Claude and Codex, boundary fixture for Codex)
- `AMixedClaudeAndCodexPairOfForksWithNoKnownInvalidationBothResolveTheCopiedWork`
- `AForkWhoseBoundaryAppearsAfterItsCopiedPrefixWasReadEndsWithNoPreSwitchWork` (growth and growth-large, Claude and Codex; own post-fork work accepted)
- `AMixedPairOfGrowingForksRetractsOnlyTheCodexCopiedWorkAndKeepsLiveWork`

## Checks
- `dotnet build -c Release --no-restore`: passed, 0 warnings, 0 errors
- `dotnet test -c Release --no-build --no-restore`: passed 680/680 (670 before)
- `dotnet format ... --include <2 C# files>` apply then `--verify-no-changes`: passed
- `node --test vscode/service.test.js vscode/focus.test.js`: passed 8/8
- Temp `artifacts/impl-008-tmp` on D: deleted; no disk-space failures.

## Limits
No live `codex fork`. The growth tests use synthetic fixtures. Not run: tests were not separately shown to fail without the fix (the QA-250 retraction was not isolated by a revert).

Identity: `head=d307c34 baseline=d307c34 files=96 diff=a3ad7d8486b6 untracked=51/39bb3b437966`
