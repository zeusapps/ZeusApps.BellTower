# Design

## Context

StateStore.ReconcileAttachments only ends an explicitly ended attachment or one whose known agent process is gone. A null-process attachment therefore remains working. HookIngestor already retries capture and upgrades when a later agent identity is available, so extend this partial repair rather than duplicating it. LaunchPresentationStore.Consume deletes the record containing the shell identity when consuming the palette. TerminalTitlePump runs every two-second overview refresh, starts a full sync-title helper and retries even invalid process targets; TerminalTitleService creates a new GitReader each time. AttachedSessionConsole uses process-wide FreeConsole/AttachConsole and explicitly requires a helper process.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `speed-up-claude-hook-ingestion`.

**Primary source boundaries:** Hosts/TerminalTitlePump, TerminalTitleService, AttachedSessionConsole; IO/LaunchPresentationStore, AttachmentStore, StateStore and GitReader; launch/detach lifecycle, tray projections and host/process tests.

## Goals / Non-Goals

**Goals:** Expose honest attachment health and bound CPU/process/log costs while preserving console isolation and verified terminal ownership.

**Non-Goals:** Do not expire known-live idle agents, put AttachConsole in the tray, terminate provider processes, or use display names as session identity.

## Decisions

### 1. Durable ownership separate from palette consumption

Persist the launching shell's PID and creation ticks with attachment/launch ownership, independently of the one-shot palette. Preserve it after palette adoption and across tray restart. Recapture missing agent or shell evidence from later ingress envelopes and validate creation time before adoption. Keep observations scoped to the same live attachment, especially after resume creates a replacement process.

### 2. Three health states

Add backward-compatible verified-live, confirmed-ended and unverified/unavailable health. Agent or trusted shell death can confirm end; shell liveness alone does not prove the agent is still active. Apply the same health policy to managed attachments in both clients. Classify unowned/shared-runtime modes separately for either provider with evidence-based limits and safe fallbacks; a mode limitation does not exempt a provider from all new features. Default evidence grace for incomplete managed provider ownership is ten minutes, configurable from one minute to 24 hours. Beyond grace, exclude that stale unknown session from actionable alerts and label it unavailable rather than claiming proven death. Persist the grace origin/status so restart cannot reset it. Only fresh verified evidence or an actual new registered attachment restores live health.

### 3. Tray-owned cache and scheduling

Move identity lookup, desired presentation computation and read-only Git discovery into a shared tray service. Reuse checkout-keyed Git caching (five-second freshness default); invalidate on explicit checkout/branch evidence and metadata changes. Track the last successful desired title, palette and destination identity; an unchanged tick spawns no writer or Git work. Keep Git outside the UI thread with a bounded work queue. The identity change will supply its separate working-checkout resolver to this boundary.

### 4. An isolated lightweight writer

Send only a validated desired title/palette and agent/shell identities to the existing isolated writer boundary; do not reload state and run Git inside it. Reuse a bounded writer process or start a lightweight helper only on actual change. Either choice must keep console attachment process-wide effects outside tray/hooks, enforce creation-time and console-membership checks, and retain the presentation lock shared with detach. Do not implement the report's literal zero-helper requirement at the expense of isolation: zero child launches is required on unchanged refreshes.

### 5. Failure backoff and cancellation

Skip unverified/ended targets before writing. Back off failed destinations at 2, 5, 15, 30 and 60 seconds, reset on fresh verified evidence/change and log only transition plus rate-limited summaries. Cancel or terminate only an owned bounded writer on timeout; WaitForExitAsync cancellation and Process.Dispose alone do not stop a hung child. Never kill the agent/shell. Drain/join writer ownership before detach restores terminal state.

## Risks / Trade-offs

- [Unknown state mistaken for dead] -> Preserve explicit unavailable health and original timestamps; only verified exit establishes end.
- [Late writer changes restored terminal] -> Keep shared presentation-lock/detach ordering and revalidate destination immediately before write.
- [Duplicate caches become incoherent] -> Publish one resolved desired-presentation boundary for title, overview and later identity changes.

## Migration Plan

Read legacy attachments without new health fields as unverified until liveness is checked. Adopt verified shell metadata without changing conversation identity/palette. Introduce durable launch ownership before changing palette consumption. Retain read compatibility with old helpers and preserve unknown JSON fields; fail safely or block rollback if new state semantics exceed a declared contract.

## Verification and fresh-context notes

Fixture cases: null initial process repaired on a later event, lost shell, PID reuse, alive idle agent, stale unknown across restart, ended target never retried, unchanged ticks with zero child/Git launches, one changed write, failed-write backoff, writer timeout cleanup and detach race. Real console probes may target only disposable hidden consoles, with no production state or sound.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 3, 4 and attachment diagnostics in 6.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
