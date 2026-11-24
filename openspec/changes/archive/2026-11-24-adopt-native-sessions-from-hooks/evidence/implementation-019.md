# Implementation 019: shared adoption evidence and native mode classification (tasks 2.1 and 2.2)

Developer assignment DEV-019. Baseline `2f1de7f`, uncommitted working tree. Implementation evidence awaiting independent QA and code-analyzer review; nothing here approves itself or closes a finding (ANALYSIS-500 is addressed and awaiting verification).

## Design choices

### Task 2.1: independent evidence facets

- New additive `Attachment.Evidence` (`AttachmentEvidence`, `src/BellTower/Core/AttachmentEvidence.cs`) with four independent facets:
  - conversation health (`unverified` or `hook-observed`, plus the latest observation time);
  - runtime access (`none` by default, reserved for `owned` or `observer`; unwritten by this task);
  - terminal binding (`unverified` by default; `none` for headless; no binder exists yet, task 4.2 is untouched);
  - lifecycle owner (`bell-tower`, `agent-process`, `shared-backend`, `unknown`).
  - It also records the classified `mode` (`interactive-client`, `embedded-runtime`, `shared-backend`, `headless`, `unknown`).
- Legacy `Health`, `Ownership` and the process attachment health evaluation are unchanged. Records written before this change read with `Evidence == null`; `AttachmentEvidence.Of` derives a conservative view (managed to `bell-tower`, shared-runtime to `shared-backend`, observed with a process to `agent-process`, otherwise `unknown`).
- All new records carry `JsonExtensionData`, as the existing attachment, host and envelope records do, so unknown fields at every level survive a round trip through `AttachmentStore.Adopt`.
- `AttachmentStore.Adopt` merges evidence: an established mode, terminal binding, runtime access and lifecycle owner are never rewritten by a later hook. Only the explicit resume replacement adopts the new generation's facets. Each accepted hook records conversation health and a monotonic observation time. A legacy managed record keeps `bell-tower` ownership and learns only the mode.
- PID plus creation-time checks are unchanged: `freshAgent` still requires `ProcessProbe.Inspect == Live`, a late non-resume hook from a different process still cannot transfer an attachment, and health evaluation still reports `confirmed-ended` for a mismatched creation time.
- Evidence persistence writes only the attachment file, so identity palettes and attention state are untouched (byte-for-byte asserted).
- `AgentLauncher` records `lifecycleOwner = bell-tower` on managed launches.

### Task 2.2: one classification and adoption policy

- `AgentModeClassifier` (`src/BellTower/Core/NativeAdoption.cs`) classifies the hook's agent ancestor from its command line. The ingress adds exactly one bounded `NtQueryInformationProcess(ProcessCommandLine)` read for that single process (`ProcessProbe.CommandLine`; failure or an elevated target returns null). No host discovery, no RPC, no tray dependency, no stored command text: only the classification persists.
- Provider vocabularies are the only per-provider code:
  - Claude: `-p` or `--print` is headless (verified, claude-native-001); any other readable invocation is an interactive client.
  - Codex 0.165.0: `app-server` (including `--managed-daemon`) is the shared backend; `exec` or `e` is headless (verified, codex-headless-004); no subcommand, `resume`, `fork` or a prompt is an embedded runtime (the TUI hosting its hooks in-process). `review` and anything unreadable are `unknown`. Option values are skipped using a short list of value-taking options.
- `NativeAdoption.Apply` is the single shared policy: interactive client and embedded runtime keep process and host as captured; headless keeps the process (lifecycle evidence) but never claims a host, terminal, title or background; shared backend and anything not provably direct for a provider that can share a backend (Codex) fail closed to `shared-runtime` with no process, no host and a session-derived identifier. An unreadable Claude command line keeps its existing direct classification (no shared Claude backend is known).
- The `BELLTOWER_DEDICATED` flag is no longer read by ingress or `HookIngestor`. It is not ownership evidence. The module and launcher still set it (harmless); retiring it is a later cleanup.
- `AttachmentCapture.Resolve` is the shared entry used by both the fast ingress and legacy `HookIngestor` (which gained an optional injectable resolver for tests), so the duplicated shared-branch code in both is gone.
- ANALYSIS-500 fix, two layers:
  1. A hook whose agent ancestor is `app-server` is shared-backend regardless of any inherited `BELLTOWER_*` flag, attachment identifier or shell variable: no process, no host (tested for both providers with a live managed launcher record, which stays byte-identical).
  2. A nested interactive/embedded/headless client that inherited a managed `BELLTOWER_ATTACHMENT` is checked at adoption: if the recorded launch process is still live and differs from the hook's agent, the client gets its own `provider-pid-ticks` identity (`observed`, lifecycle `agent-process`) and `CaptureConsumer` re-stamps the observation's attachment identifier. A dead recorded owner still permits the existing resume replacement.
