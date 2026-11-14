<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-033 / code-analyzer; Client: Claude Code 2.1.323 delegated subagent, auto mode; Model: claude-opus-5-5; Refusal: the client refused the report write ("Subagents should return findings as text"), as reported by the reviewer. -->

ANALYSIS-033 (code-analyzer) is done. The write of reports/analysis-033.md was refused ("Subagents should return findings as text"). I did not retry, so the full report is inline below for the orchestrator to save. Result: one warning and five suggestions. The code fits the architecture and the new code relies on no undocumented provider internals. The warning, ANALYSIS-3300, is that design decision 5 and the session-identity spec still forbid the colour behaviour that was implemented.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.323 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: tasks 4.1, 4.3, 4.4, 5.2, 5.3; new ANALYSIS-3300 (warning), ANALYSIS-3301, 3302, 3303, 3304, 3305 (suggestions)
changed or report paths: reserved openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-033.md (write refused; report inline below)
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <27 changed C# files> -> passed; review-identity before/after -> passed (identical); xterm.js OSC docs via ctx7 -> fetched; build/test -> not run (QA runs concurrently; assignment forbids)
target identity: before "head=1c80a7d baseline=1c80a7d files=32 diff=e01e1338ee8d untracked=7/3f79a1ab1b57"; after identical
limitations / decisions needed: report write refused by the client, full text inline; ANALYSIS-3300 needs an orchestrator artifact update (design decision 5/8 and session-identity scenarios) to record the 2026-11-13 reset-to-default colour decision; no real host run and no performance measurement
next: orchestrator saves the report, triages 3300 to 3305, and pairs them with the QA result
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-033

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.323, delegated subagent in auto permission mode, `claude-opus-5-5`
- Date: 2026-11-13T19:00Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-033 (the single final code-analyzer review of the reduced scope, design decision 8)
- Task and requirement references: tasks 4.1, 4.3, 4.4, 5.2, 5.3 ([tasks.md](../tasks.md)); design decisions 5, 7, 8 ([design.md](../design.md)); delta specs [session-identity](../specs/session-identity/spec.md) (Presentation ownership for adopted terminals), [standalone-setup](../specs/standalone-setup/spec.md), [session-tracking](../specs/session-tracking/spec.md); developer evidence [implementation-045](../evidence/implementation-045.md), [implementation-046](../evidence/implementation-046.md)
- Scope and relevant neighboring paths: the 32 assigned paths. Neighbors read for context only: `src/BellTower/Hosts/Navigator.cs`, `src/BellTower/Hosts/VsCodeNavigator.cs`, `src/BellTower/Tray/SessionViewFactory.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/IO/ConfigStore.cs`, `src/BellTower/Integration/BellTower.psm1`
- Baseline commit: 1c80a7d (dirty tree)
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 1c80a7d -Paths <32 paths>` -> `Summary: head=1c80a7d baseline=1c80a7d files=32 diff=e01e1338ee8d untracked=7/3f79a1ab1b57` (matches the assignment)
- Before/after identity: the same command after the analysis -> `Summary: head=1c80a7d baseline=1c80a7d files=32 diff=e01e1338ee8d untracked=7/3f79a1ab1b57`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: none rechecked. [analysis-032](analysis-032.md) covered the previous (DEV-044) target.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture: single decision point | Inspected `Core/NativePresentation.cs` and its consumers | `TerminalPresentationResolver.cs:14-15`, `TerminalTitleService.cs:25-26,98-99`, `WindowsTerminalFocusService.cs:14`, `WindowsTerminalNavigator.cs:12-13` | Policy lives in Core and has no provider branches. Ingress is unchanged. Claude Code and Codex share one path, and shared-backend Codex is excluded by the evidence's mode and lifecycle owner. Two call styles exist and the grant is computed twice (ANALYSIS-3302). |
| Architecture: layering | Compared against `docs/development.md` layers | `TitleRestore` (Core record), `TitleRestoreStore` (IO), console attach stays in the disposable `sync-title` helper (Hosts) | Fits the layers. Console attachment stays out of the tray process. |
| Simplest safe restore design | Traced `WriteNative`, `Restore`, `RestoreTitle`/`ResetColors`, pump `RestoreUnpresentedAsync`, `sync-title --restore` | `TerminalTitleService.cs:41-87`, `TerminalTitlePump.cs:149-186`, `SyncTitleCommand.cs:15-24` | A separate record is justified: the attachment is written by ingress, and the agent may already have exited at restore time. Keying the record by attachment needs two cross-record scans and gives an order-dependent result in one edge case (ANALYSIS-3301). |
| Duplication | Diffed `OpenShell` against `Open`. Compared `TerminalColors.Reset` with `BellTower.psm1:134`. Compared the doctor `nativeSessions` texts with the matrix rows. | `AttachedSessionConsole.cs:14-48`, `DiagnosticsReporter.cs:85-103` | ANALYSIS-3303, ANALYSIS-3305 |
| Artifact consistency | Compared the implemented color behavior with design decision 5 and the spec scenarios | `design.md:94-96`, `specs/session-identity/spec.md:27-41`, implementation-045 follow-up section | The artifacts contradict the code (ANALYSIS-3300). |
| Performance (pump per refresh) | Source inspection only, nothing measured | Tray tick 250 ms (`TrayApplication.cs:21`) and pump every 2 s (`TrayApplication.cs:278-280`). Each refresh adds one `ConfigStore.Read` per live session in the resolver and one `TitleRestoreStore.List()`. Each attention chip adds one fresh `ConfigStore.Read` in `WindowsTerminalNavigator.Precision` per 250 ms tick. | Hypothesis: negligible next to the existing per-chip `ProcessProbe` and `EnumWindows` work. Each restore helper reads every attachment file, and I found no attachment pruning (ANALYSIS-3301). The flat 30 s restore retry logs on every attempt (ANALYSIS-3304). |
| Provider-internals dependency | Listed the surfaces the slice adds. Fetched xterm.js docs through ctx7 (`/websites/xtermjs`, supported OSC table). | Hooks; OS process identity; Win32 `AttachConsole`, `GetConsoleProcessList`, `Get/SetConsoleTitle`; OSC 110/111 (xterm.js documents "Restore default foreground/background to themed color" as supported); the existing UIA tab selector (temporary marker title) | The new code relies on no undocumented provider internal. Qualification relies on the existing decision 7 mode classification. The non-owning shared-backend observer (decision 4, retained by decision 8) is the remaining Codex-internal dependency and is outside this slice. |
| `NavigationOutcome.NeedsNotice` | Inspection against the focus result vocabulary | Results are `exact tab`, `exact terminal`, `window only`, `unavailable` (`WindowsTerminalFocusService.cs:49`, `VsCodeNavigator.cs:78-94`) | Minimal and shared by both providers. No finding. |
| OSC 110/111 reset | Inspection and docs fetch | `TerminalColors.cs:9-10`, `AttachedConsoleTitleWriter.cs:25-29` | Correct sequences, written only through the shell-verified console. Reset is attempted only when `Colored` is set. Retry is safe because the title step returns true once the console no longer shows Bell Tower's title. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <27 changed C# files>` | exit 0 | passed |
| Build / tests | not run | QA runs them on the same tree at the same time, and the assignment forbids output-deleting runs | not performed |

