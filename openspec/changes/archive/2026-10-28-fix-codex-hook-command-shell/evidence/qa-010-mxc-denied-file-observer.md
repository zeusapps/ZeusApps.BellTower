# QA-010 MXC denied-file observer

## Scope

Bounded task 2.1 follow-up to distinguish filesystem metadata visibility from content-read enforcement for the two fixed Codex PowerShell candidates. A direct Node observer reports only `statSync().isFile()` success/error and whether opening the executable and attempting a two-byte read succeeds/error. It never prints or stores file bytes. No shell executable is launched; no Codex session, hook, provider, or network is used.

## Setup and identity

- Resolved direct Codex and Node executables from PATH, using the product resolver's direct Codex search order. Codex CLI reported `0.164.0`.
- Codex executable SHA-256: `6f267830b029ee25d9339864aa867cd45de9f8fd4c5ea007d2e63c13a0c66d36`.
- Node executable SHA-256: `d14ba95cdce1ef7dc9ad3ac74949ca5db38b27378ee30f30a23cf26f9e875a11`.
- QA-010 observer SHA-256: `456fa4eb0695e2425336c45035ed7cabeb2aaaab9072ed8939828290e13ea105`.
- Fresh `CODEX_HOME`, `TEMP`, `TMP`, observer, and raw outputs were kept beneath ignored `artifacts/qa-hook-fallback-010/`.
- MXC config SHA-256: `a21450c5365ba2ae8d8624a084b8f1b5e07a593c072d8a0609bd32d61f913c8d`; its `qa-hook-fallback-010` filesystem section SHA-256 is `32e76411ec24ceb37cb2e0f36130f87ba7284340c96d5ba28d37562bef7b3aa9`.
- The profile granted `:root` read and scratch-root write, disabled networking, and denied both full PowerShell directories: `C:/Program Files/PowerShell/7` and `C:/Windows/System32/WindowsPowerShell/v1.0`. This leaves each executable's parent path covered by its explicit directory deny.
- The host observer ran directly under Node. The MXC observer used the direct Codex executable with `-c windows.sandbox=mxc sandbox -P qa-hook-fallback-010 -- <absolute node.exe> <observer.js> <PowerShell 7 exe> <Windows PowerShell exe>`, using separate argument-list entries. PID, start time, and launched executable path were captured immediately. The MXC process exited normally and no process remained.
- For comparison, the preceding source investigation 008 profile hash is `5cae5f8755c8af473824174aee588db49343e092331dc09c371dad4a59f46b0b`, and its stat-only observer script hash is `b8c988547c03225a5ab3da72f23e850438f6056de385eaa559ed26c0614de587`.

## Results

| Observation | PowerShell 7 executable | Windows PowerShell executable |
| --- | --- | --- |
| Host baseline | `isFile=true`, stat error `null`, open/read succeeded, read error `null` | `isFile=true`, stat error `null`, open/read succeeded, read error `null` |
| MXC denied-directory profile | `isFile=false`, stat error `EPERM`, open/read failed with `EPERM` | `isFile=false`, stat error `EPERM`, open/read failed with `EPERM` |

The host Node process exited 0; the MXC Codex process exited 0 in 321 ms with empty stderr. Raw observer output is retained in ignored scratch at `artifacts/qa-hook-fallback-010/host.stdout.json`, `host.stderr.txt`, `mxc.stdout.json`, and `mxc.stderr.txt`. The JSON contains only fixed system paths, booleans, and error codes; it contains no file bytes.

## Interpretation and boundary

This run confirms the explicit denied-directory profile is enforced for the observer: both `statSync` and open/read attempts fail with `EPERM` in MXC, while the host baseline can stat and read both files. This differs from the metadata-only result in source investigation 008, which denied the exact executable paths but observed both as present. It does not establish that Codex's own PowerShell resolver responds to these denied-directory errors by selecting cmd, nor that the generated hook command executes or yields a durable capture. No native hook phase was run. Task 2.1 cmd/Bash fallback acceptance remains open; no provider restriction is inferred.