- Missed-SessionStart recovery: a first event of any kind (tested with UserPromptSubmit) creates the session and attachment, records conversation health, and synthesizes no wait or past attention.

## Files changed

Product: `src/BellTower/Core/{Attachment,AttachmentEvidence,NativeAdoption}.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/{AttachmentStore,ProcessProbe}.cs`, `src/BellTower/Providers/{CaptureConsumer,HookIngestor}.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, `src/BellTower.Ingress/Program.cs`, `src/BellTower.Protocol/BellTower.Protocol.csproj`, `src/BellTower/BellTower.csproj` (the two new Core files are shared with the protocol library like the other capture-shared sources).

Tests: new `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs` and `AttachmentEvidenceTests.cs`; updated `tests/BellTower.Tests/Setup/PowerShellModuleTests.cs`: its Codex fake agent is now a process image named `codex.exe` (a copy of `cmd.exe`) because adoption needs process evidence instead of the flag.

Tests are matched per provider (Claude Code and Codex theory rows) for classification, policy, inherited-metadata and nested-client cases, the legacy ingestion path, missed-start recovery, backward read, extension-data retention, merge rules, resume replacement, creation-time checks and no palette or attention reset.

## Compatibility assessment (task 2.4 precursor)

No compatibility gate is required for this change, so none was added or modified.

- Attachment files: the new `evidence` property is additive. An older tray ignores it, retains it through `JsonExtensionData` when it rewrites the record, and neither reads nor derives behavior from it. The legacy `health`, `ownership`, `process` and `host` fields keep their meaning.
- New capture shape: `Dedicated` keeps its meaning (a direct process attachment). An older tray consuming a capture from the new ingress sees either a shared-runtime branch that it already handles, or a direct attachment (including an unwrapped Codex TUI or `exec`) that it treats exactly as it treats Claude: agent liveness drives health. Unknown `evidence` is retained as extension data.
- Not yet new semantics: this task changes no reduction, health or lifecycle rule, so `attachmentHealthVersion` and the capture-v1 contract stay valid. Task 2.3 (terminal disconnect versus continuing verified work, ending rules for shared conversations) is where an older tray could misread the persisted state; assess and gate that task (likely an `attachmentHealthVersion` bump through the existing `AttachmentHealthCompatibility`) before persisting those semantics.
- Reverse direction: a cached older ingress image keeps writing the legacy shape (`Dedicated` from the flag, no `evidence`). The consumer derives evidence from ownership and still treats it correctly, but that image retains the ANALYSIS-500 flag weakness until the setup refresh replaces it.

## Checks

See the handoff for exact commands and counts. The pinned SDK 10.0.113 is not installed here (10.0.114 is, `rollForward: disable`), so builds and tests ran with `global.json` temporarily set to 10.0.114. That edit was reverted before handoff and is not part of this change.

## Limitations and left for later tasks

- Not a terminal binder: terminal binding remains `unverified`; shared-Codex client-endpoint binding (4.2), runtime transport (3.x), host routing (4.1) and presentation (4.3) are untouched.
- Classification of Codex subcommands other than `app-server`, `exec`, `resume`/`fork` and TUI is unverified (`review` is `unknown`, hence fail-closed for Codex). Claude `stream-json` IDE or SDK invocations without `-p` are still classified interactive (unverified, same as before this change).
- Command-line read can fail for an elevated or protected agent; Codex then fails closed to shared-runtime, Claude keeps its existing direct classification.
- The npm and executable entry-point probe records for task 1.1 and a real native hook run through the new ingress were not performed: no provider launches. Mode vocabulary rests on the retained probes (claude-native-001, codex-headless-004, native-adoption-009/010 and the shared-backend ancestry in `native-probes/hook-captures.json`) plus fixtures.
- The AOT ingress publish and measured hook latency were not run; the added work is one single-process query in the ingress. Measurement belongs to task 6.3.
