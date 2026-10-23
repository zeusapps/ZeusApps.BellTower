# Design

## Context

See proposal.md for the motivation. The shared workflow (`.agents/workflows/openspec-delegation.md`, `.agents/roles/*.md`, `.agents/templates/*.md`) is already provider-neutral. Codex consumes it through three thin TOML adapters in `.codex/agents/`. `.claude/agents/` exists but is empty. `.claude/skills/` and `.claude/commands/opsx/` already carry the OpenSpec workflow for Claude.

Facts that shape the approach:

- Claude Code (observed 2.1.309) reads `AGENTS.md` as project instructions when no `CLAUDE.md`, `.claude/CLAUDE.md`, or `CLAUDE.local.md` exists at or above the working directory (code.claude.com/docs/en/memory). This planning session loaded `AGENTS.md` that way.
- Claude Code subagent files are Markdown with YAML frontmatter. Required fields are `name` and `description`. Optional fields include `model` (`sonnet`, `opus`, `haiku`, `fable`, a full ID, or `inherit`), `effort`, `tools`, `disallowedTools`, `hooks`, and `permissionMode` (code.claude.com/docs/en/sub-agents). Model resolution order: per-invocation `model` parameter, then frontmatter `model`, then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main conversation model.
- Project subagent definitions load at session start, so a restart (or `/agents`) is needed after adding them.
- Bell Tower product code already maps `SubagentStart`/`SubagentStop` to background start/end for both providers (`src/BellTower/Providers/HookAdapter.cs`, `src/BellTower/Setup/ProviderSettingsEditor.cs`). Mixed-client attention behavior therefore needs verification, not product changes.

## Goals / Non-Goals

**Goals:**
- Claude role adapters that are as thin as the Codex ones and point at the same shared files.
- Versioned per-role model defaults that lower subagent cost without personal configuration.
- Reports and ledgers that carry enough client and model identity to mix evidence from both clients in one change.
- Recorded native and mixed-client trials.

**Non-Goals:**
- Path-level write enforcement for reviewers (for example, a PreToolUse hook that limits `Write` to `reports/` and `screenshots/`). Bash would still bypass it, and the existing policy already forbids claiming unproven enforcement. It can be a future change.
- A lock mechanism for ledger ownership. The current-orchestrator marker is advisory.
- Changes to OpenSpec skills or commands, Bell Tower product code, or personal/user-level settings.
- Cost measurement. Savings are expected, not measured.

## Decisions

### D1. Claude adapters mirror the Codex TOMLs

Each `.claude/agents/<role>.md` contains frontmatter (`name`, a delegation-safe `description` per D11, `model`) and a short body. The body tells the specialist to follow `.agents/roles/<role>.md` and, for reviewers, its report template (D7). Ownership, no-nesting, and return rules live in the role file, not the adapter. No `tools`, `permissionMode`, or `hooks` overrides, so permissions are inherited from the parent as with Codex.

*Alternative:* restrict reviewer `tools` (drop `Edit`). Rejected for now. `Write` and Bash remain, so it adds little real restriction and creates a parity asymmetry that needs explaining.

### D2. Per-role models live in adapters

| Role | Claude Code | Codex |
| --- | --- | --- |
| developer | `model: sonnet` | cost-efficient model key in TOML, if supported |
| qa | `model: sonnet` | cost-efficient model key in TOML, if supported |
| code-analyzer | `model: opus` | strongest reasoning model key in TOML, if supported |
| orchestrator | the user's session model | the user's session model |

The analyzer also runs at high reasoning effort on both clients: Claude `effort: high`, Codex `model_reasoning_effort = "high"`. The user decided this on 2026-10-23 (ANALYSIS-014). The analyzer stays on the strongest model because duplication, architecture, and simplification judgment benefits most from deeper reasoning, while developer and QA consume most tokens. The orchestrator raises a single assignment with Claude's per-invocation `model` parameter (or the Codex equivalent) and records why in the assignment row. The shared policy's "inherit model settings" wording becomes "use the role's declared default unless the assignment records an override".

