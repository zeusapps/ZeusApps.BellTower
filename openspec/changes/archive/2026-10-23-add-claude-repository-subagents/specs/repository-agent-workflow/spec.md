# Spec Delta

## REMOVED Requirements

### Requirement: Codex workflow and explicit Claude deferral
**Reason**: The user requested Claude Code repository multiagent configuration and mixed-client workflow parity, which ends the deferral that this requirement encoded.
**Migration**: Replaced by "Native Claude Code and Codex workflow adapters", "Per-role model selection", "Shared entry point for both clients", and "Mixed-client operation". The Codex adapters, concurrency rules, and restriction-evidence rules carry over unchanged in substance.

## ADDED Requirements

### Requirement: Native Claude Code and Codex workflow adapters

Claude Code and Codex SHALL each provide the orchestrator, developer, QA, and analyzer responsibilities, report formats, triage policy, and completion rules through the same provider-neutral shared policy and thin native role definitions for that client. Neither client's adapter SHALL duplicate or diverge from the shared role policy. Role discovery and delegated execution SHALL be verified in each installed client with version, mode, and model recorded. Concurrent sessions of either client SHALL retain distinct assignments, change-local reports, findings, and Bell Tower session identity and attention state. Subagent inspection SHALL NOT take over a main session's OpenSpec association. Native permissions SHALL remain subject to the parent session. Documentation MUST distinguish behavioral write and delegation restrictions from platform enforcement demonstrated in a recorded probe. Existing OpenSpec skills and commands for both clients SHALL remain unchanged, and Bell Tower's product integration parity SHALL NOT be weakened. Any claimed provider restriction MUST identify the exact operation, version, mode, primary evidence or reproducible probe, and supported fallback; missing verification SHALL remain unverified rather than be reported as passed or restricted.

#### Scenario: Configured Codex roles run
- **WHEN** the installed Codex client performs developer, QA, and analyzer assignments
- **THEN** each role follows the shared ownership rules and produces its required outputs with recorded native discovery and execution evidence

#### Scenario: Configured Claude Code roles run
- **WHEN** the installed Claude Code client performs developer, QA, and analyzer assignments through its native role definitions
- **THEN** each role follows the shared ownership rules and produces its required outputs with recorded native discovery, execution, version, mode, and model evidence

#### Scenario: A specialist attempts further delegation
- **WHEN** a developer, QA, or analyzer subagent of either client would need additional delegation
- **THEN** it returns the need to the orchestrator, and documentation states whether the client natively prevents nested delegation based on a recorded probe or only by instruction

#### Scenario: Simultaneous same-client work
- **WHEN** sessions of one client work concurrently on different changes or disjoint assignments in one change
- **THEN** report paths and ledger ownership are assigned without collisions, user changes are preserved, and each main session retains its own association and attention state

## MODIFIED Requirements

### Requirement: Orchestrator owns planning and delegation

The main agent SHALL own OpenSpec discovery, planning artifacts, task assignments, findings triage, progress, completion decisions, and commits. It SHALL delegate implementation and verification to the corresponding specialist subagents rather than perform their work directly. Assignments MUST identify the resolved change root, task or requirement IDs, acceptance criteria, relevant context, dependencies, permitted outputs, and review target. Planning-only requests SHALL remain planning-only and SHALL NOT launch implementation. If delegation is unavailable, the orchestrator SHALL report the limitation and request a decision rather than silently implement inline. Specialist subagents SHALL NOT commit, push, create branches, or rewrite history. The orchestrator SHALL commit implementation only after the completion criteria are satisfied, SHALL include only the scoped implementation paths and the change's artifacts, and SHALL first create a change branch when the checkout is on the default branch. Pushing or publishing SHALL require separate user authorization.

#### Scenario: Implementation is authorized
- **WHEN** the user starts applying a change with actionable tasks
- **THEN** the orchestrator reads the change context, assigns bounded implementation work to a developer, and retains ownership of planning and tracking

#### Scenario: Only a proposal is requested
- **WHEN** the user requests exploration or proposal artifacts
- **THEN** the orchestrator respects that workflow's write and approval boundaries and does not start implementation

#### Scenario: Delegation cannot run
- **WHEN** the installed client or session cannot launch the required subagent
- **THEN** the orchestrator records the exact limitation and asks how to proceed without claiming the delegated workflow ran

