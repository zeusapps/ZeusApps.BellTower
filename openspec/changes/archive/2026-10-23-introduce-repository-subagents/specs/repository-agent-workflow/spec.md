# Repository Agent Workflow

## Purpose

Define a Codex repository workflow in which an OpenSpec orchestrator delegates implementation and independent reviews, with durable change-local evidence and traceable resolution of findings. Preserve provider-neutral policies for future Claude setup, which the user has explicitly deferred.

## ADDED Requirements

### Requirement: Orchestrator owns planning and delegation

The main agent SHALL own OpenSpec discovery, planning artifacts, task assignments, findings triage, progress, and completion decisions. It SHALL delegate implementation and verification to the corresponding specialist subagents rather than perform their work directly. Assignments MUST identify the resolved change root, task or requirement IDs, acceptance criteria, relevant context, dependencies, permitted outputs, and review target. Planning-only requests SHALL remain planning-only and SHALL NOT launch implementation. If delegation is unavailable, the orchestrator SHALL report the limitation and request a decision rather than silently implement inline.

#### Scenario: Implementation is authorized
- **WHEN** the user starts applying a change with actionable tasks
- **THEN** the orchestrator reads the change context, assigns bounded implementation work to a developer, and retains ownership of planning and tracking

#### Scenario: Only a proposal is requested
- **WHEN** the user requests exploration or proposal artifacts
- **THEN** the orchestrator respects that workflow's write and approval boundaries and does not start implementation

#### Scenario: Delegation cannot run
- **WHEN** the installed client or session cannot launch the required subagent
- **THEN** the orchestrator records the exact limitation and asks how to proceed without claiming the delegated workflow ran

### Requirement: Developer owns implementation changes

The developer SHALL make implementation changes and approved review fixes, including production code, test sources, configuration, and implementation documentation. It SHALL follow the assigned scope, preserve concurrent user changes, perform appropriate developer checks, and return changed paths, behavior, results, and unresolved limitations. It SHALL return scope or design conflicts to the orchestrator and SHALL NOT self-approve independent review, close findings, modify the findings ledger, or rewrite planning requirements. Specialist subagents SHALL return further delegation needs to the orchestrator.

#### Scenario: A test gap requires a regression test
- **WHEN** QA identifies missing test-source coverage
- **THEN** the orchestrator assigns the test-source change to the developer and QA independently verifies the result

#### Scenario: Implementation conflicts with the plan
- **WHEN** a developer discovers that the requested behavior requires a scope or design change
- **THEN** it reports the conflict to the orchestrator without silently reducing the requirement or changing planning artifacts

### Requirement: Independent QA and code analysis

Every implementation SHALL receive both independent QA and code-analyzer review, including small fixes and configuration or documentation changes, with depth and checks proportional to the affected behavior. QA SHALL review correctness and acceptance coverage and execute applicable functional tests. The analyzer SHALL inspect duplication, complexity, simplification, architectural consistency, and performance opportunities. Performance findings MUST distinguish measured results from hypotheses. Neither reviewer SHALL edit product code, test sources, configuration, planning artifacts, the findings ledger, or the other reviewer's reports. Their repository writes SHALL be limited to their assigned change-local reports and supporting evidence; QA screenshots SHALL use the change's `screenshots/` directory. Test execution MAY create normal ignored build outputs or disposable test data but MUST NOT be used to make unauthorized repository edits.

#### Scenario: QA discovers a defect
- **WHEN** QA reproduces incorrect behavior during review
- **THEN** it records the defect and reproduction in its report without patching the implementation

#### Scenario: Analyzer discovers duplication
- **WHEN** the analyzer finds repeated logic in the change and relevant neighboring code
- **THEN** it reports concrete locations, the proposed simplification, and behavior-preservation risks without editing those locations

#### Scenario: Review scope is small
- **WHEN** the developer changes a small configuration or documentation item
- **THEN** both reviews still produce reports, with explicitly justified checks or non-applicable functional scenarios

### Requirement: Durable change-local reports and screenshots

Each review round SHALL produce a separately numbered QA or analysis report under `reports/` in the resolved OpenSpec change root, including a report when no findings exist. Reports SHALL identify reviewer role and client, date, reviewed commit and any uncommitted-diff identity, assigned scope, related tasks or requirements, checks and outcomes, limitations, and stable finding IDs with severity, location, evidence, and recommendation. QA screenshots, when applicable and available, SHALL be stored under the same change's `screenshots/` directory and linked from the report with scenario or finding context. Later rounds SHALL preserve earlier reports and screenshot evidence. Missing screenshot or execution capability SHALL be recorded honestly. All report and evidence links SHALL remain usable after the change is archived.

#### Scenario: Functional review produces visual evidence
- **WHEN** QA captures a screenshot that supports a tested scenario or finding
- **THEN** the screenshot is stored in the selected change's `screenshots/` directory and referenced by a relative link in the QA report

#### Scenario: Review is repeated after a fix
- **WHEN** a reviewer rechecks the developer's updated revision
- **THEN** a new numbered report references the previous findings and the new review target without overwriting the original report

#### Scenario: A desktop check cannot run
- **WHEN** QA lacks the environment or tool needed to observe the desktop behavior
- **THEN** the report marks that check as unperformed with its reason and does not substitute fixture success for observed acceptance

