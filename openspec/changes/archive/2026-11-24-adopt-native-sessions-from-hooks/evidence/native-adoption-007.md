# Native adoption probe follow-up 007

Date: 2026-10-29. Scope: assess one documented Windows Shell route for launching a harmless hidden child at standard-user privilege from the current elevated process. No provider was launched. No child process was launched. No files outside ignored artifacts and this evidence report were changed.

## Documented route

The current Microsoft Learn documentation describes `IShellWindows::FindWindowSW` as returning a matching Shell window's handle and `IDispatch`. The desktop can be selected with `SWC_DESKTOP`, and `SWFO_NEEDDISPATCH` requests that dispatch interface. The Microsoft Learn reference for `IShellDispatch2::ShellExecute` defines the operation parameter and show parameter; `open` is the ordinary launch verb and show value `0` requests an initially hidden window. Microsoft's `ShellExecute` documentation identifies `runas` as the verb that requests elevation and may display a UAC prompt. The `open` verb was selected; `runas` was not used.

Raymond Chen's Microsoft Windows developer blog documents the elevated-to-unelevated pattern: obtain the desktop folder view hosted by Explorer, get its `Application` shell object, and call `IShellDispatch2::ShellExecute`. Its explanation explicitly relies on the object being hosted in the desktop shell process. This is the route selected for the smoke; no unrelated token or shell-launch method was attempted in this follow-up.

References:

- [Microsoft Learn: IShellWindows interface](https://learn.microsoft.com/en-us/windows/win32/api/exdisp/nn-exdisp-ishellwindows)
- [Microsoft Learn: IShellWindows::FindWindowSW](https://learn.microsoft.com/en-us/windows/win32/api/exdisp/nf-exdisp-ishellwindows-findwindowsw)
- [Microsoft Learn: IShellDispatch2::ShellExecute](https://learn.microsoft.com/en-us/windows/win32/shell/ishelldispatch2-shellexecute)
- [Microsoft Learn: Launching Applications](https://learn.microsoft.com/en-us/windows/win32/shell/launch)
- [Microsoft Windows developer blog: How can I launch an unelevated process from my elevated process and vice versa?](https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643)

## Bounded read-only preflight

The desktop shell window was present. A read-only attempt to obtain the desktop automation dispatch through `Shell.Application.Windows()` failed with Access Denied. A second read-only attempt used the documented `CLSID_ShellWindows` activation path and `FindWindowSW` with the desktop class and dispatch-required option. The COM class activated, but the call failed with `E_ACCESSDENIED` (`0x80070005`) before returning the desktop dispatch. No `ShellExecute` call was made.

This outcome is an execution-context prerequisite failure for the selected documented route: the elevated process could not obtain the existing desktop shell's dispatch interface. It is not evidence that Windows lacks the route or that Codex imposes a provider restriction. The earlier linked-token conversion failure remains as recorded in [follow-up 006](native-adoption-006.md); it was not retried.

## Disposition

The exact harmless-child smoke did not run. No same-user shell-owner comparison, child PID/creation identity, child token elevation, or natural-exit result is claimed. The exact `ShellExecute` action was not submitted for auto-review because the required existing-shell dispatch preflight failed before that action could be reached. No alternate COM/API launch method, user terminal, provider, sandbox setting, or host security setting was touched. A future attempt requires an execution context in which this elevated process can read the same-user desktop shell's automation interface, followed by normal approval of the exact hidden-child smoke command before any launch.
