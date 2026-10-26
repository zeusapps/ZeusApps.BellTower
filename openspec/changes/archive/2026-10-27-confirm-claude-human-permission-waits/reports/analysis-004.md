# Analysis report: confirm-claude-human-permission-waits / 004

## Review target

- Reviewer / role: Codex delegated analyzer / code-analyzer.
- Client / version / mode / model: Codex; local CLI reports `codex-cli 0.164.0` (active host build unavailable); Default, workspace-write; `gpt-6.1-sol`, high reasoning (role default).
- Date: 2026-10-26 UTC.
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits`, Analysis 004 final second-cycle recheck after user-confirmed takeover.
- Task and requirement references: 2.5, 2.7, 2.8, 2A.6, 4.1; shared human-wait confirmation, late confirmation, restart, and independent episodes in [attention-notifications](../specs/attention-notifications/spec.md) and [session-tracking](../specs/session-tracking/spec.md).
- Scope: current source/tests/docs/README target; detailed source boundaries are Core confirmation/replay/episode policy, `Sessions/CodexPermissionPump.cs`, `Providers/ClaudeConfirmationSignal.cs`, diagnostics and tray integration. No new suggestions.
- Baseline and HEAD: `754aed525415d0cb3f47e8b6064e91fe1abdc618`.
- Before identity, executed with `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`: `head=754aed5 baseline=754aed5 files=62 diff=8850b9968dda untracked=15/1f3f5b66da20`. Full tracked diff SHA-256: `8850b9968dda9988f45aa9ab01e2104c33d14fa2db6b82f4fa6a3807ebc38653`.
- After identity: identical summary and full tracked hash; no unexpected implementation mutation. [Retained manifest and untracked hashes](../evidence/analysis-004-target-after.txt) match the initially inspected manifest. Reports and other change tracking files are outside this implementation identity.
- Prior reports: [Analysis 002](analysis-002.md), [QA 002](qa-002.md), [Implementation 003](../evidence/implementation-003.md). Rechecked ANALYSIS-002/100/101, disabled-notification part of ANALYSIS-004, and QA-100. Prior 003 reservations were superseded, not reused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared policy, episode isolation and dismissal | Source inspection plus `dotnet run --project artifacts/analysis-004-probe/Probe.csproj --no-restore` | [Core probe source](../evidence/analysis-004-core-source.cs.txt), [output](../evidence/analysis-004-core-output.txt) | Passed 26 independent assertions across both reducer paths. Both Claude and Codex silent-chip boundaries, fresh confirmation after dismissal, one announcement, and no unverified sound pass. These are simulated inputs with no physical audio. |
| Runtime attribution, normal continuing positive | Independent Core probe and inspection of pump publication guard | Same Core evidence; pump lines 149–160, reducer lines 486–505 | Passed for an already-confirmed older permission remaining outstanding; a newer provisional request receives no stored confirmation or new episode. |
| Runtime attribution, in-flight result after old request resolves | `dotnet run --project artifacts/analysis-004-pump-probe/Probe.csproj --no-restore`, real pump and StateStore, fake delayed status client | [Pump source](../evidence/analysis-004-pump-source.cs.txt), [output](../evidence/analysis-004-pump-output.txt) | Failed on both causal and unordered paths: an old query confirms a newer automatically reviewed request. Canonical ANALYSIS-002 remains failing. No socket was opened. |
| Question/permission ambiguity | Independent Core probe plus source inspection | Core probe, reducer lines 514–522; no held-prompt re-evaluation remains | Passed on both paths: resolving the question does not credit its ambiguous notification to the permission; its own later notification confirms it. |
| Restore anchor and replay floor | Independent Core probe after `Window + 40` later events | Core probe, `Observation.RestoredSince`, reducer lines 499–502, pump lines 173–174 | Passed on both paths: two pre-run requests confirm at their own stored starts plus 1 ms; remain visible and silent after restart. The confirmation observation stays at response time in the journal. |
| Already-confirmed versus previously provisional restoration | Source inspection of `Confirm` early return and runtime guard | Reducer lines 491–493, 567–569, 576–586 | Passed by inspection for preserving an already-confirmed wait's episode: it is not promoted again. A long-lived provisional wait receiving its first supported confirmation may obtain a fresh episode, including after restart; this is different from restoring already-confirmed attention and is silent under the restore anchor. No additional finding. |
| Claude disabled-notification handling | Source inspection and adapter probes; read orchestrator-fetched official documentation evidence | `ClaudeConfirmationSignal.cs:17`, tray integration at `TrayApplication.cs:209`; [contract check](../evidence/resumption-contract-check.md) | Shared silent-chip implementation and named setting verified. Documentation must retain the setting's SDK-hosted, version-specific scope; its absence is not evidence that a terminal notification was disabled by that variable. See re-verification limitation. |
| Architecture and performance | Source inspection | Shared policy remains in Core; adapter supplies transient availability and runtime observations | No new duplication or architecture finding. No performance measurement performed. |
| Full product build/tests/format | Not run by analyzer | QA owns shared build/test output | Not performed here to avoid concurrent output mutation; do not count QA's execution as this analyzer's independent test run. The Core probe compiled current source directly; the pump probe referenced the current Release assemblies after QA reported its build complete. |
| Native/installed/live acceptance | None | No provider sessions, real approvals, installed tray or production settings used | Not performed for either provider. Simulated probes do not satisfy this acceptance. |

The probe projects are disposable ignored artifacts and create only isolated data roots under their own artifact directory. The retained source/output files contain synthetic operations and no account or machine values. Their first local builds required correcting standalone project references; all results above come from the corrected successful runs. Application source and test source were not edited.

## Findings

No new finding IDs allocated. The following is continuing failure of canonical ANALYSIS-002, not a duplicate finding.

### ANALYSIS-002: An in-flight runtime status read can still confirm a newer request after its original provisional request resolves

- Severity: warning; acceptance-breaking confirmation attribution.
- Related task / requirement: 2.4, 2.5, 4.1; shared human-wait confirmation requires exact scope/freshness, and late runtime results must not establish a different wait. The automatic-approval scenario requires no human-permission alert without evidence for that request.
- Locations at the reviewed identity: `src/BellTower/Sessions/CodexPermissionPump.cs:102` awaits a status read against the captured session; lines 108 and 130 timestamp and publish the returned value; lines 149–178 check only that captured session and emit no inspected-request boundary. `src/BellTower/Core/SessionReducer.cs:486–504` confirms every permission currently present when no confirmed permission remains, including requests that were not present when the status read began.
- Reproduction, real pump with a fake client and isolated StateStore: publish P1 provisional at one minute before the run; start `RefreshAsync([P1 session])`; fake `ReadPermissionPendingAsync` signals that its read began and withholds its positive result. Resolve P1 and start a distinct P2 automatic review through the real EventStore and drain it. Release the old positive result, await refresh, then drain the emitted observation. The fake result describes P1; there is no human-wait evidence for P2.
- Actual result, both reducer paths: before the stale response, `oldGone=True, new=Provisional`; afterward, `new=Confirmed, attention=True, announcements=1` at its due time. This was reproduced twice with the real pump/StateStore, no provider connection, no physical audio and no production data.
- Why the final fix is insufficient: the guard solves the case where a confirmed old wait is still in the captured session. In this race P1 was provisional, so the guard does not stop publication; by consumption time P1 is gone and P2 is the only candidate. `At = now` prevents the replay-floor bug but also makes the old query look fresh unless its original request scope travels with it. `RestoredSince` changes restore timing and does not constrain attribution.
- Proposed correction and benefit: bind runtime evidence to the inspected request set and freshness boundary. Resolving/replacing that set while the read is in flight must invalidate its positive result for newer requests; the reducer must not broaden an old result to requests first observed afterward. Keep confirmation-only runtime observations incapable of creating waits. A request-set boundary should be separate from the silent-restore anchor.
- Behavior-preservation risks: preserve actual confirmation for still-current requests, idempotent publication, independent questions/permissions, both reducer paths and long-journal silent restore. Do not revert to hook-time confirmation or make elapsed time prove a human wait.
- Verification plan: retain this fake delayed-client race as a functional regression for both reducer paths, including result-after-resolution/replacement and a positive result for the unchanged inspected request set. Recheck the already-confirmed P1/P2 scenario, restored waits beyond the causal window, question coexistence and mixed-provider isolation.
- Evidence type: measured simulated result from real pump and StateStore with fake runtime response; not a native provider trial.
- Relationship: existing ANALYSIS-002; the previously identified continuing-positive-status case now passes, but the same attribution acceptance remains unmet under an in-flight read.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-002 | `diff=8850b9968dda untracked=15/1f3f5b66da20` | Core continuing-positive guard passes; real-pump delayed-response probe fails on both paths | Still failing, narrowly as detailed above. |
| ANALYSIS-100 | Same | Independent no-elimination probe on both paths; source inspection | Verified for the agreed removal: question resolution confirms nothing by elimination, permission's own later prompt works. |
| ANALYSIS-101 | Same | Independent two-pre-run-request probe after `Window + 40` later events, both paths, silent notification check | Verified in simulated execution. No packaged/native restore trial performed. |
| QA-100 | Same | Independent Claude/Codex dismissed-unverified-chip promotion on both paths | Verified in simulated execution: fresh confirmed episode announces once and is visible. |
| ANALYSIS-004, requested disabled-notification part | Same | Adapter/tray/doctor/matrix inspection, independent Claude silent-chip probes, official docs evidence | Implementation verified: the setting is named, no environment is read, and no-observed-prompt sessions get the shared silent unverified chip. Provider-mode wording still needs qualification: the setting affects Agent SDK permission-callback hosts at 2.1.233+, and has no terminal effect. Current generic examples do not establish a terminal-specific failure cause. This does not invalidate the no-observed-prompt fallback; final documentation acceptance belongs to the orchestrator. Other portions remain deferred. |

## Assessment and limitations

The requested final recheck is complete and the target identity is unchanged. ANALYSIS-002 still breaks acceptance, so the implementation is not ready to declare complete or commit under the completion gate. Other assigned code corrections passed bounded independent checks. No new suggestion or unrelated scope was introduced.

Developer residual edge 1 should not be generalized into a claim that the one-second polling interval guarantees attribution: a current positive read after an old wait resolves may be usable evidence, but a response to an older captured request set is demonstrably unsafe. The delayed-read probe demonstrates that distinction. Developer residual restore episode behavior does not change already-confirmed episodes; first confirmation of a previously provisional wait is intentionally separate and stays silent when back-dated for restoration.

The SDK setting's exact host restriction is supported by the orchestrator's current official documentation check, not by a new native probe from this analyzer. The sequential question capture proves that captured sequence, not a universal absence of queued or concurrent dialogs. Missing native/live auto-versus-human acceptance, packaged update trials and any incomplete mode/tool captures remain unverified. No performance result is claimed. Only the assigned report/evidence and ignored probe outputs were written; ledger changes remain the orchestrator's responsibility.

Final self-hygiene scan over this report and all `evidence/analysis-004-*` files: zero hits for current account/computer/home values, email/IP patterns and common secret patterns. No screenshots were produced. Final implementation identity remained `head=754aed5 baseline=754aed5 files=62 diff=8850b9968dda untracked=15/1f3f5b66da20`.
