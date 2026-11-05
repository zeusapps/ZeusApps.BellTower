<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-030 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the report write (as reported by the reviewer). -->

The three rechecked findings are fixed. ANALYSIS-2900, ANALYSIS-2904 and QA-3600 all re-verify, and nothing blocks them. The client refused the report write, so the full text is below for you to save as `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-030.md`.

- **ANALYSIS-2900:** every text now matches the code: the matrix row, `CodexNameModes` (which doctor prints) and the docs row and gaps bullet. All say a conversation of another Codex home is read through the tray-home backend and that no name is written without a successful read of the exact thread id. The new test fairly says it pins only a rejecting stand-in.
- **ANALYSIS-2904:** the new canary test does back `supported` for both providers. It runs the history reader on content-bearing tool input and tool-result lines for both, and the Claude custom-title read. The scan covers everything that reader can write.
- **QA-3600:** the named Claude cell is fixed.

Two non-blocking warnings remain:
- **ANALYSIS-3000:** the Claude direct-clients cell still says "Claude Code is always direct". That is the same unprobed assumption QA-3600 had removed one row above.
- **ANALYSIS-3001:** doctor `confirmationCoverage` and the prose at `docs/compatibility.md` line 48 still count shared-backend sessions as having no status signal. This contradicts the corrected matrix row; the text predates this change.