*Alternatives:* `CLAUDE_CODE_SUBAGENT_MODEL` in settings. Rejected because it is personal or untracked configuration, and repository policy forbids changing personal settings. All-`inherit` was rejected because it gives no cost benefit.

Whether Codex 0.164.0+ agent TOML accepts a `model` key is unverified. The developer checks the Codex documentation and a native probe. Without support, the result is recorded under the restriction rules and the Codex roles keep inheriting.

### D3. No CLAUDE.md, with a documented import rule

`AGENTS.md` remains the only entry point. `docs/agent-workflow.md` states that any future `CLAUDE.md`/`.claude/CLAUDE.md`/`CLAUDE.local.md` must begin with `@AGENTS.md`, because these files otherwise silently replace `AGENTS.md` for Claude. Under D7, adapters do not tell specialists to read `AGENTS.md`. Native Claude subagents were observed to start with `AGENTS.md` loaded (AN-R1, 2.1.309), and task 4.4 confirms this formally.

*Alternative:* add `CLAUDE.md` containing only `@AGENTS.md`. Rejected because it adds a second file with no current benefit, and it makes the import-only invariant something to maintain.

### D4. Client and model identity in templates and ledger

QA and analysis templates change `Client / version / mode` to `Client / version / mode / model`. The findings template's assignments table gains a `Client / model` column and a `Current orchestrator` block (client, session identity, since UTC, previous owner). Remove the Codex-only/Claude-deferred sentence in the QA template.

### D5. Bootstrap order for implementing this change

The native Claude `developer` role does not exist until this change is implemented, and project subagents load only at session start. Implementation therefore follows this order:

```
1. Developer step: Claude general-purpose subagent instructed to load
   .agents/roles/developer.md (records that the native role was unavailable).
2. Restart the Claude session so .claude/agents/* is discovered; confirm via /agents.
3. Native Claude QA (sonnet) + code-analyzer (opus) review the change.
   Doubles as Claude discovery/execution trial evidence.
4. Fixes go to the now-native Claude developer role.
5. Codex session resumes the change (hand-off trial) and runs its native roles.
6. Parallel trial: Claude and Codex orchestrators on two different changes.
```

Steps 5–6 need user participation to run Codex and observe Bell Tower attention. QA records them as installed-native or live observations, not fixtures.

### D6. Nested-delegation probe

The Claude docs indicate subagents cannot launch other subagents. This is not assumed. QA runs one probe (a role asked to delegate) on the recorded version and documents "natively prevented" or "instruction only" for each client.

### D7. Role definitions are self-contained and project-targeted

A planning-time review (2026-10-23, findings R1-R12 below) found the Codex-generated roles generic and duplicated. Each specialist was told to read `AGENTS.md`, which is already auto-loaded, plus the full 11 KB workflow, which is mostly orchestrator policy. That is roughly 15-20 KB of instructions per launch, and it undercuts the D2 savings.

The new information layout:

```
 AGENTS.md                      product parity policy + "orchestrator: read the workflow"
 .agents/workflows/...md        ORCHESTRATOR ONLY: assignment, reservation, triage, ledger,
                                completion, commits, mixed-client hand-off
 .agents/roles/<role>.md        SPECIALIST: mission, ownership/write limits, role checks,
                                return block
 .agents/roles/project-facts.md SPECIALIST, shared by all roles: commands, layer map,
                                parity touchpoints, live-instance safety (ANALYSIS-011)
 .agents/templates/<report>.md  reviewer output format (QA / analyzer only)
 .claude/agents/*.md            frontmatter + "follow .agents/roles/<role>.md and your template"
 .codex/agents/*.toml           same pointer
 docs/agent-workflow.md         short human how-to linking the above (no policy copy)
```

Each rule lives in exactly one place. Adapters no longer tell specialists to read `AGENTS.md` (Claude loads it into subagents by default; task 4.4 confirms, and if it does not, the role file repeats only the parity paragraph) or the workflow file.

