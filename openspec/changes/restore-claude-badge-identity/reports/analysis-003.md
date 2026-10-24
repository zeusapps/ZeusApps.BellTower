<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
Exact refusal returned to the reviewer by the Write tool: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
Fallback recorded in reports/findings.md. Report text below is the reviewer's, unmodified.
-->

# Analysis report: restore-claude-badge-identity / Analysis 003 (recheck 2, final)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent launched by the Claude Code main-session orchestrator, auto permission mode; model `claude-opus-5-5`
- Date: 2026-10-24T09:30Z (UTC)
- Change root / assignment: `openspec/changes/restore-claude-badge-identity` / Analysis 003 (recheck 2, final; fix round 2 = Implementation 006)
- Task and requirement references:
  - tasks 1.2, 2.1, 2.5, 3.3, 3.5, 4.3, 6.1–6.3 in [tasks.md](../tasks.md)
  - [design.md](../design.md): decision 1 (role removed), decision 3 (fork/clear/resume), decision 4 (branch invalidation), "Migration Plan"
  - [session-identity delta](../specs/session-identity/spec.md): "App-owned names and optional change association" (legacy role inputs accepted and ignored), "Git branch and worktree context", "Provider conversation identity recovery", "Shared provider presentation controls", "Provider parity"
- Scope and relevant neighboring paths (round-2 changes):
  - `src/BellTower/Core/{WorkPolicy,ChangeOrigin,HistoryState,IdentityInheritance,CheckoutPolicy,SessionIdentity,IdentityControls,AlertPolicy,IdentityPresentation}.cs`
  - `src/BellTower/IO/{IdentityStore,IdentityPresentationStore,CheckoutCache}.cs`
  - `src/BellTower/Sessions/{IdentityHistoryReader,IdentityResolver,SessionCatalog}.cs`
  - `src/BellTower/Providers/OpenSpecSelectionObserver.cs`
  - `src/BellTower/Tray/TrayApplication.cs` (refresh cadence)
  - `src/BellTower/Cli/{LaunchCommand,RenameCommand}.cs`
  - `src/BellTower/Setup/{RoleProfile,PowerShellModule}.cs`
  - `src/BellTower/Integration/BellTower.psm1`
  - `src/BellTower.Protocol/{CaptureEnvelope,CaptureProtocol}.cs`
  - fixtures `tests/BellTower.Tests/Fixtures/identity/{claude-sdk-0.3.309-fork,codex-rollout-0.164.0-fork}.jsonl`
  - Handoff [implementation-006](../evidence/implementation-006.md) read as claims, not evidence.
  - Deferred ANALYSIS-003…011, 102 and 103 were not re-raised; none was made materially worse.
- Baseline commit: `d307c34`
- Implementation identity: before the analysis, `./scripts/review-identity.ps1 -Baseline d307c34 -Paths src tests scripts README.md .gitignore docs/compatibility.md docs/development.md docs/installation.md docs/releases.md docs/session-titles.md docs/session-identity.md docs/hook-ingress.md` (PowerShell 7.6.6, repository root) printed:
  - HEAD `d307c34`, 95 manifest entries
  - tracked diff SHA-256 `360be5266a96d8c67fc7aaed7c3668cfdde9e11fdab279363dbab957aa778645`, 50 untracked files
  - `Summary: head=d307c34 baseline=d307c34 files=95 diff=360be5266a96 untracked=50/dcd7b801db74` (matches the assignment)
