<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-029 / code-analyzer; Client: Claude Code 2.1.318 delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

The task 3.4 change has one honesty defect, a warning (ANALYSIS-2900), plus four suggestions (ANALYSIS-2901 to 2904). No operation is newly called restricted or complete. The Claude native-title write stays unfinished, which agrees with `sync-native-session-titles` (not started). The defect: the matrix row, doctor `codex.nameSynchronization` (`CapabilityMatrix.CodexNameModes`) and `docs/compatibility.md` all say a conversation of another Codex home "is not read through the tray-home backend". The code does read it there: `CodexRuntimeOpener.cs` line 59 returns `new CodexAccess.Shared(home())` for every shared-backend conversation, without comparing the conversation's own home. Task 4.2 itself says these conversations are currently read through the tray-home backend.

By source, no observer read path logs or persists preview, turn, message or token fields. This covers owned and shared access, the name service, the permission pump, the native-sync CLI and the owned runtime host output. The discard test's canary scan has no timing dependence, because logging is synchronous and the pump is awaited.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.318 (Agent SDK subagent) / claude-opus-5-5
task / finding IDs: task 3.4 (DEV-041); ANALYSIS-2900 (warning), ANALYSIS-2901, ANALYSIS-2902, ANALYSIS-2903, ANALYSIS-2904 (suggestions)
changed or report paths: none in the repository; report text inline below (write refused); ignored build output only under D:\Code\ZeusApps.BellTower\artifacts\analysis-029
checks: dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-029 --filter "FullyQualifiedName~CapabilityMatrixTests|FullyQualifiedName~CodexSharedRuntimeServiceTests" -> passed (28/28); same with filter CapabilityMatrixTests|ObserverReadsDiscardPreviewsTurnsAndTokens --no-build x5 -> passed (13/13 each run); dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <4 changed .cs> -> passed; full suite -> not run (proportional scope); native checks -> not run (assignment forbids provider launch)
target identity: before head=0b11026 baseline=0b11026 files=8 diff=f8f6c76eba3a untracked=1/4ed242f74d2f; after identical
limitations / decisions needed: report write refused by the client, full text inline below for the orchestrator to save at openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-029.md; decide whether ANALYSIS-2900 is fixed in text now (recommended) or by a Select gate in task 4.2; decide whether the Claude transcript-read content property (ANALYSIS-2904) belongs in 3.4 or a follow-up; the timing concern in ANALYSIS-2903 is a hypothesis, not measured under load
next: orchestrator saves the report and triages ANALYSIS-2900..2904; developer corrects the matrix, CodexNameModes and doc text for ANALYSIS-2900
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-029 (DEV-041, task 3.4)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.318, Agent SDK subagent, non-interactive delegated mode / claude-opus-5-5
- Date: 2026-11-05T18:58Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-029
- Task and requirement references: [tasks.md](../tasks.md) 3.4 (with 4.2, 5.2, 5.3 for scope boundaries); [design.md](../design.md) decisions 2, 4 and 7; AGENTS.md parity rules; [implementation 041](../evidence/implementation-041.md); `openspec/changes/sync-native-session-titles/README.md` (read only)
- Scope and relevant neighboring paths: `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`, `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`, `docs/compatibility.md`, `docs/session-identity.md`, `docs/session-titles.md`, evidence 041. Neighbors read: `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `CodexNameService.cs`, `CodexPermissionPump.cs`, `CodexAccessStatus.cs`, `CodexRuntimeHost.cs`, `src/BellTower/Providers/CodexConnection.cs`, `CodexSharedAccess.cs`, `CodexSharedTransport.cs`, `src/BellTower/IO/LocalLog.cs`, `src/BellTower/Core/NativeNamePolicy.cs`, `src/BellTower/Cli/NativeSyncCommand.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs`, `tests/BellTower.Tests/Testing/TestWorkspace.cs`
- Baseline commit: 0b11026
- Implementation identity (before): HEAD 0b11026b2f93edc909da1ea7c17931c30a947155; manifest M `docs/compatibility.md`, `docs/session-identity.md`, `docs/session-titles.md`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`, `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs`; ? `evidence/implementation-041.md` (630645d4…c0f4); tracked diff SHA-256 f8f6c76eba3a289d48b3537db446540568322f1917acefc8ba846df50842b34d; summary `head=0b11026 baseline=0b11026 files=8 diff=f8f6c76eba3a untracked=1/4ed242f74d2f`
- Before/after identity: after the analysis the same command printed `head=0b11026 baseline=0b11026 files=8 diff=f8f6c76eba3a untracked=1/4ed242f74d2f`; identical, no unexpected mutation.
- Prior reports / rechecked findings: none rechecked (new analysis).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Matrix honesty (both providers) | Read every new or changed row and `CodexNameModes` against the opener, name service, pump, access-status labels and transport | `CapabilityMatrix.cs` lines 33-39, 66-84, 102; `CodexRuntimeOpener.cs` line 59 | No operation is called restricted. One unfinished state is described with a mechanism the code does not have (ANALYSIS-2900) |
| "not applicable" usage | Checked each Claude `not applicable` cell against a mode that Claude Code really lacks | Owned runtime, shared backend, unreached shared conversations, refused shared threads, observer read rows | Correct for the runtime-protocol surfaces. The content-handling property for Claude transcript reads is not stated anywhere (ANALYSIS-2904) |
| Claude native title vs `sync-native-session-titles` | Compared the matrix, doctor `claude.nameSynchronization`, `NativeNamePolicy.ClaudeWriteStatus` and docs with that change's README | README: not started, no proposal | Consistent: unverified (unfinished), not a provider restriction, never complete |
| Single source / duplicated policy | Traced doctor text sources; grepped for label constants and selection logic in the diagnostics code | `DiagnosticsReporter.cs` lines 25 and 33; `CodexAccessStatus` constants interpolated in the matrix | No selection, refusal or discard policy is reimplemented. `CodexNameModes` is a second prose account of the per-mode states, and the docs hard-code label strings. The first-word test catches neither (ANALYSIS-2901) |
| Test quality: backing-citation reflection test | Read `SupportedModeRowsNameBackingTestsThatExist` | Test lines 200-218; rows 69 and 72 cite only the discard test | Proves the cited method exists, not that it backs the row's claims (ANALYSIS-2902) |
| Test quality: doc agreement test | Compared the doc's matrix-section rows with the source rows; checked for duplicate first cells across all doc tables | Row sets are identical apart from the header and separator; no duplicate first cells in the 49 table lines | Sound for its stated scope (status words only) |
| Test quality: refusal-label test | Read the test against `CodexAccessStatus.For` | Every `CodexTransportFailure` is enumerated; the shared labels are distinct; `\bsupported\b` correctly lets "unsupported" through | Sound. Covers shared access only; owned access maps to constants already in the list |
| Content discard, by source | Traced every observer read path: `CodexConnection.ReadThreadAsync` / `ExchangeAsync`, the name service, the permission pump, the native-sync CLI, the shared transport `VerifyInitialized`, owned runtime host stdout and stderr | `LocalLog.Write` takes a sanitized code only; transport and access exception messages are fixed text; the name service persists only `desired` and constant statuses; the pump keeps booleans only; `Pause` does not store the external name; the CLI prints `NativeNamePolicy.StatusFor`; owned stdout and stderr are copied to `Stream.Null` | No path logs or persists preview, turn, message or token fields. Response paths the test does not cover are listed in ANALYSIS-2903 |
| Discard test: completeness and timing | Read the test, the stand-in server and `TestWorkspace` | The stand-in ignores `includeTurns` and returns turns, so the canaries reach the client (the test is not vacuous); `LocalLog` writes synchronously; `RefreshAsync` is awaited; the scan covers `workspace.Root` recursively | The scan has no timing dependence. The outcome assertions depend on 2-3 s budgets and shared per-home backoff (performance or flakiness hypothesis in ANALYSIS-2903) |
| Build and tests | `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --artifacts-path artifacts/analysis-029 --filter "FullyQualifiedName~CapabilityMatrixTests\|FullyQualifiedName~CodexSharedRuntimeServiceTests"` | 28 passed, 0 failed | Passed. Isolated ignored output folder |
| Repeat run | Same filter narrowed to `CapabilityMatrixTests` and `ObserverReadsDiscardPreviewsTurnsAndTokens`, `--no-build`, 5 runs | 13/13 passed each run (398-517 ms) | Passed on an idle machine; not run under load |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <4 changed .cs files>` | exit 0 | Passed |
| Native checks | n/a | No provider launched (assignment) | Not performed |

Client coverage: the matrix and docs checks cover both Claude and Codex cells. The content-discard tracing and tests cover Codex only, the only provider with an observer runtime read. The Claude transcript-read analogue is in ANALYSIS-2904.

## Findings

### ANALYSIS-2900: Matrix, doctor and docs say another-home conversations are not read through the tray-home backend, but the opener reads them there

- Severity: warning
- Related task / requirement: 3.4 (no missing operation silently described as other than it is), 4.2 (ANALYSIS-2403), design decision 4
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs` line 37 (`CodexNameModes`, printed by doctor `codex.nameSynchronization`) and line 78; `docs/compatibility.md` lines 71 and 89; selection at `src/BellTower/Sessions/CodexRuntimeOpener.cs` line 59
- Observation: `CodexRuntimeOpener.Select` returns `new CodexAccess.Shared(home())` for every conversation whose evidence mode is `shared-backend`. `home()` is the tray's resolved Codex home, and no per-conversation home is recorded or compared. `HomeMismatch` in `CodexSharedTransport.cs` lines 106-119 compares the backend with the resolved home, not with the conversation's own home. So a shared conversation of another Codex home is read (`thread/read`, and a `thread/name/set` would follow a successful read) through the tray-home backend. Task 4.2 says this directly ("instead of being read through the tray-home backend"). The matrix row instead says such a conversation "is not read through the tray-home backend; both report no access", `CodexNameModes` says "no shared access until task 4.2", and the compatibility closing-evidence bullet repeats it. The status the user sees is also not "no access". A foreign thread id presumably makes the tray-home backend answer with a JSON-RPC error, giving `unavailable: shared backend rejected the request`, or the mismatch or unverified labels if it answers with another thread. What a real backend returns for a thread of another home is unverified.
- Proposed improvement and benefit: correct the three texts to describe the actual mechanism. For example: "a conversation of another Codex home is still read through the tray-home backend, which does not host it; the backend's answer is unverified and expected to surface as a shared-backend refusal; no name is written unless that read succeeds for the exact thread id; per-conversation home routing is task 4.2". The matrix then states what the code does, as task 3.4 requires. If the orchestrator would rather make the text true, the alternative is a gate in `Select`, which is product policy and belongs to 4.2 (ANALYSIS-2404), not to this descriptive task.
- Behavior-preservation risks: text-only fix; `PerModeNativeNameAndStatusStatesAreHonestForBothProviders` asserts "task 4.2" appears in both texts and must still hold.
- Evidence type: source observation (the backend's actual answer for a foreign thread is unverified)
- Verification / measurement plan: an opener test with a `shared-backend` attachment whose session id the stand-in does not host. This needs a stand-in mode that answers unknown threads with an error rather than the generated `name-of-<id>` it returns today. Assert the status label and that no `thread/name/set` is recorded. Then reread the three texts against that label.
- Relationship: related to ANALYSIS-2403 and ANALYSIS-2404 (task 4.2); not a duplicate

### ANALYSIS-2901: The per-mode states have two prose sources and docs hard-code labels; the agreement tests do not tie them together

- Severity: suggestion
- Related task / requirement: 3.4 (no duplicated policy, one source), 5.2
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs` lines 33-39 (`CodexNameModes`) and the per-mode rows at lines 66-84; `docs/compatibility.md` lines 69-73 (literal `unavailable: …` labels); `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs` `CompatibilityDocumentStatesMatchTheMatrixSource`
- Observation: doctor prints both `capabilityMatrix.rows` and `codex.nameSynchronization`, and each restates the same per-mode statuses in separate prose. The tests check that `CodexNameModes` contains the two interpolated labels and the strings "task 4.2" and "task 6.1". They do not check that its per-mode status words match the rows. For example, the shared-conversation row could become `supported` when 4.2 lands while `CodexNameModes` still says "unverified, no shared access", and every test would still pass once the "task 4.2" assertion is edited. The doc cells quote the label strings literally (`unavailable: no verified dedicated runtime`, `unavailable: fork or delegated thread mapping unverified`, `unavailable: shared backend runs elevated`); a rename of a `CodexAccessStatus` constant would leave the doc stale, and the first-word test cannot see it. Separately, the elevated-backend refusal is bundled into an `unverified` row. Design decision 4 already records it as a scope decision (orchestrator default, ANALYSIS-2302), so the status word describes the fork half of the row, not the elevated half.
- Proposed improvement and benefit: either build `CodexNameModes` from the per-mode rows (surface name plus `Codex.Status` plus a short reason) so the status words have one source, or add one assertion per mode that the matching `CodexNameModes` segment contains that row's `Codex.Status`. Extend the doc agreement test so every `CodexAccessStatus` label quoted in a doc cell equals a current constant (for example, every backticked `unavailable: …` string in the matrix section must be one of the labels `CodexAccessStatus.For` can produce). Consider splitting the elevated-backend case into its own row or stating its status as a scope decision, so `unverified` describes only unfinished work.
- Behavior-preservation risks: doctor JSON text changes if `CodexNameModes` is derived. It is free text, so consumers that parse it are unlikely; verify none in `vscode/` or tests.
- Evidence type: source observation
- Verification / measurement plan: mutation. Flip the shared-conversation row to `supported` and rename `CodexAccessStatus.NoRuntime`; the extended tests should fail and the current ones do not.
- Relationship: none

### ANALYSIS-2902: The backing-test reflection check proves a citation exists, not that it backs the row

- Severity: suggestion
- Related task / requirement: 3.4 ("no missing operation silently called … complete"); matrix legend "supported = implemented with automated coverage"
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs` lines 69 and 72; `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs` lines 200-218
- Observation: the owned-runtime and shared-backend rows each cite only `CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens`, a content-discard test. Their other claims are backed by different tests that already exist in the same class: the pause policy (`NativeUserTitleOnASharedConversationPausesBellTowerWrites`), exact-thread writes (`SharedConversationNameIsWrittenThroughTheBackendToThatThreadOnly`), exact-conversation work status (`PermissionStatusComesFromTheExactConversationOnASharedBackend`) and "only the connection is closed / only the owned manager ends the runtime" (`DisposingAnObserverConnectionLeavesBothBackendsServingTheNextOne`). None of these is cited. The reflection test checks only the three hard-coded `ModeSurfaces` indices, so a new `supported` row elsewhere is unchecked. It accepts any public method of that name, attribute or not. A class-only citation passes without a method, which evidence 041 mutant 6 hit before the fix.
- Proposed improvement and benefit: cite the tests that back each claim in those two rows. Make the reflection test require at least one `Class.Method` citation per checked row, and require the method to carry `[Fact]` or `[Theory]`. Optionally apply it to every Codex or Claude `supported` row that contains a citation, rather than to fixed indices. The `supported` status then stays traceable to coverage of the specific claims.
- Behavior-preservation risks: text and test only.
- Evidence type: source observation
- Verification / measurement plan: rename `DisposingAnObserverConnectionLeavesBothBackendsServingTheNextOne` after citing it; the test should fail. Cite a non-test helper method; the attribute check should fail.
- Relationship: none

### ANALYSIS-2903: The discard test covers success, pause, refusal and mismatch bodies, but not error, initialize, notification or malformed messages; outcome assertions depend on budgets and shared backoff

- Severity: suggestion
- Related task / requirement: 3.4 (observer reads discard previews, transcript contents and tokens), design decision 4
- Locations: `tests/BellTower.Tests/Sessions/CodexSharedRuntimeServiceTests.cs` lines 389-436; `src/BellTower/Providers/CodexConnection.cs` `ExchangeAsync` (skipped messages, error branch) and `ConnectAsync`; `src/BellTower/Providers/CodexSharedTransport.cs` `VerifyInitialized`
- Observation: by source, every response path is safe today. Error responses raise fixed-text exceptions, skipped (notification or other-id) messages are parsed and dropped, `VerifyInitialized` keeps only a home comparison, `LocalLog` accepts a sanitized code, and the owned runtime's stdout and stderr go to `Stream.Null`. The test puts canaries only into `thread/read` and `thread/name/set` result bodies, so it would not catch a future change that logs or stores:
  - (a) the `error.message` or `error.data` of a JSON-RPC error;
  - (b) the initialize result;
  - (c) an interleaved notification, which on a shared backend could carry agent or turn deltas for a subscribed thread;
  - (d) the raw text of a malformed message.
  The status-text check covers `NativeSyncStatus` and the serialized pump sessions. Doctor and `list` output are covered only indirectly, through the file scan of the data directory they read. The scan itself has no timing dependence: logging is synchronous and the pump is awaited. Hypothesis (not measured): under heavy parallel test load, one shared open exceeding the 2 s `Quick` budget records a per-home failure, and the following sessions in the same test become `BackedOff`. Their status is then left unchanged, so `Assert.Equal("synchronized", …)` fails. That is a cascading flake rather than a single slow assertion. Five idle repeat runs passed (398-517 ms).
- Proposed improvement and benefit: extend the same theory with a stand-in mode that sends one interleaved notification and one JSON-RPC error carrying canaries (and, for the shared case, canaries in the initialize result beyond `codexHome`). Keep the existing scan. The content-discard claim in the matrix then covers every message the observer receives, not only result bodies. For the timing hypothesis, either give this test a budget that matches production `CodexTransportBudget.Shared`, or assert per-session outcomes only after confirming that `access.Failures(home) == 0` (when the precondition fails, the message points to load, not to discard).
- Behavior-preservation risks: test only; the stand-in server is shared by other tests, so add the modes without changing defaults.
- Evidence type: source observation; timing is a performance hypothesis, not measured
- Verification / measurement plan: mutation. Make `ExchangeAsync` include `error.message` in the `IOException` text, and have the name service log `exception.Message` through a new code-plus-text path; the extended test should fail. For timing, run the class 20 times under a CPU-saturating background load (for example, the full test suite in parallel) and count failures, before and after the budget change.
- Relationship: none

### ANALYSIS-2904: The Claude "not applicable" cell for content handling leaves the Claude transcript-read content property unstated

- Severity: suggestion
- Related task / requirement: 3.4 ("both providers"), AGENTS.md (no unexplained parity gaps; keep the matrix honest about unverified behavior)
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs` lines 82-84; `docs/compatibility.md` line 73; evidence 041 "Limitations"
- Observation: `not applicable` is accurate for the surface as named: Claude Code has no observer runtime read. The analogous Claude operation is the transcript read for native titles and history work evidence, which reads prompt-bearing transcripts. Its content handling is neither claimed nor tested; evidence 041 says so, but no matrix row does. Hook payload discard is tested for both providers (`ProviderTests` lines 60-75, `CliTests` line 186). Transcript reads have no matched canary test. As written, a reader of the matrix sees the property as covered for Codex and not relevant for Claude.
- Proposed improvement and benefit: add either a Claude-side state (for example, a "Transcript read content handling" row, `unverified` for Claude until a matched canary test exists, or `supported` with that test), or a sentence in the Claude cell naming the transcript read and its status. The orchestrator decides whether this belongs in 3.4 or a follow-up. Either way, the gap becomes explicit instead of implied by `not applicable`.
- Behavior-preservation risks: matrix text only, or a new test; the doc agreement test requires the new row in `docs/compatibility.md`.
- Evidence type: source observation
- Verification / measurement plan: a canary test that feeds a disposable Claude transcript with prompt, assistant and tool-result canaries through the history and native-title readers, then scans the data directory and serialized identities, like the Codex test.
- Relationship: evidence 041 limitation 1 (proposal, not a finding)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not performed (new analysis) |

## Assessment and limitations

- Scope covered: all eight scoped files and the observer read paths listed above, for both providers' matrix cells. No new product policy is added: the change is descriptive rows, one constant and tests, and doctor now reports `CodexNameModes` instead of a stale literal. No row is newly called `restricted`, and the Claude native-title write stays unfinished, consistent with `sync-native-session-titles` (not started). The one honesty defect is ANALYSIS-2900 (warning). ANALYSIS-2901 to 2904 are test-strength and drift suggestions.
- Content discard: by source, no observer path (owned or shared, name service, permission pump, native-sync CLI, owned runtime host output) logs or persists preview, turn, message or token fields. The only provider text kept from a response is the name, used for comparison (not persisted on pause), plus the work-status booleans.
- Performance hypothesis (not measured, outside this descriptive task): each shared observer read opens a new peer-verified connection (process lookup, image, command line and elevation checks). This happens per name verification (every 10 s per session) and per pump check (every 1 s per waiting session). The matrix phrase "one peer-verified connection" means one per read. Measure open latency and the process-lookup cost per refresh with 5-10 shared sessions before deciding whether a connection cache is warranted (task 4.2 already plans refusal caching, ANALYSIS-2405).
- Checks: an isolated build into the ignored `artifacts/analysis-029`, the targeted tests (28 passed; repeat 5 x 13 passed) and a verify-only format check of the changed C# files (passed). The full suite and native checks were not run.
- Unexpected writes: none. The report write was refused by the client, so this text is returned inline. The only file output is ignored build output under `artifacts/analysis-029`. The review identity was unchanged before and after.
- Decisions needed: whether ANALYSIS-2900 is fixed in text now (recommended) or by a `Select` gate in 4.2; and whether the Claude transcript-read content property (ANALYSIS-2904) belongs in 3.4 or a follow-up.
