# Proposal

## Why

Bell Tower is developed with both Claude Code and Codex, and the product's purpose is to work with both. The repository's delegated OpenSpec workflow (orchestrator plus developer, QA, and code-analyzer subagents) currently has native role adapters only for Codex. Claude Code configuration was explicitly deferred because no Claude subscription was available. A subscription is now available and the user intends to alternate between both clients, so the deferral should end and the workflow should reach parity.

## What Changes

- Add native Claude Code role adapters `.claude/agents/{developer,qa,code-analyzer}.md`. They are thin pointers to the existing shared policy, roles, and templates, matching the Codex TOML adapters.
- Introduce per-role default models in provider adapters: developer and QA default to a cost-efficient model (Claude: `sonnet`), and the code-analyzer defaults to the strongest reasoning model (Claude: `opus`). The orchestrator may raise a single assignment's model and records that choice. Matching Codex per-role defaults are added when the installed Codex client supports a model setting. Otherwise the gap is recorded with evidence.
- Record client, version, mode, **and model** in review reports and ledger assignments so evidence from both clients is distinguishable.
- Define mixed-client operation: concurrent Claude and Codex orchestrators on different changes, hand-off of one change between clients across sessions, and an advisory current-orchestrator marker in `reports/findings.md` to prevent two simultaneous ledger owners on one change.
- Keep `AGENTS.md` as the single entry point for both clients. Claude Code reads it natively when no `CLAUDE.md` exists. Document that any future `CLAUDE.md`/`CLAUDE.local.md` MUST import `@AGENTS.md`, because their presence otherwise stops Claude from reading `AGENTS.md`.
- Remove the Claude deferral wording from `AGENTS.md`, `.agents/workflows/openspec-delegation.md`, `docs/agent-workflow.md`, and the report templates.
- Verify Claude native role discovery and execution, and the mixed-client scenarios, with recorded versions, modes, and models. Probe Claude's native prevention of nested subagents rather than assuming it.
- Harden the shared role definitions. They were originally generated without project targeting, so a review found them generic, heavily duplicated, and costly in context. Make each role self-contained for specialists, with Bell Tower build, test, and format commands, the layer map, provider-parity touchpoints, and live-instance safety rules. Use a fixed short return block. Make the workflow file orchestrator-only, and make `docs/agent-workflow.md` a short how-to instead of a near-copy. Move change-specific trial criteria out of the generic completion rules. Use delegation-safe descriptions.
- Assign commit ownership: specialists never commit. The orchestrator commits verified work after findings are resolved, on a change branch when the checkout is on the default branch.
- Add a small `scripts/review-identity.ps1` helper that prints the review target identity (baseline, scoped path manifest, diff and untracked-file SHA-256) in a fixed format, replacing hand-assembled hashing.
- Existing OpenSpec skills and commands for both clients stay unchanged. Bell Tower product code is unchanged; the product already maps subagent start and stop events for both providers.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repository-agent-workflow`: replace the Codex-only/Claude-deferred provider requirement with a dual-client requirement. Add per-role model selection, client and model identity in reports, mixed-client concurrency and hand-off rules, and orchestrator-only commit ownership.

## Impact

- New files: `.claude/agents/developer.md`, `.claude/agents/qa.md`, `.claude/agents/code-analyzer.md`, `.agents/roles/project-facts.md`, `scripts/review-identity.ps1`.
- Substantially rewritten: `.agents/roles/*.md` (self-contained and project-targeted), `.agents/workflows/openspec-delegation.md` (orchestrator-only), `docs/agent-workflow.md` (short how-to). Codex TOML descriptions and instructions are adjusted to match.
- Possibly modified: `.codex/agents/*.toml` (per-role model defaults, if supported).
- Modified documentation and policy: `AGENTS.md`, `.agents/workflows/openspec-delegation.md`, `.agents/roles/*.md` (only where wording is Codex-specific), `.agents/templates/{qa-report,analysis-report,findings}.md`, `docs/agent-workflow.md`.
- Spec: `openspec/specs/repository-agent-workflow/spec.md`, through this change's delta. The Purpose text needs a matching update at sync or archive time.
- No product source, runtime state, personal settings, or user-level configuration changes. Lower model costs for subagent-heavy work are expected but not measured by this change.
