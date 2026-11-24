<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-019 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the reviewer did not attempt the write because the subagent harness says not to write report files (as reported by the reviewer). -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-019 (verification of DEV-031)

## Review target

- Reviewer / role: Claude Code delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent). The client version is not visible to the subagent. Default permission mode. Model `claude-opus-5-5`.
- Date: 2026-11-05T08:43Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-019 (IDs ANALYSIS-1800 to 1899)
- Task and requirement references: task 2.5 (owned runtime); QA-2500 in [QA 025](qa-025.md); ANALYSIS-1700 and 1701 in [analysis 018](analysis-018.md); [implementation-031](../evidence/implementation-031.md); the 2.4 additive-evidence rule in [implementation-025](../evidence/implementation-025.md)
- Scope: the 9 assigned paths. Neighbouring code I read: `src/BellTower/IO/{AttachmentStore,ProcessTable,Json}.cs`, `src/BellTower/Providers/{CaptureConsumer,HookIngestor}.cs`, `src/BellTower.Protocol/{CaptureJson.cs,BellTower.Protocol.csproj}`, `src/BellTower.Ingress/BellTower.Ingress.csproj`, `docs/development.md` (layers)
- Baseline commit: `e0a69e6`
- Identity before: `head=e0a69e6 baseline=e0a69e6 files=9 diff=47dad9abca05 untracked=1/7bc81710de12`. The tracked diff SHA-256 is `47dad9abca05ab88566395f0fcd4a93c0a81080b4e3450e315d32c0205d4cf87`, and implementation-031 hashes to `8e918f0c…`. This matches the assignment.
- Identity after: `head=e0a69e6 baseline=e0a69e6 files=9 diff=47dad9abca05 untracked=1/7bc81710de12`. It is identical to the before line, so nothing changed during the analysis. `git status` shows the same entries, including the untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/`.
- Prior reports and rechecked findings: [analysis 018](analysis-018.md) (ANALYSIS-1700, 1701); [QA 025](qa-025.md) (QA-2500 design)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Is the Release build current? | Compared the output and source timestamps | `bell-tower.dll` was built at 09:30:23, after `NativeAdoption.cs` (09:30:20). The ingress dll was built at 09:30:21. `BellTower.Tests.dll` was built at 09:27:22, after the last test edit (09:27:19) | Current. Nothing was rebuilt |
| Could `AgentParentIsAgent` be derived instead of carried? | Source reading of `AttachmentCapture.cs:64-87` and `NativeAdoption.cs:193-194,241-263`; policy probe on copies of the Release assemblies | 1. The ancestor chain on the wire holds only `{pid, ticks}`, so Core cannot derive the flag from it without carrying names. 2. Hosts can derive it: the image names are already in the same `ancestors` list, and the lineage walk already stops at the first ancestor it cannot identify. 3. Probe: with the lineage cut at the first `codex.exe`, both the direct-child and the tool-shell nested shapes stay shared, and the three legitimate launch shapes are unchanged | Derivable. The derived form is also stronger: see ANALYSIS-1800 |
| Coverage of the QA-2500 shapes | Same probe, plus the image-name check at `AttachmentCapture.cs:84-85` | In the shape manager -> backend -> nested, the flag is true and the nested process stays shared. In the shape manager -> backend -> powershell (tool shell) -> nested, the direct parent is `powershell.exe`, so the flag is false, the manager is at hop 3, and the probe printed `bound=True` (the nested process takes `owned-attachment`). QA-2500 names this tool-shell variant explicitly | Gap: ANALYSIS-1800 |
| Core purity | Inspection | `Verified`, the null-hop guard and the new conjunct are pure, and the image-name read stays in Hosts. The new code adds no IO to Core | Pure |
| Additive under the 2.4 rule | Source reading plus the probe | The field is optional. An older consumer keeps it in `[JsonExtensionData]` and, because it does not read it, does not get the rejection. With web defaults, `"agentParentIsAgent": false` is written into every evidence record and envelope, for Claude and Codex alike (the probe confirmed it in the persisted form) | Additive. It costs one key on every record. The derived alternative in ANALYSIS-1800 removes the key |
| Is the flag cleared after proof? | `Verified` (`NativeAdoption.cs:228-234`) is used in all three success branches (`:199`, `:203`). Traced `AttachmentStore.Adopt` and the merge in `AttachmentEvidence` (`:160-175`) | Proven captures are saved without the proof inputs. `Shared()` replaces the evidence, so a capture that falls back keeps nothing either. Records saved by earlier builds keep any stored `agentParent` and `agentAncestors` through the non-replacement merge; nothing reads them | Cleared |
| Overlap with the 2.5 CrossCheck R2 rule (a backend must have a client as its parent) | Read `ProcessTable.Neighbors` (`:48-60`) and `CrossCheck` (`:59-79`) | Both look at the direct parent's image. `Neighbors` also checks creation time and runs only when `Needs` is not `None`, that is, when the command-line mode is shared-backend or client. The owned path runs exactly when the mode is `unknown`, so it cannot rely on that read. `CrossCheck` returns only a mode string. R2 does demote the nested `app-server` to `unknown`, which is why it reaches the provisional path; it cannot reject that path | They should not share one input. Sharing would tie the ownership proof to the mode-dependent relationship read. If ANALYSIS-1800 is adopted, the two rules no longer concern the same relationship (direct parent versus any hop up to the manager). No finding |
| Complexity of `Reconcile` | Inspection | The owned-runtime condition now has 9 conjuncts across two lines, after three hardening steps. The other proofs and `ManagerBehindAgent` stay readable | Acceptable. ANALYSIS-1800 would drop the new conjunct. Extracting a named owned-backend predicate is optional and is not raised |
| `CaptureProtocol.Validate`: placement and cap | Read `CaptureProtocol.cs:71-99` and the `ProcessTable.Ancestors` bound (`:30`) | Validate is the right boundary: an invalid envelope is deleted instead of retried, and the Core null-hop guard still covers the in-process `event` path. The literal `32` repeats the envelope `Ancestors` cap on the line above and the `ProcessTable` walk bound. The comment says the cap "mirrors the ingress's own bound", but the ingress emits at most `MaxOwnedDepth` = 3 hops | The placement and the loose cap are right. The comment is wrong: ANALYSIS-1801 |
| NativeAOT and the source generator | Checked the linked files in `BellTower.Protocol.csproj` and `BellTower.Ingress.csproj`, the generated metadata, and the build output | `AttachmentEvidence.cs` is linked into Protocol. The Protocol dll contains the generated `AgentParentIsAgent` property metadata (2 UTF-16 occurrences). `NativeAdoption.cs` and `AttachmentCapture.cs` are linked into the `PublishAot` ingress; the change adds only an out bool, a `with` expression and a private static helper, with no reflection. The ingress built at 09:30:21 with warnings treated as errors | Compatible. No native publish was run; the evidence is the analyzers and the generated metadata |
| Focused tests and runtime | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter <4 classes>` with TRX, while QA ran in parallel | 133/133 passed in 52 s. By class: `NativeOwnedRuntimeProcessTests` 32 runs, 52.1 s; `CaptureTests` 47 runs, 5.3 s; `NativeAdoptionReconcileTests` 26 runs, 3.8 s; `OwnedRuntimeReconcileTests` 28 runs, about 0 s. The new nested stand-in rows took 1.6 to 3.8 s each, about 11 s for 4 runs | Passed. The policy count of 28 matches implementation-031 |
| Test duplication | Compared the new nested test with the main theory in the same class | About 25 lines are repeated: the backend wait-and-go script, the shell start, the manager `ProcessStartInfo` with the managed environment, and the runtime-record write | ANALYSIS-1802 (suggestion) |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 (21.8 s) | Passed (whole solution, dirty tree) |
| Parity | Inspection plus the test rows | Claude has no owned runtime and its captures are never provisional; the Claude rows (no record, valid record, both entry points) passed. Codex is covered by the policy and stand-in rows. The nested test runs two simultaneous Codex sessions | No parity gap was introduced |

