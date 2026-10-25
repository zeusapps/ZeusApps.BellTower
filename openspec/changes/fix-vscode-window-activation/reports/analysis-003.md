> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's report and evidence writes ("Subagents should return findings as text"); see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`. Appendix A is saved at `../evidence/analysis-003-probes.md`.

# Analysis report: fix-vscode-window-activation / Analysis 003 (recheck 2, final cycle)

## Review target

- Reviewer / role: Claude subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 / subagent (code-analyzer), auto mode / `claude-opus-5-5`
- Date: 2026-10-25T18:11Z
- Change root / assignment: `openspec/changes/fix-vscode-window-activation` / Analysis 003 (recheck 2, final) of Implementation 003
- Task and requirement references:
  - Tasks 1.2, 2.4, 3.1, 3.2, 5.2.
  - Requirements "Exact VS Code terminal selection", "Recoverable VS Code bridge storage" and "Provider parity" ([spec](../specs/terminal-navigation/spec.md)).
  - Developer claims: [implementation-003](../evidence/implementation-003.md).
- Scope and relevant neighboring paths:
  - Bridge: `vscode/service.js` and `vscode/service.test.js`.
  - Hosts: `src/BellTower/Hosts/{HostRegistration,VsCodeWindowOwnership,VsCodeNavigator}.cs`.
  - Diagnostics: `Diagnostics/{VsCodeBridgeReporter,DiagnosticsReporter}.cs`.
  - IO, Tray and setup: `IO/AttachmentStore.cs`, `Tray/{TrayApplication,NavigationOutcome,ChipView}.cs`, `Setup/VsCodeBridgeSetup.cs`.
  - Tests and docs: `tests/BellTower.Tests/Hosts/VsCodeWindowNavigationTests.cs`; `docs/compatibility.md`, `docs/installation.md`.
  - Neighbors read: `IO/{AttachmentHealth,ProcessProbe,DataPaths,Json}.cs`, `Cli/{DoctorCommand,CommonOptions}.cs`.
- Baseline commit: `9b26088`
- Implementation identity (before):
  - Command: `./scripts/review-identity.ps1 -Baseline 9b26088 -Paths vscode src tests docs/compatibility.md docs/installation.md docs/architecture.md` (PowerShell 7.6.6).
  - HEAD `8167c5d3398c50fa98b165f84d7d15dbb69bde99`; 24 modified tracked files plus 9 untracked files.
  - Tracked diff SHA-256 `3a6df874e2c9da5cf2525220e082df6fb994c9e3563b1eb7a017dad56ce4d7d9`.
  - Summary `head=8167c5d baseline=9b26088 files=33 diff=3a6df874e2c9 untracked=9/7e8916580128`, as expected.
- Before/after identity: after the analysis the summary was `head=8167c5d baseline=9b26088 files=33 diff=3a6df874e2c9 untracked=9/7e8916580128`, identical. No unexpected mutation.
- Prior reports / rechecked findings:
  - Reports: [analysis-001](analysis-001.md), [analysis-002](analysis-002.md), [analysis-002 probes](../evidence/analysis-002-probes.md), [qa-002](qa-002.md) (QA-100).
  - Rechecked: ANALYSIS-100, ANALYSIS-101, ANALYSIS-102, the ANALYSIS-005 ENOENT residual and QA-100 (structure).
  - Regression check: ANALYSIS-001, ANALYSIS-002, ANALYSIS-004 and ANALYSIS-005.
  - Exposure check only: ANALYSIS-103.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-100: reason tracks the guard | Re-ran probes D and E extended to read `windowUnavailable`; new D2 case (guard, then a foreign sample); 300-run random fuzz (7,500 steps) of the invariants; source `service.js:53, 135-145, 165-170, 201` | [Appendix A](../evidence/analysis-003-probes.md), set 1: reason `(absent)` before the guard and `multiple` from the trip onward. Fuzz: 0 violations across 229 tripped runs | Verified |
