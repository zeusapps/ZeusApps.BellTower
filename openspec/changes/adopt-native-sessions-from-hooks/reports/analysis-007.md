<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-007 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-007

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent; client version not exposed to the subagent) / delegated non-interactive subagent / `claude-opus-5-5`
- Date: 2026-11-03 20:22 UTC
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-007
- Task and requirement references: [tasks.md](../tasks.md) 2.1 and 2.2; [design.md](../design.md) decisions 1-5 and Risks; [analysis 006](analysis-006.md) ANALYSIS-500; [implementation 019](../evidence/implementation-019.md)
- Scope and relevant neighboring paths:
  - Target: `src/BellTower/Core/{AttachmentEvidence,NativeAdoption,Attachment}.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/{AttachmentStore,ProcessProbe}.cs`, `src/BellTower/Providers/{CaptureConsumer,HookIngestor}.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, `src/BellTower.Ingress/Program.cs`, both csproj files, the two new test files and `PowerShellModuleTests.cs`.
  - Neighbors read: `Hosts/TerminalTitleService.cs`, `Hosts/TerminalPresentationResolver.cs`, `Integration/BellTower.psm1` (launch environment), `BellTower.Protocol/CaptureFactory.cs`, `BellTower.Ingress.csproj`, `docs/development.md`, `docs/hook-ingress.md` (latency records).
- Baseline commit: `2f1de7f`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2f1de7f -Paths src tests openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-019.md`
  - HEAD `2f1de7fc45c54c27a7bc236b9daac1bbc003b665`; 11 tracked modified and 5 untracked files, matching the assignment manifest
  - Tracked diff SHA-256 `679cff63262dd9781a351ca87db86503900afef0ea13da25da5e340d2f7c415e`
  - Untracked: `implementation-019.md` c334bdb4..., `AttachmentEvidence.cs` d5423c48..., `NativeAdoption.cs` 1aa22364..., `AttachmentEvidenceTests.cs` 5ec6049f..., `NativeAdoptionTests.cs` 8bc48d2e...
  - Summary: `head=2f1de7f baseline=2f1de7f files=16 diff=679cff63262d untracked=5/bf90b082dd7c`