## Findings

### ANALYSIS-1800: The parent-is-agent flag rejects only a nested app-server directly under the backend; the tool-shell variant still binds, and deriving the rule from the lineage would cover both without a wire field

- Severity: warning
- Related task / requirement: 2.5; QA-2500 hardening (DEV-031)
- Locations: `src/BellTower/Hosts/AttachmentCapture.cs:74-85`; `src/BellTower/Core/NativeAdoption.cs:193-194`; `src/BellTower/Core/AttachmentEvidence.cs:102-104`
- Observation:
  - `parentIsAgent` checks only `ancestors[agentIndex + 1]`.
  - QA-2500 describes two nested shapes: a direct child of the backend, and a child "nested under a tool shell, still within three hops".
  - For the shape manager -> native `codex.exe` backend -> `powershell.exe` (tool shell) -> nested `codex.exe app-server`:
    - Hosts sends the lineage [tool shell, backend, manager] with `AgentParentIsAgent = false`.
    - `ManagerBehindAgent` finds the manager at hop 3.
    - Measured with a policy probe on copies of the Release assemblies: the nested capture bound to `owned-attachment` (`bound=True`).
    - The new stand-in test covers only the direct-child shape.
  - The new field adds one key to every persisted evidence record and envelope, and it adds an `Apply` parameter and a clearing step in `Verified`.
  - An older consumer that already reads `AgentAncestors` ignores the flag.