#### Scenario: Verified work is committed
- **WHEN** both current reviews exist, every finding is triaged, and no blocking finding remains
- **THEN** the orchestrator commits the scoped implementation and change artifacts on a change branch, leaves unrelated user changes uncommitted, and does not push without authorization

#### Scenario: A specialist would commit
- **WHEN** a developer or reviewer reaches a point where committing seems useful
- **THEN** it leaves the working tree uncommitted and reports its stable handoff to the orchestrator

### Requirement: Durable change-local reports and screenshots

Each review round SHALL produce a separately numbered QA or analysis report under `reports/` in the resolved OpenSpec change root, including a report when no findings exist. Reports SHALL identify reviewer role, client, client version, mode, and model, the date, reviewed commit and any uncommitted-diff identity, assigned scope, related tasks or requirements, checks and outcomes, limitations, and stable finding IDs with severity, location, evidence, and recommendation. QA screenshots, when applicable and available, SHALL be stored under the same change's `screenshots/` directory and linked from the report with scenario or finding context. Later rounds SHALL preserve earlier reports and screenshot evidence. Missing screenshot or execution capability SHALL be recorded honestly. All report and evidence links SHALL remain usable after the change is archived. Reviewers SHALL write their own reports. If the client refuses a reviewer's report write, the reviewer SHALL NOT work around the refusal and SHALL return the full report text. The orchestrator SHALL then save that text verbatim at the reserved path, under a header that records the refusal evidence, and SHALL record this in the ledger. This fallback SHALL apply only to the refused write.

#### Scenario: A client refuses a reviewer's report write
- **WHEN** a reviewer's write of its assigned report is refused by the client
- **THEN** the reviewer returns the complete report text without retrying through another tool, and the orchestrator saves it unchanged with a header naming the reviewer, client version, mode, model, and exact refusal message

#### Scenario: Functional review produces visual evidence
- **WHEN** QA captures a screenshot that supports a tested scenario or finding
- **THEN** the screenshot is stored in the selected change's `screenshots/` directory and referenced by a relative link in the QA report

#### Scenario: Review is repeated after a fix
- **WHEN** a reviewer rechecks the developer's updated revision
- **THEN** a new numbered report references the previous findings and the new review target without overwriting the original report

#### Scenario: A desktop check cannot run
- **WHEN** QA lacks the environment or tool needed to observe the desktop behavior
- **THEN** the report marks that check as unperformed with its reason and does not substitute fixture success for observed acceptance

#### Scenario: Reports come from different clients
- **WHEN** one change contains review rounds produced through Claude Code and through Codex
- **THEN** each report identifies its client, version, mode, and model so readers can distinguish the evidence without consulting session history

## ADDED Requirements

### Requirement: Per-role model selection

Each provider's native role definitions SHALL declare a default model per specialist role when that client supports per-role model configuration: a cost-efficient model for developer and QA, and the strongest available reasoning model for the code analyzer. For Claude Code these defaults SHALL be `sonnet` for developer and QA and `opus` for the code analyzer. The orchestrator MAY select a different model for an individual assignment and SHALL record that choice and its reason in the assignment. If a client does not support per-role model configuration, the gap SHALL be recorded under the provider-restriction evidence rules, and that client's roles SHALL inherit the parent model. Model defaults SHALL live in versioned repository role definitions and SHALL NOT require changes to personal or user-level settings.

#### Scenario: Default specialist launch
- **WHEN** the orchestrator assigns work to a developer, QA, or analyzer role without a model override
- **THEN** the specialist runs on that role's declared default model, and the review report records the model used

#### Scenario: Orchestrator raises an assignment's model
- **WHEN** the orchestrator judges an assignment to need a stronger model than the role default
- **THEN** it launches that assignment with the chosen model and records the model and rationale in the assignment entry of the findings ledger

#### Scenario: A client lacks per-role model configuration
- **WHEN** the installed client's native role definition cannot declare a model
- **THEN** the role inherits the parent model and the limitation is documented with the exact client version, mode, and probe evidence

### Requirement: Shared entry point for both clients

