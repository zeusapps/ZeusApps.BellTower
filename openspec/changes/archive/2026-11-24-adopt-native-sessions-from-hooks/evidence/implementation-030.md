# Implementation 030: fix cycle 2 for DEV-030 (task 2.5, owned-runtime restore)

Developer assignment DEV-030 on top of `5e55981`. Fixes await independent verification; nothing here closes a finding or ticks a task.

## QA-2400: managed Codex backends behind a launcher bind again

Problem: `CodexRuntimeHost` starts the backend as a native image, through `node codex.js app-server` (npm), or through `powershell -EncodedCommand` (script shim). The hooked `codex.exe` is then a grandchild or deeper descendant of the `bell-tower codex-runtime-host` manager, and the 2.5 direct-parent proof left those sessions shared.

Fix (record-based, never by image name):

- `AttachmentEvidence.AgentAncestors` (wire form): the hooked agent's nearest ancestors, direct parent first, each with its creation time. `AttachmentCapture.Capture` builds it from the hook's one `ProcessTable` (no extra snapshot), stopping at the first ancestor that cannot be identified, and takes at most `NativeAdoption.MaxOwnedDepth` hops. `AgentParent` is now its first element (one fewer probe).
- `NativeAdoption.Reconcile` (Core, pure) accepts the owned-runtime proof when the record is for this attachment (as before), manager and record shell are live, and `ManagerBehindAgent` finds the record's manager (PID and creation time) within the first `MaxOwnedDepth` hops. Every hop must be consistent in time: an ancestor never starts after its child, which rejects a reused PID at any hop, including the manager. The previous direct-parent equality is the one-hop case.
- Bound `MaxOwnedDepth = 3`, from the launch shapes: native (manager, backend: 1), npm (manager, node, backend: 2), PowerShell shim that starts node (manager, powershell, node, backend: 3). A deeper chain is not a Bell Tower launch shape and stays shared.
- Why a descendant of the manager cannot be an independent shared backend: the manager is not an ancestor of the user's shell or any user-started client. A backend under a Codex client keeps its mode `shared-backend` (the proof only runs for provisional `unknown` captures). Residual: a hook from a model-started nested `codex app-server` inside an owned backend would also have the manager as an ancestor; the existing `profile` guard (a different process already recorded for the attachment) keeps it shared once the real backend has been seen.
- Claude: no owned runtime; its managed rows are unchanged and still never read a record.

## Rows

`NativeOwnedRuntimeProcessTests` (real stand-ins, both entry points for positives and Claude, alternating for negatives): 15 -> 28 runs.

- Positive: native; npm (manager -> node -> codex app-server); shim (manager -> powershell -> codex app-server); depth 3 (powershell + node). Host shell is the record's, name service writes.
- Negative: depth 4 (shared); no record, wrong manager (a live non-ancestor process), wrong creation time, dead manager (backend outlives it), dead shell, record for another attachment, each direct and behind a launcher; independent backend under a plain shell with a live decoy manager and shell recorded (direct and behind node); backend under a Codex client with a live record (`shared-backend`).
- Claude managed with and without a record: unchanged.
- The old `wrong-pid` row used the test process as the manager; it is an ancestor of every stand-in, which the new rule correctly accepts, so the row now records a live non-ancestor.

`OwnedRuntimeReconcileTests` (policy): 10 -> 22 runs. Adds bound rows (manager at hop 1, 2, 3 bind), manager at hop 4 and a longer chain, and failure rows for no record, other attachment, wrong PID, reused manager, dead manager, dead shell, a hop that started after its child (reuse), a manager that started after the hop it launched (reuse), independent ancestry and an empty chain. The intermediate-hop reuse rows are policy-only; a real process cannot be made to start before its parent.

## Mutation checks (each restored; failures in `NativeOwnedRuntimeProcessTests` + `OwnedRuntimeReconcileTests`, 53 tests)

| Mutation | Failures |
| --- | --- |
| `MaxOwnedDepth` 3 -> 4 | 3 |
| per-hop creation check disabled | 2 |
| manager matched by PID only | 2 |
| manager matched by any hop | 13 |

All killed.

## ANALYSIS-1601: polling loop

The backend stand-in polls with `ping -n 1` instead of `ping -n 2`. The deadline stays 25 s. Timing of the class alone (28 rows, same rows both ways, machine under normal load, noisy): `-n 2` 59 s; `-n 1` 44 s and 28 s on two runs. The earlier 15-row class took 24.6 s (analysis-017).

## ANALYSIS-1602: stand-in helpers

`Testing/StandIns` now holds `CopyImage` (optional folder and image name), `Hook`, `Under`, `ScrubEnvironment`, `WaitFor(path, process?)` and the 25 s `Deadline`. `NativeAgentResolveProcessTests`, `NativeAgentRelationshipProcessTests`, `NativeOwnedRuntimeProcessTests` and `ProcessTableTests` use them; their local copies are gone. Counts and results unchanged (resolve, relationship and process-table classes together with the navigation and maintenance reruns: 90 passed).

## Count correction (ANALYSIS-1505)

implementation-029 said 28 relationship runs; the data produce 29 (17 nested, 10 held, 2 mixed), as analysis-017 counted. The evidence text there was off by one; the rows are as described.

## Results

- Release build: 0 warnings, 0 errors.
- Full suite once: 1425 passed, 2 failed (`VsCodeWindowNavigationTests.VerifiedWindowIsActivatedThenTheExactTerminalIsRevealed(codex)`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`): known load-sensitive, unrelated paths. Both classes passed on a rerun alone (together with the relationship, resolve and process-table classes: 90/90).
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`: clean (my files formatted with `--include`).
- `dotnet build-server shutdown` done. No testhost, stand-in or other process of mine remains; the user's `bell-tower` processes were not touched.

## Limits

- Not run: the permission pump itself (asserted by its identical lookup predicate), the packaged ingress benchmark, real `claude`/`codex`, the live instance.
- The wire evidence gains up to 3 identities per provisional capture only; no other consumer reads `AgentAncestors`.
- Timing figures are single noisy runs.
