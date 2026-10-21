# Handoff: Deferred Integration Ideas

## Status and Scope

These notes were requested during scope narrowing on 2026-10-21. They preserve options for a later discussion; they are not requirements, selected designs, implementation tasks, or authorization to build integrations.

The current change remains a standalone Windows session-attention tool: Claude/Codex observation, the attention strip, tray overview, app-owned names and colors, Git context, and the hooks/terminal bridges needed to operate in VS Code and Windows Terminal. Naming and OpenSpec association use explicit user choices through the tray or CLI.

WorkLog at D:/Code/ZeusApps.WorkLog is a reference for a small independently runnable .NET helper with responsibility-based organization and isolated tests. Bell Tower does not depend on its executable, database, MCP server, configuration, or source projects.

## Ideas for Later Discussion

| Candidate | Potential value | Decision needed later |
| --- | --- | --- |
| Bell Tower MCP identity tools | Let Claude/Codex supply a short session name and current change as work shifts | Whether an agent-facing tool is needed, how calls identify the correct running attachment, and when user-selected names take precedence |
| Managed private agent instructions | Ask agents to update identity after choosing or switching work, following WorkLog's private instruction pattern | Whether best-effort agent behavior is acceptable and how instructions are installed, owned, refreshed, and removed |
| WorkLog metadata coordination | Use consistent repository/change references when a user already runs both tools | Whether any actual connection is useful; preserve independent operation and avoid reading or sharing journals implicitly |
| Shared integration helpers | Reuse configuration-preserving setup, ownership tracking, Git worktree handling, and atomic file replacement | Whether duplication justifies a common package; avoid creating a shared runtime or coupling product releases prematurely |
| Per-repository initialization | Offer a WorkLog-like init command with repository/client selection, dry-run output, ownership records, backups, and worktree-aware private Git exclusions | Whether repository scoping adds value beyond the basic provider/terminal setup needed by Bell Tower |
| Release and update automation | Versioned self-contained Windows ZIP/checksum, CI publication, release discovery, explicit check/install/status, staged replacement and recovery | Which distribution features are needed, the release source, network policy, and how to coordinate Bell Tower's tray shutdown without touching agents |

## WorkLog Evidence to Revisit

The local WorkLog checkout was read without modification. Relevant entry points:

- D:/Code/ZeusApps.WorkLog/docs/architecture.md: one executable project, folders by responsibility, integration planning, and isolated tests.
- D:/Code/ZeusApps.WorkLog/docs/installation.md: independent installation/data directories, private client integration, ownership manifests, backup and conflict handling.
- D:/Code/ZeusApps.WorkLog/src/WorkLog/Integration/RepositoryInitializer.cs and AgentInstructions.cs: plan/apply setup and best-effort agent tool instructions.
- D:/Code/ZeusApps.WorkLog/src/WorkLog/Integration/Clients/: separate Claude/Codex configuration adapters.
- D:/Code/ZeusApps.WorkLog/docs/releases.md and src/WorkLog/Updates/: validated packaging, explicit updates, staging, status, and recovery.

These are implementation references from the inspected checkout, not public API commitments or a dependency declaration. Recheck the source before reusing any code.

## Boundaries to Preserve

- Hook/host adapters needed for tracking, display, and focus are part of the current change; future cross-helper integrations are separate.
- Do not add MCP registrations, managed agent instructions, journal synchronization, shared packages, release-feed polling, or an updater to the current implementation.
- A Bell Tower wait or completion notification does not establish that work is complete for WorkLog reporting.
- Current naming must remain usable without OpenSpec or an agent calling a tool.
- Current setup stays explicit and reversible and preserves unrelated client settings and trust policies.
- Manual executable replacement stops Bell Tower only. User agent sessions and terminal hosts remain untouched.
- Any later integration needs its own scope decision and corresponding planning update before implementation. Teams notifications remain separately deferred as discussed in the original PoC.
