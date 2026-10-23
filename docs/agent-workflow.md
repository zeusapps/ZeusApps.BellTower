# Repository agents

How to run Bell Tower's delegated OpenSpec workflow from Claude Code or Codex. This page is a how-to; the rules live in the files it links.

| What | Where |
| --- | --- |
| Orchestrator policy (assignment, triage, completion, commits, models, mixed-client) | [.agents/workflows/openspec-delegation.md](../.agents/workflows/openspec-delegation.md) |
| Specialist rules (developer, QA, code analyzer) and the shared [project-facts.md](../.agents/roles/project-facts.md) (commands, layout, parity, safety) | [.agents/roles/](../.agents/roles) |
| Report and ledger formats | [QA](../.agents/templates/qa-report.md), [analysis](../.agents/templates/analysis-report.md), [findings](../.agents/templates/findings.md) |
| Product parity policy and entry point | [AGENTS.md](../AGENTS.md) |
| Build, test, and format conventions | [development.md](development.md) |

## Start and activate

Use the OpenSpec skills: explore or propose for planning, apply for authorized implementation, verify for evidence, and archive only when you ask. Planning never starts apply. For example: `Apply <change> using the developer, QA, and code-analyzer roles.`

| Client | Role definitions | Activate after adding or editing them |
| --- | --- | --- |
| Claude Code | [.claude/agents/](../.claude/agents) | Restart the session, or open `/agents`, then check that all three roles are listed |
| Codex | [.codex/agents/](../.codex/agents) | Start or reload a trusted project session |

Edits to `AGENTS.md` and to the role adapters are read at session start, so restart the session before expecting subagents to see them.

Each definition is a thin adapter that points to its role file, `project-facts.md`, and (for reviewers) its report template. A native role does not prove execution; compare reports for the recorded client, version, mode, and model.

## Models

Defaults are declared only in the adapters ([.claude/agents/](../.claude/agents), [.codex/agents/](../.codex/agents)), so nothing personal needs to change: developer and QA use a cost-efficient model, and the code analyzer uses the strongest reasoning model with high effort. The Codex model and effort defaults are documented but not yet verified natively; the comment beside each value in [.codex/agents/](../.codex/agents) says so. To use a stronger model for one assignment, the orchestrator passes the client's per-invocation model setting and records the reason in the findings ledger. Reports record the resolved model ID.

## Entry point rule for Claude Code

Claude Code reads `AGENTS.md` only while no `CLAUDE.md`, `.claude/CLAUDE.md`, or `CLAUDE.local.md` exists. If you add one, begin it with `@AGENTS.md`, or Claude stops loading the shared policy.

## Switch clients on a change

1. Finish or pause the current orchestrator's assignments. Prefer a stable handoff with no running specialists.
2. In the other client, ask the new orchestrator to resume the change. It reads the `Current orchestrator` block in the change's `reports/findings.md`, the reservations, the latest reports, and the current target.
3. If the block names a different session that may still be active, the new orchestrator asks you to confirm before taking over. The marker is advisory, not a lock.
4. It updates the block, then continues report numbers and finding IDs without reuse. Earlier reports from either client stay as they are.

Two clients can work on different changes at the same time. Do not have two orchestrators edit one change's ledger.

## Targets, commits, and versions

- Identify a dirty working tree with the Review identity command in [project-facts.md](../.agents/roles/project-facts.md). It only reads Git state and prints a manifest and hashes.
- Specialists never commit. The orchestrator commits verified work on a change branch after both reviews, and pushing needs your separate approval.
- Recorded versions: Codex CLI 0.164.0 (custom agent files documented at [Codex subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents)) and Claude Code 2.1.309. Discovery and execution trials are recorded in each change's reports; anything not yet trialled stays unverified.
- Reviewers' write limits and the no-nesting rule are behavioral instructions in the role files. Whether a client enforces them natively is documented only after a recorded probe.

## Known client restrictions

- Claude Code 2.1.309, native `code-analyzer` project subagent, auto mode: its attempt to write the assigned report file under the change's `reports/` directory is refused with "Subagents should return findings as text, not write report files. Include this content in your final response instead." Observed on 3 of 3 analyzer attempts, while QA subagents wrote their reports in the same sessions.
- Fallback: the analyzer returns the report as text and the orchestrator saves it verbatim under the fallback header and ledger record described in the [workflow](../.agents/workflows/openspec-delegation.md#reports-and-evidence). The fallback covers only the refused write.
- Codex has no verified restriction; its native role behavior is not yet verified.