- Before/after identity: the same command after the analysis printed `head=2f1de7f baseline=2f1de7f files=16 diff=679cff63262d untracked=5/bf90b082dd7c`. Identical; no target mutation.
- Prior reports / rechecked findings: [analysis 006](analysis-006.md) ANALYSIS-500 is the subject of the 2.2 fix. This is not a recheck assignment, so the fix is assessed within the new scope (ANALYSIS-600) and not closed.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication / parity | Read the new policy and every caller; compared the ingress and legacy paths for both providers | `NativeAdoption.Apply` is the only adoption policy. Ingress `Program.cs` and `HookIngestor` both call `AttachmentCapture.Resolve`, which removes the duplicated shared-runtime branch. The only per-provider code is the argv vocabulary and `MayShareBackend`. | No per-provider policy duplication (both clients). Small leftovers in ANALYSIS-602 and 606. |
| ANALYSIS-500 fix | Traced the nested-client path: inherited environment (`BellTower.psm1:40-58`, `AgentLauncher.cs:32-37`) -> `AttachmentCapture` -> `NativeAdoption` -> `AttachmentStore.Adopt` -> `CaptureConsumer` -> presentation consumers | Layer 1 (an `app-server` ancestor means shared runtime) holds. Layer 2 changes only Id, Ownership and lifecycle owner. | ANALYSIS-600 (both clients). |
| Classification completeness | Read the Codex vocabulary against the design rule and the Codex 0.165.0 docs fetched with ctx7 (`/openai/codex/rust-v0.165.0`) | Unrecognized subcommands and option-only command lines return `embedded-runtime` | ANALYSIS-601 (Codex). The Claude vocabulary has no comparable default. |
| Architecture: evidence model vs 2.3/2.4/3.x/4.2 | Read `AttachmentEvidence`, the merge rules, the old-tray retention path and design decision 3 | No generation key. `TerminalEvidence` is only `{Binding, Source}`. No product code reads evidence for behavior yet. | ANALYSIS-603; lifecycle redundancy in ANALYSIS-602. |
| Architecture: boundaries and layering | Compared placement with `docs/development.md` and project facts | Provider vocabulary is in Core. Core now imports IO. The policy file is linked into Protocol, though only `AttachmentCapture` (linked into Ingress) uses it. | ANALYSIS-604. Classifying in the ingress is the right boundary (see Assessment). |
| Performance: ingress | Read the hook path. Measured a replica (P/Invoke code copied into PowerShell 7 `Add-Type`, warm, same-user target, own PID). | Command-line read 1.9 us/op (2000 iterations). `Process.GetProcessById` + `StartTime` 11.3 us/op (200 iterations). Budget: median below 300 ms, durable within 3 s (`openspec/specs/session-tracking/spec.md:138`). | The added read is negligible, measured on a replica rather than the AOT product. Discarded host discovery: ANALYSIS-605 (hypothesis). |
| Performance: AttachmentStore merge | Read `Adopt` and `AttachmentEvidence.Merge` | One small record allocation per hook. The dictionary copy runs only on replacement. The extra `Get` and `Inspect` run only on the inherited-identity branch. `Save` was already unconditional per hook. | No finding. |
| Compatibility claim | Read `AttachmentHealthCompatibility`, `CaptureCompatibility` and the `Attachment` extension data; grepped for evidence readers | Only the store merge reads `Attachment.Evidence` | Claim accepted for 2.1-2.2, with the 2.3 condition in ANALYSIS-603. |
| Build / test / format | Not run | Other dotnet processes were running, and another session had changed `global.json` to 10.0.114. That edit was reverted before my after-identity run. I made no `global.json` edit. | Not run, to avoid interfering with a concurrent build. QA owns functional verification. |
| Provider operations | Not performed | Source-only analysis | Not applicable. |

## Findings

### ANALYSIS-600: The ANALYSIS-500 re-identification keeps the nested client's inherited presentation consent, configured shell and launch identity text

- Severity: warning
- Related task / requirement: 2.2 (ANALYSIS-500); design decisions 1 and 5
- Locations: `src/BellTower/IO/AttachmentStore.cs:41-52,117-119`; `src/BellTower/Hosts/AttachmentCapture.cs:31-34,55-58`; `src/BellTower.Protocol/CaptureFactory.cs:47-48`; `src/BellTower/Providers/CaptureConsumer.cs:61-71`; consumers `src/BellTower/Hosts/TerminalPresentationResolver.cs:11-21` and `src/BellTower/Hosts/TerminalTitleService.cs:17-26`; test `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs:170-195`
- Observation:
  - A nested client inherits the launch environment that the module and launcher set: `BELLTOWER_ATTACHMENT`, `BELLTOWER_SHELL_PID`, `BELLTOWER_MANAGED_TITLE`, `BELLTOWER_MANAGED_BACKGROUND`, `BELLTOWER_NAME`, `BELLTOWER_CHANGE`, `TERM_PROGRAM` and `WT_SESSION`.
  - `AttachmentCapture` turns these into `ManagedTitle`/`ManagedBackground = true`, `Host.Shell` = the managed terminal's shell, and Kind = vscode or windows-terminal. `CaptureFactory` passes Name and Change because the capture is dedicated.
  - `RejectsInheritedManagedIdentity` replaces only Id, Ownership and `Evidence.LifecycleOwner`.
  - The resulting `observed` attachment therefore has a live nested agent, the managed terminal's shell and `ManagedTitle: true`. That combination passes the checks in `TerminalPresentationResolver` and `TerminalTitleService`.
  - Consequence: the nested session's title, and its palette background, can be written to the managed session's terminal. Its identity also starts with the parent launch's name and change. This is the outcome ANALYSIS-500 warned about, and design decision 5 forbids acquiring presentation consent from a delivered hook.
  - Whether a write actually lands still depends on the writer's console-membership check, which depends on how the parent spawned the child. That was not verified.
  - The nested-client test builds its fixture with `ManagedTitle: true, ManagedBackground: true` but asserts only Id, Process, Ownership and lifecycle owner.
  - The ownership decision is also split. `NativeAdoption.Apply` (Core) decides mode and ownership in the ingress, while the inherited-identity rule is embedded in `AttachmentStore` (IO), which also re-derives the `{provider}-{pid}-{ticks}` identifier.
  - Reachable shapes are interactive or embedded nested clients; headless clients already have these fields cleared. Combined with ANALYSIS-601, a plausible case is `codex mcp-server` under a managed Claude session. Whether that mode emits hooks is unverified.
