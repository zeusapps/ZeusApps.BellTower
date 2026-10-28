# QA-009 MXC echo controls

## Scope

Bounded task 2.1 follow-up for general MXC process-start viability. These controls run `cmd.exe` directly and do not start Codex as a sandbox child, inspect shell discovery, register or dispatch hooks, or claim fallback acceptance.

## Shared setup

- Used the same direct Codex executable resolution order as `ProviderHookProbe.ResolveCodexExecutable()`; no command or PowerShell wrapper was invoked.
- Direct executable reported `codex-cli 0.164.0`; SHA-256 `6f267830b029ee25d9339864aa867cd45de9f8fd4c5ea007d2e63c13a0c66d36`.
- Used fresh `CODEX_HOME`, `TEMP`, and `TMP` under ignored `artifacts/qa-hook-fallback-009/`. Profile config SHA-256: `a8a697d1227cc9707ee2d18b11607919c98a884f14623d03f45a577bb5f3ef53`.
- Both profiles granted `:root` read and only this scratch root write, disabled networking, and used `windows.sandbox=mxc`. The first had no deny rules. The second additionally denied the fixed PowerShell 7 and Windows PowerShell executable paths.
- Both PowerShell candidates and `System32\cmd.exe` were present. No host ACL, account, provider configuration, or installation was changed.
- Each control used separate `ProcessStartInfo.ArgumentList` elements for the CLI and child: `-c`, `windows.sandbox=mxc`, `sandbox`, `-P`, `<profile>`, `--`, `<absolute System32\cmd.exe>`, `/d`, `/c`, `echo`, `MXC_BASELINE_OK`. Codex stdout and stderr were redirected and drained asynchronously. No nested command quotes, marker redirection, or shell output redirection were used.

## Results

| Profile | Profile-section SHA-256 | Exit | Elapsed | stdout | stderr |
| --- | --- | ---: | ---: | --- | --- |
| `qa-hook-fallback-009` (no deny rules) | `e8f0ddd1ff608dbb4cf219db63d410598861b118ef758d9ab2ee6a39d549de0d` | 0 | 236 ms | `MXC_BASELINE_OK` | empty |
| `qa-hook-fallback-009-deny` (two PowerShell denies) | `2a00df930951dee4ff8c9180bd4c1d4120b2a49237fdc27664d5625544222685` | 0 | 232 ms | `MXC_BASELINE_OK` | empty |

Both exact expected stdout values prove `cmd.exe` ran for these two echo controls. The outer Codex processes exited and no process remained. Raw output files are in ignored scratch: `artifacts/qa-hook-fallback-009/baseline.stdout.txt`, `baseline.stderr.txt`, `deny.stdout.txt`, and `deny.stderr.txt`.

The deny-profile echo result establishes that a process can launch with those deny entries. It does not establish that denied files were inaccessible, that Codex shell discovery treated them as absent, or that cmd fallback and a hook capture work.

## QA-008 interpretation correction

QA-008 recorded exit 1, absent marker, and `The filename, directory name, or volume label syntax is incorrect.` The sanitized .NET `ArgumentList` used these final entries:

```text
<absolute System32\cmd.exe>
/d
/c
echo MXC_BASELINE_OK>"<scratch>\mxc-baseline.txt"
```

The final command was one argument after `/c`; its value contained literal quote characters around the marker path, with no literal backslash-before-quote characters added by the PowerShell expression. It also used cmd output redirection. That stderr could have originated from `cmd.exe` after the child started; marker absence alone does not establish that MXC failed before child creation. These QA-009 controls avoid that ambiguity and pass for the two exact echo commands. They do not determine which stage emitted QA-008's error or explain its cause. QA-008 remains immutable history; this report corrects its interpretation only.

## Boundary

No native Codex hook command, shell selection observation, durable ingress capture, or path-special-character dispatch was attempted. Task 2.1's cmd/Bash fallback acceptance remains open. No provider restriction is inferred.

