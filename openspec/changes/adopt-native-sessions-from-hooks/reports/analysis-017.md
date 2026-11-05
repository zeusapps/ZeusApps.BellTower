<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-017 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-017 (recheck of DEV-029, task 2.5)

## Review target

- Reviewer / role: Claude Code subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent). The subagent cannot see the client version. Default permission mode. Model `claude-opus-5-5`.
- Date: 2026-11-05 (UTC). The analysis started on 2026-11-04 and resumed after a rate-limit pause.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-017
- Task and requirement references: task 2.5 ([tasks.md](../tasks.md)); design decisions 1, 3 and 7; developer evidence [implementation-029](../evidence/implementation-029.md) and [implementation-028](../evidence/implementation-028.md); the ANALYSIS-1502 triage decision in [findings](findings.md): restore the binding in `Reconcile` from the `CodexRuntime` record, never from a launcher image name. The 5.2 reason code is deferred.
- Scope: the 17 assigned paths (the 8 source and 7 test files plus evidence 028 and 029).
- Neighboring code read: `src/BellTower/Core/CodexRuntime.cs`, `src/BellTower/IO/CodexRuntimeStore.cs`, `src/BellTower/Sessions/CodexRuntimeHost.cs`, `src/BellTower/Sessions/CodexNameService.cs`, `src/BellTower/Sessions/CodexPermissionPump.cs`, `src/BellTower/Integration/BellTower.psm1`, `src/BellTower/Providers/HookIngestor.cs`, `src/BellTower/Cli/PrepareSessionCommand.cs`, `src/BellTower/Hosts/VsCodeWindowOwnership.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, `tests/BellTower.Tests/Testing/*`.
- Baseline commit: `93e3534`
- Implementation identity (before): `head=93e3534 baseline=93e3534 files=17 diff=19d91ea1ab74 untracked=9/f5c9b69022f5`.
  - Tracked diff SHA-256: `19d91ea1ab74e5494c9f13023e589b010bf1216148c4b11c6ab81e828d3c1dbd`.
  - Untracked files: `ProcessTable.cs` `2d32cf05…`, `StandIns.cs` `5c258104…`, `NativeOwnedRuntimeProcessTests.cs` `1b073079…`, `OwnedRuntimeReconcileTests.cs` `ebf4d95e…`, `ProcessTableTests.cs` `854b5f0a…`, `ModeCrossCheckTests.cs` `016267b8…`, `NativeAgentRelationshipProcessTests.cs` `df3ba41e…`, implementation-028 `d38d12db…`, implementation-029 `37f855b7…`.
  - This matches the assignment.
- Before/after identity: after the analysis the same command printed `head=93e3534 baseline=93e3534 files=17 diff=19d91ea1ab74 untracked=9/f5c9b69022f5`. The two outputs are identical, so nothing changed during the analysis.
- Prior reports / rechecked findings: [analysis 016](analysis-016.md) ANALYSIS-1500, 1501, 1502 and 1505. [QA 023](qa-023.md) was read for context.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Snapshot count per hook (Codex and Claude) | `git grep` of every `ProcessTable.Take`, `ProcessProbe.Ancestors` and `.Ancestors(` call in `src`, then the hook call paths traced by hand | Ingress: one `Take` (`Program.cs:34`) serves `Capture` (`AttachmentCapture.cs:66,91`), `Neighbors` (`:57`) and the envelope ancestors (`Program.cs:39`). The `event` path (`HookIngestor.cs:15`) also takes one, through the `Resolve` overload (`AttachmentCapture.cs:15`). The other snapshot callers (`PrepareSessionCommand`, `VsCodeWindowOwnership`, `AgentLauncher` via `Capture`) are not on the hook path | 1 snapshot per hook for both providers, down from up to 4 for a Codex hook at DEV-028. The developer notes that no test pins this property |
| Cost per hook (micro-benchmark, rerun of the ANALYSIS-016 method) | Release `bell-tower.dll` and `BellTower.Protocol.dll` copied to scratch and loaded by reflection into a child pwsh on .NET 10.0.14. 300 warmed iterations with about 380 processes, while QA ran in parallel (17 dotnet/testhost processes) | `ProcessTable.Take` median 18.16 ms (p95 23.52). `table.Ancestors` 0.03 ms. `table.Neighbors` with children 0.12 ms (p95 0.30). `ProcessProbe.Ancestors`, which takes a fresh snapshot, 18.07 ms. The current hook shape (1 table, 2 ancestry walks, neighbors, envelope walk) took a median of 18.63 ms (p95 24.29). The DEV-028 shape (4 snapshots) took 73.54 ms (p95 98.18) | About 55 ms (75%) less per Codex hook at this load. The figures are relative: they come from a JIT host under load, not from the packaged NativeAOT ingress. The task 6.3 packaged benchmark was not run |
| NativeAOT compatibility of `ProcessTable` | `dotnet publish` with `PublishAot` of a scratch copy of the ingress project and its linked sources. vswhere was added to PATH for that process only, as `scripts/package.ps1` does. The repository's bin and obj folders were not touched | ILC and link succeeded with 0 warnings (`TreatWarningsAsErrors` is on); the exe is 3.4 MB. A smoke run of the AOT exe with a disposable `--data-dir` under a `codex.exe app-server` stand-in returned `{}`. With a pwsh parent the capture was `mode=unknown`, `ownership=shared-runtime`; under a `codex.exe` client stand-in it was `mode=shared-backend`. The envelope ancestors came from the same table | Passed. The P/Invoke surface is the same Toolhelp code moved out of `ProcessProbe`, so nothing new was added to the AOT surface |
| Duplication between `ProcessTable` and `ProcessProbe` | Inspection of the diff | The Toolhelp struct, the P/Invokes and the ancestry walk moved into `ProcessTable`. `ProcessProbe.Ancestors` is now a one-line delegate (`ProcessProbe.cs:109`). The only repeated declaration is a private `CloseHandle` P/Invoke in each class. `ProcessNeighbors` is gone and `NeighborSet` is nested, which resolves the one-top-level-type note in analysis 016 | No duplication worth acting on |
| Core purity of the owned-runtime proof | Inspection of `NativeAdoption.cs`, `AttachmentStore.cs:43-45`, `CodexRuntime.cs` | `Reconcile` gets an `OwnedRuntime` value and a `live` delegate and does no IO. The record is read only by `AttachmentStore` (IO), and only for a provisional capture. No launcher image name appears in Core or in `CrossCheck`. `NativeAdoption.cs:1 using BellTower.IO` predates this work (`ContentHash`). `OwnedRuntime` is a three-field, provider-neutral projection of the Core `CodexRuntime` record; it keeps provider naming out of shared policy, which is acceptable | Pure. Matches the triage decision |
| Complexity of `Reconcile` | Inspection (`NativeAdoption.cs:166-212`) | The provisional branch now has three proofs: the recorded launch, the profile shell and the owned runtime. The owned proof returns early, behind `!recordedLaunch && !shell` so the existing proofs keep their result shape. The method grew by about 12 lines. The `<summary>` at lines 160-165 still describes only the recorded-launch proof (the inline comment at 172-175 is current) | Proportionate. The stale summary does not contradict the code; this is noted, not a finding |
| Owned-runtime flow through the store | Inspection of `AttachmentStore.Adopt` and `CodexRuntimeHost.cs:18,54-57,139-140` | On first adoption a prepared presentation shell overrides `Host.Shell` (`AttachmentStore.cs:53-55`). Repeated hooks pass through `recordedLaunch` and keep `previous.Host` (`:85-91`). In production the record's `Shell` and the inherited `BELLTOWER_SHELL_PID` name the same process, because the runtime host reads that same variable | Consistent in production. The real-process rows cover only the first hook with differing shells. The repeated hook is covered at policy level only (`ARepeatedHookOfTheSameBackendStaysDirect`, which asserts the ID) |
| `StandIns` versus the existing Testing helpers | Inspection of `Testing/*` and every caller | `StandIns.Find` and `KillUnder` (orphan cleanup for the dead-manager row) cover cases that `DisposableSleeper` (a single ping process), `BuiltApplication` (paths) and `NativeFixtures` (payloads) do not. The stand-in image, hook and launch helpers stay as `internal` statics on `NativeAgentResolveProcessTests` and are used by two other classes. `ProcessTableTests.Image`, three 25 s flag-wait loops and three environment-scrub loops repeat the same pattern | ANALYSIS-1602 |
| Test runtime | `dotnet test … --no-build --no-restore` with TRX: first the 6 focused classes together, then three classes on their own | Together: 163/163 passed (40 s, 43.5 s wall). Alone: relationship class 29 runs, 11.74 s serial (mean 0.40 s). `NativeOwnedRuntimeProcessTests` 15 runs, 24.56 s (mean 1.64 s; 40.6 s when run with the other classes). `ProcessTableTests` 7 runs, 3.14 s. `OwnedRuntimeReconcileTests` 10 runs, 0.02 s. `ModeCrossCheckTests` 74 runs, 0.85 s. Scanning `MainModule` of every process (`KillUnder`) costs about 80-90 ms | ANALYSIS-1601 |
| N5: PID-order assertion | Inspection of `ProcessTableTests.cs:134` and `ProcessTable.cs:65-80`, and of how `CrossCheck` uses the result | ANALYSIS-1600 | Deterministic, so not flaky, but it pins a property no consumer observes |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 | Passed (whole solution, dirty tree) |
| Process hygiene | `Get-Process` filtered by temp and scratch paths, plus `ping` and `bell-tower-ingress` | Nothing from my scratch directory is running. One testhost under a temp path belonged to another run and exited on its own; it was not touched. No `ping` or ingress processes remain. The user's own `codex` process was not touched | Passed |

## Findings

### ANALYSIS-1600: The child sort and the PID-order assertion pin a detail that no consumer observes (N5)

- Severity: suggestion
- Related task / requirement: 2.5; QA-2300
- Locations: `src/BellTower/IO/ProcessTable.cs:65-80` (`.Order()` and its "listed set is stable" comment); `tests/BellTower.Tests/IO/ProcessTableTests.cs:134`
- Observation:
  - The assertion is not flaky. `Neighbors` sorts matching PIDs before listing them, so the order of the listed children does not depend on Toolhelp or `Dictionary` enumeration order.
  - The order also has no effect on the result:
    - At or below the limit, every valid child is listed, in any order.
    - Above it, `Complete` is false. `CrossCheck` then returns `unknown` without reading the children (`!found.Complete || …`).
    - `Complete` depends only on how many children are valid, never on their order. No other code reads `Children`.
  - So the sort and the assertion pin an unobservable detail. The "Neighbors sort removed" mutation in implementation-029 is killed only by that assertion. The kill depends on Toolhelp not happening to enumerate the children in ascending PID order, so it is likely but not guaranteed.
- Proposed improvement and benefit: either drop the sort and line 134, keeping the count, `Complete` and membership assertions; or keep the sort and change the comment to say the order is for deterministic diagnostics, for the case where task 5.2 lists children. Either way the test then checks only the contract.
- Behavior-preservation risks: none for policy. If 5.2 lists children in a reason code, keep the sort for stable output.
- Evidence type: source observation
- Verification / measurement plan: rerun `ProcessTableTests` and `ModeCrossCheckTests`. Every guard mutation in implementation-029 except the sort mutation must still be killed.
- Relationship: answers the orchestrator's N5 note

### ANALYSIS-1601: The owned-runtime process rows cost about 4 times a relationship row, mostly in a 1-second polling loop

- Severity: suggestion
- Related task / requirement: 2.5 tests; ANALYSIS-1505, which recovered about 9.6 s of the same kind of cost
- Locations: `tests/BellTower.Tests/Hosts/NativeOwnedRuntimeProcessTests.cs:95-101` (the backend's `ping -n 2` go-flag loop), `:236` (`KillUnder`)
- Observation:
  - The class takes 24.56 s for 15 rows when run alone (mean 1.64 s), and 40.6 s alongside the other focused classes under QA load. A relationship row takes 0.40 s, and the stand-in startup is similar.
  - The backend checks the go flag about once a second (`ping -n 2`), so each row probably waits up to 1 s (about 0.5 s on average) after the test writes the flag.
  - `KillUnder` scans every process's `MainModule`, which measured about 80-90 ms with one outlier of 0.87 s.
- Proposed improvement and benefit:
  - Poll with `ping -n 1 127.0.0.1` (one stand-in process start per check, tens of milliseconds) instead of `ping -n 2`.
  - Hypothesis: this saves about 0.5 s per row, roughly 7 s serial.
  - Leave `KillUnder` as it is; the dead-manager row needs it.
- Behavior-preservation risks: none for the product. The loop polls a little faster, but the 25 s deadline is unchanged.
- Evidence type: measured class and row durations from TRX. The share spent polling is a hypothesis.
- Verification / measurement plan: compare TRX runs of `NativeOwnedRuntimeProcessTests` alone before and after the change. All 15 rows must still pass, and the implementation-029 mutations that these rows kill must still be killed.
- Relationship: new; requested regression check (test runtime)

### ANALYSIS-1602: Stand-in process helpers are split between a test class and `Testing/StandIns`

- Severity: suggestion
- Related task / requirement: 2.5 tests
- Locations:
  - `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:49-115`: `Run`, `CopyImage`, `Hook`, `Under`, `Launch` and `AssertRan`, made `internal` and called 29 times from `NativeAgentRelationshipProcessTests` and `NativeOwnedRuntimeProcessTests`.
  - `tests/BellTower.Tests/Testing/StandIns.cs`.
  - `tests/BellTower.Tests/IO/ProcessTableTests.cs:23-29,52-56`.
  - `NativeOwnedRuntimeProcessTests.cs:240-248`.
  - `NativeAgentRelationshipProcessTests.cs:232,244`.
  - `NativeAgentResolveProcessTests.cs:82`.
- Observation:
  - `StandIns` does not duplicate the existing helpers. `DisposableSleeper` is a single ping process; `BuiltApplication` and `NativeFixtures` cover other concerns.
  - The stand-in toolkit is spread across three places:
    - the image, hook and launch helpers on a test class;
    - find and kill in `Testing`;
    - local copies in other classes. `ProcessTableTests.Image` copies `CopyImage` with a custom name, the 25 s flag-wait loop appears three times, and the `BELLTOWER_`/`WT_SESSION`/`TERM_PROGRAM` environment scrub appears three times.
- Proposed improvement and benefit:
  - Move `CopyImage` (with an optional image name), `Under`, `Hook`, a `WaitFor(path)` helper and an environment-scrub helper into `Testing/StandIns`.
  - Point the three classes at them.
  - Stand-in conventions then live in one place: the cmd.exe quoting rule, the environment that must not leak, and the deadlines.
- Behavior-preservation risks: tests only. Keep the 25 s deadlines and the kill-the-whole-tree guarantees.
- Evidence type: source observation
- Verification / measurement plan: run the four process test classes. Counts and pass results must be unchanged.
- Relationship: new; requested regression check (StandIns versus the Testing helpers). Low priority; can be batched with other test work.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1500 | `head=93e3534 … diff=19d91ea1ab74 untracked=9/f5c9b69022f5` | `Needs` (`NativeAdoption.cs:36`) decides which relationships each mode needs. `CrossCheck` (`:52`) calls the delegate at most once, only when needed, and keeps the R5 asymmetry. Hosts passes `AgentModeClassifier.MayShareBackend(provider)` and reads children only when `Relation.Children` is requested (`AttachmentCapture.cs:55-58`). A Claude hook reads nothing; a shared backend reads only the parent. `ModeCrossCheckTests` has the `Needs` table and the delegate-request rows for both providers (74/74 passed). Residual note: Hosts still returns the mode without calling Core when the agent has no identity (`AttachmentCapture.cs:50`). In that case a Codex `shared-backend` keeps its label instead of failing to `unknown` under R5. Only the label differs (`Apply` returns `Shared` either way). The case is practically unreachable, because a command line read for the agent implies its identity was readable, barring an exit race | verified (label-only residual noted; no action needed unless 5.2 reports the mode) |
| ANALYSIS-1501 | same | Call sites traced: one `ProcessTable` per hook on both the ingress and `event` paths. Children are filtered before sorting. Micro-benchmark: the hook shape costs a median of 18.6 ms, against 73.5 ms for the DEV-028 four-snapshot shape at the same load. The AOT publish succeeded with 0 warnings, and the AOT exe produced correct R1 results through `ProcessTable` | verified. The packaged end-to-end latency stays with task 6.3. No test pins the single-snapshot property |
| ANALYSIS-1502 | same | Matches the triage decision. The proof sits in `Reconcile` (`NativeAdoption.cs:181-188`): the record must be for the captured ID, `AgentParent == record.Manager` (PID and creation time), and both the manager and the record's shell must be live. The host shell comes from the record (`ShellSource = "launch-record"`). The proof is only tried when no other proof applies and any previous record names the same agent. No launcher image is accepted in `CrossCheck`, and the backend stays `unknown`. Core stays pure; the record is read in IO, only for provisional captures. `CodexNameService` and `CodexPermissionPump` look up the runtime with the same predicate (`runtimes.Get` plus `Host.Shell == runtime.Shell`), and the record's shell is the same process as `BELLTOWER_SHELL_PID` (`CodexRuntimeHost.cs:54-57,139-140`). `NativeOwnedRuntimeProcessTests` (15/15) and `OwnedRuntimeReconcileTests` (10/10) passed. They cover the positive row, missing record, wrong PID, wrong creation time, dead manager, dead shell, record for another attachment, npm- and shim-shaped parents, a client-started backend, and Claude rows with and without a record. Claude has no owned runtime, so the surface is Codex-only and parity is unaffected | verified (design). Limits: the permission pump is checked through its predicate, not run. Repeated hooks through the store are covered at policy level only. npm and shim installs stay shared, as documented |
| ANALYSIS-1505 | same | Rows counted in the data: 17 nested runs (14 rows, 3 under both entries), 10 held runs (8 rows, 2 under both) and 2 mixed, 29 in total. Implementation-029 says 28, an off-by-one in the evidence text only. Both entries still run R1 (nested row 0), R2 (nested row 5), R3 (held row 0), the Claude nested and held rows, and the mixed row. The two Claude-under-Codex nested rows are gone and remain covered by `NativeAgentResolveProcessTests`. Measured with the class alone: 11.74 s serial, against 21.3 s for 50 runs in analysis 016, about 9.6 s (45%) less | verified |

## Assessment and limitations

- Covered:
  - Re-verified ANALYSIS-1500, 1501, 1502 and 1505.
  - Regression checks: duplication between `ProcessTable` and `ProcessProbe`, NativeAOT compatibility (the publish and an AOT smoke run with Codex stand-ins), Core purity of the owned-runtime proof, `Reconcile` complexity, `StandIns` against the Testing helpers, test runtime, and the N5 note.
  - Clients: the focused unit and real-process rows for Claude and Codex, plus the mixed simultaneous row, passed (163/163). The owned-runtime proof is Codex-only by provider fact.
- Overall: DEV-029 resolves the four rechecked findings without architectural drift or new AOT risk.
  - New findings: three test-only suggestions. ANALYSIS-1600 is the N5 note, ANALYSIS-1601 is test runtime, ANALYSIS-1602 is helper placement.
  - None blocks task 2.5.
  - One evidence text correction: there are 29 relationship runs, not 28.
- Not performed:
  - the packaged ingress benchmark (task 6.3);
  - an end-to-end latency measurement of the AOT exe against a baseline AOT build;
  - mutation reruns (QA's domain; the implementation-029 table was read, not reproduced);
  - real `claude` or `codex` launches, live-instance checks, and running the permission pump.
- Writes: none in the repository, because the report write was refused. Disposable data stayed in the session scratch folder: copied DLLs, the benchmark script, TRX files, the scratch AOT project copy and publish output, and smoke data directories. Source, tests, planning, the repository's bin and obj folders, and the untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/` were not touched. Nothing was committed.