Shared facts live once in `.agents/roles/project-facts.md`, and each adapter points to the role file plus that file. The user chose this over byte-identical copies on 2026-10-23 (ANALYSIS-011) after AN-R1 found that the per-role copies had already drifted (ANALYSIS-010). The facts file contains:
- Build and test: `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --no-restore`, `node --test vscode/service.test.js vscode/focus.test.js`. Formatting is `./scripts/format.ps1` for the developer and `./scripts/format.ps1 -Verify` for reviewers.
- Layer map from `docs/development.md` (`Core`, `IO`, `Providers`, `Sessions`, `Hosts`, `Tray`, `Setup`, `Integration/BellTower.psm1`, `Diagnostics`) and the projects under `src/` and `tests/`. Link to `docs/development.md` rather than copy its rationale.
- Parity touchpoints: provider-specific behavior belongs in provider adapters (`Providers`, `Setup/ProviderSettingsEditor`, `Integration/BellTower.psm1`, `Sessions/*Name*`) while shared policy stays in `Core`. Product work needs matched Claude and Codex fixtures, and QA states per check which client(s) it covers.
- Live-instance safety: never run `setup --desktop --apply`, `exit`, `toggle-voice`, or test sounds against the live install. Never use the default `%LOCALAPPDATA%\ZeusApps.BellTower`; always pass `--data-dir` or `BELLTOWER_DATA_DIR`. Never stop the tray app or user agent sessions, and never edit provider settings in the user profile.

Findings addressed: R1, R2, R3, R6, R7.

### D8. Fixed return block

Specialists end with a short fixed block (about 15 lines). Details stay in the report or handoff file, which keeps the orchestrator's context small (R9):

```
status: done | blocked | partial
role / client / model: ...
task / finding IDs: ...
changed or report paths: ...
checks: <command> -> passed|failed|not run (reason)
target identity: <review-identity.ps1 summary line>
limitations / decisions needed: ...
next: ...
```

### D9. Orchestrator owns commits

Specialists never run `git commit`, `push`, branch creation, or history rewrites. After the completion criteria hold (current reports from both reviewers, every finding triaged, tasks reconciled), the orchestrator commits the verified implementation together with its change artifacts. When the checkout is on the default branch, it first creates a change branch. Push and publication still need their own user authorization. Until the commit, the review identity remains the dirty-tree identity from D10. This resolves R4.

*Alternative:* a developer commit per handoff, which gives a simpler SHA identity. Rejected by the user in favor of keeping history clean and commits behind verification.

### D10. `scripts/review-identity.ps1`

A small read-only script wraps the identity procedure that the workflow already defines. It requires PowerShell 7. The documented invocation is `./scripts/review-identity.ps1 -Baseline <commit> -Paths <path> [<path> ...]`, and comma-joined and positional forms are equivalent. Paths resolve relative to the caller and must lie inside the repository; they are matched literally and normalized to sorted repository-root-relative form. A path that matches nothing in the working tree, the index, or the baseline fails the run. The hashed diff pins `--no-renames --no-color --no-ext-diff --no-textconv --src-prefix=a/ --dst-prefix=b/`, so user git configuration cannot change the bytes. Output goes to the pipeline so before and after runs can be compared with `Compare-Object` (DEV-002 fixes for QA-001 and ANALYSIS-002 to 006). Given a baseline and explicit scoped paths, it prints `git rev-parse HEAD`, a sorted manifest with added and deleted paths, the SHA-256 of `git diff --binary <baseline> -- <paths>` (exact bytes), and the SHA-256 of each relevant untracked file, plus a one-line summary for the return block. Reviewers run it before and after their review and compare. It stores nothing and tracks nothing. The workflow's "do not build a separate tracking tool" wording is clarified to permit this stateless helper. The developer adds a small test in a disposable Git repository (in `tests/` or a Pester-free PowerShell check) that verifies stable output and that it detects tracked, staged, untracked, and deleted changes. This resolves R8.

### D11. Smaller adjustments

