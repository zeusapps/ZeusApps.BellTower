# Codex headless hook shell preflight 002

Date: 2026-10-29. This is a synthetic handler-shell preflight following QA-005. No Codex provider process was launched. The preflight read the saved QA-005 launch specification and reused its exact environment key set and values in the child process without printing them. The saved Codex home still declares one `SessionStart` command whose shell is `pwsh`; the command hash was observed unchanged. The corresponding script path exists, and `pwsh` resolves in the saved `PATH`.

The QA-005 run did not persist stdout or stderr, so its detailed CLI failure category cannot be recovered from artifacts. The preflight invoked the unchanged configured command with a synthetic `SessionStart` payload containing a `qa-synthetic-006-` session ID. The direct `pwsh` invocation started and exited 1 with stderr present, stdout absent, and no capture file created. Raw stderr was not retained or quoted; the sanitized category is `hookexitfailure`. A first `cmd.exe` wrapper invocation exited 64, but a `where pwsh` check under the same saved environment succeeded, so the wrapper result does not establish that the shell is unavailable.

No config, hook, or trust record was edited. The native capture directory remained empty. Since the handler produced no matching capture, there was no file to move into the separate synthetic evidence folder. No synthetic result is presented as native hook dispatch or ancestry evidence.

## Results

| Check | Result | Evidence boundary |
| --- | --- | --- |
| Saved QA-005 environment inspection | Passed | Recorded environment key names only; no values were displayed. |
| Declared shell availability | Passed | Configured shell is `pwsh`; it resolves through the saved `PATH`, and the configured handler script exists. |
| Unchanged handler invocation | Failed | Direct `pwsh` process started with the saved environment and exact configured command, then exited 1; stderr was present. Sanitized category: `hookexitfailure`. |
| Synthetic capture | Failed to establish | No new capture was written for the synthetic session. The native capture directory remains empty. |
| Private home preservation | Passed | No config, hook, or trust edits were made. |
| QA-005 stderr recovery | Not performed | No stdout/stderr artifact exists for that run; no raw output was available to inspect. |
| Native provider launch | Not performed | Explicitly outside this follow-up. |

The exact next native-probe precondition is a successful shell preflight that emits one synthetic capture matching its synthetic session ID, with the capture moved to the separate synthetic artifact folder. Until then, native SessionStart dispatch and ancestry remain unverified; no provider restriction is inferred.
