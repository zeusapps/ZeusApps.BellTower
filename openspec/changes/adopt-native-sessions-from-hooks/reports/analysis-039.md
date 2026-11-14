<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-039 / code-analyzer; Client: Claude Code 2.1.325, Agent SDK subagent, non-interactive (as reported by the reviewer); Model: claude-opus-5-5; Refusal: the client refused the report write, as reported by the reviewer. -->

The report write was refused, so the full report is inline below; please save it to `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-039.md`.

The fix closes ANALYSIS-3800 for presentation, for both Claude Code and Codex, with one shared rule and no second policy. But dropping the shell has a side effect (ANALYSIS-3900, warning, needs your decision): a nested client in the launch's shell now gets no VS Code navigation at all. Its Windows Terminal click brings the window forward but reports "The Windows Terminal session is no longer registered." That contradicts the new doc note.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.325 (Agent SDK subagent) / claude-opus-5-5
task / finding IDs: DEV-052 recheck / ANALYSIS-3800 (verified for presentation), ANALYSIS-3801 (verified, with three test limitations), ANALYSIS-3803 (verified), ANALYSIS-3804 (verified); new regression ANALYSIS-3900 (warning)
changed or report paths: none written. The write of openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-039.md was refused; full report inline below
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <8 changed .cs files> -> passed (exit 0); grep -rn -i "off by default|default false|off-by-default" docs src -> passed (no hits); build/test/mutant rerun -> not run (QA owns them)
target identity: before head=d465737 baseline=d465737 files=13 diff=e14c44638628 untracked=2/931c2df99821; after: identical
limitations / decisions needed: report write refused, text inline. ANALYSIS-3900 needs a decision: either mark the shell instead of dropping it (one clause in IsAdoptedDirect), or keep the drop and correct the doc note, the matrix and doctor text, and the Windows Terminal helper result. ANALYSIS-3900 is from reading the code and was not reproduced live. ANALYSIS-3801 has three test limitations, recorded in the re-verification table.
next: triage ANALYSIS-3900; developer fix plus tests for nested-client navigation on both hosts and both providers; then a recheck
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-039 (recheck of ANALYSIS-3800, 3801, 3803, 3804 after DEV-052)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.325, Agent SDK subagent, non-interactive; claude-opus-5-5
- Date: 2026-11-14T10:37Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-039 (recheck)
- Task and requirement references: DEV-051, fix cycle DEV-052 ([implementation 052](../evidence/implementation-052.md)); session-identity "Presentation ownership for adopted terminals" ([spec](../specs/session-identity/spec.md))
- Scope and relevant neighboring paths:
  - Target: the 11 modified tracked files outside `openspec/` (`docs/attachment-health.md`, `docs/compatibility.md`, `docs/installation.md`, `src/BellTower/Core/AppConfig.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/Tray/TrayApplication.cs`, `tests/BellTower.Tests/Hosts/NativePresentationTests.cs`, `tests/BellTower.Tests/Hosts/TerminalTabSelectionTests.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs`) and the two untracked evidence files.
  - Neighbors read for side effects: `src/BellTower/IO/AttachmentStore.cs` (`Adopt`), `src/BellTower/Core/NativePresentation.cs`, `src/BellTower/Core/HostTarget.cs`, `src/BellTower/Core/AttachmentHealth.cs`, `src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/Hosts/WindowsTerminalNavigator.cs`, `src/BellTower/Hosts/WindowsTerminalFocusService.cs`, `src/BellTower/Hosts/VsCodeNavigator.cs`, `src/BellTower/Hosts/TerminalTitlePump.cs`, `src/BellTower/Sessions/CodexRuntimeOpener.cs`, and every other `Host.Shell` reader found by `grep`.
- Baseline commit: `d465737`
- Implementation identity (before):
  - HEAD `d46573748ed65bb3d4c22a371fc43d4a2d7772f4`, baseline the same.
  - Manifest: `?` `evidence/implementation-051.md`, `?` `evidence/implementation-052.md`, `M` for the 11 tracked files above.
  - Tracked diff SHA-256 `e14c446386289dc0977c6ce0a2a5e7a5abe1f2854e0a40a2ede9f5720ea34ecc`.
  - Untracked: `646949248b2ddfeff47a28b939be6b2b32d951099bbcb74890b5c15ab6cb9848` (implementation-051.md), `3e1317f7b4df97a4db4ad7581090e3bb70673e4dbd918a369e6a2f8de31dc315` (implementation-052.md).
  - Summary `head=d465737 baseline=d465737 files=13 diff=e14c44638628 untracked=2/931c2df99821`.
