# Codex headless native hook retry 003

Date: 2026-10-29. Scope: one bounded native Codex CLI 0.165.0 `exec` retry for task 1.1 after the isolated handler preflight was repaired. The reviewer role model is gpt-6-luna; the private Codex configuration selected `gpt-5-codex`, but no model response was received.

## Synthetic handler preflight

Before launching Codex, the unchanged trusted `SessionStart` handler was run directly through its configured PowerShell 7 shell under the elevated context needed for the read-only ancestry query. It used the saved QA-005 child environment, a fresh synthetic session ID, a separate ignored capture directory, and a unique run nonce. The handler exited 0 and wrote exactly one parseable capture. Its event and synthetic session ID matched; capture time followed handler start; the capture recorded the exact PowerShell child PID, creation FILETIME, and image in its ancestry. The capture carried a GUID run nonce and a distinct GUID per-invocation capture nonce. The native capture directory remained empty during this preflight.

## Native attempt

The version-matched Codex executable ran once with `exec --json --sandbox read-only` and a literal prompt prohibiting tools, file access, and changes. The child received only the isolated QA-005 environment key set, including a dummy `OPENAI_API_KEY`; the private model endpoint was loopback-only and closed, and no private auth file existed. The isolated hook enablement was true. Private config, hook definition, handler command, and script hashes stayed unchanged across the run.

The ConPTY helper reached its 25-second bound (helper exit 124) and had recorded the exact launched native Codex PID, creation FILETIME, and image. After cleanup, that exact process was no longer present. The native capture directory contained no new file. Consequently there is no native SessionStart event, session ID, or provider-recorded ancestor chain to compare with the exact launched Codex identity. No detailed CLI error output was retained for further classification.

This is an installed native headless/embedded attempt under elevated execution; it is not shared-daemon evidence and does not establish terminal binding. It does not prove a Codex provider restriction. Current native hook dispatch and ancestry remain unverified.
