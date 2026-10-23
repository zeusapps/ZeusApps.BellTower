# Design

## Context

Baseline: 0.1.8, .NET SDK 10.0.113, Windows x64. HookCommandBuilder encodes a PowerShell pipeline; the launcher then leases/starts the full WinForms application. EventCommand calls HookIngestor, which probes ancestry, writes identity and attachment data, recognizes selections and publishes a normalized Observation. HookAdapter timestamps processing; OpenSpecSelectionObserver requires a pre receipt. The field Windows 11/RDP/PowerShell 5.1/VS Code 1.141 install measured 5.0 s median and 6.5 s maximum for the full hook chain; these are reported measurements, not newly reproduced results. Roughly 80/110 pre hooks and 68/103 post hooks timed out at three seconds.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** none; implement against the shipped 0.1.8 baseline.

**Primary source boundaries:** New focused ingress project; Providers/HookIngestor, HookAdapter, OpenSpecSelectionObserver; IO/EventStore and StateStore; Core/Observation and SessionReducer; Setup/HookCommandBuilder and ProviderSettingsEditor; Diagnostics; packaging, version leases and process/ordering tests.

## Goals / Non-Goals

**Goals:** Make capture fast and durable independently of tray availability and make later processing causally correct. Deliver fast durable capture for both clients while preserving data and safe runtime ownership.

**Non-Goals:** Do not port the WinForms application to NativeAOT, start an agent/service from a hook, bypass version-retention safety, or write provider decision output.

## Decisions

### 1. A dedicated ingress with an immutable installation path

Add a small console project, provisionally src/BellTower.Ingress, with NativeAOT win-x64 publishing and AOT-compatible JSON contracts. Reference only a small pure protocol/parser boundary, never BellTower's Windows Desktop assembly. Package the executable in the versioned payload; explicit setup copies its verified bytes into an app-owned content-addressed directory under the selected data root, such as integrations/provider-ingress/<protocol>/<hash>/. Hook commands point directly there. This bypasses the launcher without changing current.json or racing cleanup of payload directories. Never overwrite a referenced ingress image; retain old compatible ingress binaries for cached hooks until references are explicitly retired. Updating root bootstrap or assuming copied versioned executables can be deleted was rejected.

### 2. Capture before expensive work

Read at most the existing one-MiB input budget, strip a UTF-8 BOM and capture envelope ID, protocol version, provider/session, hook kind, ingress time, available source/provider timestamps, hook-shell creation time, process identities including creation ticks, attachment ID, approved launch environment fields and transcript location. Parse/retain only fields needed for lifecycle/correlation and bounded derived identity candidates; hash arbitrary tool arguments rather than spooling full commands, responses or transcripts. Make any shared pure selector emit candidate/evidence records so later identity improvements do not require persisting raw private input. Atomically write one envelope using a unique ID and return {}. No Git, network, audio, console attachment, state lock or installation mutex in the critical path. If extra parsing threatens the latency budget, split capture-safe candidate extraction from deferred bounded transcript recovery, not into an unbounded raw spool.

### 3. Durable background consumer

Add a separate protocol-labelled capture inbox, leaving the existing normalized inbox readable for old helpers of either client. The tray drains bounded batches, validates protocol/ownership, normalizes and commits effects before deleting capture receipts. Stopped/restarting trays leave durable captures untouched. Define cleanup, protocol rejection, corrupt-envelope and crash-replay behavior. Startup compatibility must reject an old tray that cannot consume an enabled new capture protocol, or continue compatible old normalized ingestion until the new tray is ready; do not claim unsafe application rollback is supported. Pin/retain ingress compatibility through updates and expose pending/rejected counts.

### 4. Causal identity instead of receipt ordering

Store capture time separately from processing time. Prefer verified provider/source order and use hook-shell creation evidence when available; record confidence when unavailable. Timestamping alone does not reconstruct arbitrary async ordering. Give completed operations, turn boundaries and attachment end markers durable precedence; stale registration/start/notification cannot reopen resolved state. Successful post-tool selection evidence is authoritative without pre delivery. Define deterministic tie-breaking and keep any reorder buffering bounded. Preserve compatible envelopes and valid receipts while testing new causal behavior with both clients.

### 5. Ownership-aware direct setup and diagnostics

Use one ingress executable/protocol with adapters for Claude and Codex. Verify installed hook contracts and generate direct fast commands, enabling async where supported. A verified hard restriction can change the transport mechanics, but not exempt one client from the same durable-delivery and latency outcome. Quote for Claude's actual Windows shell with fixture paths containing spaces, apostrophes and Unicode. Preview full handler changes and use an explicit migration choice for edited owned entries, including the user's timeout:3/async:true hand edits. Add typed sanitized failure codes and bounded doctor summaries for rejection/latency, unverified sessions and optional transcript timeout records. Do not log exception messages or complete payloads.

### 6. Performance evidence matches deployment

Keep the existing development-executable test as a component check, and add a real packaged provider-shell benchmark with at least 100 samples, parallel sessions, Unicode and cold/warm starts. Required median is below 300 ms on the representative loaded setup; record p95/p99/max and all durable event counts. Because async ignores provider timeout, separately prove ordinary capture finishes and persists within three seconds. Dependency failure must not masquerade as a successful latency result.

## Risks / Trade-offs

- [Async start order differs from provider event order] -> Carry available causal evidence, retain uncertainty and test missing/late pre events explicitly; do not promise source order from a processing clock.
- [Old consumer or rollback cannot understand captures] -> Version the protocol and declare compatible tray/ingress ranges before enabling it; preserve normalized legacy ingestion during migration.
- [NativeAOT toolchain is absent] -> Install documented Windows compiler prerequisites in build/CI; do not silently ship the slow full-app path as the performance fix.

## Migration Plan

Provision the verified helper first, validate its neutral read-only protocol/version probe, then atomically apply previewed owned handler changes. Existing trusted hooks and unrelated handlers remain intact until replacement succeeds. Retain a recorded old handler/protocol path for rollback only when the selected tray can consume it. No active agent or editor restart is required or forced; provider trust review remains explicit. Add the helper to package inventory and future signing coverage.

## Verification and fresh-context notes

Use isolated homes/data and fixture agents. Cover direct quoted commands, matched provider setup, one-MiB limits, fail-neutral output, partial writes, duplicate/shuffled delivery, process-start identities, tray-off ingestion, crash replay, old/new helper coexistence, rollback compatibility and edited-hook migration. Current docs checked via Context7: https://code.claude.com/docs/en/hooks and https://github.com/dotnet/docs/blob/main/docs/core/deploying/native-aot/index.md plus System.Text.Json source-generation guidance. Refresh installed-version contracts before implementing. Specifically verify custom main-agent versus subagent hook metadata and transcript timeout record fields using small redacted fixtures.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 1, 2 and 6; prerequisite for dependable naming and permission signals.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
