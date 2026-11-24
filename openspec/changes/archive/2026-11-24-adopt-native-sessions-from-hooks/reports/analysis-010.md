<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-010 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-010

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent); CLI version not probed (launching the provider CLI is out of bounds); role-default mode; `claude-opus-5-5`
- Date: 2026-11-04T08:50Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-010 (review of DEV-023)
- Task and requirement references: task 2.2 remainder (QA-1304), user-approved SDK pin change; [implementation-023](../evidence/implementation-023.md)
- Scope and relevant neighboring paths: `global.json`, `docs/compatibility.md`, `tests/BellTower.Tests/BellTower.Tests.csproj`, `tests/BellTower.Tests/Fixtures/native/*`, `tests/BellTower.Tests/Providers/NativeReplayTests.cs`, `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs`. Neighbors read: `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs`, `tests/BellTower.Tests/Testing/{BuiltApplication,TestWorkspace,HookSequence,IdentityFixtures}.cs`, `tests/BellTower.Tests/Fixtures/{hooks,identity}/`, `tests/BellTower.Tests/Providers/CaptureTests.cs`, `tests/BellTower.Tests/Setup/PowerShellModuleTests.cs`, `src/BellTower/Providers/{AgentModeClassifier,HookIngestor}.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower.Ingress/Program.cs`, `src/BellTower/Setup/HookCommandBuilder.cs`
- Baseline commit: `b6c0b45`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline b6c0b45 -Paths src,tests,tools,global.json,docs/compatibility.md,openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-023.md` -> HEAD `b6c0b452025f`, 3 modified (`docs/compatibility.md`, `global.json`, `tests/BellTower.Tests/BellTower.Tests.csproj`), 8 untracked (implementation-023, five native fixtures, two test files), tracked diff SHA-256 `e1b1b94c7a14...`, summary `head=b6c0b45 baseline=b6c0b45 files=11 diff=e1b1b94c7a14 untracked=8/111df6b0cbfd`
- Before/after identity: after the analysis the same command printed the identical summary line; HEAD unchanged. No unexpected mutation in scope. Outside scope, `reports/qa-018.md` appeared during the review (concurrent QA work, not written by this analyzer).
- Prior reports / rechecked findings: none rechecked; this is a first review of DEV-023.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| SDK pin (config) | Diff of `global.json`, `docs/compatibility.md`; `dotnet --version`, `dotnet --list-sdks`; grep for pin consumers | `rollForward: disable` kept; repo root resolves 10.0.114; CI reads `global-json-file: global.json`; `docs/validation.md` and `docs/distribution-validation.md` keep 10.0.113 as historical records | Correct; no finding |
| csproj fixture item | Inspection | `Fixtures/native/*` mirrors the existing `hooks/*` and `identity/*` items | Consistent; no finding |
| Duplication | Compared new tests with `NativeAdoptionTests` and `Testing/*` | Replay tests repeat the HookIngestor and capture-path cases already in `NativeAdoptionTests` with an injected classification | ANALYSIS-903 |
| Determinism and process lifetime | `dotnet build -c Release --no-restore` (up to date), then `dotnet test ... --filter "FullyQualifiedName~NativeReplayTests\|FullyQualifiedName~NativeAgentResolveProcessTests"` 3 times; then each class alone while sampling `ping.exe -n 60` processes | 12/12 passed every run. Replay class alone: 7 tests in 275 ms but `dotnet test` wall time 62 s, with an orphaned `ping.exe` whose parent cmd had been killed. Resolve class alone: 5 tests, 3 s wall time, no orphans | ANALYSIS-901, ANALYSIS-906 |
| Reliance on built `bell-tower.exe` | Inspection of `BuiltApplication` and the test csproj | Path matches the configuration (`output.Parent.Name`), and the `BellTower` ProjectReference builds it; this follows the pattern of `IdentityControlCliTests`, `ConsoleTitleProcessTests`, `PowerShellModuleTests` | Acceptable; failure diagnosability covered in ANALYSIS-906 |
| Flakiness under load | Inspection + measurement | 20 s timeout against ~1 s for 5 rows; nearest-ancestor match (`FirstOrDefault`) keeps the copied image ahead of any real `claude.exe`/`codex.exe` that runs the test. Host shell/window come from the runner's ancestry but are not asserted | Low risk; no finding beyond ANALYSIS-906 |
| Codex `exec` restriction claim | Probe: copied `cmd.exe` to disposable `codex.exe`/`claude.exe` in the scratchpad and ran `exec /c ...`, `e /c ...`, `exec --json /c ...`, `-p /c ...`, `--print /c ...`, `app-server /c ...`; classified command lines by loading the built `bell-tower.dll` and calling `AgentModeClassifier.Arguments`/`Classify` | Every form ran its `/c` command and exited 0. Current form with marker `exec` still classifies `embedded-runtime`; `exec /c ...` gives `headless`; `review /c ...` gives `unknown` | ANALYSIS-900 (measured) |
| Drift-safeguard readiness | Compared fixture shape with the `hooks/` and `identity/` conventions and with what the ingress records | No provider version, no provenance label, single-entry-per-provider assumptions, and the classifier input is missing | ANALYSIS-902 |
| Parity / production path | Inspection of `HookCommandBuilder`, `Ingress/Program.cs` | Fast hooks run `bell-tower-ingress.exe`; the new real-process rows exercise only `bell-tower.exe event`; the retained Claude capture shows a Claude agent nested under Codex | ANALYSIS-904, ANALYSIS-905 |
| Architecture | Against `docs/development.md` layers | Test-only change; no product code; no per-provider policy duplicated | No finding |

## Findings

### ANALYSIS-900: The Codex `exec` real-process row is feasible, and the current Codex embedded row ignores its marker

- Severity: warning
- Related task / requirement: 2.2 (QA-1304); AGENTS.md parity (a claimed restriction needs proof)
- Locations: `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:15-22,40`; `src/BellTower/Providers/AgentModeClassifier.cs:72-104`; implementation-023 "Limitations" first bullet
- Observation: implementation-023 says `/c` must be cmd.exe's first argument, so `codex exec` cannot be shaped. A probe on this machine (Windows 11 build 26300) shows otherwise: a copied `cmd.exe` named `codex.exe` ran its `/c` command and exited 0 for `exec /c ...`, `e /c ...` and `exec --json /c ...`, and a copy named `claude.exe` did the same for `-p /c ...`, `--print /c ...` and `app-server /c ...`. The current form also hides a weak row. In `/c echo <marker> >nul & ...`, the Codex classifier treats `/c` as the first positional token and returns `embedded-runtime` whatever the marker is. Classifying with the built `bell-tower.dll` gave `embedded-runtime` for marker `x` and also for marker `exec`. So the Codex "embedded" row cannot fail on the property it names. Claude rows are unaffected, because the Claude classifier scans every token.
- Proposed improvement and benefit: put the provider arguments before the switch: `Arguments = $"{providerArgs} /c \"{BuiltApplication.Executable}\" event --provider ..."`. Then add Codex rows for `exec` and `e` (headless, no host claimed), at least one fail-closed row such as `review` or `--future-option value` (unknown, then shared runtime, never claiming the process), and a Claude `-- -p` row (interactive). Codex coverage then matches Claude's, including the headless mode that is currently a parity gap, and the embedded row becomes able to fail. Keep the existing `{}` stdout and attachment assertions: they double as a self-check that cmd.exe still accepts leading tokens, so a future cmd.exe change fails loudly instead of passing vacuously. If that tolerance is ever judged too undocumented to rely on, the fallback is a tiny test-only stub executable that forwards stdin to its child. That costs a project, so use it only if needed.
- Behavior-preservation risks: test-only change. cmd.exe's acceptance of text before `/C` is observed behaviour, not documented in `cmd /?`; the self-check above guards against it changing.
- Evidence type: measured result (process probe and classifier invocation); recorded under "Analysis performed"
- Verification / measurement plan: run the class with the new rows. Mutation check: temporarily expect `embedded-runtime` for the `exec` row and confirm the test fails.
- Relationship: new; corrects the restriction stated in implementation-023

### ANALYSIS-901: The replay sleeper leaves an orphaned `ping.exe` that keeps `dotnet test` running for about 60 s

- Severity: suggestion
- Related task / requirement: 2.2 test infrastructure
- Locations: `tests/BellTower.Tests/Providers/NativeReplayTests.cs:41,47-55`; `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs:140-144` (shared `Sleeper`), callers in `NativeAdoptionTests.cs` and `NativeAdoptionReconcileTests.cs`
- Observation: `LiveAgent.Dispose` calls `process.Kill()` on the `cmd.exe /c ping -n 60` sleeper, which kills only cmd. Once ping has started, it survives as an orphan for 60 s while still holding the test host's inherited std handles. Measured: `NativeReplayTests` alone ran 7 tests in 275 ms, but `dotnet test` took 62 s, and an orphaned `ping.exe` was observed whose parent had already exited. The Resolve class alone took 3 s. `LiveAgent` is still better than the existing callers of `Sleeper()`, which use `using var agent = Sleeper()` and never kill at all.
- Proposed improvement and benefit: move one disposable sleeper into `Testing/` that starts `ping.exe` directly (no grandchild) or kills with `Kill(entireProcessTree: true)`, with redirected or non-inherited std handles. Use it from `NativeReplayTests`, `NativeAdoptionTests` and `NativeAdoptionReconcileTests`. Filtered runs would drop from about 60 s to a few seconds, and processes would stop outliving the tests.
- Behavior-preservation risks: tests that need a live identity keep one for their whole lifetime. If the sleeper is `ping.exe` itself, tests that check the image name must not assume `cmd.exe`; none of the reviewed callers do.
- Evidence type: measured result (wall time and process sampling)
- Verification / measurement plan: rerun the filtered replay class and compare wall time; sample `ping.exe -n 60` afterwards and expect 0.
- Relationship: new

### ANALYSIS-902: The native fixture format will need rework for the planned per-version drift safeguard

- Severity: warning
- Related task / requirement: 2.2; user decision 2026-11-04 (drift safeguards, saved real hooks refreshed per provider version)
- Locations: `tests/BellTower.Tests/Fixtures/native/*`; `tests/BellTower.Tests/Providers/NativeReplayTests.cs:20-31,60,64-66,72`; `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:62`
- Observation:
  - No provider version anywhere. The `hooks/` fixtures carry it in the name (`claude-2.1.306-...`, `codex-0.164.0-...`), and the retained captures came from Claude 2.1.310 and Codex 0.164.0, but the new names and records do not say so. Refreshing a version would overwrite the file and lose history.
  - No provenance label. The four payloads are reconstructions of recorded shapes, which is exactly what `Fixtures/identity/provenance.json` exists to label. Here the label lives only in a C# comment and in evidence, so a drift test cannot tell a verbatim capture from a reconstruction.
  - Single-entry assumptions. `For(provider)` uses `.Single(...)`, the coverage fact asserts exactly `["claude","codex"]`, and the Resolve test hard-codes the Codex session ID from the fixture. Adding a second capture for one provider breaks all three.
  - The classifier input is missing. The retained record keeps ancestor names and a boolean outcome, but not the agent's command-line arguments. `Classify` derives the mode from `capturedAgent != null` (Headless or Unknown) and feeds it to `NativeAdoption.Apply`, so the replay supplies the expected classification as its own input and cannot detect vocabulary drift. That drift is the main risk the safeguard targets. `Classify`'s `retained` parameter is also unused.
- Proposed improvement and benefit: settle the schema before more captures accumulate. Use one file per provider version and event, for example `native/<provider>-<version>-<event>.json`, holding `{provider, providerVersion, source: verbatim|reconstructed, payload, agentArguments (redacted), ancestorNames, expected: {mode, dedicated, lifecycleOwner}}`. Add one data-driven theory that enumerates the folder, runs `agentArguments` through `AgentModeClassifier.Classify`, and feeds the payload through `HookIngestor` and the capture path, asserting `expected`. The most faithful option is to store the redacted protocol-1 envelope that `bell-tower-ingress.exe` already writes, since it carries ancestry, and replay it through `StateStore.Drain`. A refresh is then "save a capture and add a file", with no test edits. Read the session ID in the Resolve test from the fixture instead of a literal.
- Behavior-preservation risks: test-only. Redaction of command-line arguments must follow the content hygiene rules (no paths or prompts).
- Evidence type: source observation
- Verification / measurement plan: add a dummy second-version file and confirm the suite stays green with no code edit; alter `agentArguments` in one file and confirm the theory fails.
- Relationship: new; ANALYSIS-903 is the duplication side of the same design

### ANALYSIS-903: The replay tests duplicate existing native adoption ingestion tests

- Severity: suggestion
- Related task / requirement: 2.2
- Locations: `tests/BellTower.Tests/Providers/NativeReplayTests.cs:76-139`; `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs:146-152,190-225`
- Observation: `ARetainedSessionStartEstablishes...ThroughTheHookIngestor`, `...ThroughTheCapturePath` and `ALaterRetainedShapeEventRecovers...` repeat `TheLegacyIngestionPathAppliesTheSamePolicyWithoutAWrapperFlag` and `ALaterEventRecoversAMissedSessionStartWithoutInventingAttention`. Both drive `HookIngestor` or `NativeAdoptionTests.Capture` with an injected `NativeAdoption.Apply` outcome, for both providers. The only new input is the payload JSON (retained session IDs, fully qualified `transcript_path`, `source`, and Codex `permission_mode`/`model`) in place of the `Start`/`Prompt` constants.
- Proposed improvement and benefit: express the gain as data, not a parallel class. Either run the existing tests over the native fixture payloads (`MemberData` over fixture names), or fold the cases into the ANALYSIS-902 data-driven theory. That leaves one set of assertions per ingestion path to maintain.
- Behavior-preservation risks: keep the current assertions (dedicated process, shared runtime, no waits, HookObserved) when merging.
- Evidence type: source observation
- Verification / measurement plan: the test count per scenario stays the same or grows; the merged tests fail when a fixture payload drops `session_id`.
- Relationship: related to ANALYSIS-902

### ANALYSIS-904: The real-process rows skip the production fast-hook entry point

- Severity: suggestion
- Related task / requirement: 2.2; parity and production path
- Locations: `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:40`; `src/BellTower/Setup/HookCommandBuilder.cs:17-25`; `src/BellTower.Ingress/Program.cs:30-45`; existing ingress path computation in `tests/BellTower.Tests/Providers/CaptureTests.cs:540-541` and `tests/BellTower.Tests/Updates/HookMaintenanceTests.cs:144`
- Observation: installed fast hooks run `bell-tower-ingress.exe`, which has its own entry point, its own ancestry and HookShell capture, and the provisional or candidate wire encoding in `CaptureFactory`. The new rows run only the legacy `bell-tower.exe event`. `AttachmentCapture.Resolve` is shared source, but the ingress is a separate binary whose envelope goes to the consumer, so no live-ancestor row covers the path that production native sessions take.
- Proposed improvement and benefit: add an entry-point dimension (`event` | `ingress`) to the theory. For the ingress, run its built image under the same copied provider image, drain with `StateStore`, and assert the same attachment facts. Add `BuiltApplication.Ingress` to replace the two hand-built path computations.
- Behavior-preservation risks: the ingress needs exactly `--provider <p> --data-dir <d>` and always prints `{}`. Failures are visible only through the capture inbox or rejection record, so assert on those.
- Evidence type: source observation
- Verification / measurement plan: the new rows pass for both providers; temporarily break `Resolve` in the ingress link and confirm that only the ingress rows fail.
- Relationship: new

### ANALYSIS-905: No real-process mixed (nested Claude under Codex) row, though the retained capture is that shape

- Severity: suggestion
- Related task / requirement: 2.2 (ANALYSIS-500 nested-client protection); AGENTS.md mixed simultaneous sessions
- Locations: `tests/BellTower.Tests/Fixtures/native/retained-hook-captures.json` (Claude ancestry `claude.exe` -> `pwsh.exe` -> `codex.exe`); `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs`
- Observation: the retained real Claude capture is a Claude agent running inside a Codex session, yet no new row reproduces nesting with live processes. The ANALYSIS-500 nested-client rules are covered only by injected-outcome tests.
- Proposed improvement and benefit: with the ANALYSIS-900 command form, chain two copied images (`codex.exe app-server /c claude.exe -p /c bell-tower event --provider claude ...`, and an embedded-Codex outer variant). Assert that Claude claims only its own process and never the outer Codex attachment, and that a `--provider codex` hook from the inner level resolves the nearest `codex.exe`. This gives real-process evidence for the mixed scenario at little cost.
- Behavior-preservation risks: test-only; nested quoting needs care (use `ArgumentList` for the outer image).
- Evidence type: source observation
- Verification / measurement plan: the rows pass; swapping the image names flips the asserted owner.
- Relationship: builds on ANALYSIS-900

### ANALYSIS-906: Resolve process test failures orphan processes and are masked by workspace cleanup

- Severity: suggestion
- Related task / requirement: 2.2 test diagnosability
- Locations: `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:28-55`; `tests/BellTower.Tests/Testing/TestWorkspace.cs` (`Dispose`)
- Observation: on a `WaitForExit(20000)` timeout, nothing kills the copied image or its `bell-tower.exe` child. `TestWorkspace.Dispose` then fails to delete the locked `claude.exe`/`codex.exe`, and that exception replaces the original assertion during `using` unwinding. If the built executable is missing, cmd exits 1 and `Assert.Equal(0, ExitCode)` fails before the test reads stderr, which holds the reason.
- Proposed improvement and benefit: wrap the run in `try/finally { if (!HasExited) Kill(entireProcessTree: true); WaitForExit(); }`. Assert stderr before the exit code, or include it in the message, and optionally `Assert.True(File.Exists(BuiltApplication.Executable), ...)`. Failures under load then report their real cause and leave no processes behind.
- Behavior-preservation risks: none for passing runs.
- Evidence type: source observation (passing runs measured clean: 3 s, no orphans)
- Verification / measurement plan: temporarily point the run at a missing executable and confirm the message names it, with no workspace cleanup exception.
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | - | - | not applicable |

## Assessment and limitations

- Scope covered: the full DEV-023 target. The SDK pin and the csproj item are correct. The new tests pass deterministically (12/12 in 3 runs) and fit `docs/development.md`; no product code changed and no per-provider policy is duplicated.
- Main recommendations: ANALYSIS-900, because the stated Codex `exec` restriction is disproved by a probe and the current Codex embedded row ignores its marker. ANALYSIS-902, to settle the fixture schema before the drift safeguard is built. The other findings are low-cost suggestions. ANALYSIS-901 is a measured runtime improvement of about 60 s for filtered runs.
- Measurements: the filtered test runs, the orphan-process sampling, the cmd.exe leading-token probe (disposable scratchpad copies, deleted afterwards) and classifier invocations through the built assembly. The full suite was not run (QA-018 owns it). No provider was launched.
- Not investigated: the leaked test commit (QA-018). Neither new test invokes git directly; the `event` path observes checkout state for the placeholder `D:/example` directory.
- Unexpected writes: none in scope. Build and test wrote only ignored outputs and disposable `artifacts/unit-tests` workspaces.
- Decisions needed: whether to accept cmd.exe's observed leading-token tolerance (ANALYSIS-900) or fund a stub executable, and whether to adopt the ANALYSIS-902 schema now or as part of the drift-safeguard task. The orchestrator owns triage and ledger status.
