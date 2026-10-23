# Repository agents

Bell Tower uses a provider-neutral [delegation workflow](../.agents/workflows/openspec-delegation.md), with native repository roles currently configured for Codex. The main agent owns OpenSpec planning, assignments, finding triage, and completion. Developer, QA, and code-analyzer subagents perform the implementation and reviews.

The user explicitly deferred Claude Code multiagent configuration and its native/mixed-client workflow verification until a later request because no Claude subscription is available. This is a repository-workflow scope decision, not a platform restriction or a change to Bell Tower's Claude Code/Codex product parity policy. Shared roles and templates remain provider-neutral for future reintegration.

| Role | Owns | Returns |
| --- | --- | --- |
| Orchestrator | OpenSpec artifacts/tasks and the change's findings ledger | Assignments, decisions, evidence-backed status |
| Developer | Assigned code, tests, configuration, documentation, and fixes | Changed paths, behavior, checks/results, limitations |
| QA | Independent correctness review and applicable functional checks | Numbered report, reproductions, coverage, optional screenshots |
| Code analyzer | Duplication, simplification, complexity, architecture, performance analysis | Numbered report with concrete findings, benefits, risks, and evidence |

Both independent reviews are required for every implementation, with depth proportional to scope. Reviewers never patch implementation or test sources. Their only maintained repository writes are assigned reports and evidence inside the corresponding OpenSpec change. Normal ignored build outputs/disposable test data are allowed. The developer addresses clear fixes; the orchestrator closes them only after independent re-verification.

## Start and activate

Use the existing OpenSpec skills: explore/propose for planning, apply for authorized implementation, verify for evidence, archive when separately requested. Planning does not automatically start apply. An example implementation request is `Apply introduce-repository-subagents using the developer, QA, and code-analyzer roles.`

Codex project roles are `.codex/agents/{developer,qa,code-analyzer}.toml`. Start/reload a trusted project session after adding role files, then ask the main agent to delegate to the named role. Project discovery and available tools depend on the installed client and managed policy; the files alone do not prove native execution. Do not edit personal settings or bypass trust/permissions to force a trial.

The adapters omit model and permission overrides. Codex inherits parent capabilities; shared instructions prohibit nested delegation and out-of-scope writes. Shell and write access is not an enforced report-only filesystem sandbox. Verify behavior and report native enforcement separately.

The current configuration reference is [Codex subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents). During implementation the observed Codex CLI was 0.164.0. Role execution and discovery are recorded independently in this change's reports. The sandboxed CLI reported a missing home-directory warning, which is an environment limitation, not proof of unsupported custom agents. This change adds no `CLAUDE.md` or Claude role definitions; existing Claude commands and skills remain available as before.

## Assign bounded work

Resolve the change root and context through OpenSpec. Supply task/spec IDs, objective, acceptance, applicable Codex/concurrent-session workflow coverage and product provider cases, dependencies, implementation target, allowed write paths, exact report/evidence paths, finding-ID allocation, and expected return. A specialist loads its shared role file before acting.

Use one developer writer per shared scope. Reserve distinct outputs before parallel QA/analysis. If parallel implementation needs an isolated checkout, ensure it contains the assigned revision and relevant untracked/user changes. The main agent keeps the ledger; specialists return additional delegation needs rather than launch nested subagents.

Example assignment data, not an executed trial:

```text
Role: qa
Change root: <absolute CLI-resolved root>
Tasks / requirements: 2.1 / Durable change-local reports and screenshots
Target: <baseline commit; scoped tracked-diff hash; relevant file hashes>
Scope: <explicit implementation paths>
Allowed outputs: reports/qa-001.md; screenshots/qa-001-example.png if useful
New finding IDs: QA-001 through QA-020
Rechecks: none
Expected return: report path, findings, commands/results, limitations
```

## Reports and finding tracking

Use the shared [QA](../.agents/templates/qa-report.md), [analysis](../.agents/templates/analysis-report.md), and [ledger](../.agents/templates/findings.md) templates:

```text
openspec/changes/<change>/
  reports/qa-001.md
  reports/analysis-001.md
  reports/findings.md
  reports/qa-002.md
  screenshots/qa-001-example.png
```

Reports are numbered separately per role and continue after resumption. Preserve earlier rounds. Include a report even with zero findings. Each records target identity, scope, reviewer/client/version/mode, date, checks/results, finding IDs, and limitations. Keep fixture, native, and live evidence distinct. Screenshots are optional when useful visual evidence exists; unavailable required observations remain unperformed. Use report-relative links such as `../screenshots/qa-001-example.png`, which survive moving the whole change during archive.

The orchestrator reserves report paths and unused `QA-...`/`ANALYSIS-...` IDs in `reports/findings.md` before assignments. Rechecks reuse finding IDs, not report filenames. Every finding gets a ledger entry. Duplicate, dismissed, and deferred findings retain rationale/history; duplicates point to the canonical ID. Suggested improvements do not automatically block completion, but required behavior cannot be silently deferred.

Example lifecycle, not a real finding:

```text
QA-001 reported: a documented default is missing
  --> assigned: clear in-scope correction delegated to developer
  --> awaiting-verification: developer reports corrected target
  --> reported: independent check still fails; unsuccessful history retained
  --> assigned --> awaiting-verification
  --> verified: QA report records successful independent recheck
```

For an uncertain fix, assign focused investigation. If behavior/scope/architecture still needs a choice, record `awaiting-user-decision` and present evidence, options, recommendation, and impact. Continue independent work while waiting. Do not ask approval for a clear in-scope correction. A required-scope deferral needs explicit user agreement and coherent planning updates. Optional deferrals need a follow-up destination or reconsideration condition.

## Identify a dirty review target

Record `git rev-parse HEAD` plus the explicit scoped implementation path manifest. Use `git diff --binary <baseline> -- <paths>` to include staged and unstaged tracked changes, with a recorded SHA-256/encoding method. Include SHA-256 for relevant untracked files; supplementary hashes for every scoped file make before/after comparison simple. Record deleted paths too. Use existing Git and standard hashes rather than add a revision-tracking tool.

Exclude report/screenshot creation and orchestrator tracking edits from the implementation identity. Do not exclude an actual implementation file because it is untracked. Later developer changes require affected reviews and checks again; an older review does not approve a new revision. On resumption read reservations, ledger history, current reports, and actual target before assigning the next number.

## Verification and completion

Use checks from [development conventions](development.md) appropriate to changed paths. Reviewers use `scripts/format.ps1 -Verify` rather than a formatter that modifies sources. Fixture tests do not prove real native discovery or live desktop behavior. Record Codex native trials with exact client versions/modes and applicable concurrent-session scenarios without report collisions, stolen main-session identity/association, or incorrect attention/completion behavior. Claude native configuration and Claude/Codex mixed-client workflow trials are user-deferred rather than current completion gates. Product implementation still requires its applicable provider parity checks.

Require both current review reports, every finding triaged, required acceptance complete, and no unresolved blocking finding before declaring implementation complete. Update task checkboxes only on evidence that satisfies the applicable OpenSpec workflow. Unavailable native tests remain named gaps, not provider restrictions. A hard restriction needs exact operation/version/mode, primary evidence or reproducible probe, fallback, and regression coverage.

## Rollback

Revert the workflow/role/template additions and amended entry-point/documentation sections only. Preserve existing repository guidance, unrelated changes, and all change reports/screenshots/finding history. There is no runtime state migration. Keep personal settings and live sessions intact.
