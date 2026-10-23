# Proposal

## Why

Repository work currently has no persistent separation between planning, implementation, independent QA, and code analysis. Introduce a consistent delegated workflow so the main agent preserves requirements and decisions while specialists perform implementation and supply review evidence with tracked resolution.

## What Changes

- Make the main agent the orchestrator: own OpenSpec planning and tracking, assign bounded work, triage findings, and determine completion from evidence rather than implement changes directly.
- Define developer, QA, and code-analyzer roles for Codex using provider-neutral shared instructions and thin native role definitions. The user explicitly deferred Claude Code repository multiagent configuration and its native/mixed-provider workflow verification because no Claude subscription is active on this machine; future Claude setup will be requested separately. This is a user scope decision, not a provider restriction or a change to Bell Tower product parity.
- Assign all implementation changes, including test sources, configuration, documentation, and review fixes, to the developer. Require independent QA and code analysis for every implementation, with depth proportional to scope.
- Restrict QA and analyzer repository edits to their own reports and supporting evidence in the selected OpenSpec change. QA may capture applicable screenshots in that change's `screenshots/` directory.
- Store numbered reports in `reports/` and an orchestrator-owned findings ledger in `reports/findings.md`. Preserve findings, review history, triage rationale, assignments, and verification evidence across rounds and archive.
- Delegate clear, in-scope fixes back to the developer without asking for routine approval. Investigate uncertain fixes and escalate unresolved scope, behavior, or architectural decisions to the user with evidence and options.
- Require re-verification after fixes and honest reporting of unperformed tests, provider coverage, and evidence limitations.

## Capabilities

### New Capabilities

- `repository-agent-workflow`: Codex orchestration, role boundaries, change-local review evidence, finding triage and verification, and concurrent Codex workflow acceptance, with Claude repository multiagent setup explicitly user-deferred.

### Modified Capabilities

None. Existing Bell Tower session identity, tracking, and attention behavior remains the acceptance baseline for delegated sessions.

## Impact

Implementation will extend `AGENTS.md`, add shared role/workflow instructions and report templates under `.agents/`, native definitions under `.codex/agents/`, and developer-facing workflow documentation. No new `CLAUDE.md` or Claude role adapters are delivered in this scope; preserve existing Claude skills and commands. OpenSpec apply/verify integration must preserve existing planning boundaries and task completion rules; generated skill copies should not become competing policy sources. Reports and optional screenshots live inside each resolved change root. No new product runtime feature, model subscription, external service, release, or migration of existing changes is required.
