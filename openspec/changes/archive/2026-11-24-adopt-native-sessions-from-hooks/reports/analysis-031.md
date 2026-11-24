<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-031 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the report write (as reported by the reviewer). -->

Both rechecked findings are fixed, and nothing blocks closing task 3.4.
- **ANALYSIS-3000:** the Claude direct-clients cell now says Claude Code is "treated as direct", with the interactive shape a stated assumption. It matches the shared-backend cell. "always direct" is gone and a test pins its absence.
- **ANALYSIS-3001:** doctor `confirmationCoverage` now calls the same `CodexRuntimeOpener.Select` the permission pump calls. There is no copy of the rule, so no second policy. The coverage texts and the line-48 coverage sentence match the matrix and the code.
- **Claude coverage:** unchanged.
- **Regression from the fix:** none found.

One non-blocking warning, new ID ANALYSIS-3100 (text only): doctor `codex.approvalResolution` and an earlier confirmer sentence at `docs/compatibility.md` line 48 still name only the owning runtime as the confirming source. The pump also confirms waits from a shared backend. It can go to task 5.2 or 5.3.

The client refused the report write, so the full text is below for you to save as `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-031.md`.

```
status: done
role / client / model: code-analyzer / Claude Code (Agent SDK delegated subagent; version not printed) / claude-opus-5-5
task / finding IDs: task 3.4 recheck after DEV-043 (fix cycle 2); rechecked ANALYSIS-3000 (verified), ANALYSIS-3001 (verified for the assigned surfaces, residual in ANALYSIS-3100); new ANALYSIS-3100 (warning, text only, not blocking)
changed or report paths: none in the repository (write refused); report text inline below for openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-031.md; ignored build output only under D:\Code\ZeusApps.BellTower\artifacts\analysis-031
checks: dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-031 --filter "ConfirmationCoverageTests|CapabilityMatrixTests|CodexPermissionPump|CodexSharedRuntimeServiceTests" -> passed (37/37); repeat x3 (ConfirmationCoverageTests|CapabilityMatrixTests) -> passed (19/19 each); dotnet format --verify-no-changes --include <7 changed .cs> -> passed; full suite -> not run (proportional recheck); mutation -> not run (source edits outside ownership); native checks -> not run (assignment)
target identity: before head=0b11026 baseline=0b11026 files=13 diff=aafff4d2de5a untracked=3/3c21cdd5b8ff; after identical
limitations / decisions needed: report write refused by the client, full text inline for the orchestrator to save; performance cost in doctor is a hypothesis (not measured); route ANALYSIS-3100 to 5.2 or 5.3, or accept it as a two-sentence text fix outside the cycle budget
next: orchestrator saves the report and triages ANALYSIS-3100; no blocker for closing ANALYSIS-3000 and ANALYSIS-3001
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-031 (DEV-043 recheck, task 3.4 fix cycle 2)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK delegated subagent, non-interactive) / claude-opus-5-5; client version not printed in this session
- Date: 2026-11-05
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-031
- Task and requirement references: [tasks.md](../tasks.md) 3.4 (with 4.2, 5.2, 5.3 as scope boundaries); AGENTS.md parity and honest diagnostics; [implementation 043](../evidence/implementation-043.md); [analysis 030](analysis-030.md)
- Scope and relevant neighboring paths: the 13 target paths. Neighbors read: `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `src/BellTower/Sessions/CodexPermissionPump.cs`, `src/BellTower/Core/ConversationLifecycle.cs`, `src/BellTower/Providers/CodexSharedEndpoint.cs`, `src/BellTower/Providers/CodexSharedAccess.cs`, `src/BellTower/Core/NativeNameSchedule.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/IO/CodexRuntimeStore.cs`, `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/Diagnostics/CaptureHealthReporter.cs`
- Baseline commit: 0b11026
- Implementation identity (before): HEAD 0b11026b2f93edc909da1ea7c17931c30a947155; manifest M `docs/compatibility.md`, `docs/session-identity.md`, `docs/session-titles.md`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`, `tests/BellTower.Tests/Diagnostics/ConfirmationCoverageTests.cs`, `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`, `tests/BellTower.Tests/Sessions/IdentityHistoryTests.cs`; ? `evidence/implementation-041.md` (630645d4…c0f4), `evidence/implementation-042.md` (cb1c220b…3113), `evidence/implementation-043.md` (be6d6275…a13); tracked diff SHA-256 aafff4d2de5a27abad8b6de111fc535c4010cedbb80f5a2d8b36590e3976b679; summary `head=0b11026 baseline=0b11026 files=13 diff=aafff4d2de5a untracked=3/3c21cdd5b8ff` (matches the expected identity)
- Before/after identity: after the analysis the same command printed `head=0b11026 baseline=0b11026 files=13 diff=aafff4d2de5a untracked=3/3c21cdd5b8ff`. They are identical, with no unexpected mutation. `git status` showed no new tracked change from the isolated restore and build.
- Prior reports / rechecked findings: [analysis 030](analysis-030.md) ANALYSIS-3000, ANALYSIS-3001

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-3000 text (Claude) | Read the direct-clients and shared-backend Claude cells in source and docs; `rg "always direct\|existing or remote\|existing/remote"` over `src`, `docs`, `vscode`, `README.md` | `CapabilityMatrix.cs` lines 72 and 75; `docs/compatibility.md` lines 69 and 70 | The two cells now give one account: no known shared-backend mode, with the interactive shape a stated assumption. The status word is `unverified`, as before. No stale wording is left in product sources or docs |
| ANALYSIS-3001 selection reuse (Codex) | Traced `ConfirmationCoverageReporter.CodexReport` → `CodexRuntimeOpener.Select`; compared with `CodexPermissionPump.VerifyAsync` line 106 | Reporter lines 49-51; opener lines 41-60; pump line 106 | The count calls the same `Select` the pump calls. It has no copy of the rule (attachment not ended, owned runtime with matching shell, or shared conversation with the classifier-proven `shared-backend` mode). When task 4.2 moves the gate to binder evidence, doctor follows without an edit. No second policy |
| How evidence and home are obtained | Read `Select`, `AttachmentStore.Get`, `CodexRuntimeStore.Get`, `CodexSharedEndpoint.DefaultHome`, the `CodexSharedAccess` and `NativeNameSchedule` constructors | See the ANALYSIS-3001 row in Re-verification | The attachment and its evidence come from the attachment file. The tray home is resolved only for shared conversations (an environment variable plus the profile folder, no file I/O). The doctor builds its own opener, and its `CodexSharedAccess` only allocates dictionaries. `Select` never connects |
| I/O and performance in doctor | Source inspection (hypothesis, not measured) | Per active Codex session: one more small JSON attachment read. The runtime record read, two process probes and the stop-marker check were already there. `CaptureHealthReporter` lines 54-60 already read every active session's attachment twice per doctor run | Negligible (O(active Codex sessions) small-file reads). No sockets, waits or backoff state. Failure exposure is not new, because the same unguarded `AtomicFile.Read` on attachment files already runs earlier in doctor. Measurement plan if wanted: time `doctor` with 50 synthetic Codex sessions, before and after. Not performed |
| Coverage texts vs matrix and code | Read `Policy`, `Configured`, `Status` against matrix rows 101-105, `CodexNameModes`, task 4.2 and `docs/compatibility.md` lines 48 and 80 | "shared conversations not yet reached by the task 4.2 binder" matches the code: an unclassified shared conversation (unknown mode) gets no access, and the new test pins it | Consistent with the matrix row and the code. One residual is outside the assigned fields (ANALYSIS-3100) |
| Claude coverage | Diff of `ClaudeReport` (none); Claude clause of `Policy` (unchanged); `ClaudeCoverageIsUnchangedByCodexBackendSelection` | Only the Codex clause of the shared `Policy` string changed | Unchanged |
| Tests | Read the three new `ConfirmationCoverageTests` and the new `CapabilityMatrixTests` assertions | The mixed test covers owned, shared-backend, unclassified-shared and plain sessions (2 verifiable, 2 unavailable). The single shared test has no unavailable count and no "unavailable" status. `always direct` is asserted absent from the matrix and the docs | Adequate for the change. Developer mutants 1 and 2 are accepted as developer evidence only, because ownership forbids source edits |
| Build and tests | `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-031 --filter "FullyQualifiedName~ConfirmationCoverageTests\|FullyQualifiedName~CapabilityMatrixTests\|FullyQualifiedName~CodexPermissionPump\|FullyQualifiedName~CodexSharedRuntimeServiceTests"` | 37 passed, 0 failed | Passed. Output stayed in the isolated, ignored folder |
| Repeat | Same assembly, `ConfirmationCoverageTests\|CapabilityMatrixTests`, 3 runs | 19/19 each run (260-274 ms) | Passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <7 changed .cs>` | exit 0 | Passed |
| Full suite, mutation, native | not run | Proportional recheck. Ownership forbids source edits, and the assignment forbids launching providers | Not performed. The `CodexSharedAccessTests` failure that DEV-043 reported is in `Providers/`, outside this diff, and was not rechecked |

