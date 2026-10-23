# Repository instructions

## Claude Code and Codex feature parity

Bell Tower is one product with Claude Code and Codex integrations. Maintain feature parity between them by default.

- Every feature, fix, workflow, configuration option and user control must support both clients unless a verified hard provider or platform restriction prevents it. A report originating from one client does not limit the implementation scope to that client.
- Keep identity, checkout resolution, lifecycle, attention, notification and configuration policies shared. Use thin provider adapters for different native events, APIs, history formats and presentation capabilities. Avoid separate product flows or duplicated policy implementations.
- Preserve user data, explicit overrides, safe runtime ownership and active sessions while extending both integrations. Compatibility does not mean freezing one client's feature set.
- Accept an exception only with evidence of a hard restriction. Document the exact operation, provider version and mode, supporting primary documentation or reproducible probe, supported fallback and regression coverage. Missing implementation, fixtures, credentials or investigation is unfinished work, not proof of a restriction.
- Limit exceptions to the affected capability or surface. For example, a native footer styling restriction must not remove shared identity, color controls or worktree association from the rest of the product.
- Include both providers in proposals, specs, designs, tasks and acceptance criteria. Verify matched scenarios for each client and mixed simultaneous sessions; regressions alone are insufficient when adding a feature to the other integration.
- Keep the capability matrix and diagnostics honest about supported, proven-restricted and unfinished or unverified behavior. Treat unexplained parity gaps as incomplete release work.

## Delegated OpenSpec workflow

The repository's native multiagent configuration currently targets Codex. The user explicitly deferred Claude Code configuration and its native/mixed-client workflow verification until a later request because no Claude subscription is available. This is an agreed repository-workflow scope decision, not a proven platform restriction; the Bell Tower product parity policy above remains in effect.

Read `.agents/workflows/openspec-delegation.md` before repository work. The main agent is the orchestrator: it owns OpenSpec planning, assignments, task tracking, finding triage, and completion decisions. Delegate implementation to the developer role and independent review to both QA and code-analyzer roles for every implementation, including small changes. Review depth is proportional to scope.

Specialist subagents follow their assigned role under `.agents/roles/`; they do not become orchestrators or spawn further subagents. The developer owns implementation edits, including test sources, configuration, and documentation. QA and the analyzer only write assigned change-local reports and evidence; QA screenshots belong in that change's `screenshots/` directory. The orchestrator alone maintains the findings ledger and planning artifacts. Clear in-scope fixes return directly to the developer; unresolved decisions are escalated to the user with evidence and options.

Keep the selected OpenSpec skill's authorization boundaries. Exploration and proposal requests never start implementation automatically. If required delegation is unavailable, report the exact limitation and request a decision rather than silently perform the specialist's work in the main agent.
