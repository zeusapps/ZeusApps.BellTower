# Implementation evidence DEV-045 (tasks 4.1, 4.3, 4.4)

Baseline `1c80a7d`. Scope is the 2026-11-13 reduced slice (design decision 8): adopted direct sessions only. Awaiting independent verification.

## 4.1 Visible click result

Cause (from the 2026-11-13 trial record): a plain `claude` in Windows Terminal had one visible Terminal window, so `WindowsTerminalNavigator.IsAvailable` passed and `WindowsTerminalFocusService` succeeded at `window only`. `TrayApplication.Navigate` showed a balloon only when `!result.Success`, so a partial success wrote only to the Sessions window status and a click on a window that was already in front looked dead.

Fix (shared tray policy, no per-provider code): `NavigationOutcome.NeedsNotice(FocusResult)` is true for a failure or any success whose precision does not start with `exact` (window only, terminal only, tab or window). `Navigate` balloons the existing result message in that case. An exact tab or terminal shows no extra balloon.

Several windows in one Terminal process: `IsAvailable` still requires exactly one visible window and matching is not widened. The failure already ballooned; its message now says the process has several windows (`WindowsTerminalNavigator.UnavailableReason`, `WindowProbe.VisibleWindowCount`), otherwise the old text.

Tests: `NavigationOutcomeTests.OnlyAnExactSuccessIsAllowedToSkipTheVisibleNotice` (8 cases), `AWindowTerminalProcessWithSeveralWindowsStatesItsReasonAndNeedsANotice` (claude, codex).

Not covered by a fixture: a real second Terminal window and a reused shell PID at the navigator level (the existing `NavigationTests` and `TerminalTitleTests` cover stale handles and reused shells; the new tests cover the reused shell for presentation writes).

## 4.3 Native-presentation setting

Decision point: `Core/NativePresentation` (`IsAdoptedDirect`, `GrantsTitle`, `Effective`) on the consumer side. Ingress (`NativeAdoption`, capture) is unchanged and stays neutral: the stored attachment keeps `ManagedTitle` and `ManagedBackground` false. Every consumer that today reads `attachment.ManagedTitle` asks the one function with the one setting: `TerminalPresentationResolver` (tray pump), `TerminalTitleService` (isolated helper, re-reads the setting under the presentation lock), `WindowsTerminalFocusService` (tab selection) and `WindowsTerminalNavigator.Precision`. Reason: the setting then applies to live sessions with no stored-state migration and no write on the hook path.

Qualification: observed ownership, no `endedAt`, verified agent process, verified shell, stamped evidence for that agent generation with lifecycle owner `agent-process`, not provisional, mode neither `shared-backend` nor `headless`. Managed launches never qualify (their own controls and opt-outs win). Plain default Codex records no process or shell and is `shared-runtime`, so it never qualifies; a forged shared-backend record with a process is excluded by its evidence.

Setting: `AppConfig.NativePresentation`, default false; tray menu item "Title native sessions (plain claude, codex --no-daemon)" next to the voice and microphone items, with a tooltip.

Live toggle (chosen): applied without restart. The pump resolves each refresh, so turning it on writes the title to live qualifying sessions, and turning it off (or a session ending) restores the title.

Surface left unchanged: background color. A session Bell Tower did not launch has no recorded prior color, and no existing mechanism can restore it (managed launches restore with a reset sequence written by their own shell wrapper, which does not exist for native sessions). Per the spec scenario "No safe restoration mechanism is available" it stays unchanged; `NativePresentation` grants the title only and `TerminalTitleService` rejects a background request for a native session. This is a limitation to report in diagnostics and documentation (tasks 5.2, 5.3), not a provider restriction.

Restoration (new mechanism, stated): the existing console opener requires the agent to be in the console process list, which fails after the agent exits, so nothing existing can restore at exit. Added:
- `TitleRestoreStore` records, before the first write, the shell identity (pid plus creation time), the previous title read from the verified console and the last title written. No readable previous title means no write.
- `sync-title --restore <attachment>` (isolated helper) runs `TerminalTitleService.Restore`: discards the record without any write when the shell is gone or reused; skips when another live native session in the same shell owns the title (a second session inherits the original previous title); otherwise attaches by the shell identity alone (`AttachedSessionConsole.OpenShell`) and sets the previous title only if the console still shows the title Bell Tower wrote.
- `TerminalTitlePump` restores every record whose attachment is no longer presented (ended, identity changed, setting off), with a 30 second retry; it also catches records left by a tray restart. Managed launches have no record and keep their wrapper-based restoration.

