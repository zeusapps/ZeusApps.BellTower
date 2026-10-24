# Design

## Context

Baseline fields are ManualName, Change, Role, Palette and selection receipts; Name resolves manual/change/folder. Claude's statusline reads the provider cwd/current_dir, while title/overview use SessionState.Directory. No runtime dependency on CLAUDE_BADGE_FILE, transcript title reader, work-touch resolver or color controls exists. The field sessions all launch in C:/Code/D360; ticket checkouts live under C:/Code/D360-worktrees/DEP-XXXX. Therefore main-checkout Git output can be correct for the launch directory while being wrong for the work the badge should identify. The older follow-openspec-session-titles change shipped most implementation but retains unverified tasks 1.1/5.3 and a policy that treats read-only CLI status as selection.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`.

**Primary source boundaries:** Core/SessionIdentity, SessionPresentation, IdentityText and Palette; Providers selection/status-line readers; a Claude transcript adapter; Sessions/SessionCatalog; tray editors and speech formatting; IO/GitReader, IdentityStore; legacy import and metadata tests.

## Goals / Non-Goals

**Goals:** Recover the expected Claude identity in a fresh install and resumed conversation, keep all presentation surfaces consistent and deliver shared policy in both clients while preserving safe native ownership.

**Non-Goals:** Do not reinstall the old mod, execute transcript commands, infer work from arbitrary mentions/proposal prose, or use a ticket/check-out path as process ownership.

## Decisions

### 1. Explicit provenance and precedence

Use one resolver/model for both clients: Bell Tower full title > verified explicit native title > automatic subject. Bell Tower no longer uses a session role anywhere (user decision 2026-10-24: names and speech, both clients); legacy role inputs are accepted and ignored for script compatibility, and saved role values are preserved on disk but unused. Subject is pinned change > active work > unique branch change > ticket > branch > folder. Distinguish generated and last-Bell-Tower-written native names from actual user edits to avoid feedback. Codex's authenticated name reader/ownership records and Claude's verified custom-title adapter provide evidence to the common policy. Preserve native user names and explicit synchronization ownership while extending both clients. Full titles are not prefixed again; shared speech normalization runs once.

### 2. Work evidence, not inspection

Use one classifier over adapter-normalized successful edits/patches, workflow/skill intent, checkout observations and operation/root success evidence. Claude Write/Edit/NotebookEdit/Skill and Codex supported patch/edit/workflow forms must produce equivalent evidence, verified against their real contracts. Eligible non-archive work and explicit user association select work. Reads/searches/status/instructions cannot establish active work by themselves or replace it. A classified explain workflow may provide weak initial context without stealing work. Handle bounded literal shell wrappers, PowerShell, pipes/redirection only with success evidence; never execute recorded commands. Verify main/subagent identity for each client.

### 3. History recovery with bounded work

Feed one incremental recovery policy through provider history adapters. Claude uses verified custom-title JSONL and call/result pairs at transcript_path with newest-first chunks, per-tick budgets and rotation/partial-line handling. Codex must use its supported authenticated thread/read history/resume data or a supported bounded read-only adapter after verifying exact fields and title provenance; never write native databases/transcripts. Continue older chunks instead of discarding useful history after one tail read. Match first-install/resume/fork/clear outcomes for both clients. Missing captures or an unimplemented adapter are unfinished tasks, not a hard restriction.

### 4. Working checkout is a separate association

Use one launchDirectory/planningRoot/workingCheckout model and resolver for both clients. Priority is user checkout > verified provider worktree/current-checkout evidence > unique work mapping > honest launch fallback. Cache bounded canonicalized Git worktree enumeration and retain separate process ownership. Ambiguity requires attach/clear controls. Branch changes invalidate only branch-bound automatic evidence; pins/native titles/palette survive. An external planning store is never a checkout.

### 5. One shared presentation record

Publish one snapshot of title, speech subject, palette, branch/ticket/worktree, provenance and version for all product surfaces. Claude can render bundled ANSI through the fast helper; Codex uses supported native thread-name/branch/footer transport. Verify native title read/write in both: reuse working Codex transport, implement a supported Claude equivalent or record a precise hard restriction with app-owned presentation fallback. Never inject /rename or write provider databases/transcripts. A proven inability to style a native Codex footer affects that surface, not identity, checkout/color/reset controls elsewhere. Use common limits/normalization except verified native constraints. Stale snapshots never borrow another conversation's identity.

