> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-046); client: Claude Code 2.1.330; mode: Agent SDK subagent, non-interactive; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-046 (DEV-058 phase 2)

## Review target

- Reviewer / role: independent code analyzer / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330 (Agent SDK subagent, non-interactive) / `claude-opus-5-5`
- Date: 2026-11-22T19:24Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-046
- Task and requirement references: trial items T-4, T-6, T-7 ([findings](findings.md), 2026-11-22 entries); task 4.1 ([tasks](../tasks.md)); terminal-navigation requirement "a navigation request for an adopted session SHALL always produce a visible result" ([spec](../specs/terminal-navigation/spec.md)); developer evidence [implementation-058](../evidence/implementation-058.md)
- Scope: `src/BellTower/Hosts/NavigationReasons.cs` (new), `src/BellTower/Hosts/Navigator.cs`, `src/BellTower/Hosts/WindowsTerminalFocusService.cs`, `src/BellTower/Hosts/WindowActivator.cs` (`Raise`), `src/BellTower/Tray/NavigationNotice.cs` (new), `src/BellTower/Tray/NoticeForm.cs` (new), `src/BellTower/Tray/OverviewForm.cs`, `src/BellTower/Tray/TrayApplication.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`. Neighbors read: `Tray/NavigationOutcome.cs`, `Tray/StripForm.cs`, `Hosts/WindowsTerminalNavigator.cs`, `Hosts/VsCodeNavigator.cs`, `Hosts/FocusResult.cs`, `Core/ConversationLifecycle.cs`, `Core/NativePresentation.cs`, `IO/LocalLog.cs`, `vscode/service.js` (precision values), `docs/compatibility.md`, `docs/installation.md`, `tests/BellTower.Tests/Hosts/ClickReasonTests.cs`.
- Baseline commit: `7a9d1be`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 7a9d1be -Paths src tests docs vscode` -> HEAD `b3f5325471dc999986362ecc0be3bfce1b0d0e6d`; manifest: `? src/BellTower/Hosts/NavigationReasons.cs`, `? src/BellTower/Tray/NavigationNotice.cs`, `? src/BellTower/Tray/NoticeForm.cs`, `? tests/BellTower.Tests/Hosts/ClickReasonTests.cs`, `M docs/compatibility.md`, `M docs/installation.md`, `M src/BellTower/Diagnostics/CapabilityMatrix.cs`, `M src/BellTower/Hosts/Navigator.cs`, `M src/BellTower/Hosts/WindowActivator.cs`, `M src/BellTower/Hosts/WindowsTerminalFocusService.cs`, `M src/BellTower/Tray/OverviewForm.cs`, `M src/BellTower/Tray/TrayApplication.cs`; tracked diff SHA-256 `412c6ee17c97b16136df4815b123c662f60a716cd5b8076269925a055a003ba6`; untracked `c19afd5a06f1d73b0fa2d6200f66e7c3a9cac217515ca4f261a72502171c9f23` NavigationReasons.cs, `7841cdd4702380433aa3663993199a1fdf7e02be3f43d3f2d4588371579ddd32` NavigationNotice.cs, `3da3f4f1c70f361798247c15b0263afda1f5fdda5cb3c1688ccd80e7b5242a8e` NoticeForm.cs, `6b9034d24113529d3c3e9d64cf20597275ea8451ee95f2a8de75c034faa597fc` ClickReasonTests.cs; summary `head=b3f5325 baseline=7a9d1be files=12 diff=412c6ee17c97 untracked=4/b7bcfb5846e5`
- Before/after identity: after the analysis the same command printed `head=b3f5325 baseline=7a9d1be files=12 diff=412c6ee17c97 untracked=4/b7bcfb5846e5`; identical, no unexpected mutation.
- Prior reports / rechecked findings: none rechecked (new analysis of DEV-058 phase 2).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness: reason per click outcome | Source trace of `Navigator.FocusAsync`, `NavigationReasons.ForSharedConversation`, `WindowsTerminalFocusService.Focus`, `WindowsTerminalNavigator`, `VsCodeNavigator`, `NavigationOutcome.NeedsNotice` | Mapping table below | Every host result that reaches `TrayApplication.Navigate` past line 405 carries a non-empty constant message, and `NeedsNotice` is true for every non-exact or failed result. Remaining silent paths: ANALYSIS-4600, ANALYSIS-4602 |
| Delivery and visibility | Source plus Microsoft Learn `QUERY_USER_NOTIFICATION_STATE` (read 2026-11-22) | The documented states cover screen saver/lock, full-screen, D3D exclusive, presentation settings, first-hour quiet time and Store apps; Focus/Do Not Disturb and notification settings are not mentioned | ANALYSIS-4600 |
| UI thread, lifetime, disposal, timers | Source | `Navigate` resumes on the WinForms context (no `ConfigureAwait(false)` in the tray) and checks `disposed` after the await; `notice` is a single field instance created on the UI thread and disposed in `ExitThreadCore`; its timer is disposed in `Dispose(bool)`; a repeated `ShowNotice` replaces the text, re-places the window and restarts the timer | No pile-up: one notice at most, and the last reason wins. No leak found. After a click-hide the timer keeps running, and its later `Hide` on a hidden form is harmless |
| Foreground stealing: `OverviewForm.Raise` pulse | Source; WinForms `Form.TopMost` setter (dotnet/winforms main) | The pulse is `TopMost = true; TopMost = false;` with nothing in between, and nothing else writes `OverviewForm.TopMost`, so the window always ends non-topmost (HWND_NOTOPMOST puts it at the top of the non-topmost band). Callers: the menu item, the tray double click, the tone-selection error, and update check or install completion; all are user-initiated | Always undone. The update-completion raise can come seconds after the user's request and lift the window over whatever is then in front; intentional and acceptable. The second `SetForegroundWindow` may flash the taskbar button; harmless |
| Foreground stealing: `NoticeForm` | Source; WinForms `Form.CreateHandle`, `TopMost` setter, `SetVisibleCore` | ANALYSIS-4601 | Hypothesis, needs a probe |
| Logging | `LocalLog.Write` code filter; all `FocusResult` construction sites; bridge precision values in `vscode/service.js` | Codes are `navigation-{ok,refused}-{precision}`. The precision values are constants (`exact tab`, `exact terminal`, `window only`, `unavailable`), except that a deserialized VS Code bridge receipt or `focus-terminal` helper output can pass its own value through. `LocalLog` keeps only ASCII letters, digits, `-`, `_`, `.` and space, caps codes at 120 characters, and the 250 KB cap applies | Path-free (separators are stripped) and bounded. One line per click. A pre-existing null-field issue is described in the assessment |
| Duplication | Source | `NavigationReasons` centralizes four reasons; about a dozen other host messages stay inline. `NavigationNotice.Decide` builds on `NavigationOutcome.NeedsNotice` and does not duplicate it. `NoticeForm` repeats `StripForm`'s non-activating topmost style (`ShowWithoutActivation`, ExStyle `0x08000080`) | No finding. A shared base class for two windows would be speculative |
| Complexity | Source | `ForSharedConversation` is a three-way expression and computes `AttachmentEvidence.Of` twice (trivial). `presentationOff` has a redundant `stored is not null` that the guard above already ensures | No finding (style-level) |
| Architecture | Against `docs/development.md` layers | Reason texts and the shared-conversation rule live in `Hosts`, delivery in `Tray`, window activation in `Hosts.WindowActivator`; the predicates come from `Core` and do not depend on the provider | Consistent |
| Claude Code and Codex parity | Source and tests | `IsAdoptedDirect` and `IsSharedConversation` do not depend on the provider, and no per-provider click branch was added. The shared-backend reason applies only to Codex because Claude Code has no shared backend (matrix `Never`). Tests cover both providers for a gone process and for the setting being off, both hosts for the shared backend, and mixed sessions | Behavior matches for both providers. The docs and diagnostics text does not: ANALYSIS-4603 |
| Builds / tests / format | Not run | Assignment: QA runs them | Not performed |

### Click outcome mapping (both providers, both hosts)

| Outcome | Claude Code | Codex | Windows Terminal | VS Code | Result and delivery |
| --- | --- | --- | --- | --- | --- |
| Shared-backend conversation | not applicable (no shared backend) | `SharedBackend` | same (decided before host dispatch) | same | failed/`unavailable`, notice |
| Shared conversation, unrecognized mode | only if the evidence marks it shared | `TerminalUnverified` | same | same | failed/`unavailable`, notice |
| Direct, process gone or ended | `ProcessUnverified` | `ProcessUnverified` | same | same | failed/`unavailable`, notice |
| Direct, setting off, window focused | `WindowOnlyPresentationOff` | `WindowOnlyPresentationOff` | yes | not applicable (navigation does not depend on the setting) | ok/`window only`, notice |
| Managed launch that opted out, or setting on and tab not found | generic window-only text | same | yes | not applicable | ok/`window only`, notice |
| `focus-terminal` helper timeout, bad JSON or no process | generic "select the session's tab or pane" (this fallback drops the setting-off reason; acceptable edge case) | same | yes | not applicable | ok/`window only`, notice |
| Foreground refused | "Windows declined…" / "did not bring…" | same | yes | yes | failed, notice |
| Exact tab or terminal | none | none | yes | yes | no message (intended) |
| Session key no longer in state | silent, unlogged | silent, unlogged | both | both | ANALYSIS-4602 |

## Findings

### ANALYSIS-4600: Balloon-or-notice choice still allows a click with no visible result

- Severity: warning
- Related task / requirement: T-4, T-6; task 4.1; terminal-navigation "always produce a visible result"
- Locations: `src/BellTower/Tray/NavigationNotice.cs:36-46` (`Decide`), `:49-60` (`Query`); `src/BellTower/Tray/TrayApplication.cs:433-441`
- Observation: the in-app notice is used only when `SHQueryUserNotificationState` reports not present, busy, D3D full screen, presentation mode or quiet time. Microsoft Learn defines quiet time as the first hour after a user's first sign-in or an OS upgrade, not Focus or Do Not Disturb. The API documentation does not cover Focus/Do Not Disturb, the per-app notification switch or the global notification switch. On Windows 10 and 11 a NotifyIcon balloon is shown as a toast, and those settings can suppress it while the API still reports "accepts notifications". In those states `Decide` picks `Balloon` and nothing appears, which is the T-4/T-6 symptom. The developer says the suppression cause of the silent Windows Terminal clicks in the trial is unconfirmed, so the fix rests on one hypothesis about how Windows decides visibility. A smaller point: when the strip is hidden (the click came from the Sessions window), the notice is anchored at the top-left of the primary work area, not near the window the user clicked in.
- Proposed improvement and benefit: show the in-app notice for every result where `NeedsNotice` is true, with the balloon only as an optional extra. This removes `Query`, the P/Invoke, `UserNotificationState` and the state branching, and makes "a stated reason is visible" independent of Windows notification policy. If balloons must stay primary, at least document that Focus/Do Not Disturb and disabled notifications are not detected, and add this to the open-evidence notes. Anchor the notice to the Sessions window bounds when the click came from that window.
- Behavior-preservation risks: the notice appears more often (8 s, non-activating, next to the strip); the `ClickReasonTests` delivery theory changes; the docs and matrix wording ("in a balloon, or in an in-app notice when Windows suppresses balloons") changes with it. Fix ANALYSIS-4601 first or in the same change, because a notice that is always shown must not take focus.
- Evidence type: source observation plus primary documentation; whether Focus/Do Not Disturb changes the API's answer on build 26300 was not measured
- Verification / measurement plan: a unit test that `Decide` returns the notice for every non-exact result. One manual probe with Do Not Disturb on and one with Bell Tower notifications off in Settings, clicking a window-only session and a shared-backend session in Windows Terminal and VS Code, with one Claude and one Codex session.
- Relationship: none

### ANALYSIS-4601: The notice may take keyboard focus from the terminal the click just focused

- Severity: warning
- Related task / requirement: T-4, T-6 delivery; foreground-stealing review
- Locations: `src/BellTower/Tray/NoticeForm.cs:20-24` (`TopMost = true` in the constructor), `:41-52`, `:55-68`; called from `src/BellTower/Tray/TrayApplication.cs:439`
- Observation: in dotnet/winforms (`Form.cs`, main), `CreateHandle` applies a stored `TopMost` through the property setter, which calls `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE | SWP_NOSIZE)` without `SWP_NOACTIVATE`. `SetVisibleCore` also calls `FocusActiveControlInternal()` for a `TopMost` form without checking `ShowWithoutActivation`; this is the known "TopMost ignores ShowWithoutActivation" behavior. The notice appears right after the navigator gave the foreground to Windows Terminal or VS Code, while the tray thread still holds the foreground right from the user's chip click. If either call activates the notice, keyboard focus leaves the terminal the user just navigated to, in exactly the full-screen case the notice is meant for. `WS_EX_NOACTIVATE` blocks activation by the mouse and by the system, but not necessarily these programmatic calls. `StripForm` uses the same pattern with no reported focus loss, but it appears at alert time, not right after a click hands the foreground to another window.
- Proposed improvement and benefit: do not set the `TopMost` property. Add `WS_EX_TOPMOST` (`0x00000008`) to the `CreateParams` ExStyle next to the existing `0x08000080`, and keep `ShowWithoutActivation`. Alternatively, show the window with `ShowWindow(SW_SHOWNOACTIVATE)` and `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE)`. The notice is then topmost from creation and never goes through activation.
- Behavior-preservation risks: the notice must still appear above a full-screen Windows Terminal; the WinForms `TopMost` property then reads false (nothing reads it).
- Evidence type: source observation of the framework plus a hypothesis; not measured
- Verification / measurement plan: a manual or harness probe that forces the in-app path (or, after ANALYSIS-4600, any window-only click). After the notice shows, `GetForegroundWindow` is still the terminal window and typed keys reach the terminal. Check Windows Terminal and VS Code, with one Claude and one Codex session.
- Relationship: none

### ANALYSIS-4602: Two click paths still end silent and unlogged

- Severity: suggestion
- Related task / requirement: T-4 diagnosability; task 4.1
- Locations: `src/BellTower/Tray/TrayApplication.cs:403-409` (early return when the key is not in `state.Sessions`), `:413` (`await navigation.FocusAsync` in an `async void` handler with no catch), `:431-432` (the new log line)
- Observation: the comment says every click is accounted for in the local log, but two paths are not:
  - The early return for a key that is no longer in the tray state writes nothing and shows nothing.
  - An exception from a navigator (for example `Process.Start` in `WindowsTerminalNavigator`, or `AtomicFile.Write` in `VsCodeNavigator`) skips the log line and surfaces only through the WinForms unhandled-exception path.

  The trial's note that "the log records no click" for the Windows Terminal T-4 case therefore still cannot be told apart from a click that never arrived.
- Proposed improvement and benefit: on the early return, log `navigation-session-missing` and state a short reason through the same delivery. Catch the navigators' known exception types (`IOException`, `UnauthorizedAccessException`, `Win32Exception`, `InvalidOperationException`) around `FocusAsync`, log `navigation-failed` and state a generic reason. The change's claim that every click is logged then holds, and the spec's visible-result guarantee covers these paths.
- Behavior-preservation risks: no contract change; keep the codes constant and path-free
- Evidence type: source observation
- Verification / measurement plan: a unit or harness test with a stale key and a throwing navigator, asserting one log line and one notice decision, for one Claude and one Codex session
- Relationship: none

### ANALYSIS-4603: The Codex navigation column understates the shared behavior

- Severity: suggestion
- Related task / requirement: parity and capability-matrix honesty (AGENTS.md); T-6
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs:103` (Codex column of "Terminal navigation: adopted direct sessions"), `docs/compatibility.md:79` (same row, Codex column)
- Observation: the Claude column now says that a partial click states its reason (with the setting off, that the setting is off), in a balloon or an in-app notice. The Codex column still says "the same click balloon for a partial result". The code is shared and tests cover `codex --no-daemon` for the setting-off reason, so `doctor` and the compatibility doc understate Codex and suggest a parity gap that does not exist.
- Proposed improvement and benefit: use the same wording in the Codex column, or "the same stated reason and delivery as Claude Code". The matrix then stays honest for both providers.
- Behavior-preservation risks: none (text only); keep `CapabilityMatrix` and `docs/compatibility.md` identical
- Evidence type: source observation
- Verification / measurement plan: review the text; run any existing matrix and doc consistency test
- Relationship: none

