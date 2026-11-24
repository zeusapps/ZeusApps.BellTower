# Implementation 031: DEV-031 hardening (task 2.5, owned-runtime proof)

Developer assignment DEV-031 on top of `e0a69e6`, user-approved after two fix cycles. Fixes await independent verification; nothing here closes a finding or ticks a task.

## QA-2500: a nested app-server never takes the managed attachment

- New additive wire input `AttachmentEvidence.AgentParentIsAgent`. `AttachmentCapture.Capture` sets it from the hook's one process table: the hooked agent's direct ancestor entry has the provider's image name. No extra probe or snapshot.
- `NativeAdoption.Reconcile` (Core, pure) adds `!pending.AgentParentIsAgent` to the owned-runtime proof. It only rejects; nothing is accepted by image name, and the record plus manager-in-ancestry proof is unchanged. Recorded-launch and profile-shell proofs are untouched.
- Claude has no owned runtime; its managed rows are unchanged.
- Rows:
  - `NativeOwnedRuntimeProcessTests.ANestedAppServerInsideTheOwnedBackendStaysSharedInEitherHookOrder` (4 runs, both entry points, both orders), stand-ins: manager -> backend `codex.exe app-server` -> nested `codex.exe app-server`, live valid record. Asserts the real backend binds to the managed attachment (process, record shell) and the nested session stays shared (no process, no shell, mode unknown) in nested-first and backend-first order.
  - Policy: `AHookedAgentWhoseDirectParentIsAnAgentIsRejectedEvenWithTheManagerAbove` (manager direct and behind one launcher, rejected with the flag, binds without it).
  - Existing npm (node parent) and shim (powershell parent) positive rows still bind (class: 28 -> 32 runs, all pass).

## ANALYSIS-1700: proof inputs are not persisted

`NativeAdoption.Verified` clears `AgentParent`, `AgentAncestors` and `AgentParentIsAgent` (and `Provisional`) in all three success branches (owned runtime, recorded launch, profile shell). Field comments now say they are read by Reconcile, cleared when a proof succeeds, and may remain on a capture that stays provisional or falls back to shared. Asserted in `OwnedRuntimeReconcileTests` (direct and launcher positives), the recorded-launch test and the profile-shell test (`NativeAdoptionReconcileTests`), and on the stored attachment in the nested stand-in test.

## ANALYSIS-1701: malformed ancestry

- `ManagerBehindAgent` treats a null hop as the end of the chain (returns false, shared). Policy row `null-hop`.
- `CaptureProtocol.Validate` rejects an `AgentAncestors` list with a null hop, a non-positive pid or creation time, or more than 32 entries (literal cap; Protocol does not reference NativeAdoption) with the existing `envelope-invalid` outcome.
- `CaptureTests.AgentAncestorsEvidenceIsValidatedAndAnOlderCaptureWithoutItStillDecodes` (8 rows): absent field, valid, exactly 32 decode; null hop, zero pid, negative pid, zero ticks, 33 entries are `envelope-invalid`. The absent row confirms an older capture still decodes.

## Mutation checks (each restored)

| Mutation | Failures |
| --- | --- |
| remove the `AgentParentIsAgent` rejection | 4 (2 policy, 2 stand-in nested-first rows, both entry points) |
| null hop skipped instead of ending the chain | 1 (`null-hop`) |

## Count note

The reconcile policy class has 25 runs at `e0a69e6` (implementation-030 and later notes saying 22 were off); it now has 28 (adds `null-hop` and two parent-is-agent runs).

## Results

- Release build (whole solution including the NativeAOT-analyzed ingress project): 0 warnings, 0 errors.
- Full suite once: 1439 passed, 3 failed: `StatusLineFallbackTests.MissingSnapshotStillShowsThisConversationsBranchAndWorktree(codex)`, `CliTests.GeneratedPowerShellHookCommandPreservesUnicodeAndFinishesWithinItsTimeout`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`. Known load-sensitive timing tests on unrelated paths; rerun alone (10 tests with the same classes), all passed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`: clean.
- `dotnet build-server shutdown` done; no process of mine remains.

## Limits

Not run: the packaged NativeAOT ingress publish, real `claude`/`codex`, the live instance. `AgentParentIsAgent` is one more boolean on provisional wire evidence only.
