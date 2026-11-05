<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-016 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-016 (DEV-028, task 2.5)

## Review target

- Reviewer / role: Claude Code subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent); client version unavailable to the subagent; default permission mode; model `claude-opus-5-5`
- Date: 2026-11-04T18:09Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-016
- Task and requirement references: task 2.5 ([tasks.md](../tasks.md)); design decisions 1, 3, 7 and Risks ([design.md](../design.md)); neighboring tasks 2.2, 2.4, 4.2, 5.2; developer evidence [implementation-028](../evidence/implementation-028.md)
- Scope and relevant neighboring paths:
  - Scope: `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/IO/ProcessProbe.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs`, `tests/BellTower.Tests/Hosts/NativeAgentRelationshipProcessTests.cs`, `tests/BellTower.Tests/Providers/ModeCrossCheckTests.cs`, `evidence/implementation-028.md`.
  - Neighbors read: `src/BellTower.Ingress/Program.cs`, `src/BellTower/Providers/HookIngestor.cs`, `src/BellTower/Providers/AgentModeClassifier.cs`, `src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/Sessions/CodexRuntimeHost.cs`, `src/BellTower/Hosts/CodexRuntimeProcess.cs`, `src/BellTower/Sessions/CodexNameService.cs`, `src/BellTower/Core/CodexRuntime.cs`, `src/BellTower/Integration/BellTower.psm1`, `docs/development.md`, `docs/hook-ingress.md`, [analysis 006](analysis-006.md) ANALYSIS-500, [implementation-025](../evidence/implementation-025.md).
