# Implementation 032: DEV-032 fix cycle 1 for DEV-031 (task 2.5, owned-runtime proof)

Developer assignment DEV-032 on top of `4ccc963`. Fixes await independent verification; nothing here closes a finding or ticks a task.

## ANALYSIS-1800: lineage cut replaces the parent-is-agent flag

- `AttachmentCapture.Capture`'s owned-lineage walk now stops at the first ancestor whose image is `<provider>.exe`, next to the existing stop at an unidentifiable ancestor. A nested agent's lineage therefore never reaches a manager through another agent of its provider, directly or through a tool shell. It only rejects; nothing is accepted by image name, and Core's record-based proof is unchanged.
- Removed: the `AttachmentEvidence.AgentParentIsAgent` wire field, the extra `NativeAdoption.Apply` parameter, the `Reconcile` conjunct and the clearing in `Verified`. `Verified` still clears `Provisional`, `AgentParent` and `AgentAncestors`. The field comments say the lineage ends before the first provider image.
- Effect on `AgentParent`: it is `chain[0]`, so it is null when the direct parent is a provider image. The profile-shell proof compares it with a recorded launch shell, which is never a provider image (shells are powershell/pwsh/cmd), so that proof is unaffected. The existing profile-shell rows pass unchanged.
- Rows:
  - `NativeOwnedRuntimeProcessTests.ANestedAppServerInsideTheOwnedBackendStaysSharedInEitherHookOrder` now has 8 runs (both entry points, both hook orders, direct child and tool-shell). The tool-shell shape is stand-ins manager -> backend `codex.exe app-server` -> `powershell.exe` (tool shell) -> nested `codex.exe app-server`, live valid record. The real backend binds (process, record shell); the nested session stays shared (no process, no shell, mode unknown).
  - Policy: `OwnedRuntimeReconcileTests.ANestedAgentWhoseLineageEndsAtTheBackendNeverReachesTheManager` (empty lineage and tool-shell-only lineage are shared; the real backend with 0 or 1 launchers binds). The Hosts cut itself is covered by the stand-in rows, because `Capture` reads the live process table.
  - Direct-child nested rows kept; native, node, powershell and powershell+node positives still bind; Claude managed rows are unchanged (all in the 36-run class, passing).

## ANALYSIS-1801

`CaptureProtocol.Validate` comment corrected (sanity cap equal to the envelope ancestor cap; policy reads at most `NativeAdoption.MaxOwnedDepth`). One named constant `CaptureProtocol.MaxAncestors = 32` is used by both clauses; the value is unchanged and the existing `CaptureTests` cap rows pass.

## ANALYSIS-1802

`NativeOwnedRuntimeProcessTests` has class-local helpers `WriteWaitThenHook`, `ManagerStart` and `WriteRecord`, used by the main theory and by the nested rows (direct and tool-shell). No helper moved to `StandIns`.

## Mutation check (restored)

Removing the provider-image cut in `Capture` fails 4 runs of the nested test: the nested-first rows of both shapes (direct and tool-shell) on both entry points. Backend-first rows pass either way, as in DEV-031 (the real backend adopts first, so the nested session finds the attachment already taken).

## Results

- Release build: 0 warnings, 0 errors. Focused (`NativeOwnedRuntimeProcessTests`, `OwnedRuntimeReconcileTests`, `NativeAdoptionReconcileTests`, `CaptureTests`): 137/137.
- Full suite once: 1444 passed, 2 failed, both known load-sensitive unrelated timing tests: `CliTests.GeneratedPowerShellHookCommandPreservesUnicodeAndFinishesWithinItsTimeout`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`. Rerun alone: 2/2 passed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`: clean.
- The Release build's implicit restore rewrote four `packages.lock.json` files (ILLink 10.0.13 to 10.0.14). They are outside the allowed paths, so I reverted them with `git checkout`; they are not part of this change. `dotnet build-server shutdown` done; no process of mine remains.

## Limits

Not run: the packaged NativeAOT ingress publish (the ingress project built with warnings as errors), real `claude`/`codex`, the live instance.
