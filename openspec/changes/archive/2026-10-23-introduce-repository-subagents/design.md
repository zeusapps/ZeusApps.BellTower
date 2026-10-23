# Design

## Context

See `proposal.md` for the motivation and `specs/repository-agent-workflow/spec.md` for the behavior contract. The repo has a spec-driven OpenSpec root, generated OpenSpec skills under `.agents/skills/` and `.claude/skills/`, and Claude command wrappers. It has no project role definitions under `.claude/agents/` or `.codex/agents/`, no `CLAUDE.md`, and no standard review ledger. `AGENTS.md` already contains provider parity rules and currently exists as user-owned untracked work; preserve its content.

`docs/development.md` and `.github/workflows/validate.yml` define formatting, Release build, .NET tests, Node tests, and packaged acceptance. Existing archived changes retain `evidence/` notes, but do not provide standardized review rounds or finding ownership. Existing live acceptance and compatibility documents distinguish fixtures from observed installed behavior. The workflow must keep that distinction.

This design is included because the change introduces a delegated workflow architecture and write-ownership rules spanning persistent guidance, role definitions, and OpenSpec integration. During apply, the user explicitly narrowed repository multiagent setup to Codex: no active Claude subscription is available and Claude setup will be requested later. Remove this change's newly added Claude adapters/entry point; preserve existing Claude skills/commands and all product parity instructions. This user deferral supersedes the original two-client workflow acceptance scope without claiming a provider restriction.

## Goals / Non-Goals

**Goals:** Put provider-neutral policy in one maintained location; make assignments, report ownership, and resumption explicit; preserve evidence through rechecks and archive; verify native Codex delegation and concurrent role behavior. Keep Claude repository setup explicitly deferred until the user requests it.

**Non-Goals:** Build a scheduler or issue-tracking service, change Bell Tower's runtime session policies, replace OpenSpec schemas, impose model names or personal settings, grant additional permissions, auto-publish, or retrofit reports into historical changes. Reports are evidence, not new schema prerequisites or substitutes for existing OpenSpec completion rules.

## Decisions

### 1. Shared policy with thin client definitions

Use this implementation layout:

```text
AGENTS.md
.agents/
  workflows/openspec-delegation.md
  roles/developer.md
  roles/qa.md
  roles/code-analyzer.md
  templates/qa-report.md
  templates/analysis-report.md
  templates/findings.md
.codex/agents/
  developer.toml
  qa.toml
  code-analyzer.toml
docs/agent-workflow.md
```

Extend `AGENTS.md` with concise delegation rules, shared references, and the explicit Claude repository-workflow deferral. Native Codex definitions instruct each subagent to load its shared role policy and selected change context before acting. Verify those references actually load in the installed Codex client; file existence is insufficient. Do not deliver a new `CLAUDE.md` or Claude role files now. Retain prior discovery attempts as historical evidence only and leave pre-existing Claude skills/commands intact.

The active primary reference is [Codex subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents). Confirm actual supported fields and discovery against the installed client. Preserve inherited model choices and parent permissions. Add project-level Codex role registration only if the installed client requires it, retaining the same shared policy and recording the exact native mechanism. Claude documentation examined earlier does not make its deferred setup an active deliverable.

Alternative: duplicate all policy inside native definitions. Rejected because triage and completion rules would drift and future provider setup would be harder. A new external orchestration framework is unnecessary for three specialist roles.

### 2. Orchestration overlays the existing OpenSpec workflows

The main agent continues to invoke the applicable OpenSpec skill and CLI, resolves context paths from CLI output, and owns planning artifacts, `tasks.md`, and `reports/findings.md`. Implementation instructions are fulfilled by assigning the requested implementation to a developer and collecting results. Do not hand-edit generated OpenSpec skill copies or introduce a parallel task list.

The shared workflow explains planning versus apply boundaries, how apply delegates implementation, how QA uses verify criteria for completeness/correctness/coherence, and how analysis supplements that review. Both reviews are mandatory for every implementation; their depth is proportional. An authorized proposal never starts apply automatically. Routine clear fixes inside authorized apply work require no repeated user approval. Missing delegation capability is surfaced rather than silently replaced with direct main-agent implementation.

Assignments contain change root, task/requirement IDs, objective, acceptance, dependencies, input paths, implementation target, allowed writes, exact report/evidence paths, and expected return format. Subagents return further delegation needs to the orchestrator; they do not establish competing task owners. Developer returns include changed paths, developer checks, limitations, and relevant finding IDs. Reviewers return report paths, finding IDs, and remaining evidence gaps.

