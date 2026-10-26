# Provider coverage evidence check 008

Captured 2026-10-26 for the reviewed Codex CLI 0.164.0 candidate. This is a bounded evidence audit; it does not change implementation or acceptance status.

## Task 1.1: predecessor coverage versus current permission hook evidence

The predecessor inspection recorded in [resumption-contract-check.md](resumption-contract-check.md) identifies the archived reliable-ingress ordering/delivery, crash replay, attachment-health ownership/lifecycle, and restart evidence, and notes inherited ordering and attachment-health suites passed in the current review cycle. This addresses the predecessor-inspection portion of 1.1. It is separate from current-feature native/live acceptance in 3.4 and 4.1.

Current retained hook captures prove the Claude 2.1.310 default and auto AskUserQuestion sequences: PreToolUse includes tool_use_id; PermissionRequest follows at about 125–127 ms without that ID and has matching tool input/prompt identity; Notification(permission_prompt) follows at about 6.2 s with prompt_id but no tool fields. Codex 0.164.0 TUI captures show PreToolUse/PostToolUse for request_user_input; they show no PermissionRequest or Claude-style permission_prompt for that question operation. The retained Codex permission-approval comparison is not a matched PermissionRequest versus notification fixture with timing. Therefore the predecessor portion is evidenced, but the complete literal 1.1 field/timing comparison across both providers is not closed by these records. Do not infer a Codex event contract from Claude or from question-tool hooks.

## Task 2A.5: Plan-mode TUI feasibility

Previously retained evidence establishes:
- Claude CLI 2.1.310, interactive TUI, --permission-mode default: actual AskUserQuestion hook sequence and timing in implementation-001-2a5-claude-askuserquestion-default-mode.json.
- Codex CLI 0.164.0, --no-daemon, Default mode: request_user_input_async absent from the tool list in implementation-001-2a5-codex-request-user-input-async-not-offered.json.
- Codex CLI 0.164.0 app-server, Plan mode, fake endpoint: ordinary request_user_input was offered and its positive control raised item/tool/requestUserInput; async was not offered (implementation-005-async-plan.md).
- The bundled local model catalog returned no async-capable experimentalSupportedTools entry (implementation-006-async-catalog.md).

I inspected the existing ignored terminal probe helper. It starts Codex in a separate visible console (cmd start; windowsHide: false) and does not provide a hidden pseudoterminal. No existing artifact supplies an isolated ConPTY/PTY runner. Running the helper would create a visible window and require typing /plan into it; the assignment disallows visible windows and interaction with real sessions. I did not launch it or build a console framework. Thus the exact remaining gap is the installed Codex 0.164.0 interactive TUI's Plan-mode request_user_input_async tool list/schema. App-server evidence and the local catalog are not substitutes for that TUI observation. Task 2A.5 remains partial; the app-server Plan capture only proves the tested configuration.

## Task 4.2: standalone no-daemon signal and parity audit

The retained 0.164.0 standalone --no-daemon captures exercise question tools, not an actual permission-requiring command in both automatic and human-review cases. They show no PermissionRequest event for request_user_input, but that question-tool result does not prove whether a standalone permission operation has a human-versus-automatic signal.

The current Codex hook implementation documentation retrieved through Context7 describes PermissionRequest as running before guardian/automatic review or user approval. Its approval path distinguishes hook, guardian and user resolution internally, but the cited contract does not establish that a standalone hook payload exposes which later resolution occurred. The retrieved source is current mainline documentation, not a version-pinned 0.164.0 contract; it cannot fill the installed-version native capture gap.

Existing implementation and compatibility records document shared setup hooks and shared fallback wording/diagnostics. Claude has the observed permission_prompt signal; Codex sessions without an owning verified runtime are explicitly classified as unavailable/unverified and retain a silent chip without sound, reminder, or escalation. The repository records this as an unfinished parity gap, not a hard provider restriction. The available evidence does not establish that Codex --no-daemon can never expose another supported signal, and it does not establish live auto-versus-human behavior.

Context7 commands used for the hook-contract question:

```text
npx ctx7@latest library "Codex CLI" "0.164.0 --no-daemon hook events permission approval human versus automatic approval signal"
npx ctx7@latest docs /openai/codex "Codex CLI hook event contract for standalone --no-daemon: whether PermissionRequest or another event distinguishes human approval from automatic approval; supported event names and fields"
```

The documentation returned approval-precedence source links in the OpenAI Codex repository (current mainline); it did not return a version-pinned 0.164.0 hook schema. No native permission trial was run because the existing terminal harness creates a visible window. No user settings, credentials, live sessions, dialogs, approvals, installation, or product processes were touched.

## Disposition

- 1.1: predecessor-document inspection is complete; cross-provider permission field/timing proof is still incomplete. This gap is distinct from live acceptance tasks 3.4 and 4.1.
- 2A.5: partial; app-server Plan and local-catalog evidence added earlier, but matching TUI Plan-mode async availability remains unverified.
- 4.2: partial; silent fallback and diagnostics are documented, while standalone Codex human-versus-auto signal availability remains unverified. No hard restriction is proven.

No task checkbox or acceptance claim is changed by this evidence note.