- Before/after identity: after the analysis, the same command gave `head=d465737 baseline=d465737 files=13 diff=e14c44638628 untracked=2/931c2df99821`. It matches the before identity and the assigned identity, and nothing in the target changed unexpectedly.
- Prior reports / rechecked findings: [analysis 038](analysis-038.md): ANALYSIS-3800, ANALYSIS-3801, ANALYSIS-3803, ANALYSIS-3804

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-3800 rule, both providers | Read the `Reconcile` nested branch and `NativePresentation` | One provider-neutral condition, `host.Shell is { } nestedShell && previous.Host.Shell == nestedShell`, drops `Shell`/`ShellSource`. `IsAdoptedDirect` needs `Host.Shell`, so `GrantsPresentation`, `Effective`, the resolver and the title service all refuse. `ProcessIdentity` is a record, so the comparison includes creation time. It applies to a profile launch, a `bell-tower launch` and an owned-runtime launch, because each one stores its shell in `previous.Host.Shell` | Presentation is closed for Claude Code and Codex. No second policy: `GrantsPresentation` is unchanged |
| Persistence of the dropped shell | Read `AttachmentStore.Adopt` | First hook: `previous` for the own ID is null, and the launch-presentation store is keyed by the managed ID, so no shell is added back. Later hooks while the launch lives: `freshShell` is null (the captured shell is null and the store has no entry), so `previous.Host.Shell ?? freshShell` stays null | The drop holds for the life of the nested record. A nested record saved by a dev build before DEV-052 keeps its stored shell, because the merge prefers `previous.Host.Shell`. Only dev builds since `dc7eaac` can have one, so this is negligible |
| Navigation side effects | Read both navigators and `WindowsTerminalFocusService` | Windows Terminal: the chip says `window only` and `FocusAsync` brings the window forward first. The `focus-terminal` helper requires `Host.Shell`, so it then returns `unavailable` with "The Windows Terminal session is no longer registered." VS Code: `Match` requires `Host.Shell`, so the chip says `unavailable` and a click does nothing ("VS Code registration is unavailable or ambiguous.") | Regression: ANALYSIS-3900 |
| Health and lifecycle | Read `AttachmentHealth.Evaluate` | With no shell, only the agent process decides when the record ends, so the owning shell exiting no longer ends it directly. The nested agent runs inside that console, so its own exit still ends the record | Acceptable: the agent rule covers it. Listed under the risks of ANALYSIS-3900 |
| `evidence.terminal.Shell` compared with `Host.Shell` | Read `AttachmentEvidence`, how `ConversationLifecycle` uses it, `docs/attachment-health.md:34` | `TerminalEvidence.Shell` is reserved for the shared-Codex binder, and nothing writes it yet. The `evidence.terminal` rule applies only to shared conversations (`IsSharedConversation`), and a nested direct record is not one | No effect |
| Restore and pump | Read the restore path of `TerminalTitleService` and `TerminalTitlePump` | No grant means no desired destination and no `shell-title-restore` record for the nested client, so its exit resets nothing. The launch's wrapper-based restoration is untouched | Matches the evidence claim |
| Attention, alerts, runtime access | `grep Host.Shell` over `src` | Attention and alerts do not read `Host.Shell`. `CodexRuntimeOpener` compares the shell only for a runtime record keyed by the attachment ID, and the nested client's own ID has none. Palette consumption runs only for direct captures that were not inherited | No effect |
| Races | Read the order of `Reconcile` and `Adopt` | The launch dies before `Reconcile`: `live(recorded)` is false, so the branch is skipped and the capture keeps the managed ID. `Adopt` then returns the launch record unchanged (process conflict, or already ended). Nothing is written to the shell and nothing is granted; this behavior existed before DEV-052 and fails closed. The launch dies just after `Reconcile`: the nested record keeps no shell, and later hooks go to the ended launch record, so the nested client never regains a grant. That is conservative | No new race |
| Standalone session after the launch ends | Read the guard in `Reconcile` | The branch needs `captured.Ownership == "managed"` (an inherited identifier). A standalone capture is `observed` and never enters it, so its grant does not depend on the launch's state | Correct by construction. How well the test covers it is under ANALYSIS-3801 |
| Docs, doctor, matrix | `git diff`; `grep -rn -i "off by default\|default false\|off-by-default" docs src` (no hits) | The tray, `AppConfig`, `DiagnosticsReporter`, `CapabilityMatrix`, `installation.md`, `compatibility.md` and `attachment-health.md` all state the same default. The new nested note says "tracking, alerts and window navigation continue" (`docs/compatibility.md:94`). That is not true for VS Code or for the Windows Terminal click result. Doctor and the matrix promise "the registered terminal" in VS Code without that exception | Covered by ANALYSIS-3900 |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <8 changed .cs files>`; `git ls-files --eol` | Exit code 0. `AppConfig.cs`, `NativeAdoption.cs` and `NativeAdoptionReconcileTests.cs` are `w/crlf` | Passed |
| Builds / tests / mutant rerun | not run | QA runs builds and tests in parallel (assignment) | Not performed. The mutant result is taken from the evidence |

## Findings

### ANALYSIS-3900: Dropping the shell removes VS Code navigation and misreports the Windows Terminal click for a nested client

- Severity: warning (a regression introduced by the ANALYSIS-3800 fix; it contradicts the new doc note)
- Related task / requirement: DEV-052 / ANALYSIS-3800; session-identity "Presentation ownership for adopted terminals"; the navigation rows for adopted direct sessions in the capability matrix
- Locations (working tree on `d465737`):
  - `src/BellTower/Core/NativeAdoption.cs:209-214`
  - `src/BellTower/Hosts/VsCodeNavigator.cs:33-35,64-69,172-180` (`Match` needs `Host.Shell`)
  - `src/BellTower/Hosts/WindowsTerminalNavigator.cs:12-21`
  - `src/BellTower/Hosts/WindowsTerminalFocusService.cs:15-18`
  - `docs/compatibility.md:94` ("while tracking, alerts and window navigation continue")
  - `src/BellTower/Diagnostics/CapabilityMatrix.cs:102-103` and `DiagnosticsReporter.NativeSessionsReport` (VS Code: "the registered terminal")
- Observation: the fix refuses presentation by deleting a fact that navigation also uses. For a nested client (Claude Code or Codex) in the live launch's shell:
  - **VS Code:** without `Host.Shell`, `Match` returns null. The chip says `unavailable`, and a click returns "VS Code registration is unavailable or ambiguous." without bringing anything forward or revealing anything. Before DEV-052 (and with the setting off before DEV-051), the shell matched the registered terminal and a click revealed that exact terminal. That is the correct destination, because the nested client runs inside it.
  - **Windows Terminal:** the chip says `window only`, and `FocusAsync` does bring the window forward. The `focus-terminal` helper then fails its `Host.Shell` check and returns `(false, "unavailable", "The Windows Terminal session is no longer registered.")`, which becomes the click result and the balloon. The developer's "falls back to window only" is true for what happens on screen, but not for the result that is reported.
  - **No test:** nothing covers how a nested client navigates.
  - Both effects are the same for both providers. This is about host behavior and doc accuracy, not provider parity.
- Proposed improvement and benefit: keep the ANALYSIS-3800 rule in its one place, but mark the shell instead of dropping it.
  - For example, set `ShellSource = "launch-shared"` (a new documented value in `HostTarget`) and keep `Shell`.
  - Add one clause to `NativePresentation.IsAdoptedDirect`: `Host.ShellSource is not "launch-shared"`.
  - Presentation still refuses, and no restore record or reset happens. VS Code reveal, the Windows Terminal helper (window only, with its normal "tab could not be identified" text) and the owning-shell exit rule in health all keep working, and the doc note becomes true.
  - If you prefer to keep the drop: correct the doc note and the VS Code text in the matrix and doctor to say that a nested client in a launch's shell has no VS Code navigation. Also make the Windows Terminal helper report `window only` when the window is verified but the shell is missing.
- Behavior-preservation risks:
  - The marker carries through the `previous.Host with {...}` merge in `Adopt`, which keeps `ShellSource`. It must not be cleared by a later capture while the launch lives.
  - The shell-conflict check in `AttachmentStore` would then see the launch's shell on the nested record. That is the value that was stored before DEV-052, so nothing changes there.
  - Older readers keep an unknown `ShellSource` value only as data, since only `Reconcile` reads it.
- Evidence type: source observation (not reproduced live)
- Verification / measurement plan:
  - In `NativeAdoptionReconcileTests` (both providers, default setting), keep the current assertions: `GrantsPresentation == false` and a null resolver result.
  - Add host-level checks: `VsCodeNavigator.Precision` with a fake registration that contains the shell gives `exact terminal` or `terminal only`, and `WindowsTerminalFocusService.Focus` gives a `window only` result for the nested record.
  - A mutant that ignores the marker in `IsAdoptedDirect` must fail the presentation assertions.
- Relationship: a regression of the ANALYSIS-3800 fix. The proposed direction is the second option in ANALYSIS-3800 ("keep it for navigation but mark the evidence").

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3800 | `head=d465737 ... diff=e14c44638628 untracked=2/931c2df99821` | One provider-neutral condition in `Reconcile`. Without a shell, `IsAdoptedDirect` refuses, so a nested client in the live launch's shell gets no title, no background and no restore record, whether the launch presents or opted out, for both Claude Code and Codex. The launch record is untouched. Standalone clients and clients in another shell are unaffected by construction. Races fail closed | Verified for presentation. The side effect on navigation and the doc note is raised as ANALYSIS-3900 |
| ANALYSIS-3801 | same | The new theory saves no setting, so the default (on) applies, and it covers claude and codex with the launch presenting and opted out. It asserts the own ID, no shell, `GrantsPresentation(own, true) == false`, a null resolver result and an untouched launch record. The other-shell half of the second theory asserts a non-null resolver result under the default. Limitations: (a) in the mutant run, the first assertion to fail is the structural `Assert.Null(own.Host.Shell)`, so it is not shown that the behavioral assertions would fail on their own; (b) `Assert.Empty(TitleRestoreStore.List())` passes trivially, because no title service runs in the test; (c) the "standalone session after the launch ended" half uses an `observed` capture, which never enters the nested branch. Setting `EndedAt` also leaves the launcher process running, and the rule checks process liveness, so that half passes whether or not the rule exists and does not test the launch-ended guard | Verified: the invariant is now asserted under the default for both providers. Limitations (a) to (c) are recorded for triage; no new finding raised (recheck scope) |
| ANALYSIS-3803 | same | `installation.md:79,80,106` and `compatibility.md:80,94` now say the setting is on by default, a saved false is kept, and a click selects the tab while the setting is on. The grep for the old wording has no hits in `docs` or `src`. The tray, doctor and the matrix agree | Verified. The new nested note is inaccurate (ANALYSIS-3900) |
| ANALYSIS-3804 | same | `dotnet format ... --verify-no-changes --include <8 changed .cs files>` exited with code 0. `git ls-files --eol` shows `w/crlf` for `AppConfig.cs` | Verified |

## Assessment and limitations

This recheck covered the assigned findings and looked for regressions in presentation, navigation, health, evidence, restore, the pump, attention, runtime access, races, docs, doctor, the matrix and formatting.

The ANALYSIS-3800 rule is one provider-neutral rule, and it does close the presentation path under the default for both providers without a second policy. But it works by removing `Host.Shell`, which VS Code navigation and the Windows Terminal focus helper also depend on. As a result, a nested client in a launch's shell loses VS Code navigation, and its Windows Terminal click reports "no longer registered", which contradicts the new doc note (ANALYSIS-3900). Marking the shell instead of dropping it keeps the same single rule without that side effect. The orchestrator decides.

I did not run builds, tests or the mutant rerun (QA owns them), and I did no live terminal checks. ANALYSIS-3900 is a source observation. The client refused the report write, so the report is returned inline. There were no other writes, and the target identity is unchanged. The orchestrator owns triage and ledger status.