Alternative: customize the OpenSpec schema or generated skills to encode the whole organization. Rejected because it adds maintenance and regeneration drift without improving the agreed workflow.

### 3. One implementation writer, independent reviewers

The developer owns source, tests, configuration, and implementation documentation. The orchestrator alone changes planning and the ledger. QA and the analyzer inspect files and run applicable checks; their only maintained repository outputs are their assigned reports and evidence. QA may additionally capture screenshots. They request developer-authored regression tests rather than patch test sources themselves. Reviewers do not use shell tools, formatters, or test commands to bypass the edit boundary.

Permit normal ignored build output and disposable test data from verification. Prefer `scripts/format.ps1 -Verify` over formatting that rewrites sources. Review the working-tree delta before and after reviewer execution to confirm expected outputs. Native tool settings should narrow capabilities where compatible with report creation and functional test execution, while retaining the parent's permissions. A shell-capable reviewer with report-writing access is not automatically subject to an OS-enforced path allowlist. Document the actual behavioral restrictions and any demonstrated native enforcement accurately.

Start with a single developer writer per checkout. QA and analysis may run concurrently after a stable handoff. Parallel implementation requires disjoint ownership or an explicitly prepared isolated checkout; never run competing writers against shared files. Isolated work must use the assigned implementation revision and preserve relevant user/untracked changes rather than assuming a default-branch checkout contains them. Do not terminate user agents or alter production settings to validate this workflow.

### 4. Change-local report rounds and review identity

Resolve the change root through OpenSpec rather than the terminal directory. During apply, use this layout:

```text
<change-root>/
  reports/
    qa-001.md
    analysis-001.md
    findings.md
    qa-002.md
  screenshots/
    qa-001-scenario-description.png
```

The orchestrator reserves each report filename and an unused finding-number range for that assignment before launching reviewers. QA IDs use `QA-001` and analyzer IDs use `ANALYSIS-001`, unique within the change. Rechecks refer to existing IDs; new findings use new reserved IDs. Numbering is per change and role, never reset per session. No empty screenshot folder or placeholder image is required when there is no visual evidence.

Each report records role, client/version/mode, date, assignment, task/spec references, base commit, reviewed implementation identity, commands and outcomes, coverage and limitations, findings, and recheck results. For a dirty checkout, implementation identity includes the diff against the named baseline plus content identities for relevant untracked implementation files. Use existing Git output and standard content hashes, recording the method and scoped paths; do not build a separate revision-tracking tool. Exclude reports, screenshots, and orchestrator tracking edits from that identity so evidence production does not invalidate itself. An unchanged implementation can be referenced by later reports; changed implementation requires affected reviews/checks again, with reasons for any unaffected coverage carried forward.

Reports are historical records. Do not overwrite a previous round to claim its result changed. If a report needs a factual correction, write a linked subsequent report. Use relative links between reports, findings, screenshots, and change artifacts so the entire change can be archived without breaking internal evidence links. Screenshots must show relevant scenario evidence and avoid unrelated private material. Missing capture capability is an explicit limitation, not a reason to create a placeholder.

Alternative: one mutable QA report per change. Rejected because it loses the reviewed target and the distinction between initial failure and verified correction.

### 5. One authoritative findings ledger

`reports/findings.md` contains a summary table and a detail/history section per canonical finding. The table records ID, source report links, task/requirement, severity, blocking disposition, status, owner, and latest verification link. Details retain triage rationale, proposed action, dated transitions, developer handoffs, user decisions, duplicate links, and verification evidence. The orchestrator is the only ledger writer, including when reviewers run concurrently.

Use `critical`, `warning`, and `suggestion` severities, with blocking disposition recorded separately. A suggestion is not automatically a completion blocker; missing required behavior cannot be downgraded away by labeling it a suggestion. Lifecycle states are:

```text
reported --> assigned --> awaiting-verification --> verified
   |                              |
   |                              +--> reported (failed recheck)
   +--> awaiting-user-decision --> assigned / dismissed / deferred
   +--> dismissed / duplicate / deferred
```

Every report finding gets an entry, even if dismissed or duplicated. A duplicate points to the canonical finding and retains its own source evidence. Only the orchestrator changes status; `verified` means closed using independent evidence, not the developer's assertion. Track and re-evaluate unresolved entries on resumption. Optional deferrals retain rationale and a follow-up destination or reconsideration condition. Deferral affecting required scope requires explicit user agreement and coherent OpenSpec updates.

