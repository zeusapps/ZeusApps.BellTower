# Native Codex TUI and hook evidence 009

Captured 2026-10-26 with installed Codex CLI 0.164.0. The tests used the interactive TUI under standalone --no-daemon, a tool-owned PTY, private CODEX_HOME and disposable project, and a loopback fake Responses endpoint with a synthetic key. No real model request or external inference was made. The full sanitized event record is in [implementation-009-tui-contract.json](implementation-009-tui-contract.json).

## Plan-mode question tools

The TUI was switched to Plan mode interactively. The fake endpoint's first request contained the built-in request_user_input function and schema, but not request_user_input_async. The blocking control opened Codex's native question UI. Hook capture recorded PreToolUse(request_user_input) with permission_mode, tool_input, tool_use_id and turn_id; the question remained unanswered, so no PostToolUse was expected or observed.

In a separate run under the same TUI mode and fake model, the tool list again contained request_user_input and omitted request_user_input_async. The turn ended through Stop without a PreToolUse for the async tool. Thus request_user_input_async was unavailable in the tested Codex 0.164.0 interactive TUI Plan configuration, with request_user_input as the supported blocking fallback. This is version/mode/configuration evidence, not a claim about other catalogs or versions. The prior offline local catalog probe found no model advertising async experimental tools. A supplementary attempt with catalog slug gpt-6.1-sol and the fake provider yielded an empty tool list, including no blocking control, so it is excluded from this conclusion.

## Standalone permission hook sequence

A separate Plan-mode run used approval_policy=on-request and sandbox_mode=read-only. The fixture model requested a single Set-Content marker in the private project. Codex displayed its native approval prompt, which remained unanswered. No file was created.

The redacted hook sequence, relative to UserPromptSubmit, was:

| Offset | Event | Tool | tool_use_id |
| ---: | --- | --- | --- |
| +366 ms | PreToolUse | Bash | present |
| +703 ms | PermissionRequest | Bash | absent |

PermissionRequest followed PreToolUse by 337 ms. Its keys included permission_mode, tool_input and turn_id, but no tool_use_id. The Notification event was registered in the isolated hook config; none arrived while the request remained pending for 26.7 seconds, until the owned TUI was interrupted. The interruption is not evidence of a human approval or automatic-review resolution.

The native 0.164.0 help output from an isolated CODEX_HOME states that --no-daemon will “Run without the shared background server, even if it is already running.” This supports that the captured standalone invocation did not use the shared background server. It does not establish that all future hook signals are impossible. In this mode, the observed PermissionRequest precedes the native approval UI and does not identify a later human-versus-automatic resolution. No owning-runtime status was available in the standalone capture. Treat the runtime-less silent fallback as the implemented behavior, not a proven provider-wide restriction or completed parity claim.

## Commands, execution and cleanup

The installed CLI help was invoked through its installed Node entry with a private CODEX_HOME. The copied helper under ignored artifacts/permission-provider-009 launched Codex directly with stdio inherited from the tool PTY and windowsHide=true; it did not use cmd start or create a visible window. The sandboxed loopback attempt could not reach the fake endpoint; successful probes were run outside the sandbox after observing that exact failure. Every successful Codex process exited with status 0 and its fixture server closed on child exit. Unanswered native prompts were canceled only in their own PTY. The disposable project contained no created marker file. No user settings, credentials, user sessions or production approvals were used.

Context7 documentation calls used for the separate config syntax lookup confirmed the current Codex enums read-only and on-request. The installed CLI itself accepted both values in the 0.164.0 capture; no documentation source is used to generalize hook behavior beyond the observed version.

## Task disposition recommendation

- 1.1: recommend complete. Existing Claude 2.1.310 auto/default fixtures plus this Codex 0.164.0 Plan capture now retain redacted cross-provider event fields and timing, including the observed absence of a later Notification while the Codex request was pending. Predecessor ingress/lifecycle inspection is recorded separately in resumption-contract-check.md. This remains distinct from live acceptance tasks 3.4 and 4.1.
- 2A.5: recommend complete for the installed 0.164.0 Plan TUI configuration tested here: async was not offered and the ordinary blocking question control was offered. Keep the model/catalog scope explicit; no provider-wide restriction is claimed.
- 4.2: remains partial. The standalone help and hook capture establish the shared-server-disabled mode and pre-decision PermissionRequest behavior, but do not prove that no future supported human-resolution signal exists. The silent fallback remains appropriate for sessions without verified owning-runtime evidence; do not label the gap a hard restriction.

No task checkbox or acceptance is changed in this evidence report.

