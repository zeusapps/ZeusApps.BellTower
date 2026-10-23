# Hook contract inventory

Checked 2026-10-23 for tasks 1.1 and 1.2. The source-boundary table describes the initial 0.1.8 baseline. The nine-case input inventory is synthetic documented-contract evidence. Subsequent [isolated native probes](provider-timeout-probes.md) retain actual redacted timeout/custom-main evidence; none is an authenticated model trial.

## Versions and transport

| Component | Evidence | Scope |
| --- | --- | --- |
| Bell Tower | `Directory.Build.props`: 0.1.8 | Source baseline |
| .NET SDK | `dotnet --version`: 10.0.113 | Local read-only probe |
| Claude Code | Native `claude.exe --version`: 2.1.306 | Local read-only probe; no conversation started |
| Codex | Native `codex.exe --version`: 0.164.0 | Local read-only probe; emitted a home-directory/PATH-alias warning; no conversation started |
| Git Bash | `C:/Program Files/Git/bin/bash.exe --version`: 5.2.37 | Installed MSYS shell; the System32 `bash.exe` is a different executable |
| Earlier compatibility baseline | Claude 2.1.289, Codex 0.163.0 | Historical repository record, not retested versions |
| Field machine | Claude 2.1.30x; no Codex installed | Reported environment; not reproduced locally |

Current official documentation was retrieved directly and through Context7. Claude's shell form uses Git Bash on Windows when installed; its current exec form can spawn a real executable with an argument vector. Codex exposes `commandWindows`. Provider invocation and quoting still need packaged tests; finding an installed shell is not proof that a provider used it.

Both current contracts support async command handlers. Codex always runs SessionEnd synchronously and cancels unfinished background hooks at session exit. Claude documents its own SessionEnd timeout budget. The generated adapters must account for these lifecycle constraints; async alone cannot prove durable delivery.

## Fixture inventory and provenance

The inputs in [provider-hooks.json](provider-hooks.json) contain invented identifiers, paths and output. Case metadata stays outside provider input. These are minimal examples of the fields Bell Tower needs, not copies of complete provider wire schemas.

| Cases | Verified contract | Evidence still needed |
| --- | --- | --- |
| Claude ordinary/custom main-agent pre | Common fields and tool input; `agent_type` can identify the session's custom main agent; `agent_id` identifies a subagent/teammate | Installed hook capture; do not infer subagent context from `agent_type` alone |
| Claude subagent pre | `agent_id` and `agent_type` in subagent tool context | Authenticated capture |
| Claude successful/failed tool | PostToolUse carries tool_response; PostToolUseFailure carries top-level error | Installed successful/failed selection captures |
| Codex main/subagent pre | Main request omits subagent identifiers; subagent context supplies them | Installed captures including configured agent/profile modes |
| Codex successful/failed tool | PostToolUse also fires for nonzero Bash exits; Bash output can be a string without exit status | Authenticated captures; never equate PostToolUse with success |
| Claude transcript hook timeout | Actual 2.1.306 SDK CLI attachment/hook_cancelled, timedOut/timeoutMs and timestamps | Retained minimal fixture and bounded scanner tests; other provider versions remain separately evidenced |
| Codex transcript timeout | Actual 0.164.0 local exec and terminal probes time out SessionStart without a structured transcript timeout record | Narrow complete-count restriction, shared health fallback and regression fixtures; do not fabricate records or a zero count |

The docs establish custom-main-agent semantics, but do not establish that a custom main agent receives `agent_id`. No fixture asserts that claim. Failed Codex output is labelled by fixture scenario metadata; it deliberately has no invented `exit_code` member in provider input.

## Source boundaries reviewed

| Boundary | Current behavior and required change |
| --- | --- |
| HookCommandBuilder / ProviderSettingsEditor | Encoded PowerShell per event; three-second timeout; ownership records command strings only. Handler edits need structural ownership and explicit migration. |
| HookInput / HookAdapter | Character-count limit and BOM handling; processing timestamp; arguments hashed for correlation; agent_id excludes main-thread work. New ingress requires a byte bound and separate causal evidence. |
| HookIngestor / AttachmentCapture / ProcessProbe | Hook process does ancestry, attachment/identity writes, launch palette consumption and selection. Deferred processing must use captured process creation identities and launch environment, never the tray's process/environment. |
| OpenSpecSelectionReader / Observer | Literal Bash selectors, derived operation hashes, success checks, mandatory pre receipt. Preserve privacy while allowing sufficient successful post-only evidence. |
| Observation / SessionReducer / SessionState | IDs and At only; starts remove completed-operation markers; registration can reopen an ended attachment. Causal receipts must survive delayed delivery. |
| EventStore / StateStore / StateSnapshotStore | Legacy normalized inbox, batches of 2000, commit-before-delete; receipts retained while inbox files remain. New capture inbox must coexist and extend crash receipts. |
| IntegrationSetup / IntegrationOwnership / DiagnosticsReporter | Previewed settings writes and command ownership; no capture health or full handler ownership. Preserve unrelated settings and unknown shared fields. |
| TrayApplication | 250 ms UI timer and shared StateStore; capture processing must stay bounded. |
| package.ps1 / ReleaseContract / UpdateTransaction | Inventoried immutable version payloads, leases, selected/previous retention, schema-1 state checks and rollback. An isolated content-addressed ingress cannot silently enable captures for an incompatible old tray. |

Launch evidence currently consumed includes BELLTOWER_ATTACHMENT, SHELL_PID, DEDICATED, NAME, CHANGE, ROLE, MANAGED_TITLE and MANAGED_BACKGROUND (all with the BELLTOWER_ prefix), plus TERM_PROGRAM and WT_SESSION. BELLTOWER_CODEX_TOKEN is a credential and must never enter the envelope. This is an inventory, not the finalized protocol allowlist.

## Resolved timeout fixture investigation

The initial field notes and release handoff left the timeout schema unverified. Current Claude documentation and Context7 did not define that JSONL shape. The implementation subsequently generated a controlled Claude 2.1.306 capture and Codex 0.164.0 positive/negative controls, resolving the needed fixture evidence without personal data.

See [the exact modes, retained paths, redactions and restriction](provider-timeout-probes.md). No user-supplied transcript is required. The Claude reader uses captured fields; Codex uses an explicit unavailable-count status in the verified modes and the common capture-health fallback. Other modes/versions remain unverified rather than inheriting an unsupported exception.

## Primary sources

- [Claude hooks](https://code.claude.com/docs/en/hooks): common/tool/failure input, custom agent context, Windows execution and async lifecycle.
- [Codex hooks](https://learn.chatgpt.com/docs/hooks): common/tool input, failed Bash output, async limits and unstable transcripts.
- [Codex PreToolUse source](https://github.com/openai/codex/blob/main/codex-rs/hooks/src/events/pre_tool_use.rs): main versus subagent serialization; moving main-branch source, not installed-version proof.
- [.NET NativeAOT](https://github.com/dotnet/docs/blob/main/docs/core/deploying/native-aot/index.md) and [JSON source generation](https://github.com/dotnet/docs/blob/main/docs/standard/serialization/system-text-json/source-generation.md): isolated AOT project and generated serializer requirements; native publication and the packaged neutral probe passed.