- Proposed improvement and benefit:
  - Preferred: drop the new field. In `Capture`'s lineage walk, stop at the first ancestor whose image is the provider's (`provider + ".exe"`), next to the existing stop at an unidentifiable ancestor.
    - A nested agent then never reaches a manager through another agent of its provider, whether directly or through a shell.
    - The probe confirmed that both nested shapes stay shared once the lineage is cut at the backend.
    - The three `CodexRuntimeHost` shapes (native, node, powershell [+node]) have no provider image between the manager and the backend, so they are unaffected.
    - Core's record-based proof is unchanged, so nothing is ever accepted by image name.
    - This removes the wire field, the `Apply` parameter, the `Reconcile` conjunct and the extra `Verified` clearing.
    - It also works with a DEV-030 consumer.
  - Minimal alternative: keep the field but compute it as "any provider image among the nearest `MaxOwnedDepth` ancestors", and rename it accordingly.
- Behavior-preservation risks:
  - `AgentParent` becomes null when the direct parent is a provider image. The profile-shell proof compares it with a recorded launch shell, which is never a `codex.exe`, so the result is the same.
  - Claude captures are never provisional, so they are unaffected.
  - The `shared:shared-backend` row (a backend under a Codex client) is decided by its mode before this point.
- Evidence type: measured (policy probe on the Release assemblies), plus source observation for the Hosts lineage. Unverified: whether a real Codex on Windows runs model commands with the tool shell as a direct child of `codex.exe`. A sandbox runner would add a hop and push the manager past the bound, so this exact 3-hop shape is the remaining exposure.
- Verification / measurement plan:
  - Add a stand-in row: manager -> backend -> `powershell` stand-in -> nested, in both hook orders and through both entry points.
  - Add a policy row with the tool-shell lineage.
  - Rerun the owned-runtime, resolve and reconcile classes, and check that the native, node, powershell and powershell+node positive rows still bind.
  - QA-026 owns the correctness verdict.
- Relationship: residual of QA-2500. This is the fail-closed lineage hardening that analysis-018 offered as optional.

### ANALYSIS-1801: The `Validate` comment misstates the `AgentAncestors` cap

- Severity: suggestion
- Related task / requirement: ANALYSIS-1701 fix
- Locations: `src/BellTower.Protocol/CaptureProtocol.cs:84-86`
- Observation:
  - The comment says the cap "mirrors the ingress's own bound", but the ingress emits at most `NativeAdoption.MaxOwnedDepth` = 3 hops.
  - The literal `32` actually repeats the envelope `Ancestors` cap on the line above (and the `ProcessTable.Ancestors` walk bound).
  - The loose cap is the right choice: a rejection drops the whole observation, so a tight cap of 3 would make a consumer reject a future producer's longer lineage and lose its events. A reader following the comment could "fix" it to 3.
