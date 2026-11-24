<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-038 / code-analyzer; Client: Claude Code 2.1.325, Agent SDK subagent, non-interactive (as reported by the reviewer); Model: claude-opus-5-5; Refusal: the client refused the report write, as reported by the reviewer. -->

The report write was refused, so the full report is inline below; please save it to `openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-038.md`.

The way the default is stored is reliable: a config without the value reads on, and a saved false stays off. The main issue is ANALYSIS-3800: with the setting now on by default, a nested interactive client can get the title and background on the managed launch's console, even when that launch opted out. That needs your decision.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.325 (Agent SDK subagent) / claude-opus-5-5
task / finding IDs: DEV-051 / ANALYSIS-3800 (warning, decision needed), ANALYSIS-3801 (suggestion), ANALYSIS-3802 (suggestion), ANALYSIS-3803 (warning), ANALYSIS-3804 (warning)
changed or report paths: none written. The write of openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-038.md was refused; full report inline below
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <7 changed .cs files> -> failed (17 line-ending errors, all in src/BellTower/Core/AppConfig.cs, which has LF endings in the working tree); build/test -> not run (QA owns them)
target identity: before head=d465737 baseline=d465737 files=11 diff=9d68ca8c47a8 untracked=1/8a0529033cdf; after: identical
limitations / decisions needed: report write refused, text inline. ANALYSIS-3800 needs a policy decision (fail closed for a nested client in the managed launch's own shell, or accept and document it). ANALYSIS-3800 is from reading the code and was not reproduced live. No builds, tests or live terminal checks.
next: triage; developer fixes ANALYSIS-3803 (docs) and ANALYSIS-3804 (run the formatter on AppConfig.cs, record the result); decide ANALYSIS-3800, then ANALYSIS-3801 adds tests that run with the setting at its default (on).
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-038 (DEV-051, native presentation on by default)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.325, Agent SDK subagent, non-interactive; claude-opus-5-5
- Date: 2026-11-14T10:21Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-038
- Task and requirement references: DEV-051 ([implementation 051](../evidence/implementation-051.md)); session-identity requirement "Presentation ownership for adopted terminals", scenario "Native presentation default" ([spec](../specs/session-identity/spec.md))
- Scope and relevant neighboring paths: the 10 changed tracked files (`src/BellTower/Core/AppConfig.cs`, `src/BellTower/Tray/TrayApplication.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `docs/installation.md`, `docs/compatibility.md`, `docs/attachment-health.md`, `tests/BellTower.Tests/Hosts/NativePresentationTests.cs`, `tests/BellTower.Tests/Hosts/TerminalTabSelectionTests.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs`) plus the evidence file. Neighbors read: `src/BellTower/IO/ConfigStore.cs`, `src/BellTower/IO/Json.cs`, `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/Core/NativePresentation.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/Hosts/TerminalPresentationResolver.cs`, `src/BellTower/Hosts/TerminalTitleService.cs`, `src/BellTower/Hosts/TerminalTitlePump.cs`, `src/BellTower/Hosts/WindowsTerminalFocusService.cs`, `src/BellTower/Hosts/WindowsTerminalNavigator.cs`, `tests/BellTower.Tests/Hosts/MixedLaunchAndNativeTests.cs`, `tests/BellTower.Tests/Providers/NativeAdoptionTests.cs`
- Baseline commit: `d465737`
- Implementation identity (before): HEAD `d46573748ed65bb3d4c22a371fc43d4a2d7772f4`, baseline the same; manifest: `?` `openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-051.md`, `M` for the 10 tracked files listed above; tracked diff SHA-256 `9d68ca8c47a809be875b6dfa17697e95d2b1c76487b2c140a2058d421fb821c0`; untracked `646949248b2ddfeff47a28b939be6b2b32d951099bbcb74890b5c15ab6cb9848` (implementation-051.md); summary `head=d465737 baseline=d465737 files=11 diff=9d68ca8c47a8 untracked=1/8a0529033cdf`
- Before/after identity: after the analysis, the same command gave `head=d465737 baseline=d465737 files=11 diff=9d68ca8c47a8 untracked=1/8a0529033cdf`. It matches the assigned identity, and nothing in the target changed unexpectedly.
- Prior reports / rechecked findings: none rechecked. This is a new review of DEV-051.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Absent compared with explicit false | Read `Json.Options`, `AtomicFile.Read`, `ConfigStore.Read/Update` | `JsonSerializerDefaults.Web` without `DefaultIgnoreCondition` writes every property, including `false`. A missing file returns `default`, so `lastGood = new()` gives the initializer `true`. A missing property keeps the initializer. `Update` round-trips `Read()` through the encoder and keeps an explicit `false` | Reliable. The new tests pin this behavior (`SettingDefaultsOnAndRoundTrips...`, `AConfigurationWithoutTheValueReadsAsOn...`). Side effect: an invalid or corrupt `config.json` now falls back to on, the same way `Voice` already falls back to its default. That is consistent with "absent" and is not a finding |
| Migration and older writers | `git log -S NativePresentation`, tag contents | The property appeared in `dc7eaac` (2026-11-14). `v0.1.8` and `v0.1.9` do not contain it, so no released config holds an implicit value. An older release keeps an unknown `nativePresentation` through `JsonExtensionData` | Releases are not affected. Dev builds from `dc7eaac` are covered by ANALYSIS-3802 |
| Other `AppConfig` constructions | `grep AppConfig src` | Only `ConfigStore.lastGood = new()` and `TrayApplication.config = new()` (true until the first read). Every product reader (`TerminalPresentationResolver`, `TerminalTitleService`, `WindowsTerminalFocusService`, `WindowsTerminalNavigator`, `DiagnosticsReporter`, tray) goes through `ConfigStore`. `Integration/BellTower.psm1`, Launcher, Ingress, Distribution and `vscode/` do not read the setting | No reader still assumes false. Both providers read the same single property |
| Write safety under the default | Read `NativePresentation.IsAdoptedDirect/GrantsPresentation`, `NativeAdoption.Reconcile` (nested branch), `AttachmentCapture.Capture` shell walk, the resolver, the title service, the pump | The grant still requires observed ownership, a live verified agent and shell, stamped current evidence, `agent-process` lifecycle, a mode other than shared or headless, and no managed title. The title service rechecks under the presentation lock. A nested interactive client whose ancestry shell is the live managed launch's shell qualifies | See ANALYSIS-3800. Before DEV-051 this path needed an opt-in; now it is reachable by default |
| Test meaningfulness | Read the four adjusted tests and the new ones | Off test, pump test and tab-selection test: an explicit off is set before the first assertion, then switched on. They still check both states for both providers. Nested-client test: it now checks only the off state, and its own comment states a default-on property that nothing asserts | ANALYSIS-3801 |
| Text consistency (doctor, matrix, docs, tray) | Diff review plus a grep for the old phrasing | Tray tooltip, `DiagnosticsReporter`, `CapabilityMatrix`, the `docs/compatibility.md` table row and `docs/attachment-health.md` agree for Claude Code and Codex. `docs/installation.md:106` still says "default false". `docs/compatibility.md:94` still says "The setting is off by default". `docs/installation.md:79` describes window-only Windows Terminal navigation as the general behavior | ANALYSIS-3803 |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <7 changed .cs files>` | 17 `ENDOFLINE` errors, all in `src/BellTower/Core/AppConfig.cs`. `git ls-files --eol` shows `w/lf` for that file, while the other changed files are `w/crlf` and `.editorconfig` sets `end_of_line = crlf`. The evidence file says "format.ps1 -Verify checked below" but gives no result | ANALYSIS-3804 |
| Duplication / smallest change | Diff review | The default comes from one initializer, `ConfigStore` is unchanged, and the texts use the existing short-form pattern | This is the smallest change. Nothing to simplify |
| Parity | All of the above | One shared property, one grant function and matched Claude Code and Codex test rows. Plain default Codex on a shared backend is still excluded and documented as unfinished | No parity gap was introduced |
| Builds / tests | not run | QA owns them in parallel (assignment) | not performed |

## Findings

### ANALYSIS-3800: Default-on gives a nested interactive client native presentation in the managed launch's shell

- Severity: warning
- Related task / requirement: DEV-051; session-identity "Presentation ownership for adopted terminals" (scenarios "Native presentation default" and "Existing managed launch has explicit presentation controls")
- Locations at `d465737` plus the working tree: `src/BellTower/Hosts/AttachmentCapture.cs` (shell walk and the `BELLTOWER_SHELL_PID` comparison, around lines 82-92); `src/BellTower/Core/NativeAdoption.cs` (`Reconcile` nested branch, around lines 203-221); `src/BellTower/Core/NativePresentation.cs` (`IsAdoptedDirect`, `GrantsPresentation`); `src/BellTower/Core/AppConfig.cs:16`
- Observation:
  1. A second live agent that carries an inherited managed attachment identifier becomes its own observed `agent-process` attachment.
  2. It keeps the host when `ShellSource == "ancestry"`.
  3. The capture's shell walk does not stop at an ancestor of the same provider. For a client started from a non-PowerShell tool shell (for example Git Bash) inside a managed launch, the nearest PowerShell or cmd ancestor is the launch's own shell. The inherited `BELLTOWER_SHELL_PID` names that same shell, so the source stays `ancestry`.
  4. That nested attachment then passes `IsAdoptedDirect`. With the new default it is granted a title and a palette background on the console that the managed launch presents.
  5. This bypasses an opt-out the user set on the managed launch (`ManagedTitle`/`ManagedBackground` false on that launch). It also means that when the nested client exits, the restore resets the colors to the terminal default on that shell. The pump then repaints the managed launch only if it is still presented.
  6. The developer confirmed this behavior in the test comment ("With the native setting on (the default) a client in its own verified shell is presented as a native session") and switched the setting off in that test rather than deciding the policy.
  7. Before DEV-051 this needed an explicit opt-in. Now it is the default. `MixedLaunchAndNativeTests` accepts a managed and a native session in one shell, but nothing covers a nested client that writes over a launch's opt-out.
- Proposed improvement and benefit: needs an orchestrator or user decision. One option is the smallest fail-closed rule: in the `Reconcile` nested branch, drop the shell when it equals the live recorded launch's `Host.Shell`, or keep it for navigation but mark the evidence so that `GrantsPresentation` refuses. The other option is to accept the behavior explicitly and document it. Either way the default no longer writes into a console whose presentation another live managed attachment owns or declined, unless that is the stated policy.
- Behavior-preservation risks: navigation for such a nested client could fall back to a less precise result. The rule must stay provider-neutral (Claude Code and Codex nested rows). A legitimate standalone session that later runs in the same shell after the launch has ended must keep its grant (`live(recorded)` already limits the branch to a live launcher).
- Evidence type: source observation. This is not reproduced live. The test fixture `ANestedClientThatInheritedAManagedIdentifierKeepsItsOwnIdentityAndNoLaunchPresentation` (ancestry rows) uses the same shell for launcher and nested client, and its comment says the resolver returns a presentation there with the setting on.
- Verification / measurement plan: in `NativeAdoptionReconcileTests`, add a default-on assertion for the ancestry rows. It should cover both a launch with presentation and a launch that opted out, for both providers, asserting whatever behavior is decided.
- Relationship: ANALYSIS-500 (inherited launch metadata) is the earlier guard on this path. ANALYSIS-3801 covers the test side.

### ANALYSIS-3801: The nested-client test no longer asserts its invariant under the default

- Severity: suggestion
- Related task / requirement: DEV-051; ANALYSIS-500 lineage
- Locations: `tests/BellTower.Tests/Providers/NativeAdoptionReconcileTests.cs:50-53`
- Observation: the test name and comment promise "no launch presentation": the writer is never offered the launch's title or background. The adjustment sets the setting off and asserts `Resolve == null`, which follows trivially from the setting being off. Under the production default, the claim "presented as a native session, never as the managed launch" is not asserted for any row. The other three adjusted tests stay meaningful because each one switches the setting back on and checks the on state.
- Proposed improvement and benefit: keep the default (do not save a value) and assert per row:
  - ancestry rows that are not headless: the resolved presentation's `AttachmentId == own.Id`, and its title and background come from the nested session's identity, not "Parent task" and not the launch palette (or `null`, if ANALYSIS-3800 decides fail-closed);
  - `launch-env` and headless rows: `null`.

  Then keep the explicit-off assertion. This restores the original guard on the path users now get by default.
- Behavior-preservation risks: none. This changes tests only.
- Evidence type: source observation
- Verification / measurement plan: a mutant that copies `previous.ManagedTitle`/`Id` into the nested attachment should fail the default-on assertion.
- Relationship: depends on the decision in ANALYSIS-3800.

### ANALYSIS-3802: Dev builds since `dc7eaac` may have persisted an implicit false that now reads as an explicit off

- Severity: suggestion
- Related task / requirement: DEV-051; scenario "Native presentation default"
- Locations: `src/BellTower/IO/ConfigStore.cs:31-38`, `src/BellTower/IO/Json.cs:8-17`
- Observation: the writer serializes every property. Any `ConfigStore.Update` in a build containing `dc7eaac`, such as a mute, voice or reminder toggle or an update setting, wrote `"nativePresentation": false` while the default was false, even if the user never touched the native item. This build cannot tell that value apart from a deliberate off, so such a data directory stays off. Released versions (`v0.1.8`, `v0.1.9`) do not contain the property, so users of released builds are not affected.
- Proposed improvement and benefit: no code change is recommended. A one-time migration would need a schema marker, which is more than this change needs. Record in the evidence or in the trial 6.1 instructions that a dev install that saved any setting since `dc7eaac` may show the item unchecked, and that the tray checkbox is the source of truth. This avoids a false "default did not apply" result in the live trial.
- Behavior-preservation risks: none
- Evidence type: source observation plus `git log -S NativePresentation` and the tag contents
- Verification / measurement plan: before trial 6.1, check the tray checkbox state or `doctor` `nativeSessions.setting` on the trial data directory.
- Relationship: none

### ANALYSIS-3803: Three doc passages still describe the setting as off by default

- Severity: warning (contradicts other user-facing rules)
- Related task / requirement: DEV-051; spec "Native presentation default"
- Locations: `docs/installation.md:106`, `docs/compatibility.md:94`, `docs/installation.md:79`
- Observation:
  - `docs/installation.md:106` says `nativePresentation (default false; ...)`. That contradicts `docs/installation.md:80`, the tray tooltip, `doctor`, the capability matrix and `AppConfig`.
  - `docs/compatibility.md:94` says "The setting is off by default". That contradicts the table row on line 80 of the same file.
  - `docs/installation.md:79` presents "a click brings the owning window forward (window only) and shows a short balloon" as the general Windows Terminal behavior, which now applies only after the user switches the setting off. The next bullet says a click selects the tab while the setting is on, which is now the default.
- Proposed improvement and benefit: change `installation.md:106` to "default true; a saved false is kept". Change `compatibility.md:94` to "The setting is on by default, can be switched off in the tray, and applies to running sessions without a restart". Qualify `installation.md:79` as "with the setting off: window only, with a balloon; with it on (the default): the session's tab". The docs then agree with `doctor` and the matrix for both providers.
- Behavior-preservation risks: none. The changes are to docs only.
- Evidence type: source observation (`grep`)
- Verification / measurement plan: run `grep -n -i "off by default\|default false" docs` and confirm no native-presentation hits remain.
- Relationship: none

### ANALYSIS-3804: `AppConfig.cs` fails the format verification (LF line endings in the working tree)

- Severity: warning
- Related task / requirement: DEV-051 evidence ("format.ps1 -Verify checked below", with no result recorded)
- Locations: `src/BellTower/Core/AppConfig.cs` (all lines)
- Observation: `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <7 changed .cs files>` exited with code 2 and 17 `ENDOFLINE` errors, all in this file. `git ls-files --eol` shows `i/lf w/lf`, while the other changed files are `w/crlf`, and `.editorconfig` requires `crlf` (`core.autocrlf=true`). The edit rewrote the file with LF. The three docs files show the same LF warning from Git. That does not affect the C# format check.
- Proposed improvement and benefit: the developer runs `dotnet format ZeusApps.BellTower.slnx --no-restore --include src/BellTower/Core/AppConfig.cs` (or rewrites the file with CRLF) and records the verify result in the evidence. The index content and the review diff hash stay the same because Git normalizes to LF.
- Behavior-preservation risks: none
- Evidence type: measured result (command output)
- Verification / measurement plan: rerun the verify command above and expect exit code 0.
- Relationship: none

## Re-verification

None. This is not a recheck assignment.

## Assessment and limitations

The way the default is implemented is sound and minimal. A property initializer combined with a writer that never omits defaults reliably separates "absent" (on) from a saved `false` (off), and unrelated saves keep both states. All product readers go through `ConfigStore`, and Claude Code and Codex share one property and one grant function. The doctor, matrix, tray and two of the three docs agree. ANALYSIS-3803 lists the stale passages. No duplication or simplification opportunity was found.

The main risk is ANALYSIS-3800. Turning the default on makes the nested-client native grant reachable without any user action, and that grant can write over a managed launch's console or its opt-out. It needs a policy decision, and ANALYSIS-3801 should then pin that decision under the default.

Formatting fails on `AppConfig.cs` (ANALYSIS-3804). The evidence file records no format result.

I did not run builds or tests (QA owns them) or any live terminal checks. ANALYSIS-3800 is a source observation and was not reproduced in a real Windows Terminal or VS Code shell. The client refused the report write, so the report is returned inline for the orchestrator to save. There were no other writes, and the target identity is unchanged. The orchestrator owns triage and ledger status.