| ANALYSIS-100: reason never re-enables evidence | Source `VsCodeWindowOwnership.cs:41-49`, `HostRegistration.cs:9-10`; test `MultipleWindowsGuardReadsAsReloadRecoveryForClaudeAndCodexInNavigationAndDoctor` (`VsCodeWindowNavigationTests.cs:543`) | The reason is read only inside the `Window is null` branch, so the only output is `Unverified`. Bridge side: after the trip, `selfHeal` returns early (window null) and `seenHandles` never shrinks | Verified |
| ANALYSIS-101 | Source `TrayApplication.cs:315-327`, `NavigationOutcome.cs:13-22` | The inline guard is gone. `ApplyViewed` is the only decision point, inside the existing `try/catch`, and `Tick()` depends on its result | Verified |
| ANALYSIS-102: layering | Source `AttachmentStore.cs:9-33`, `VsCodeBridgeReporter.cs:40-61`; `grep paths.Attachments` (the only enumerator is now `AttachmentStore`); compared with `AttachmentHealth.cs:9-16` | Enumeration now goes through the owner. The new ended check repeats the confirmed-ended rule of `AttachmentHealth.Evaluate` | Verified. Duplicated rule raised as ANALYSIS-150 |
| ANALYSIS-005 ENOENT residual | Probe set 1: Unlink_ENOENT and Unlink_EPERM; source `service.js:295-300`; Node tests at `service.test.js:530-560` | ENOENT: receipt written, request gone, 0 log lines. EPERM: request kept, 1 log line | Verified |
| QA-100 structure | Source `VsCodeBridgeReporter.cs:22-38`, `Json.cs:8` (Web defaults, so nulls are written); `DoctorCommand.cs:16` uses `CommonOptions.Paths`, which calls `DataPaths.Ensure()`; test `DoctorKeepsArrayTypesAndSetsErrorWhenRegistrationsCannotBeRead` (`:530`); `docs/compatibility.md` | Shape is `{bridge, states, windows[], sessions[], error}`; arrays are always arrays; `error` is a string or JSON `null`. `Ensure()` creates `hosts`, so a fresh install does not hit the error path | Verified (one note below) |
| Regression (ANALYSIS-001/002/004/005) | Re-ran the analysis-002 probe script unchanged (Appendix A, set 2); source `service.js:97-156`, `VsCodeNavigator.cs:97-170, 187-245` | A, A-noteFocus, C, E, G and H give the same output as round 2. Unavailable-sample handling, the snapshot, `ReadRegistration` and receipt cleanup and sweep are unchanged | Not regressed |
| ANALYSIS-103 exposure | Probe F re-run; source `service.js:61-71` (`interacted` unchanged) | F still records the sibling `2222` first. The next real focus gain now publishes `multiple` instead of an unexplained `null` | Exposure unchanged; the later state is now reported honestly |
| Parity / transport | `git diff --stat 9b26088` and `git status --porcelain` over `src/BellTower/{Providers,Integration,Sessions,Core}`, `Setup/ProviderSettingsEditor.cs`, `src/BellTower.Ingress`, `src/BellTower.Protocol` (all empty); case-insensitive grep for `claude`/`codex` in the bridge, host, reporter, store and outcome code (none) | The registration field is additive and provider-neutral. Claude and Codex parity is asserted in `:474` and `:543` | No per-provider policy and no transport change. Both clients are covered by construction and in fixtures; live per-client checks were not run |
| Functional / format | `node --test vscode/service.test.js vscode/focus.test.js`; `dotnet format ... --verify-no-changes --include <this round's .cs files>` | 39 passed, 0 failed; format exit 0 | .NET build and tests not run (QA scope) |

## Findings

### ANALYSIS-150: The doctor's "session process has ended" check re-implements the confirmed-ended rule of `AttachmentHealth`

- Severity: suggestion (non-blocking)
- Related task / requirement: 5.2 (the ANALYSIS-102 fix)
- Locations:
  - `src/BellTower/Diagnostics/VsCodeBridgeReporter.cs:44-55`: `EndedAt: null` filter, then `ProcessProbe.Inspect(shell) == Ended || Process is { } agent && Inspect(agent) == Ended`.
  - `src/BellTower/IO/AttachmentHealth.cs:11-15`: `EndedAt is not null || agent == Ended || shell == Ended` gives `confirmed-ended`.
- Observation:
  - Implementation 003 added the ended check inline in Diagnostics. Today it matches the canonical session-health rule in `IO/AttachmentHealth` exactly.
  - `DiagnosticsReporter` advertises that rule as provider-shared ("verified-live, confirmed-ended, ..."). A later change to the canonical rule, such as how `Unverified` or shared-runtime processes are classified, would leave the doctor saying "session process has ended" for sessions the tray treats differently, or the reverse.
  - The duplication is small (two lines) and has no runtime effect today.
- Proposed improvement and benefit:
  - Expose the decision once, for example `public static bool IsConfirmedEnded(Attachment attachment, Func<ProcessIdentity, ProcessLiveness> inspect)` on `AttachmentHealth`.
  - `Evaluate` and the reporter both call it. Benefit: one ended rule for both providers and both surfaces.