- Proposed improvement and benefit:
  - Move the rule into one Core policy function, for example `NativeAdoption.Reconcile(captured, previous, ownerLive)`, called by `Adopt` under the existing lock.
  - On re-identification, clear `ManagedTitle` and `ManagedBackground`.
  - Drop a shell that came only from `BELLTOWER_SHELL_PID`, keeping the ancestry shell or none. This needs an additive shell-provenance field in the capture.
  - Have the consumer suppress the inherited Name and Change for a re-identified client.
  - Benefit: all inherited-metadata decisions live in one tested place, and the fix fully meets decision 5.
- Behavior-preservation risks: the genuine managed agent's first hook must stay unaffected; rejection already requires a different, live recorded owner. Resume after the owner dies must still replace the attachment. Claude and Codex must remain symmetric.
- Evidence type: source observation. The write path was not reproduced.
- Verification / measurement plan: extend `ANestedClientThatInheritedAManagedIdentifierKeepsItsOwnProcessIdentity` to assert `ManagedTitle`/`ManagedBackground` false and no launch-derived shell. Assert that `TerminalPresentationResolver.Resolve` returns null for the nested session, and that the parent's name and change are not inherited. Use paired Claude and Codex rows.
- Relationship: refines ANALYSIS-500 (layer 2 of its fix).

### ANALYSIS-601: The Codex vocabulary classifies unrecognized subcommands and option-only command lines as an embedded runtime

- Severity: warning
- Related task / requirement: 2.2, 1.1; design decision 3 ("do not ... assume every executable named codex is a dedicated TUI")
- Locations: `src/BellTower/Core/NativeAdoption.cs:53-79` (default arms at lines 75 and 78); `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs:16-32`
- Observation:
  - Only `app-server`, `exec`/`e` and `review` are named. Any other first positional token returns `EmbeddedRuntime`, and so does a command line made only of options (unknown options are skipped without consuming a value). `EmbeddedRuntime` is direct, so process, host and presentation fields are kept.
  - Codex 0.165.0 documents other non-TUI subcommands, including `mcp-server`, `exec-server` (which does not match `exec`), `execpolicy` and `app-server daemon ...`, as well as `login`, `apply`, `cloud` and `sandbox`. Unknown hidden helper flags take the same path.
  - Implementation 019 itself says other subcommands are unverified. The default arm therefore contradicts the policy's own rule that anything not provably direct fails closed for Codex.
  - Which of these modes run lifecycle hooks is unverified. `mcp-server` hosts conversations, so it is the most plausible.
- Proposed improvement and benefit:
  - Invert the default: add a version-scoped table of known non-TUI subcommands for 0.165.0 that map to `Unknown`, which fails closed to the shared runtime.
  - Treat a token as a TUI prompt only when it is not a known subcommand name. This mirrors clap, which resolves subcommands before the positional prompt.
  - Treat option-only command lines as `EmbeddedRuntime` only when every option is a known TUI option.
  - Benefit: unverified modes keep conversation tracking but cannot claim a host or presentation.