## Findings

### ANALYSIS-3300: Design decision 5 and the session-identity spec still forbid the color behavior that was implemented

- Severity: warning
- Related task / requirement: 4.3, 5.3; design decision 5; session-identity "Presentation ownership for adopted terminals"
- Locations: `openspec/changes/adopt-native-sessions-from-hooks/design.md:96`; `openspec/changes/adopt-native-sessions-from-hooks/specs/session-identity/spec.md:27-29,39-41`; implementation in `src/BellTower/Core/NativePresentation.cs:27-28` and `src/BellTower/Hosts/TerminalColors.cs:9-10`. Stale text that only grants a title: `src/BellTower/Core/AppConfig.cs:15` ("the managed tab title"), the `GrantsTitle` name and the `Effective` summary (`NativePresentation.cs:23,26`), and the test comment "the background is never granted" at `tests/BellTower.Tests/Hosts/NativePresentationTests.cs:101`, which the next assertion contradicts.
- Observation: decision 5 says "If safe prior-value restoration is unavailable, leave that surface unchanged". The spec scenario "No safe restoration mechanism is available" requires Bell Tower to leave a color surface unchanged when its prior presentation cannot be established. "Native presentation is on" says it "restores only what it changed". The code instead writes the palette background with no recorded prior color and later resets it to the terminal default with OSC 110/111. That follows the 2026-11-13 user decision, which is recorded only in implementation-045 ("Follow-up"). The design, the spec and the code now give contradictory rules. One accepted trade-off is not stated anywhere: a background the user set before the session is replaced by the profile default when the session ends.
- Proposed improvement and benefit: the orchestrator updates design decisions 5/8 and the session-identity scenarios (through the update-change workflow) to record the user decision. Native background is reset to the terminal default and never restored. The prior title is restored. Correct the stale title-only texts in the same pass. Verification and the 6.1 trial will then judge one rule.
- Behavior-preservation risks: none to code. Only the artifacts and comments change.
- Evidence type: source observation
- Verification / measurement plan: `openspec validate --strict`, then reread the scenarios against `NativePresentationTests`.
- Relationship: new

