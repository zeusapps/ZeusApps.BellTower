# Design

## Context

The eight retained feature changes require integrated release acceptance. On 2026-10-29 the user cancelled remote webhook notifications, including Teams; their implementation and acceptance requirements are removed, not deferred. The source timing test invoked the development executable through PowerShell, omitting Git Bash and the stable launcher, while field hooks took seconds and timed out. Current live records include prior accepted behavior as well as contrary work-machine failures; fixture success cannot override those reports. The session-title and badge-identity changes were archived with user-approved transfer of three unfinished live checks to tasks 6.1-6.3 and retention of the fork parity gap under task 6.4. Their specs were composed oldest first, preserving newer role-free naming and checkout rules. The completed publish-github-with-session-safe-updates change was archived on 2026-10-29 after its three delta specs were synchronized, preserving newer attachment-health and direct-ingress requirements; its archived evidence remains input to this release gate, not proof that the gate passed.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`, `restore-claude-badge-identity`, `fix-vscode-window-activation`, `confirm-claude-human-permission-waits`, `suppress-voice-during-microphone-use`, `add-idle-attention-reminders`, `verify-signed-releases-and-update-policy`.

**Primary source boundaries:** New packaged acceptance fixtures/scripts; scripts/update-acceptance.ps1 and release checks; docs/handoffs/next-release-plan.md, compatibility and release evidence; older OpenSpec reconciliation and version agreement.

## Goals / Non-Goals

**Goals:** Produce a single reviewable installed candidate and an evidence ledger proving every required report item, including full matched Claude/Codex feature conformance and mixed-provider runs.

**Non-Goals:** Do not fabricate authenticated captures, alter user production state/settings, play physical audio, publish prematurely or mark unavailable RDP/provider checks passed. Remote notification delivery is outside the retained scope.

## Decisions

### 1. One installed candidate and real baseline

Extend the packaged acceptance project/scripts, retaining the actual 0.1.8 release archive as baseline rather than compiling current code with a fixture version and calling it historical compatibility. Install candidate and baseline under isolated roots, use distinct --data-dir and fixture homes, and exercise real setup/command/launcher/ingress/VSIX paths. Candidate version is chosen once after checking unused tags; 0.1.9 is provisional, not reserved by this plan. Application, extension, package, signature descriptor and probes must agree.

### 2. Representative field fixture

Create matched five-conversation fixtures for each provider and a mixed-provider run launching in one main checkout, with ticket worktrees, explicit/custom/work-derived names, historical successful calls and current inspections of other changes. Include delayed/lost pre hooks, post-only work, null-process capture, tray stop/restart, unverified attachment timeout and branch switches. Require independent names/palettes/worktree context and preserved attention/navigation after upgrade/rollback. An actual provider hook capture validates fixture schema; fake agents must not be described as authenticated provider acceptance.

### 3. Cross-feature evidence

Benchmark the full installed provider-shell hook command under representative load and verify durable delivery totals. Verify no per-refresh process/Git churn on stable titles; no leaked .tmp files; foreground activation of background/minimized owning VS Code window with duplicate terminals; real human versus auto approval; microphone/playback state transitions; idle caps/restart; signature refusal and managed discovery policy. Keep physical audio silent throughout automated acceptance.

### 4. Coverage ledger and remaining live checks

Maintain a row for every field finding 1-13, original VS Code activation and badge rules: implementing change, test/fixture, run/command, result, evidence location, outstanding live check. Record the remote portion of finding 12 as cancelled by the user's 2026-10-29 decision, with no remaining delivery or endpoint gate; retain its local reminder acceptance. The user has already reported the failures and prior confirmations; do not demand an entire old acceptance matrix again. Record targeted Windows 11/RDP/PowerShell/VS Code and both provider versions and new redacted schema captures. Real call/foreground acceptance happens only through explicit user-controlled checks; missing signing prerequisites or required platform evidence remains a named gate.

### 5. Reconcile plans and release once

On 2026-10-29 the user approved archiving the shipped session-title and badge-identity changes with three live checks still unverified. Their original checkboxes remain unchecked in the archives; tasks 6.1-6.3 here now own the hook captures, two-session desktop trial and five-conversation trial. Task 6.4 retains ANALYSIS-300 as an unfinished fork parity gap and tracks related unobserved capability evidence. Older title/runtime deltas are synchronized before newer role-free identity and checkout rules. Archive and transfer do not establish release readiness; new live evidence remains user-controlled and separate from fixtures.

Before spec sync, reconcile shipped 0.1.8 change deltas with current main specs, preserve old unchecked evidence gaps, then sync/archive the new changes in the handoff order so older deltas cannot overwrite new semantics. Reuse already-shipped safe runtime/footer transports while extending both clients to the shared policy. Complete feature tasks, strict validation, formatting/build/unit/Node and packaged acceptance, then prepare release notes/trust assets and a concrete publication handoff. Publication is a separate explicitly authorized action and tags/assets remain immutable.

## Risks / Trade-offs

- [A fixture passes but installed behavior fails] -> Require real packaged command/bridge evidence and targeted field acceptance with honest source labels.
- [Old spec archive overwrites a new requirement] -> Reconcile and sync older deltas first; check composed requirements after each newer sync.
- [External signing key/platform unavailable] -> Finish independent fixture work and state the exact remaining prerequisite rather than omitting required scope.

## Migration Plan

Run this change last after the eight retained feature changes. It owns integrated evidence, version coordination and candidate packaging, not duplicate implementations. Preserve 0.1.8 install/state/runtime compatibility with actual baseline probes. A failed integrated gate keeps the candidate unpublished and records the cause.

## Verification and fresh-context notes

Run the repository formatting verification, Release build, complete .NET tests, Node bridge tests, strict OpenSpec validation and scripts/update-acceptance.ps1, plus the new field acceptance harness. Use current docs/development.md and docs/releases.md commands and current CLI help for new options. Retain results/latency distributions and the report coverage ledger under a documented artifacts path; source-controlled docs summarize evidence without committing private data or secrets.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Release gate for all field report items 1-13, badge parity, original VS Code report and confirmed release scope.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
