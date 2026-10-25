> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's report and evidence writes ("Subagents should return findings as text"); see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`. Appendix A is saved at `../evidence/analysis-002-probes.md`.

# Analysis report: fix-vscode-window-activation / Analysis 002 (recheck 1)

## Review target

- Reviewer / role: Claude subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 / subagent (code-analyzer), auto mode / `claude-opus-5-5`
- Date: 2026-10-25T17:47Z
- Change root / assignment: `openspec/changes/fix-vscode-window-activation` / Analysis 002 (recheck 1) of Implementation 002
- Task and requirement references: tasks 1.2, 2.3, 2.4, 3.1, 3.2, 5.2; requirements "Exact VS Code terminal selection", "Recoverable VS Code bridge storage" and "Provider parity" ([spec](../specs/terminal-navigation/spec.md)). Developer claims: [implementation-002](../evidence/implementation-002.md)
- Scope and relevant neighboring paths:
  - Bridge: `vscode/{service,extension,focus}.js` and their tests.
  - Hosts: `src/BellTower/Hosts/{VsCodeNavigator,VsCodeWindowOwnership,FocusRequest,HostRegistration,WindowActivator}.cs`.
  - New files: `src/BellTower/Diagnostics/VsCodeBridgeReporter.cs`, `src/BellTower/Tray/NavigationOutcome.cs`.
  - Tray and setup: `Tray/{TrayApplication,ChipView}.cs`, `Setup/VsCodeBridgeSetup.cs`.
  - Tests and docs: `tests/BellTower.Tests/Hosts/VsCodeWindowNavigationTests.cs`; docs `compatibility.md`, `installation.md`, `architecture.md`.
  - Neighbors read: `IO/{DataPaths,AttachmentStore}.cs`, `Tray/SessionViewFactory.cs`, `Sessions/SessionCatalog.cs`, `Cli/CommonOptions.cs`, `docs/development.md`.
