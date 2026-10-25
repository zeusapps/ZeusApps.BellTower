# Implementation 001: fix-vscode-window-activation

Baseline `9b26088`. Working tree is uncommitted. Prototype evidence: `prototype-001-window-mapping.md`.

## Design as built

- Registration (written by the JS bridge) gains `protocol: 2` and `window` (handle string, owner `{pid, startedUtcTicks}`, class, observedAt) or `null`. Old records (no `protocol`/`window`) still deserialize and are an explicit partial state. The release `bridge = 1` contract is unchanged: file formats stay mutually readable (new tray with old bridge, and old tray with new bridge, which keeps the legacy focus path for requests without `protocol`).
- Request gains `protocol: 2`, `mode: "reveal"`. A protocol 2 bridge only reveals (`revealTerminal`: exact `terminal.show`, never global `terminal.focus`); the legacy `focusTerminal` stays for requests without a protocol.
- `VsCodeWindowOwnership` (C#) verifies the evidence at click time. `VsCodeNavigator` activates first (`WindowActivator.ActivateConfirmedAsync`: restore only if iconic, SetForeground, confirm the actual foreground HWND), then requests the reveal, then re-confirms the foreground. Success requires verified owner, confirmed foreground and confirmed reveal. Unverified: the terminal is still revealed best effort, the result is a failure, and `Precision` is `terminal only`.
- `bell-tower foreground-window` (hidden CLI, two agreeing samples 30 ms apart) supplies the foreground sample; registered as a non-tray command in `TrayInvocationCommand`.
- Bridge storage: `atomic()` removes only its own unique `.tmp` on any failure and never touches the destination; registration publication is separated from request processing; expired ownership (no successful publish within 15 s) is never used and gets an honest failure receipt; a per request error does not abort other requests; logging is one line per key per minute with a suppressed count.

## Task status

| Task | Status | Evidence |
| --- | --- | --- |
| 1.1 | done | Predecessor `reconcile-session-liveness-and-title-refresh` (b3e007e) supplies verified process/start identities used by the registration checks (`ProcessProbe.Identify`); existing bridge protocol tests read first. |
| 1.2 | done | `prototype-001-window-mapping.md` |
| 2.1 | done | `VsCodeWindowNavigationTests` (legacy record, no evidence, expired registration, PID reuse, conflicting claims, handle of another process, destroyed handle, lineage, `OlderBridgeRegistrationsStayReadable`) |
| 2.2 | done with fakes | `WindowActivationTests` (restore only minimized, confirm the actual foreground, denied), `VisibleWindowIsNeverRestoredButAMinimizedOneIsRestoredOnce`, `DeniedActivationFailsHonestly...`. Real fullscreen/maximized/minimized behavior is live task 4.2. |
| 2.3 | done | `VerifiedWindowIsActivatedThenTheExactTerminalIsRevealed` (claude and codex), `MixedClaudeAndCodexSessionsInDifferentWindowsNeverCrossRoutes`, `focus.test.js` reveal test (exact terminal plus focused window, no `terminal.focus`) |
| 2.4 | done | `Navigate` marks viewed only on success and `StateStore.MarkViewed` is episode guarded; existing `ViewedTimerTests.DelayedNavigationCannotHideTheTimerOfANewerWait` plus the failure paths above. No tray change was needed. |
| 3.1 | done | `service.test.js`: injected rename and write failures leave zero `.tmp`, previous registration kept, next tick recovers |
| 3.2 | done | `service.test.js`: request served while ownership is live during a registration failure, expired ownership never focuses (failure receipt), failing result write keeps the request queued, bounded logs |
| 3.3 | done, isolated | `scratchpad` run of the VSIX assembly steps from `scripts/package.ps1` plus `code --install-extension --user-data-dir --extensions-dir` into disposable dirs: installed `extension.js`, `service.js`, `focus.js` byte-identical to source, a pre-seeded `settings.json` unchanged, `Code.exe` process count unchanged (15 before and after), exit 0. `scripts/package.ps1` itself was not run (publishes the whole app, needs the native toolchain); the VSIX file list is unchanged. Setup now adds a note that no editor or agent is restarted and that a reload loads the bridge. |
| 4.1 | done, no production windows | Node: 25 passed. .NET: 853 passed (one unreproduced failure in one earlier full run, clean on two reruns). Disposable two-window VS Code fixture with the real extension, built `bell-tower.exe` and a harness calling `VsCodeNavigator`: live reveal request path and the partial result for never-focused windows. Native activation tests use recording fakes, not real windows. |
| 4.2 | not done | Not this role. During the unattended session the foreground belonged to the user's application and SetForegroundWindow from automation was refused, which also reproduced the field symptom. |
| 4.3 | done | `docs/compatibility.md`, `docs/installation.md`, `docs/architecture.md`; `dotnet format --verify-no-changes` on changed files, Release build 0 warnings, tests above. No Claude or Codex provider transport touched. |
| 5.1 | done | Host path is shared; `claude` and `codex` theory cases plus the mixed two-window case. |
| 5.2 | done with a gap | No provider specific setup, default, configuration, diagnostic or control exists on this path; chip tooltip and precision are provider independent. No provider restriction claimed. `doctor` does not yet report per window verification state. |

## Limits and decisions

- Windows can still refuse a foreground request; the result is then a failure and the chip keeps its timer.
- A VS Code window must gain focus once after its bridge loads to be verifiable. Until then chips read `terminal only` and clicks report failure while still selecting the terminal.
- Not claimed: keyboard input focus, remote/WSL windows, layouts where the extension host is not a direct child of the window-owning process.
