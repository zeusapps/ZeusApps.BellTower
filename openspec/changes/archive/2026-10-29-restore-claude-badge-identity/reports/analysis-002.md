<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

# Analysis report: restore-claude-badge-identity / Analysis 002 (recheck 1)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent launched by the Claude Code main-session orchestrator, auto permission mode; model `claude-opus-5-5`
- Date: 2026-10-24T08:45Z (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 002 (recheck 1 of the fix round, Implementation 005)
- Task and requirement references: tasks 1.2, 2.1, 2.4, 2.5, 3.3, 4.1, 6.1–6.3 in [tasks.md](../tasks.md); [design.md](../design.md) "Migration Plan", decision 4 (branch invalidation) and "Native-title writing deferred (user decision, 2026-10-24)"; [session-identity delta](../specs/session-identity/spec.md) "Provider conversation identity recovery", "Git branch and worktree context", "App-owned names and optional change association", "Provider parity"; QA-002/QA-003/QA-004 fix shapes as they touch the analyzed structure
- Scope and relevant neighboring paths: the fix-round changes in `src/BellTower/Core/{ChangeOrigin,IdentityInheritance,WorkPolicy,HistoryState,CheckoutPolicy,SessionIdentity,NativeNamePolicy,IdentityControls}.cs`, `src/BellTower/IO/{IdentityStore,IdentityPresentationStore}.cs`, `src/BellTower/Sessions/{IdentityHistoryReader,IdentityResolver,NativeNameControl,NativeNamePump,SessionCatalog}.cs`, `src/BellTower/Providers/OpenSpecSelectionObserver.cs`, `src/BellTower/Cli/{RenameCommand,NativeSyncCommand}.cs`, `src/BellTower/Tray/{OverviewForm,TrayApplication}.cs`; tests `tests/BellTower.Tests/Sessions/IdentityRecoveryPolicyTests.cs`; baseline `d307c34` versions of `SessionIdentity` and `OpenSpecSelectionObserver`. Handoff [implementation-005](../evidence/implementation-005.md) read as claims, not evidence. Deferred suggestions ANALYSIS-003…011 were not re-raised.
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src tests scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md` printed:
  - HEAD `d307c34`, 84 manifest entries
  - tracked diff SHA-256 `6e39570f2be87f8fb93bb9e802a8f7811455e7dfb2423cda101870a6847cd31c`, 49 untracked files
  - `Summary: head=d307c34 baseline=d307c34 files=84 diff=6e39570f2be8 untracked=49/6dbedbadb00d` (matches the assignment)
- Before/after identity: after the analysis the same command printed `Summary: head=d307c34 baseline=d307c34 files=84 diff=6e39570f2be8 untracked=49/6dbedbadb00d` (tracked diff SHA-256 `6e39570f…cd31c`). It is identical, so the review target was not changed. The only writes were the two `evidence/analysis-002-*` files, both outside the identity paths, plus disposable probe data in the session scratchpad, which the probes deleted. The client refused the write of this report, so the orchestrator saves it.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md) (ANALYSIS-001, ANALYSIS-002); [qa-001.md](qa-001.md) and [findings.md](findings.md) read for context only

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-001 (a): weak evidence and walk termination | Read `IdentityHistoryReader.Satisfied`; reran the unchanged Analysis 001 `probe-weak.ps1` against the reviewed Release build | [probe results §1](../evidence/analysis-001-probe-results.txt) before; [analysis-002 probe results §1](../evidence/analysis-002-probe-results.txt) now: with a custom title and a newest weak `explain-change change-b`, recovery now ends at `ActiveWork=change-a WorkWeak=False` after 3 ticks (before: `change-b`, weak, 1 tick) | Verified (Claude shape; the stop condition is shared code) |
| ANALYSIS-001 (c): one clearing helper | `grep` for `WorkPolicy.Clear`, `ApplyBranch`, `HistoryState.Reset` and direct writes of the work fields | `WorkPolicy.Clear(identity, WorkClear.Branch\|History)` is the only full clear; callers are `CheckoutPolicy.ApplyBranch` (`CheckoutPolicy.cs:72`) and `HistoryState.Reset` (`HistoryState.cs:41`). `SessionCatalog.Rename` (`SessionCatalog.cs:50-53`) still nulls `SelectionRoot/SelectionOperation/SelectionStore` on a change pin. That is baseline behavior, now redundant with the `ChangeOrigin` marker. Low impact, because the next live evidence restores the root | Verified for work-evidence clears. The Rename partial clear is noted, not raised |
| ANALYSIS-001 (b): history floor | Source trace of `Clear(Branch)` → `HistoryFloor = max(floor, HistoryLength)`, the cursor-only no-write rule (`IdentityHistoryReader.cs:122-127`) and `HistoryState.Reset` (`HistoryState.cs:31`); probe `probe-floor.ps1`, both providers, two shapes | [analysis-002 probe results §2](../evidence/analysis-002-probe-results.txt) | Still failing in the common live-work case, not only in the acknowledged floor-0 window → ANALYSIS-100 |
| ANALYSIS-002: classification and tiering | Read `ChangeOrigin`, `SessionIdentity.PinnedChange/WorkSubject`, `WorkPolicy.Apply` legacy guard, `IdentityStore.GetCore` (classify on load), `SessionCatalog.Rename`, `IdentityControls.Reset`, `IdentityPresentationStore.Save`; compared with baseline `OpenSpecSelectionObserver` writes (`Change`, `SelectionRoot`, `SelectionEvent`, `SelectionAt`) | A baseline automatic record derives `legacy-selection`. A baseline rename (SelectionRoot cleared) and a launch value stay pins. The marker is bound to its value, so a `Change` rewritten by 0.1.8 is classified again. Weak evidence cannot fill while any `Change` exists (`WorkPolicy.cs:23-29`, observer `:48`). History at or before the legacy `SelectionAt` is rejected (`WorkPolicy.cs:19-22`). Live strong work and an explicit `Pin` replace it. `Classify` runs in memory on each load and persists only with the next real save, and `Of()` derives the same value before that, so there are no extra writes and no behavior gap | Verified; residual after supersession → ANALYSIS-101 |
| ANALYSIS-002: rollback / unknown fields | Baseline `SessionIdentity` has `[JsonExtensionData]`; developer test `AnAutomaticChangeWrittenByTheOlderBuildAfterARollbackIsClassifiedAgain`; probe `probe-legacy.ps1` with a `futureField` | [§3](../evidence/analysis-002-probe-results.txt): the unknown field survives every save, both providers. 0.1.8 still sees the legacy `Change`, which is the intended rollback view | Verified |
| ANALYSIS-002: behavior after supersession | Probe `probe-legacy.ps1`: 0.1.8-shaped automatic `Change`, then newer live work, then branch invalidation | [§3](../evidence/analysis-002-probe-results.txt): the subject goes `legacy-x` → `change-a` → `legacy-x` (provenance `legacy-selection`) for Claude and Codex | ANALYSIS-101 |
| Fork inheritance structure / parity | Read `IdentityInheritance`, the reader's `forkedFrom` harvest and `Find` call, `HistoryState.Reset` | Policy is in Core, pure, and copied once (`ForkInheritedFrom`). The provider adapter only proves the parent. Inherited work keeps `HistoryWorkOffset=-1`, so the fork's own walk still runs to completion. Codex has no fork adapter, which is recorded as unverified in the matrix (unfinished parity, not a restriction, per AGENTS.md) | No duplicated policy; the parity gap is honestly recorded (orchestrator owns the release-gate decision). Dead return value in ANALYSIS-102 |
| "Use Bell Tower name" structure / parity | Read `NativeNamePolicy.UseBellTowerName`, `NativeNameControl`, `SessionCatalog`, `RenameCommand`, `OverviewForm`, `TrayApplication` | The shared rule is in Core. The provider branch is in the `Sessions/*Name*` adapter: Claude only releases the title; Codex reuses `SetEnabled(true)` (take ownership). CLI and tray both go through `SessionCatalog` | No duplicated policy. Wiring duplication and UI-thread handling in ANALYSIS-103 |
| `NativeNamePump` Codex-only / helper removal | Read `NativeNamePump`, `NativeNamePolicy.StatusFor`; `grep` for `ClaudeName`, `helper.mjs`, `nativeHelper`, `stage-claude`, `Node runtime` in `src tests` | The pump visits only Codex sessions and the Codex service/schedule are unchanged. `StatusFor` is the single status source for list, overview and doctor. No reference to removed types remains. `CodexRuntimeHost`'s `node.exe` lookup is baseline Codex runtime code | Clean, except two dead persisted fields and an unreachable-for-Claude branch → ANALYSIS-102 |
| Lock ordering with `IdentityStore.Find` | Traced every `DataLock` site | `Find` takes only `identity` and is called from `IdentityHistoryReader.Refresh` outside any lock, before the separate `Update`. Nestings are unchanged: `native-name`→`identity` (`CodexNameService`, both `NativeNameControl` methods) and `identity`→`launch` (`LaunchPresentationStore`). There is no inverse order. Reading the parent outside the child's lock is a harmless race, because inheritance is retried while `ForkInheritedFrom` differs | No deadlock risk |
| Duplicated precedence | Compared `SessionIdentity.Name/Subject/WorkSubject` with the provenance chain in `IdentityPresentationStore.cs:19-23` | The provenance chain restates the `WorkSubject` order (pin → work → legacy → fallback). It is consistent today | Noted only; it will need the same edit as ANALYSIS-101 |
| Targeted tests | `dotnet test -c Release --no-build --no-restore --filter "FullyQualifiedName~IdentityRecoveryPolicyTests\|FullyQualifiedName~IdentityHistoryTests\|FullyQualifiedName~NativeNameTests\|FullyQualifiedName~WorkPolicyTests"` | Passed 54/54 (existing build; the binaries are newer than every source file) | Passed. Full build, full test suite and format verification were not run (QA 002 owns them on the same target) |

Client coverage: each probe ran for both Claude (transcript `tool_use`/`tool_result`) and Codex (rollout `custom_tool_call(apply_patch)`/output). The walk-termination probe is Claude-only because only Claude has a history title; the stop condition is shared.

## Findings

### ANALYSIS-100: The branch-invalidation history floor uses a lagging cursor, and any fresh read erases it

- Severity: warning
- Related task / requirement: 3.3; "Git branch and worktree context" (branch changes invalidate only branch-bound automatic evidence); QA-004
- Locations:
  - `src/BellTower/Core/WorkPolicy.cs:16-18` (floor check), `:56` (`WorkBranch = ObservedBranch`), `:81` (`HistoryFloor = Math.Max(HistoryFloor, HistoryLength)`)
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:122-127` (forward-cursor growth under 64 KiB is not persisted when nothing derived changed), `:112` (fresh pass calls `HistoryState.Reset`)
  - `src/BellTower/Core/HistoryState.cs:31` (`HistoryFloor = -1` on every reset)
  - `src/BellTower/Core/CheckoutPolicy.cs:72`; `src/BellTower/Sessions/IdentityResolver.cs:46`, `:50`
- Observation:
  - The floor is set from the persisted `HistoryLength`. To save writes, the reader does not persist forward-cursor growth below `CheckpointBytes` (64 KiB) unless derived state changed.
  - A history record that duplicates live work changes nothing: `WorkPolicy.Apply` rejects it because the live receipt (`SelectionAt` from the hook) is newer than the transcript timestamp. So the region holding the pre-switch edit is read every tick, but its cursor stays unsaved.
  - When the branch then switches, the floor lands below that record. The next pass re-reads the region with `ActiveWork == null`, the record is above the floor, and it is applied with `WorkBranch` set to the new `ObservedBranch`. The pre-switch work now looks bound to the new branch and will not be invalidated again.
  - Measured for both providers ([§2 "tail"](../evidence/analysis-002-probe-results.txt)): `HistoryLength=4352` while the file is 9296 bytes, `HistoryFloor=4352` after the switch, and then `ActiveWork=change-a WorkSource=history:edit WorkBranch=feature`. Codex gives the same with 4314/9080.
  - This is the ordinary shape (edit a change, then switch branch within 64 KiB of transcript growth). It is not the rare floor-0 window that Implementation 005 acknowledged.
  - The floor-0 case is real too, and for a broader reason than "one tick". Every fresh pass (first read, fingerprint change, truncation) runs `HistoryState.Reset`, which sets `HistoryFloor = -1`, so the floor never survives a re-read of the same file. Measured ([§2 "before-first-read"](../evidence/analysis-002-probe-results.txt)): floor 0 is replaced by -1 on the first pass, and the pre-switch work is selected on `feature`.
  - `ABranchSwitchWritesAHistoryFloorSoOlderPreSwitchWorkIsNeverReselectedForBothClients` seeds the work through a fresh read, where the cursor is persisted at file length. It therefore does not cover either shape.
- Proposed improvement and benefit:
  - Record the invalidation as a time floor instead of a byte offset. `WorkPolicy.Clear(Branch)` would set, for example, `WorkFloorAt = now`. `Apply` would reject history evidence with `At < WorkFloorAt`, and a fresh pass of the same conversation would keep it.
  - History evidence already carries the provider timestamp, live evidence is ordered by time, and a time floor does not depend on cursor persistence, chunking or rotation.
  - The `Satisfied` floor clause then becomes "a floor exists".
  - Benefit: closes QA-004 for the tail and fresh-read shapes in one shared Core rule for both clients, and removes the coupling between a write-saving optimization and invalidation correctness.
  - Keep the existing `SelectionFloorAt` (live-evidence pruning) separate.
- Behavior-preservation risks:
  - Clock skew between provider timestamps and the tray clock: use the provider-timestamp domain or a small tolerance.
  - Records with unparseable timestamps (`DateTimeOffset.MinValue`) would fall below the floor. That is correct, because unknown is not newer.
  - Post-switch work must still be accepted (existing test step).
  - An explicit reset may clear the floor; a transcript restart should not.
- Evidence type: measured result (isolated probe, reviewed build) plus source observation
- Verification / measurement plan:
  - Add both probe shapes to `IdentityRecoveryPolicyTests` for Claude and Codex: live work, tail growth under 64 KiB, switch, tick, expect `ActiveWork == null`; and switch before the first read, tick, expect `null`. Keep the post-switch acceptance step.
  - Rerun `IdentityRecoveryPolicyTests`, `IdentityHistoryTests`, `WorkPolicyTests` and the [probe](../evidence/analysis-002-probe-scripts.ps1.txt).
- Relationship: residual of ANALYSIS-001 part (b) and of QA-004. Not a duplicate of the acknowledged floor-0 limitation: the common tail case is new.

### ANALYSIS-101: A superseded legacy-selection `Change` is never retired and comes back as the subject when the newer work is cleared

- Severity: warning
- Related task / requirement: 1.2, 2.1; design "Migration Plan" ("until stronger verified evidence or user action clarifies them"); ANALYSIS-002
- Locations:
  - `src/BellTower/Core/SessionIdentity.cs:66-70` (`WorkSubject => PinnedChange ?? ActiveWork ?? Change`)
  - `src/BellTower/Core/WorkPolicy.cs:19-22` (legacy guard keyed on `SelectionAt`), `:53-59`, `:66-83` (`Clear` nulls `ActiveWork` and `SelectionAt`)
  - `src/BellTower/IO/IdentityPresentationStore.cs:19-23`
  - `src/BellTower/Sessions/IdentityResolver.cs:30` (`work = identity.WorkSubject` drives the unique-work checkout mapping)
  - `src/BellTower/Tray/OverviewForm.cs:199` (change box shows only `PinnedChange`)
- Observation:
  - The legacy value correctly yields to newer verified work, but only while that work stays in `ActiveWork`.
  - When the work is cleared by a branch invalidation, or by `HistoryState.Reset` for history-derived work on a transcript restart, the pre-upgrade automatic `Change` becomes the subject again. It also drives checkout mapping again.
  - Measured for both providers ([§3](../evidence/analysis-002-probe-results.txt)): `legacy-x` (provenance `legacy-selection`) → newer live work `change-a` (provenance `edit`) → branch invalidation → `legacy-x` (provenance `legacy-selection`).
  - The legacy guard is also gone at that point, because the newer work overwrote `SelectionAt` and `Clear` nulled it.
  - The migration plan says legacy values stand until newer evidence clarifies them. Bringing back an inspection-derived 0.1.8 name after a branch switch reproduces the field-report misname class.
  - The overview change box shows only `PinnedChange`. A user therefore cannot see the legacy value there or clear it alone. "Reset pins" also resets title, checkout and palette, and an unchanged empty box sends no change.
- Proposed improvement and benefit:
  - Make supersession one-way inside the existing classifier. When `WorkPolicy.Apply` applies strong verified work while `ChangeOrigin.IsLegacySelection` is true, record a superseded origin bound to the value (for example `ChangeSource = "legacy-superseded"`, `ChangeSourceValue = Change`).
  - `WorkSubject` and the provenance chain then skip it.
  - `Change` itself stays, so a rollback to 0.1.8 still sees it, and a value rewritten by 0.1.8 is classified again through the existing value binding.
  - Benefit: honors the migration plan, keeps `ChangeOrigin` the single classifier, and removes the resurfacing for both clients.
- Behavior-preservation risks:
  - Weak evidence must not supersede, and it cannot fill anyway.
  - Fork inheritance copies `ChangeOrigin.Of(parent)`, so the new origin must be handled there too.
  - Rollback display is unchanged.
  - `IdentityPresentationStore` restates the precedence and needs the same edit.
- Evidence type: measured result (isolated probe, reviewed build) plus source observation
- Verification / measurement plan: extend `ALegacyAutomaticChangeYieldsToNewerVerifiedWorkWhileUserAndLaunchValuesStayPins` with two cases for both providers. In each, newer work is followed by (1) a branch invalidation and (2) a transcript restart, and `WorkSubject` must fall back to `AutomaticSubject`, not the legacy value. Keep the rollback test green.
- Relationship: residual of ANALYSIS-002 (the yield is implemented; retirement after supersession is not)

### ANALYSIS-102: Fix-round leftovers: dead persisted fields and an unreachable Claude branch

- Severity: suggestion
- Related task / requirement: 2.5, 6.3; design compatibility rule ("New shared JSON writers must preserve unknown fields")
- Locations:
  - `src/BellTower/Core/SessionIdentity.cs:40` (`PendingForkTitle`): never read or written anywhere in `src`/`tests`
  - `src/BellTower/Core/SessionIdentity.cs:32` (`NativeTitleAt`): only ever cleared (`HistoryState.cs:37`, `NativeNamePolicy.cs:57`); nothing sets it since the Claude SDK path was removed
  - `src/BellTower/Core/IdentityInheritance.cs:7` (`FromParent` always returns `true`; the result is unused at `IdentityHistoryReader.cs:115`)
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:208` (`NativeNamePolicy.Ours` exclusion)
- Observation:
  - Neither field has shipped. Once released, both become part of the persisted identity contract that every later build, including `sync-native-session-titles`, must carry.
  - The `Ours` exclusion is unreachable for Claude in this release, because no Claude write records `NativeLastName`/`NativePendingName`. Keeping it for the follow-up change is reasonable. However, tests that seed those fields do not show that Claude-managed titles are excluded in practice.
- Proposed improvement and benefit:
  - Remove `PendingForkTitle` and `NativeTitleAt` before release; this costs no migration now.
  - Make `FromParent` `void`.
  - Add a one-line comment on the `Ours` branch that it is reserved for the deferred write.
  - Benefit: a smaller compatibility surface and no misleading state.
- Behavior-preservation risks: none for stored data, because the fields were never shipped. Identity files written during development by Implementations 001–005 keep the values through `JsonExtensionData`.
- Evidence type: source observation (`grep`)
- Verification / measurement plan: Release build, full tests, and `grep -rn "PendingForkTitle\|NativeTitleAt" src tests` returning nothing
- Relationship: new (helper-removal and fork-fix leftovers)

### ANALYSIS-103: On Codex, "Use Bell Tower name" duplicates "Use name in native chat" but runs on the UI thread without that control's error handling

- Severity: suggestion
- Related task / requirement: 4.1, 6.2; QA-003 fix shape
- Locations:
  - `src/BellTower/Sessions/NativeNameControl.cs:35-46` (Codex branch calls `SetEnabled(true)`), `:13-31`
  - `src/BellTower/Tray/TrayApplication.cs:151-166` (`SetNativeSync`: worker thread plus `catch` for IO, UnauthorizedAccess, Json and NotSupported, logged), `:168` (`UseBellTowerName`: synchronous, no `catch`)
  - `src/BellTower/Tray/OverviewForm.cs:103-116`, `:190`
- Observation:
  - For Codex rows both buttons perform the identical operation, `NativeNameControl.SetEnabled(paths, session, true)`, but they are wired differently.
  - The new handler runs on the UI thread. It waits on the per-session `native-name` lock, which `CodexNameService` holds during a sync, and then on `identity`. `SessionCatalog.Find` throws `ArgumentException` if the session left state after the grid refreshed.
  - No `Application.ThreadException` handler exists (`grep`). The status text "Native title released" is set even when nothing was released.
  - The rule itself is shared (`NativeNamePolicy.UseBellTowerName` in Core), so this is duplicated wiring, not duplicated policy. The existing `SaveIdentity` handler has the same synchronous pattern, which predates this change.
- Proposed improvement and benefit:
  - Route both controls through one tray handler (worker thread, the same `catch` and log, status from the result).
  - Have `UseBellTowerName` return whether a title was released.
  - For Codex, either merge the two buttons or document that they are the same action.
  - Benefit: one code path per control and no UI-thread exception from a race.
- Behavior-preservation risks: UI only; the stored effect is unchanged
- Evidence type: source observation (no UI run)
- Verification / measurement plan: `OverviewControlTests` for both providers asserting one handler, the status text when nothing is released, and a fake catalog that throws without escaping the handler
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=6e39570f2be8 untracked=49/6dbedbadb00d` | (a) The Analysis 001 probe now recovers `change-a`, not weak, with a custom title and a newest weak skill ([§1](../evidence/analysis-002-probe-results.txt)); `Satisfied` requires `!WorkWeak` or the floor. (c) `WorkPolicy.Clear` with Branch/History scopes is the only work-evidence clear (Rename's baseline partial clear of planning fields noted). (b) The floor is written, but it uses the lagging persisted cursor and is erased by any fresh pass. Measured re-selection on the new branch for both providers ([§2](../evidence/analysis-002-probe-results.txt)) | Partially verified: (a) and (c) verified; (b) still failing, residual tracked as ANALYSIS-100 |
| ANALYSIS-002 | same | `ChangeOrigin` is bound to its value. A 0.1.8 automatic record derives `legacy-selection`, and a baseline rename or launch value stays a pin. It is the lowest work tier; weak evidence cannot displace it; older history cannot displace it; newer live work and an explicit `Pin` replace it. A `Change` rewritten after rollback is classified again. Unknown fields are preserved (probe `futureField`, baseline `JsonExtensionData`). User and launch values remain pins. Same results for Claude and Codex ([§3](../evidence/analysis-002-probe-results.txt)); targeted tests 54/54 | Verified for classification, tiering, explicit association, rollback and unknown fields. Residual: the superseded value comes back after the newer work is cleared, tracked as ANALYSIS-101 |

## Assessment and limitations

- Fix-round structure is sound in layering:
  - Fork inheritance, "use Bell Tower name", change origin and work clearing are single Core policies with thin provider branches only in allowed adapter locations.
  - `NativeNamePump` is Codex-only, and the shipped Codex flow is unchanged.
  - Status comes from one source (`NativeNamePolicy.StatusFor`).
  - Lock ordering is unchanged (`Find` is never nested).
  - The helper removal left no references to deleted types.
- Two warnings remain. Both are residuals of the rechecked findings and both were measured for Claude and Codex:
  - ANALYSIS-100: the history floor misses the common live-work tail case, and a fresh read erases it.
  - ANALYSIS-101: a superseded legacy `Change` comes back.
- Two suggestions concern cleanup and wiring: ANALYSIS-102 and ANALYSIS-103.
- Parity: Codex fork inheritance has no adapter and is recorded as unverified. Under AGENTS.md this is unfinished parity, not a restriction. The orchestrator owns whether it gates the release.
- Not performed:
  - full Release build, full test suite and format verification (QA 002 scope on the same target)
  - UI run of the overview controls
  - any live provider session (prohibited)
- Probes used only isolated scratchpad data and synthetic transcripts, with a copy of the reviewed build.
- No unexpected writes. Before and after identity match.
- The report write was refused by the client and the text was returned inline.
- Decisions for the orchestrator:
  - Triage ANALYSIS-100 and ANALYSIS-101 as blocking or not. They decide whether ANALYSIS-001 part (b) and ANALYSIS-002 can be closed.
  - For ANALYSIS-100, choose whether the fix is a time floor (recommended) or an offset floor taken from the transcript length that survives same-file fresh passes.