Client coverage: the wording checks cover the Claude cells and Claude coverage. The selection and count checks cover Codex. The mixed test runs owned, shared and unreachable Codex sessions together, and the Claude-unchanged test runs a Claude session.

## Findings

### ANALYSIS-3100: Doctor `codex.approvalResolution` and the confirmer sentence of the human-wait prose still name only the owning runtime as the confirming status source

- Severity: warning. It is not blocking: the text understates a supported path and claims nothing stronger than the evidence. It is a residual of ANALYSIS-3001, and DEV-043 did not cause it.
- Related task / requirement: 3.4 (status matrix reconciled), 5.2 (doctor surfaces), 5.3 (compatibility docs); AGENTS.md (diagnostics honest)
- Locations: `src/BellTower/Diagnostics/DiagnosticsReporter.cs` line 34 (`ApprovalResolution`, unchanged baseline text); `docs/compatibility.md` line 48 ("or, for managed Codex sessions, by the verified owning runtime's `waitingOnApproval` status")
- Observation: the pump confirms a human wait from a shared-backend status read too. `VerifyAsync` takes `Shared` access from `Select` and calls `SubmitConfirmation`, and the corrected matrix row (`CapabilityMatrix.cs` line 105, docs line 80) and the new coverage count now say so. The same doctor output still prints `approvalResolution` = "verified owning-runtime waitingOnApproval status confirms a human wait … without a verified runtime … the request is never confirmed". A shared conversation has no owning runtime, so a reader concludes it is never confirmed. Line 48 now says in one sentence that a verified shared-backend conversation "is counted as verifiable", while an earlier sentence of the same paragraph still lists only managed sessions' owning runtime as a confirmer.
- Proposed improvement and benefit: name both sources in the two places, for example "the verified owning runtime's or, for a verified shared-backend conversation, the backend's `waitingOnApproval` status", and "without a verified owning runtime or shared backend". Leave `CodexConfirmationEvidence` as it is, because it lists natively trialed evidence and is accurate. Doctor and the docs then give one account.
- Behavior-preservation risks: text only. No test or `vscode/` consumer parses `approvalResolution`. A quick check found only doctor output.
- Evidence type: source observation
- Verification / measurement plan: `rg -n "verified owning-runtime waitingOnApproval|by the verified owning runtime's" src docs` returns nothing, or each match also names the shared backend. The doctor and matrix tests still pass.
- Relationship: residual of ANALYSIS-3001 (same text-versus-selection class, a sibling doctor field). It can go to task 5.2 or 5.3.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3000 | `head=0b11026 … diff=aafff4d2de5a untracked=3/3c21cdd5b8ff` | The direct-clients Claude cell (`CapabilityMatrix.cs` line 75, docs line 70) reads "treated as direct (no known shared-backend mode; the interactive shape is a stated assumption, see the shared backend row)". This matches the shared-backend cell (line 72, docs line 69) and the task 5.3 text. The status stays `unverified`, and `sync-native-session-titles` and "not a provider restriction" remain. `rg "always direct"` over product sources and docs returns nothing. `CompatibilityDocumentStatesMatchTheMatrixSource` asserts its absence in the matrix and the docs and passes. | verified |
| ANALYSIS-3001 | same | Count: `CodexReport` uses `CodexRuntimeOpener.Select`, the same method the pump calls (pump line 106). There is no duplicate predicate. Owned access requires a live attachment, a live runtime record and the same shell. Shared access requires `IsSharedConversation` and the classifier-proven mode, with the home from `DefaultHome` only on that branch. Both match the pump exactly. Texts: `Policy`, `Configured`, `Status`, the docs line-48 sentence and matrix row 105 / docs line 80 all say "neither a verified owning runtime nor a verified shared backend (explicit --no-daemon, remote, and shared conversations not yet reached by the task 4.2 binder)". This matches the code, because an unclassified shared conversation gets no access. Doctor cost: one more small attachment read per active Codex session, with no connection (a hypothesis, not measured). Tests: the 3 new tests pass in 3 repeats, and the mixed test separates all four session kinds. Claude: `ClaudeReport` and the Claude clause of `Policy` are unchanged, and the new Claude test passes. Residual: two sibling texts still name only the owning runtime as the confirmer (ANALYSIS-3100, not blocking). | verified for the assigned surfaces (`confirmationCoverage`, the line-48 coverage sentence); residual in ANALYSIS-3100 |