- Before/after identity:
  - After the analysis the same command printed byte-identical output, including `Summary: head=d307c34 baseline=d307c34 files=95 diff=360be5266a96 untracked=50/dcd7b801db74`. The review target was not changed.
  - Writes: the two `evidence/analysis-003-*` files (outside the identity paths) and disposable probe data outside the repository (see limitations).
  - The client refused the write of this report, so the orchestrator saves it.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md) (ANALYSIS-001, ANALYSIS-002) and [analysis-002.md](analysis-002.md) (ANALYSIS-100, ANALYSIS-101); [qa-002.md](qa-002.md) and [findings.md](findings.md) read for context

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-100: time floor in the same conversation | Read `WorkPolicy.Apply`/`Clear` (`WorkPolicy.cs:17`, `:68-85`), `HistoryState.Reset` (writes no floor), `CheckoutPolicy.ApplyBranch(…, now)` (`CheckoutPolicy.cs:57-74`) and `IdentityResolver.cs:148` (`clock()`). Re-ran the Analysis 002 floor probe, adapted to `WorkFloorAt` (the switch passes a detection time on the synthetic timeline), plus three new cases | [results §1](../evidence/analysis-003-probe-results.txt), [scripts](../evidence/analysis-003-probe-scripts.ps1.txt) `probe-floor.ps1` | Verified for Claude and Codex:<br>- The tail and before-first-read shapes now end with `ActiveWork=` after the switch.<br>- Post-switch work at t=30 is accepted (`change-b`, `WorkBranch=feature`).<br>- The floor survives a fresh pass on a recreated file, an explicit reset plus history reset, and a rebind to another transcript. |
| Time-floor clock domain | Traced both clocks in the source; added a `lag` probe case. Tray timing: 2 s overview cadence (`TrayApplication.cs:215-220`), 5 s Git cache (`CheckoutCache.cs:19`), serial per-session loop (`TrayApplication.cs:266-271`) | [§1 `lag`](../evidence/analysis-003-probe-results.txt) | No finding:<br>- The floor is the tray's detection time; history `At` is the provider record stamp. Both come from the same machine clock.<br>- Detection is never earlier than the switch, so pre-switch records always fall below the floor. The error is in the safe direction.<br>- Post-switch records written before the switch is detected (within about 2 s cadence + 5 s cache + the serial loop) are also excluded. Measured: `change-b` at t=17, detected at t=20, gives `ActiveWork=`, and it stays excluded after a fresh pass. The next work action brings it back.<br>- The live receipt from that window was already cleared by the same switch before round 2.<br>- A skew tolerance would move the floor back into the pre-switch region and reopen ANALYSIS-100, so leaving it out is correct. |
| Floor versus live evidence and `SelectionFloorAt` | Read `OpenSpecSelectionObserver.cs:9-24`, `:46-60` and `:92-99` | Live evidence uses `Observation.At` (ingress time) and is not floor-filtered. `SelectionFloorAt` remains live-evidence pruning only and is untouched | The separation is correct. A spooled-envelope hypothesis is noted under the assessment (not raised) |
| ANALYSIS-101: supersession | Read `ChangeOrigin.Supersede/Holds/Legacy`, `WorkPolicy.cs:53-54`, `SessionIdentity.WorkSubject` (`:72`), `IdentityPresentationStore.cs:19-23` and `IdentityInheritance.cs:10`. Ran an extended legacy probe | [§2](../evidence/analysis-003-probe-results.txt) `probe-legacy.ps1` | Verified for Claude and Codex:<br>- Weak evidence does not supersede.<br>- Strong live work persists `changeSource=legacy-superseded` and keeps the value.<br>- After a branch invalidation, and after a transcript restart that clears history work, the subject falls back to `fallback`, not to `legacy-x`.<br>- A fork child does not inherit the retired value.<br>- Unknown fields survive.<br>- When a rollback build writes a different value, it is classified `legacy-selection` again. |
| Supersession is one-way; rollback | Same probe, with a rollback build rewriting the same value | [§2](../evidence/analysis-003-probe-results.txt) "rollback build rewrites the same value" | The record stays `legacy-superseded`, because the marker is bound to the value:<br>- This matches the design ("until stronger verified evidence … clarifies them"). An older build re-selecting the same inspection-derived value is not new verified evidence.<br>- After a rollback, 0.1.8 still displays the kept `Change`; this is the accepted rollback view.<br>- An explicit reset clears `Change` entirely (`IdentityControls.cs:22`), which is a user action. |
| What a fork inherits | Read `IdentityInheritance.FromParent`. New probe: a parent does pre-switch work, the checkout switches, there is no later work, then the conversation is forked on the same checkout | [§3](../evidence/analysis-003-probe-results.txt) `probe-fork-floor.ps1` | The superseded origin is handled. `WorkFloorAt` is not inherited, so the fork re-derives the parent's invalidated work for both providers. Raised as ANALYSIS-150 |
| QA-151 structure | Read `IdentityHistoryReader.cs:208-226` | A fork-suffix title is skipped before any state or cursor changes. Bell Tower-owned and released titles keep their earlier behavior. For a fork-only title, `NativeTitleOffset` stays -1, so the backward walk reaches the start once and then stops (`:67`, `back == 0`); work is not repeated | Sound. Applies to Claude only by nature: Codex titles are not in rollouts at 0.164.0, as stated in `Capabilities` |
| Codex fork adapter | Read the `ReadCodex` session_meta branch (`IdentityHistoryReader.cs:333-338`), the `Relevant` filter (`:206`) and the head read (`:76-82`). Cross-checked the fixture against the openai/codex rust-v0.164.0 recorder sources that the developer fetched into the session scratchpad. `rec.rs` shows: `SessionMeta{id, forked_from_id, …}` is written first; `JsonlWriter.write_rollout_item` stamps every line with `OffsetDateTime::now_utc()`; `ForkPersistence::Copied` is the default | Only the line whose `payload.id` is this session counts; `parent_thread_id` and copied parent `session_meta` lines are ignored. The policy stays in the shared `IdentityInheritance` | The adapter is thin and correct. It matters for ANALYSIS-150 that copied parent items in a Codex fork carry the fork time, not their original time |
| Head-window cost | `probe-head.ps1`: an 8 MiB synthetic rollout with a sparse head versus a head of 697 dense `exec_command` pairs. Stopwatch around the first (fresh) `Refresh`, 5 runs | [§4](../evidence/analysis-003-probe-results.txt): sparse 2.4–4.5 ms after JIT, dense 18–32 ms; `ForkParent=parent` is found in both | Bounded by construction:<br>- One extra 256 KiB read, only on a fresh pass (first read, fingerprint change or truncation), never per tick. Retries use the saved `ForkParent`.<br>- Classified head calls are discarded and only the fork parent is kept. The measured overhead is small; not raised.<br>- A `session_meta` line longer than `LineLimit` (256 KiB) is dropped, and the fork is then not inherited (graceful). |
| Role removal | `grep` for role symbols across `src` (`*.cs`, `*.psm1`, scripts). Read `AlertPolicy.Phrase`, `SessionIdentity.Name`, `IdentityPresentation`, `CaptureProtocol.cs:84`, `RoleProfile`, `PowerShellModule.cs:15-34` and `BellTower.psm1:6-7`, `:44`. Compared the persisted `SessionIdentity` properties with `d307c34` | No role remains in the name, speech, presentation, fork inheritance or capture consumption.<br>The remaining shims are minimal:<br>- hidden, ignored `--role` on launch and rename<br>- the module accepts and ignores `-Role` and nulls `BELLTOWER_ROLE`<br>- legacy shortcut functions are kept<br>- `CaptureEnvelope.Role` is decoded and ignored; its length check was dropped, but the envelope stays bounded by `MaximumEnvelopeBytes`<br>No baseline persisted field was removed (`Role` is kept). `IdentityPresentation` is new in this change, so dropping its `Role` costs nothing | Clean. Speech restates the name precedence instead of using `Name`: ANALYSIS-151 |
| Duplicated policy and lock ordering | Compared the precedence sites; `grep "new DataLock"` | The presentation provenance chain now matches `WorkSubject` (`ChangeOrigin.Legacy`). `AlertPolicy.cs:51` duplicates `SessionIdentity.cs:74`. Lock sites and nesting are unchanged since Analysis 002 (`native-name`→`identity`, `identity`→`launch`), and `Find` is still called outside the child's lock | One duplication (ANALYSIS-151); no lock-ordering risk |
| Targeted tests | `dotnet test -c Release --no-build --no-restore --filter "…IdentityRecoveryPolicyTests\|…WorkPolicyTests\|…SpeechTests\|…IdentityPrecedenceTests\|…IdentityHistoryTests\|…LegacyImportTests"` | Passed 113/113. The test bin's `bell-tower.dll` is byte-identical to the reviewed product build, and no test source is newer than the test assembly | Passed. Full build, full suite and format verification were not run (QA 003 scope on the same target) |