### 6. Palette controls and legacy import

Expose the same title/change/checkout/color/reset controls to both integrations through shared UI/CLI. Allocate readable mixed-provider palettes under the identity lock and retain resume pins. Reset Bell Tower pins without erasing native explicit user titles or attention. The legacy OSC rgb/hex importer is one adapter into shared identity and introduces no mod dependency for either client.

### Native-title writing deferred (user decision, 2026-10-24)

The user decided that Bell Tower writing its name into provider-native session titles, for both Claude Code and Codex, deserves its own change and research: `sync-native-session-titles`. That change supersedes the "Supported Claude SDK host" decision below for this change: no bundled Node runtime, SDK helper or Claude native-title write ships here. This change still reads verified explicit native titles for both clients (Claude `custom-title` transcript records; the Codex thread-name service), and generated or Bell Tower-managed titles never count as user overrides. Existing shipped Codex native-name synchronization and ownership safeguards are preserved unchanged, so there is no Codex regression; extending or redesigning native writes for either client belongs to the follow-up change. Claude's native `/resume` title stays a recorded unfinished parity item, not a provider restriction. Bell Tower's own surfaces (status line, managed tab, chips, speech) present the shared identity for both clients.

### Supported Claude SDK host (implementation decision, 2026-10-23; superseded for this change on 2026-10-24, see above)

Use the official Claude Agent SDK API for supported native session mutations; do not reproduce its transcript writer in Bell Tower. An isolated probe of SDK 0.3.309 against a copied disposable Claude 2.1.306 transcript verified `renameSession`, `getSessionInfo` and `getSessionMessages`, including an SDK-generated `custom-title` record and preservation of existing bytes. This establishes the tested persisted-title operation, not visible refresh in an existing interactive client.

Package a pinned Node runtime and pinned official SDK helper with the standalone Windows payload so ordinary installation does not depend on separately installed developer tools. Include dependency lockfiles, relevant licenses and helper/runtime files in the normal verified package inventory; do not download executable dependencies during tray or hook operation. Use a bounded isolated helper with structured arguments/input, exact provider/session/root targeting and shared native-name ownership/conflict handling. Missing helper or unsupported provider mode is an explicit unavailable diagnostic, not proof of a provider restriction. Native live-refresh behavior requires a separate version/mode-specific probe and an honest app-owned presentation fallback where a hard limitation is established. This implements the already required provider adapter and does not authorize production session mutation during acceptance.

### 7. Resolve the older plan without regressing shipped Codex

Reconcile older follow-openspec-session-titles artifacts first. Reuse working runtime/name transport, ownership safeguards and real evidence while replacing divergent naming/inspection defaults with the shared user policy for both clients. Existing support is a foundation to extend, not a frozen product. Sync older accepted deltas before new requirements so archive order cannot restore divergence.

## Risks / Trade-offs

- [Weak branch or skill evidence steals another session's name] -> Preserve evidence tiers and ambiguity, scope by provider/conversation, and require successful work or explicit association.
- [Transcript is huge or contains private data] -> Bound per-tick reads, continue incrementally and persist only derived metadata.
- [Automatic checkout changes navigation ownership] -> Never derive attachment identity from working checkout or display title.

## Migration Plan

Read legacy identities with their existing overrides/palette intact; add provenance fields compatibly. Existing Change values remain explicit/legacy associations until stronger verified evidence or user action clarifies them; do not erase all saved changes. Recover pre-install conversations in the background. Import old badge files only on explicit provider/session selection. New controls have preview/clear semantics and tests preserve attention. No production transcript or settings mutation is needed for acceptance.

## Verification and fresh-context notes

Use redacted fixtures for exact custom-title/main-agent schemas, all reported shell shapes, edits/skills, archive exclusion, successful-post-only ingestion, weak inspection, conflicting tickets, branch switches, external stores, fork/clear/resume and two concurrent sessions. Create isolated real linked worktrees to verify explicit/unique mapping, missing checkout and detached HEAD. Assert badge/chip/tab/speech identity agreement, four-word speech, pin/reset/resume/color distinction, OSC import and matched provider outcomes and safe native ownership. Carry forward field report values and legacy rules, not their old temp-file watcher as a runtime dependency.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 7, 8 and 9; the complete session-badge behavior report and the confirmed inspection-does-not-rename decision.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