- Baseline commit: `93e3534`
- Implementation identity (before): `head=93e3534 baseline=93e3534 files=7 diff=3145ed9aff3c untracked=3/5cc17dcf241c`. Tracked diff SHA-256 `3145ed9aff3c373a…c1745`. Untracked: implementation-028 `d38d12db…`, NativeAgentRelationshipProcessTests `6197a388…`, ModeCrossCheckTests `16b3667e…`. Matches the assignment.
- Before/after identity: after the analysis the same command printed `head=93e3534 baseline=93e3534 files=7 diff=3145ed9aff3c untracked=3/5cc17dcf241c`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: none rechecked. [Analysis 006](analysis-006.md) ANALYSIS-500 was consulted for the owned-runtime note.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared policy vs provider adapter | Inspection of `NativeAdoption.CrossCheck`, `AttachmentCapture.CrossChecked`, `AgentModeClassifier` | One rule set in Core (`NativeAdoption.cs:27-45`). Relatives are classified by the unchanged `AgentModeClassifier.Classify` (`AttachmentCapture.cs:51-52`). The adapter supplies only `MayShareBackend` and the `provider + ".exe"` image. Both entry points go through `AttachmentCapture.Resolve` (`Program.cs:33`, `HookIngestor.cs:15`) | No duplicated classification and no copied per-provider policy. The gate that decides when to run the policy is duplicated in Hosts (ANALYSIS-1500) |
| Duplication with snapshot/ancestry code | Inspection of `ProcessProbe.Ancestors/Neighbors/Snapshot`, `AttachmentCapture.Capture`, `Program.cs` | `Snapshot()` is factored out properly and `Ancestors` behaves as before. The agent's direct parent is already computed in `Capture` (`AttachmentCapture.cs:68`) from snapshot 1, and `Neighbors` (`ProcessProbe.cs:144-149`) derives it again from a new snapshot | Moderate duplication (ANALYSIS-1501) |
| Cost per hook | Micro-benchmark: Release `bell-tower.dll` copied to scratch and loaded into pwsh 7.6.9 (.NET 10.0.14). 300 interleaved, warmed iterations of each call. 404 processes and about 8.6k threads; QA's suite was running in parallel (17 dotnet/testhost processes) | `Ancestors` median 20.61 ms (p95 25.74). `Neighbors` median 20.96 ms (p95 31.10). `CommandLine` median 0.04 ms (p95 0.06). The snapshot is the whole cost | Measured under load on a JIT host, not the NativeAOT ingress, so the numbers are relative. `tools/BellTower.Ingress.Bench` was not run because it needs a `scripts/package.ps1` package build, which the assignment excludes (ANALYSIS-1501) |
| Core-to-IO direction | Inspection | The new Core types (`RelatedProcess`, `ProcessRelations`) use only `ProcessIdentity` (Core) and strings. The IO type `ProcessNeighbors` is mapped to Core in Hosts. The `using BellTower.IO` in `NativeAdoption.cs:1` was already there (`ContentHash`) | No new drift. Minor style point, not a finding: `ProcessNeighbors` is a second top-level type in `ProcessProbe.cs`, against the "one top-level type per file" rule in development.md |
| Complexity | Inspection | `CrossCheck` is 18 lines with one deliberate asymmetry (R5): a backend with null relations becomes unknown, a client with null relations keeps its mode. `Neighbors` is 30 lines | Proportionate. `OrderBy` sorts the whole process table (`ProcessProbe.cs:152`) to find a few matches, which costs under 0.1 ms |
| Owned-runtime launcher decision | Inspection of `BellTower.psm1:54-114`, `CodexRuntimeHost.cs:15-162`, `CodexRuntimeProcess.cs:16`, `NativeAdoption.Apply/Reconcile`, `CodexNameService.cs:37-45`; `git show 2f1de7f:src/BellTower.Ingress/Program.cs` | ANALYSIS-1502 | Recommendation given; the decision is the orchestrator's |
| Mode label consumers | `git grep` in `src` for `AgentModes.SharedBackend` and `Mode ==` | Nothing outside classification checks for `shared-backend`. `AttachmentEvidence.cs:163` keeps a stored known mode when an unknown one arrives | No current consumer is affected; future constraint in ANALYSIS-1503 |
| Planning edits | Inspection of implementation-025 (the 2.4 decision), design Risks, location of the `docs/compatibility.md` matrix | The 2.4 decision covers additive fields inside `evidence` | ANALYSIS-1504 |
| Test duplication and runtime | `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ModeCrossCheckTests\|FullyQualifiedName~NativeAgentRelationshipProcessTests\|FullyQualifiedName~NativeAgentResolveProcessTests"`, TRX logger in scratch | 142/142 passed in 21 s (22.9 s wall). New real-process class: 50 runs, 21.3 s total (nested 32 runs 11.21 s, held 16 runs 7.80 s, mixed 2 runs 2.25 s). Unit class: 64 rows, about 0.4 s. 2.2 Resolve class: 28 runs, 8.9 s | Covers Claude and Codex rows plus the mixed row (ANALYSIS-1505). QA runs the full suite |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 | passed |
| Process hygiene | `Get-Process codex,claude,ping,bell-tower-ingress` after the tests | No stand-in, ping or ingress process left over. The only match was the user's own client process, which was not touched | passed |

## Findings

### ANALYSIS-1500: The decision to gather relations sits in Hosts, so Core's provider gate never runs in production

- Severity: suggestion
- Related task / requirement: 2.5; design decision 7; AGENTS.md (shared policy, thin adapters)
- Locations:
  - `src/BellTower/Hosts/AttachmentCapture.cs:44-57`: the gate at 46-47, the literal `true` at 56, children gathered at 54-55.
  - `src/BellTower/Core/NativeAdoption.cs:27-45`: the gate at 29-32; the `SharedBackend` rule at 36-37 uses only `Parent`.
- Observation:
  - `CrossChecked` repeats two pieces of Core policy: the provider gate (`MayShareBackend`) and the list of modes the rules judge (`SharedBackend`, `InteractiveClient`, `EmbeddedRuntime`). It then calls `CrossCheck(mode, true, …)`.
  - Core's `mayShareBackend` branch therefore runs only in unit tests, and `ModeCrossCheckTests` check a gate that production never reaches.
  - If a Core rule is later added for another mode, or for Claude once evidence exists, the Hosts filter returns first. The new rule would never run in production, and the unit rows would still pass.
  - Hosts also reads the identity and command line of every same-image child of a `SharedBackend` agent, although R1 only looks at the parent. This costs about 0.2 ms per child.