### ANALYSIS-3301: Key the title-restore record by shell identity rather than by attachment

- Severity: suggestion
- Related task / requirement: 4.3; restoration "SHALL NOT write to a reused or unrelated shell" and "restores only what it changed"
- Locations: `src/BellTower/Hosts/TerminalTitleService.cs:41-52` (inheritance scan through `records.List()`), `:62-87` (`Restore`: `AttachmentStore.List()` plus a `records.Get` for every attachment); `src/BellTower/IO/TitleRestoreStore.cs:49-52`; `src/BellTower/Hosts/TerminalTitlePump.cs:149-186`
- Observation: the console title belongs to the shell's console, but each attachment gets its own record. A second session in the same shell therefore has to copy the first record's `Previous`, and `Restore` has to scan every attachment ever stored, because `AttachmentStore` has no pruning. The result also depends on order. Take two live qualifying sessions in one shell when the setting is switched off. The pump restores records in hashed-filename order. The first is removed without a write because the other still has a record. The second calls `RestoreTitle` with its own `Written`. If the other session wrote last, the console does not match, `RestoreTitle` returns true, the record is deleted and Bell Tower's title stays. The case is rare: it needs two concurrently live adopted direct agents whose recorded shell is the same process. The sequential case, where a new session starts before the old record is restored, works correctly.
- Proposed improvement and benefit: store one record per shell (file name from the PID plus creation time) with `Previous`, `Written`, `Colored` and the owning attachment, which is the last writer. `WriteNative` reuses the shell's record or reads the title once. The pump restores a record when its owning attachment is not presented. This removes both scans and the "another live session" rule, and the last written title always matches the record. `sync-title --restore` can keep taking an attachment ID and resolve it to the shell record.
- Behavior-preservation risks: existing `title-restore` files from dev builds use attachment-keyed names. Ignore or migrate them, since the records are not yet released. Keep the gone or reused shell discard and the retry-on-failure behavior. Keep `Colored` true across title-only rewrites.
- Evidence type: source observation. The order-dependent outcome follows from the code. It was not reproduced.
- Verification / measurement plan: extend `ASecondSessionInTheSameShellInheritsTheOriginalTitleAndTheFirstExitLeavesItAlone` with a switch-off variant that runs both write orders and asserts the prior title is restored once, for both providers.
- Relationship: new

### ANALYSIS-3302: One call shape for the native grant and one setting source