- The generic completion section of the workflow drops the trial criteria left over from the previous change ("Codex concurrent-session scenarios, report reservations, ..."). Those belong to this change's acceptance only (R5).
- Adapter descriptions are delegation-safe. Developer: "Use only for an authorized OpenSpec apply task or an approved fix assigned by the orchestrator." Reviewers: "...only for an orchestrator-assigned review of a stable implementation target." This keeps planning and explore sessions from delegating automatically (R11).
- Claude adapters MUST NOT set `isolation: worktree`, because it branches from the default branch and would drop dirty or untracked user work (R10).
- **Proven restriction (ORCH-001): Claude Code refuses native analyzer report writes.**
  - Operation: the `Write` tool creating `reports/analysis-001.md` from the native `code-analyzer` subagent, Claude Code 2.1.309, auto mode, claude-opus-5-5.
  - Evidence: two refusals on 2026-10-23 with "Subagents should return findings as text, not write report files. Include this content in your final response instead." The second came after a project `Edit(...)` allow rule was added, and that rule was later removed because it had no effect. No local hook or plugin contains the text. Native QA report writes succeeded twice in the same session.
  - Fallback, user-approved 2026-10-23: a reviewer tries its own report write once. If the client refuses it, the reviewer returns the full report text. The orchestrator then saves it verbatim at the reserved path, under a header naming the refusal evidence, and records it in the ledger.
  - Scope: only that reviewer report write. Codex reviewers and Claude QA keep writing their own reports. Regression coverage: each later analyzer round records whether its write succeeded or the fallback was used.
- For docs-only or config-only targets, the orchestrator may run the analyzer with a `sonnet` per-assignment override. Both reviews remain required (R12).

### D12. Bounded, pragmatic review loop

The user requested this on 2026-10-23 after this change ran several fix-and-recheck rounds that were driven largely by wording suggestions. The rules are a severity gate, narrow rechecks, no wording-only findings, light review for docs-only or config-only targets, and at most two fix cycles before escalating to the user (spec "Bounded, pragmatic review loop"). They live in the workflow (orchestrator side) and in the reviewer roles (reviewer side), so they apply to both clients. This change itself switches to them for its final round.

## Risks / Trade-offs

- [Sonnet developer or QA misses issues Opus would catch] -> The analyzer remains on Opus, the orchestrator can raise individual assignments, and the per-report model record makes quality comparisons possible later.
- [The `opus` alias resolves to different versions over time] -> Reports record the resolved model ID, not only the alias.
- [A contributor adds `CLAUDE.md` and Claude silently stops reading `AGENTS.md`] -> Documented import rule, plus the task 4.4 entry-point check.
- [The advisory marker is ignored and two orchestrators edit one ledger] -> The marker check is required in the shared policy, and the conflict scenario requires asking the user. A lock is out of scope.
- [Codex lacks per-role model support] -> The parity gap is limited to cost tuning and documented with evidence. Roles still function by inheritance.
- [Specialists that no longer read the full workflow miss a rule they need] -> Role files carry every specialist-facing rule; QA verifies a rule-coverage mapping from the workflow to roles.
- [Project facts in roles drift from `docs/development.md`] -> Roles list commands and paths only and link to the rationale; analyzer checks consistency when either changes.
- [Orchestrator commits on the wrong branch or includes unrelated user changes] -> Commit only the scoped manifest paths plus change artifacts, branch first when on the default branch, never push without authorization.
- [Mixed-client trials need manual coordination] -> Tasks name them explicitly, and missing trials stay unverified rather than passed.

## Migration Plan

All edits are additive or documentation-only. There is no runtime state migration. Rollback means deleting `.claude/agents/*.md`, reverting the policy, documentation, template, and any TOML model lines, and keeping all reports and ledger history. At archive time the orchestrator also updates the Purpose paragraph of `openspec/specs/repository-agent-workflow/spec.md`, which still describes a Codex-only workflow with Claude deferred, because a delta cannot change Purpose.

## Open Questions

- Exact Codex model identifiers for the developer/QA and analyzer defaults. These are chosen during implementation from the installed Codex client's documented options, under D2's tiering.