- Proposed improvement and benefit:
  - Let Core decide which relations it needs, and keep the IO work lazy. Either change the signature to `CrossCheck(string mode, bool mayShareBackend, Func<bool /*children*/, ProcessRelations?> relations)`, or add a Core predicate (`NativeAdoption.Needs(mode, mayShareBackend)` returning none, parent, or parent plus children) that `CrossChecked` calls before any IO.
  - Pass `AgentModeClassifier.MayShareBackend(provider)` instead of `true`.
  - The rule set then lives in one place, a Claude hook still does no extra work, and a backend hook skips the child reads.
- Behavior-preservation risks: none for the current rule table if the predicate mirrors the switch exactly. Keep the null-relations asymmetry (R5).
- Evidence type: source observation
- Verification / measurement plan: keep `ModeCrossCheckTests` and the relationship rows as they are. Add one mutation: remove the Hosts gate and confirm a test fails, or that the check no longer applies because the gate is gone.
- Relationship: new

### ANALYSIS-1501: Each Codex hook now takes another full process snapshot; ancestry, neighbors and envelope ancestors could share one

- Severity: suggestion
- Related task / requirement: 2.5; design Risks ("verify unchanged hook latency budgets"); task 6.3; `docs/hook-ingress.md` (300 ms adjusted-mean budget)
- Locations:
  - `src/BellTower/Hosts/AttachmentCapture.cs:63`: snapshot 1.
  - `src/BellTower/Hosts/AttachmentCapture.cs:68`: the direct parent's identity.
  - `src/BellTower/Hosts/AttachmentCapture.cs:88`: snapshot 2, taken whenever no `WindowsTerminal.exe` is in the hook's ancestry and a shell was found. That includes every VS Code hook.
  - `src/BellTower/Hosts/AttachmentCapture.cs:53` with `src/BellTower/IO/ProcessProbe.cs:137-167`: snapshot 3, new, Codex only.
  - `src/BellTower.Ingress/Program.cs:37`: snapshot 4, for the envelope ancestors.
- Observation:
  - Before DEV-028, a Codex ingress hook took 2 snapshots in Windows Terminal and 3 in VS Code. DEV-028 adds one more for every Codex hook in a client or backend mode, which covers every regular capture event, prompt and tool hooks included.
  - Under the parallel QA load, one snapshot costs about 20 ms median (p95 25-31 ms). The new call costs the same as `Ancestors`; command-line reads add about 0.04 ms each.
  - `Neighbors` derives the agent's direct parent again, though `Capture` already has it from snapshot 1.
  - When the shell came from ancestry, `Ancestors(shell.Pid)` walks a chain that is already in snapshot 1.
- Proposed improvement and benefit:
  - Take one snapshot per hook. For example, make `Snapshot()` return a small immutable process table with `Ancestors(pid)` and `Neighbors(agent, image)`, and pass it to `Capture`, `CrossChecked` and `Program.cs`.
  - This removes the DEV-028 snapshot and 1-2 older ones, about 20-60 ms per Codex hook at the measured load.
  - Ancestry, parent and children then all come from the same moment.
  - Filter children before sorting (`ProcessProbe.cs:152`).
- Behavior-preservation risks:
  - `Program.cs` currently takes its ancestors after `Resolve`. Reusing the earlier table loses at most a few milliseconds of freshness, and nothing depends on that order.
  - `VsCodeWindowOwnership` and `PrepareSessionCommand` keep calling `Ancestors(pid)` unchanged.
  - The NativeAOT ingress links the same file, so keep the new type simple.
- Evidence type: measured result for per-snapshot cost (relative, under load, JIT host); performance hypothesis for the end-to-end ingress change
- Verification / measurement plan: build a package (`scripts/package.ps1`) and run `tools/BellTower.Ingress.Bench`. Compare Codex native-ingress mean and p95 at `93e3534`, at DEV-028 and with one shared snapshot, using the trial-002 method in `docs/hook-ingress.md`. This fits task 6.3. Not done here because it needs a package build.
- Relationship: new

### ANALYSIS-1502: Owned-runtime decision: don't accept the launcher as a backend parent; `unknown` adds no harm, but the owned runtime has been unbound since 2.2