```
status: done
role / client / model: code-analyzer / Claude Code (Agent SDK delegated subagent; version not printed) / claude-opus-5-5
task / finding IDs: task 3.4 recheck after DEV-042; rechecked ANALYSIS-2900 (verified), ANALYSIS-2904 (verified), QA-3600 (verified for the named cell, residual in ANALYSIS-3000); new ANALYSIS-3000 (warning, text only), ANALYSIS-3001 (warning, baseline doctor and doc drift)
changed or report paths: none in the repository (write refused); report text inline below for openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-030.md; ignored build output only under D:\Code\ZeusApps.BellTower\artifacts\analysis-030
checks: dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-030 --filter "CapabilityMatrixTests|CodexSharedRuntimeServiceTests|IdentityHistoryTests" -> passed (53/53); repeat x5 (new transcript and tray-home tests plus CapabilityMatrixTests) -> passed (14/14 each); dotnet format --verify-no-changes --include <5 changed .cs> -> passed; full suite -> not run (proportional recheck); mutation -> not run (source edits outside ownership); native checks -> not run (assignment)
target identity: before head=0b11026 baseline=0b11026 files=10 diff=d3f3244b4973 untracked=2/eb8085b8cd24; after identical
limitations / decisions needed: report write refused by the client, full text inline for the orchestrator to save; decide whether ANALYSIS-3000 (one text fix) and ANALYSIS-3001 (doctor count and line-48 prose) are fixed in this 3.4 cycle or routed to 5.3 and 5.2
next: orchestrator saves the report and triages ANALYSIS-3000 and 3001; no blocker for closing the assigned IDs
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-030 (DEV-042 recheck, task 3.4 fix cycle 1)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK delegated subagent, non-interactive) / claude-opus-5-5; client version not printed in this session
- Date: 2026-11-05
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-030
- Task and requirement references: [tasks.md](../tasks.md) 3.4 (with 4.2, 5.2, 5.3 for scope boundaries); [design.md](../design.md) decisions 3 and 4; AGENTS.md parity rules; [implementation 042](../evidence/implementation-042.md); [analysis 029](analysis-029.md); [QA 036](qa-036.md)
- Scope and relevant neighboring paths: the 10 target paths below. Neighbors read: `src/BellTower/Sessions/IdentityHistoryReader.cs`, `CodexRuntimeOpener.cs`, `CodexNameService.cs`, `CodexPermissionPump.cs`, `src/BellTower/Providers/CodexConnection.cs`, `CodexThreadIdentity.cs`, `AgentModeClassifier.cs`, `src/BellTower/Diagnostics/TranscriptTimeoutReader.cs`, `ConfirmationCoverageReporter.cs`, `src/BellTower/IO/DataPaths.cs`, `tests/BellTower.Tests/Testing/TestWorkspace.cs`, `IdentityFixtures.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs`
- Baseline commit: 0b11026
- Implementation identity (before): HEAD 0b11026b2f93edc909da1ea7c17931c30a947155; manifest M `docs/compatibility.md`, `docs/session-identity.md`, `docs/session-titles.md`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`, `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`, `tests/BellTower.Tests/Sessions/IdentityHistoryTests.cs`; ? `evidence/implementation-041.md` (630645d4…c0f4), `evidence/implementation-042.md` (cb1c220b…3113); tracked diff SHA-256 d3f3244b49731d5d0f52f03b1752fa420b33716a354f0daf4b0496430fefbd2a; summary `head=0b11026 baseline=0b11026 files=10 diff=d3f3244b4973 untracked=2/eb8085b8cd24` (matches the expected identity)
- Before/after identity: after the analysis the same command printed `head=0b11026 baseline=0b11026 files=10 diff=d3f3244b4973 untracked=2/eb8085b8cd24`; identical, no unexpected mutation. `git status` showed no lock-file or other tracked change from the isolated restore and build.
- Prior reports / rechecked findings: [analysis 029](analysis-029.md) ANALYSIS-2900, ANALYSIS-2904; [QA 036](qa-036.md) QA-3600

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-2900 text vs code (Codex) | Read the matrix row, `CodexNameModes`, both docs rows and the gaps bullet against `CodexRuntimeOpener.Select`, `CodexNameService.Sync`, `CodexConnection.ReadThreadAsync`, `CodexThreadIdentity.Require` | `CapabilityMatrix.cs` lines 38 and 79; `docs/compatibility.md` lines 71 and 90; opener line 58-59 | All texts now say the other-home conversation is read through the tray-home backend. The read precedes any `thread/name/set`, and a shared read requires the exact thread id. "Expected to surface as a shared-backend refusal" is hedged as unverified. Verified |
| ANALYSIS-2900 test | Read `AConversationTheTrayHomeBackendDoesNotHostIsRefusedAndNeverWritten` and the stand-in `RejectAfterInitialize` mode | Stand-in answers every post-initialize request with a JSON-RPC error | Pins the refusal label and the absence of `thread/name/set`, resume and turns for a rejecting backend; the texts say "pins a rejecting stand-in only". Honest |
| ANALYSIS-2904 test reach (both) | Traced the theory through `IdentityHistoryReader.Refresh`, `Collect`, `Relevant`, `ReadClaude`, `ReadCodex`, `Merge`, `AddCall` | See Re-verification | Both providers' parsing paths that touch content run on canary data; the verified outcomes are asserted, so the test is not vacuous |
| ANALYSIS-2904 scan completeness | Read `TestWorkspace` and `DataPaths` | Transcript at `<root>/transcript.jsonl`, data directory `<root>/data`, scan covers every file under the data directory plus serialized identity, session and status strings | Complete for everything the history reader can write (it writes only through `IdentityStore`; it has no log call) |
| Other transcript readers | `rg` over `src` for transcript reads | Only `IdentityHistoryReader` (history, Claude title) and `TranscriptTimeoutReader` (doctor `transcriptTimeouts`) read transcript content | The doctor reader returns counts, timestamps, version and mode only (by source). The new row names the history reader, so it does not claim the doctor reader |
| QA-3600 wording (Claude) | Read the shared-backend Claude cell in source and docs; `rg` for "verified interactive", "always direct"; read `AgentModeClassifier.Claude`; design decisions 3 and 4; task 5.3 | `CapabilityMatrix.cs` lines 72 and 75; `docs/compatibility.md` lines 69 and 70 | The named cell is fixed. The sibling direct-clients cell still states the same assumption as fact (ANALYSIS-3000) |
| Doctor and docs agreement | `DoctorReportsTheSamePerModeCodexNameText`, `CompatibilityDocumentStatesMatchTheMatrixSource`; `rg` for stale "managed runtimes only", "existing or remote", "not read through" | `ConfirmationCoverageReporter.cs` line 59, `docs/compatibility.md` line 48 | Matrix, `CodexNameModes`, doctor `codex.nameSynchronization`, `session-identity.md`, `session-titles.md` agree. Doctor `confirmationCoverage` and the human-wait prose still contradict the updated matrix row (ANALYSIS-3001, baseline text) |
| Build and tests | `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-030 --filter "FullyQualifiedName~CapabilityMatrixTests\|FullyQualifiedName~CodexSharedRuntimeServiceTests\|FullyQualifiedName~IdentityHistoryTests"` | 53 passed, 0 failed | Passed; isolated ignored output |
| Repeat | Same assembly, filter `TranscriptReadsKeepNoPromptAssistantOrToolContent\|AConversationTheTrayHomeBackendDoesNotHostIsRefusedAndNeverWritten\|CapabilityMatrixTests`, 5 runs | 14/14 each run (150-203 ms) | Passed on an idle machine |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <5 changed .cs>` | exit 0 | Passed |
| Mutation | not run | Ownership forbids source edits | DEV-042 mutants 1 and 2 accepted as developer evidence only; the source trace above is independent |
| Full suite, native checks | not run | Proportional recheck; assignment forbids launching providers | Not performed |

Client coverage: the transcript-read checks cover Claude and Codex. The other-home checks cover Codex only (Claude has no runtime read). The wording checks cover the Claude cells.

## Findings

### ANALYSIS-3000: The Claude direct-clients cell still states "always direct" as fact, while the shared-backend cell now calls the interactive shape an assumption

- Severity: warning (text only; no status word changes; not blocking for the assigned IDs)
- Related task / requirement: 3.4 (honest per-mode states), 5.3, QA-3600; design decision 3 ("Do not hardcode Claude as direct")
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs` line 75; `docs/compatibility.md` line 70
- Observation: DEV-042 corrected the shared-backend Claude cell to "no known shared-backend mode … the interactive shape is a stated assumption". The direct-clients Claude cell of the same matrix (printed by doctor) still says "Claude Code is always direct". That rests on the same unprobed interactive shape. `AgentModeClassifier.Claude` returns `interactive-client` for every non-`--print` launch by design, not by a probe. The status word (`unverified`) is honest. The detail is stronger than the evidence and contradicts the corrected cell one row above.
- Proposed improvement and benefit: reword to "Claude Code is treated as direct (no known shared-backend mode; the interactive shape is a stated assumption, see the shared backend row)". The matrix then gives one account of the assumption. Optionally extend the existing `DoesNotContain` assertion in `CompatibilityDocumentStatesMatchTheMatrixSource` to "always direct".
- Behavior-preservation risks: text only. `PerModeNativeNameAndStatusStatesAreHonestForBothProviders` asserts only `sync-native-session-titles` and "not a provider restriction" in this cell, and both stay.
- Evidence type: source observation
- Verification / measurement plan: `rg -n "always direct" src docs` returns nothing; the targeted tests still pass.
- Relationship: residual of QA-3600 (same wording class, sibling cell)

