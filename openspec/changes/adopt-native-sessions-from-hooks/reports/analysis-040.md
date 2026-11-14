<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-040 / code-analyzer; Client: Claude Code Agent SDK subagent, non-interactive (version not exposed; last reported 2.1.325); Model: claude-opus-5-5; Refusal: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The reviewer's first return wrongly named the file as written; its corrected second return supplied this text. -->

```
status: done
role / client / model: code-analyzer / Claude Code (Agent SDK subagent; version not exposed, last reported 2.1.325) / claude-opus-5-5
task / finding IDs: DEV-053 recheck / ANALYSIS-3900 (verified, both providers and both hosts), ANALYSIS-3801 (a) verified, (b) not addressed, (c) verified; no new findings (ANALYSIS-4000..4099 unused)
changed or report paths: none written. The write of openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-040.md was refused; full report inline below
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <11 changed .cs files> -> passed (exit 0); git ls-files --eol -> passed (changed files w/crlf); build/test/mutant rerun -> not run (QA owns them)
target identity: before head=d465737 baseline=d465737 files=17 diff=971804bb3cca untracked=3/7e1dd81cc9e4; after: identical
limitations / decisions needed: report write refused ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), text inline. All from reading the source, no live terminal checks. Mutant results taken from the evidence. Leftover items for triage only: 3801(b), which assertion failed under mutant (b), one test name and one stale comment, no cross-provider nesting test.
next: orchestrator saves the report and triages. Optionally a developer test-only cleanup for the leftovers, then close ANALYSIS-3900 once QA confirms.
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-040 (recheck of ANALYSIS-3900 and the ANALYSIS-3801 limitations after DEV-053)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent), non-interactive. The version is not exposed to the subagent; [analysis 039](analysis-039.md) last reported 2.1.325. Model claude-opus-5-5
- Date: 2026-11-14T10:53Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-040 (recheck)
- Task and requirement references:
  - Fix cycle DEV-053 ([implementation 053](../evidence/implementation-053.md))
  - Session-identity requirement "Presentation ownership for adopted terminals" ([spec](../specs/session-identity/spec.md))
  - The navigation and presentation rows for adopted direct sessions in the capability matrix
- Scope and relevant neighboring paths:
  - Target: the 14 modified tracked files outside `openspec/`:
    - `docs/attachment-health.md`, `docs/compatibility.md`, `docs/installation.md`
    - `src/BellTower/Core/AppConfig.cs`, `src/BellTower/Core/HostTarget.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Core/NativePresentation.cs`
    - `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/Tray/TrayApplication.cs`
    - `tests/BellTower.Tests/Hosts/NativePresentationTests.cs`, `tests/BellTower.Tests/Hosts/TerminalTabSelectionTests.cs`, `tests/BellTower.Tests/Hosts/VsCodeWindowNavigationTests.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs`
    - plus the three untracked evidence files.
  - Neighbors read for side effects:
    - `src/BellTower/IO/AttachmentStore.cs` (`Adopt`), `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/Core/AttachmentHealth.cs`
    - `src/BellTower/Hosts/TerminalPresentationResolver.cs`, `src/BellTower/Hosts/TerminalTitleService.cs`, `src/BellTower/Hosts/TerminalTitlePump.cs`
    - `src/BellTower/Hosts/VsCodeNavigator.cs`, `src/BellTower/Hosts/WindowsTerminalNavigator.cs`, `src/BellTower/Hosts/WindowsTerminalFocusService.cs`, `src/BellTower/Diagnostics/VsCodeBridgeReporter.cs`
    - `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, `src/BellTower/Providers/CaptureConsumer.cs`, `src/BellTower/Providers/HookIngestor.cs`
    - `tests/BellTower.Tests/Testing/DisposableSleeper.cs`
    - every other `Host.Shell` / `ShellSource` reader that `grep` found over `src`, `docs`, `vscode` and `tests`.
