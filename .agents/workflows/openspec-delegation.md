# Orchestrator workflow for delegated OpenSpec work

This policy is for the orchestrator, the main agent in either Claude Code or Codex. Specialists (developer, QA, code analyzer) do not read it: each follows its role file and `project-facts.md` in `.agents/roles/`, with the report template for reviewers. Each rule lives in one place; this file owns assignment, triage, completion, commits, model selection, and mixed-client operation.

## Planning and authorization

- Invoke the applicable OpenSpec skill and preserve its authorization boundaries, CLI-controlled state, context, and completion criteria. Use CLI-resolved change and context paths; never guess from the only active change or the current directory.
- The orchestrator owns exploration, proposal/design/spec artifacts, `tasks.md`, assignments, `reports/findings.md`, user communication, commits, and final status. Planning requests stay planning-only. An apply request authorizes developer work within that change's agreed scope.
- Delegate all implementation edits to the developer, including sources, test sources, configuration, documentation, and fixes. Never implement a convenient fallback inline. If delegation cannot run, describe the exact client, version, and mode limitation, and ask how to proceed while completing independent authorized work.
- Do not edit generated OpenSpec skill copies or add a competing task tracker. Fulfill apply through developer assignments and verify's completeness, correctness, and coherence criteria through independent QA evidence.
- Preserve user changes, explicit overrides, current sessions, and unrelated active changes. Do not change personal configuration, stop user processes, publish, or archive merely to complete a task.

## Assignment and reservation

Before launching a specialist, supply:

| Field | Required content |
| --- | --- |
| Role and objective | Developer, QA, or code-analyzer; bounded outcome |
| Scope | Absolute resolved change root; task and requirement IDs; relevant context paths |
| Acceptance | Expected behavior, dependencies, and the applicable Claude Code and Codex product cases and concurrent-session cases |
| Review target | Baseline commit and the output of `scripts/review-identity.ps1` for the scoped paths, including relevant dirty or untracked files |
| Ownership | Permitted implementation paths, or exact reserved report and evidence paths; explicit prohibited writes |
| Finding allocation | Existing IDs to recheck and an unused ID range for new findings |
| Model | The role default, or the override and its reason (see Model selection) |
| Return | The fixed block defined in the role file, plus report paths |

- Reserve report filenames and finding IDs in `reports/findings.md` before concurrent reviewers start. Number reports per role and change, continuing across sessions and clients. Reserve `QA-001` and `ANALYSIS-001` finding IDs independently of report numbers, and never reuse an ID or an allocation after interruption.
- Use one developer writer per shared scope or checkout and one ledger owner per change. Parallel developers need disjoint ownership or an isolated checkout that contains the assigned revision and the relevant user and untracked work. Do not assume a default-branch worktree holds the current implementation.
- Every implementation receives both QA and code analysis, even a small documentation or configuration fix. Scale depth to the affected behavior. Start reviews on a stable developer handoff; QA and analysis may run concurrently on distinct reserved outputs.
- Subagents cannot take over orchestration. Route their additional delegation needs through yourself, and never let a specialist's inspection select another main session's change.

## Review target identity

Run the Review identity command from [project facts](../roles/project-facts.md) for a dirty-tree target. It is a stateless read-only wrapper over standard Git output and SHA-256 hashes, not a tracking tool, and stores nothing. Record its output in the assignment. Check that each reviewer's report records matching before and after identities, and treat any difference as an unexpected edit. Do not claim an enforcement mechanism that was not demonstrated; tool access does not make report-only scope an OS-enforced sandbox.

Exclude reports, screenshots, `tasks.md`, and other orchestrator tracking edits from the implementation identity; never exclude an actual implementation file because it is untracked. Later implementation changes require affected checks and reviews again. An older report is historical evidence, not current approval; carry forward unaffected coverage only with explicit rationale and target mapping. On resumption read the marker, ledger, reservations, latest reports, and current target before assigning work.

## Reports and evidence

Reviewers use `.agents/templates/qa-report.md`, `analysis-report.md`, and you use `findings.md`. Review evidence lives in the resolved change:

```text
<change-root>/
  reports/qa-001.md
  reports/analysis-001.md
  reports/findings.md
  screenshots/qa-001-scenario-description.png
```

