# Proposal

## Why

`add-claude-repository-subagents` (archived 2026-10-23) shipped the dual-client repository workflow. Its Codex native checks and mixed-client checks were deferred by the user, and they are still unverified. The spec `repository-agent-workflow` already requires them, so this change tracks the verification work.

## What Changes

- Run the deferred Codex native checks: role discovery, the TOML `model` and `model_reasoning_effort` values, and a native Codex review round on a target.
- Run the mixed-client checks: Claude to Codex hand-off of one change, Claude and Codex orchestrators on two different changes at once with Bell Tower attention observed, and the same-change conflict.
- Update or remove the "Documented, not yet natively verified" TOML comments and the matching note in `docs/agent-workflow.md`.
- Correct the scope of the "Known client restrictions" note in `docs/agent-workflow.md`. After the restart in the previous change, Claude QA was also told not to write report files, so the restriction does not cover only the analyzer.
- Record any proven Codex restriction under the AGENTS.md restriction-evidence rules.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This change verifies requirements that already exist, so `skip_specs` is set.

## Impact

- Possibly `.codex/agents/*.toml` (comments, or corrected model values) and `docs/agent-workflow.md`.
- Evidence goes under this change's `reports/`.
- Needs the user to run Codex sessions and observe Bell Tower.