- Baseline commit: `d465737`
- Implementation identity (before):
  - HEAD `d46573748ed65bb3d4c22a371fc43d4a2d7772f4`, baseline the same.
  - Manifest: `?` for `evidence/implementation-051.md`, `evidence/implementation-052.md` and `evidence/implementation-053.md`; `M` for the 14 tracked files above.
  - Tracked diff SHA-256 `971804bb3cca4a3ca814f61c57d5745980e64d3cebacf389353471f206272ed7`.
  - Untracked:
    - `646949248b2ddfeff47a28b939be6b2b32d951099bbcb74890b5c15ab6cb9848` (implementation-051.md)
    - `3e1317f7b4df97a4db4ad7581090e3bb70673e4dbd918a369e6a2f8de31dc315` (implementation-052.md)
    - `758e651ae1fd7ec1439de1d16c008bd6c0f9f8d6ef31bb86da8ac5b306955202` (implementation-053.md)
  - Summary `head=d465737 baseline=d465737 files=17 diff=971804bb3cca untracked=3/7e1dd81cc9e4`.
- Before/after identity: after the analysis, the same command gave `head=d465737 baseline=d465737 files=17 diff=971804bb3cca untracked=3/7e1dd81cc9e4`. This matches both the before identity and the assigned identity, so nothing in the target changed.
- Prior reports / rechecked findings: ANALYSIS-3900 from [analysis 039](analysis-039.md). ANALYSIS-3801 from [analysis 038](analysis-038.md), with limitations (a) to (c) recorded in analysis 039.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| The rule, both providers | Read the `Reconcile` nested branch and `NativePresentation` | `NativeAdoption.cs:209-214` has one provider-neutral condition, `host.Shell is { } nestedShell && previous.Host.Shell == nestedShell`. It now keeps `Shell` and sets `ShellSource = HostTarget.LaunchShared`. `NativePresentation.cs:19` adds `attachment.Host.ShellSource != HostTarget.LaunchShared` to `IsAdoptedDirect`. `GrantsPresentation` and `Effective` are unchanged and are still the only grant path. Per `grep`, their only consumers are the resolver, both `Apply` overloads of the title service, the Windows Terminal navigator and the focus service | One rule, one clause, no second policy. Neither place checks the provider, so the rule also covers a nested client of the other provider |
| Marker persistence | Read `AttachmentStore.Adopt` and `AttachmentCapture.Capture` | **First hook:** no own-ID record exists yet and `LaunchPresentationStore.Shell(ownId)` is null, so the reconciled host is saved with its marker. **Later hooks:** the inherited `BELLTOWER_ATTACHMENT` makes every capture `managed` with the launch ID, so `Reconcile` runs again and re-marks. The normal merge is `previous.Host with { Shell = previous.Host.Shell ?? freshShell, ... }`, which keeps the stored `ShellSource`. A replacement (`resume` after an end) uses `captured.Host`, which is the re-marked host. **Launch ended:** `live(recorded)` fails, so the capture keeps the launch ID. `Adopt` then returns the launch record because of the process conflict, so the nested record is no longer written and keeps its marker | The marker is never lost or reset for a record created under DEV-053. The second capture in the theory confirms the merge path. Transitional limitation: a nested record written by an earlier dev build without the marker keeps its old `ShellSource` through the merge. From `dc7eaac` that value is `ancestry`. From the uncommitted DEV-052 tree it is the dropped host, which would then get the shell back from `freshShell` with no marker. `v0.1.8` and `v0.1.9` contain no native adoption (`git show <tag>:src/BellTower/Core/NativeAdoption.cs`), so no released record is affected. Only dev-build sessions that span the upgrade are |
| VS Code navigation | Read `VsCodeNavigator.Precision`, `Explain`, `FocusAsync`, `Match`, `RevealAsync` | None of them read `ShellSource`. `Match` finds the kept shell in the registration, so the precision is `exact terminal` or `terminal only`, and a click reveals the registered terminal through the bridge. This is the same as before DEV-052 | ANALYSIS-3900 closed for VS Code, both providers |
| Windows Terminal navigation | Read `WindowsTerminalNavigator` and `WindowsTerminalFocusService.Focus` | `Precision`: `Effective` grants nothing, so the chip says `window only`. The focus helper's guard needs `Host.Shell`, which is now present, so the guard passes. `attachment.ManagedTitle` is false, so there is no retitle and no tab selection. The result is `(true, "window only", "Windows Terminal focused; its tab could not be identified. ...")`, not "no longer registered" | ANALYSIS-3900 closed for Windows Terminal, both providers |
| Health | Read `AttachmentHealth.Evaluate` | With the shell stored, the owning shell exiting ends the nested record again, as before DEV-052. That shell is the console the nested client runs in, so this is correct | No regression |
| Shell-conflict check in `AttachmentStore` | Read `Adopt` lines 70-79 | For the nested own record, `previous.Host.Shell` and `freshShell` are both the launch shell, so there is no conflict. Once `Reconcile` changes the ID, a nested capture never targets the launch record | No regression |
| Presentation writer, restore, pump | Read the resolver, both `TerminalTitleService.Apply` overloads, `Restore` and `TerminalTitlePump.RestoreUnpresentedAsync` | No grant means no desired value, no write and no `shell-title-restore` record owned by the nested ID, so its exit restores nothing. The pump restores only records whose owner is not presented, and the nested ID owns none. The launch's own wrapper-based restoration is untouched | No regression |
| Doctor and diagnostics | Read `DiagnosticsReporter.NativeSessionsReport`, `CapabilityMatrix`, `VsCodeBridgeReporter.Sessions` | None of them read `ShellSource`. `VsCodeBridgeReporter` lists the nested VS Code record again because it has a shell, and explains it through `VsCodeNavigator.Explain`, which now matches. The doctor and matrix navigation text ("the session's tab with it on") states the general rule without the nested exception. That exception is stated in `docs/compatibility.md:94` and `docs/installation.md:80` | Accurate for VS Code. The general Windows Terminal text omits the nested exception, but the exception is documented alongside it. Not a regression of DEV-053; not raised (recheck scope) |
| Capture, runtime access, launcher, palette | Read `AttachmentCapture`, `CodexRuntimeOpener.Select`, `AgentLauncher`, `CaptureConsumer:77`, `HookIngestor:30` | Capture only writes `ancestry` or `launch-env` and never reads a stored value. `CodexRuntimeOpener` compares the shell only when a runtime record exists for the attachment ID, and the nested own ID has none. `AgentLauncher` reads the shell of the attachment it launches, not of a nested record. Palette consumption is keyed by the launch ID and is skipped for inherited captures | No effect |
| Docs that enumerate shell sources | `grep -rn "launch-env\|launch-record\|launch-shared\|shellSource"` over `docs`, `openspec/specs`, the change artifacts, `Integration` and `vscode` | No hits. The only enumeration is the comment at `HostTarget.cs:9`, which now lists `launch-shared`, plus the new XML doc on the constant | Complete |
| Doc accuracy | Read `docs/compatibility.md:94` and `docs/installation.md:79-80,106` | "tracking, alerts and window navigation continue" is now true for both hosts. VS Code gives the registered terminal, which includes its window | The ANALYSIS-3900 doc contradiction is closed |
| Tests and mutants | Read the three new tests and the adjusted theory; read `DisposableSleeper` | See the re-verification table. Under mutant (a) from the evidence (ignore the marker), the shell and marker assertions still pass. So the failures in the presentation theory and the Windows Terminal test come from the behavior assertions: `GrantsPresentation`, precision, message and selections | Presentation and Windows Terminal navigation are now proven by behavior, not only by record contents. Leftover items are listed under ANALYSIS-3801 |
| Smallest change | Diff review | One constant, one conditional `with` and one clause. The marker has to be stored because `IsAdoptedDirect` cannot see the launch record, and a stored source value is the least state that carries the fact | Smallest change |
| Parity | All of the above | Claude Code and Codex share the same rule and clause, and each new test is a claude/codex theory. Cross-provider nesting (a Codex client inside a Claude launch, or the reverse) goes through the same provider-neutral branch, but no test covers it | No parity gap. The cross-provider case is a coverage limitation; not raised (recheck scope) |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <11 changed .cs files>`; `git ls-files --eol` | Exit code 0. The new and changed files are `w/crlf` | Passed |
| Builds / tests / mutant rerun | Not run | QA runs builds and tests in parallel (assignment) | Not performed. Mutant results are taken from the evidence |

## Findings

**No findings.** I found no new regression in the assigned scope. Leftover test limitations are recorded under ANALYSIS-3801 below for triage.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3900 | `head=d465737 ... diff=971804bb3cca untracked=3/7e1dd81cc9e4` | The shell is kept and marked, and the navigation code ignores the marker. **VS Code:** `Match` succeeds, the precision is `exact terminal`, and the bridge reveal names the shell (`VsCodeWindowNavigationTests.ANestedClientInALaunchShellStillRevealsTheRegisteredTerminal`, claude and codex, record built by `NativeAdoption.Reconcile`). **Windows Terminal, default setting:** success, `window only`, the normal "tab could not be identified" text, no selection, no retitle, one activation (`TerminalTabSelectionTests.ANestedClientInALaunchShellFocusesTheWindowOnlyUnderTheDefault`, claude and codex, record built by `Reconcile`). **Presentation:** still refused for both providers, with the launch presenting or opted out. The doc note is accurate. One rule, no second policy | Verified for both providers and both hosts |
| ANALYSIS-3801 (a) | same | The presentation theory now asserts `Shell == launch shell` and `ShellSource == launch-shared` before `GrantsPresentation(own, true) == false` and a null resolver result. Under mutant (a) the first two assertions still pass, so the reported failures there must come from the behavior assertions. Leftover: in the navigation tests, the marker check (`Assert.Equal(LaunchShared, ...ShellSource)`) runs before the behavior assertions, and the evidence does not say which assertion failed under mutant (b). For mutant (b), the marker check may therefore have hidden the behavior check. From the source, a dropped shell makes `Match` return null and the Windows Terminal guard fail, so the behavior assertions would also fail on their own | Verified for presentation and Windows Terminal. Navigation under mutant (b) rests on reading the source, not on the mutant output |
| ANALYSIS-3801 (b) | same | `Assert.Empty(new TitleRestoreStore(workspace.Paths).List())` is unchanged in the new theory and still passes trivially, because no title service runs in the test. The behavior it names (no restore record, nothing reset at exit) follows from the asserted refusal of `GrantsPresentation`: `TerminalTitleService.Apply` is the only writer of restore records, and it refuses without a grant. The evidence does not address (b) | Not addressed. The assertion still proves nothing by itself; the behavior is covered indirectly through the single grant function |
| ANALYSIS-3801 (c) | same | The new test `AnInheritedIdentifierWhoseRecordedLaunchIsGoneDoesNotTakeTheNestedBranch` (claude and codex) disposes the launcher sleeper (`Kill(entireProcessTree)` plus `WaitForExit`) before the capture, so `live(recorded)` is false. It asserts that the session stays on `launch-attachment`, that no own-ID record exists, and that the launch record keeps its process and its `launch-env` source. Removing `live(recorded)` would create the own-ID record and fail the test, so the test catches that change. Leftovers: (1) `ANestedClientInAnotherShellAndAStandaloneSessionAfterTheLaunchEndedKeepTheirGrant` no longer ends the launch, so its name and comment ("after the launch has ended") no longer match it. It now shows that an `observed` capture in a live launch's shell is granted, which tests the `managed`-ownership guard instead. (2) The comment at `NativeAdoptionReconcileTests.cs:52-53` still says that under the default a client "in its own verified shell is presented as a native session". That is no longer true for that test's ancestry rows, which share the launch's shell and are now marked. The explicit setting-off line there is no longer needed; removing it would let the original six-row theory assert under the default | Verified: the launch-ended guard is now tested for both providers. The name and comment leftovers are recorded for triage only |

## Assessment and limitations

This recheck covered ANALYSIS-3900 and the ANALYSIS-3801 limitations (a) to (c). It also swept every reader of `Host.Shell` and `ShellSource` for regressions: health, capture, the shell-conflict check and merge in `AttachmentStore`, the presentation writer, restore and the pump, doctor and diagnostics, both navigators and the focus helper, `CodexRuntimeOpener`, the launcher, palette consumption and the docs.

ANALYSIS-3900 is closed for Claude Code and Codex, in VS Code and Windows Terminal. Marking the shell instead of dropping it restores navigation and the correct Windows Terminal click result, while the single grant function still refuses presentation. It is still one rule and one clause with no second policy, and it is the smallest change that stores the fact. No reader treats `launch-shared` incorrectly. The marker is not lost or reset for records created by this build. Only nested records written by earlier dev builds keep their old source through the merge, and no released version contains native adoption.

Leftovers for triage, not raised as findings because this is a recheck:
- Limitation (b) is unchanged: the restore-store assertion still passes trivially.
- The mutant (b) evidence does not show which assertion failed in the navigation tests.
- One test name and one test comment no longer describe what the tests do.
- No test covers cross-provider nesting, although the rule is provider-neutral.

I did not run builds, tests or the mutant rerun (QA owns them), and I did no live terminal checks. Everything here comes from reading the source, plus the verify-only format check. The client refused the report write ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), so the report is inline. I made no other writes, and the target identity is unchanged. The orchestrator owns triage and ledger status.