- Severity: warning. The problem predates DEV-028 (it came with task 2.2), and a decision is needed.
- Related task / requirement: 2.2, 2.5, 3.3; design decisions 1, 4 and 7; [analysis 006](analysis-006.md) ANALYSIS-500, whose behavior-preservation note says owned-runtime launches must stay dedicated
- Locations:
  - `src/BellTower/Integration/BellTower.psm1:54-58,97-114`
  - `src/BellTower/Hosts/CodexRuntimeProcess.cs:16` (the backend inherits the environment)
  - `src/BellTower/Sessions/CodexRuntimeHost.cs:75,111,139-140`
  - `src/BellTower/Core/CodexRuntime.cs:3`
  - `src/BellTower/Core/NativeAdoption.cs:63-91,139-151`
  - `src/BellTower/Sessions/CodexNameService.cs:37-45`
- Observation:
  - Topology:
    - A managed `Start-Codex` launch without `--no-daemon` runs the TUI as `codex --remote ws://127.0.0.1:<port> …` under the profile shell.
    - The hooks run in the owned `codex app-server`. This is the same hook-ancestry assumption implementation-028 uses.
    - For a native `codex.exe`, that backend's direct parent is the `bell-tower codex-runtime-host` manager. For npm installs it is `node.exe`; for script shims it is `powershell.exe`.
    - The backend inherits `BELLTOWER_ATTACHMENT`, `BELLTOWER_SHELL_PID` and `BELLTOWER_DEDICATED=1`.
  - Before 2.2 (`2f1de7f`), the inherited flag made these hooks dedicated: the session's process was the backend and its host was the launch shell.
  - At the baseline (after 2.2):
    - The mode is `shared-backend`, so `Apply` returns `Shared`. There is no process and no host, and the ID is a `SharedId` instead of the managed attachment ID.
    - `CodexNameService` needs `runtimes.Get(session.AttachmentId)` and `attachment.Host.Shell == runtime.Shell`, so it reports "unavailable: no verified dedicated runtime".
    - `CodexPermissionPump.cs:103-104` looks up the runtime the same way.
    - So managed owned-runtime Codex sessions appear to have lost native names, and probably the owned-runtime permission path, since 2.2.
    - No test covers this path; `git grep app-server tests` finds only classifier and relationship rows.
  - With DEV-028:
    - The mode becomes `unknown`. Because `Ownership == "managed"` and a process exists, `Apply` makes a provisional candidate.
    - `Reconcile` rejects it, because the backend is neither the recorded process nor a child of the launch shell. The result is the same `Shared` shape as the baseline; only the diagnostic label changes.
- Recommendation:
  1. Don't accept a launcher image (`bell-tower.exe`, `node.exe`, `powershell.exe`) as a backend parent in `CrossCheck`. An image name is not an identity, so any process with that name would pass. It would also gain nothing: `Apply` never makes a `shared-backend` direct, so only the label would change.
  2. If restoring owned-runtime binding is in scope (analysis 006 says the owned runtime should stay dedicated), keep `unknown` and add the proof to the provisional path that DEV-028 now enters:
     - In `Reconcile`, accept the candidate when the live `CodexRuntime` record for `captured.Id` has `Manager == pending.AgentParent` (same PID and creation time, process live) and its `Shell` is live.
     - Take the host shell from `record.Shell`, not from the inherited `BELLTOWER_SHELL_PID`. The environment variable then only locates the record; Bell Tower's own record is the proof (design decision 1).
     - Core stays pure if the consumer passes in the verified manager identity, as it passes `launchShell` today.
     - npm and shim installs, where the parent is not the manager, stay shared and fail closed unless a bounded "manager is a near ancestor" check is added and tested.
  3. If it is not in scope, record the 2.2 loss of owned-runtime native names in the capability matrix and diagnostics instead of leaving it implicit.
- Behavior-preservation risks:
  - The proof must never accept a backend whose manager record belongs to another attachment, or whose manager is dead or has a reused PID.
  - It must leave independently started shared backends alone (task 3.3).
  - Claude has no owned runtime, so this surface is Codex-only and does not affect parity. Claude managed launches are already proven through the profile shell.
- Evidence type: source observation. Pre-2.2 behavior read from `git show 2f1de7f`. The native-name loss is inferred from the code, not reproduced.
- Verification / measurement plan if option 2 is chosen:
  - Add a real-process row: a manager image, a backend `app-server` child, a written `CodexRuntime` record, and the hook. Expect a direct attachment under the managed ID with `Host.Shell == record.Shell`, and `CodexNameService` resolving the runtime.
  - Add negative rows: wrong manager PID or creation time, a dead manager, a record for another attachment, an npm-shaped parent. Expect `shared:unknown`.
  - Pair them with an unchanged Claude managed-launch row.
