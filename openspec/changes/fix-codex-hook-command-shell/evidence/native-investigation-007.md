# Native Codex MXC probe plan 007

## Status

This records the source-backed plan and the result of the one initial control released by the orchestrator. No hook, fake endpoint, or native Codex child session has been run.

The target is the unchanged installed Codex CLI `0.164.0`, selected by the product's `ProviderHookProbe.ResolveCodexExecutable()` resolution path and verified by its absolute executable path and file hash before launch. Do not use a profile shim or alter the installation. Product identity remains the frozen `aadbfb3` baseline described in the handoff; no product files are in scope.

## Initial harmless control released and attempted

The first invocation was intended to check only whether MXC could start a child with explicit denied filesystem paths and whether the child observed the two PowerShell executables as absent. It did not start Codex as a child, contact a provider, register or dispatch a hook, or claim cmd fallback coverage.

Before launch, the controller must:

1. Create a fresh scratch root under the repository's already ignored `artifacts/codex-hook-fallback-007/`, validate the resolved absolute path is beneath that exact directory, and place the isolated `CODEX_HOME`, `TEMP`, `TMP`, profile, and marker beneath it. No path is resolved through the user's home directory.
2. Set `CODEX_HOME`, `TEMP`, and `TMP` in the child process environment before starting the native executable. Limit `PATH` to `%SystemRoot%\System32`; preserve required inherited Windows variables without dumping or recording the environment.
3. Read-only inventory the two fixed PowerShell paths and the resolved Codex executable. If either fixed candidate is absent on the host, stop and report the control inconclusive rather than treating absence as sandbox masking.
4. Write this profile to the isolated Codex config using the pinned `0.164.0` named permission profile schema. `<scratch-absolute-path>` must be the validated root above.

```toml
[permissions.codex-hook-fallback-007.filesystem]
":root" = "read"
"C:/Windows/System32" = "read"
"<scratch-absolute-path>" = "write"
"C:/Program Files/PowerShell/7" = "deny"
"C:/Windows/System32/WindowsPowerShell/v1.0" = "deny"

[permissions.codex-hook-fallback-007.network]
enabled = false
```

`:root` read access retains normal loader/system reads to avoid unrelated Windows loader failures; the scratch write grant permits the marker, and the two narrower PowerShell deny entries must remain denied even below the broader System32 read grant. Network is disabled for this filesystem-only control. The profile field shapes and `read`/`write`/`deny` values come from the pinned [permission TOML schema](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/config/src/permissions_toml.rs#L1492-L1664) and [filesystem access enum](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/protocol/src/permissions.rs).

The direct native CLI argument vector is:

```text
<resolved-codex.exe>
-c
windows.sandbox=mxc
sandbox
-P
codex-hook-fallback-007
--
<SystemRoot>\System32\cmd.exe
/d
/c
if exist "C:\Program Files\PowerShell\7\pwsh.exe" (echo PS_VISIBLE>"<scratch-absolute-path>\mxc-control.txt" & exit /b 23) else if exist "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" (echo PS_VISIBLE>"<scratch-absolute-path>\mxc-control.txt" & exit /b 23) else (echo PS_MASKED>"<scratch-absolute-path>\mxc-control.txt" & exit /b 0)
```

Construct the process with .NET `ProcessStartInfo.ArgumentList` so Windows argument quoting is not reimplemented in PowerShell. Resolve `<SystemRoot>` from the existing process environment and verify it is the Windows system directory before launch. Do not invoke `codex sandbox windows`; the supported form is the unified `codex sandbox` command with the explicit `windows.sandbox=mxc` override.

Capture the launched PID, process start time, executable path, and start timestamp immediately after `Start()`, before waiting. Wait no more than 10 seconds. Record only the exit code, whether the marker is `PS_MASKED`/`PS_VISIBLE`, elapsed time, and sanitized stderr. If MXC reports the backend or native denied-path capability unavailable, or either path remains visible, stop without retries. On timeout, stop only the process whose PID, start time, and executable path match this launch; do not stop unrelated processes or descendants by broad name. Verify the marker is under the owned scratch root before reading it.

The first in-sandbox attempt exited in 95 ms with `failed to prepare MXC sandbox: native MXC is unavailable on this executor`; it produced no marker. Following the approved outside-tool-sandbox retry, the unchanged native executable exited in 216 ms with `MXC launcher: CreateProcessW failed with error code 5 (0x00000005)`; it also produced no marker. Both attempts used the same isolated profile and exact direct-executable argument vector. PID and process start time were recorded immediately after each start. A read-only follow-up found neither launched PID still running. No marker means the child never reported either visibility result.

The capability/visibility control is inconclusive: the outer MXC command could not create its child in either execution context. The second error is an access-denied launch failure, but it does not establish whether the backend lacks native denied-path support or whether this executor blocks child creation. No fallback behavior, shell selection, or product restriction is inferred. Stop here; do not retry or broaden the probe.

## Native hook phase remains unapproved and underspecified

After a successful control, the next desired probe would start an unchanged native Codex child under a second isolated profile and dispatch a product-generated Codex `SessionStart` command. Before that phase can be planned for release, the loopback fake endpoint needs a pinned-schema-supported, isolated networking profile. Source reviewed so far shows `network.enabled=false` denies loopback, while MXC's managed-network path changes local-loopback behavior. I have not established a minimal supported profile that allows only the fake endpoint and blocks external destinations. Therefore no endpoint, session command, hook registration, or native-capture attempt is included in this release request.

If later authorized, the stage must use the existing fake endpoint and unchanged product builder/helper with the recorded package/helper hashes from the orchestrator's assignment. It must prove a valid durable ingress capture and a real cmd shell marker from native hook dispatch. Direct cmd recorder runs remain controls. Path cases must include spaces, apostrophe, Unicode, `%NAME%`, and `!&^()`; the child must define `NAME` differently from its parent so unwanted expansion is observable. Do not report native fallback acceptance from a marker without the protocol capture and shell identity evidence.

## Source boundary and current outcome

The pinned source trace in [investigation 006](native-investigation-006.md) shows MXC creates a native denied-path policy with DACL mutation disabled and fails closed if native denied paths are unavailable. The orchestrator separately checked the unified CLI help. Neither source nor help proves runtime availability or that the shell resolver sees the fixed PowerShell candidates as absent; only the released control can establish those observations.

The isolated `CODEX_HOME`, profile, project, and temp paths remain only under the assigned ignored scratch root. No process remained after the attempts. No native hook invocation has been run in this phase. Native Codex cmd/Bash hook coverage and task 2.1 acceptance remain open. The stage-2 networking question remains unresolved and unapproved.
