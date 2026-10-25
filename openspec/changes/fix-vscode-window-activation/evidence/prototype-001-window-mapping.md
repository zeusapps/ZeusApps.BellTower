# Prototype 001: instance-bound editor-window mapping (task 1.2)

Environment: Windows 11 (10.0.26300), VS Code 1.143.1 (x64) installed per user. All runs used a disposable fixture: `Code.exe --user-data-dir <scratch>\ud --extensions-dir <scratch>\xd --extensionDevelopmentPath <scratch>\extN -n <scratch>\wN`, two folder windows in one fixture main process, a fresh `BELLTOWER_DATA_DIR`. Only processes whose command line contained the scratch path were ever stopped. The user's own editor windows were not touched.

## Findings

1. Extension-host PID alone cannot pick a window (reproduced). Process tree of the two-window fixture: one main `Code.exe` (42380); both extension hosts (34712 for w1, 36256 for w2), both renderers (`--vscode-window-config=vscode:<uuid>`), the GPU and network processes are all children of that one main process. The two visible top-level windows (class `Chrome_WidgetWin_1`, HWNDs 12190848 and 48696652) are both owned by the main process. No process or command-line attribute links an extension host or renderer to an HWND; only the window title differs, which is a forbidden mechanism.
2. No stable official API exposes the window handle. `vscode.window.nativeHandle` exists only as the proposed `nativeWindowHandle` API (VS Code 1.96 release notes, ctx7 `/microsoft/vscode-docs`). A real extension that touched it failed activation: `Extension 't.proto' CANNOT use API proposal: nativeWindowHandle ... start in extension development mode or use --enable-proposed-api`. A shipped VSIX therefore cannot use it.
3. `window.state.focused` plus a foreground sample is a usable correlation. With the foreground switched between the two fixture windows five times, each extension host's `onDidChangeWindowState(focused)` event arrived 2 to 3 ms after the foreground HWND changed (5 ms poll):

   | foreground change (epoch ms) | HWND | extension events |
   | --- | --- | --- |
   | 1792938304380 | 48696652 (w1) | w1 focused=true at ...382, w2 focused=false at ...384 |
   | 1792938305917 | 12190848 (w2) | w1 false ...899, w2 true ...901 |
   | 1792938307413 | w1 | w2 false ...408, w1 true ...411 |
   | 1792938308926 | w2 | w1 false ...920, w2 true ...921 |
   | 1792938310439 | w1 | w2 false ...435, w1 true ...438 |

   The order of the false/true pair is not guaranteed, so a sample must be validated after it is taken.
4. The initial `window.state.focused` is not trustworthy. In later runs a newly opened fixture window reported `focused: true` at activation while the user's other application held the foreground, so the first sample returned a foreign window and was rejected by the owner/class/lineage check (`The foreground window is not this editor window` in the bridge output log). The bridge therefore samples only after an observed focus-gain event.
5. Ambiguous mappings fail safely. Covered by `VsCodeWindowNavigationTests` (two bridges claiming one handle, PID reuse of the window owner, handle owned by another process, destroyed handle, owner outside the bridge process lineage, never-focused window, protocol-less record): none of these calls any native activation API.

## Conclusion

A sound, supported-API-only mapping is available: a bridge records the foreground top-level window sampled while its own window reports a focus gain (stable double sample, owner pid equal to the extension host parent, class `Chrome_WidgetWin_1`, focus still reported afterwards). The host re-verifies owner identity (pid plus start time), class, visibility, process lineage and absence of another bridge claiming the handle at click time, and refuses anything else. Residual limits: a window never focused since its bridge loaded has no evidence (reported as `terminal only`); layouts where the extension host is not a direct child of the window-owning process also yield no evidence; keyboard input focus is not observable. A focus event that lags a very fast window switch could mis-attribute a sample; the post-sample focus check, the owner/class/lineage check and the duplicate-claim refusal bound this, but a lone mis-attributed claim with no competing claim cannot be excluded by construction (see handoff).

## Limit of this prototype

The foreground in the unattended session belonged to the user's application, and Windows refused SetForegroundWindow from the automation process (and new fixture windows did not take the foreground), so a live positive activation could not be driven without stealing the user foreground. That is left to task 4.2.