- Severity: suggestion
- Related task / requirement: 4.3; decision 5 (single decision point)
- Locations: `src/BellTower/Hosts/TerminalPresentationResolver.cs:14-15`; `src/BellTower/Hosts/TerminalTitleService.cs:8,25-26,98-99`; `src/BellTower/Hosts/WindowsTerminalFocusService.cs:14`; `src/BellTower/Hosts/WindowsTerminalNavigator.cs:12-13`; `src/BellTower/Core/NativePresentation.cs:23-28`
- Observation: three call sites run `var native = GrantsTitle(stored, enabled); var attachment = Effective(stored, native);`. They pass the grant back as `enabled`, which evaluates `IsAdoptedDirect` a second time. The other two call `Effective(stored, setting)`. The setting is read through a resolver-held `ConfigStore`, which keeps its last good value, in one place. It is read through fresh `new ConfigStore(paths)` instances, which fall back to defaults and log `config-invalid` on every failed read, in three. `Precision` runs for each attention chip on every 250 ms tick. With an unreadable config, the resolver therefore keeps presenting (last good: on) while precision and tab focus report the setting as off and the log repeats every tick. This fails closed but is inconsistent.
- Proposed improvement and benefit: one function, for example `NativePresentation.Apply(Attachment, bool enabled) => (Attachment Effective, bool Native)`, or callers compare `effective.ManagedTitle != stored.ManagedTitle`. Rename `GrantsTitle`, which also grants the background. Give the in-tray consumers (navigator, focus service, resolver) one injected setting reader such as `Func<bool>` from a shared `ConfigStore`. The helper keeps its own read under the presentation lock. All four consumers then use one call shape.
- Behavior-preservation risks: the helper's re-read under the lock (`TurningTheSettingOffStopsWritesEvenForARequestComputedEarlier`) must stay. Precision must keep updating live after a toggle.
- Evidence type: source observation. The per-tick read cost is a performance hypothesis and was not measured.
- Verification / measurement plan: the existing `NativePresentationTests` and `TerminalTabSelectionTests` must pass unchanged, and both mutants named in implementation-045 must still be killed.
- Relationship: new

### ANALYSIS-3303: Merge `OpenShell` into `Open` and open the console once per restore

- Severity: suggestion
- Related task / requirement: 4.3 (restoration safety)
- Locations: `src/BellTower/Hosts/AttachedSessionConsole.cs:14-48`; `src/BellTower/Hosts/AttachedConsoleTitleWriter.cs:25-39`; `src/BellTower/Hosts/TerminalTitleService.cs:81`
- Observation: `OpenShell` copies the safety-relevant verify-attach-verify sequence of `Open` (identity check, `AttachConsole`, 256-entry process list, membership, identity recheck) without the agent checks. A later fix to one copy can miss the other. A colored restore also attaches twice, once for `RestoreTitle` and once for `ResetColors`.
- Proposed improvement and benefit: one private `Attach(ProcessIdentity shell, ProcessIdentity? agent)` used by both public openers, and one writer method, for example `Restore(shell, expected, previous, resetColors)`, that opens the console once. This gives one verification path and one attach per restore.
- Behavior-preservation risks: keep the partial-failure semantics. Title restored but reset failed must leave the record for a retry. Title no longer shown must count as done.
- Evidence type: source observation
- Verification / measurement plan: `ExitRestoresOnlyWhatBellTowerChangedAndOnlyOnTheRecordedShell`, `ARecordWithoutColorNeverResetsColors` and `ARecordedShellThatIsGoneOrReusedIsDiscardedWithoutAnyWrite` must stay green.
- Relationship: new

### ANALYSIS-3304: The restore retry repeats the pump's backoff and diverges from it

