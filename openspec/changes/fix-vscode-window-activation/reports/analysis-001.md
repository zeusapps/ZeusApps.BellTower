> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's evidence write ("subagents return reports as text"), so the reviewer did not attempt this report write; see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`. Appendix A is saved at `../evidence/analysis-001-probes.md`.

# Analysis report: fix-vscode-window-activation / Analysis 001

## Review target

- Reviewer / role: Claude subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 / subagent (code-analyzer), auto mode / claude-opus-5-5
- Date: 2026-10-25T15:00Z
- Change root / assignment: `openspec/changes/fix-vscode-window-activation` / Analysis 001 of Implementation 001
- Task and requirement references: tasks 1.2, 2.1–2.4, 3.1, 3.2, 5.1. Requirements "Exact VS Code terminal selection", "Recoverable VS Code bridge storage" and "Provider parity" ([spec](../specs/terminal-navigation/spec.md)). Developer evidence: [implementation-001](../evidence/implementation-001.md), [prototype-001](../evidence/prototype-001-window-mapping.md)
- Scope and relevant neighboring paths: `vscode/{service,focus,extension}.js` and tests; `src/BellTower/Hosts/{FocusRequest,HostRegistration,IWindowActivationApi,NativeWindowActivationApi,VsCodeNavigator,WindowActivator,WindowEvidence,IWindowInspector,NativeWindowInspector,VsCodeWindowOwnership}.cs`; `Cli/{CliApplication,ForegroundWindowCommand}.cs`; `IO/TrayInvocationCommand.cs`; `Setup/VsCodeBridgeSetup.cs`; `Tray/ChipView.cs`; `tests/BellTower.Tests/Hosts/*`; docs `compatibility.md`, `installation.md`, `architecture.md`. Neighboring paths read: `IO/AtomicFile.cs`, `IO/Json.cs`, `IO/ProcessProbe.cs` (Ancestors), `Program.cs`, `BellTower.Distribution/{Installation,CaptureCompatibility}.cs`, `Tray/{TrayApplication,SessionViewFactory}.cs`, `Hosts/Navigator.cs`, `Setup/TerminalProfilesEditor.cs`
- Baseline commit: `9b26088`
- Implementation identity (before): HEAD `9b26088191b2`, 21 modified tracked files plus 6 untracked (manifest as assigned), tracked diff SHA-256 `6146e35f8ede508aae772c4325127ff1f8f897c8b940cadb25fd866547076662`, summary `head=9b26088 baseline=9b26088 files=27 diff=6146e35f8ede untracked=6/4ffcd8e5d034`
- Before/after identity: after the analysis it was `head=9b26088 baseline=9b26088 files=27 diff=6146e35f8ede untracked=6/4ffcd8e5d034`, identical to before. No unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Safety of the window mapping (Q1) | Traced sample → publish → `Verify` → activate → reveal through `service.js`, `extension.js`, `focus.js`, `VsCodeWindowOwnership.cs`, `VsCodeNavigator.cs` | Owner == `process.ppid` at sample time; owner pid+start, live visible handle with the same owner and class, lineage and duplicate claim are re-checked at click time; `revealTerminal` requires the bridge's own `window.state.focused` | A non-VS-Code window, or a window of another VS Code main process, cannot be activated. A different window of the same main process can be. Confirmed by the race probe (ANALYSIS-001); a deterministic path is suspected (ANALYSIS-003) |
| Race probe | Node probe drives the real `Bridge`: A focus gain, sample in flight, A→B→A, sampler returns B | Appendix A, probe A: `{"samples":1,"recordedHandle":"2222","windowDirty":false}` | Confirmed: the second focus gain is dropped and A publishes B's handle |
| Spawn cost (Q2) | 10 timed runs of `bell-tower.exe foreground-window --data-dir <empty scratch>` (local Release build, framework-dependent, direct) | Appendix A, probe B: 152–180 ms, avg 161 ms; `process-info` 158 ms; no writes | One measurement. Installed single-file, launcher hop, cold start and antivirus effects were not measured (hypotheses) |
| Spawn failure modes (Q2) | Inspected `Program.Main` (installation lease), `extension.js` `sampleWindow`, `refreshWindow` | Any spawn failure returns `null`, which clears valid evidence and logs "not this editor window" | ANALYSIS-002 |
| Tray hot path | Inspected `TrayApplication.Tick` (250 ms), `SessionViewFactory` → `Navigator.Precision` → `VsCodeNavigator.Precision` → `ReadFresh` + `Verify` | Per VS Code session every 250 ms: read every registration, identify each, plus the new owner identify and `Inspect`. `AtomicFile.Read` uses `File.ReadAllText` (no `FileShare.Delete`) | ANALYSIS-004 (pattern existed before; slightly amplified) |
| Request/receipt lifecycle | Inspected `service.js` `process()` and `VsCodeNavigator.RevealAsync` | Result write, then request unlink; the tray deletes only the consumed receipt, or the pending request on timeout | ANALYSIS-005 |
| Duplication / atomic helper (Q4) | Compared JS `atomic()` with C# `AtomicFile.Write` | Both use a unique temp file, remove only the temp on failure and never delete the destination. They are separate because of the language boundary. JS has no other atomic helper | Minimal and correct. No finding |
| Registration/request separation (Q4) | Inspected `publish()`, `ownershipLive()`, per-request try/catch, focus-folder guard, Node tests 3.1/3.2 | A publish failure is retried on the next tick; requests are served while the last publish is under 15 s old and answered honestly after that; one failed request no longer aborts the others | Correct. Only the ordering issue in ANALYSIS-002(c) |
| Complexity / precision consistency | `Precision` uses `checkLineage: false`, `FocusAsync` uses `true` | A chip can read `exact terminal` and then fail lineage on click. Lineage only fails in abnormal layouts, and the bridge already enforces owner == ppid. Skipping the Toolhelp snapshot every 250 ms is justified | Accepted as a trade-off. No finding |
| Cross-language constants | `OwnershipTtlMs`/15 s, `Protocol`/`WindowProtocol` 2, `EditorWindowClass` in JS and C# | Inherent to the file contract, with comments at both ends | No finding |
| Parity / architecture (Q3) | `git diff --stat 9b26088 -- src/BellTower/Providers src/BellTower/Integration src/BellTower/Setup/ProviderSettingsEditor.cs src/BellTower/Sessions` (empty); grep of test names | No provider adapter or transport changed. `VsCodeNavigator` is the shared host path. Claude/Codex theory cases and the mixed two-window case exist (`VsCodeWindowNavigationTests`) | No duplicated per-provider policy and no transport change. Hosts-layer placement matches `docs/development.md`. Covers both clients by construction (host path is provider-independent); I did not run live per-client checks |
| Functional sanity | `node --test vscode/service.test.js vscode/focus.test.js` | 25 passed, 0 failed | .NET tests not run (QA scope; analysis needs only source) |
| VS Code API semantics | ctx7 `/websites/code_visualstudio_api`, `/microsoft/vscode` | `WindowState.focused`: "Whether the current window is focused." The docs say nothing about auxiliary windows | Auxiliary-window behavior is unverified (ANALYSIS-003) |

