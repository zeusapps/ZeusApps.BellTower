# Analysis report: validate-next-release-field-acceptance / ANALYSIS-015

> Saved verbatim by the orchestrator from the ANALYSIS-015 hand-back. Reviewer: code-analyzer subagent; the client refused its report write ("Subagents should return findings as text"). Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

## Review target

- Reviewer / role: ANALYSIS-015 code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (the subagent cannot see the version), delegated subagent, auto mode, model `claude-opus-5-5`
- Date: 2026-11-26T19:51Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance`, ANALYSIS-015. This is the final source-only re-review of task 2.4, fix cycle 2. Reserved IDs ANALYSIS-280 to ANALYSIS-299; used ANALYSIS-280.
- Task and requirement references: task 2.4 ([tasks.md](../tasks.md)); [implementation-012](../evidence/implementation-012.md), [harness-2.4](../evidence/harness-2.4.md)
- Scope:
  - `tools/BellTower.FieldAcceptance/Harness/{SandboxWindowGuard,LossPredicates,AgentSession,FakeAgent,Instance,StateView,OwnedRuntimeRecord,ProviderModel}.cs`;
  - `Program.cs`, `Scenarios/FaultGroupRun.cs`;
  - `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs`;
  - `docs/distribution-validation.md`.
- Neighboring paths read:
  - product `src/BellTower/IO/ProcessTable.cs`, `Hosts/AttachmentCapture.cs`, `Core/NativeAdoption.cs:95-131`, `Providers/AgentModeClassifier.cs`, `Tray/TrayApplication.cs`;
  - harness `Isolation/ProductionGuard.cs:61-104`;
  - `v0.1.9` `src/BellTower.Ingress/Program.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/AttachmentStore.cs`.
- Baseline commit: 02d4f56
- Implementation identity before: `head=02d4f56 baseline=02d4f56 files=100 diff=bc522ec0901d untracked=82/153b37620856`. This matches the assigned target.
- Before/after identity: identical after the analysis. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-014](analysis-014.md) (ANALYSIS-260 to ANALYSIS-266), [qa-014](qa-014.md) (QA-260)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 11 changed .cs files>`, plus a count of CR bytes | Exit 2. Every changed file fails with `ENDOFLINE` on every line. All 11 contain 0 CR bytes, while untouched neighbors are CRLF. | failed (ANALYSIS-260 still failing) |
| Null-process ancestry | Read `AgentSession.StartDetached`, `FakeAgent.SpawnDetached`, `ProcessTable.Ancestors`, `AttachmentCapture.Capture`, `ProductionGuard.BuildEnvironment` | See QA-260 below | resolved, with a residual (known limitations) |
| Window guard | Read `SandboxWindowGuard.cs`, `Instance.cs:40,272`, `FaultGroupRun.cs:857-860` | See ANALYSIS-280 | defect |
| Loss predicates | Read `LossPredicates.cs`, the call sites `FaultGroupRun.cs:369,459,480,733` and the fixture tests `FaultInjectionTests.cs:361-384` | Exact ids, and direction-specific | resolved |
| Stable state | Read `FaultGroupRun.cs:839-854`, `StableState.RemainingQuiet`, `Send` (`:206`) | One comparison, no retry | resolved |
| 0.1.9 vs 0.1.11 classification | `git show v0.1.9:...` for the ingress `Program.cs`, `Hosts/AttachmentCapture.cs` and `IO/AttachmentStore.cs`; read candidate `NativeAdoption.cs:101,106,130` and `AgentModeClassifier` | Correct by source; one line reference is imprecise | resolved |
| Harness run | not run (assignment) | - | not performed |

## Findings

### ANALYSIS-280: The window guard's ownership cache is keyed by bare PID and never invalidated, so after PID reuse it can hide a window of a non-sandbox process