`AGENTS.md` SHALL remain the single repository instruction entry point for both clients. The repository SHALL NOT include a project `CLAUDE.md`, `.claude/CLAUDE.md`, or committed `CLAUDE.local.md` that would stop Claude Code from reading `AGENTS.md`. If such a file is introduced, it MUST import `@AGENTS.md` before any client-specific additions. Native role definitions for each client SHALL direct the specialist to its shared role file, the shared project facts file, and, for reviewers, the applicable report template. Together these SHALL be self-contained for that specialist. The role file holds role-specific ownership and write limits, delegation limits, checks, and the return format. The shared project facts file holds facts common to all specialists: project build, test, and format commands, the layer map, provider-parity touchpoints, and live-instance safety rules. Specialists SHALL NOT need the orchestrator-only workflow policy to do their assignment. Each rule SHALL have one authoritative location, and other documents SHALL link to it rather than restate it.

#### Scenario: Claude Code session starts in the repository
- **WHEN** a Claude Code session starts at the repository root with default instruction settings
- **THEN** it loads `AGENTS.md` as project instructions, including the parity policy and delegation workflow

#### Scenario: A specialist starts an assignment
- **WHEN** a developer, QA, or analyzer subagent of either client receives an assignment
- **THEN** its role file, the shared project facts file, and its template give it the project commands, safety limits, ownership rules, and return format, without loading the orchestrator workflow

#### Scenario: A specialist needs the live Bell Tower instance
- **WHEN** a check would run setup, exit, voice or sound commands, or use the default data directory of the user's installed Bell Tower
- **THEN** the specialist uses an isolated data directory and fakes or records the check as not performed, leaving the live instance, its settings, and user agent sessions untouched

#### Scenario: A Claude-specific instruction file is proposed
- **WHEN** a change would add a `CLAUDE.md` or equivalent project instruction file
- **THEN** that file begins with an `@AGENTS.md` import so the shared policy continues to load

### Requirement: Bounded, pragmatic review loop

Only critical findings and warnings that break agreed acceptance or safety SHALL receive a fix round. Suggestions SHALL be recorded as deferred follow-ups without a fix or recheck unless the user requests them. A recheck round SHALL verify only its assigned finding IDs plus regression checks, and SHALL NOT raise new suggestions. Reviewers SHALL NOT report phrasing, duplicated text, or style unless it causes a concrete defect or contradictory rules. Documentation-only or configuration-only targets SHALL receive light reviews from both roles, limited to correctness and safety. After two fix-and-recheck cycles on one change, the orchestrator SHALL stop and escalate any remaining open findings to the user with a recommendation rather than start another cycle.

#### Scenario: A recheck finds only a suggestion
- **WHEN** a recheck round verifies its assigned findings and notices a new suggestion-level issue
- **THEN** the suggestion is recorded as a deferred follow-up and no fix round starts

#### Scenario: The fix-cycle limit is reached
- **WHEN** a change has completed two fix-and-recheck cycles and a finding remains open
- **THEN** the orchestrator presents the open findings, evidence, and a recommendation to the user instead of starting a third cycle

### Requirement: Mixed-client operation

The workflow SHALL support Claude Code and Codex used concurrently or alternately in one repository. Each change SHALL have at most one active orchestrator at a time. `reports/findings.md` SHALL record the current orchestrator's client and session identity and when it took ownership. This marker SHALL be advisory and SHALL NOT be presented as an enforced lock. An orchestrator that resumes a change SHALL read the marker, reservations, ledger history, latest reports, and current implementation target before assigning work. It SHALL continue report numbering and finding-ID allocation without reuse, regardless of which client produced earlier rounds. Concurrent Claude Code and Codex sessions on different changes SHALL retain distinct reports, ledgers, OpenSpec associations, and Bell Tower identity and attention state, including attention during each client's background subagent work.

#### Scenario: Different changes in parallel across clients
- **WHEN** a Claude Code orchestrator works on one change while a Codex orchestrator works on another
- **THEN** each change's reports and ledger receive only its own outputs, and Bell Tower keeps each main session's identity, OpenSpec association, and attention state distinct, including suppression of completion until that session's subagents finish

#### Scenario: A change is handed off between clients
- **WHEN** a change orchestrated in one client is resumed in the other client in a later session
- **THEN** the new orchestrator updates the current-orchestrator marker, continues report numbers and unused finding IDs from the ledger, and preserves all earlier reports and history

#### Scenario: Two orchestrators target the same change
- **WHEN** an orchestrator finds the current-orchestrator marker naming a different active session
- **THEN** it does not take over the ledger silently and asks the user to confirm the hand-off before assigning work
