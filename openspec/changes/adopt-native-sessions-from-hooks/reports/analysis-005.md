# Analysis report: adopt-native-sessions-from-hooks / 005

## Review target

- Reviewer / role: setup_analysis / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / delegated Default mode / gpt-6.1-sol (high).
- Date: 2026-10-29T21:10:44Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-005, bounded source-only SessionStart investigation.
- Task and requirement references: tasks 1.1-1.3 and 6.1; native dispatch, exact process evidence and shared-client binding. Full terminal support remains required.
- Scope: `src/BellTower.Ingress/Program.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/Providers/HookIngestor.cs`, `src/BellTower/Providers/HookAdapter.cs`. Relevant neighbors: `ProcessProbe`, public Codex release-tag source, ignored private handler/setup metadata. Read [native-adoption-005](../evidence/native-adoption-005.md), [006](../evidence/native-adoption-006.md), [007](../evidence/native-adoption-007.md), [008](../evidence/native-adoption-008.md), [QA-005](qa-005.md), [QA-006](qa-006.md), [QA-007](qa-007.md) and the prior Claude product-ingress evidence in [QA-004](qa-004.md).
- Baseline commit: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Prior report: [analysis-004](analysis-004.md); this assignment is investigation, not a correction recheck.
- Implementation identity, before and after: the exact four-path command `scripts/review-identity.ps1 -Baseline a69392e9ae6734b7f229d9266ebe4a3144ef49a4 -Paths src/BellTower.Ingress/Program.cs src/BellTower/Hosts/AttachmentCapture.cs src/BellTower/Providers/HookIngestor.cs src/BellTower/Providers/HookAdapter.cs` produced matching full outputs:

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: src/BellTower.Ingress/Program.cs src/BellTower/Hosts/AttachmentCapture.cs src/BellTower/Providers/HookAdapter.cs src/BellTower/Providers/HookIngestor.cs
Manifest (A added, M modified, D deleted, ? untracked):
Tracked diff SHA-256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
Untracked file SHA-256:
Summary: head=a69392e baseline=a69392e files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc
```

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Version-specific dispatch | Inspected official `rust-v0.165.0` source downloaded to ignored `artifacts/binder-analysis-004`; used repository Context7 documentation workflow for supporting current documentation | Exact-tag call sites below; public source tree `360319d8dee62d87e2d59fc0c21805d79519cfed` | Source observation, not execution or installed-binary equivalence proof; current main documentation is not installed-version proof. |
| Current private prerequisites | Read only selected boolean/key/shape metadata from owned `artifacts/n3c` and source of `artifacts/n5u/hook.ps1` | Trusted-hash record present, handler enabled true, command references the private script, SessionStart timeout 15 seconds; no explicit hook-feature or daemon override | No credentials, prompt/event bodies, production sessions or database accessed. |
| Capture reliability | Inspected private handler and existing four-source product ingress, plus `ProcessProbe` neighbor | Custom handler has an unguarded CIM ancestry loop before its sole capture write; product ingress uses existing bounded process capture | Missing custom output does not identify the provider dispatch stage. |
| Existing dynamic evidence | Read QA-007 and earlier reports | Outside-context preflight passed; actual 25-second native exec attempt timed out without capture; setup hashes remained unchanged | This role performed no native launch or synthetic replay. |
| Review integrity | Scoped identity before/after, source inspection and report hygiene check | Identical four-path target above | No product/config/helper edits. Build/tests not run: no implementation changed. |

### SessionStart ordering and gates

Both ordinary CLI and exec sessions use the same core SessionStart dispatch. Session construction records initial history and queues the startup source; it does not immediately execute the hook ([session initialization](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/session.rs#L2003)). The first actual turn eventually calls `run_pending_session_start_hooks`; ordinary sources select SessionStart, while internal subagent sources have separate handling ([dispatch](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/hook_runtime.rs#L130)). Creating or displaying a thread without submitting a turn is insufficient.

The turn reaches that call only after asynchronous initialization, pre-sampling compaction, required MCP/context work and skill/plugin preparation. Hook dispatch precedes the ordinary sampling loop, but network-dependent initialization and socket prewarming can happen earlier ([turn ordering](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/turn.rs#L173), [hook call](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/turn.rs#L340), [thread prewarming](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/request_processors/thread_processor.rs#L1546)). A successful model response is not intrinsically required before this hook, but a closed endpoint and a fixed 25-second budget are not a guarantee that startup reaches it. QA-007 retained no stage trace capable of identifying the stopped await; catalog delay remains a hypothesis.

Exec enforces login restrictions before initializing its in-process app server, creates/loads a thread, then sends TurnStart for a user-turn operation ([exec initialization](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/exec/src/lib.rs#L657), [in-process startup](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/exec/src/lib.rs#L1022), [TurnStart](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/exec/src/lib.rs#L1210)). This supports a headless dispatch probe, not shared-daemon or terminal acceptance. The exact-tag exec hook integration test exercises this mechanism with a mocked endpoint, but is excluded on Windows and uses a trust bypass; it does not prove the required Windows normal-trust scenario ([test](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/exec/tests/suite/hooks.rs)). No bypass is recommended.

Required prerequisites are a loaded applicable hook definition, the hooks feature, applicable managed policy, an accepted definition hash, handler enablement, and a submitted first turn reaching dispatch. The `hooks` feature is stable and enabled by default ([feature entry](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/features/src/lib.rs#L1404)); the core passes it into hook configuration ([configuration](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mod.rs#L5633)). Discovery separately checks trust and enabled state: trusting a hash does not override `enabled = false` ([trust selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/hooks/src/engine/discovery.rs#L917), [enable selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/hooks/src/engine/discovery.rs#L1019)). The repaired current private record is enabled; earlier disabled-handler results cannot establish native dispatch failure.

The UI maps Enter on a selected handler to enable/disable, whereas `t` sends the trust action ([key handling](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/bottom_pane/hooks_browser_view.rs#L590)). An older helper appending Enter after `t` could therefore explain the disabled record. No immutable byte trace establishing that sequence was found; this remains a source-consistent hypothesis, not a proven cause. Leave accepted trust state untouched.

The request uses the core session identity, not an independently verified terminal/client identity. Core exposes distinct conversation-thread and session accessors, with session identity shared across a root and descendants ([accessors](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/session.rs#L695)). No new assumption that every hook session ID always equals every concrete protocol thread ID is justified.

### Supported local catalog fixture

Root session creation selects `OnlineIfUncached` and can await catalog initialization even with an explicit model ([session catalog selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/mod.rs#L738)). The normal catalog manager refreshes according to provider/auth capabilities and discovery settings; some API-key configurations skip refresh entirely ([refresh logic](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/models-manager/src/manager.rs#L587)). The strategy name alone does not prove that this private provider attempted a catalog request.

There is a supported deterministic alternative: top-level `model_catalog_json` in the private startup configuration, with an absolute path to a public JSON file. It is startup-only; thread-level config changes do not reapply it ([configuration field](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/config/src/config_toml.rs#L408)). The loader parses `ModelsResponse` and rejects an empty model array ([loader](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/config/mod.rs#L2223)). The provider immediately selects StaticModelsManager when that catalog exists; its catalog method ignores refresh strategy and HTTP client ([provider selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/model-provider/src/provider.rs#L400), [static implementation](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/models-manager/src/manager.rs#L869)). Thus this removes catalog network/cache dependence without fabricating a cache, auth result or provider status. It does not disable unrelated startup operations or security controls.

Use the unmodified [official tagged models.json](https://raw.githubusercontent.com/openai/codex/rust-v0.165.0/codex-rs/models-manager/models.json), already saved as ignored `artifacts/binder-analysis-004/codex-rs__models-manager__models.json`. SHA-256: `1384625c0b6b3e67d3c60da29becea2b017f2c283fc1e2d05b8edafb4698eb10`. Its root is an object containing `models`, a nonempty array of twelve complete model records. Prefer this valid full fixture to inventing a minimal model schema. The existing private explicit model is absent from this catalog; the static manager retains an explicit model when provider fallback is false and constructs local fallback metadata for an unmatched slug ([selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/models-manager/src/manager.rs#L833), [metadata fallback](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/models-manager/src/manager.rs#L975)). No real model availability or completion is thereby claimed.

Any authorized fixture edit must place this key at the TOML root, before table headers, and take effect at provider startup. Setting a catalog does not change the Windows token-elevation branch: elevated local TUI startup forces embedded mode ([startup decision](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L26)). No catalog change converts this tool environment into a shared native client.

### Capture and best next bounded probe

The private PowerShell handler sets Stop-on-error and performs CIM/process ancestry operations before its only final output write. An unavailable ancestor, access denial or lookup error can prevent any capture despite real dispatch. The repaired outside-context preflight proves one synthetic invocation succeeds, not that every actual hook child's ancestry is readable. This observation is about ignored measurement code, not a new product defect.

The strongest next bounded dispatch probe is an authorized disposable setup using the already working product ingress from QA-004, the exact public static catalog above, unchanged read-only sandbox/normal hook trust, a closed model endpoint, and one first-turn exec invocation. Register and trust the exact new product-ingress command through the normal flow in that disposable fixture; do not transplant or overwrite the existing custom command's accepted hash. Before the single native attempt, require a fresh product-ingress preflight and capture PID plus creation time. Validate any resulting envelope by event type, session identity and exact launched process identity in `Ancestors`. Do not set `BELLTOWER_DEDICATED=1`: existing native Codex capture intentionally leaves terminal attachment unproven while retaining ancestry (`src/BellTower.Ingress/Program.cs:33`, `:46`; `HookIngestor.cs:15`). Reuse existing product capture parsing and a metadata-only rejection summary; do not add another WMI recorder. If only the current unchanged custom hook is used first, classify that as a catalog diagnostic with the remaining capture ambiguity.

This role did not create that fixture or authorize a new trust action. A timeout must remain inconclusive unless safe stage metadata identifies the blocking step. No arbitrary prompt/log bodies need to be retained. A passing exec probe establishes direct-process dispatch only. Current tool contexts are elevated, while attempted ordinary-token launch routes failed in prior evidence; a normal-user external run, or a separately proven permitted normal-user launcher, is still needed for the shared case in this environment. This is an environment limitation, not a general Codex restriction or an accepted scope reduction. Do not retry rejected Windows sandbox setup or alter security controls.

Ignored measurement inputs were inspected separately from the Git target. Snapshot SHA-256 values: private handler `176b15c91bac3cf222755890929ae4e8162baac66506657ff61211f658a1643e`; private config `259f15423aab1c166efda9b7cb7c98a087661458e89578c9e992d265587edf27`; hook definition `712bcc0dd30f02fb260c92f34a6af56a47815cf78929e07e018deb89a2a7a8b5`; current helper source `1fc8c1e0200a90de0d5ba6f5824945e64465874af50fbdd6032e486f8f36f8a4`. These are point-in-time research observations, not files covered by the four-path review identity.

## Findings

**No findings.** No new implementation defect is established in the assigned product target. ANALYSIS400-499 remain unused. The source-supported next probe above addresses an evidence gap; it is not native acceptance or proof of the cause of QA-007's timeout.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| Not applicable | Source-only investigation | No correction assigned | No finding closure claimed. |

## Assessment and limitations

The source establishes lazy first-turn dispatch, independent enable/trust gates, and a supported local catalog that eliminates catalog IO. It does not identify the timed-out await or prove dispatch on Windows. No provider/client, hook, process-control, config, database or terminal operation was performed by this reviewer. No production or message content was read. Existing Claude success is a capture-path reference, not matched Claude/Codex or simultaneous-session acceptance. Shared-client ownership, PID-reuse safety and host binding still require actual independent native evidence under the retained full scope. Orchestrator owns triage and all next-step authorization.