- Severity: suggestion
- Related task / requirement: 4.3
- Locations: `src/BellTower/Hosts/TerminalTitlePump.cs:18,149-186` against the write backoff at `:118-127`
- Observation: writes back off over 2, 5, 15, 30 and 60 s and log at most once a minute per destination. Restores use a second dictionary, a flat 30 s retry, and log `title-restore-unavailable` on every failed attempt. A restore that keeps failing while its shell stays open starts one helper process every 30 s and writes one log line each time for the life of that shell. This is bounded but noisier than the write path.
- Proposed improvement and benefit: reuse the `Destination` backoff and log throttle for restore targets, or at least share the delay table and once-a-minute logging. That leaves one retry policy in the pump.
- Behavior-preservation risks: a restore must still be retried after a tray restart (records are listed from disk), and a successful restore must clear the retry state.
- Evidence type: source observation. Helper start cost is a performance hypothesis.
- Verification / measurement plan: a pump test with a writer whose `RestoreAsync` keeps failing, asserting the backoff schedule and at most one log line a minute.
- Relationship: new

### ANALYSIS-3305: Hand-maintained restatements without a source link (doctor `nativeSessions`, reset sequence)

- Severity: suggestion
- Related task / requirement: 5.2, 4.3
- Locations: `src/BellTower/Diagnostics/DiagnosticsReporter.cs:85-103`; `src/BellTower/Diagnostics/CapabilityMatrix.cs` (the four new rows); `src/BellTower/Hosts/TerminalColors.cs:9-10` against `src/BellTower/Integration/BellTower.psm1:134`; `tests/BellTower.Tests/Hosts/NativePresentationTests.cs:289-293`
- Observation: `nativeSessions` restates the matrix rows "in short form" as separate literals. `ClaudeDirect` and `CodexNoDaemon` are identical objects. The same doctor output already includes `CapabilityMatrix.Rows`, and nothing ties the two texts together. Each edit must change both, and the tests pin substrings in both. Separately, the test named `...IsTheOneManagedLaunchesUse` asserts only the C# literal, while the PowerShell module keeps its own copy of the sequence.
- Proposed improvement and benefit: keep only what is state-dependent in `nativeSessions` (setting on or off and the current Windows Terminal precision) and name the matrix rows for the policy text. Alternatively, build both texts from shared constants. For the reset sequence, read `BellTower.psm1` in the test, as `CompatibilityDocumentStatesMatchTheMatrixSource` already does for the docs, and assert it contains the same sequence. This means less text to keep in sync as provider releases change behavior.
- Behavior-preservation risks: doctor JSON consumers. The `nativeSessions` shape is new in this change, so changing it costs nothing yet.
- Evidence type: source observation
- Verification / measurement plan: the updated `DoctorReportsTheNativePresentationStateAndBothPrecisionsPerProvider`, plus a psm1 containment assertion.
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | not applicable | not applicable | not performed (no recheck assigned) |

## Assessment and limitations

The slice is small and fits the architecture. Policy sits in one Core function. Ingress, capture and the hook path are untouched. Both providers share one code path, tested in matched Claude Code (`interactive-client`, `unknown`) and Codex `--no-daemon` cases with a mixed simultaneous case. Shared-backend Codex is excluded by its stored evidence. The new code adds no dependency on undocumented provider internals. It uses hooks, operating-system process identity, documented Win32 console APIs, OSC 110/111 (xterm.js documents both as supported, so the VS Code side is now documented and only host-verified behavior remains open) and the existing UIA tab selector.

A separate title-restore record and a `sync-title --restore` helper are a reasonable minimum. A restore must work after the agent has exited, console attachment must stay in a disposable process, and the attachment record is ingress-owned. Keying the record by shell (ANALYSIS-3301) is the main simplification still open.

Only ANALYSIS-3300 is a warning. It is artifact drift: the design and spec still forbid the implemented color behavior. The other findings are bounded simplifications.

Observations for QA and the orchestrator, not raised as findings:

- When a live session's resolve returns null transiently, for example because the attachment health dips to `unavailable` inside the evidence grace, the pump restores the session and later re-applies it. The user would see the title and color flap. This is a hypothesis and was not reproduced.
- Task 4.1 kept the single-visible-window rule. With several windows in one Terminal process the click still fails, now with a stated reason. Whether that satisfies 4.1 is a scope question for QA and the orchestrator.

Not performed: builds and tests (QA owns the concurrent run), any real host run (task 6.1), and performance measurements. The client refused the report write, so the report is returned inline. I wrote nothing else.