- Severity: warning. It affects the maintainer's desktop and breaks the guard's own contract: it must never touch a non-sandbox window.
- Related task / requirement: 2.4, "no visible sandbox UI"; the safety rules in project facts
- Locations: `tools/BellTower.FieldAcceptance/Harness/SandboxWindowGuard.cs:17, 81-94, 96-107`; `Harness/Instance.cs:40` (the guard lives as long as the instance, which spans a whole group run)
- Observation:
  - `Owned(pid)` caches `ImageUnderInstall(pid)` in `Dictionary<int,bool> owned` the first time a PID owns a visible top-level window. Entries are never removed, and they are not tied to the process's start time.
  - A group restarts the sandbox tray several times: at the upgrade coordinator, at the user Exit and 0.1.9 start in the rollback, and at the reupgrade selection. Each tray PID is cached as `true`. After that tray exits, Windows can give its PID to any process on the machine. Examples are the user's own Bell Tower tray when it restarts, or any app the user opens.
  - If the new owner shows a visible top-level window while the group is still running (several to tens of minutes), the guard calls `ShowWindow(..., SW_HIDE)` on it every 100 ms. Nothing re-shows the window afterwards, so the user's window stays hidden after the run.
  - The reverse case: a stale `false` for a reused PID leaves a sandbox window visible, and `VisibleNow()` does not count it. `final/no-visible-sandbox-ui` then passes falsely.
  - Install-root matching is otherwise correct. The prefix carries a trailing separator, the image is read from `MainModule`, and unreadable processes give `false`.
- Proposed improvement and benefit:
  - For a positive entry, keep the `Process` object open. An open handle prevents the PID from being reused. Drop the entry once `HasExited` is true.
  - Alternatively, key the cache by (PID, start time) and re-validate on every hit.
  - Do not cache negative results across polls, or key them the same way.
  - Either way, the guard can only ever touch windows of live sandbox-image processes.
- Behavior-preservation risks: none to the product. The guard's poll cost rises slightly.
- Evidence type: source observation. The reuse window is a hypothesis; no machine reproduction was done.
- Verification / measurement plan:
  - A fixture test fakes a cache hit for a PID whose process has since been replaced and asserts it is not hidden. This needs a seam for the image lookup.
  - Alternatively, a review check that every positive entry holds an open process handle.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-260 | `bc522ec0901d` | Verify-only format exits 2 with `ENDOFLINE` on all 11 changed files: `AgentSession`, `FakeAgent`, `Instance`, `LossPredicates`, `OwnedRuntimeRecord`, `ProviderModel`, `SandboxWindowGuard`, `StateView`, `Program`, `FaultGroupRun` and `FaultInjectionTests`. All have 0 CR bytes. `ProviderModel.cs` (mtime 12:27Z) and the two other files from the original seven (`CaptureObserver.cs`, `PackagedHookBenchmarkScenario.cs`) are still LF too. The claim in implementation-012 ("CRLF again ... verified with `--verify-no-changes`") is wrong. | still failing |
