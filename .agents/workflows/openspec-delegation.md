# Delegated OpenSpec work

This provider-neutral policy currently has native role configuration for Codex. The user explicitly deferred Claude Code configuration and native/mixed-client workflow verification until a later request because no Claude subscription is available. That deferral is an agreed repository-workflow scope decision, not a proven provider restriction, and does not change Bell Tower's product parity requirements. The main agent orchestrates; specialist subagents perform implementation and independent reviews. A specialist follows its assigned role instead of taking over orchestration. Do not spawn nested specialists: return additional delegation needs to the orchestrator.

## Planning and authorization

- Invoke the applicable OpenSpec skill and preserve its authorization boundaries, CLI-controlled state, context, guidance, and completion criteria. Use CLI-resolved change/context paths; never guess from the only active change or the current directory.
- The orchestrator owns exploration, proposal/design/spec artifacts, `tasks.md`, assignments, `reports/findings.md`, user communication, and final status. Planning requests stay planning-only. An apply request authorizes developer work within that change's agreed scope.
- Delegate all implementation edits to the developer, including sources, test sources, configuration, documentation, and fixes. The main agent does not implement a convenient fallback. If delegation cannot run, describe the exact client/version/mode limitation and ask how to proceed while completing independent authorized work.
- Do not edit generated OpenSpec skill copies or add a competing task tracker. Fulfill apply's implementation instructions through developer assignments and verify's completeness/correctness/coherence criteria through independent QA evidence.
- Preserve user changes, explicit overrides, current sessions, and unrelated active changes. Do not change personal configuration, stop user processes, publish, or archive merely to complete a task.

## Assignment and handoff

Before launching a specialist, supply:

| Field | Required content |
| --- | --- |
| Role and objective | Developer, QA, or code-analyzer; bounded outcome |
| Scope | Absolute resolved change root; task and requirement IDs; relevant context paths |
| Acceptance | Expected behavior, dependencies, applicable Codex/concurrent-session workflow cases and product provider cases when relevant |
| Review target | Baseline commit and scoped implementation identity, including relevant dirty/untracked files |
| Ownership | Permitted implementation paths or exact reserved report/evidence paths; explicit prohibited writes |
| Finding allocation | Existing IDs to recheck and unused ID range for new findings |
| Return | Changed paths or report paths, commands/results, finding IDs, limitations, next action |

Reserve report filenames and finding IDs in `reports/findings.md` before concurrent reviewers start. Use one developer writer per shared scope/checkout and one ledger owner per change. Parallel developers need disjoint ownership or an explicitly prepared isolated checkout containing the assigned revision and relevant user/untracked work. Do not assume a default-branch worktree contains the current implementation.

The developer returns behavior, changed paths, checks/results, finding IDs addressed, and unresolved limitations. QA and the analyzer return their report paths, finding IDs, coverage, and unavailable checks. Specialists report scope conflicts rather than alter planning requirements or silently reduce acceptance.

## Independent review and permitted writes

Every implementation receives both QA and code analysis, even a small documentation or configuration fix. Scale review depth and functional checks to the affected behavior. Start reviews on a stable developer handoff; QA and analysis may run concurrently on distinct reserved outputs.

QA and the analyzer cannot edit implementation, test sources, configuration, planning artifacts, the ledger, or another reviewer's reports. Their maintained repository writes are limited to their own assigned reports and supporting evidence inside the selected change. QA may write relevant screenshots in `<change-root>/screenshots/`. Build outputs and disposable test data are permitted only as normal verification artifacts, not as a way to bypass edit restrictions. Use verification-mode formatters; request developer-authored regression tests when needed.

The parent session's permissions still apply. Native tool restrictions supplement these rules where available; shell/report-write access is not an OS-enforced report-only path sandbox. Compare scoped implementation identities before and after reviewers run and report any unexpected edits. Do not claim an enforcement mechanism that was not demonstrated.

## Reports, evidence, and target identity

Use `.agents/templates/qa-report.md`, `.agents/templates/analysis-report.md`, and `.agents/templates/findings.md`. All maintained review evidence belongs to the resolved change:

```text
<change-root>/
  reports/qa-001.md
  reports/analysis-001.md
  reports/findings.md
  screenshots/qa-001-scenario-description.png
```

Number reports per role and change, continuing across sessions. Reserve unique `QA-001` and `ANALYSIS-001` finding IDs independently of report numbers. Existing findings keep their IDs during rechecks. Return to the orchestrator if the reserved range is exhausted; do not reuse IDs.

