# Provider contract check during Codex resumption

Checked 2026-10-26 through the repository-required Context7 CLI using the find-docs skill. This is documentation evidence, not native execution or live acceptance.

Commands:

```text
npx ctx7@latest library "Claude Code" "Notification permission_prompt hook delay after six seconds inactivity CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS"
npx ctx7@latest docs /websites/code_claude "Notification permission_prompt hook timing six seconds idle typing and CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS disabling prompt notification"
```

The official documentation index was selected for its exact product match, official source, broader snippet coverage and higher benchmark score. No version-specific entry for the captured Claude 2.1.310 was returned. These are current documentation claims, not proof of all behavior in that version.

## Contract distinctions

- [Notification hooks](https://code.claude.com/docs/en/hooks#notification): terminal permission notifications follow about six seconds of no typing; typing defers them. Answering before the delay suppresses the notification.
- The same page distinguishes SDK callback-hosted sessions: permission notifications arrive about six seconds after the request, without typing deferral. Before 2.1.233, that surface did not emit them.
- The page also says a queued permission or elicitation request can notify while another dialog is open. The existing sequential main-agent capture does not prove that all provider operations are sequential.
- [Environment-variable reference](https://code.claude.com/docs/en/env-vars): `CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS=1` disables permission notifications for sessions hosted through the Agent SDK permission callback, including Claude Desktop and the VS Code extension. It requires 2.1.233 or later and does not affect terminal sessions.

## Review implications

The roughly six-second source delay is supported by documentation and retained captures. The disabling setting must be scoped to its documented host surface; it does not establish why a terminal session lacks a notification. Missing observed prompts remain unverified coverage. Both final reviewers received these distinctions for the disabled-notification part of ANALYSIS-004 and correlation regression review.

No provider settings, real dialogs, installed application, approvals or audio were used in this check.

## Required predecessor evidence

The orchestrator inspected the archived predecessor completion evidence during resumption:

- [Fast-ingress verification](../../archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/verification.md) retains capture-v1, mixed-provider ordering/delivery, crash replay and packaged lifecycle results, plus actual native ingress benchmarks. Its 332-test result is historical; current QA 004 passed 971 tests including the inherited ordering suites.
- [Liveness/title-refresh ledger](../../archive/2026-10-23-reconcile-session-liveness-and-title-refresh/reports/findings.md#completion-assessment) records independently reviewed ownership-generation replacement, attachment-health compatibility, isolated package/restart acceptance and completed predecessor tasks. Current QA 004 passed the inherited attachment-health suites. Prior host/native acceptance limitations remain historical limitations, not new passes.
- Current permission-specific source captures remain in this change's `claude-askuserquestion-hooks.json`, `codex-request-user-input-hooks.json` and `implementation-001-2a5-*.json`. Their bounded modes/versions are unchanged; Plan-mode async and the current feature's live auto-versus-human trials remain unverified.

This closes the predecessor-document inspection part of task 1.1; it does not extend native coverage beyond the named captures or waive tasks 2A.5, 3.4, 4.1 or 4.2.