### ANALYSIS-3001: Doctor `confirmationCoverage` and the human-wait prose still count shared-backend sessions as having no status signal, contradicting the corrected matrix row

- Severity: warning (diagnostics understate a supported path; baseline text, not introduced by DEV-041 or DEV-042; not blocking for the assigned IDs)
- Related task / requirement: 3.4 (status matrix reconciled with current work), 5.2 (doctor surfaces); AGENTS.md (diagnostics honest)
- Locations: `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs` lines 48-59 (outside the review target); `docs/compatibility.md` line 48 (in the target, unchanged by this change); compare `CapabilityMatrix.cs` line 106 and `docs/compatibility.md` line 80
- Observation: the matrix row "Human-wait confirmation without an owning runtime" now says a shared-backend conversation reaches the runtime status over the observer path. The code agrees: `CodexPermissionPump` line 106 uses `opener.Select`, which returns shared access, and `PermissionStatusComesFromTheExactConversationOnASharedBackend` covers it. Doctor `codex` `confirmationCoverage` counts as verifiable only sessions with an owned `CodexRuntimeStore` record. So a live shared-backend session is counted in `UnavailableWithoutRuntime`, with the status text "unavailable for sessions without a verified owning runtime (explicit --no-daemon, existing or remote sessions)". The compatibility prose at line 48 repeats "existing or remote sessions … have no human-wait signal". Doctor thus prints two contradictory accounts for the same session: the matrix row and `confirmationCoverage`.
- Proposed improvement and benefit: count a session as verifiable when `CodexRuntimeOpener.Select` returns access (owned or shared), so the same selection rule is used and no second policy is created. Then align the status text and the line-48 prose with the matrix row ("no verified owning runtime or shared backend; explicit --no-daemon, remote and shared conversations not yet reached by task 4.2"). If the orchestrator keeps code changes for 5.2, at least correct the line-48 prose now and record the doctor count under 5.2.
- Behavior-preservation risks: `confirmationCoverage` field values change for shared sessions, and its counts are additive diagnostics. Check the `vscode/` consumers and the doctor tests for parsing of `UnavailableWithoutRuntime`; none was found in a quick search of `tests`.
- Evidence type: source observation
- Verification / measurement plan: a `ConfirmationCoverageReporter` test with one active shared-backend attachment (mode `shared-backend`, shared conversation) expecting `WithVerifiableRuntime = 1`; rerun the doctor and matrix tests.
- Relationship: neighbor of ANALYSIS-2900 (same class of text-versus-selection drift); task 5.2

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-2900 | `head=0b11026 … diff=d3f3244b4973 untracked=2/eb8085b8cd24` | `CodexNameModes` (line 38, printed by doctor `codex.nameSynchronization`, pinned by `DoctorReportsTheSamePerModeCodexNameText`), the matrix row (line 79), docs row (line 71) and gaps bullet (line 90) all describe reading through the tray-home backend, an unverified answer and no write without an exact-thread read. Code check: `Select` returns `Shared(home())` without a per-conversation home; `Sync` reads before `SetNameAsync`; `ReadThreadAsync` calls `CodexThreadIdentity.Require`, which rejects a different id and, for proven (shared) transports, a missing id. The new test passes (5 repeats) and correctly scopes its claim to a rejecting stand-in. Tests assert the old wording is gone from source and docs. `session-identity.md` "report `unavailable`" still holds, because the expected label is `unavailable: shared backend rejected the request` | verified |
| ANALYSIS-2904 | same | Both theory cases run the history reader. Claude: the prompt and assistant lines are dropped by the `Relevant` byte prefilter, as in production. The `Bash` `tool_use` input canary goes through `AddCall` and the classifier and is not kept. The `tool_result` canary is materialized by `Text(body)` and reduced to a hashed id plus offset and two booleans. The `custom-title` is parsed and `NativeTitle` asserted, so the Claude native-title reader is exercised. Codex: the `exec_command` input canary goes through `AddCall`. The `function_call_output` canary is materialized and only regex-matched, then reduced to the same record. `ActiveWork == change-a` proves the parse ran for both. The Codex native title is not in the rollout. Its reader is the observer name read, covered by `ObserverReadsDiscardPreviewsTurnsAndTokens`, and the Codex cell correctly claims no title. Scan: every file under the data directory (the transcript lives outside it, so the scan cannot self-match), serialized identity and session, name, native title and sync status; the reader writes only through `IdentityStore`. `supported` for both is backed under the legend. Residuals, no new finding: the "keeps only" list omits bookkeeping (offsets, timestamps, a 16-character hash of the first 256 bytes as fingerprint), which is not content. The new row's citation is outside the fixed-index reflection check (ANALYSIS-2902, open suggestion). The doctor `TranscriptTimeoutReader` is not named by the row and returns only counts and timestamps by source | verified |
| QA-3600 | same | The shared-backend Claude cell (line 72, docs line 69) now states no known shared-backend mode, the retained evidence (`--print` captures, launch-shape fixtures) and the interactive shape as a stated assumption. "verified interactive" and "2.1.310" are gone, and the test asserts their absence in source and docs. Task 5.3 records the same wording and the design decision 4 citation. Decision 4 itself says only "no Claude native-title transport by assumption", so the citation is the orchestrator's framing rather than a decision text. The sibling direct-clients cell still says "always direct" (ANALYSIS-3000) | verified for the named cell; residual in ANALYSIS-3000 |

## Assessment and limitations

- Blocking for the assigned IDs: none. ANALYSIS-2900, ANALYSIS-2904 and QA-3600 are corrected as assigned. The new transcript canary test exercises the content-touching parse paths for both providers with a complete scan of what the reader can write, so `supported` is backed for both cells.
- Not blocking: ANALYSIS-3000 (warning, one text fix that completes the QA-3600 wording) and ANALYSIS-3001 (warning, baseline doctor and doc drift that the corrected matrix row now contradicts; can go to 5.2 if the orchestrator prefers). No row is stronger than the evidence apart from ANALYSIS-3000. ANALYSIS-3001 is weaker than the code.
- Checks: isolated build and targeted tests (53 passed), 5 repeat runs (14/14), a verify-only format check (passed). Not run: the full suite, mutation testing (source edits are outside ownership) and native checks (no provider launch).
- Unexpected writes: none apart from ignored build output under `artifacts/analysis-030`. The report write was refused by the client, so this text is returned inline. The review identity was unchanged before and after.
- Decisions needed: whether ANALYSIS-3000 and ANALYSIS-3001 are fixed in this 3.4 cycle or routed to 5.3 and 5.2.