- Relationship: related to ANALYSIS-500 (analysis 006). New as a finding; the developer raised it as an open decision.

### ANALYSIS-1503: Many genuine shared backends now classify as `unknown`; 4.2 and 5.2 must not check for `Mode == shared-backend`

- Severity: suggestion (planning constraint)
- Related task / requirement: 2.5, 4.2, 5.2; design decision 3 (binder); decision 2 (the backend outlives its clients)
- Locations: `src/BellTower/Core/NativeAdoption.cs:36-37`; `src/BellTower/IO/ProcessProbe.cs:143-149`; `src/BellTower/Core/AttachmentEvidence.cs:163`
- Observation:
  - R1 requires a live same-image client as the parent. A default shared-mode daemon outlives the TUI that started it; decision 2 records threads that remained after both TUIs exited.
  - So every conversation that starts after the first client exits is classified `unknown`. An `app-server` started by an IDE extension or app is also `unknown`.
  - Today only the label changes: `Shared` has the same shape either way, and the merge keeps an earlier known mode for the same conversation.
  - `Neighbors` returns `Parent = null` in four different cases: the parent exited, it has another image, its PID was reused, or `Identify` failed. Diagnostics can't tell these apart.
- Proposed improvement and benefit:
  - Add to the 4.2 acceptance criteria: the binder selects shared conversations by ownership (`shared-runtime` plus provider) and by its own listener and connection evidence, never by `Mode == shared-backend`. A daemon whose launcher has exited is the normal case for the binder.
  - For 5.2, have `Neighbors` report the parent's state (`exited`, `other-image`, `reused`, `unreadable`). This costs nothing because the snapshot already holds the parent's PID entry, and it supplies the reason code in ANALYSIS-1504.
- Behavior-preservation risks: none if the policy result stays `unknown`. This changes reporting only, not what is accepted.
- Evidence type: source observation; topology from the design and evidence 009-011
- Verification / measurement plan: a 4.2 row where the backend's launching client has exited, which must still bind through the listener; a unit row for each parent state.
- Relationship: extends the developer's stated limit ("a genuine backend whose launching client already exited")

### ANALYSIS-1504: Proposed planning edits: the reason code is additive under the 2.4 decision; describe Claude as "not applicable", not "unverified"

- Severity: suggestion
- Related task / requirement: 2.4, 2.5, 5.2, 5.3, 6.6; AGENTS.md (keep the capability matrix honest)
- Locations: [implementation-025](../evidence/implementation-025.md) lines 7, 21 and 29; design Risks (the 2.4 bullet); the "Session identity capability matrix" in `docs/compatibility.md`; the "Limits and decisions needed" section of [implementation-028](../evidence/implementation-028.md)
- Observation:
  - The developer says a reason field "would change the persisted shape (task 2.4 gate)". But the 2.4 decision found that fields inside the already additive `evidence` object are never read by an older tray and need no gate. Task 2.3 added `evidence.terminal.*` on that basis.
  - The wire envelope uses default System.Text.Json member handling; there is no `UnmappedMemberHandling` setting anywhere in `src`.
  - So a rule code such as `evidence.modeCheck` falls under the same rule, as long as it is not a legacy field, carries no command-line text, and follows the existing mode merge (a stored known value wins).
  - On the Claude note: Claude has no shared backend, so the relationship rules have nothing to apply to. That is a provider fact the adapter supplies, not an unverified capability. The only retained Claude evidence is one `-p` capture on 2.1.310.
- Proposed improvement and benefit:
  - Put the reason code under 5.2, so diagnostics can say which rule fired, using the parent states from ANALYSIS-1503. Acceptance needs only the 2.4-style additive check: an older-consumer test in the style of `LegacySharedRecordsWithoutEvidenceKeepTheirBehaviorAndGainOnlyAdditiveEvidence`, not a new `attachmentHealth` or capture gate.
  - In the capability matrix (5.3, `docs/compatibility.md`), list the Claude cross-check as "not applicable: no shared backend known; verified shapes interactive and `-p` on 2.1.310".
  - Add a drift item to 6.6 that flags any new Claude daemon, server or remote subcommand in the help output, so "not applicable" gets rechecked instead of assumed.
