# Implementation 009 (user-authorized one-clause round)

Fixes awaiting independent verification: ANALYSIS-250, QA-300. Target in: `diff=a3ad7d8486b6 untracked=51/39bb3b437966`.

## Changed files
- `src/BellTower/Sessions/IdentityHistoryReader.cs`: removed the `fork:*` clause; exclusion and late-boundary retraction gate on `WorkFloorAt is not null` only.
- `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs`
- `docs/session-identity.md`, `docs/compatibility.md`
- this handoff

## Tests
- `ACodexForkInherits...` is now a Claude+Codex theory (`AForkInheritsTheParentsLiveWorkAndRecordsThatForkBoundaryAndFloorNeverLower`) asserting `ActiveWork == "change-a"`.
- Stale-parent (strong change-b older than the copies) and weak-parent (weak change-b skill): Claude, Codex theory plus a mixed pair; all resolve change-a.
- QA-300 growth and growth-large, Claude and Codex, parent live change-b at t=5: outcome identical before and after the boundary arrives (no timing dependence).
- Residual (parent live work at t=90, newer than every copy, other change): both clients keep change-b.

## Outcome notes (open, honest)
- QA-300 shape (parent live work newer than Claude's original copy stamps but older than the fork time): Claude keeps the parent's change-b; Codex takes change-a, because Codex restamps copies with the fork time and the shared newer-evidence rule prefers them. Timing-independent on both, but not the same value. This is a restamp consequence of one shared rule, documented in both docs; making it equal would need provider-specific precedence, which ANALYSIS-250 rejects. Orchestrator decision needed if exact parity is required.
- The stale-parent test uses a parent stamp before the copies (2026-10-23T11:00Z) so it is older than both clients' copies.

## Mutation (temp copy `D:\Temp\bt-i9`, working tree untouched, copy deleted)
With the old clause restored, `IdentityRecoveryPolicyTests` fails 3 of 59: `AnUnreadParentWithOlderLiveWorkOnAnotherChangeYieldsToTheNewerCopiedHistory` (codex, weak true and false) and `AMixedPairOfForksOfStaleAndWeakParentsResolvesTheCopiedHistoryForBothClients`. Claude variants pass (unaffected).

## Checks
- Release build: 0 warnings, 0 errors
- Full tests: 692/692 (680 before)
- dotnet format apply and verify-only on the 2 changed C# files: passed
- node tests: 8/8
- Temp `artifacts/impl-009-tmp` and `D:\Temp\bt-i9` deleted; no disk-space failures.