## Findings

### ANALYSIS-001: A focus gain during an in-flight sample is dropped, so a focus bounce records another VS Code window as the bridge's own

- Severity: warning
- Related task / requirement: 1.2, 2.1, 2.3; "Exact VS Code terminal selection" (scenarios "Duplicate editor windows", "Two VS Code windows")
- Locations at reviewed identity: `vscode/service.js:50-58` (`noteFocus`), `:61` (`this.sampling` early return), `:75` (focus-only post-check), `:91` (`windowDirty = false`); `vscode/extension.js:78-80` (only `focused === true` events are forwarded)
- Observation: `refreshWindow` returns early while a sample is in flight. When that sample completes, it clears the `windowDirty` flag the second `noteFocus` set. The post-sample check only asks whether this window is focused now, so A→B→A within the sample (about 160 ms measured, Appendix A) passes. A blur event never reaches the bridge, so nothing invalidates the sample. The probe confirmed A publishes B's handle and does not re-sample until A's next focus gain.

  What can go wrong, given the existing checks:
  1. **Non-VS-Code window: not possible.** The sample needs owner == `process.ppid` (this instance's main `Code.exe`). At click time the owner's pid and start time must match, the handle must still belong to that owner with the editor class, and the owner must be in the bridge's lineage. Windows of other VS Code installs, Insiders or a different `--user-data-dir` have a different main process and fail too. A reused HWND in another process fails `facts.OwnerPid != evidence.Owner.Pid`.
  2. **Another window of the same main process: possible.**
     - If B's bridge also claims B, the duplicate-claim rule refuses both, so A and the innocent B both drop to `terminal only` until A re-samples (collateral loss on B).
     - If nothing else claims B, the tray raises B, and restores it if it was minimized. B might have no bridge yet, have the extension disabled, never have been focused, or be a non-workbench Electron window such as DevTools. This is an intrusive wrong-window activation.
     - The bridge-side `revealTerminal` check (`focus.js:23`, requires A's `window.state.focused`) then fails after about 1 s, so the click reports failure and the chip timer stays. **A false success needs A to report focused while another HWND is foreground.** That is the auxiliary-window case (ANALYSIS-003).
- Proposed improvement and benefit (stricter options, cheapest first):
  - (a) **Focus generation counter (recommended, about 10–15 lines of JS plus one test).** Forward every `onDidChangeWindowState` (focused and blurred) to the bridge and increment a generation counter. `refreshWindow` records the generation before sampling and discards the sample, keeping `windowDirty` set, if it changed. A `noteFocus` during sampling sets a re-run flag instead of being dropped. This removes the bounce class; what remains is an event lagging past the end of the sample (observed lag is 2–3 ms against about 160 ms of sampling).
  - (b) **Self-heal on a failed reveal (small, compatible).** When a protocol-2 reveal finds its own window unfocused, the bridge clears `window` and sets `windowDirty`. Optionally the tray adds the handle it activated to the request, so the bridge only clears when that handle is its own evidence. Older trays don't send the field. This caps any mis-attribution at one wrong raise.
  - (c) **Two-sample consensus.** Publish only after two separate focus gains report the same handle. It costs UX: every window stays `terminal only` until it is focused twice. Not recommended if (a) and (b) are adopted.
  - No stronger supported mechanism exists. `nativeHandle` is a proposed API (prototype finding 2), and titles are forbidden.
- Behavior-preservation risks: (a) must keep the "first state is untrusted" rule and the bounded retry and attempt limits. (b) must not clear evidence on legacy (no-protocol) requests or when the reveal failed only because the terminal was not found.
- Evidence type: source observation, plus a measured probe result (Appendix A, probe A)
- Verification / measurement plan: Node tests for (1) the probe sequence, expecting no B evidence and a second sample; (2) blur then refocus during a sample; (3) a protocol-2 reveal with an unfocused window, expecting evidence cleared. A navigator test where a verified handle's reveal fails, expecting failure and the timer kept, already exists (`TerminalRevealFailureIsNotSuccessEvenWhenTheWindowIsForeground`).
- Relationship: new; refines the residual risk the developer flagged in prototype-001

### ANALYSIS-002: Every focus gain spawns a process, and any spawn failure erases valid evidence and blocks the tick

- Severity: suggestion
- Related task / requirement: 1.2, 3.2; "Recoverable VS Code bridge storage"
- Locations: `vscode/extension.js:34-51`; `vscode/service.js:50-58`, `:78-82` (null sample, then `this.window = null` and the "not this editor window" log), `:138-139` (`await this.refreshWindow()` before `publish` and request processing); `src/BellTower/Program.cs:17-18` (installation lease on every invocation); `src/BellTower/Cli/ForegroundWindowCommand.cs` (30 ms sleep; exit 1 when the two samples differ)
- Observation:
  - Measured: about 150–180 ms wall time per spawn on the local framework-dependent build.
  - Hypotheses (not measured): the installed single-file payload adds self-extract or cold-start cost. If `bellTower.executablePath` points at the stable launcher, each sample costs two .NET processes. Antivirus scanning may add more.
  - A spawn runs on every focus gain of every bridged window, even when the window already has valid evidence. A window's HWND is fixed for its lifetime. The `sampling` guard prevents concurrency, but nothing rate-limits across focus gains.
  - Failure modes all collapse to `null`, and each one clears previously valid evidence with a misleading log line:
    - the 3 s timeout
    - an executable that moved after an update or dev-build switch
    - a shared lease that fails while a switch or retire holds the exclusive lease
    - the foreground changing within 30 ms
    - JSON or maxBuffer errors
  - `tick()` awaits a retry sample before publishing and before serving requests. A slow or timing-out spawn delays a pending reveal by up to 3 s against the tray's 5 s request expiry (this also feeds ANALYSIS-005).
- Proposed improvement and benefit:
  - (a) Separate "sample unavailable" (spawn failed, exit ≠ 0, timeout) from "sample contradicts" (foreign owner or class). On unavailable, keep the prior evidence and retry. This stays fail-safe because the tray re-verifies everything at click time. Log a distinct message.
  - (b) Don't make request processing wait on sampling: run `refreshWindow` after processing requests, or fire and forget with the existing guard.
  - (c) Optional, only together with ANALYSIS-001(b) self-heal: skip re-sampling while valid evidence exists, so spawns drop from one per focus gain to about one per window lifetime. Without self-heal, keep re-sampling, since it is the correction path for ANALYSIS-001.
- Behavior-preservation risks: the test "a later contradicting sample clears earlier evidence" must still pass for real contradictions. A mis-attributed record must not survive longer if (c) is adopted without self-heal.
- Evidence type: measured result (local spawn latency only) plus source observation; installed-path costs are performance hypotheses
- Verification / measurement plan: time `foreground-window` through the installed launcher path in a disposable install (10 runs, cold and warm); count spawns per hour of normal use from a debug counter in the bridge output channel; Node tests for (a) and (b) with an injected failing or slow `sampleWindow`.
- Relationship: new

### ANALYSIS-003: Auxiliary (floating) editor windows may be recorded as the bridge's window deterministically, which would allow a false success

- Severity: warning (hypothesis; needs a probe before any code change)
- Related task / requirement: 1.2, 2.3, 4.2; "Exact VS Code terminal selection", "Window behind another app"
- Locations: `vscode/service.js:61-91`; `vscode/focus.js:23`; `src/BellTower/Hosts/VsCodeWindowOwnership.cs` (only class and owner are checked; main and auxiliary windows share both); `docs/compatibility.md:33` (auxiliary windows not mentioned)
- Observation: VS Code auxiliary windows (floating editors) are separate top-level `Chrome_WidgetWin_1` windows of the same main process. From my knowledge of the VS Code source (not confirmed by ctx7, whose API docs only say "whether the current window is focused"), the extension-visible window state is true when either the main window or one of its auxiliary windows has focus.

  If so, moving focus from another app straight into a floating window fires `focused=true` with no race, and the stable sample is the auxiliary HWND. Every tray check passes. A click then raises the floating window; `revealTerminal` sees `state.focused` true and `activeTerminal` matching, and the final `IsForeground(aux)` is true. The result is reported as success and the chip timer is hidden, while a panel terminal in the main window can stay behind or minimized.
- Proposed improvement and benefit:
  - First, add an auxiliary-window probe to the isolated two-window fixture (task 4.2, about 30 min). Open a floating editor, focus it from another app, and check the recorded handle and the click outcome.
  - If confirmed, use one of these fail-safe options:
    - (a) The bridge keeps the set of distinct handles sampled for its instance (pruning dead ones on the tray side is unnecessary; the bridge can drop a handle when a later sample on focus gain differs). It publishes no evidence while more than one handle has been seen in a short window. Cost: about 15 lines of JS. Windows with a floating editor then show `terminal only`.
    - (b) If no API distinguishes the windows, record auxiliary windows as unverified or unsupported in the compatibility matrix with the fallback (`terminal only`, honest failure).
- Behavior-preservation risks: (a) can degrade users who use floating windows. Document it honestly instead of letting a success be reported falsely.
- Evidence type: unverified hypothesis (source knowledge only)
- Verification / measurement plan: the fixture probe above, recording the VS Code version and the window handles observed. If confirmed, add a Node test for multiple observed handles and a matrix entry.
- Relationship: new; related to ANALYSIS-001 (same consequence path, but not race-dependent)

### ANALYSIS-004: Tray polling of registrations every 250 ms is a likely cause of the rename failures, and the new verification adds work to that hot path

- Severity: suggestion
- Related task / requirement: 3.1, 3.2 (the field report's 114 leaked temp files)
- Locations: `src/BellTower/Tray/TrayApplication.cs:18-20, 208`; `src/BellTower/Tray/SessionViewFactory.cs:15, 20`; `src/BellTower/Hosts/VsCodeNavigator.cs:20-25, 99-120`; `src/BellTower/IO/AtomicFile.cs:51` (`File.ReadAllText`)
- Observation: every tray tick (250 ms, UI thread) calls `Precision` for each VS Code session. Each call re-reads every registration file, runs `ProcessProbe.Identify` for each, identifies the shell, and now also runs `Verify` (identifies the owner, then calls `Inspect`). With S sessions and R windows that is about 4·S·R file opens and 4·S·(R+2) process opens per second. The pattern existed before; the change adds one identify and `Inspect` per session.

  Hypothesis: `File.ReadAllText` opens without `FileShare.Delete`, so the bridge's `renameSync` (MoveFileEx replace) fails with EPERM whenever it overlaps a tray read. The cleanup now prevents leaks, but the failure rate still scales with session count × 4 Hz.
- Proposed improvement and benefit: snapshot `ReadFresh()` once per chip build or tray tick inside `VsCodeNavigator`, cached for one tick and never past the 15 s freshness. And/or read host registrations through a reader that opens with `FileShare.ReadWrite | FileShare.Delete`, scoped to the VS Code reader rather than changing `AtomicFile.Read` globally. This cuts UI-thread process opens by a factor of S and should cut rename collisions.
- Behavior-preservation risks: the cache must not outlive a tick, or a destination could look live after it has gone stale. A share-mode change in shared `AtomicFile.Read` would affect every state file, so keep it scoped.
- Evidence type: source observation; the rename-failure cause and its cost are performance hypotheses
- Verification / measurement plan: with N disposable bridges registered and the tray tick simulated at 250 ms, count "Bridge registration could not be written" lines (the bounded logger already reports suppressed counts) over 10 minutes, before and after. Optionally time `Precision` per tick with a Stopwatch in a test harness.
- Relationship: new; root-cause context for 3.1

### ANALYSIS-005: Request and receipt files can be orphaned

- Severity: suggestion
- Related task / requirement: 3.1, 3.2
- Locations: `vscode/service.js:208-209`; `src/BellTower/Hosts/VsCodeNavigator.cs:72-83`
- Observation: the bridge writes `.result` and then unlinks the request. If the unlink fails, the per-request catch leaves the request queued, so it is processed again: the terminal is revealed again, or a "stale" result is written after expiry, and that second `.result` is never read. The tray deletes only the receipt it consumed, and only the pending request on timeout. A receipt written after the tray's 5 s timeout is never removed; ANALYSIS-002(c) makes late receipts more likely. Nothing sweeps `*.result`. This is bounded at one file per affected request, but grows without limit over time, which is the same class of leak as the original field report.
- Proposed improvement and benefit: after reading a receipt, the tray also deletes `pending` (idempotent). On timeout it deletes both `pending` and any `receipt`. Optionally, when it writes a request, it sweeps `Requests/*.result` older than 1 minute. A few lines of C# with no protocol change.
- Behavior-preservation risks: don't delete the receipt of a request that is still in flight; the age threshold must exceed the 5 s expiry.
- Evidence type: source observation
- Verification / measurement plan: a navigator test where the bridge answers after the timeout leaves no `.result`; a Node test where `unlinkSync` fails reprocesses at most once and the tray-side cleanup removes the leftovers.
- Relationship: new

## Re-verification

None assigned.

## Assessment and limitations

- **Q1 (mapping safety):** a non-VS-Code window, or a window of a different VS Code main process, cannot be activated. The owner, lineage and identity checks make that hold by construction. A different window of the same main process can be activated.
  - Through a focus bounce, which the probe reproduced (ANALYSIS-001). Usually the result is a wrong raise followed by an honest failure, because `revealTerminal` requires the bridge's own window focus. When another bridge also claims the window, the result is collateral `terminal only`.
  - Possibly deterministically through auxiliary windows, where a false success becomes possible (ANALYSIS-003, unverified).
  - Recommended: the focus-generation counter plus self-heal on a failed reveal (low cost), and an auxiliary-window probe in task 4.2.
- **Q2 (spawn cost):** about 160 ms per spawn measured on the local build. Installed-path costs are hypotheses. The main practical issues are failure handling and the tick ordering, not raw cost (ANALYSIS-002).
- **Q3 (parity and transport):** confirmed. There is no per-provider policy and no provider transport change, and both clients go through the shared `VsCodeNavigator` path.
- **Q4 (atomic writes and separation):** the atomic write is minimal and mirrors `AtomicFile.Write` semantics. Separating publication from request processing is correct. Adjacent issues: orphaned receipts (ANALYSIS-005) and a probable cause of the rename failures (ANALYSIS-004).
- **Not performed:** .NET tests (QA scope), live window activation (task 4.2; I did not touch the user's foreground), installed-path timing, and the auxiliary-window probe. The probe ran only in scratch with fakes; no user window, setting or data directory was touched.
- **Unexpected writes:** none. The evidence file write was refused by the client.
- **Decisions for the orchestrator:** whether to fold ANALYSIS-001(a)/(b) into Implementation 002 before live acceptance, and whether to make the auxiliary-window probe an explicit part of 4.2.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 001 of Implementation 001; new ANALYSIS-001 (warning), ANALYSIS-002 (suggestion), ANALYSIS-003 (warning, hypothesis), ANALYSIS-004 (suggestion), ANALYSIS-005 (suggestion)
changed or report paths: none written. Intended: reports/analysis-001.md and evidence/analysis-001-probes.md (text inline). Disposable probe: %USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\bounce-probe.js
checks: node --test vscode/service.test.js vscode/focus.test.js -> passed (25/25); focus-bounce probe -> reproduced ANALYSIS-001; foreground-window latency x10 -> measured 152-180 ms; dotnet build/test -> not run (QA scope, not needed for analysis); format verify -> not run (no source analyzed for formatting)
target identity: before head=9b26088 baseline=9b26088 files=27 diff=6146e35f8ede untracked=6/4ffcd8e5d034; after identical
limitations / decisions needed: the client refused the evidence write, so the report text was returned inline for the orchestrator to save. ANALYSIS-003 is an unverified hypothesis that needs the auxiliary-window probe. Installed-path spawn cost was not measured. Decision: whether ANALYSIS-001(a)/(b) go into the next implementation round before live acceptance 4.2.
next: orchestrator saves the report and appendix, triages ANALYSIS-001..005, and routes the fixes and the auxiliary-window probe to the developer and task 4.2.
```
