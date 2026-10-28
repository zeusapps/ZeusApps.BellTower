# Native Codex MXC PowerShell visibility probe 008

## Scope and result

This bounded probe checks whether the pinned Codex CLI `0.164.0` MXC permission profile makes its two fixed PowerShell executable candidates appear absent to a child process. It uses a small Node metadata observer that only calls `fs.statSync` and writes JSON to stdout. It does not run a Codex session, register a hook, contact a model/provider, or test cmd fallback dispatch.

**Result: MXC started the observer successfully, but both PowerShell paths remained visible.** Host and sandbox observations both returned `exists: true` for PowerShell 7 and Windows PowerShell. Because the released next phase required the paths to be hidden, stop here. No native Codex session or hook attempt was made, and no fallback acceptance or provider restriction is inferred.

## Setup and identity

- Used the direct installed executable resolved by the product's `ProviderHookProbe.ResolveCodexExecutable()` search order, without invoking the command shim. Its SHA-256 was `6f267830b029ee25d9339864aa867cd45de9f8fd4c5ea007d2e63c13a0c66d36`, matching the direct executable recorded in QA-009.
- Created the profile, observer script, isolated `CODEX_HOME`, `TEMP`, `TMP`, and working directory only beneath ignored `artifacts/codex-hook-fallback-008/`. The scratch path was validated to remain within that exact directory.
- Limited the child `PATH` to `System32`; passed the absolute Node executable and observer-script path as separate `ProcessStartInfo.ArgumentList` entries. Codex received `-c windows.sandbox=mxc sandbox -P codex-hook-fallback-008 -- <node.exe> <observer-script>`.
- The selected isolated profile used the same read boundary as QA-009, wrote only to its own scratch root, disabled networking, and denied the two fixed executable paths:

```toml
[permissions.codex-hook-fallback-008.filesystem]
":root" = "read"
"<validated-scratch-root>" = "write"
"C:/Program Files/PowerShell/7/pwsh.exe" = "deny"
"C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe" = "deny"

[permissions.codex-hook-fallback-008.network]
enabled = false
```

The isolated profile SHA-256 is `5cae5f8755c8af473824174aee588db49343e092331dc09c371dad4a59f46b0b`; the observer script SHA-256 is `b8c988547c03225a5ab3da72f23e850438f6056de385eaa559ed26c0614de587`. No real provider credential or network request was used.

The observer emitted, in both the host baseline and the MXC child:

```json
[{"path":"C:/Program Files/PowerShell/7/pwsh.exe","exists":true,"error":null},{"path":"C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe","exists":true,"error":null}]
```

The host observer exited 0. The direct native Codex MXC command exited 0 in 287 ms with exactly that JSON on stdout and empty stderr. PID and process start time were captured immediately after each launch. A read-only follow-up found neither process still running. No host ACL, installed executable, provider setting, or user configuration was changed.

## Interpretation and boundary

The result proves that the metadata observer can see both fixed PowerShell paths under this exact denied-path profile. It does not prove whether attempts to open or execute either file are denied, nor what Codex's PowerShell discovery implementation would do after such a denial. The probe did not launch PowerShell, alter `PATH` beyond the child process, or use a shell wrapper that could misidentify cmd as PowerShell.

The next native-hook phase was conditional on both paths being hidden and was therefore not run. There is no SessionStart event, generated hook command, shell-identity marker, or durable ingress capture in this evidence. Task 2.1 native cmd/Bash fallback acceptance remains open. The orchestrator may choose a different source-supported isolation strategy; no acceptance exception is proposed.

## Checks

- Host metadata observer: passed; both fixed executable paths present.
- Direct native MXC metadata observer: passed as a process-launch/control check; both fixed executable paths remained present.
- Process cleanup audit: passed; both launched processes had exited.
- Scratch ignore and report hygiene checks: passed. All generated configuration and script artifacts remain in the ignored scratch directory.
- Product build, tests, native hook capture, and fallback acceptance: not run; outside this evidence-only probe or gated by the failed visibility precondition.