- Behavior-preservation risks: the reason field must not affect policy or the mode merge. Keep it diagnostic only.
- Evidence type: source and document observation
- Verification / measurement plan: an older-consumer round-trip test for the new field; a doctor row for each rule code on Codex, and "not applicable" for Claude.
- Relationship: responds to the developer's proposed planning edits

### ANALYSIS-1505: Every relationship row runs under both entry points, and two Claude rows repeat 2.2 topology

- Severity: suggestion
- Related task / requirement: 2.2, 2.5
- Locations:
  - `tests/BellTower.Tests/Hosts/NativeAgentRelationshipProcessTests.cs:41-42` (Claude under Codex); lines 48 and 77 (every row run under both entries).
  - `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:165-193`: `AClaudeUnderCodexClaimsOnlyItsOwnProcess`, with outer arguments `""` and `app-server`.
- Observation:
  - The new real-process class costs 21.3 s of serial time: 50 runs at about 0.35-0.68 s each. Tests in one xUnit class run one after another.
  - Both entry points call the same `AttachmentCapture.Resolve`, and the 2.2 `Shapes` rows already check that. The relationship result doesn't depend on the entry point, so about half the cost (≈10.6 s) repeats the same check through the second entry.
  - The nested rows `("claude","codex","","")` and `("claude","codex","app-server","")` (4 runs, ≈1.4 s) repeat the 2.2 Claude-under-Codex topology. For Claude, `CrossChecked` returns before reading any relationships, so these rows only test classification again.
- Proposed improvement and benefit:
  - Run each relationship row under one entry point, alternating between the two.
  - Keep both entries for one representative row per rule (R1, R2, R3, Claude unchanged) and for the mixed row.
  - Drop the two Claude-under-Codex nested rows, or fold them into the 2.2 theory.
  - Keep the Claude-under-Claude nested and held rows as the parity rows.
  - This saves about 10 s of serial time without losing rule or parity coverage.
- Behavior-preservation risks: if an entry point stopped using `Resolve`, the 2.2 rows and the representative rows would still catch it.
- Evidence type: measured result (TRX durations, under parallel QA load)
- Verification / measurement plan: after trimming, rerun the mutation table in implementation-028. Every mutation must still be killed, by at least one test in each entry point.
- Relationship: new

## Re-verification

Not applicable (no recheck assigned).

## Assessment and limitations

- Covered:
  - Areas: the split between shared policy and adapter, duplication with process snapshot and ancestry code, per-hook cost (micro-measured), the Core/IO/Hosts direction, complexity, test duplication and runtime, the owned-runtime decision, and the proposed planning edits.
  - Clients: unit and real-process rows for both Claude and Codex, plus the mixed simultaneous row, passed (142/142). The owned-runtime analysis is Codex-only, since Claude has no owned runtime.
- Overall: DEV-028 implements one shared policy correctly and duplicates no classification. The findings:
  - ANALYSIS-1500: a gate placed in Hosts.
  - ANALYSIS-1501: snapshot cost that could be avoided.
  - ANALYSIS-1502: an owned-runtime regression that predates DEV-028 (task 2.2) and bears on the open decision. Needs an orchestrator decision.
  - ANALYSIS-1503 and 1504: planning constraints.
  - None blocks task 2.5 on its own.
- Decision recommendation: don't accept a launcher parent in `CrossCheck`; `unknown` causes no harm compared with the baseline. If owned-runtime binding should be restored, prove it in `Reconcile` from the `CodexRuntime` record: the manager's PID and creation time must match the backend's parent, and the record's shell must be live.
- Not performed: the packaged ingress benchmark (needs a package build); any real `claude` or `codex` launch; live-instance checks; reproducing the 2.2 owned-runtime native-name loss.
- Writes: none in the repository, because the report write was refused. Disposable data only in the session scratch folder (copied DLLs, benchmark scripts, TRX). No source, test or planning edits. The untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/` were not touched.