| ANALYSIS-261 | same | `FaultGroupRun.cs:843-854`: the check sleeps `RemainingQuiet(lastHookAt, 1 min, 15 s)` (`lastHookAt` is stamped on every `Send`, `:206`), then compares once over 3 s, with no loop. Any change fails the check. A slower periodic change can escape a 3 s window, but that is inherent to a single comparison, not masking. | verified |
| ANALYSIS-262 | same | `LossPredicates.FreshIdLoss` (`:18-20`) requires `Waits == 0`, at least 4 journal events, `toolu_plain_1` in the journal, the event's own id non-empty and different from the session's, and more than one id. It is used at `FaultGroupRun.cs:480`. The fixture tests at `FaultInjectionTests.cs:375-384` include the same-id negative. | verified |
| ANALYSIS-263 | same | `IdLoss` (`:11-12`) requires `AttachmentId == sessionId` and `AttachmentOfOperation(op) == eventId`, where `AttachmentOfOperation` reads that operation's own event and not `events[^1]`. The pairs: plain gets (shared, own `codex-<pid>-<ticks>`, `:132,136`); owned-runtime rollback gets (att-, shared); heal gets (shared, att-, `:459`). Rollback judges each `rbdown` id's own event (`:733`). The reversed-direction fixture is at `FaultInjectionTests.cs:368`. | verified |
| ANALYSIS-264 | same | `wait-fresh-*` conversations start after the setup rerun (`:164-168, 335, 340-357`). The manager is a dedicated `--manager` helper (`FakeAgent.RunManager`). The record is written before the first hook (`AgentSession.cs:165-169`). `app-server` classifies as `SharedBackend` (`AgentModeClassifier.cs:93-95`), so registration under the managed id needs the owned-runtime verification and is not a direct shortcut. The evidence rows `upgrade/fresh-owned-runtime-registered` pass. There is still no negative control (known). | verified (negative control still absent) |
| ANALYSIS-265 | same | `v0.1.9:src/BellTower.Ingress/Program.cs:33` keeps the managed id when `BELLTOWER_DEDICATED=1`. In `v0.1.9:Hosts/AttachmentCapture.cs:30-31` the environment id wins without a process. In `v0.1.9:IO/AttachmentStore.cs:86`, `Adopt` fills `Process` from a later live hook. In the candidate, `NativeAdoption.cs:101` makes Unknown direct only when the backend cannot be shared, and `:106` requires a process; everything else goes to `Shared` at `:130`. The candidate does not read `BELLTOWER_DEDICATED` in `Hosts`, `Core` or `Ingress`. Conclusion: the Codex null-process later-hook loss is new in 0.1.11, and the plain Claude fresh id is pre-existing (`v0.1.9 :31`). The docs' classification and the expected-failure list are correct. The only imprecision: the docs cite `v0.1.9 Hosts/AttachmentCapture.cs:41-44` for the environment-id rule, but that rule is at `:30-31`; `:44` is the ownership line (known limitations). | verified |
| ANALYSIS-266 | same | `FaultGroupRun.cs:723` waits only on kinds with neither documented loss, then on journal presence (`:728`) | verified |
| QA-260 | same (source only, no run) | An unidentified agent is started through `--spawn-detached`, which exits at once (`AgentSession.cs:123-145`, `FakeAgent.cs:30-39`). `ProcessTable.Ancestors` stops at a parent PID missing from the snapshot, so the product's walk above the agent is empty whatever launched the harness. The child environment is an allowlist (`ProductionGuard.cs:61-104`), so host `BELLTOWER_*`, `WT_SESSION` and `TERM_PROGRAM` do not leak. `AgentSession.Start` throws on a provider ancestor (`:170-174`), and `candidate/null-process-ancestry` reports the agents' and the harness's ancestry (`FaultGroupRun.cs:490-500`). The evidence rows show "empty (dead parent)" under a `claude.exe` host. The outcome is now independent of the host tree, except for PID reuse (known limitations). | verified, with a residual |

## Known limitations (harness fidelity, not findings)

- **Spawner PID reuse.** The ancestry assertion is a snapshot taken at start (`AgentSession.cs:164`), and the product's `Ancestors` walk does not check creation times. If the dead spawner's PID is reused by a provider-image process during the few seconds of an unidentified agent's own hooks, the walk could follow it. That would most likely produce a false failure. For `ghostplain` it could instead produce a misattributed `FreshIdLoss` pass, because the predicate does not check that the foreign id has the random (32-hex) shape.
- **Evidence predates the final guard source.** `harness-2.4.md` (run 17:55Z to 18:22Z) is older than the final `SandboxWindowGuard.cs` (19:32Z). The two-read `VisibleNow` form was run only in a Claude-only group (55 of 55), which is not recorded in that evidence file.
- **Remaining sandbox UI on the desktop.** The guard covers windows only. The sandbox trays' `NotifyIcon` icons still appear in the user's notification area. The strip flashes for up to 100 ms each time; the guard hid 611, 1298 and 2677 shows in the three groups.
- **Guard can leak on a failed create.** The guard starts in the `Instance` constructor (`Instance.cs:40`). If `Instance.Create` throws after that (for example in `InstallFakeProviders`), the guard is never disposed and its thread runs until the process exits, which combines with ANALYSIS-280.
- **Owned-runtime record.** There is no negative control for the owned-runtime record. The `AgentSession.Start` and `Instance.Run` swap-guard call sites have no unit tests (the developer lists both).

## Assessment and limitations

- Of the eight assigned findings:
  - ANALYSIS-261 to ANALYSIS-266 are resolved by source;
  - QA-260 is resolved by source (no run in this review);
  - ANALYSIS-260 is still failing: all changed files are LF, and implementation-012's format claim is wrong.
- One new defect, ANALYSIS-280, affects the maintainer's machine.
- None of these makes a 2.4 check verdict in the recorded evidence wrong. ANALYSIS-280 could, in a future run, hide a user window or let a sandbox window pass `final/no-visible-sandbox-ui` unseen.
- Not performed: harness runs, build and tests (source-only assignment). No code was edited, and nothing was written: the report write was refused.