If a reviewer returns its report as text because the client refused its write, save that text verbatim at the reserved path under a header naming the reviewer, client, version, mode, model, and the exact refusal message, and record the fallback in the ledger. The fallback covers only the refused write; reviewers that wrote their own reports need no header. Proven restriction: Claude Code 2.1.309 refuses native analyzer report writes (see [Known client restrictions](../../docs/agent-workflow.md#known-client-restrictions)).

Earlier rounds and screenshots are immutable; a recheck or factual correction is a new numbered report. A report exists even with no findings. When accepting a report, check that every check is marked passed, failed, not performed, or not applicable with a reason, that fixtures, installed native trials, and live observations are labeled distinctly, and that analyzer performance hypotheses are not presented as measurements.

## Finding triage and fix routing

Only the orchestrator edits `reports/findings.md`. Track every finding, including duplicates, dismissals, and deferrals. The summary table links IDs, source reports, task or requirement, severity, blocking decision, status, owner, and latest verification. Each entry's history records dates, rationale, assignments, developer results, user decisions, duplicate relationships, and independent evidence.

Severities are `critical`, `warning`, `suggestion`; record blocking disposition separately. Missing required behavior cannot be downgraded away by severity. States are `reported`, `assigned`, `awaiting-verification`, `verified`, `awaiting-user-decision`, `dismissed`, `duplicate`, and `deferred`. A failed recheck returns the finding to `reported`, with history retained.

- Severity gate: only `critical` findings and `warning`s that break agreed acceptance or safety get a fix round. Record every `suggestion` as a deferred follow-up in the ledger with no fix or recheck unless the user asks.
- A gated finding with a clear fix has an expected result in agreed acceptance criteria, stays in scope, and needs no unresolved behavior or architecture choice. Assign it directly to the developer with the finding ID and recheck expectation; do not ask the user for routine approval.
- A recheck assignment names only its finding IDs plus regression. A docs-only or config-only target gets a light review from both roles, limited to correctness and safety.
- After two fix-and-recheck cycles on one change, stop. Escalate any open findings to the user with evidence and a recommendation instead of starting a third cycle.
- For uncertainty, request bounded investigation. Escalate remaining decisions to the user with evidence, options, recommendation, and impact. Continue independent work meanwhile; silence is not approval.
- Link duplicates to the canonical ID and preserve both reports. Record dismissal rationale. Optional deferrals need rationale and a follow-up destination or reconsideration condition. Deferring required behavior or coverage needs explicit user agreement and coherent OpenSpec updates.
- Developer fixes enter `awaiting-verification`. QA rechecks correctness and functional findings; the analyzer rechecks its own. Cross-cutting fixes may need both. Only the orchestrator marks a finding `verified`, using independent report evidence; a developer cannot approve its own fix.

## Model selection

Concrete model values and reasoning effort are declared only in the native role definitions ([.claude/agents/](../../.claude/agents), [.codex/agents/](../../.codex/agents)): developer and QA use a cost-efficient model, and the code analyzer uses the strongest reasoning model with high effort. A specialist runs on its role default unless the assignment records an override. To override one assignment, pass the client's per-invocation model setting and record the model and rationale in that assignment's ledger row. Do not change `CLAUDE_CODE_SUBAGENT_MODEL` or other personal settings. For a docs-only or config-only target the orchestrator may run the analyzer on the developer/QA cost-efficient default of the client as a recorded override; both reviews remain required. Reports record the resolved model ID, not only the alias. If a client lacks per-role model configuration, its roles inherit the parent model and the gap is recorded under the restriction-evidence rules in [AGENTS.md](../../AGENTS.md).

## Mixed-client operation

Claude Code and Codex may be used alternately or concurrently. Each change has at most one active orchestrator.

- `reports/findings.md` carries a `Current orchestrator` block: client, session identity, since (UTC), previous owner. The marker is advisory, not an enforced lock.
- On resuming a change, read the marker first. If it names a different active session, do not take over the ledger silently; ask the user to confirm the hand-off before assigning work. After confirmation, or when the previous owner is finished, update the marker (record the previous owner), then continue report numbering and finding IDs from the ledger without reuse, regardless of which client produced earlier rounds. Preserve all earlier reports and history.
- Concurrent sessions of either client on different changes must keep their own reports, ledgers, OpenSpec associations, and Bell Tower identity and attention state. Report paths never cross changes.

## Commits

Specialists never commit, push, create branches, or rewrite history. After the completion criteria hold, the orchestrator commits the scoped implementation paths together with the change's artifacts and excludes unrelated user changes. When the checkout is on the default branch, create a change branch first. Push and publication need separate user authorization. Record the commit SHA in the ledger's completion assessment. Until the commit, the review identity is the dirty-tree identity above.

## Completion

Before declaring implementation complete require both current role reports on the current target, every finding triaged, the change's required acceptance satisfied with evidence of the kind it names, and no unresolved blocking finding or unapproved scope deferral. Update task checkboxes only when behavior and required evidence satisfy the controlling OpenSpec workflow; report existence and developer assertions prove nothing. Missing native trials remain unverified, not passed or proven restricted. Fixture success does not replace native or live acceptance. Provider restrictions follow the evidence rules in [AGENTS.md](../../AGENTS.md). Retain optional follow-ups visibly and report remaining gaps honestly. Archival and publication follow their own user authorization.
