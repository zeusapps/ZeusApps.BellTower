<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

# Analysis report: restore-claude-badge-identity / Analysis 001

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent launched by the Claude Code main-session orchestrator, auto permission mode; model `claude-opus-5-5`
- Date: 2026-10-24T02:03Z (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 001 (frozen target of Implementations 001–004)
- Task and requirement references: tasks 2.4, 2.5, 3.2, 3.4, 4.1, 4.2, 6.1–6.3 and the design "Supported Claude SDK host" decision in [tasks.md](../tasks.md), [design.md](../design.md); [session-identity delta](../specs/session-identity/spec.md) requirements "App-owned names and optional change association", "Provider conversation identity recovery", "Shared provider presentation controls", "Git branch and worktree context", "Provider parity"; intake items I-5, I-8, I-12 and the tray-loop cost question in [findings.md](findings.md)
- Scope and relevant neighboring paths: `src tests integrations scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md`; focused reading of the changes in `src/BellTower/{Core,IO,Providers,Sessions,Tray,Hosts,Diagnostics}`, `src/BellTower.Protocol`, `integrations/claude-native/**`, `scripts/stage-claude-native.ps1`, `scripts/package.ps1`; baseline neighbors `SessionIdentity`, `SessionCatalog.Rename`, `OpenSpecSelectionObserver` at `d307c34`; developer handoffs [implementation-002](../evidence/implementation-002.md), [-003](../evidence/implementation-003.md), [-004](../evidence/implementation-004.md) read as claims, not evidence
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src tests integrations scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md` printed:
  - HEAD `d307c34`, 95 manifest entries
  - tracked diff SHA-256 `4ae6a7eacb501fa1633d5fdf35c518e1a035d403c702d7533c914dc7ac9eb5a1`, 56 untracked files
  - `Summary: head=d307c34 baseline=d307c34 files=95 diff=4ae6a7eacb50 untracked=56/550306f8303f` (matches the assignment)
- Before/after identity: after the analysis the same command printed `Summary: head=d307c34 baseline=d307c34 files=95 diff=4ae6a7eacb50 untracked=56/550306f8303f`. It is identical, so the review target was not changed.
- Prior reports / rechecked findings: none (first analysis). The concurrent [qa-001.md](qa-001.md) was read only to avoid duplicates; each finding notes its relationship to QA findings.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture: shared policy vs adapters | Read every new Core type (`WorkPolicy`, `WorkClassifier`, `ShellGrammar`, `CheckoutPolicy`, `HistoryState`, `NativeNamePolicy`, `NativeNameSchedule`, `IdentityControls`) and its callers | Core types are pure: only `Path` string APIs, no disk, provider JSON or UI. One classifier feeds both live hooks and history; one checkout policy and one schedule serve both clients | Layering matches `docs/development.md`. Exceptions: ANALYSIS-003 (native-name sync flow written twice) and ANALYSIS-009 (provider transcript parsing in `Sessions`) |
| Duplication | Compared `ClaudeNameService`/`CodexNameService`, `HookIngestor`/`CaptureConsumer`, the three places that clear work evidence, and the capability statements | The same step sequence is written twice with diverging details; the same ingestion block appears in two ingress paths | ANALYSIS-003, ANALYSIS-006, ANALYSIS-010; the divergent clear sites are covered under ANALYSIS-001 |
| History walk (correctness-adjacent structure) | Read `IdentityHistoryReader.Satisfied`/`Merge`; ran an isolated probe with the built assembly | [probe results §1](../evidence/analysis-001-probe-results.txt): with a custom title present, a newest weak `explain-change` ends the backward walk and older strong work is never recovered | ANALYSIS-001 (measured) |
| Persisted-data compatibility, migration, rollback | Compared baseline `SessionIdentity`, `SessionCatalog.Rename` and observer writes with the new precedence | Baseline auto-selection wrote `Change`; the new code treats every `Change` as a permanent pin | ANALYSIS-002. Unknown fields are still preserved (`JsonExtensionData` on `SessionIdentity` at baseline and target) |
| Lock ordering | Traced every `DataLock` nesting (`identity`, `launch`, `native-name-<key>`) | Only two nestings exist: `identity`→`launch` (`LaunchPresentationStore.Prepare`) and `native-name`→`identity` (both name services, `NativeNameControl`). No inverse order. Helper and network calls hold only the `native-name` lock, never `identity` | No deadlock risk found; UI-thread lock waits are noted in ANALYSIS-005 |
| D-1 race (history pass vs native write) | Read `NativeNamePolicy.RecordIntent/Confirm/Ours` and the title handling in `Merge` | The pending name is recorded under the identity lock before the SDK/runtime write. `Merge` treats the last or pending name as Bell Tower's own, and applies only the newest title offset | Closed for both clients; generated-title provenance is QA-001 territory |
| Write amplification (I-5 recheck) | Source reading plus probes | History `Refresh` writes only when derived state changes or the tail grows ≥64 KiB; an idle tick costs 0.3 ms and writes nothing ([§3](../evidence/analysis-001-probe-results.txt)). The resolver writes only when the encoded identity changes. The presentation is rewritten only on change or on the 15 s heartbeat. Backward-walk checkpoints write once per tick until the walk ends (bounded) | I-5 resolved as observed; remaining read cost in ANALYSIS-004 |
| Palette allocation (I-8 recheck) | Source reading plus probe | Allocation runs under the `identity` lock for identity creation, random color, reset and launch preparation. Measured 54–77 ms per creation with 200 full identities ([§2](../evidence/analysis-001-probe-results.txt)) | I-8 atomicity resolved; cost and unbounded directory growth in ANALYSIS-005 |
| Native-name scheduling (I-12 recheck) | Read `NativeNameSchedule`, `NativeNamePump` and both services | One schedule per provider. The Claude helper starts only on a name change, an ownership request, the 5 min verification or a backoff expiry. The pump runs one pass at a time and handles sessions sequentially | I-12 resolved as designed; the remaining read spawn and refusal-code handling are in ANALYSIS-003 and ANALYSIS-011 |
| Hook ingestion hot path | Read `HookIngestor`, `CaptureConsumer`, `CaptureFactory` (ingress) | The NativeAOT ingress adds only pure grammar/classifier work, plus a directory walk for `Skill` events. The tray consumer and the legacy direct path add one extra `identity` lock + read per event, and write only when a transcript is bound or the cwd changes (no-op update 0.34 ms, write 10.7 ms, measured) | No material regression of `speed-up-claude-hook-ingestion`; simplification in ANALYSIS-006 |
| Status-line path | Read `StatusLineReader`, `GitHeadReader`, `IdentityPresentationStore` | Baseline started up to four `git` processes per render. The target uses the fresh snapshot or a process-free read of `HEAD` | Improvement; one Git format gap in ANALYSIS-007 (measured) |
| Git enumeration caching | Read `IdentityResolver`, `CheckoutCache`, `GitReader` | Worktree list and toplevel are cached 30 s (bounded to 128 entries). Branch context is cached 5 s in the tray's shared `CheckoutCache`, used by the resolver, title pump and native-name pump | Adequate; CPU cost of the repeated identity reads is in ANALYSIS-004 |
| History per-tick budget | Probe on a 13 MB tool-heavy synthetic transcript | 51 ticks, 16 ms average and 40 ms maximum per tick, including the checkpoint write ([§3](../evidence/analysis-001-probe-results.txt)) | Within budget; the serialize/parse round trip in `AddCall` is noted in ANALYSIS-009 |
| Packaging / scripts | Read `stage-claude-native.ps1`, the `package.ps1` diff, `helper.mjs` and the pin files | The verification chain (zip hash, exe hash, lockfile integrity, `npm ci --ignore-scripts`) is proportionate and readable | ANALYSIS-008 (version pin drift). Reading `ClaudeNameClient` confirms nothing is downloaded at tray or hook time |
| Unbounded persisted collections | Read the writers and the `IdentityStore.GetCore` validation | Writers cap `HistoryCalls` ≤64, `HistoryResults` ≤64, `SelectionReceipts` ≤64, `SelectionEvidence` ≤128. Read-side validation checks only `SelectionEvidence`. Files in `identities/` and the new `presentations/` are never pruned | Each file is bounded; directory growth noted in ANALYSIS-005 |
| Formatting, build, tests | Not run | QA 001 owns these checks on the same frozen target (see QA-007) | Not performed by analysis (proportionality) |

Client coverage: every probe and source path above applies to both clients unless stated otherwise. The measured case in ANALYSIS-001 is Claude, which today is the only provider with weak skill evidence and a history title; the stop condition itself is shared code. ANALYSIS-003, 006, 009 and 011 compare the two adapters directly.

## Findings

### ANALYSIS-001: Backward history walk stops on weak evidence, so older work is never recovered

- Severity: warning
- Related task / requirement: 2.4, 2.5; "Provider conversation identity recovery" (scenario "Unrelated recent read"); "Provider active work evidence" ("explain/read-only inspection of another change preserves an established association")
- Locations: `src/BellTower/Sessions/IdentityHistoryReader.cs:59`, `:82`, `:122-123` (`Satisfied`); `src/BellTower/Core/WorkPolicy.cs:47-52`; clear sites `src/BellTower/Core/HistoryState.cs:22-46` and `src/BellTower/Core/CheckoutPolicy.cs:57-78`
- Observation:
  - `Satisfied` ends recovery once `HistoryWorkOffset >= 0` (and, for Claude, `NativeTitleOffset >= 0`).
  - `WorkPolicy.Apply` sets `HistoryWorkOffset` for weak evidence too. So a weak `explain-change`/explore skill on change B in the newest chunk counts as "satisfied".
  - Measured with the built assembly ([probe results §1](../evidence/analysis-001-probe-results.txt)): a transcript with Write(change-a), about 600 KB of chatter, a custom title, then `explain-change change-b` finishes after one tick with `HistoryOffset=361838`, `ActiveWork=change-b`, `WorkWeak=True`. Without the title the walk continues and change-a wins.
  - The existing test `WeakHistoricalContextOnlyFillsAnEmptyAssociationRegardlessOfChunkOrder` covers only the no-title shape.
  - QA-004 has the same root: walk termination depends on fields that change as active work changes. Branch invalidation resets `HistoryWorkOffset`, which restarts the walk and re-applies older work.
  - The work-evidence fields are cleared in three places with different subsets: `ApplyBranch` keeps `SelectionRoot`, while `HistoryState.Reset` clears it but resets `HistoryWorkOffset` separately. This makes such interactions hard to reason about.
- Proposed improvement and benefit:
  - Give recovery its own state instead of inferring it from `ActiveWork`. For termination, use a persisted "strong history work found" marker, or simply `HistoryWorkOffset >= 0 && !WorkWeak`.
  - Write a history floor offset on branch invalidation (QA-004's recommendation).
  - Put the field set behind one `WorkPolicy.Clear(identity, scope)` used by all three sites.
  - Benefit: the recovery outcome no longer depends on whether a title or a weak record happened to be in the newest chunk, and QA-004 can be fixed in the same place.
- Behavior-preservation risks: continuing the walk after weak evidence reads more of long transcripts (still one bounded chunk per tick; measured 16 ms per tick on average). History must still never override newer live work, and the Claude title condition must stay.
- Evidence type: measured result (isolated probe) plus source observation
- Verification / measurement plan: add an `IdentityHistoryTests` case with a custom title in the newest chunk, the weak skill as the newest record, and strong work older than one chunk; assert change-a and `WorkWeak=false`. Rerun the QA-004 probe and the whole `IdentityHistoryTests`/`WorkPolicyTests` group.
- Relationship: new. Shares its root cause with QA-004 but is not a duplicate: QA-004 is invalidation re-selecting old work, this is weak evidence preventing recovery.

### ANALYSIS-002: Legacy automatic `Change` values become permanent pins after upgrade and after a rollback round trip

- Severity: warning (decision needed)
- Related task / requirement: 1.2, 2.1, 2.4; design "Migration Plan" ("Existing Change values remain explicit/legacy associations until stronger verified evidence or user action clarifies them"); "App-owned names and optional change association" ("Existing identity data … SHALL be migrated")
- Locations:
  - `src/BellTower/Core/SessionIdentity.cs:52` (`Subject => Change ?? ActiveWork ?? …`)
  - `src/BellTower/Providers/OpenSpecSelectionObserver.cs` (no longer writes `Change`)
  - the only places that clear it: `src/BellTower/Core/IdentityControls.cs:22` (reset) and `src/BellTower/Sessions/SessionCatalog.cs:50` (rename)
  - baseline `d307c34:src/BellTower/Providers/OpenSpecSelectionObserver.cs` (`identity.Change = selected.Change; identity.SelectionRoot = selected.Root; identity.SelectionEvent = selected.Id`) and `d307c34:src/BellTower/Sessions/SessionCatalog.cs` (`Rename` clears `SelectionRoot`)
- Observation:
  - At baseline, `Change` was written automatically by the selector driven by `status`/`instructions`. That is the superseded policy that produced the field-report misnames.
  - The target treats any saved `Change` as a user pin that outranks all work evidence. Nothing implements the "until stronger verified evidence … clarifies them" half of the migration plan.
  - So resumed conversations (the exact field scenario) keep a possibly wrong, inspection-derived subject until the user resets it.
  - A rollback round trip does the same: 0.1.8 keeps writing `Change` automatically, including from inspection, and the next roll-forward turns those values into pins.
  - Implementation 002 recorded this as a deliberate choice ("Legacy saved Change values now remain explicit pins"). So this is a mismatch between plan and implementation, not an oversight hidden in code.
  - The baseline data carries a way to tell them apart: automatic selections have `SelectionRoot` and `SelectionEvent` set, and baseline `Rename` cleared `SelectionRoot`.
- Proposed improvement and benefit:
  - Record where `Change` came from, for example a `ChangeSource` of `user`, `launch` or `legacy-selection`.
  - Classify existing records once: a `Change` with `SelectionRoot` and `SelectionEvent` set and no new-format marker becomes `legacy-selection`.
  - Treat `legacy-selection` as strong automatic work that newer verified work or an explicit association replaces, or move it into `ActiveWork`.
  - Alternatively, amend the design's migration plan and document that upgraded sessions need "Reset pins".
  - Benefit: upgraded sessions recover the intended identity without a user command, and a rollback cannot silently create pins.
- Behavior-preservation risks: a user who pinned a change through the old CLI after an automatic selection may have had `SelectionRoot` cleared by the rename path, so the rule is conservative. A launch `BELLTOWER_CHANGE` initial value has no `SelectionRoot` and stays a pin. Older binaries preserve the new field through `JsonExtensionData`.
- Evidence type: source observation (baseline versus target)
- Verification / measurement plan: build fixture identity JSON in the baseline shape (automatic selection, manual rename, launch change) and load it with the target. Assert that only the automatic one yields to newer work evidence, for both providers, and that a 0.1.8-written automatic `Change` behaves the same after roll-forward.
- Relationship: new

### ANALYSIS-003: Native-name synchronization flow is implemented twice and has already diverged

- Severity: suggestion
- Related task / requirement: 4.1, 4.4, 6.1–6.3; AGENTS.md "Avoid separate product flows or duplicated policy implementations"
- Locations: `src/BellTower/Sessions/ClaudeNameService.cs:16-79`; `src/BellTower/Sessions/CodexNameService.cs:13-93`
- Observation:
  - Both services repeat the same policy sequence: native-name lock → `Get` → enabled check → `schedule.Due` → availability → read current → `Conflict` → `Pause` → `RecordIntent` → write → `Confirm` → `Succeeded`/`Failed` + status. Only read, write, availability and the desired name differ.
  - The copies have diverged:
    - Claude `SetUnavailable` writes the status without checking `NativeSyncEnabled`; Codex `SetStatus` checks it. So a session the user paused can show `unavailable: …` for Claude only.
    - Codex logs `native-name-unavailable`; Claude logs nothing.
    - The two use different exception filters.
  - The Claude helper is also started on every verification just to read a value that, per QA-001, cannot tell generated titles from user titles. The history reader already holds the verified `custom-title` for the same transcript.
- Proposed improvement and benefit:
  - One shared `NativeNameSync.Run(session, schedule, adapter)` in `Sessions`, with a small adapter interface: `Unavailable()`, `Desired(identity, git)`, `Read()`, `Write(expected, desired)`. There are two real implementations, so an interface fits the `docs/development.md` rule.
  - Policy fixes such as QA-001 and QA-003 then land once for both clients.
  - If QA-001 is fixed by using the history reader's verified title as Claude's "current" value, the verification spawn disappears and the helper starts only for writes.
- Behavior-preservation risks: the Codex desired name includes the worktree suffix and depends on Git context, while Claude's is plain. Codex must keep its rule that network awaits never run across a `DataLock`. Existing `NativeNameTests` encode the current per-provider statuses.
- Evidence type: source observation
- Verification / measurement plan: the existing `NativeNameTests`, `ClaudeNameClientTests` and `MetadataPreservationTests` for both providers, plus one test that a paused session stays paused when the provider becomes unavailable (both clients).
- Relationship: related to QA-001 and QA-003 (where their fixes would land), not a duplicate

### ANALYSIS-004: The tray refresh pass and the chip timer re-read and re-encode the same identity many times; history checkpoints inflate every hot read

- Severity: suggestion
- Related task / requirement: 2.5, 3.4; follow-up to intake I-5 and the assignment's tray-loop cost question
- Locations:
  - `src/BellTower/Tray/TrayApplication.cs:264-270`: history → resolver → `identities.Get` → presentation, sequential per session every 2 s
  - `src/BellTower/Tray/TrayApplication.cs:205`: `views.Chips` reads each identity every 250 ms
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:40`, `:96-118`: `Get`, then `Update` with up to three `Json.Encode` calls
  - `src/BellTower/Sessions/IdentityResolver.cs:20`, `:43-53`: `Get`, then `Update` with two encodes, plus `EnumerateDirectories` per pass
  - `src/BellTower/Hosts/TerminalTitlePump.cs:72`; `src/BellTower/Sessions/NativeNamePump.cs:59` plus each service's own `Get`
  - `src/BellTower/Core/SessionIdentity.cs:25-35`: `HistoryOffset`/`Length`/`Fingerprint`/`HistoryResults`/`HistoryCalls` stored in the main identity
- Observation:
  - Each live session's identity file is opened, locked and fully deserialized about six times per 2 s pass: history `Get` + `Update`, resolver `Get` + `Update`, presentation `Get`, title pump `Get`, native-name pump/service `Get`.
  - The 250 ms chip timer adds about eight more reads per 2 s, on the UI thread.
  - All of these share one global `identity` mutex across all sessions.
  - The derived history checkpoint lives in the same document ([probe results §2](../evidence/analysis-001-probe-results.txt)):
    - At the writer limits an identity is about 80 KB and takes about 1 ms to read; a plain one is 1.3 KB.
    - Every checkpoint is a write-through save (about 10.7 ms measured) that also rewrites the user-visible identity and its `.bak`.
  - Change detection serializes the whole identity two or three times per `Update`.
  - About 24 KB of the worst case is new in this change (64 calls + 64 results); `SelectionEvidence` (128 entries) predates it.
- Proposed improvement and benefit:
  - (a) Store history cursors and receipts in a separate derived checkpoint file owned by `IdentityHistoryReader` (for example `history/<hash>.json`), and take the `identity` lock only when the derived title or work actually changes.
    - The identity then stays small for hooks, the status line and chips.
    - Most fsyncs move off the identity file.
    - A corrupt checkpoint can be discarded without touching pins.
  - (b) Pass one identity snapshot through the per-session pass instead of re-reading it, and have the policies return explicit "changed" flags instead of comparing whole-document encodes.
  - (c) Optionally cache the change-folder enumeration alongside the 30 s worktree cache.
- Behavior-preservation risks: checkpoint and identity must stay consistent when a transcript is rebound (`HistoryState.Bind` currently resets both atomically under one lock), so the checkpoint needs a binding key such as transcript path plus fingerprint. An older binary ignores the separate file harmlessly. Moving fields out of `SessionIdentity` changes the JSON the new version writes; older readers keep existing fields through extension data.
- Evidence type: measured result for single-operation costs; the aggregate per-pass cost is a performance hypothesis (not measured with a running tray)
- Verification / measurement plan: a disposable-data micro-benchmark of one `RefreshOverview`-equivalent pass for 10 sessions with full histories, before and after, counting `DataLock` acquisitions and elapsed time; run `IdentityHistoryTests`, `MetadataPreservationTests` and `PresentationAgreementTests`.
- Relationship: new

### ANALYSIS-005: Palette allocation deserializes up to 64 whole identities under the global lock and scans an ever-growing directory

- Severity: suggestion
- Related task / requirement: 4.2; follow-up to intake I-8
- Locations:
  - `src/BellTower/IO/IdentityStore.cs:88-109` (`AllocatePalette`: `EnumerateFiles(...).OrderByDescending(LastWriteTimeUtc).Take(64)`, full `SessionIdentity` decode per file)
  - Callers: `IdentityStore.GetCore` (first hook/capture event without a launch palette), `SetColor`, `LaunchPresentationStore.Prepare`
  - UI-thread caller: `overview.ConfigureIdentity` in `src/BellTower/Tray/TrayApplication.cs` → `SessionCatalog.Configure` (`src/BellTower/Sessions/SessionCatalog.cs:74-91`, which also runs `git rev-parse` synchronously)
  - The new `presentations/` directory (`src/BellTower/IO/IdentityPresentationStore.cs`) and `identities/` are never pruned
- Observation:
  - I-8's atomicity is fixed: allocation runs under the `identity` lock, the order is identity → launch, and there is no inverse nesting.
  - The price is heavy work inside that lock. Every identity file ever written is enumerated and sorted, and the 64 newest are fully deserialized (history lists included) just to read `Palette.Accent`.
  - Measured 54–77 ms per identity creation with 200 full identities ([§2](../evidence/analysis-001-probe-results.txt)). During that time every hook ingestion, status line and chip read waits on the same mutex.
  - "Random color" and "Reset pins" run this on the WinForms UI thread, together with a `git` process and up to 5 s of mutex wait.
- Proposed improvement and benefit:
  - Cut the cost of finding the accents in use, with any one of:
    - decode a `{ Key, Palette }` projection (System.Text.Json skips the rest without building objects);
    - keep a small palette index updated in `SaveCore`;
    - use fresh `presentations/` snapshots (live sessions by construction) plus recent launches as the "taken" set.
  - Prune presentation snapshots older than a day when the tray starts.
  - Run `Configure` on a worker thread, as `SetNativeSync` already does.
  - Benefit: a shorter lock hold on the hook path and a responsive overview, with the same allocation behavior.
- Behavior-preservation risks: the "taken" set must still include just-prepared launch palettes (the consume-then-create window closed in Implementation 003). The barrier test in `PaletteAllocationTests` must still fail when the lock is removed.
- Evidence type: measured result (allocation cost); the UI-freeze impact is a performance hypothesis
- Verification / measurement plan: rerun the size probe ([scripts](../evidence/analysis-001-probe-scripts.ps1.txt), `probe-size.ps1`) before and after; run `PaletteAllocationTests` and `OverviewControlTests`.
- Relationship: new

### ANALYSIS-006: Hook and capture ingestion duplicate the identity block and take the identity lock twice per event

- Severity: suggestion
- Related task / requirement: 2.5, 3.2; predecessor `speed-up-claude-hook-ingestion`
- Locations: `src/BellTower/Providers/HookIngestor.cs:39-51`; `src/BellTower/Providers/CaptureConsumer.cs:47-55`
- Observation:
  - Both paths run `IdentityStore.Get(initial values)`, then a separate `IdentityStore.Update` (bind transcript + `CheckoutEvidence.Observe`), then the selection observer's own `Update`. That is three lock/read cycles per event where two would do.
  - The two copies already differ in the cwd they pass: the direct path uses the raw hook `cwd` (null when absent), the capture path uses `observation.Directory`, which falls back to the attachment directory.
  - `HistoryState.Bind` also runs for subagent events, while `CheckoutEvidence.Observe` only accepts main-thread events.
  - The cost of the extra cycle is small (no-op update 0.34 ms measured; the ~10 ms write happens only on transcript bind or cwd change), so this is not a latency regression of the predecessor. The NativeAOT ingress gains only pure grammar/classification work.
- Proposed improvement and benefit:
  - Add one shared `IdentityIngest.Apply(paths, observation, initial…, transcriptPath, cwd)`.
    - It creates the identity with its initial values and applies bind and cwd in the same `Update`.
    - Both paths use it, with the same cwd semantics.
  - Accept `Bind` only for main-thread events, like the cwd evidence, unless a primary contract shows subagent hooks always carry the main transcript path.
- Behavior-preservation risks: `GetCore` currently applies initial values only when it first creates the identity; the merged call must keep that. The `selection-unavailable` failure boundary in `HookIngestor` must still cover the new work.
- Evidence type: source observation; latency figures measured on single operations
- Verification / measurement plan: `CaptureTests`, the `WorkEvidenceTests` hook-ingestion cases with and without `agent_id`, and `CheckoutResolutionTests` for both clients.
- Relationship: new

### ANALYSIS-007: Process-free Git fallback reports `.invalid` as the branch in reftable repositories

- Severity: suggestion
- Related task / requirement: 3.4; the fallback path from intake I-9; "Git branch and worktree context" ("unavailable context SHALL be omitted")
- Locations: `src/BellTower/IO/GitHeadReader.cs:46-55` (`Head`)
- Observation:
  - Git's reftable backend keeps a compatibility `HEAD` file containing `ref: refs/heads/.invalid`.
  - Measured with git 2.51.0 ([probe results §4](../evidence/analysis-001-probe-results.txt)): `git symbolic-ref` reports `feature/DEP-1`, while `GitHeadReader.Read` returns branch `.invalid`.
  - The status line uses this fallback whenever the tray snapshot is missing or stale, so it shows a wrong branch instead of omitting it.
  - Reftable is opt-in today, but Git has announced it as the planned default for Git 3.0.
- Proposed improvement and benefit:
  - Treat `refs/heads/.invalid` (or a `reftable/` directory in the common git dir) as unknown and omit the branch.
  - Optionally fall back to the cached `git` read in the tray only, never in the status-line process.
  - Benefit: the fallback stays honest, as the spec requires.
- Behavior-preservation risks: none for files-backend repositories; the worktree label logic is unaffected.
- Evidence type: measured result
- Verification / measurement plan: extend `StatusLineFallbackTests` with a `git init --ref-format=reftable` repository (skip when the installed Git lacks reftable).
- Relationship: new

### ANALYSIS-008: The Claude SDK version is pinned in six places but cross-checked in three; a partial bump fails only at run time

- Severity: suggestion
- Related task / requirement: design "Supported Claude SDK host"; 5.2, 6.3
- Locations:
  - Checked by `scripts/stage-claude-native.ps1:14-17`: `integrations/claude-native/package.json`, `package-lock.json`, `runtime.json`
  - Not cross-checked: `integrations/claude-native/helper.mjs:16` (`SDK_VERSION`); `src/BellTower/Providers/ClaudeNameClient.cs:16` (`SdkVersion`, enforced by `Unavailable()`); `src/BellTower/Diagnostics/CapabilityMatrix.cs:18`
  - Test `tests/BellTower.Tests/Providers/ClaudeNameClientTests.cs:50` asserts the C# constant against the literal `"0.3.309"`
  - Manual procedure in `docs/development.md` ("Updating the SDK or Node means changing …")
- Observation:
  - If a maintainer bumps the three files the staging script checks but not `ClaudeNameClient.SdkVersion`, staging and every test still pass, because the test compares the constant with a literal.
  - The installed payload then reports `unexpected SDK version`, and Claude native names become unavailable for every user.
  - `helper.mjs` reports its own version constant, which nothing checks.
- Proposed improvement and benefit: either derive the expected version at run time from the shipped `claude-native/runtime.json` (already in the release inventory), or add a test that reads `integrations/claude-native/runtime.json`, `package.json` and `helper.mjs` and asserts they equal `ClaudeNameClient.SdkVersion`. That gives one source of truth and turns the documented manual procedure into a failing check.
- Behavior-preservation risks: reading `runtime.json` at run time adds a file to the availability check; a missing or corrupt file must stay an explicit unavailable state.
- Evidence type: source observation
- Verification / measurement plan: a mutation check that edits one pin in a temp copy and confirms the new test fails; `ClaudeNameClientTests`.
- Relationship: new

### ANALYSIS-009: Provider transcript parsing sits in `Sessions` and reuses the hook classifier through a synthetic JSON round trip

- Severity: suggestion
- Related task / requirement: 2.2, 2.5; `docs/development.md` responsibilities (`Providers`: bounded input and normalization); the project-facts parity rule (provider-specific behavior belongs in adapters)
- Locations: `src/BellTower/Sessions/IdentityHistoryReader.cs`:
  - `:181-183` (`Relevant`), `:262-335` (`ReadClaude`/`ReadCodex`)
  - `:338-349` (`AddCall` serializes an anonymous hook envelope and parses it back)
  - `:318`, `:330` (Codex arguments serialized and parsed again before `AddCall`)
  - `:22-29` (capability text)
- Observation:
  - The cursor/merge policy is provider-neutral, but the same class also holds both providers' native record formats, regexes and success headers.
  - Each candidate tool call is turned into JSON text and parsed back (twice for Codex), so that `OpenSpecSelectionReader.Read` can see a fake PostToolUse envelope.
  - This works and is bounded (16 ms average per 256 KiB tick measured, [§3](../evidence/analysis-001-probe-results.txt)). But it ties history recovery to hook-envelope field names, and it puts provider adapters outside `Providers`.
- Proposed improvement and benefit:
  - Move `ReadClaude`, `ReadCodex`, `Relevant` and their regexes into `Providers` history adapters that emit (call, result) records.
  - Expose a typed `OpenSpecSelectionReader.Classify(tool, JsonElement toolInput, cwd, mainThread)` that both the hook path and history recovery call. That removes the round trip and leaves one classifier entry point.
  - Benefit: clearer layering, and a new provider format touches only an adapter.
- Behavior-preservation risks: the synthetic envelope currently forces `hook_event_name = PostToolUse` and an empty `tool_response`, so with the typed entry point history success must still be decided only by the paired result. These files are also compiled into the NativeAOT ingress through `BellTower.Protocol.csproj`, which must keep building.
- Evidence type: source observation; the allocation cost is a performance hypothesis (not measured in isolation)
- Verification / measurement plan: `IdentityHistoryTests`, `WorkEvidenceTests`, `ShellGrammarTests` and the ingress capture round trip; rerun `probe-tick.ps1` for comparison.
- Relationship: new

### ANALYSIS-010: Capability facts are maintained in three unsynchronized copies

- Severity: suggestion
- Related task / requirement: 6.2; AGENTS.md "Keep the capability matrix and diagnostics honest"
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs:24-68`; `src/BellTower/Sessions/IdentityHistoryReader.cs:22-29` (`Capabilities`, printed by `DiagnosticsReporter` as `History`); the capability table in `docs/compatibility.md`
- Observation:
  - The same version-scoped statements appear in three places: the structured matrix, a free-text string in the history reader, and the docs table. Examples: Codex history title absent at 0.164.0, shell tools, failed-patch PostToolUse, Claude fork.
  - `CapabilityMatrixTests` validate only the structured rows. The doctor `History` line and the docs can drift from the matrix, for example when QA-005 changes the Codex skill row.
- Proposed improvement and benefit: build the doctor `History` field from the relevant `CapabilityMatrix` rows, and either add a test that every matrix surface and status appears in `docs/compatibility.md` or generate that table section. That keeps one honest record.
- Behavior-preservation risks: `DiagnosticsReporter` tests may depend on the doctor JSON field shape, so the field must stay even if its text is derived.
- Evidence type: source observation
- Verification / measurement plan: `CapabilityMatrixTests` plus the new docs-consistency assertion.
- Relationship: related to QA-005 (row content), not a duplicate

### ANALYSIS-011: The Claude adapter accepts titles longer than the helper limit and collapses helper refusal codes, which causes a permanent retry loop

- Severity: suggestion
- Related task / requirement: 3.4, 6.3; design decision 5 ("Use common limits/normalization except verified native constraints")
- Locations:
  - `src/BellTower/Core/SessionIdentity.cs:53` (`Role + ": " + Subject`, each part up to 100 characters via `IdentityText.Clean`)
  - `integrations/claude-native/helper.mjs:18`, `:52-53` (`TITLE_LIMIT = 100` → `invalid-title`)
  - `src/BellTower/Providers/ClaudeNameClient.cs:67` (every code other than conflict becomes `claude-native-unavailable`)
  - the generic catch in `src/BellTower/Sessions/ClaudeNameService.cs` → `unavailable: supported Claude metadata API` + backoff
- Observation:
  - A role-prefixed name can reach about 200 characters, and the helper refuses anything over 100 with `invalid-title`.
  - The client maps every refusal except conflict to one generic code, and the service records "supported Claude metadata API" as unavailable.
  - The schedule then retries every 60 s forever, starting Node each time for a request that can never succeed.
  - The helper's stable codes (`invalid-title`, `session-unavailable`, `sdk-unavailable`, `input-limit`) are lost before they reach diagnostics.
  - Codex, by contrast, shapes its name in its own adapter (`SessionPresentation.CodexName`).
- Proposed improvement and benefit:
  - Normalize the desired Claude name to the native limit in the adapter, as Codex shapes its name.
  - Carry the helper's code into `NativeSyncStatus`.
  - Treat deterministic refusals (`invalid-title`, `invalid-target`) as not worth retrying until the desired name changes.
  - Benefit: no futile process spawns, and honest diagnostics.
- Behavior-preservation risks: truncation must happen before `RecordIntent`, so the pending and last names equal the value actually written; otherwise the D-1 ownership checks break.
- Evidence type: source observation (lengths derived from code; the helper was not run)
- Verification / measurement plan: `NativeNameTests` with a 150-character role + subject (a fake client asserts the received length), and a fake client that returns `invalid-title` (assert one attempt per desired name).
- Relationship: new

## Re-verification

There are no earlier analysis findings. The intake items were rechecked as directed, by source reading plus the probes cited above. Status ownership stays with the orchestrator.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| I-5 | `diff=4ae6a7eacb50 untracked=56/550306f8303f` | `IdentityHistoryReader.Refresh` returns early and writes only on derived change or a ≥64 KiB checkpoint (idle tick 0.3 ms with no write, measured); `IdentityResolver` writes only on encoded change; `IdentityPresentationStore.Save` skips identical content within the 15 s heartbeat | verified (remaining read cost: ANALYSIS-004) |
| I-8 | same | `AllocatePalette` is called only under the `identity` lock (`GetCore`, `SetColor`, and `Prepare` with identity→launch order); no inverse nesting found | verified for atomicity (cost: ANALYSIS-005) |
| I-12 | same | `NativeNameSchedule` makes Claude helper calls change-driven (5 min verification, 2–60 s backoff); the pump runs one pass at a time, sequentially | verified as designed (refusal loop: ANALYSIS-011; read spawn: ANALYSIS-003/QA-001) |
| Tray loop cost | same | Per-session pass: history refresh (bounded, measured), cached worktree/toplevel/branch reads, about 6 identity lock/read cycles, presentation write on change or heartbeat | not a blocker; improvement in ANALYSIS-004 (aggregate cost is a hypothesis) |

## Assessment and limitations

The structure mostly follows the shared-policy/thin-adapter rule. Work classification, checkout choice, the history merge, native-name ownership and scheduling are each implemented once, in pure Core types, for both clients. Lock ordering is consistent and the D-1 race is closed. I-5, I-8 and I-12 are resolved as observed. Hook ingestion latency is essentially unchanged, and the status line is faster because it no longer starts `git` processes.

The two warnings need attention before release:
- ANALYSIS-001 is a measured recovery defect in shared code, in the same area as QA-004.
- ANALYSIS-002 is a migration/rollback mismatch with the design. The orchestrator must decide whether to implement the planned migration or amend the design.

ANALYSIS-003 is the main parity-structure suggestion, and it is also where the QA-001 and QA-003 fixes would land once for both clients. The other suggestions are bounded performance, robustness and maintainability improvements.

Not performed:
- No measurement with a running tray, real Node helper spawns, installed providers or a NativeAOT ingress build.
- No build, test or format runs; QA 001 owns those.
- The aggregate tray-pass cost and the UI-freeze effects remain hypotheses.

The probes used only a scratchpad copy of the built assembly, disposable data directories, synthetic transcripts and a synthetic Git repository, all deleted afterwards. The review target identity was the same before and after.

Writes made: [analysis-001-probe-results.txt](../evidence/analysis-001-probe-results.txt) and [analysis-001-probe-scripts.ps1.txt](../evidence/analysis-001-probe-scripts.ps1.txt). The client refused the write of this report (`reports/analysis-001.md`), so the text is returned inline for the orchestrator to save. Nothing else in the repository was written; scratch files are outside the repository.
