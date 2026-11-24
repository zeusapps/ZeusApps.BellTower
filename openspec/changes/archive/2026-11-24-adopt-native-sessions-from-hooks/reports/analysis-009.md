<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-009 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-009 (recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent; client version not exposed) / delegated non-interactive subagent / `claude-opus-5-5`
- Date: 2026-11-04 07:35 UTC
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-009 (recheck after DEV-021, the final fix cycle)
- Task and requirement references: [tasks.md](../tasks.md) 2.2 and the 2.4 precursor; [design.md](../design.md) decision 1 and Risks; [implementation 021](../evidence/implementation-021.md); [analysis 008](analysis-008.md) ANALYSIS-700; QA-1400
- Scope and relevant neighboring paths:
  - DEV-021 changes: `src/BellTower/Core/{NativeAdoption,AttachmentEvidence}.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/Providers/{CaptureConsumer,HookIngestor}.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs`
  - The rest of the 24-file target for regressions
  - Neighbor: `src/BellTower/IO/LaunchPresentationStore.cs` (`Shell`, `Consume`)
- Baseline commit: `2f1de7f`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2f1de7f -Paths src tests tools .../implementation-019.md .../implementation-020.md .../implementation-021.md`
  - HEAD `2f1de7fc45c54c27a7bc236b9daac1bbc003b665`
  - Tracked diff SHA-256 `320bb160e78b6e4220b6e7d9a6ccec9e357aa8f288f9891324e6427a924bffdf`
  - 9 untracked files, including implementation-021 59111a5f..., `AttachmentEvidence.cs` efdbf3ba..., `NativeAdoption.cs` 036f61d1..., `NativeAdoptionReconcileTests.cs` 8fbacc2c...
  - Summary: `head=2f1de7f baseline=2f1de7f files=24 diff=320bb160e78b untracked=9/ceb5557d175e`
- Before/after identity: identical after the recheck. No target mutation.
- Prior reports / rechecked findings: [analysis 008](analysis-008.md) ANALYSIS-700.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-700 recheck | Traced a provisional Codex capture through ingress, the wire, a pre-change consumer and the new consumer | See Re-verification | Verified. |
| QA-1400 design soundness | Read `Reconcile`, the parent capture, `Adopt`'s launch-shell lookup and `LaunchPresentationStore`. Checked the paths that should fail closed. | Proof requires the hooked agent's direct parent to equal the recorded launch shell (PID and creation time), the shell to be live, and no different recorded managed process | Sound (both clients; Claude never becomes provisional). |
| Duplication | Compared `CaptureConsumer` and `HookIngestor` | Both callers run the same short Promote -> Adopt -> identity-suppression sequence. The policy (`Promote`, `Reconcile`, `Shared`) exists once in Core. | No duplicated policy. |
| Layering | Namespaces and links | `Promote` and the proof are in Core. IO only supplies the recorded shell and a liveness callback. Ingress links are unchanged. The Core-to-IO `ContentHash` import from ANALYSIS-604 remains. | No regression. |
| Compatibility | Three combinations: pre-change capture-v1 consumer with the new ingress; new consumer with an older cached ingress image; new consumer with the new ingress | See Assessment | No defect; one wording correction. |
| Performance | Read the hot paths; reused the ANALYSIS-007 replica measurement | One extra `ProcessProbe.Identify` per hook for the agent's parent (about 11 us/op measured on the replica). A launch-record read only for provisional captures, under the same lock order (presentation, then attachments, then launch) already used by `Adopt`'s first-record path. | Negligible; no regression. |
| Build / test / format | Not run | Building was optional; `global.json` was left alone while QA-016 may be building | Not run. |

## Findings

**No findings.** The ANALYSIS-800 to ANALYSIS-899 range is unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-700 | `diff=320bb160e78b untracked=9/ceb5557d175e` | See below | Verified |
| QA-1400 (design soundness only) | same | See below | Design sound; functional closure is QA's |

### ANALYSIS-700

- **What leaves the ingress.** `NativeAdoption.Apply` (`NativeAdoption.cs:35-57`) returns a provisional capture as `Dedicated=false` in the `Shared` shape: session-derived id, no process, unknown host, no title or background flags, and `shared-runtime` ownership. `CaptureFactory` then nulls the envelope Name and Change, because the capture is not dedicated.
- **Where the direct form travels.** The direct candidate, plus the launch Name and Change (bounded in `AttachmentCapture.Resolve`), rides only in `evidence.candidate`.
- **Pre-change consumer.** It forces `shared-runtime` because `Dedicated` is false, so it adopts a shared conversation, matching DEV-019's fail-closed behavior.
- **New consumer.** `Promote` rebuilds the direct form only from a wire capture that is provisional and has a candidate. `Adopt` keeps that form only when `Reconcile` verifies it. Otherwise `Shared` produces new evidence without the candidate, and the id differs from the promoted id, so the consumer suppresses palette, name and change.
- **Tests.** `AProvisionalCaptureIsFailClosedOnTheWireForAPreChangeConsumer` covers the plain-JSON reader view, with paired Claude and Codex rows.

### QA-1400

- **The proof.** `Reconcile` (`NativeAdoption.cs:108-118`) promotes a provisional capture when either:
  - the recorded managed process is the hooked agent and is live (the `AgentLauncher` path), or
  - the launch shell recorded by `prepare-session` equals `evidence.agentParent`, the hooked agent's direct parent with its creation time (`AttachmentCapture.cs`, parent identity), is live, and no different managed process is recorded.
- **Why it fails closed where it should:**
  - A nested client's parent is the outer agent's tool shell.
  - The launch shell is busy running the agent, so it cannot start a sibling directly. A background job runs in a child process.
  - The shared daemon's parent is the TUI.
  - PID reuse changes the creation time, and a dead shell fails the liveness check.
- **Later hooks.** `Consume` only marks the palette consumed (`LaunchPresentationStore.cs:27`), so `Shell(id)` stays readable for later hooks. After the first promotion, the persisted managed record with its process also verifies those hooks through the recorded-process path.
- **Known gaps.** The npm shim (an intermediate `node` process) and launches without a record fail closed, as implementation 021 documents.

## Assessment and limitations

- **Compatibility:**
  - New ingress with a pre-change capture-v1 consumer: fail closed (verified above).
  - Older cached ingress image with the new consumer: unchanged. A legacy capture has no evidence, so `Promote` returns null and the path behaves as before. Those images keep the documented ANALYSIS-500 weakness until setup refreshes them.
  - New ingress with the new consumer: verified or shared, never in between.
- **Correction to implementation 021 (wording only, not a defect).** It says the persisted record never keeps `candidate`. When a pre-change consumer persists the shared record, the whole `evidence` object, including `candidate`, `provisional` and `agentParent`, survives as extension data. A newer tray later reads it as typed evidence, and `Merge` keeps `Candidate`. It stays inert: `Promote` reads only wire captures, and `Reconcile` inspects only the incoming capture. The local attachment file therefore holds the launch name/change text and the process and host identities. The orchestrator may want the sentence corrected.
- **No regression** in duplication, layering or performance. ANALYSIS-604's Core-to-IO residual is unchanged. ANALYSIS-605 is still deferred to 6.3.
- **Not performed:** build, test, format, the tools, a mixed-version run, an installed profile launch and any provider run. My only write attempt was this report, which the client refused, so the text is inline.
