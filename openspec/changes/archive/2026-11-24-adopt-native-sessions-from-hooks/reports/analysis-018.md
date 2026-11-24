<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-018 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the reviewer did not attempt the write because the subagent harness says not to write report files (as reported by the reviewer). -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-018 (recheck of DEV-030; final for this cycle)

## Review target

- Reviewer / role: Claude Code delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent). The client version is not visible to the subagent. Default permission mode. Model `claude-opus-5-5`.
- Date: 2026-11-05 (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-018
- Task and requirement references: task 2.5 and the owned-runtime restore; QA-2400 and the orchestrator decision in [findings](findings.md) (bounded-depth, record-based, never by image name); [implementation-030](../evidence/implementation-030.md); the 2.4 additive-evidence rule in [implementation-025](../evidence/implementation-025.md)
- Scope: the 10 assigned paths. Neighbouring code read: `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/IO/ProcessTable.cs`, `src/BellTower/IO/ProcessProbe.cs`, `src/BellTower.Ingress/Program.cs` and its csproj, `src/BellTower.Protocol/{CaptureProtocol,CaptureJson}.cs` and its csproj, `src/BellTower/Providers/{CaptureConsumer,HookIngestor}.cs`, `src/BellTower/IO/StateStore.cs`, `src/BellTower/Tray/TrayApplication.cs`, `src/BellTower/Sessions/CodexRuntimeHost.cs`, `src/BellTower/Integration/BellTower.psm1`, `src/BellTower/IO/Json.cs`
- Baseline commit: `5e55981`
- Implementation identity (before): `head=5e55981 baseline=5e55981 files=10 diff=e8c3d3e84501 untracked=1/3af0d26339da`. The tracked diff SHA-256 is `e8c3d3e8450104977f49f22c92c37debd54f29dbd7238e018faf7bc7b2b6e054`. The untracked file is implementation-030, SHA-256 `6b1caef3…`. This matches the assignment.
- Before/after identity: after the analysis the same command printed `head=5e55981 baseline=5e55981 files=10 diff=e8c3d3e84501 untracked=1/3af0d26339da`. The two lines are identical, so nothing changed during the analysis.
- Prior reports / rechecked findings: [analysis 017](analysis-017.md) (ANALYSIS-1601, 1602); [QA 024](qa-024.md) (QA-2400 design)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Is evidence or the capture envelope the right home for the field? | Read `Program.cs:39-46`, `CaptureEnvelope.Ancestors`, `HookIngestor.cs:15-20`, `NativeAdoption.Reconcile` | The envelope's `Ancestors` exist only on the ingress path; the `event` CLI path has no envelope. They start at the ingress process, not at the agent. They also skip ancestors that cannot be identified (`Where(p => p.Process is not null)`), and a skipped ancestor breaks the per-hop adjacency the proof relies on. `Reconcile` works on `Attachment` in Core, and its sibling `AgentParent` already lives in evidence | Evidence is the right shared home for both entry points. Justified |
| Persistence of `AgentAncestors` | Traced Promote, then Reconcile, then `AttachmentStore.Adopt` (`:50-60`, `AttachmentEvidence.Of`). Reflection probe against the Release assemblies | A successful proof returns `pending with { Provisional = false }`. Because `Generation == Process`, `Adopt` saves that evidence as is on first adoption, and later non-replacement merges keep it. The probe showed the npm-shape result still carrying the chain. Nothing reads the field after adoption | Persisted, which contradicts the "Wire form only" comment. ANALYSIS-1700 |
| Size bound | Probe serialization through the `CaptureJson` source-generated metadata | Hosts emits at most 3 hops, and Core reads at most `MaxOwnedDepth`. Three hops add 153 compact bytes to the evidence JSON. Every evidence record now writes `"agentAncestors": null` (the web defaults write nulls, as for `agentParent`). The wire list itself is not validated (`CaptureProtocol.Validate` checks only `envelope.Ancestors`, `<= 32`); the 128 KiB envelope limit is the only cap | The bound is adequate for the policy. The missing wire validation leads to ANALYSIS-1701 |
| Additive under the 2.4 rule | Read the code and ran the probe with a legacy-producer row | The field is optional. `ManagerBehindAgent` falls back to `[AgentParent]` when the field is absent, and the probe showed an older-producer capture with only a parent still binding the native shape. An older consumer keeps the unknown field in evidence `[JsonExtensionData]` and decides from `AgentParent`, as before. `AgentParent` stays equal to `AgentAncestors[0]` for older consumers and for the profile-shell proof | Additive. The redundancy with `AgentParent` is justified by compatibility |
| Core purity | Inspection | `ManagerBehindAgent` (`NativeAdoption.cs:229-246`) is pure, with no IO; liveness still comes through the `live` delegate. The `using BellTower.IO` is older (`ContentHash`). Hosts depends on Core's `MaxOwnedDepth`, which is the right direction | Pure |
| Bounded-depth proof: complexity and duplication | Inspection of `NativeAdoption.cs:190-191, 229-246` and `AttachmentCapture.cs:73-84` | It replaces the old `pending.AgentParent == owned.Manager` check, of which the direct-parent case is now the one-hop case, so nothing is duplicated. The profile-shell proof (`:186`) is a different proof and stays direct-parent only. Each hop costs a time comparison and an equality test; O(3) | Proportionate. The 20-line helper has a clear contract |
| Depth constant: one place, derived from launch shapes | Read `CodexRuntimeHost.cs:76-107` and `BellTower.psm1:18` | One definition (`NativeAdoption.MaxOwnedDepth = 3`). Hosts uses it at `AttachmentCapture.cs:74` and Core at `:233`; Core repeats the `Take` to guard against producers that send longer chains. The shapes it is derived from are documented next to the constant. They match the host's three branches: native, npm (`node codex.js`) and the PowerShell `-EncodedCommand` shim. `Get-Command … ExternalScript` resolves `codex.ps1` ahead of `codex.cmd`, so the shim runs in-process and gives manager, powershell, node, codex (3 hops). A `codex.cmd` resolution would be 4 hops and stay shared. `TheBoundIsTheSharedConstant` pins the value | Sound. The link to the host's shapes is by documentation only, which is acceptable. I did not verify that a real npm install's vendored binary image is named `codex.exe`; the stand-ins assume it |
| Residual: nested `codex app-server` inside an owned backend | Reasoning over the hop sequence | manager, backend, (shell), nested codex is within 3 hops. Only the `profile` guard keeps it shared, and only once the real backend's hook has been recorded. The developer documented this | Not raised: documented and practically unreachable, because the backend hooks first. Optional fail-closed hardening, if wanted: stop the Hosts lineage at a same-provider image. The names are already in the table, and this only rejects, never accepts |
| Interaction with ANALYSIS-1500/1501 | Traced the call sites; measured `ProcessProbe.Identify` with the probe (2000 warmed calls on live ancestors, under concurrent QA load) | Still one `ProcessTable.Take` per hook (`Resolve(…, table)`), and the lineage comes from the `table.Ancestors` list that already existed. The extra cost is at most 2 more `Identify` calls per hook: median 20 µs (p95 56 µs) each, under 0.12 ms in total against a snapshot of about 18 ms (analysis-017). Those `Identify` calls repeat ones `Program.cs:40` already makes for the envelope, and the lineage is also built for Claude and for captures that are not provisional, where it is unused. Both effects are in the tens of microseconds. `Needs` and `CrossCheck` are unchanged | Still one snapshot per hook, with no material extra cost |
| NativeAOT | Checked `Directory.Build.props` (`TreatWarningsAsErrors`), the ingress `PublishAot`, the timestamps of the current Release outputs, and the generated metadata in `BellTower.Protocol.dll` | The Release build at 08:47, later than the sources, compiled the ingress with AOT and trim analysers as errors. The source generator emitted `IReadOnlyListProcessIdentity` metadata, and the probe deserialized through that metadata. No new reflection or P/Invoke was added; collection expressions only | Compatible. No scratch native publish was run (ILC takes minutes and needs the vswhere PATH setup); analyzer and source-gen evidence only |
| Robustness of the new wire field | Reflection probe: `Reconcile` with `agentAncestors: [null]` and a live owned record | `System.NullReferenceException` | ANALYSIS-1701 |
| Focused tests and runtime | `dotnet test … --no-build --no-restore` with TRX: the 6 focused classes together, then the owned-runtime class alone. 17 dotnet/testhost processes were running (QA in parallel) | Together: 191/191 passed (38 s). The relationship class ran 29 runs (17.8 s), resolve 28 (12.1 s), owned-runtime 28 (38.4 s), `OwnedRuntimeReconcileTests` 25 (0.03 s), `ProcessTableTests` 7 (13.8 s), `ModeCrossCheckTests` 74 (0.7 s). Owned-runtime alone: 28/28 passed, 32.5 s, mean 1.16 s per run (analysis-017: 1.64 s with 15 rows) | Passed. Note on the evidence text: `OwnedRuntimeReconcileTests` runs 25 times (10 + 3 + 1 + 10 + 1), not the 22 stated in implementation-030 |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 | Passed (whole solution, dirty tree) |
| Process hygiene | `Get-Process` filtered by scratch paths, plus `ping` and `bell-tower-ingress` | 0 matches | Passed. I touched no process I did not start |

## Findings

### ANALYSIS-1700: `AgentAncestors` (and `AgentParent`) are persisted after adoption, contrary to the "Wire form only" comment

- Severity: suggestion
- Related task / requirement: QA-2400 fix (DEV-030); 2.4 additive evidence
- Locations: `src/BellTower/Core/AttachmentEvidence.cs:97-99`; `src/BellTower/Core/NativeAdoption.cs:196,200` (`pending with { Provisional = false }`); `src/BellTower/IO/AttachmentStore.cs:55-60`
- Observation: every successful provisional proof (recorded launch, profile shell, owned runtime) keeps the wire-only proof inputs on the evidence. `Adopt` saves that evidence on first adoption, and the merge keeps it for the life of the attachment. Nothing reads either field after `Reconcile`. `Candidate`, by contrast, is cleared in `Promote`. The cost is small (up to about 150 bytes for each bound Codex managed attachment), but the comment states a guarantee the code does not provide.
- Proposed improvement and benefit: clear the proof inputs when the proof succeeds (`pending with { Provisional = false, AgentParent = null, AgentAncestors = null }` in both returns), or correct the comment to say the fields persist. Clearing makes the comment true and keeps process-lineage data out of stored records.
- Behavior-preservation risks: none for policy, because no reader exists after adoption. Older trays never read these fields from stored records. Records persisted since 2.2 with `agentParent` stay harmless.
- Evidence type: source observation, plus the reflection probe (the Reconcile result retained the chain)
- Verification / measurement plan: add a policy assertion in `OwnedRuntimeReconcileTests` that a bound result has `Evidence.AgentAncestors` and `AgentParent` null, then rerun the owned-runtime and resolve classes.
- Relationship: new; part of the requested persistence check

### ANALYSIS-1701: A null hop in the wire `AgentAncestors` throws in `Reconcile`, and the exception escapes the capture consumer and the tray tick

- Severity: suggestion
- Related task / requirement: QA-2400 fix (DEV-030); size bound and robustness of the new wire field
- Locations: `src/BellTower/Core/NativeAdoption.cs:233-235`; `src/BellTower.Protocol/CaptureProtocol.cs:71-94` (no evidence-level checks); `src/BellTower/Providers/CaptureConsumer.cs:47` (catches only JSON, InvalidData and Argument exceptions); `src/BellTower/Tray/TrayApplication.cs:272` (catches only IO, access, JSON and InvalidData exceptions)
- Observation:
  - The reflection probe ran `Reconcile` on a provisional capture with `"agentAncestors":[null]` and a live owned record, and got a `NullReferenceException` at `hop.StartedUtcTicks`.
  - The exception escapes `Prepare` and `Drain` before the spool file is deleted, so the file is picked up again on every tray tick.
  - The ingress never writes such a list. Only a malformed spool file written by a same-user local process can carry one.
  - Unvalidated evidence wire fields are an existing class of problem. By source reading, a null `candidate.host` is a similar case. This change adds one more instance.
- Proposed improvement and benefit:
  - Minimum: treat a null hop as the end of the chain in `ManagerBehindAgent` (`if (hop is null) return false;`). This is pure and fails closed.
  - Optional: have `CaptureProtocol.Validate` reject an `AgentAncestors` list with null or non-positive entries, or longer than the `Ancestors` cap of 32. A malformed capture is then rejected and deleted like other invalid envelopes instead of being retried.
- Behavior-preservation risks: none for valid captures. Validation in Protocol must not depend on `NativeAdoption`, which is not linked there, so use a literal cap.
- Evidence type: measured (reflection probe on the Release assemblies) plus source observation for the propagation path
- Verification / measurement plan: add a policy row `null-hop` to `AnythingLessThanTheFullLineageProofFailsClosed`, expecting the shared result. If validation is added, add a `CaptureProtocol` decode row that expects `envelope-invalid`.
- Relationship: new; the existing evidence-validation gap is noted, not reopened

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1601 | `head=5e55981 … diff=e8c3d3e84501 untracked=1/3af0d26339da` | The backend stand-in polls with `ping -n 1` (`NativeOwnedRuntimeProcessTests.cs:112`), and the 25 s deadline is now the shared `StandIns.Deadline`. Run alone: 28/28 passed in 32.5 s, mean 1.16 s per run, against 1.64 s per run (15 rows) in analysis-017, while QA was loading the machine. The class total grew because the row count rose from 15 to 28 and the new rows have launcher chains | verified (per-run cost about 29% lower; single noisy run) |
| ANALYSIS-1602 | same | `Testing/StandIns` now holds `CopyImage`, `Hook`, `Under`, `ScrubEnvironment`, `WaitFor` and `Deadline`. The four process classes call it 14, 11, 11 and 2 times. A grep finds no local `File.Copy` of cmd.exe, no environment-scrub loop and no 25 s wait loop left in those classes. `ProcessTableTests` keeps a 50 ms polling loop for `Find`, which is a different wait. Counts and results are unchanged (relationship 29, resolve 28, process table 7, all passed). Residual: `StandIns.Hook` refers to the test-class constant `NativeAgentResolveProcessTests.Ingress` (`using BellTower.Tests.Hosts`), so the helper depends on a test class. This is minor and not raised | verified |
| QA-2400 (design) | same | Record-based; no image name is accepted. The manager is matched by PID and creation time within `MaxOwnedDepth` hops, with a time-consistency check at every hop that rejects reused PIDs. The manager and shell must be live. The depth is derived from the three `CodexRuntimeHost` branches, one constant. One snapshot per hook, with at most about 0.1 ms of extra `Identify` cost. Additive under 2.4, with a fallback for older producers (probe). Core stays pure, and the AOT analyzers and source generator are clean. Policy rows (25) and real stand-in rows (28) passed. Codex-only by provider fact; Claude is never provisional and stays unchanged | design sound, with two suggestions (ANALYSIS-1700, 1701). QA-025 owns the correctness verdict |

## Assessment and limitations

- Overall: DEV-030 resolves ANALYSIS-1601 and 1602. The QA-2400 design is proportionate, pure, additive and AOT-compatible, and keeps one snapshot per hook. Neither suggestion blocks the restore:
  - ANALYSIS-1700: proof inputs are persisted although the comment says they are wire-only.
  - ANALYSIS-1701: a null hop in a malformed spool file throws.
- Evidence text: `OwnedRuntimeReconcileTests` runs 25 times, not the 22 stated in implementation-030.
- Clients: the focused rows cover Claude and Codex at both entry points, plus the mixed row; all 191 passed.
- Not performed:
  - a scratch native AOT publish (ILC takes minutes; I relied on the analysers and the source-generator evidence);
  - the packaged ingress benchmark (task 6.3);
  - a real npm or shim Codex install, including the vendored image name;
  - real `claude` or `codex` launches and the live instance;
  - an end-to-end run of a malformed spool file through the tray (the propagation path comes from source reading).
- Timings are single runs taken while QA ran in parallel.
- Writes: none in the repository. The subagent harness says not to write report files, so I did not attempt the reserved `reports/analysis-018.md` and this report is returned inline for the orchestrator to save. Disposable data (copied DLLs, the probe and TRX scripts, TRX files) stays in the session scratch folder. Source, tests, planning, the bin and obj folders, and the untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/` were not touched. Nothing was committed.