Write a report even when no findings exist. Include reviewer/client/version/mode, date, target, scope/task/spec references, commands and outcomes, functional coverage, evidence, and limitations. Mark checks passed, failed, not performed, or not applicable with a reason. Distinguish fixtures, installed native trials, and live observations. Performance hypotheses are not measurements.

Earlier report rounds and screenshots are immutable history. Write a new numbered report for rechecks or factual corrections, linking earlier findings. Use relative links within the change, such as `../screenshots/qa-001-example.png`, so archive retains usable evidence links. Use repository-relative source references with the reviewed revision rather than fragile relative links outside the change. Capture only relevant visual evidence; no screenshot folder or placeholder is needed when visual evidence is not applicable. Unavailable capture capability remains an explicit limitation.

Use standard Git output and content hashes for review identity; do not build a separate tracking tool. Record the baseline (`git rev-parse HEAD`), explicit scoped paths, tracked diff identity (`git diff --binary <baseline> -- <paths>` includes staged and unstaged content), and SHA-256 per relevant untracked file. Hash exact diff bytes or a consistently recorded UTF-8 encoding; state the method. File hashes provide a simple supplementary identity for all scoped implementation files. Include additions and deletions in a sorted path manifest. Exclude reports, screenshots, `tasks.md`, and other orchestrator tracking edits from the implementation identity; their creation must not invalidate the implementation review. Never exclude an actual implementation file just because it is untracked.

Later implementation changes require affected checks and reviews again. A report on an earlier target is historical evidence, not current approval. Carry forward unaffected coverage only with explicit rationale and target mapping. On resumption read the ledger, reservations, latest reports, and current target before assigning work.

## Finding triage and fix routing

Only the orchestrator edits `reports/findings.md`. Track every report finding, including duplicates, dismissals, and deferrals. The summary table links IDs, source reports, task/requirement, severity, blocking decision, status, owner, and latest verification. Each finding's history records dates, rationale, assignments, developer results, user decisions, duplicate relationships, and independent evidence.

Use severities `critical`, `warning`, and `suggestion`; record blocking disposition separately. Missing required behavior cannot be downgraded away by severity. States are `reported`, `assigned`, `awaiting-verification`, `verified`, `awaiting-user-decision`, `dismissed`, `duplicate`, and `deferred`. A failed recheck returns the finding to `reported` with its history retained.

- A clear fix has an expected result in agreed acceptance criteria, stays within scope, and needs no unresolved behavior or architectural choice. Assign it directly to the developer with the finding ID and recheck expectation; do not ask for routine user approval.
- For uncertainty, request bounded investigation from the appropriate specialist. Escalate remaining decisions to the user with evidence, options, recommendation, and impact. Continue independent work while awaiting the answer; silence is not approval.
- Link duplicate findings to their canonical ID and preserve both reports. Record dismissal rationale. Optional deferrals need rationale and a follow-up destination or reconsideration condition. Deferring required behavior or coverage needs explicit user agreement and coherent OpenSpec updates.
- Developer-reported fixes enter `awaiting-verification`. QA rechecks correctness/functional findings; the analyzer rechecks its analysis findings. Cross-cutting fixes may require both. Only the orchestrator closes a finding as `verified` using independent report evidence; a developer cannot approve its own fix.

## Completion and scoped provider coverage

Use the Codex native adapters with the shared role/report/triage policy. Record exact versions and modes for role discovery and execution. Verify applicable Codex concurrent-session scenarios, report reservations, main-session identity/OpenSpec association, foreground attention during background work, and completion suppression until relevant background work ends. Never let a specialist's inspection select another main-session change. Claude native configuration and Claude/Codex mixed-client workflow trials are explicitly user-deferred and are not completion gates for this scoped repository workflow. Reinstate that work only on a later user request; do not claim it passed or classify the absent subscription as a hard platform restriction. Product changes still follow the existing Claude Code/Codex parity policy.

Missing native trials remain unverified, not passed or proven restricted. A hard restriction needs the exact affected operation/version/mode, primary documentation or reproducible probe, supported fallback, and regression evidence. Fixture success does not replace native or live acceptance.

Before declaring implementation complete require both current role reports, every finding triaged, required acceptance satisfied, and no unresolved blocking finding or unapproved scope deferral. Update each task checkbox only when its behavior and required evidence satisfy the controlling OpenSpec workflow. Artifact/report existence and developer assertions alone prove nothing. Retain optional follow-ups visibly and report remaining gaps honestly. Archival and publication follow their own user authorization.