- Behavior-preservation risks: plain `codex`, `--no-daemon`, a prompt, `resume` and `fork` must stay embedded. A future subcommand missing from the table would still be read as a prompt; record that limit in diagnostics (5.2) and refresh the table with the provider version gate.
- Evidence type: source observation plus primary provider documentation. Hook emission of the listed modes is not verified.
- Verification / measurement plan: add theory rows `mcp-server`, `exec-server`, `execpolicy check`, `login`, `cloud` and `--unknown-flag`, each expecting `Unknown`. Keep the TUI rows, and keep a paired Claude row to confirm its vocabulary is unchanged.
- Relationship: new. It compounds ANALYSIS-600 for a nested `mcp-server`.

### ANALYSIS-602: Persisted lifecycle owner duplicates Ownership and is maintained by four writers with an asymmetric learning rule

- Severity: suggestion
- Related task / requirement: 2.1
- Locations: `src/BellTower/Core/AttachmentEvidence.cs:73-83,96-108`; `src/BellTower/Core/NativeAdoption.cs:164,170,180,193`; `src/BellTower/IO/AttachmentStore.cs:48-49`; `src/BellTower/Sessions/AgentLauncher.cs:19`
- Observation:
  - Every current writer sets `LifecycleOwner` as a function of Ownership and whether a Process is present. The writers are `Apply`, the re-identification in `Adopt`, `AgentLauncher` and `Of`, and each must keep both fields in sync.
  - `Merge` learns `Mode` when the stored value is `unknown`, but it never learns `LifecycleOwner`. A legacy `observed` record without a process therefore stays `unknown` permanently, even after a later hook adds the process.
- Proposed improvement and benefit: until 2.3 introduces a case where the two values diverge, either derive the owner on read, or compute it in one shared function and give it the same learn-from-unknown rule as `Mode`.
- Behavior-preservation risks: none today, because nothing reads the field.
- Evidence type: source observation.
- Verification / measurement plan: one test where a legacy observed record without a process receives a hook with a live process and ends with `agent-process`.
- Relationship: new.

### ANALYSIS-603: Evidence is not tied to the generation it describes, and the 4.2 binder output needs a defined home before 2.3 consumes evidence

- Severity: suggestion (no current behavior depends on it; it shapes the design of 2.3, 2.4 and 4.2)
- Related task / requirement: 2.1, 2.3, 2.4, 3.x, 4.2; design decisions 2-4 and the compatibility risk
- Locations: `src/BellTower/Core/AttachmentEvidence.cs:33-50,56-67,96-108`; `src/BellTower/IO/AttachmentStore.cs:101-110`
- Observation:
  - **Stale evidence across versions.** An older tray keeps `evidence` as extension data. On a resume replacement it rewrites Process, Host and RegisteredAt but carries the previous generation's evidence forward. A newer tray then merges the next hook as a non-replacement and keeps the stale Mode, Terminal and lifecycle owner. For example, a headless generation's `Terminal: none` would survive into a later interactive generation. This is harmless while evidence is write-only, but it would mislead a 2.3 or 4.x reader.
  - **What 4.2 needs.** The binder needs the client's PID and creation time, the endpoint port, an ordering key for the latest-only rule, an observation time and re-validation at the action boundary. `TerminalEvidence` has only `{Binding, Source}`. It is extensible, but nothing is reserved yet.
  - **Wrong home for binder output.** Writing the bound client into the shared-runtime record's `Attachment.Process`/`Host` would switch on the direct-process health and exit rules. A terminal exit would then end the conversation, which is exactly what 2.3 must prevent, and it would collide with the rule in `Adopt` that late hooks cannot transfer ownership.
  - **Latest-only demotion.** Under the latest-only rule, a newer binding demotes older ones, possibly on other attachments. A persisted `verified` value would therefore be a cache, not authority.
- Proposed improvement and benefit:
  - Before 2.3 adds any evidence reader, stamp `AttachmentEvidence` with the generation it describes (RegisteredAt or the process identity) and a schema revision.
  - Treat a mismatched stamp as absent and fall back to `Of(attachment)`.
  - Reserve the 4.2 destination fields in `TerminalEvidence` (client identity, shell, ordering key, observation time).
  - Document that action-time code re-derives actionability instead of trusting a stored `verified` value.
  - Benefit: 2.3, 3.x and 4.2 extend the model additively, and the 2.4 gate covers only the semantic change.