Alternative: let each reviewer track its own resolution state independently. Rejected because it creates conflicting decisions and makes resumed work difficult to reconstruct.

### 6. Fix routing and completion

An obvious fix has a clear expected result in the agreed specs, remains within scope, and needs no unresolved behavior or architectural choice. The orchestrator assigns it directly to the developer with the finding ID and verification expectation. For uncertainty, assign bounded investigation to the appropriate specialist, then ask the user only for the unresolved decision with evidence, options, recommendation, and impact. Continue independent tasks while waiting; elapsed time does not resolve a pending decision.

Developer fixes enter `awaiting-verification`. QA rechecks correctness/functional findings and the analyzer rechecks analysis findings; cross-cutting fixes can require both. Failure reopens the finding. After the last implementation change, require both applicable current review reports, all findings triaged, required acceptance complete, and no unresolved blocking findings. Planning status and reports alone do not prove completion. Update task checkboxes only when their specified behavior and required evidence satisfy the controlling OpenSpec workflow.

### 7. Codex acceptance and preserved product parity

Validate the workflow itself with bounded installed-Codex scenarios: role discovery; developer handoff; zero-finding reports; QA defect and analyzer duplication reports without implementation edits; report-only writes; obvious-fix delegation; recheck success/failure; uncertainty escalation; screenshot evidence when available; and resumption without overwritten reports. Retain versions, mode, commands, target identity, outcomes, and explicit unavailable checks. Do not call a mocked adapter test a native delegation trial. Claude execution and mixed-provider workflow acceptance are explicitly user-deferred rather than failed release gates in this scoped change.

Run concurrent Codex assignments with disjoint report reservations and one ledger owner, covering separate scopes and shared-change review. Verify no report overwrite or cross-change writes, preservation of user edits, independent main-session identity/association, foreground attention during background work, and completion only after relevant background work ends. Existing anchors include `CaptureTests.CustomMainAgentRemainsMainAndSubagentEvidenceIsExcluded`, `OpenSpecSelectionTests`, and the foreground/background `ReducerTests`. These remain product regression evidence for both integrations; native Codex role discovery and execution require their own observed trials. Record the evidence source for session/attention checks accurately, rather than calling fixtures live desktop observations. If a product defect is exposed, report it separately and obtain a scope decision rather than adding unplanned runtime work here.

Use the documented formatting, Release build, .NET and Node checks where implementation paths require them; a guidance-only change does not require unrelated packaging/performance work. Strictly validate this OpenSpec change. Record unavailable native trials as incomplete verification, not hard restrictions. A hard restriction requires exact version/mode, operation, primary documentation or reproducible probe, fallback, and regression evidence.

## Risks / Trade-offs

- [Shared instructions are referenced but not loaded] -> Verify role context in each native client instead of validating filenames only.
- [Review tools can edit files beyond the intended boundary] -> Use explicit shared policy, appropriate native restrictions, and before/after delta checks; distinguish these controls from an unproven filesystem sandbox.
- [Parallel work overwrites reports or the ledger] -> Reserve output paths/IDs centrally and maintain one implementation writer and one ledger owner per shared scope.
- [Reports become stale after fixes] -> Identify the reviewed implementation separately from evidence files and require affected rechecks after edits.
- [Mandatory reviews cause overhead] -> Keep assignments bounded and reviews proportional while preserving both independent perspectives.
- [Unavailable desktop or authenticated provider checks are mistaken for success] -> Preserve unperformed checks as evidence gaps and request only the specific remaining input or environment.

## Migration Plan

1. Developer adds shared policies/templates, native adapters, entry points, and workflow documentation while preserving existing repository instructions and personal configuration.
2. Activate and inspect the roles in fresh or reloaded Codex sessions as required by the installed client. Start the workflow on this change; leave historical change evidence intact, including reports of the original two-provider scope.
3. Execute Codex native and concurrent-role acceptance, collect current QA/analyzer reports, triage findings, and recheck fixes before declaring this implementation complete. Do not require Claude authentication or configure Claude roles in this scope.
4. Apply the shared workflow to future authorized implementations and resumed active work, retaining each change's existing artifacts and user decisions.
5. If rollback is needed, revert only the workflow/adapter additions and restore amended entry-point content. Keep change reports, screenshots, findings history, and unrelated user work. There is no runtime state migration.