- Behavior-preservation risks:
  - The `EndedAt` filter in the reporter must stay: it skips ended attachments instead of reporting them.
  - The report stays read-only, so `Evaluate`'s `with` record must not be persisted.
  - Parity stays automatic.
- Evidence type: source observation
- Verification / measurement plan: the existing `DoctorReportsWindowVerificationEquallyForClaudeAndCodex` (dead-shell case) plus the existing `AttachmentHealth` tests; no new test is needed.
- Relationship: residual of the ANALYSIS-102 fix. Can be deferred past this final cycle without risk.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-100 | `diff=3a6df874e2c9 untracked=9/7e8916580128` | **Reason tracks the guard state exactly.** `windowReason` is assigned in one place, the trip branch (`service.js:136-141`), at the same moment `seenHandles.size` first exceeds 1. `publish` writes it only while `window` is null (`:201`), and the window cannot become non-null after the trip: the record branch needs `size === 1`, `seenHandles` only grows after the trip, and `selfHeal` returns early on a null window (`:167`). Probes D and D2 show `(absent)` before the trip and `multiple` after it, including after heal attempts naming either handle and after a foreign sample. Fuzz: 0 violations of "reason ⇔ size>1", "never window+reason" and "no window after trip" over 7,500 steps. **The reason can never re-enable evidence.** C# reads it only inside the `Window is null` branch and returns `Unverified` either way (`VsCodeWindowOwnership.cs:41-49`). An unknown value falls back to "not yet observed". A registration without the field keeps the old text (test `:566-569`). The `interacted` path is also closed after the trip (`seenHandles.size === 0`). Diagnostics: the chip tooltip, the failed-click message, `doctor` windows and sessions, `installation.md` and `compatibility.md` all name Developer: Reload Window, asserted for matched Claude and Codex sessions (`:543`). Note (pre-existing; performance hypothesis, not measured): after the trip, every later focus gain still spawns one sampler process whose result can only re-confirm the guard (probe D: 4 samples) | verified |
| ANALYSIS-101 | same | `TrayApplication.cs:315-327`: the single call `if (NavigationOutcome.ApplyViewed(...)) Tick();` inside the existing `try/catch` for IO, permission and JSON errors. The three conditions now live only in `NavigationOutcome.cs:15`. Behavior is identical | verified |
| ANALYSIS-102 | same | **Layering.** `AttachmentStore.List()` (IO, `:9-33`) is read-only, uses ordinal order and skips unreadable files (IO, JSON, invalid data and permission errors, matching `ReadFresh`). It is now the only enumerator of `paths.Attachments`. The reporter (Diagnostics) consumes it and delegates verification to `VsCodeNavigator.Explain` (Hosts), consistent with `docs/development.md`. The arbitrary `Take(200)` is gone. **Doctor reuse.** Dead-shell sessions are reported as `session process has ended` for both providers (`:474`, asserts at `:515-517`). Residual: the ended rule duplicates `AttachmentHealth` (ANALYSIS-150). Performance hypothesis (not measured): attachments are not pruned, so the doctor now decodes every attachment file. It is doctor-only and runs once per command. Measure only if field doctor runs are slow (time `List()` over a synthetic directory of about 10,000 files) | verified (residual ANALYSIS-150) |
| ANALYSIS-005 (ENOENT residual) | same | `service.js:295-300` tolerates only `ENOENT`. Probe Unlink_ENOENT (a real ENOENT, file already removed): receipt present, request gone, 0 "A focus request could not be completed" lines. Unlink_EPERM: request kept queued, 1 line. Node tests `service.test.js:530-560` pass | verified |
| QA-100 (structure) | same | `VsCodeBridgeReporter.Report()` always returns `{Bridge, States, Windows: List, Sessions: List, Error: string?}`. `Json.Options` uses Web defaults with no null-ignore, so `error` is serialized as JSON `null` when there is no failure. A missing `hosts` directory is only reachable artificially, because `doctor` resolves paths through `CommonOptions.Paths`, which calls `DataPaths.Ensure()`. Test `:530` asserts the array and string kinds; the happy-path test asserts the arrays. The shape change is documented in `docs/compatibility.md`. Note (no action needed): if `Sessions()` failed mid-iteration, `error` would be set while `windows` and part of `sessions` are kept. That is only possible in a race, and the documented contract does not promise empty arrays on error | verified |
| ANALYSIS-001 | same | The analysis-002 probe script re-run unchanged gives the same outputs: A `2, 1111`; A-noteFocus `2, 1111`; C discards then records `1111`; E self-heals | not regressed |
| ANALYSIS-002 | same | `service.js:119-124` unchanged (an unavailable sample keeps the record, logs and retries within bounds); `tick()` still serves requests before `refreshWindow()` (`:241-242`) | not regressed |
| ANALYSIS-004 | same | `VsCodeNavigator.cs:187-245`: `Snapshot` (re-filtered per use), `ReadFresh` and `ReadRegistration` (`FileShare.ReadWrite \| Delete`) are unchanged; `atomic()` is unchanged | not regressed |
| ANALYSIS-005 (cleanup) | same | `VsCodeNavigator.cs:97-170`: removal on read, including an unreadable receipt; removal on timeout; bounded `Remove` retry; 1-minute sweep. All unchanged | not regressed |
| ANALYSIS-103 (exposure only; deferred to 4.2) | same | `interacted` (`service.js:64-65`) is unchanged; probe F still records the sibling `2222` first. The false-success window between that mis-record and the next real focus gain is unchanged. After the next gain, the trip is now published as `multiple` and reported with a reload remedy instead of an unexplained `null` | exposure unchanged; post-trip diagnostics improved; remains deferred to live 4.2 |