- Behavior-preservation risks: the stamp must be additive and tolerate records that lack it.
- Evidence type: source observation (forward-looking architecture).
- Verification / measurement plan:
  - A round-trip test: write evidence for generation A, simulate an old-tray replacement that keeps the extension data, apply a new-tray hook, and expect evidence re-derived for generation B.
  - In the 4.2 tests, assert that the shared-runtime record's Process stays null while a binding exists.
- Relationship: new. It is the condition attached to the compatibility conclusion below.

### ANALYSIS-604: Provider vocabulary sits in Core, Core now depends on IO, and the adoption policy is linked into the wire-contract assembly

- Severity: suggestion
- Related task / requirement: 2.2; project facts (provider behavior in adapters, shared policy in Core); `docs/development.md`
- Locations: `src/BellTower/Core/NativeAdoption.cs:1-2,10-79,160`; `src/BellTower.Protocol/BellTower.Protocol.csproj:13-14`; `src/BellTower/BellTower.csproj:15`; `src/BellTower.Ingress/BellTower.Ingress.csproj:12-15`
- Observation:
  - The Claude and Codex argv vocabularies, the Codex value-option list and `MayShareBackend` are adapter knowledge placed in Core.
  - `NativeAdoption.cs` is the only Core file that imports `BellTower.IO` (for `ContentHash`).
  - `AttachmentEvidence.cs` belongs in Protocol because it is serialized in the envelope. `NativeAdoption.cs` is not part of the wire contract; its only ingress caller, `AttachmentCapture`, is linked into Ingress directly.
  - Behavior is correct, and there is a single shared policy.
- Proposed improvement and benefit:
  - Move the vocabulary functions and `MayShareBackend` into a Providers adapter file, linked the same way as `HookAdapter.cs`.
  - Keep `Apply` in Core, taking the mode and the may-share fact as inputs.
  - Link the policy file into Ingress next to `AttachmentCapture` rather than into Protocol.
  - Benefit: the documented layering holds, and Protocol stays the wire contract.
- Behavior-preservation risks: compile-link changes only. Keep the `Compile Remove` list in `BellTower.csproj` consistent.
- Evidence type: source observation.
- Verification / measurement plan: a Release build, including the AOT ingress publish in 6.3, with the NativeAdoption tests unchanged.
- Relationship: new.

### ANALYSIS-605: Ingress performs host discovery before classification and discards it for shared and headless hooks