### Requirement: Orchestrator tracks every finding

The orchestrator SHALL maintain `reports/findings.md` as the authoritative cross-review ledger within each change. Every reported finding SHALL have a stable change-local ID and a ledger entry linking its source report, related task or requirement, severity, blocking decision, rationale, assigned owner when applicable, status history, and verification evidence. Repeated findings SHALL retain their original ID or be explicitly linked as duplicates. Dismissed, duplicate, and deferred entries SHALL preserve their rationale and history rather than disappear. Reviewers SHALL reference existing IDs during re-verification while leaving ledger updates to the orchestrator.

#### Scenario: Both reviewers report the same defect
- **WHEN** QA and the analyzer identify one underlying problem
- **THEN** the orchestrator retains both source reports and links the duplicate to a canonical tracked finding

#### Scenario: Work resumes in a new session
- **WHEN** another orchestrator resumes the change
- **THEN** it can recover outstanding findings, decisions, assignments, and verification state from the change's reports and ledger

### Requirement: Clear fixes are delegated and uncertain decisions are escalated

The orchestrator SHALL triage findings against the agreed scope and acceptance criteria. When the required behavior is clear, the fix is within scope, and no unresolved product or architectural decision remains, it SHALL assign the fix to the developer without requesting routine user approval. For unclear fixes it SHALL first request focused investigation as useful and SHALL escalate unresolved decisions to the user with evidence, options, and a recommendation. It SHALL continue independent work while awaiting a decision. Deferring a finding SHALL NOT silently drop required behavior or acceptance coverage; any such scope change MUST receive explicit user agreement.

#### Scenario: An obvious defect has an in-scope correction
- **WHEN** a reviewed defect contradicts an explicit acceptance criterion and its correction requires no new decision
- **THEN** the orchestrator delegates the correction to the developer and records the assignment without asking the user to approve the routine fix

#### Scenario: A fix requires a behavior choice
- **WHEN** focused investigation leaves multiple materially different behaviors or architectural choices
- **THEN** the orchestrator records an awaiting-user-decision finding and presents the evidence, options, and recommended choice to the user

### Requirement: Completion requires current verification evidence

A developer's reported fix SHALL move a finding to awaiting verification, not directly to closed. QA SHALL recheck correctness and functional findings; the analyzer SHALL recheck its analysis findings. The orchestrator SHALL close findings only against recorded independent verification evidence and SHALL reopen failed fixes. Reviews SHALL target a stable implementation revision; later implementation edits MUST receive affected checks and review again. Implementation completion SHALL require both current role reports, triage of every finding, satisfaction of required acceptance criteria, and no unresolved blocking finding. Task completion SHALL preserve the applicable OpenSpec rules and SHALL NOT be inferred from artifact existence, report existence, or unverified developer claims. Optional improvements MAY remain deferred with recorded rationale when they do not reduce agreed scope.

#### Scenario: Developer reports a fix
- **WHEN** the developer returns a correction for a tracked QA finding
- **THEN** the orchestrator marks it awaiting verification and closes it only after QA records a successful recheck

#### Scenario: A recheck fails
- **WHEN** independent verification reproduces the reported problem on the corrected revision
- **THEN** the orchestrator reopens the finding and routes it back through triage without deleting the unsuccessful fix history

#### Scenario: Code changes after review
- **WHEN** implementation edits occur after a review was completed
- **THEN** affected reviews and checks are repeated against the new target and the old review is retained as historical evidence

### Requirement: Codex workflow and explicit Claude deferral

Codex SHALL provide orchestrator/developer/QA/analyzer responsibilities, report formats, triage policy, and completion rules through provider-neutral shared policy and native Codex role definitions. Role discovery and delegated execution SHALL be verified in the installed Codex client with version and mode recorded. Concurrent Codex sessions SHALL retain distinct assignments, change-local reports, findings, and Bell Tower session identity and attention state. Subagent inspection SHALL NOT take over a main session's OpenSpec association. Native permissions SHALL remain subject to the parent session; documentation MUST distinguish behavioral write restrictions from demonstrated platform enforcement. Claude Code repository multiagent configuration, native execution, and mixed-provider workflow trials SHALL remain explicitly user-deferred until requested; this change SHALL NOT deliver Claude role adapters or a new Claude entry point, alter existing Claude skills/commands, or weaken Bell Tower's product integration parity. Documentation MUST label this as the user's scope decision, not a proven platform restriction. Any independently claimed provider restriction MUST identify the exact operation, version, mode, primary evidence or reproducible probe, and supported fallback; missing verification SHALL remain unverified.

#### Scenario: Configured Codex roles run
- **WHEN** the installed Codex client performs developer, QA, and analyzer assignments
- **THEN** each role follows the shared ownership rules and produces its required outputs with recorded native discovery and execution evidence

#### Scenario: Simultaneous Codex work
- **WHEN** Codex sessions work concurrently on different changes or disjoint assignments in one change
- **THEN** report paths and ledger ownership are assigned without collisions, user changes are preserved, and each main session retains its own association and attention state

#### Scenario: Claude setup is requested later
- **WHEN** the user later wants to use Claude Code multiagents in this repository
- **THEN** its native configuration and acceptance are handled as follow-up work against the shared policy, and historical Claude discovery attempts are not presented as current configured support