- Proposed improvement and benefit: correct the comment, for example "a sanity cap equal to the envelope's ancestor cap; policy reads at most MaxOwnedDepth". Optionally, name one Protocol constant (for example `CaptureProtocol.MaxAncestors = 32`) and use it in both `Validate` clauses.
- Behavior-preservation risks: none if the value stays 32.
- Evidence type: source observation
- Verification / measurement plan: the existing `CaptureTests` cap and over-cap rows.
- Relationship: new; follows on from ANALYSIS-1701

### ANALYSIS-1802: The new nested stand-in test repeats the class's launch setup

- Severity: suggestion
- Related task / requirement: 2.5 tests
- Locations: `tests/BellTower.Tests/Hosts/NativeOwnedRuntimeProcessTests.cs:254-359` compared with `:93-180`
- Observation: about 25 lines are repeated: the wait-and-go backend batch, the shell stand-in start, the manager `ProcessStartInfo` with `BELLTOWER_ATTACHMENT` and `BELLTOWER_DEDICATED`, and the record and token write. Each nested run takes 1.6 to 3.8 s (4 stand-ins and two hooks).
- Proposed improvement and benefit: if rows are added for ANALYSIS-1800, extract class-local helpers (`WaitThenHook(script…)`, `StartManager(workspace, script)`, `WriteRecord(store, runtime)`) so the tool-shell row reuses them instead of a third copy. Do not move them into `StandIns` unless another class needs them.
- Behavior-preservation risks: test-only.
- Evidence type: source observation; TRX timings from a single run under parallel QA load
- Verification / measurement plan: the same 32-run count (plus any new rows) passes, with a similar class time.
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1700 | `head=e0a69e6 baseline=e0a69e6 files=9 diff=47dad9abca05 untracked=1/7bc81710de12` | 1. `Verified` clears `Provisional`, `AgentParent`, `AgentAncestors` and `AgentParentIsAgent`, and all three proof branches use it (`NativeAdoption.cs:199,203`). 2. `Shared()` resets the evidence. 3. In `Adopt`, a first adoption saves the cleared evidence, and a replacement merge takes the incoming (cleared) evidence. 4. The assertions in the owned positives, recorded launch, profile shell and the stored nested-test attachment pass. 5. The comment now matches the code. Residual: records saved by earlier builds keep their stored values through the non-replacement merge; nothing reads them | verified |
| ANALYSIS-1701 | same | 1. The null-hop guard ends the chain and fails closed (policy row `null-hop` passed). 2. `Validate` rejects a null hop, a non-positive pid or ticks, and more than 32 entries with `envelope-invalid`, so the spool file is deleted instead of retried. 3. The 8 `CaptureTests` rows passed, including the absent-field row for older captures. The comment issue is ANALYSIS-1801 | verified |
| QA-2500 (design) | same | The design is pure, additive and AOT-compatible, and the stand-in and policy rows pass. It covers only the direct-child shape: the tool-shell shape still binds (probe) | partially addressed; see ANALYSIS-1800. QA-026 owns the correctness verdict |

## Assessment and limitations

- DEV-031 closes ANALYSIS-1700 and 1701 cleanly.
- The QA-2500 hardening is sound for the shape it tests, but it is narrower than QA-2500 and adds a wire field that can be derived. Cutting the lineage at the first provider image covers both nested shapes with less code. This is ANALYSIS-1800, a warning that needs an orchestrator decision.
- ANALYSIS-1801 (wrong comment) and 1802 (test setup duplication) are minor.
- Checks:
  - Focused tests: 133/133 passed, with `--no-build`.
  - Formatting: clean.
  - Release build: current, not rebuilt.
- Not performed:
  - a native AOT publish;
  - a stand-in process run of the tool-shell shape (the probe is policy-level; the Hosts flag value comes from source reading);
  - real `claude` or `codex` (including how Codex actually starts tool shells on Windows), the live instance, and model services.
- Writes: no repository writes. The disposable probe and TRX are in the session scratch folder. I touched no process I did not start, and nothing was committed.