- Baseline commit: `9b26088`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 9b26088 -Paths vscode src tests docs/compatibility.md docs/installation.md docs/architecture.md` (PowerShell 7.6.6). HEAD `8167c5d3398c`, 23 modified tracked plus 9 untracked files (the manifest matches the assignment). Tracked diff SHA-256 `5443a2b9d46ea63c907f4903951a1f4280080ac589ded724a0499346b01a60ee`. Summary `head=8167c5d baseline=9b26088 files=32 diff=5443a2b9d46e untracked=9/80744c697558`, as expected.
- Before/after identity: after the analysis the summary was `head=8167c5d baseline=9b26088 files=32 diff=5443a2b9d46e untracked=9/80744c697558`, identical. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md), [analysis-001 probes](../evidence/analysis-001-probes.md); ANALYSIS-001 to ANALYSIS-005

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-001 race | Re-ran probe A adapted to `noteWindowState`, plus blur-then-refocus, noteFocus-only, lag and self-heal variants | [Appendix A](../evidence/analysis-002-probes.md), set 1: A records `1111` after 2 samples (before: `2222`). C discards, then records `1111` on refocus. E self-heals | Verified (see Re-verification) |
| ANALYSIS-003 guard lifetime | Probe D, plus inspection of `service.js:133-142, 162-171` and `VsCodeWindowOwnership.cs:30-33` | One mis-sample, then `null` on every later focus gain; `seenHandles` is never pruned; the doctor and chip reason says "Focus it once" | Permanent for the instance lifetime and misreported (ANALYSIS-100) |
| QA-003 interaction path | Probe F/G; source; ctx7 `/websites/code_visualstudio_api` (`WindowState.active`) | F records sibling `2222` when `focused` is stale-true; G rejects a foreign owner. The docs don't say which inputs count as activity | Does not reproduce the prototype-001 case by construction; the residual depends on undocumented semantics (ANALYSIS-103) |
| ANALYSIS-004 rename | Independent Node 24.13.0 probe: held reader per share mode, then a 10 s contention loop with an immediate-retry counter | All share modes give EPERM. 1.9% collisions at a 4 Hz reader (PowerShell reader, 8.5 ms hold); immediate retry heals 10–13% | Developer measurement confirmed. A rename retry is not warranted (see Re-verification) |
| ANALYSIS-004 snapshot | Inspection of `VsCodeNavigator.cs:29-35, 187-199`, `TrayApplication.cs:53-54`, `SessionCatalog.cs:20` | The tray holds one long-lived `Navigator`, so the 200 ms snapshot is shared across all chips of a tick. Freshness is re-filtered on every use | Correct. The cost reduction is a hypothesis (not timed) |
| ANALYSIS-005 cleanup | Inspection of `VsCodeNavigator.cs:97-170`, `service.js:290-291` | Removal on read and timeout; sweep of receipts older than 1 min before each request | Verified. Residual misleading bridge log noted |
| Layering | Compared the new files against `docs/development.md` | Reporter: read-only, delegates policy to `Hosts`. Outcome: a Tray decision over the IO store | Placement is fine. Duplicated guard (ANALYSIS-101); attachment-file ownership drift (ANALYSIS-102) |
| Parity / transport | `git diff --stat 9b26088 -- src/BellTower/{Providers,Integration,Sessions,Core} src/BellTower/Setup/ProviderSettingsEditor.cs src/BellTower.Ingress src/BellTower.Protocol` (empty); grep for provider names in host and bridge code (none); test names | Claude/Codex theory cases, a mixed two-window test, and `DoctorReportsWindowVerificationEquallyForClaudeAndCodex` | No per-provider policy and no transport change. Covers both clients by construction; live per-client checks were not run |
| Functional sanity | `node --test vscode/service.test.js vscode/focus.test.js` | 35 passed, 0 failed | .NET build and tests were not run (QA scope) |

## Findings

### ANALYSIS-100: The multiple-handle guard disables native activation for the whole window lifetime, and diagnostics tell the user a remedy that cannot work

- Severity: warning
- Related task / requirement: 1.2, 5.2; "Exact VS Code terminal selection"; AGENTS.md requires honest diagnostics
- Locations: `vscode/service.js:133-142` (guard), `:51` (`seenHandles` only grows), `:162-167` (self-heal needs `this.window`, which the guard set to null); `src/BellTower/Hosts/VsCodeWindowOwnership.cs:30-33`; `src/BellTower/Tray/ChipView.cs:12`; `docs/installation.md:85`
- Observation:
  - Once two distinct handles have been sampled, every later focus gain re-trips the guard. Self-heal cannot run because `window` is null, so only a window reload recovers. Probe D: after one mis-sample, three real focus gains all published `null`.
  - The trigger is not limited to auxiliary windows. Any single mis-sample sets it, such as the residual event-lag case or a QA-003 stale-focus sample (probe F). Self-heal recovers only when a failed click comes before the next focus gain (probe E).
  - The registration then says only `window: null`. So `Verify`, and through it the doctor (`Explain`/`Windows`) and the chip tooltip, report "has not been seen in the foreground since its bridge loaded. Focus it once so Bell Tower can verify it." That advice cannot work. The docs don't name reload as the recovery for this state.
  - The permanence itself is fail-safe and acceptable: decaying the guard would reopen the ANALYSIS-003 false-success path when the consensus handle is the auxiliary window. The misreported state is the defect.
- Proposed improvement and benefit:
  - The bridge publishes an additive reason with the null window, for example `windowUnavailable: "multiple"`. Older trays ignore it.
  - `Verify` maps that reason to text such as "More than one top-level window was observed for this VS Code window (for example a floating editor); run Developer: Reload Window to re-verify it."
  - `installation.md` and the `terminal only` tooltip name reload as the recovery.
  - About 10 lines of JS and C#, with no change to policy.
- Behavior-preservation risks:
  - Keep the guard permanent; do not let the reason re-enable evidence.
  - Treat a protocol 2 registration without the field as "not yet observed", as today.
  - Doctor output parity is automatic, since both providers go through `Explain`.
- Evidence type: measured probe result (Appendix A, D/E) plus source observation
- Verification / measurement plan:
  - A Node test: two handles, then the registration carries the reason.
  - A navigator or doctor test: the reason maps to the reload text for matched Claude and Codex sessions.
  - Live 4.2: open a floating editor, then read the doctor.
- Relationship: follows from the ANALYSIS-003 fix

### ANALYSIS-101: The QA-002 decision function is guarded again inline, so its tests do not cover the production branch

- Severity: suggestion
- Related task / requirement: 2.4 (QA-002)
- Locations: `src/BellTower/Tray/TrayApplication.cs:315` (`if (result.Success && hideTimer && episode is not null)`) wrapping `:319` (`NavigationOutcome.ApplyViewed(...)`); `src/BellTower/Tray/NavigationOutcome.cs:87`
- Observation: `ApplyViewed` repeats the same three conditions. Production decides on the inline copy, so `NavigationOutcomeTests` prove the function and not the branch that runs. A later edit to either copy can diverge with the tests still green, which is the risk QA-002 was raised to close.
- Proposed improvement and benefit: remove the outer `if`, so `Navigate` calls `ApplyViewed` inside the existing `try`. This gives one decision point and the tests cover what runs. Behavior is identical.
- Behavior-preservation risks: the `try/catch` must still wrap the `MarkViewed` I/O; `Tick()` stays conditional on the return value.
- Evidence type: source observation
- Verification / measurement plan: the existing `NavigationOutcomeTests` plus the full test run; no new test is needed.
- Relationship: refines the QA-002 fix

### ANALYSIS-102: The doctor's session list enumerates attachment files directly and counts every unended attachment as a live session

- Severity: suggestion
- Related task / requirement: 5.2 (QA-004)
- Locations: `src/BellTower/Diagnostics/VsCodeBridgeReporter.cs:29-58`; `docs/development.md` ("`AttachmentStore` owns attachment files")
- Observation:
  - The reporter enumerates `paths.Attachments` itself, bypassing `AttachmentStore`, the only attachment owner per `docs/development.md`.
  - It treats `EndedAt: null` as live. Crashed or abandoned attachments therefore appear as sessions with `unavailable` and "No single fresh VS Code bridge registration contains this terminal", a misleading line in exactly the field diagnostic QA-004 targeted.
  - `Take(200)` on file-name order truncates arbitrarily.
  - Policy placement is correct: verification is delegated to `VsCodeNavigator.Explain`, with no duplicated rule and nothing provider-specific.
- Proposed improvement and benefit:
  - Source the sessions from the live session set (the state store, or `SessionCatalog`, which the tray and CLI already use), or add `AttachmentStore.List()`.
  - Optionally report a dead shell as "session process has ended" instead of a registration miss.
  - This makes the doctor more honest with minimal code.
- Behavior-preservation risks: keep the report read-only and keep both providers reported identically (the existing parity test).
- Evidence type: source observation
- Verification / measurement plan: extend `DoctorReportsWindowVerificationEquallyForClaudeAndCodex` with one unended attachment whose shell is dead.
- Relationship: new

### ANALYSIS-103: The QA-003 interaction path relies on undocumented VS Code activity semantics; under a stale `focused` flag it records a sibling window

- Severity: suggestion (hypothesis; would be a warning if the probe confirms it)
- Related task / requirement: 1.2, 4.2 (QA-003)
- Locations: `vscode/service.js:59-69` (`interacted`), `:53-54` (`lastFocused` from the initial state, `lastActive` a constant `false`); `vscode/focus.js:23`
- Observation:
  - The path does not reproduce the prototype-001 case by construction. It never samples from the initial state, it needs a change event, and a foreign owner is still rejected (probe G).
  - Its safety rests on the developer's premise that an `active` rise "proves input reached this window". The API only says "interacted with recently" (ctx7). My recollection of the VS Code source is that activity comes from keydown, mousedown and touchstart, which imply the window is in the foreground. That recollection is unverified.
  - If a non-foreground window can flip `active` (wheel-scroll over an inactive window, hover, an auxiliary window's input), and `focused` is stale-true as in prototype-001 finding 4, then the sample is the real foreground. A sibling of the same process is recorded (probe F: `2222`).
  - In that same stale-focused state, `revealTerminal`'s `state.focused` check may pass. The click would then be reported as a success while the bridge's own window stays behind. Self-heal does not fire, because it needs a failed reveal.
  - Separately, `lastActive = false` at construction means a window focused and active at load gets no sample until it has gone idle and the user interacts again. That is a coverage limit, not a safety issue (QA's scope).
- Proposed improvement and benefit:
  - First add a fixture probe to task 4.2. While another app or window holds the foreground, scroll and hover over a background fixture window that reported `focused` at load, and log `onDidChangeWindowState`.
  - If `active` can rise without foreground input, drop the interaction trigger. Document "switch away and back once" instead; the honest fallback stays `terminal only`.
  - If it cannot, record the verified semantics, with VS Code version, in the compatibility matrix.
- Behavior-preservation risks: removing the path restores the QA-003 coverage gap but never a wrong activation.
- Evidence type: measured probe result (Appendix A, F/G) plus an unverified hypothesis about VS Code activity semantics
- Verification / measurement plan: the fixture probe above, recording the VS Code version and the event log; no code change before it.
- Relationship: related to ANALYSIS-003 (same false-success consequence path)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=5443a2b9d46e untracked=9/80744c697558` | Probe A re-run: `samples 2, recorded 1111` (was `samples 1, recorded 2222`). Blur-then-refocus: discard with no record and `dirty` true, then `1111` on refocus. noteFocus-only also caught (the generation is incremented in `noteFocus`). Self-heal (E): a failed reveal while unfocused with the matching handle clears the record and the handle, and the next gain records `1111`. Source: `service.js:59-69, 95-116, 162-171, 275`; the tray sends `WindowHandle` only after confirmed activation (`VsCodeNavigator.cs:73-74`); legacy requests, other handles, a focused window or a missing terminal do not clear (self-heal is reached only inside the matched-terminal branch). `active` churn costs one extra spawn (H) and is bounded by events. Residual: an event lag of more than about 30 ms (the double sample's gap plus process exit) can still mis-record (hypothesis), and then ANALYSIS-100 applies | verified (residual by design) |
| ANALYSIS-002 | same | (a) `extension.js:48-51` returns `{unavailable:true}` and `service.js:117-121` keeps the record, logs `window-unavailable` and retries within the 3 s / 5-attempt bounds; real contradictions still clear (`:127-131`). (b) `tick()` publishes and serves requests, then fires `refreshWindow()` (`:237-238`), and sampling republishes through `changed()`. Node tests at `service.test.js:382, 404` pass. (c) Not adopted; consistent with the analysis-001 advice, since spawns per focus gain remain the correction path | verified |
| ANALYSIS-003 | same | Fail-safe (a) is implemented (`service.js:133-142`) and the docs and matrix are marked unverified. The guard is permanent for the bridge lifetime (probe D); that is acceptable as fail-safe, but its state is misreported (ANALYSIS-100). Residual: a first-ever sample taken in the auxiliary window (before the main one is sampled) can still be recorded alone, which leaves the false-success consequence open until 4.2 | partially verified; consequence still unverified (4.2); see ANALYSIS-100 |
| ANALYSIS-004 | same | The snapshot is shared across all chips per tick, because the tray holds one long-lived `Navigator` (`TrayApplication.cs:53`), and freshness is re-filtered on every use (`VsCodeNavigator.cs:187-199`). `FocusAsync`, `Explain` and `Windows` read fresh. `ReadRegistration` (`:228-238`) is scoped and `AtomicFile.Read` is unchanged. My own measurement confirms the developer's: Node `renameSync` gets EPERM under every reader share mode. Rename retry (assessment): not warranted. Measured: an immediate synchronous retry heals only 10–13% of collisions, so an effective retry must wait about the reader's hold time. In the extension host that means either blocking the thread or adding an async retry, for a write that is already retried 1.5 s later against a 15 s TTL. Hypothesis (not measured): with the compiled C# reader holding well under a millisecond at about 4 Hz, collisions are about 0.1% of publishes, and losing ownership needs about 10 consecutive failures, which is negligible. The cheaper residual lever is to decode after closing the stream in `ReadRegistration` (today `Json.Decode` runs while the handle is open), which shortens the hold | verified; no retry recommended |
| ANALYSIS-005 | same | Receipt and request are removed on read, including an unreadable receipt (`:119-123`), and on timeout (`:129-130`), with a 5×10 ms bounded retry. A sweep of `*.result` older than 1 min runs before each request (`:99, 154-170`), well above the 5 s expiry, so concurrent requests' receipts are safe. Residual (minor, new): when the tray removes `pending` before the bridge's late `unlinkSync` (`service.js:291`), ENOENT is thrown into the per-request catch, which logs the misleading "could not be completed; it will be retried". The leftover `.result` is then swept by a later request. Tolerating ENOENT on that unlink would fix the log | verified (minor residual log) |

## Assessment and limitations

- **Rechecks.**
  - ANALYSIS-001: the race class is closed and self-heal works.
  - ANALYSIS-002, -004 and -005: verified.
  - ANALYSIS-003: the fail-safe guard is in place. Its permanence is acceptable, but the state is misreported (ANALYSIS-100). The false-success consequence still needs live task 4.2.
- **ANALYSIS-004 rename retry:** not warranted. The measured mechanics show an immediate retry rarely helps, and the residual collision rate is a hypothesis with negligible ownership impact. If 4.2 field logs show the `registration` line recurring, the better lever is decoding after the stream is closed in `ReadRegistration`.
- **QA-003:** this path does not reintroduce the prototype-001 initial-state mismatch. It relies on undocumented activity semantics (ANALYSIS-103); probe it in 4.2 before relying on it.
- **Layering:** `VsCodeBridgeReporter` (Diagnostics, read-only, policy delegated to Hosts) and `NavigationOutcome` (Tray) are placed correctly per `docs/development.md`. Minor issues: the duplicated guard (ANALYSIS-101) and bypassing attachment ownership (ANALYSIS-102). Also minor: the setup note says an older bridge reports as "unverified" (`VsCodeBridgeSetup.cs`), while the doctor calls it "partial".
- **Parity:** confirmed. There is no per-provider policy and no transport change; the shared host path and the doctor cover Claude and Codex equally.
- **Not performed:** .NET build, tests and format verify (QA scope; not needed for source analysis), live activation and floating-window semantics (task 4.2), and timing of the C# reader hold (hypothesis).
- **Unexpected writes:** none. The client refused both the report write and the evidence write, so the text is returned inline. Disposable probe files are only in the scratchpad.
- **Decisions for the orchestrator:** whether ANALYSIS-100 blocks before 4.2 (I recommend fixing it, since the diagnostics are dishonest), and whether to add the ANALYSIS-103 activity probe to 4.2.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 002 (recheck 1) of Implementation 002; rechecked ANALYSIS-001 (verified, residual by design), ANALYSIS-002 (verified), ANALYSIS-003 (partially verified; consequence still needs 4.2), ANALYSIS-004 (verified; no rename retry recommended), ANALYSIS-005 (verified, minor residual log); new ANALYSIS-100 (warning), ANALYSIS-101 (suggestion), ANALYSIS-102 (suggestion), ANALYSIS-103 (suggestion, hypothesis)
changed or report paths: none written (client refused). Intended: reports/analysis-002.md and evidence/analysis-002-probes.md (text inline). Disposable probes: %USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\analysis-002-probe.js and %USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\a2-rename.js
checks: node --test vscode/service.test.js vscode/focus.test.js -> passed (35/35); bridge probe set (A, A-noteFocus, C, D, E, F, G, H) -> run, results in Appendix A; Node rename/share-mode and contention probe -> run, EPERM confirmed in all share modes, immediate retry heals 10-13%; dotnet build/test/format verify -> not run (QA scope, not needed for source analysis)
target identity: before head=8167c5d baseline=9b26088 files=32 diff=5443a2b9d46e untracked=9/80744c697558; after identical
limitations / decisions needed: the client refused both the report and evidence writes; the full text is inline for the orchestrator to save. Live 4.2 not performed (floating-window focus semantics, real foreground grant, VS Code activity semantics for ANALYSIS-103). The C# reader hold time was not measured (hypothesis). Decisions: whether ANALYSIS-100 (misreported, permanent guard state) blocks before 4.2; whether to add the ANALYSIS-103 activity probe to 4.2.
next: orchestrator saves the report and appendix, triages ANALYSIS-100..103, and routes the ANALYSIS-100/101 fixes (and optionally 102, plus the ENOENT-tolerant unlink residual from ANALYSIS-005) to the developer, and the ANALYSIS-103 probe to task 4.2.
```