- Severity: suggestion
- Related task / requirement: 2.2, 6.3; the design risk on hook latency
- Locations: `src/BellTower/Hosts/AttachmentCapture.cs:12-17,21-59`; `src/BellTower.Ingress/Program.cs:33-43`
- Observation:
  - `Resolve` runs the full `Capture` first: an ancestry snapshot, a possible second snapshot from the shell, `WindowProbe.SingleVisibleWindow` and three `Identify` calls. Only then does it classify.
  - For `shared-backend` (Codex's default mode) and for `headless`, `Apply` discards the host, shell, window and handle.
  - `Program.cs` takes another full ancestry snapshot afterwards. That double snapshot predates this change.
  - The new command-line read is not the cost; it measured about 1.9 us on a replica.
- Proposed improvement and benefit:
  - Find the agent, read and classify its command line, and capture the host only for direct modes.
  - Share one ancestry list between `Resolve` and the evidence ancestors in `Program.cs`.
  - Hypothesis: shared Codex hooks skip window enumeration and up to one snapshot, and every hook skips one snapshot.
- Behavior-preservation risks: keep the current evidence-ancestor filtering and shell selection in `Program.cs`. Claude, which is direct by default, keeps its host capture.
- Evidence type: performance hypothesis. Only the command-line read was measured, and only on a replica.
- Verification / measurement plan: compare median and p95 for Codex shared-mode and Claude hooks before and after the change in the existing task 6.3 provider-shell benchmark.
- Relationship: new.

### ANALYSIS-606: Small redundancies left by the consolidation

- Severity: suggestion
- Related task / requirement: 2.2
- Locations: `src/BellTower/Core/NativeAdoption.cs:149`; `src/BellTower/Providers/HookIngestor.cs:20`; `src/BellTower/Hosts/AttachmentCapture.cs:45` and `src/BellTower/IO/AttachmentStore.cs:47`
- Observation:
  - `AdoptionOutcome.Mode` duplicates `Attachment.Evidence.Mode` and no product code reads it.
  - `HookIngestor` re-applies `Ownership = dedicated ? ... : "shared-runtime"`, which `Apply` already guarantees. The same line in `CaptureConsumer` is still needed for captures from older ingress images.
  - The process identifier format `{provider}-{pid}-{ticks}` is written out in two files.
- Proposed improvement and benefit: remove the unused member and the redundant override, and add one identifier helper in Core (for example `Attachment.ProcessId`).
- Behavior-preservation risks: the identifier text must stay byte-identical for existing records.
- Evidence type: source observation.
- Verification / measurement plan: the existing nested-client test asserts the identifier text, and the legacy-ingestion test covers the override.
- Relationship: ANALYSIS-600 touches the same `AttachmentStore` line.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-500 | `head=2f1de7f ... diff=679cff63262d untracked=5/bf90b082dd7c` | Source trace: layer 1 (`app-server`) holds for both providers; layer 2 leaves the inherited presentation fields (ANALYSIS-600). Not a formal recheck. | Not performed as a recheck; partial-fix observation recorded in ANALYSIS-600. |

## Assessment and limitations

- **Duplication and parity.** The consolidation meets 2.2. One policy serves both entry points and both providers, the provider differences are limited to the argv vocabulary, and the tests are matched per provider. There is no duplicated per-provider policy. The parity concerns are where the code lives (ANALYSIS-604) and the Codex fail-open default (ANALYSIS-601), not divergent flows.
- **Classification boundary.** Classifying in the ingress is right. The command line has to be read while the hook's ancestor is still alive and before its PID can be reused, and the consumer may run much later because captures stay durable while the tray is stopped. Only the classification is persisted.
  - Trade-off: policy that runs in an immutable ingress image only changes when that image is refreshed. Implementation 019 already records this for the ANALYSIS-500 weakness in cached older images.
  - Optional: the consumer could re-apply `Apply` idempotently whenever `Evidence.Mode` is present.
- **Sharing into BellTower.Protocol.** Appropriate for `AttachmentEvidence`, which is part of the envelope and covered by the source-generated `CaptureJson` graph. Unnecessary for `NativeAdoption` (ANALYSIS-604).
- **Performance.** The added per-hook work is one `OpenProcess` plus one `NtQueryInformationProcess`, about 2 us on a replica against a median budget below 300 ms. That figure is not from the AOT product under concurrency; task 6.3 remains authoritative. The AttachmentStore merge cost is negligible next to the existing per-hook atomic write.
- **Compatibility claim.** I agree that no gate is needed for 2.1-2.2:
  - the evidence is additive;
  - older trays retain it through `JsonExtensionData`;
  - no product code reads it for behavior;
  - `Dedicated` keeps its meaning.

  I also agree that 2.3 needs a gate or semantic version bump. The condition is that the generation stamp from ANALYSIS-603, or a gate covering the stale-evidence case, lands before any 2.3 reader trusts evidence.
- **Triage order.** ANALYSIS-600 and 601 before closing 2.2. ANALYSIS-603 before starting 2.3. The rest are opportunistic.
- **Not performed.** Build, test and format verification; the AOT publish; product latency measurement; any provider run, including checking whether `codex mcp-server` emits hooks. My only write attempt was this report, which the client refused, so the text is inline.
- **Decision needed.** Should nested interactive or embedded clients keep navigation to the parent terminal at all? This report only recommends removing their presentation consent and the launch-configured shell.