## Re-verification

None assigned.

## Assessment and limitations

Scope covered: the DEV-058 phase 2 code for T-4, T-6 and T-7 listed above, for both providers and both hosts, by source inspection only.

- The reason mapping is correct and does not depend on the provider. The shared-backend reason now comes before the process check on both hosts, and the setting-off reason is limited to adopted direct sessions, so managed launches that opted out do not get it.
- The Sessions window pulse is always undone and runs only on user-initiated paths.
- Notices cannot pile up.
- Log codes are path-free and bounded.

The two warnings are about delivery. ANALYSIS-4600 is a remaining path with no visible result, caused by relying on an API that does not see Focus or notification settings. ANALYSIS-4601 is a likely focus steal by the notice and needs a probe. Neither blocks the reason texts, but both decide whether T-4 and T-6 actually close in the next trial.

Pre-existing and out of scope (no finding): a `FocusResult` can be deserialized from a VS Code bridge receipt (`VsCodeNavigator` returns a failed `reveal` unchanged) or from the `focus-terminal` helper output. Such a result can have a null `Precision` or `Message`, because System.Text.Json does not enforce non-nullable record parameters. `Navigate` then throws (the new `Precision.Replace`, and `NeedsNotice` before it), and `ShowBalloonTip` rejects empty text. Normalizing the deserialized result once in each of the two navigators would close this.