Client coverage: every probe ran for both Claude (transcript `tool_use`/`tool_result`, `forkedFrom`) and Codex (rollout `custom_tool_call(apply_patch)`/output, `session_meta.forked_from_id`). The exceptions are QA-151 (Claude `custom-title` only) and the head-window measurement, which covers a Codex-only code path.

## Findings

### ANALYSIS-150: A fork of a conversation whose pre-switch work was invalidated re-derives that work on the new branch

- Severity: warning. This is a narrow edge, and round 2 did not introduce it: the round-1 byte floor was not inherited either, and the child's fresh pass erased it.
- Related task / requirement: 2.5 (fork/clear/resume outcomes), 3.3; "Git branch and worktree context" (branch-bound evidence is invalidated); ANALYSIS-100 / QA-004
- Locations:
  - `src/BellTower/Core/IdentityInheritance.cs:7-27` copies pins and `ActiveWork`, but not `WorkFloorAt`.
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:111-124` runs inheritance, then `Merge` applies the child's own copy of the parent history.
  - `src/BellTower/Core/WorkPolicy.cs:17`: the floor check uses only the child's own floor, which is null.
- Observation:
  - The sequence:
    1. The parent works on change A on `main`.
    2. The checkout switches to `feature`. The parent's `WorkFloorAt` is set, its `ActiveWork` is cleared, and no work follows.
    3. The conversation is forked on the same checkout.
  - The fork's transcript contains the copied parent records. `FromParent` inherits no work (the parent has none) and does not copy the floor. The child's first pass then applies the copied pre-switch edit and binds it to the current branch.
  - Measured for both providers ([§3](../evidence/analysis-003-probe-results.txt)): the child shows `ActiveWork=change-a WorkSource=history:edit WorkBranch=feature Subject=change-a`, while the parent on the same checkout shows `Subject=repo`. This is the QA-004 misname class, reached through a fork.
  - The two providers differ in a way that matters for the fix:
    - Claude SDK forks keep the original record timestamps (fixture `claude-sdk-0.3.309-fork.jsonl`), so an inherited time floor would exclude the copies.
    - Codex restamps copied items at write time (`write_rollout_item` uses `now_utc()`; the default is `ForkPersistence::Copied`). The copies carry the fork time, so a time floor alone cannot exclude them.
- Proposed improvement and benefit:
  - Keep one shared rule in `IdentityInheritance`: for a proven fork, the parent's resolved identity is authoritative up to the fork point. That means:
    - Copy `WorkFloorAt = max(child, parent)`. This is sufficient for Claude.
    - Have the adapters exclude copied parent records from the child's own work evidence. Claude copies carry `forkedFrom`. Codex needs a fork boundary taken from the rollout, for example the first item after the parent's copied history, verified against the rust-v0.164.0 sources.
  - If the Codex boundary cannot be verified now, ship the floor copy and record Codex as unverified for this edge in the capability matrix. That is unfinished parity, not a restriction.
  - Benefit: the parent and the fork agree after a branch invalidation, and ANALYSIS-100's guarantee extends to forks for both clients.
- Behavior-preservation risks:
  - Ordinary fork inheritance must not change; the existing fork tests for both providers cover it.
  - Work the fork does after the fork point must still be accepted.
  - Excluding copied Codex items must not drop the fork's own first turn.
  - Floor inheritance must be one-way (max) and must never lower the child's own floor.
- Evidence type: measured result (isolated probe on the reviewed build) plus source observation
- Verification / measurement plan:
  - Add the probe sequence to `IdentityRecoveryPolicyTests` for Claude, for Codex and for a mixed pair. The child must report `ActiveWork == null` after the parent's invalidation and must then accept new post-fork work.
  - Rerun the fork, floor and supersession tests and [probe-fork-floor.ps1](../evidence/analysis-003-probe-scripts.ps1.txt).
- Relationship: new. It extends the scope of ANALYSIS-100 rather than reopening it, because the same-conversation guarantee is verified.

### ANALYSIS-151: Speech restates the name precedence instead of using `SessionIdentity.Name`

- Severity: suggestion
- Related task / requirement: 3.4, 3.5; "Shared provider presentation controls" (badge, title, chip and speech agree)
- Locations:
  - `src/BellTower/Core/AlertPolicy.cs:51`: `identity.ManualName ?? identity.NativeTitle ?? identity.Subject`
  - `src/BellTower/Core/SessionIdentity.cs:74`: `Name => ManualName ?? NativeTitle ?? Subject`
  - In baseline `d307c34`, `AlertPolicy.Phrase` used `identity.Name`.
- Observation:
  - Round 2 removed the role prefix from `Name`. The separate chain in `Phrase` was probably needed while `Name` carried that prefix; it is now an exact copy of `Name`.
  - The two are equal today. A later change to name precedence, such as the deferred `sync-native-session-titles` work, would have to edit both places, or speech would diverge from the badge, title and chip.
- Proposed improvement and benefit: use `identity.Name` in `Phrase`, so all surfaces share one precedence source.
- Behavior-preservation risks: none today, because the expressions are identical. The speech tests for both providers already pin the output.
- Evidence type: source observation
- Verification / measurement plan: `SpeechTests` and `PresentationAgreementTests` stay green
- Relationship: new; introduced by this round's role-removal edit. Raised only because the assignment asks for duplicated policy introduced by the fixes.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=360be5266a96 untracked=50/dcd7b801db74` | (a) Unchanged since Analysis 002: `Satisfied` still requires strong work, and the targeted recovery tests pass.<br>(c) `grep` shows `WorkPolicy.Clear` (Branch/History) is still the only clear. Its callers are `CheckoutPolicy.cs:72` and `HistoryState.cs:40`; the only other writers of `ActiveWork` are `Apply` and fork inheritance.<br>(b) Closed through ANALYSIS-100 below. | Verified (all parts) for Claude and Codex. The fork edge is tracked separately as ANALYSIS-150 |
| ANALYSIS-002 | same | Classification and tiering are unchanged. Retirement of the superseded value (the residual) is verified under ANALYSIS-101. Unknown fields are kept, and rollback reclassification works | Verified for Claude and Codex |
| ANALYSIS-100 | same | [§1](../evidence/analysis-003-probe-results.txt). Each of these shapes keeps `WorkFloorAt=12:00:20` and excludes the pre-switch work:<br>- tail shape: `HistoryLength=4352` < file 9082, switch at t=20, then `ActiveWork=`<br>- before-first-read shape: `ActiveWork=`<br>- fresh pass on a recreated file<br>- explicit reset plus `HistoryState.Reset`<br>- rebind to a copied transcript<br>Post-switch work at t=30 is accepted. Results are the same for Claude and Codex; targeted tests 113/113 | Verified for the same conversation. Forks do not inherit the floor: ANALYSIS-150 |
| ANALYSIS-101 | same | [§2](../evidence/analysis-003-probe-results.txt):<br>- `legacy-x` → `change-a` (persisted `legacy-superseded`)<br>- → `fallback` after a branch invalidation<br>- → `fallback` after a transcript restart<br>The fork child does not inherit the retired value. The value stays on disk along with the unknown field. A different value written by the rollback build is classified `legacy-selection` again. Claude and Codex give identical results | Verified |

