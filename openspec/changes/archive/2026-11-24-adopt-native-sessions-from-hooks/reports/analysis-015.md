<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-015 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-015 (recheck of DEV-027)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent); version not queried, because the assignment forbids launching `claude`; auto mode; `claude-opus-5-5`
- Date: 2026-11-04 (about 17:45 UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-015 (recheck)
- Task and requirement references:
  - Task 2.3, with 4.2 as the future producer of terminal facts ([tasks](../tasks.md))
  - [design](../design.md) decision 2 (disconnected and terminal-unverified are different states) and decision 3 step 5
  - [implementation 027](../evidence/implementation-027.md)
  - [Analysis 014](analysis-014.md), [QA 021](qa-021.md) (QA-2100), and the ledger rows ANALYSIS-1300, ANALYSIS-1301 and QA-2100 in [findings](findings.md)
- Scope: the 13 assigned paths. Since ANALYSIS-014 the substantive changes are in `src/BellTower/Core/ConversationLifecycle.cs` (`ReduceTerminal`) and `tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs`; `evidence/implementation-027.md` is new.
- Neighboring paths read:
  - `src/BellTower/Core/AttachmentHealth.cs`
  - `src/BellTower/IO/StateStore.cs` (the write decision at line 181)
  - `src/BellTower/Core/AttachmentEvidence.cs` (`TerminalEvidence`)
  - `docs/attachment-health.md`
  - the task 4.2 text
- Baseline commit: `683ad12`
- Implementation identity before the analysis (`./scripts/review-identity.ps1 -Baseline 683ad12 -Paths <the 13 assigned paths>`):
  - HEAD and baseline: `683ad12fa0e5c97e05457848be260eabd4c9bc0e`
  - Manifest:
    - untracked (`?`): implementation-025.md, implementation-026.md, implementation-027.md, `Core/AttachmentHealth.cs`, `Core/ConversationLifecycle.cs`, `ConversationLifecycleTests.cs`
    - deleted (`D`): `IO/AttachmentHealth.cs`
    - modified (`M`): `docs/attachment-health.md`, `AttachmentEvidence.cs`, `AttachmentStore.cs`, `StateStore.cs`, `AttachmentEvidenceTests.cs`
  - Tracked diff SHA-256: `c3e23e41ad34cdecc3a3f5210c7789ebb2bbd930e30d479d2a8bd343f469e27a`
  - Untracked file hashes:
    - `4180888f…` implementation-025.md
    - `c57cb3b0…` implementation-026.md
    - `bdf53c8e…` implementation-027.md
    - `e12a3017…` Core/AttachmentHealth.cs (unchanged since ANALYSIS-014)
    - `e1144c7c…` ConversationLifecycle.cs
    - `46f680dd…` ConversationLifecycleTests.cs
  - Summary line: `head=683ad12 baseline=683ad12 files=12 diff=c3e23e41ad34 untracked=6/c934da3ad015`, which matches the assignment.
  - `src/BellTower/IO/ProcessLiveness.cs` still has no content diff against `683ad12`, so the 13 paths count as 12 files, as in ANALYSIS-014.
- Identity after the analysis: the same summary line. Nothing in scope changed.
- Prior reports / rechecked findings: [Analysis 014](analysis-014.md): ANALYSIS-1300, ANALYSIS-1301

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build currency | No rebuild (assignment). Compared timestamps and hashes. | `src/BellTower/bin/Release/net10.0-windows/bell-tower.dll` is newer than every source file under `src/BellTower` and `tests/BellTower.Tests`. The test output's copy of `bell-tower.dll` has the same SHA-256 (`b55f5660…`). `BellTower.Tests.dll` is newer than `ConversationLifecycleTests.cs`. | The Release output is current for the reviewed target |
| Focused tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~ConversationLifecycleTests\|FullyQualifiedName~AttachmentHealthTests\|FullyQualifiedName~AttachmentEvidenceTests"` | 64 passed, 0 failed. `ConversationLifecycleTests` now has 32 tests, up from 27. | passed. Full suite not run (QA owns it). |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit code 0 | passed |
| Behavior probes | Small console project in the session scratch directory, outside the repository. It references a copy of the reviewed Release `bell-tower.dll` (identical hash) and calls `AttachmentHealth.Evaluate` with fake liveness and fake facts. Built with `-nodeReuse:false`. No repository file, data directory or provider was involved. | P1 to P4 re-run as in ANALYSIS-014, plus regression probes R1 to R12 | Measured on the reviewed build. Results are below and in the re-verification table. |
| Regression of the no-facts path | Probes R1 to R3 | **R1:** a fresh `connected` binding with a live owner and no facts keeps a record-equal terminal, so the attachment is not rewritten (`StateStore.cs:181` writes only when `evaluated != attachment`). **R2:** an aged `connected` binding becomes `unverified` / `stale-connection` and keeps its old observation time. **R3:** a dead owner gives `disconnected` / `owner-exited`; a second pass is record-equal, so there is no repeated rewrite. | no regression |
| Regression of the facts path | Probes R4 to R12 | **R4:** facts naming a dead owner give `owner-exited` with the facts' owner, shell and observation time. **R5:** the same owner without a shell keeps the stored shell. **R6:** facts without an owner clear the client and the shell. **R7:** late facts with a new unreadable owner are ignored; the stored owner and shell stay. **R8:** an ended facts shell gives `shell-exited` at `now`; replaying the same facts counts as late, and the result is stable. **R9:** `demoted` with an unreadable owner is stored, not capped. **R10:** `connected` without an owner is capped to `unverified` / `owner-unverifiable`. **R11:** replaying identical facts with the same observation time gives a record-equal terminal. **R12:** a shared record with `EndedAt` stays `confirmed-ended`, with record-equal evidence. | No regression. All results match decision 2 and the ANALYSIS-1300 proposal. |
| Complexity | Reading `ConversationLifecycle.cs:108-143` | `ReduceTerminal` grew from about 30 to 36 lines and keeps the same ordered steps: drop late facts, select owner and shell, check owner death, apply the facts outcome (with the `connected` cap), age without facts, check the evidence shell. The shell selection at `:118-119` is a nested conditional, but the comment at `:115-116` states the rule. | Acceptable; no regression |
| Duplication | Reading | No rule is copied. The owner-death and shell checks each run once for both the facts and no-facts paths, and `Changed` is still the only writer of the terminal fields. | no regression |
| Seam fit for the 4.2 binder | Reading against task 4.2 and design decision 3 | The binder now follows one rule: a non-late observation is stored as reported, with two exceptions. A confirmed `Ended` owner gives `owner-exited`, and a `connected` claim without a `Live` owner gives `owner-unverifiable`. Owner and shell are always replaced together, so a new owner's shell never ends up paired with the previous owner (P4, P4b). The snapshot contract (`ConversationEvidenceSnapshot`, `SharedTerminalFacts`) is unchanged, and the binder still writes only `evidence.terminal`. One consequence for 4.2: facts without an owner clear the stored client (R6), so a producer must include the owner in every observation where it knows it. The `ReduceTerminal` comment says this; `docs/attachment-health.md` does not yet (see the note below; not a finding). | fits |
| Test quality | Reading `ConversationLifecycleTests.cs:404-502` and the mutation table in implementation 027 | Each P-probe has a matching test that asserts the stored terminal: binding and reason, plus client and shell where they matter. I traced each of the four listed mutations to an assertion that kills it. Restoring the `?? stored.Client` fallback would keep client 4001 and give `owner-exited`, which fails `FreshFactsWithoutAnOwner…` (`:471-473`). Removing the future rejection would make the `+1 h` case `unverified`, which fails `:416`. Removing the clamp would set `UnverifiedSince` to `now + 10 s`, which fails `:422`. Removing the `EndedAt` gate would reduce the record, which fails `:436-438`. I did not run the mutations myself, because source edits are not allowed. Minor: `AnUnreadableOwnerDoesNotBlockAFreshDisconnectOutcome` does not assert the new observation time; P3 and `DelayedAndOutOfOrderFacts…` cover it. | adequate |
| Claude/Codex parity | Reading | The change touches only the shared-backend reducer, which branches on the lifecycle owner, not the provider. The direct Claude Code and Codex rows and the mixed simultaneous drain are unchanged and pass. Shared rows stay Codex-only, as the provider facts require. | no parity gap |

## Findings

**No findings.** The DEV-027 change has no regressions, and this recheck raises no new suggestions.

## Re-verification

All rows were checked at `head=683ad12 baseline=683ad12 files=12 diff=c3e23e41ad34 untracked=6/c934da3ad015`.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1300 | `ReduceTerminal` (`src/BellTower/Core/ConversationLifecycle.cs:115-131`): the owner comes from non-late facts with no fallback; the shell is replaced together with the owner; only a confirmed `Ended` overrides the outcome; `connected` without a `Live` owner is capped to `unverified` / `owner-unverifiable` | Probes on the reviewed Release build. **P1** (never-bound record; facts `unverified` / `missing-origin` without an owner): now `unverified` / `missing-origin`, observed now. Before, the facts were ignored and the source stayed null. **P2** (stored `disconnected` / `owner-exited` with dead owner 4001; the same facts): now `unverified` / `missing-origin` with client null. Before, the stale `owner-exited` with client 4001 remained. **P3** (stored `connected`; owner unreadable; facts `listener-gone`): now `disconnected` / `listener-gone`, observed now. Before, it stayed `connected`. **P4** (stored owner 4001 with shell 4100; facts name a new unreadable owner 4002 with shell 4200, `connected`): now `unverified` / `owner-unverifiable` with client 4002 and shell 4200. Before, client 4001 was paired with shell 4200. Follow-ups: in P4b the old shell 4100 exiting afterwards changes nothing; in P4c the new shell 4200 exiting gives `shell-exited`. Regression probes R1 to R12 show the no-facts path and the late, same-owner, dead-owner and demoted cases are unchanged. Tests `AMissingOriginOutcome…`, `FreshFactsWithoutAnOwner…`, `AnUnreadableOwnerDoesNotBlock…` and `ANewUnreadableOwnerTakesItsShell…` pass. | verified |
| ANALYSIS-1301 | `StaleRuntimeEvidenceIsNotWork` (`tests/BellTower.Tests/Providers/ConversationLifecycleTests.cs:404-423`) | The future case (`now + 1 h`) is now evaluated and asserts `unavailable` with `UnverifiedSince` unchanged (`:414-417`). A new within-skew case (`now + 10 s`) asserts `unverified` with `UnverifiedSince == now` (`:419-422`), which also pins the clamp. Reading the expressions, both assertions fail if `active.ObservedAt <= now + ClockSkew` (`ConversationLifecycle.cs:165`) or the clamp (`:166`) is removed; the developer's mutation run reports the same. The test passes. | verified |

## Assessment and limitations

- What I covered: ANALYSIS-1300 and ANALYSIS-1301, plus a regression check of the DEV-027 change for complexity, duplication, seam fit for the 4.2 binder and test quality. This covers the shared Codex reducer and the unchanged direct Claude Code and Codex paths.
- Results:
  - Both findings are verified.
  - ANALYSIS-1300 was the remaining residual of ANALYSIS-1201. Whether ANALYSIS-1201 can now close is the orchestrator's decision.
  - As part of the regression check I also read and probed (R12) the new `EndedAt` test for QA-2100. QA-022 owns that finding.
- Note for 4.2, not a finding: `docs/attachment-health.md` line 28 explains the terminal facet when no new facts arrive, but not what happens with fresh facts:
  - the owner and shell are replaced together
  - facts without an owner clear the stored client
  - a `connected` report without a live owner is stored as `unverified` / `owner-unverifiable`

  The `ReduceTerminal` comment states this rule. Adding it to the doc when the binder lands would keep the doc complete.
- The measurement note from ANALYSIS-014 still applies. Once a producer refreshes facts on every drain, each new observation time rewrites the attachment file; repeating identical facts does not (R11). Not measured, because no producer exists yet.
- Not done:
  - the full suite, which QA is running
  - mutation runs, because source edits are not allowed; I checked the four mutations by tracing the assertions instead
  - live provider runs, which the assignment excludes
- Processes and writes:
  - I did not build the solution. The scratch probe was built with `-nodeReuse:false`.
  - No test host or probe process of mine remains.
  - QA's processes and the shared build servers were left alone. I did not run `dotnet build-server shutdown`, because QA was running in parallel.
  - The client refused the write of this report, so it is returned inline. No repository file was written.
  - The untracked `openspec/changes/verify-codex-repository-subagents/reports/` and `openspec/changes/verify-signed-releases-and-update-policy/` were left untouched.
- Triage and the findings ledger belong to the orchestrator.
