# Next release readiness

## Purpose

Require installed-package evidence for the full next release so isolated component tests cannot conceal the Windows Claude failures reported in 0.1.8 or regressions in work completed after 0.1.9.

## ADDED Requirements

### Requirement: Complete next-release scope
The release SHALL include the retained feature changes and the post-0.1.9 work named in this release plan, the retained numbered field findings, badge identity parity and VS Code foreground activation. The user's 2026-10-29 cancellation removes remote webhook notifications, including Teams, from implementation and acceptance scope; field finding 12 retains local reminders only. Cancellation SHALL NOT be reported as implementation completion. No retained feature SHALL be labeled deferred or complete solely because its proposal or unit fixtures exist. The user's 2026-11-22 decision defers publisher-signed updates to a later release: this release SHALL keep SHA-256 update verification, SHALL NOT claim a publisher signature, and its release notes SHALL state that updates are hash-verified only. The deferral SHALL NOT be reported as implementation completion.

#### Scenario: Release candidate review
- **WHEN** the candidate is assessed for publication
- **THEN** each scope item maps to implemented tasks, tests and explicit acceptance evidence or a blocking unmet prerequisite

#### Scenario: Deferred update signing
- **WHEN** the candidate is assessed while publisher signing is deferred
- **THEN** the release notes state that updates are verified by SHA-256 only, no signing scenario is counted as passed, and the signing change remains open for a later release

### Requirement: Installed-package acceptance
Release acceptance SHALL exercise the actual packaged hook command and versioned layout against isolated data, baseline 0.1.9 upgrade and rollback, parallel Claude and Codex sessions sharing a launch directory with distinct working checkouts, matched feature conformance and mixed-provider scenarios. It SHALL include hook latency/durability, metadata recovery, process liveness, foreground navigation, audio/reminders, update hash verification, native session adoption, the hook-started tray with its user-stop suppression, and recovery of oversized monitoring state.

#### Scenario: Representative field scenario
- **WHEN** five isolated conversations covering each provider and a mixed-provider run share a main launch checkout but have separate ticket worktrees and historical names
- **THEN** the installed candidate preserves independent identities, context, notifications and navigation through delayed events and tray restart

#### Scenario: Packaged hook benchmark
- **WHEN** the full provider-shell command is benchmarked under representative load
- **THEN** the recorded end-to-end latency and delivered-event counts satisfy the ingress contract rather than only development-executable timings

### Requirement: Honest silent acceptance evidence
Automated acceptance SHALL use isolated data/settings/transcripts, disposable controlled hosts, silent sinks, fake microphone/idle observations and disposable update fixtures. User-confirmed historical behavior, retained fixture evidence, desktop probes and new live acceptance SHALL be recorded separately; unavailable authenticated or RDP evidence SHALL NOT be represented as passed.

#### Scenario: No external test credentials
- **WHEN** authenticated provider sessions are unavailable
- **THEN** fixture verification proceeds and the remaining live prerequisites are explicitly recorded without reading production secrets or sending alerts

#### Scenario: Candidate publication readiness
- **WHEN** all automated checks pass but a required field acceptance remains unobserved
- **THEN** the release evidence remains incomplete until that acceptance is obtained or the user explicitly revises the release requirement

#### Scenario: Identity changes archived before live acceptance
- **WHEN** the shipped session-title and badge-identity changes are archived with user-approved transfer of their unfinished checks
- **THEN** provider hook captures, the two-session desktop trial and the five-conversation identity/worktree trial remain required and unverified in the release gate until evidence is obtained
- **AND** the documented fork-time identity parity gap remains unfinished rather than becoming a proven restriction through archive or transfer


### Requirement: Provider parity for release readiness in release-readiness
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded

### Requirement: Release provider capability matrix
The release SHALL include a feature-by-provider-by-mode acceptance matrix with supported, proven-restricted or unfinished/unverified states. Each restriction SHALL cite version-scoped primary evidence or a reproducible probe, its fallback and a regression check. Unimplemented adapters, absent fixtures or historical policy differences SHALL NOT establish a hard restriction. The release gate SHALL reject unexplained parity gaps.

#### Scenario: Only one provider has passed
- **WHEN** Claude feature tests pass but Codex has neither implementation evidence nor a proven restriction
- **THEN** release acceptance remains incomplete for that feature

#### Scenario: Native display limitation
- **WHEN** a supported provider version cannot render a requested native color or widget under a verified contract
- **THEN** only that native surface is marked restricted, while shared identity, controls and other supported displays remain available