## Assessment and limitations

- Round 2 closes the four rechecked findings with shared Core rules and thin adapters:
  - The time floor is a single `WorkPolicy` rule that does not depend on cursors.
  - Supersession lives in the existing `ChangeOrigin` classifier, bound to the value, so rollback and roll-forward behave.
  - The Codex fork adapter only supplies the parent id to the shared inheritance.
  - Role removal leaves only minimal, ignored shims, and the persisted contract is unchanged (no baseline field removed, `Role` preserved).
  - Lock ordering is unchanged.
- Time domain: leaving out a skew tolerance is correct. The floor errs late (it is the detection time), so every pre-switch record is excluded. The cost is post-switch work done in the few-second detection window (measured as `lag`), which the next work action recovers.
- New findings:
  - ANALYSIS-150 (warning): a narrow fork edge, measured for both clients and not introduced by round 2. For Codex, the fix needs a verified fork boundary in addition to the floor copy.
  - ANALYSIS-151 (suggestion): a one-line deduplication.
- Hypothesis, not raised:
  - A spooled live capture of pre-switch work, applied after the tray has already detected the switch (for example across a tray restart), would bind that work to the new branch, because live evidence is not floor-filtered.
  - I did not verify the order of spool consumption against the first resolver pass at tray start. Check: an isolated tray-start fixture with a spooled PostToolUse older than the detected switch.
- Not performed:
  - full Release build, full test suite and format verification (QA 003 scope)
  - any live provider session or live `codex fork` (prohibited or unavailable)
  - a UI run
- Disk space and leftover probe data:
  - The C: volume holding the session scratchpad was full (0 MB free). The probes therefore ran from `D:\Temp\bt-a3`, outside the repository. Each probe used an isolated data dir and deleted it, and loaded the build read-only from `src/BellTower/bin/Release/net10.0-windows`.
  - The client's built-in removal safety check blocked deleting the leftover `D:\Temp\bt-a3` folder (probe scripts, result text and an empty `tmp`). The user needs to delete it. It holds no product or user data. If `D:\Temp` did not exist before, it can be removed too.
- Writes: the evidence files were copied into `evidence/analysis-003-*` with a shell copy, inside my assigned paths. The client refused the write of this report, so the text is returned inline.
- Decisions for the orchestrator:
  - triage ANALYSIS-150: release-blocking, or a recorded limitation or follow-up for the final round
  - accept or defer ANALYSIS-151