Regression from the fix: none found. The count is now stricter for owned sessions: an owned runtime whose attachment is missing, has ended or shows a different shell no longer counts. That matches the pump and is correct. The new `using BellTower.Sessions` in `Diagnostics` follows precedent, since `CapabilityMatrix` and `DiagnosticsReporter` already reference `Sessions`.

## Assessment and limitations

- Blocking defects: none. ANALYSIS-3000 and ANALYSIS-3001 are corrected as assigned. The coverage count reuses the single selection rule and creates no second policy. The Claude cells are honest and consistent. Claude coverage is unchanged.
- Not blocking: ANALYSIS-3100 (warning, text only). The `approvalResolution` field and the confirmer sentence of `docs/compatibility.md` line 48 still name only the owning runtime. They understate the shared-backend confirmation path that the matrix and the coverage count now report. It is suitable for routing to 5.2 or 5.3, since this was the last fix cycle.
- Performance: hypothesis only (one extra small file read per active Codex session in doctor, with no network). Not measured.
- Checks: an isolated build and targeted tests (37 passed), 3 repeat runs (19/19), and a verify-only format check (passed). Not run: the full suite, mutation testing and native checks.
- Unexpected writes: none, apart from ignored build output under `artifacts/analysis-031`. The review identity was the same before and after.
- Decisions needed: route ANALYSIS-3100 to task 5.2 or 5.3, or accept it as a two-sentence text fix outside the fix-cycle budget.
