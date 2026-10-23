# Tasks

Carried over from the archived `2026-10-23-add-claude-repository-subagents` change (tasks 1.2 Codex discovery and 4.1–4.3). Follow `.agents/workflows/openspec-delegation.md` and its bounded review loop.

## 1. Codex native checks

- [ ] 1.1 Codex orchestrator and user: in a fresh Codex session at the repository root, confirm that Codex discovers `developer`, `qa`, and `code-analyzer` from `.codex/agents/`, and that a Codex specialist quotes the current first sentence of the `AGENTS.md` "Delegated OpenSpec workflow" section. Verify by recording the Codex version, mode, and quote in a QA report.
- [ ] 1.2 Codex QA: confirm that Codex accepts the TOML `model` and `model_reasoning_effort` values for each role, or record the exact error and fallback as restriction evidence. Verify the resolved model per role in the report.
- [ ] 1.3 Developer: update or remove the "not yet natively verified" TOML comments and the matching `docs/agent-workflow.md` note to match 1.2. Correct the "Known client restrictions" scope to cover any Claude reviewer, not only the analyzer. Verify by grep and a light review.

## 2. Mixed-client checks

- [ ] 2.1 Codex orchestrator and user: hand a change from Claude to Codex (or back), run one native review round, and update the current-orchestrator marker. Verify that report numbers and finding IDs continue without reuse and that earlier reports are preserved.
- [ ] 2.2 QA and user: run a Claude Code orchestrator and a Codex orchestrator at the same time on two different changes, each delegating to subagents. Verify there are no cross-change writes and that Bell Tower keeps each session's identity, OpenSpec association, and attention distinct, with completion suppressed until that session's subagents finish. Label live observations.
- [ ] 2.3 QA: start a second orchestrator on a change whose marker names another active session. Verify it asks the user before taking over the ledger.