## Assessment and limitations

- **Rechecks.**
  - ANALYSIS-100: the published reason tracks the guard exactly and cannot re-enable evidence (bridge and tray). Diagnostics now give a workable remedy for Claude and Codex alike.
  - ANALYSIS-101, ANALYSIS-102, the ANALYSIS-005 ENOENT residual and QA-100: verified.
  - ANALYSIS-001, ANALYSIS-002, ANALYSIS-004 and ANALYSIS-005: not regressed.
- **New:** ANALYSIS-150 (suggestion, non-blocking). A small duplication of the confirmed-ended rule, introduced by the ANALYSIS-102 fix. It is safe to defer past this final cycle.
- **Parity:** confirmed. There is no per-provider policy and no transport or provider-adapter change. The new registration field is additive and provider-neutral.
- **Performance:** no measurements were taken this round. Two items are labeled hypotheses: the post-trip sampler spawn per focus gain, and the unbounded `List()` decode in the doctor. Neither is recommended for change without field evidence.
- **Not performed:**
  - .NET build and test (QA scope; the developer reported 870 passed twice).
  - Live task 4.2: floating-window semantics, real foreground grant, and the ANALYSIS-103 activity probe.
- **Unexpected writes:** none. The client refused both the report write and the evidence write ("Subagents should return findings as text"), so the text is returned inline. Disposable probe data is only in the scratchpad, and each probe deleted its own root.
- **Decision for the orchestrator:** triage ANALYSIS-150 (fix now or defer).

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.310 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: Analysis 003 (recheck 2, final) of Implementation 003; ANALYSIS-100 verified, ANALYSIS-101 verified, ANALYSIS-102 verified (residual ANALYSIS-150), ANALYSIS-005 ENOENT residual verified, QA-100 structure verified; ANALYSIS-001/002/004/005 not regressed; ANALYSIS-103 exposure unchanged (deferred to 4.2); new ANALYSIS-150 (suggestion, non-blocking)
changed or report paths: none written (client refused both writes). Intended: reports/analysis-003.md and evidence/analysis-003-probes.md. Disposable probe: %USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\analysis-003-probe.js
checks: node --test vscode/service.test.js vscode/focus.test.js -> passed (39/39); analysis-003 probe set (D, D2, E, fuzz 7,500 steps, ENOENT/EPERM unlink) -> run, 0 invariant violations; analysis-002 probe script regression re-run -> identical to round 2; dotnet format --verify-no-changes (this round's .cs files) -> passed; dotnet build/test -> not run (QA scope)
target identity: before head=8167c5d baseline=9b26088 files=33 diff=3a6df874e2c9 untracked=9/7e8916580128; after identical
limitations / decisions needed: the client refused both the report and evidence writes (role write fallback), so the full text was returned inline for the orchestrator to save. Live 4.2 not performed. Performance items are hypotheses only (post-trip sampler spawn per focus gain; doctor decodes all attachments). Decision: triage ANALYSIS-150 (fix now or defer past the final cycle).
next: orchestrator saves the report and Appendix A, updates the ledger for ANALYSIS-100/101/102/005-residual/QA-100, triages ANALYSIS-150, and proceeds to live task 4.2 (including the ANALYSIS-003/100 floating-window and ANALYSIS-103 activity probes).
```
