# Implementation 021: fix cycle 2 for task 2.2

Developer assignment DEV-021, on top of [implementation-020](implementation-020.md). Baseline `2f1de7f`, uncommitted working tree. These changes address ANALYSIS-700 and QA-1400 and await independent verification.

## ANALYSIS-700: provisional captures are fail closed on the wire

- A provisional capture (unknown mode, managed identifier, hooked agent process, a provider that can share a backend) now leaves the ingress as `Dedicated=false` in the shared-runtime shape: session-derived identifier, no process, unknown host, no managed title or background, `shared-runtime` ownership, and no Name or Change in the envelope fields.
- The direct candidate (identifier, process, host, presentation flags, and the launch Name and Change) rides inside `evidence.candidate` together with `evidence.provisional` and `evidence.agentParent`. Only the new consumer can use it: `NativeAdoption.Promote` rebuilds the direct form, and `AttachmentStore.Adopt` keeps it only when `NativeAdoption.Reconcile` verifies the launch. Otherwise the result is the same shared shape the wire carried. `CaptureConsumer` and the legacy `HookIngestor` both promote this way.
- The record the new consumer persists never keeps `candidate`; `provisional` is cleared on verification. When a pre-change tray persists the shared record instead, the whole `evidence` object (including `candidate`, `provisional` and `agentParent`) can survive as extension data in the local attachment file, including the launch name and change text and the process and host identities. It stays inert: it is never promoted, because `Promote` reads only wire captures and `Reconcile` inspects only the incoming capture.
- Compatibility statement, corrected: implementation-020 said the provisional record exists only inside a capture envelope and is resolved before persistence. That reasoning ignored an older capture-v1 consumer receiving the envelope. With this change an older consumer sees a plain shared-runtime conversation with the candidate as inert extension data, exactly the DEV-019 fail-closed behavior, so no gate change is needed. A new tray with the candidate but no verifying launch record also fails closed.
- Test: `AProvisionalCaptureIsFailClosedOnTheWireForAPreChangeConsumer` decodes the encoded envelope as plain JSON (the fields a pre-change consumer reads) for a Codex provisional capture and asserts no process, unknown host, no presentation, shared ownership and shared identifier, null Name and Change, and the inert candidate. The paired Claude row shows Claude is never provisional and stays direct.

## QA-1400: profile launches

- The prepared launch record already stores the launching shell identity. `prepare-session` verifies that `BELLTOWER_SHELL_PID` (the module's `$PID`) is an ancestor, then `LaunchPresentationStore.Prepare` persists `Shell` as PID plus creation time. No format change was needed, so persisted records remain backward readable. `Adopt` reads it for a provisional capture through the existing `LaunchPresentationStore.Shell`.
- The ingress now captures the hooked agent's direct parent identity (the next ancestor, with creation time) as `evidence.agentParent`.
- `Reconcile` promotes a provisional capture when either the stored managed launch record's process is live and is the hooked agent (the `AgentLauncher` path, unchanged), or the recorded launch shell is live, has the same creation time as the hooked agent's direct parent, and no different live managed process is recorded. A nested client's parent is the outer agent's tool shell, a dead shell is not live, and a reused PID has a different creation time; all three fail closed.
- Profile-shaped tests for both providers: a direct child of the recorded shell is promoted for Codex (its binding, managed title and identity text are kept; Claude keeps its existing direct classification); a nested child, a dead shell and a PID-reuse mismatch fail closed for Codex (shared identifier, no process, host, presentation or inherited name) while Claude stays as before.

## Files changed

`src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/Providers/CaptureConsumer.cs`, `src/BellTower/Providers/HookIngestor.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs`.

## Limitations

- The direct-parent rule fails closed when an intermediate process sits between the shell and the hooked agent (for example an npm shim that starts `node` before `codex.exe`). That Codex profile launch with an unproven mode loses its binding; a known mode is unaffected.
- A profile launch outside VS Code and Windows Terminal, or with background coloring disabled, never calls `prepare-session`, so it has no launch record; with an unknown mode it fails closed. It has no host to bind in those cases.
- No installed profile launch, provider launch or mixed-version run was performed.