Not performed: builds, tests and format verification (QA owns them). No live tray, real data directory, Windows Terminal or VS Code click. No measurement of `SHQueryUserNotificationState` under Focus/Do Not Disturb, and no probe of the notice's activation behavior. The attempt to write the reserved report was refused; no files were written.

### Independent opinion on phase 1 recommendations (for the user's decision; not findings)

**T-12.** Option B is to create the tray with `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` set to the user's shell, plus a child-side kill-on-close self-check, with D as the fallback.

- B is technically sound. It makes "no worse than the shell" true by construction, and the probes show that is the only property you can rely on on this build. Whichever option is chosen, decision 9's "outside a provider job object" should be reworded to that invariant, because the session-wide job makes "outside any job" unattainable.
- Points to settle with B:
  - Pass `bInheritHandles = FALSE`. With a parent attribute, inherited handles come from the shell's handle table, not the hook's.
  - Build the environment from the shell's token.
  - Identify the shell through `GetShellWindow` and its process, check image, session, owner and creation time, and fail closed and queue when no shell is found.
  - The elevated-hook case is the main benefit (the token comes from the shell, so the tray is not elevated) and is still unverified.
  - Parent-process spoofing is a known detection pattern (MITRE T1134.004). For a tool started from hooks on developer and corporate machines, an EDR alert is a real adoption risk, not only a theoretical false positive.
