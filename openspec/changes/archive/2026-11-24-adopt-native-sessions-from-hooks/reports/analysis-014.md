<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-014 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-014 (recheck of DEV-026)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent); version not queried, because the assignment forbids launching `claude`; auto mode; `claude-opus-5-5`
- Date: 2026-11-04 (about 17:10 UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-014 (recheck)
- Task and requirement references:
  - Tasks 2.3 and 2.4, with 3.x and 4.2 as the future producers of the seam ([tasks](../tasks.md))
  - [design](../design.md) decisions 2, 3 and 4 and the Risks section
  - [implementation 026](../evidence/implementation-026.md), [implementation 025](../evidence/implementation-025.md)
  - [Analysis 013](analysis-013.md), [QA 020](qa-020.md), triage rows ANALYSIS-1200 to 1207 in [findings](findings.md)
- Scope:
  - `src/BellTower/Core/AttachmentHealth.cs` (moved from `src/BellTower/IO/AttachmentHealth.cs`, deleted)
  - `src/BellTower/Core/ConversationLifecycle.cs`, `src/BellTower/Core/AttachmentEvidence.cs`
  - `src/BellTower/IO/StateStore.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/IO/ProcessLiveness.cs`
  - `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs`, `tests/BellTower.Tests/Providers/AttachmentEvidenceTests.cs`
  - `docs/attachment-health.md`, `evidence/implementation-025.md`, `evidence/implementation-026.md`
- Neighboring paths read: `src/BellTower/Core/NativeAdoption.cs` (evidence writers), `src/BellTower/Sessions/AttachmentLifecycle.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, `src/BellTower/Tray/TrayApplication.cs` (drain caller), `src/BellTower.Ingress/BellTower.Ingress.csproj`, `scripts/install-dev.ps1` (type references), baseline `src/BellTower/IO/AttachmentHealth.cs` at `683ad12`
- Baseline commit: `683ad12`
- Implementation identity before the analysis (`./scripts/review-identity.ps1 -Baseline 683ad12 -Paths <the 12 assigned paths>`):
  - HEAD and baseline: `683ad12fa0e5c97e05457848be260eabd4c9bc0e`
  - Manifest: untracked (`?`) implementation-025.md, implementation-026.md, `Core/AttachmentHealth.cs`, `Core/ConversationLifecycle.cs`, `ConversationLifecycleTests.cs`; deleted (`D`) `IO/AttachmentHealth.cs`; modified (`M`) `docs/attachment-health.md`, `AttachmentEvidence.cs`, `AttachmentStore.cs`, `StateStore.cs`, `AttachmentEvidenceTests.cs`
  - Tracked diff SHA-256: `c3e23e41ad34cdecc3a3f5210c7789ebb2bbd930e30d479d2a8bd343f469e27a`
  - Untracked file hashes: `4180888f…` (implementation-025.md), `c57cb3b0…` (implementation-026.md), `e12a3017…` (Core/AttachmentHealth.cs), `26952ead…` (ConversationLifecycle.cs), `0d1ed7ea…` (ConversationLifecycleTests.cs)
  - Summary line: `head=683ad12 baseline=683ad12 files=11 diff=c3e23e41ad34 untracked=5/66c97d0eff99`, which matches the assignment
  - `src/BellTower/IO/ProcessLiveness.cs` shows `M` in `git status` but has no content diff against `683ad12`, so the script does not count it (11 files for 12 paths). It is most likely a line-ending or timestamp artifact of the reverted move attempt described in implementation-026.
- Identity after the analysis: the same summary line. Nothing in scope changed.
- Prior reports / rechecked findings: [Analysis 013](analysis-013.md), ANALYSIS-1200 to ANALYSIS-1207

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build | `dotnet build -c Release --no-restore -nodeReuse:false`, run after QA's full-suite test host had exited and before my tests | 0 warnings, 0 errors (incremental; the Release `bell-tower.dll` was already newer than every scoped source file) | passed |
| Focused tests | `dotnet test -c Release --no-build --no-restore --filter` on ConversationLifecycleTests, AttachmentHealthTests, AttachmentEvidenceTests, StorageTests, NativeAdoption* and AttachmentHealthCompatibilityTests | 154 passed, 0 failed | passed. Full suite not run (QA owns it). |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit code 0 | passed |
| Behavior probes | Throwaway console project in the session scratch directory, referencing a copy of the reviewed Release `bell-tower.dll` (hash identical to the build output). It calls `AttachmentHealth.Evaluate` with fake process checks and fake facts. No repository file, data directory or provider was involved. | Probes P1 to P10, quoted in the findings and the re-verification table | Measured on the reviewed build |
| Seam fit for 4.2 and 3.x (ANALYSIS-1200) | Reading `StateStore.Drain`/`ReconcileAttachments` and the snapshot type against decisions 1, 3, 4 and Risks | The drain only does `facts.Find(session.Key)`, a dictionary lookup (`StateStore.cs:180`). Late facts are dropped by `ObservedAt` (`ConversationLifecycle.cs:111`), so gathering facts before the locks is safe. | Fits. Remaining work under the locks is the PID-plus-creation-time check, the same as for direct records. |
| Dependency direction and single evaluator (ANALYSIS-1205) | `grep "using BellTower.IO" src/BellTower/Core`; diff of the legacy branch against `683ad12:src/BellTower/IO/AttachmentHealth.cs`; grep of `scripts/`, `tools/`, csproj links for the old type name | Core imports IO only in `NativeAdoption.cs` (pre-existing), `AttachmentHealth.cs` and `ConversationLifecycle.cs` (both for the `ProcessLiveness` enum only). `Core/AttachmentHealth.cs:19-34` is identical to the baseline body. No `ProcessProbe` default remains in Core. No reference to `BellTower.IO.AttachmentHealth` remains outside `openspec/`. | Verified within the accepted `ProcessLiveness` residual |
| Duplication | Reading | Two grace formulas remain (sticky for direct records, forward-only for shared), but both are behind the one entry point and documented per lifecycle owner in `docs/attachment-health.md` | No regression |
| Complexity | Reading | `ReduceTerminal` is about 30 lines with four ordered branches; `Changed` avoids rewrites when nothing changed (P10) | Acceptable. ANALYSIS-1300 is about branch semantics, not size. |
| Optional `TimeProvider` and `inspect` on `StateStore` | Reading; grep of `UtcNow` in `StateStore.cs`; count of `new StateStore(` callers | Both default to the production behavior (`TimeProvider.System`, `ProcessProbe.Inspect`); every production caller uses `new StateStore(paths)`. The clock covers reconciliation only; `StateStore.cs:228` and the capture/replay steps still use the system clock, which the parameter doc states. | No regression. Tests that advance `FixedTime` only move reconciliation time, which is what they assert. |
| Evidence writers (ANALYSIS-1207) | Grep for `Evidence =`, `Stamped(`, `attachments.Save` | Existing evidence is rewritten only by `ConversationLifecycle.cs:104` and `AttachmentStore.cs:105`, and both now preserve an unusable revision. `AttachmentLifecycle.cs:19` keeps evidence. `AgentLauncher` and the `Adopt` first-record path create new records. A missing `revision` deserializes to `CurrentRevision` (`AttachmentEvidence.cs:88`), so no record is frozen as revision 0. | Verified |
| Test quality | Reading the 27 cases and the mutation table in implementation-026 | Tests assert stored attachments and session health. Two-drain tests go through `StateStore.Drain` with an injected clock. One test has a dead tail (ANALYSIS-1301). | See findings |
| Claude/Codex parity | Reading | Policy branches on the lifecycle owner, not the provider. Direct rows run for Claude and Codex, shared rows for Codex (provider fact), and a mixed simultaneous drain covers both. | No parity gap |

## Findings

### ANALYSIS-1300: A fresh binder outcome is applied only when the resolved owner process is confirmed live

- Severity: warning
- Related task / requirement: 2.3 and 4.2; ANALYSIS-1201 (explicit outcome); decision 2 (disconnected and terminal-unverified are different states); decision 3 step 5 (a missing origin or non-client owner yields unverified)
- Locations: `src/BellTower/Core/ConversationLifecycle.cs:115` (`owner = facts?.OwnerClient ?? stored.Client`), `:117-119` (the shell is replaced before the outcome is decided), `:120-133` (facts are applied only in the `Live` branch)
- Observation: `ReduceTerminal` applies `facts.Outcome` only when `inspect(owner)` returns `Live`. When the facts name no owner, `owner` falls back to the stored client. Measured on the reviewed build:
  - **P1.** A never-bound record and fresh facts `unverified` / `missing-origin` with no owner: the facts are ignored, so the source stays `null` and the reason is lost.
  - **P2.** A stored `disconnected` / `owner-exited` binding whose old owner is dead, and fresh facts `unverified` / `missing-origin` with no owner (for example the conversation is now open in an IDE): the result is still `disconnected` / `owner-exited`, client 4001, with the observation time moved to now. The binder's current outcome is replaced by a stale claim about a previous client.
  - **P3.** A stored `connected` binding, owner liveness unreadable (`Unverified`, for example an elevated client), and fresh facts `listener-gone`: the result stays `connected` / `client-endpoint` with the old observation time. The disconnect is lost until the five-minute aging.
  - **P4.** A stored `connected` binding for owner 4001 with shell 4100, and fresh facts for a new owner 4002, which is unreadable, with shell 4200: the result is `connected`, client 4001, shell 4200. The new owner's shell is now paired with the old owner, so that shell exiting would disconnect the old owner's binding.

  So the binder cannot always report decision 3 step 5 directly, which was the aim of ANALYSIS-1201. Today nothing produces facts and host actions do not read the binding, so there is no user-visible effect yet. 4.2 diagnostics and actions would read these states.
- Proposed improvement and benefit: treat non-late facts as the current binding observation.
  - When facts are present, use `facts.OwnerClient` as the owner, without falling back to the stored client.
  - Only a confirmed `Ended` for that owner overrides the outcome (`owner-exited`). An unreadable owner should not block the reported outcome. If needed, cap a reported `connected` to `unverified` with a reason such as `owner-unverifiable`.
  - Replace the shell only together with the owner, inside `Changed`.
  - With no facts, keep today's behavior: stored-owner death check, aging and shell check.

  The binder then has one rule: what it reports is what is stored, unless a process check proves it wrong.
- Behavior-preservation risks: `ASharedOwnerKilled…`, `DelayedAndOutOfOrderFacts…`, `BellTowerRestart…` and `AnOwnerWithAReusedPid…` either pass an owner in the facts or use no facts, so they keep their results. The owner-death-without-facts and aging rules are unchanged. Facts without an owner would clear the stored client; that is intended, because the binder could not identify one.
- Evidence type: measured result (probes P1 to P4)
- Verification / measurement plan: reducer tests for P2 (expect `unverified` / `missing-origin`, no client), P3 (expect `disconnected` / `listener-gone`), and P4 (client and shell both from the new observation, or neither). Add a mutation check that restores the `?? stored.Client` fallback.
- Relationship: residual of ANALYSIS-1201; the P4 case also touches the ANALYSIS-1202 shell pairing

### ANALYSIS-1301: The future-observation case in `StaleRuntimeEvidenceIsNotWork` is never evaluated, so the clock-skew guard is not tested

- Severity: suggestion
- Related task / requirement: 2.3 ("stale runtime evidence"); QA-2002 (surviving mutations)
- Locations: `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs:413-414`; `src/BellTower/Core/ConversationLifecycle.cs:161` (`active.ObservedAt <= now + ClockSkew`)
- Observation: The test sets `evidence.Works["conversation"]` to an active observation one hour in the future, then ends with no `Evaluate` and no assertion. No other test supplies a future runtime time. The guard is correct today (probe P9: health `unavailable`, evidence time unchanged at the last hook). If the guard were removed, a future observation would pass the freshness check, be clamped to now and keep the conversation `unverified`, and no test would fail. This was reasoned from the code; I did not run the mutation, because the assignment does not allow source edits.
- Proposed improvement and benefit: evaluate after line 414 and assert `unavailable`. Optionally add a within-skew case that is clamped to `now`. Either pins the guard.
- Behavior-preservation risks: none; test only
- Evidence type: source observation, with probe P9 for the current behavior
- Verification / measurement plan: the new assertion passes, and removing the `<= now + ClockSkew` condition makes it fail
- Relationship: new; test-quality regression check of DEV-026

## Re-verification

All rows were checked at `head=683ad12 baseline=683ad12 files=11 diff=c3e23e41ad34 untracked=5/66c97d0eff99`.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1200 | `ConversationEvidenceSnapshot` and `StateStore.Drain(snapshot)` | The source interface is gone. The drain does a dictionary lookup only (`StateStore.cs:180`), and the snapshot is passed in from outside the locks. Its keys and types match what the 4.2 binder (`SharedTerminalFacts`) and the 3.x transport (`RuntimeWorkFacts`) will produce. Late facts are dropped, so gathering early is safe. A missing entry changes nothing except the documented aging and process checks. | verified |
| ANALYSIS-1201 | `SharedTerminalFacts.Outcome`/`Reason`, `FromEndpoint`, `TerminalFreshness` aging | An incomplete endpoint maps to `unverified` / `incomplete-endpoint` (test). A stale `connected` ages to `unverified` / `stale-connection` with its observation time kept, and owner death is detected without facts (test). A second drain causes no rewrite (P10). However, the explicit outcome is dropped unless the resolved owner is `Live` (P1 to P3). | partially verified; aging and mapping verified, outcome handling is still open in ANALYSIS-1300 |
| ANALYSIS-1202 | Shell rule reads `evidence.terminal.Shell`; facts carry a shell; owner change replaces or clears it; 4.2 planning constraint | P8: an exited evidence shell gives `disconnected` / `shell-exited`; an exited `Host.Shell` alone changes nothing; an owner change without a shell clears it. `AttachmentHealth` routes shared records away before `Host.Shell` is read (`AttachmentHealth.cs:15-20`). The tasks.md 4.2 text and the design Risks section carry the legacy-field constraint, including the `attachmentHealth` 2 gate cost. | verified; one residual pairing case (owner changes to an unreadable process) is covered by ANALYSIS-1300 |
| ANALYSIS-1203 | Forward-only evidence time (`ConversationLifecycle.cs:96-99`) | P5, the original scenario with a 10-minute grace and the last hook 1 hour ago: active at t0 gives `unverified` since t0. At t0+3 with no facts, and at t0+3 with a 3-minute-old fact, it stays `unverified` since t0. At t0+11 it is `unavailable`, still since t0. The time never moves back. The test runs two and three drains through `StateStore` with an injected clock. | verified |
| ANALYSIS-1204 | Unused surface removed | `HookActivity`, `LifecycleResult`, `IConversationEvidenceSource` and the direct-record terminal mapping are gone (grep: no matches in `src/`). Hook freshness has one input (`ConversationObservedAt`), and tests assert stored state. Minor residual, not raised as a new finding: `SharedTerminalFacts.ReportedRuntimeStatus` is neither persisted nor read (`Changed`, `:148-156`); task 5.2 should decide whether to store it or drop it. | verified |
| ANALYSIS-1205 | Single evaluator `Core/AttachmentHealth`; `ConversationLifecycle` no longer calls it back | One entry point. The legacy branch is identical to the baseline. Core has no process probe (no default `inspect`). The only Core-to-IO uses are the `ProcessLiveness` enum (accepted residual) and the pre-existing `NativeAdoption` use. No stale references to the old namespace remain in scripts, tools, tests or csproj links. | verified (accepted residual: `ProcessLiveness` stays in IO) |
| ANALYSIS-1206 | Accepted decision: hook revival documented and tested | P7: an unavailable shared record with a newer hook gives `unverified`; without it, `unavailable`. A direct unknown-owner `unavailable` record stays `unavailable`. `docs/attachment-health.md` "Shared conversations" states the rule, the `verified-live` ban and the rollback difference. The test goes through `AttachmentStore.Adopt` and `StateStore.Drain`. | verified |
| ANALYSIS-1207 | `AttachmentEvidence.IsUnusableRevision` in the reducer and in `AttachmentStore.Adopt` | P6: revision-2 evidence comes back as the same object, health is updated to `unavailable`, and fresh facts are not applied. The test keeps the evidence JSON byte-identical through a drain and a hook merge. No other writer rewrites existing evidence (see Analysis performed). | verified |

## Assessment and limitations

- What I covered:
  - All eight assigned findings, for Claude Code and Codex (direct rows for both, shared rows for Codex, a mixed drain), plus regression checks of duplication, complexity, the new `StateStore` parameters, evidence writers and test quality.
- Results:
  - ANALYSIS-1200, 1202, 1203, 1204, 1205 (with the accepted residual), 1206 and 1207 are verified.
  - ANALYSIS-1201 is partially verified. The remaining gap is ANALYSIS-1300, which should be closed before 4.2 relies on the binder outcome. It has no user-visible effect today, because no facts producer exists yet.
  - ANALYSIS-1301 is a small test-only gap.
- Not a finding, but measure in 3.1 and 4.2: once a producer refreshes facts on every drain, each refresh changes `ObservedAt` or `UnverifiedSince` and so rewrites the attachment file under the presentation lock, once per shared conversation per drain. That is fine at current drain rates, but measure write count and drain time when the pump exists. Not measured here, because no producer exists.
- Not done:
  - the full suite, which QA is running
  - mutation runs, because source edits are not allowed
  - live provider runs, which the assignment excludes
- Build timing: I ran the Release build only after QA's full-suite test host had exited, and it finished before QA started its next filtered run (HookMaintenance and AudioQueue). Those QA processes and the MSBuild reuse nodes that were already running were left alone. None of my processes remain.
- Probe project: in the session scratch directory, outside the repository, against a copy of the Release output. It wrote nothing to the repository or to any data directory.
- Unexpected writes: none. The untracked `openspec/changes/verify-codex-repository-subagents/reports/` and `openspec/changes/verify-signed-releases-and-update-policy/` were left untouched.
- Triage and the findings ledger belong to the orchestrator.
