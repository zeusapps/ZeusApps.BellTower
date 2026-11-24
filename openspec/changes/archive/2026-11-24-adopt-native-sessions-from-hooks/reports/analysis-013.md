<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-013 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-013 (DEV-025, tasks 2.4 and 2.3)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent); version not queried, because the assignment forbids launching `claude`; auto mode; `claude-opus-5-5`
- Date: 2026-11-04 (about 16:30 UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-013
- Task and requirement references:
  - Tasks 2.3 and 2.4, with 3.x and 4.2 as the future users of the seam
  - [design](../design.md) decisions 2, 3, 4 and 7 and the Risks section
  - [session-tracking spec](../specs/session-tracking/spec.md) and [terminal-navigation spec](../specs/terminal-navigation/spec.md)
  - [implementation 025](../evidence/implementation-025.md)
- Scope:
  - `src/BellTower/Core/ConversationLifecycle.cs` (new)
  - `src/BellTower/Core/AttachmentEvidence.cs`
  - `src/BellTower/IO/StateStore.cs`
  - `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs` (new)
  - `evidence/implementation-025.md`
- Neighboring paths read:
  - `src/BellTower/IO/AttachmentHealth.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/IO/ProcessProbe.cs`, `src/BellTower/IO/StateSnapshotStore.cs`
  - `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Hosts/Navigator.cs`
  - `src/BellTower.Launcher/Program.cs`, `src/BellTower.Distribution/ReleaseContract.cs`
  - `tests/BellTower.Tests/IO/AttachmentHealthTests.cs`, `tests/BellTower.Tests/IO/StorageTests.cs`
  - `docs/attachment-health.md`, `docs/development.md`
- Baseline commit: `683ad12`
- Implementation identity before the analysis (`./scripts/review-identity.ps1 -Baseline 683ad12 -Paths <the 5 scoped paths>`):
  - HEAD and baseline: `683ad12fa0e5c97e05457848be260eabd4c9bc0e`
  - Manifest: untracked (`?`) implementation-025.md, ConversationLifecycle.cs and ConversationLifecycleTests.cs; modified (`M`) AttachmentEvidence.cs and StateStore.cs
  - Tracked diff SHA-256: `03c97bf31901b39fb221ed39868365f0c25a525282ca161abab492ec5aa94d43`
  - Untracked file hashes: `4180888f…` (implementation-025.md), `728f4ea0…` (ConversationLifecycle.cs), `f4c0db3c…` (ConversationLifecycleTests.cs)
  - Summary line: `head=683ad12 baseline=683ad12 files=5 diff=03c97bf31901 untracked=3/25286b6dadb7`, which matches the assignment
- Identity after the analysis: the same summary line. Nothing in scope changed.
- Prior reports or rechecked findings: none. This is a new review of DEV-025.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build | `dotnet build -c Release --no-restore` | 0 warnings, 0 errors (incremental build) | passed |
| Focused tests | `dotnet test -c Release --no-build --no-restore --filter` on ConversationLifecycleTests, AttachmentHealthTests, StorageTests, AttachmentEvidenceTests and NativeAdoption | 144 passed, 0 failed | passed. I did not rerun the full suite; the developer reports 1252 passed. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit code 0 | passed |
| Behavior probes | A throwaway console project in the session scratch directory that references the Release `bell-tower.dll` and calls `ConversationLifecycle.Evaluate` with fake process checks and fake evidence sources. No repository files, data directory or provider were involved. | Results are quoted in ANALYSIS-1201, 1202, 1203, 1204, 1206 and 1207 | Measured on the reviewed build |
| Duplication with `AttachmentHealth`, `NativeAdoption` and reconciliation | Reading the code | ANALYSIS-1203, 1204, 1205, 1206 | Direct records are passed unchanged to `AttachmentHealth`, which is good. The shared path adds a second grace/stickiness rule and a second source for hook freshness. |
| Fit of the seam for the 4.2 binder and 3.x transport | Reading against decisions 3, 4 and 7 and the terminal-navigation scenarios, plus probes | ANALYSIS-1200, 1201, 1202 | The seam needs rework before 4.2 and 3.x use it |
| Core-to-IO dependency direction | Reading; `grep "using BellTower.IO" src/BellTower/Core` | ANALYSIS-1205 | New Core policy depends on the IO types `AttachmentHealth` and `ProcessLiveness`. The only earlier Core-to-IO use was `NativeAdoption` using `ContentHash`. |
| Drain performance | Reading `StateStore.ReconcileAttachments` and `ProcessProbe.Inspect` | No new I/O today: `NoConversationEvidence` returns null and no shared record stores a client yet, so the reducer makes no new process checks. After 4.2, each bound shared record adds one PID-plus-creation-time check per drain, the same cost as a direct record today. No process enumeration was added. | The risk is in the seam contract (ANALYSIS-1200). Not measured, because no real evidence producer exists yet. |
| Thin adapters and Claude/Codex parity | Reading | The policy branches on `LifecycleOwner`, not on the provider name. Claude reaches the shared path only if an adapter classifies it as a shared backend (`mayShareBackend`), so there is no per-provider policy. Direct-process tests run for both Claude and Codex; shared tests run for Codex only, which matches the provider facts; the mixed-session test covers both. | No parity gap. No adapter exists yet, which is accepted as out of scope. |
| Tests | Reading | `ADirectAgentCrash…` and `AQuietLiveProcess…` overlap `StorageTests.DeadAttachmentIsRemovedWithoutInactivityExpiry` and `QuietLiveProcessIsNotExpired`, but they add the Codex case and the injected process check that task 2.3 requires, so the overlap is acceptable. Most tests assert persisted behavior; the exception is ANALYSIS-1204. No test runs two drains in a row with runtime evidence (ANALYSIS-1203). | See findings |
| Developer's 4.2 constraint | Reading `AttachmentHealth`, the launcher, `ReleaseContract.Validate`, `StateSnapshotStore.IsValid` and `Navigator` | ANALYSIS-1202 | The constraint is correct; adopt it with the additions listed there |

## Findings

### ANALYSIS-1200: The drain asks for evidence one record at a time while holding the state and presentation locks

- Severity: warning
- Related task / requirement: 2.3; 3.1, 3.2 and 4.2; decision 1 (runtime and host evidence are resolved by the consumer or by existing asynchronous services); decision 4 (bounded time budgets and backoff); Risks ("keep runtime and host work outside capture", "bounded polling/backoff")
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:31-35` (the `IConversationEvidenceSource` interface) and `:94-95` (the per-record calls); `src/BellTower/IO/StateStore.cs:81` (the `state` lock) and `:176-179` (the `presentation` lock held around each call)
- Observation: `ReconcileAttachments` calls `source.SharedTerminal(...)` and `source.Work(...)` for every session that has not ended. It does this while holding the `state` lock and that session's `presentation` lock. The planned real sources do blocking I/O:
  - The 4.2 binder calls `mcpServerStatus/list`, finds the process that owns the loopback port, and checks the backend connection.
  - The 3.x transport calls `thread/read` over a local socket or WebSocket.

  Nothing in the interface says an implementation must answer without blocking. A straightforward implementation would therefore run RPCs and socket-table lookups inside the drain. That would also stall title writes and detach, which share the `presentation` lock, and acknowledgements and reminders, which share the `state` lock. The interface is also synchronous, which does not fit an asynchronous transport.
- Proposed improvement and benefit: Before 3.x and 4.2 are built, turn the seam into a snapshot. Two options:
  - Give the drain one read-only map of conversation ID to terminal and work facts, filled by an asynchronous pump that runs outside the locks, similar to `NativeNamePump`.
  - Or state in the interface contract that implementations return cached facts immediately with no I/O, and pin that with a test using a source that throws if called without a cache.

  Keep polling limits and backoff in the pump. This makes drain time independent of how responsive the provider is and avoids reworking 3.1 and 4.2 later.
- Risk to existing behavior: none today, because only `NoConversationEvidence` is connected. A snapshot map must keep the rule that a missing observation never changes stored state.
- Evidence type: reading the code. The performance effect is a hypothesis, because no real source exists yet.
- How to verify: once the first real source exists, time `Drain` with N shared sessions while the source is slow or hung (for example a fake endpoint that waits 5 seconds). Drain time must not depend on the source, and the title writer must not wait on it for the `presentation` lock.
- Relationship: new

### ANALYSIS-1201: The terminal facts cannot report a fresh "cannot verify" result, and a stored `connected` binding never expires

- Severity: warning
- Related task / requirement: 2.3 and 4.2; decision 3 step 5 (a missing origin, an unloaded thread or an unknown version gives "unverified" and no host actions); decision 2 (the same freshness rules apply to all independent evidence); terminal-navigation scenarios "Shared-Codex client no longer verifiably displays a conversation" and "Terminal identity is replaced or moved"
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:18-25` (the facts record), `:116` (`owner = facts?.OwnerClient ?? stored.Client`), `:127-146` (no branch for "unverified") and `:146` (the code falls back to the stored state)
- Observation: The binder has no way to say "I checked just now and could not verify a binding":
  - If the facts have no owner client, the reducer falls back to the client stored earlier.
  - If that process is still running and the listener and backend fields are empty, the stored state is returned unchanged.
  - A stored binding with no facts at all never ages.

  Measured:
  - A stored `connected` binding plus fresh facts with no owner, listener or backend value stayed `connected` with source `client-endpoint`, and its observation time stayed 5 hours old.
  - A 5-hour-old stored `connected` binding with no facts stayed `connected`.

  To fail closed, the 4.2 binder would have to report "listener gone", which records a false `disconnected` (`listener-gone`). Decision 2 keeps "disconnected" and "terminal unverified" as different states. The 5.2 diagnostics, and any future action check based on this evidence, would show or act on an out-of-date `connected`.
- Proposed improvement and benefit:
  - Add an explicit outcome to the facts, for example a binding value of `connected`, `disconnected`, `demoted` or `unverified` plus a reason. The binder can then report decision 3 step 5 directly.
  - Make the reducer downgrade `connected` to `unverified` once its observation time is older than a freshness window, the same way runtime evidence uses `RuntimeFreshness`.
  - Keep checking whether the stored owner process is alive, so `owner-exited` is still detected after a restart.

  4.2 can then plug in without changing the reducer, and the diagnostics stay honest.
- Risk to existing behavior: `BellTowerRestartKeeps…` expects stored state to survive a drain with no facts. That stays true for `disconnected` and `demoted`; only `connected` would age. Host actions do not read the binding today, so current behavior does not change.
- Evidence type: measured with the probe against the reviewed Release build
- How to verify: add reducer tests for three cases. Fresh "unverified" facts after a `connected` binding give `unverified`, not `disconnected`. An old `connected` binding with no facts gives `unverified`. An owner process that dies with no facts still gives `owner-exited`.
- Relationship: new

### ANALYSIS-1202: The "shell exited" rule reads `Host.Shell`, which the 4.2 constraint forbids, and ignores the shell stored in the terminal evidence

- Severity: warning
- Related task / requirement: 2.3, 2.4 and 4.2; implementation-025 "Constraint for task 4.2" and proposed planning edit 2
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:149-152` (`ShellExited` reads `attachment.Host.Shell`), `:154-168` (`Changed` never sets the shell) and `:18-25` (the facts have no shell field); `src/BellTower/Core/AttachmentEvidence.cs:49-59` (`TerminalEvidence.Shell`, reserved for this); `src/BellTower/Hosts/Navigator.cs:19,30` (navigation requires `attachment.Process`)
- Observation: The developer's constraint keeps the bound client and shell only in `evidence.terminal`. The reducer does not match that layout in three ways:
  - **The shell-exit rule would never run.** If the shell is stored in `evidence.terminal.Shell` and `Host.Shell` is empty, the real shell is never checked. Measured: with the evidence shell exited and `Host.Shell` empty, the binding stayed `connected`.
  - **The binder cannot supply a shell.** The facts record has no shell field, so nothing can fill `evidence.terminal.Shell`.
  - **The old shell survives an owner change.** Measured: after facts named a new owner, the client was the new process (4002) but the shell was still the old owner's shell (5005).

  The constraint itself is right, for two reasons.
  - **An older tray would end the session for good.** A health-v1 tray treats a dead `Host.Shell` as `confirmed-ended` and writes `EndedAt` (`AttachmentHealth.cs:13-15`). The new shared path respects `EndedAt` (`ConversationLifecycle.cs:89-92`), so the session stays ended even after upgrading again.
  - **An `attachmentHealth: 2` gate is more than a version bump.** All three of these accept only 0 or 1, so the gate needs a coordinated change to the distribution contract and the stable launcher:
    - `ReleaseContract.Validate` (`ReleaseContract.cs:42`)
    - `StateSnapshotStore.IsValid` (`StateSnapshotStore.cs:38`)
    - the stable launcher's check (`BellTower.Launcher/Program.cs:19`)

  Separately, `Navigator` requires `attachment.Process` (`Navigator.cs:19,30`). Navigation for shared conversations in 4.2 will therefore need a destination path based on `evidence.terminal` plus a re-check at the moment of the action anyway, which agrees with the constraint.
- Proposed improvement and benefit:
  1. Adopt the planning edit and extend it. For a shared conversation, the 4.2 binder writes none of the legacy liveness or destination fields: `Process`, `Host.Shell`, `Host.WindowProcess`, `Host.WindowHandle`, `Host.TerminalSession` and `EndedAt`. Client, shell and host destination go in `evidence.terminal`. Navigation and title updates for shared conversations read that evidence and re-check it at the moment of the action. The plan should state that the gate alternative needs changes to the distribution contract, the launcher and the snapshot validity check.
  2. In the reducer:
     - Check the shell stored in `evidence.terminal` for the `shell-exited` rule.
     - Add a shell field to the facts record.
     - Make `Changed` replace or clear the shell whenever the owner changes.
     - Stop reading `Host.Shell` for shared records, or keep that read only as a commented fallback for legacy records.

  This removes a rule that can never run, closes a stale-destination risk and lets 4.2 plug in without rework.
- Risk to existing behavior: none. Shared records never have `Host.Shell` today, because `NativeAdoption.Shared` sets the host to `unknown` and `AttachmentStore.Adopt` finds no launch shell for a shared ID. Moving the rule changes no current result.
- Evidence type: measured for the reducer behavior; reading the code for the cost of the gate
- How to verify: add reducer tests for a shell exit recorded in `evidence.terminal` and for an owner change clearing the shell. In 4.2, add an older-consumer test confirming a shared record carries no `Host.Shell` or `Process`.
- Relationship: new; this evaluates the developer's proposed 4.2 constraint

### ANALYSIS-1203: The time of runtime-confirmed work is not kept; `UnverifiedSince` moves backward and the effective grace becomes 2 minutes

- Severity: warning
- Related task / requirement: 2.3; session-tracking scenario "Terminal closes while backend work continues"; decision 2 (the same freshness and uncertainty rules for all evidence)
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:96-104`, mainly `:97` (`evidence.ConversationObservedAt ?? attachment.UnverifiedSince ?? attachment.RegisteredAt`)
- Observation: The shared path picks the evidence time with `??`. Whenever there is a last-hook time (`ConversationObservedAt`), it wins over the `UnverifiedSince` that runtime activity advanced on an earlier drain. Measured with a 10-minute grace and a last hook 1 hour ago:
  - At time t0, with the runtime reporting active work: `unverified`, since 16:29:01.
  - At t0 plus 3 minutes, with no runtime facts: `unavailable`, since 15:29:01.
  - At t0 plus 3 minutes, with the same 3-minute-old runtime fact: `unavailable`.

  So the stored time moves back by an hour. Work confirmed by the runtime keeps the conversation visible for only 2 minutes (`RuntimeFreshness`), not for the configured grace; hooks get the full grace. The existing `AttachmentHealth` never moves `UnverifiedSince` backward. After a transport reconnect, a gap between polls or a Bell Tower restart, the conversation drops to `unavailable` until the next fresh poll. The current tests evaluate only once, so they miss this.
- Proposed improvement and benefit: make the evidence time only move forward, for example the latest of the last-hook time, `UnverifiedSince` (or `RegisteredAt` if both are missing) and the active-work time. Alternatively, store the time of active runtime work in the evidence (for example `Runtime.ObservedAt`) and take the latest value. Runtime evidence then gets the same grace as hooks, which is the single rule decision 2 asks for.
- Risk to existing behavior: for shared records, `UnverifiedSince` already means "time of the latest evidence", so taking the latest value keeps that meaning. An older tray would see a later time and stay `unverified` only within the grace, the same as for hook-driven records. It never shows `verified-live`.
- Evidence type: measured with the probe
- How to verify: a two-step test. At t0 the runtime reports active work; at t0 plus 3 minutes there are no facts, with a 10-minute grace. Expect `unverified` and a `UnverifiedSince` that is unchanged or later. Run it through `StateStore.Drain` twice.
- Relationship: new; related to ANALYSIS-1205 (it comes from the second grace rule)

### ANALYSIS-1204: `HookActivity`, the `Work` result and `LifecycleResult.Terminal` have no effect in production, yet tests check them

- Severity: suggestion
- Related task / requirement: 2.3; development conventions (do not add speculative surface)
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:45-52` (`HookActivity`, `LifecycleResult`) and `:170-180` (`ReduceWork` choosing between hook and runtime signals); `src/BellTower/IO/StateStore.cs:179` (uses only the returned attachment); tests in `ConversationLifecycleTests.cs` at lines 124, 147, 184, 221, 225 and 239 (check `Work`) and line 91 (checks `Terminal` for a direct record)
- Observation: The only production caller ignores `Terminal` and `Work`. `HookActivity` only affects `Work`; hook freshness actually reaches the stored record through `ConversationObservedAt`, so there are two sources for the same thing. Measured: evaluating with fresh active hooks and with no hooks gave identical attachments (both `unavailable`); only `Work` differed (`Active` against `Unknown`). The logic that picks the newest signal and breaks ties in favor of the runtime only feeds a value nobody reads, and the tests check that detail rather than stored behavior.
- Proposed improvement and benefit: either remove `HookActivity`, the `WorkSignal` result and the direct-record `Terminal` mapping until something uses them (the 5.2 diagnostics), keeping `ReduceWork` as "time of fresh active runtime work"; or connect them to a consumer now and make `HookActivity` the only hook-freshness input instead of `ConversationObservedAt`. Either way one source decides, the reducer gets smaller, and the tests check stored state.
- Risk to existing behavior: none for stored state. Tests that check `Work` would check `Health` and `UnverifiedSince` instead.
- Evidence type: measured with the probe, plus reading the code
- How to verify: after simplifying, the existing stored-state assertions still pass, and a test that changes only hook activity shows a stored difference (or the input is gone).
- Relationship: new

### ANALYSIS-1205: Two health evaluators in two layers, with Core policy depending on IO

- Severity: suggestion
- Related task / requirement: 2.3; `docs/development.md` Responsibilities (Core holds policy without disk or native access; IO holds persistence and process inspection)
- Locations:
  - `src/BellTower/Core/ConversationLifecycle.cs:1` (`using BellTower.IO`)
  - `:77` and `:81` (uses `ProcessLiveness` and creates `new AttachmentHealth(inspect)`)
  - `:89-92` (repeats the `EndedAt` rule from `AttachmentHealth.cs:13-15`)
  - `:97-98` (a second grace and stickiness rule)
  - `src/BellTower/IO/AttachmentHealth.cs:5-27`
- Observation: The existing health policy (`AttachmentHealth`) lives in IO, while the new wrapper lives in Core and calls into IO. The two namespaces now depend on each other: IO's `StateStore` uses Core, and Core's `ConversationLifecycle` uses IO's `AttachmentHealth` and `ProcessLiveness`. `AttachmentHealth` now has only one production caller. The shared path re-implements the grace and stickiness rule with different meaning: `unavailable` is not sticky, and the clock starts from the latest evidence instead of from when the record first became unverified. The policy is split across two types instead of one shared evaluator, and ANALYSIS-1203 and 1206 come from that split.
- Proposed improvement and benefit: keep the data records (`SharedTerminalFacts`, `RuntimeWorkFacts`, `WorkSignal`) in Core and put all evaluation in one type. Either extend `AttachmentHealth` in IO with the shared path, or move `AttachmentHealth` and `ProcessLiveness` into Core as pure policy (it already receives the process check as a parameter). That one type should state the grace and stickiness rule for each lifecycle owner in one place. The result is one entry point, one dependency direction and an obvious home for the decision 2 rule.
- Risk to existing behavior: low, because this is a mechanical move or merge. Behavior for direct and legacy records must stay exactly as it is; `AttachmentHealthTests` already pins it.
- Evidence type: reading the code
- How to verify: the full test suite passes unchanged, and `grep "using BellTower.IO" src/BellTower/Core` shows no new policy dependency.
- Relationship: new; the root cause of ANALYSIS-1203 and 1206

### ANALYSIS-1206: Shipped behavior has changed: a hook alone now brings a shared conversation back from `unavailable`, unlike the documented rule

- Severity: warning
- Related task / requirement: 2.3, 2.4 and 5.3; `docs/attachment-health.md` (the health table and "Restart preserves the unknown origin and unavailable state"); session-tracking spec ("only fresh verified evidence SHALL restore their live status")
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:97-98`; `src/BellTower/IO/AttachmentStore.cs:102-103` (a hook keeps `Health` and `UnverifiedSince` but refreshes `ConversationObservedAt` through `Merge` and `Observe`); `tests/BellTower.Tests/IO/AttachmentHealthTests.cs:170-172` (for other records whose owner is unknown, "a newer hook timestamp alone do not revive it")
- Observation: Even with only `NoConversationEvidence` connected, as shipped, an `unavailable` shared conversation becomes `unverified` on its next hook. Before this change, `unavailable` was permanent for it. Measured: an unavailable shared record with a new hook became `unverified`, while the same record evaluated by `AttachmentHealth` stayed `unavailable`. `SessionState.RequiresAttention` and `PossiblyWaiting` then turn attention back on for that session.

  This may be what decision 2 intends, since a hook for the exact conversation is independent evidence about it. But it is a second revival rule next to the documented and tested one, and:
  - the evidence file says no documentation was changed;
  - no test pins hook revival for shared records;
  - the 2.4 assessment does not mention that rolling back to an older tray brings back the permanent `unavailable`.
- Proposed improvement and benefit: you need to confirm which rule is intended.
  - If revival is intended: document it in `docs/attachment-health.md` (shared conversations return to `unverified` on evidence for that exact conversation, and never become `verified-live` without a process), add a matching test, and mention the rollback difference in the 2.4 assessment.
  - If it is not intended: keep `unavailable` permanent for shared records until binder or runtime evidence exists.

  Either way there should be one documented rule per lifecycle owner, ideally in the single evaluator from ANALYSIS-1205.
- Risk to existing behavior: attention can reappear for revived shared sessions. They are `unverified`, never `verified-live`, so there are no reminders or host actions.
- Evidence type: measured with the probe, plus reading the code and the docs
- How to verify: a test through `AttachmentStore.Adopt` and `StateStore.Drain` in which an unavailable shared session receives a hook, with the expected health stated.
- Relationship: new

### ANALYSIS-1207: Each drain now overwrites evidence written by a newer version, even when no hook arrives

- Severity: suggestion
- Related task / requirement: 2.4 (rollback compatibility) and 2.1 (evidence that fails closed)
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:93` and `:103`; `src/BellTower/Core/AttachmentEvidence.cs:125-130` (`Of` builds fresh evidence when the stored revision stamp does not match)
- Observation: When a shared record's stored evidence has a different (for example newer) revision, `Of` returns freshly built evidence and the reducer saves `evidence with { Terminal = terminal }`. The newer evidence is thrown away on the next drain. Measured with a `revision: 2` record holding a `connected` terminal and an unknown extra field: the record was rewritten as revision 1, the extra field was dropped and the binding became `unverified`. `AttachmentStore.Adopt` already does this when a hook arrives. Now the drain also does it for every shared record on the first drain after a rollback between health-v1 versions, even with no activity. The 2.4 assessment says such evidence is "treated as absent", but it is also overwritten.
- Proposed improvement and benefit: when the stored evidence cannot be used, update only the legacy `Health` and `UnverifiedSince` and leave `Evidence` as it is, or keep the unusable content as extension data. Apply the same rule in `Merge` so both writers behave the same. Newer evidence then survives a temporary rollback.
- Risk to existing behavior: none for records at the current revision.
- Evidence type: measured with the probe
- How to verify: a test in which a revision-2 shared record goes through a drain with no facts and its `evidence` bytes are unchanged.
- Relationship: new

## Re-verification

None assigned.

## Assessment and limitations

- What I covered: the DEV-025 implementation of tasks 2.4 and 2.3, the seam that 3.x and 4.2 will use, and the developer's 4.2 constraint, for both providers. Claude and Codex direct-process tests, Codex shared tests and mixed sessions were all examined by reading and tests.
- What holds up: behavior for direct processes is unchanged, the drain adds no I/O today, and the 2.4 conclusion is sound for the records 2.3 writes: no gate is needed.
- Rework needed before 3.x and 4.2 build on the seam:
  - ANALYSIS-1200: snapshot contract for the evidence source
  - ANALYSIS-1201: an explicit "unverified" result and expiry of `connected`
  - ANALYSIS-1202: where the shell is stored and checked
  - ANALYSIS-1203: an evidence time that only moves forward. This is a correctness gap now, but only reachable once a runtime evidence source exists.
- Needs a decision now: ANALYSIS-1206 is the only finding that changes behavior with the current wiring.
- The developer's proposed 4.2 planning edit: recommended, with the additions in ANALYSIS-1202.
- Not done:
  - the full test suite; the developer reports it and the focused subset passed
  - drain timing measurements, because there is no evidence source to measure
  - live provider runs, as the assignment requires
- Probe project: a throwaway console project in the session scratch directory, outside the repository. It was built against the Release output and wrote nothing to the repository or to any data directory.
- Processes: none of mine are left running. MSBuild reuse nodes that were already running, and another agent's concurrent `dotnet test` run, were left alone.
- Unexpected writes: none. The untracked `openspec/changes/verify-codex-repository-subagents/reports/` and `openspec/changes/verify-signed-releases-and-update-policy/` were left untouched.
- Triage and the findings ledger belong to the orchestrator.
