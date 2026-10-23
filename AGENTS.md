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

The main agent is the orchestrator, in either Claude Code or Codex: read `.agents/workflows/openspec-delegation.md` before repository work, delegate implementation to the developer role, and have both QA and code-analyzer roles review every implementation. Specialist subagents (`.claude/agents/`, `.codex/agents/`) follow their role file under `.agents/roles/` and `.agents/roles/project-facts.md`, and do not read the workflow. Keep the selected OpenSpec skill's authorization boundaries: exploration and proposal requests never start implementation automatically, and if required delegation is unavailable, report the exact limitation and request a decision rather than silently perform the specialist's work in the main agent.