- The child-side self-check queries the immediate job with `QueryInformationJobObject(NULL, …)` and exits with a recorded reason on `KILL_ON_JOB_CLOSE`. It is cheap but sees only the immediate job, so it mainly guards against picking the wrong shell. Make it diagnostic first (a log code and a doctor field), so that a false trigger never silently keeps the tray down.
- Option C deserves a second look: the shell itself launches the tray through `IShellDispatch2.ShellExecute` from the desktop shell view. Its measured 400 to 700 ms hardly matters because the hook never waits. It is the documented route for launching unelevated from elevated and does not look like PPID spoofing. Its real costs are COM interop under NativeAOT and passing arguments through a shortcut or a fixed command line. If EDR friction is a concern, C may be the better primary option, with B as the alternative.
- D as the fallback is reasonable and the most robust option for elevated hooks. However, it is a persistent system registration, close in spirit to the sign-in entry the user rejected. If chosen:
  - set the execution time limit to none;
  - disable the battery conditions;
  - use the limited interactive principal and the ignore-new instance policy;
  - give doctor and removal ownership of the task.

**T-5.** Option 2 is an opt-in setup step that stops the provider's own title, with option 3 (color and badge only, with an honest matrix) as the fallback.

- I agree with the direction. Codex rewrites the title about 20 times a second during a turn, so writing after the provider (option 1) cannot win. The probe reproducibly shows a provider behavior that can be configured away, not a hard restriction, so per AGENTS.md the configured route has to be offered.
- Scope caveat: both settings apply to the whole provider profile (`settings.json` `env` for Claude Code, `config.toml` for Codex), not only to sessions Bell Tower presents. `CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1` also stops the generated-title request and the terminal status reports. The consent text must say this, and the step must follow the existing ownership, backup and removal rules.
- It is unverified that Codex `[tui] terminal_title = []` stops the title; probe it before offering it.
- Tab selection in Windows Terminal depends on the managed title. Under option 3, or while the step is off, native-session navigation should be reported as window only with the stated reason from this change, rather than as a tab that sometimes matches.
- Option 4 (the Claude `sessionTitle` hook output) is worth noting for Claude, but it changes the hook's `{}` contract and has no Codex equivalent, so it would need a parity exception. I do not prefer it over option 2.

Decisions needed (orchestrator or user): triage of ANALYSIS-4600 to ANALYSIS-4603; the T-12 and T-5 choices above.
