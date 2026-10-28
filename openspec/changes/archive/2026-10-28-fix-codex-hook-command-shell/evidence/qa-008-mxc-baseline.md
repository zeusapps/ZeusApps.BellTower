# QA-008 MXC no-deny baseline

## Scope

Bounded task 2.1 continuation for `fix-codex-hook-command-shell`. This checks general MXC child startup with the installed Codex CLI and a fresh private profile. It does not start Codex as a child, select a Codex hook shell, register or dispatch a hook, or establish fallback acceptance.

## Setup

- Resolved the native executable with the same search order and packaged-executable path as `ProviderHookProbe.ResolveCodexExecutable()`; no PowerShell or command wrapper was invoked.
- Direct executable reported `codex-cli 0.164.0`; SHA-256 `6f267830b029ee25d9339864aa867cd45de9f8fd4c5ea007d2e63c13a0c66d36`.
- Used a fresh private `CODEX_HOME`, `TEMP`, and `TMP` beneath the ignored `artifacts/qa-hook-fallback-008/` root. The profile allowed `:root` read and scratch-root write, disabled networking, and contained no deny rules.
- Set `windows.sandbox=mxc` explicitly and launched the absolute `System32/cmd.exe` path with `/d /c` and a command that would write `MXC_BASELINE_OK` to a marker verified beneath the owned scratch root.
- Both fixed PowerShell discovery candidates were present. No host ACL, account, provider configuration, or installation was changed.

## Result

**Failed: general MXC startup control.** The direct CLI exited with code 1 after 229 ms. It created no marker; stderr was `The filename, directory name, or volume label syntax is incorrect.` The launched CLI process was no longer running on the read-only follow-up check.

Since the no-deny baseline failed, the optional deny-policy comparison was not run. This result does not identify a deny-policy cause, prove a Codex restriction, show shell selection, or satisfy task 2.1 native fallback coverage. No retry or native hook phase was attempted.