Parity: one code path for both providers; tests are parameterized for Claude Code (`interactive-client`, `unknown`) and Codex `--no-daemon` (`embedded-runtime`), with a mixed simultaneous case. Shared-backend Codex is excluded by design decision 8 and asserted to receive no title, no tab selection and no restore record.

Tests (`NativePresentationTests`, `TerminalTabSelectionTests`): default off and round trip; off leaves title untouched; on writes title only for both providers and not for shared-backend Codex; setting turned off between resolve and write stops the write; background request refused; changed agent or shell identity and reused shell write nothing; no readable prior title means no write; exit restores only the recorded shell and only the written title; gone or reused recorded shell discarded without a write; two sessions in one shell; pump applies without restart and restores on setting off or session end; managed launches never restored by the pump and keep their background; Windows Terminal tab selected only while the setting is on and never for shared backend.

Mutants (both killed, then reverted): removing the `enabled &&` gate in `GrantsTitle` failed 9 tests; removing the evidence check (mode and lifecycle exclusion of shared backend and headless) failed 8 tests.

## 4.4 Managed launches unchanged

`MixedLaunchAndNativeTests`: a managed launch and a native session for each provider in one data directory, with the setting toggled off, on, off, on. The native sessions do not inherit the launch's prepared palette, pending launch record or presentation flags; managed launches keep title and background; waiting episodes and start times are unchanged; a second alert collection after each toggle replays nothing; palettes are not reallocated. Existing fixtures for pre-launch coloring and native-name controls (`NativeAdoptionReconcileTests`, `PresentationSchedulerTests`, `TerminalTitleTests`) pass unchanged. The final shell restoration is in the PowerShell wrapper and has no new code here; its existing tests pass.

## Limitations

- Background is not applied to native sessions (above).
- Previous-title restoration restores the title found when Bell Tower first wrote; if the provider changed the title in between, Bell Tower does not overwrite the provider's newer title.
- Real Windows Terminal and VS Code behavior was not exercised (isolated data and fakes only). Task 6.1 owns that.

## Follow-up (user decision 2026-11-13): native color

Purpose: visual association between simultaneously running sessions. Exact prior-color restoration is not required, so the earlier "background left unchanged" limitation is superseded. The first section above is kept as the record of that round.

Change:
- `NativePresentation.Effective` now grants title and background together, under the same qualification and the same single setting. Shared-backend Codex, headless runs, managed launches and unverified sessions still receive nothing.
- `TerminalPresentationResolver` requests the session's identity palette background for a qualifying native session (the palette the chip and badge use and the managed path uses); `TerminalColors.Apply` pairs the readable foreground. The background refusal for native sessions in `TerminalTitleService` is removed; it still requires the background to be a valid hex color.
- Reset sequence: `TerminalColors.Reset` = OSC 110 then OSC 111, each terminated by ST. These are the same sequences `BellTower.psm1` writes for a managed launch on exit. Their effect in Windows Terminal is the established behavior of that terminal; their effect in the VS Code integrated terminal (xterm.js) is ASSUMED, not verified here (no real host was run). Trial task 6.1 should confirm it.
- Decision rule for resetting: `TitleRestore.Colored` is set when Bell Tower itself wrote a background for that attachment (kept true across later writes). Restore resets colors only when the record says `Colored`, regardless of what color the console shows; Bell Tower never reads or saves a prior color. A record without `Colored` (a title-only write) never resets colors.
- Safety is the same path as the title restore: the helper attaches by the recorded shell identity (PID plus creation time) via `AttachedSessionConsole.OpenShell`; a gone or reused shell discards the record with no write; restore is skipped when another live native session with its own record shares the shell. Reset runs when the session ends, the setting is switched off, or the session stops qualifying (the pump restores any record whose attachment is no longer presented). If the title restore or color reset fails, the record stays and is retried; the title step is idempotent.
- Menu item renamed "Title and color native sessions", tooltip updated.

Tests (claude and codex `--no-daemon`, plus mixed): off writes no title or color; on writes each session its own distinct palette background; invalid color refused and a valid one accepted; exit restore resets colors once on the recorded shell only, and not twice; a gone or reused shell gets no write; a title-only record never resets colors; two sessions in one shell leave the color to the last live one; shared-backend Codex gets no color or restore; managed launches keep their palette background and are never restored by the pump; the pump applies and resets without a restart on switch-off or end; the reset sequence constant is asserted. Mutants killed (reverted afterward): the effective-background grant (11 failures) and the resolver's background request (8 failures).
