# Standalone Setup

## Purpose

Install and operate Bell Tower as personal Windows tooling with bundled integrations, independent data ownership, reversible configuration changes, and useful compatibility diagnostics.

## Requirements

### Requirement: Independent installation
Bell Tower SHALL ship its own session identity support, agent event integrations, launcher helpers, and required terminal bridges. A separate Claude mod framework, existing personal launchers, an OpenSpec installation, a WorkLog installation, or a Bell Tower MCP server SHALL NOT be required for normal tracking and notification operation.

#### Scenario: Installation on a clean user profile
- **WHEN** a user installs Bell Tower with a supported agent and terminal present but without the personal PoC, badge mod, WorkLog, or a Bell Tower MCP registration
- **THEN** Bell Tower can register sessions, assign names and colors, and deliver supported attention notifications

### Requirement: Explicit reversible integration setup
Bell Tower setup SHALL show which user configuration files and integration entries it will change, preserve unrelated settings, and support removal of only Bell Tower-owned additions. Setup SHALL NOT bypass the provider's hook trust or approval mechanisms. Setup SHALL be sufficient using Bell Tower-owned hooks and host bridges without requiring cross-project managed instructions or connections to another helper application.

#### Scenario: Existing agent hooks are present
- **WHEN** the user enables Bell Tower's integration
- **THEN** setup adds Bell Tower's hooks while preserving existing unrelated hook definitions

#### Scenario: Provider trust is pending
- **WHEN** an installed hook requires review before the provider will run it
- **THEN** Bell Tower reports pending setup and supplies the review steps without claiming that event tracking is operational

### Requirement: App-owned runtime data and compatibility import
Bell Tower SHALL maintain its own configuration, session identity, position, and runtime state without requiring storage under an agent's home directory. It SHALL support an explicitly selected import of available legacy names, palettes, voice preference, and position without modifying the original files or requiring legacy tooling afterward.

#### Scenario: Legacy badge metadata is imported
- **WHEN** a user selects import from available legacy badge files
- **THEN** Bell Tower preserves valid names and palette values in its own state and does not require later reads from those files

#### Scenario: Legacy files are absent
- **WHEN** Bell Tower starts without any PoC configuration or badge files
- **THEN** it creates usable app-owned defaults and operates normally

### Requirement: Integration capability diagnostics
Bell Tower SHALL report provider/host version information when available and separately identify event-tracking coverage, terminal badge/background support, hook trust status, and navigation precision. A failure of optional decoration or navigation SHALL NOT disable otherwise supported notifications.

#### Scenario: Tracking works but an in-session badge is unsupported
- **WHEN** an agent supplies valid attention events but cannot display the custom footer
- **THEN** diagnostics distinguish operational tracking from unavailable decoration and notifications continue

### Requirement: Independent tray lifetime and safe management
Bell Tower SHALL run as a single tray instance that survives the exit of a launcher terminal. Normal event ingestion SHALL NOT start multiple tray instances. Management commands SHALL stop or control Bell Tower only, and SHALL NOT stop, signal, restart, or modify running agent sessions or their terminal hosts. Compatible updates SHALL permit the tray to stop and restart monitoring without stopping active dedicated session runtime managers. The restarted tray SHALL retain the selected data directory and restore existing session monitoring. Setup, diagnostics, and validation SHALL remain silent unless the user explicitly invokes a sound test.

#### Scenario: The starting terminal closes
- **WHEN** the terminal that originally launched Bell Tower exits
- **THEN** the tray remains available for other registered sessions

#### Scenario: Bell Tower is updated manually
- **WHEN** a user follows the documented manual executable replacement procedure
- **THEN** the procedure stops Bell Tower before replacement and restarts it afterward without stopping Claude, Codex, VS Code, or Windows Terminal

#### Scenario: Compatible version activation with live runtimes
- **WHEN** an explicitly initiated update closes and restarts the tray while dedicated session runtimes remain active
- **THEN** runtime and agent processes remain running and the tray resumes monitoring against the same data directory without creating a second tray instance

### Requirement: Limited local observability
Bell Tower SHALL retain bounded local diagnostics and avoid recording prompt bodies, full transcripts, credentials, or complete tool payloads in ordinary logs. Malformed event data SHALL be diagnosed without crashing the tray or treating that data as an instruction.

#### Scenario: A hook sends malformed input
- **WHEN** Bell Tower receives an invalid event
- **THEN** it rejects or quarantines the input, records a bounded diagnostic without sensitive payload content, and continues processing other sessions

### Requirement: Direct capability-aware provider hooks
Setup for either provider SHALL generate direct fast hooks without the heavy PowerShell/launcher chain, preserving Unicode and quoted paths. Each generated command SHALL be valid in the shell that the provider actually uses to run hooks for the targeted version and platform, or SHALL use a shell-independent form the provider supports; the shell SHALL be established by evidence, not assumed. Setup SHALL verify each generated command by running it through that real provider hook shell against the packaged ingress before reporting the integration as installed, and SHALL report a failure instead of installing a command that cannot run. Async SHALL be enabled where supported; a verified restriction SHALL use an adapter with the same durable-delivery and latency goals. Unrelated settings and safe runtime ownership SHALL be preserved.

#### Scenario: Claude setup preview
- **WHEN** the user previews setup for either client
- **THEN** the planned hook commands invoke the dedicated ingress directly with asynchronous execution where supported

#### Scenario: Codex fast setup
- **WHEN** setup targets Codex
- **THEN** the same fast durable capture feature is installed through its verified adapter, preserving unrelated runtime settings

#### Scenario: Provider runs hooks through PowerShell
- **WHEN** the provider runs hook commands through PowerShell and the selected data path contains spaces, apostrophes and Unicode
- **THEN** the generated command parses and delivers a durable capture through that shell for both clients

#### Scenario: Generated command cannot run in the provider shell
- **WHEN** the setup verification through the provider's real hook shell produces no durable capture or a shell error
- **THEN** setup reports the failure with a sanitized category and does not report the integration as installed

#### Scenario: Baseline-adjusted hook runtime comparison
- **WHEN** the benchmark compares either provider's generated hook with `echo 1` through the same provider shell, environment, sample count and concurrency
- **THEN** it reports arithmetic mean full-hook elapsed time, arithmetic mean baseline elapsed time and their difference as estimated Bell Tower added runtime, applying the below-300 ms overhead comparison to that difference while retaining raw distributions, the three-second raw-duration guard and durable capture/replay checks
- **AND** a failed baseline or hook command cannot produce a passing comparison, negative estimates remain visible, and historical raw-median results are not relabeled as adjusted-mean measurements

### Requirement: Edited provider hook migration
Setup SHALL retain sufficient handler ownership metadata to detect edits to an owned hook. Replacing edited entries with the new fast form SHALL require an explicit previewed migration action, preserve unrelated handlers and avoid duplicate Bell Tower handlers.

#### Scenario: Existing async hand edit
- **WHEN** the prior owned command remains but timeout or async fields were changed by the user
- **THEN** ordinary setup identifies the edit and offers an explicit replacement rather than silently erasing it

#### Scenario: Migration is applied twice
- **WHEN** the user applies the previewed migration and then repeats it
- **THEN** exactly one appropriate Bell Tower handler remains per supported event and unrelated hooks remain intact

### Requirement: Actionable provider hook diagnostics
Diagnostics SHALL report sanitized exception categories, ingest/consumer health and sessions with unverified process ownership. For each provider, diagnostics SHALL distinguish hooks that are configured and have delivered captures from hooks that are configured but have never delivered a capture, and SHALL report the latter as delivery unverified rather than working. Owned hook entries whose command form is known not to run in the provider's hook shell SHALL be reported with the available previewed migration. A read-only, bounded optional transcript inspection SHALL summarize recent hook timeout counts from verified timedOut/timeoutMs records without logging transcript contents.

#### Scenario: Hook rejection
- **WHEN** a hook fails parsing or persistence
- **THEN** the diagnostic identifies its safe failure category without recording commands, prompts or credentials

#### Scenario: Transcript timeout scan
- **WHEN** a fixture contains known timeout records and private conversation records
- **THEN** doctor reports the timeout count and time range while private contents remain absent from output and logs

#### Scenario: Configured hooks never delivered
- **WHEN** a provider's hooks are configured but no capture from that provider has ever been recorded
- **THEN** doctor reports that provider's hook delivery as unverified, for either client, without claiming the integration works

#### Scenario: Known-broken owned command
- **WHEN** an owned Codex hook entry uses a command form that does not run in Codex's verified hook shell
- **THEN** doctor names the affected entry category and the previewed migration, and no provider settings are changed until the user applies it

### Requirement: Provider parity for fast hook delivery in standalone-setup
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded

### Requirement: Legacy badge format compatibility
Explicit badge import SHALL accept legacy background values represented as supported OSC rgb color strings or hex RGB and normalize them to Bell Tower's internal palette. Import SHALL be provider/session-scoped and SHALL NOT introduce a runtime dependency on the legacy mod or its temporary badge folder.

#### Scenario: OSC background import
- **WHEN** the user explicitly imports a fixture badge containing an OSC rgb background and hex accent
- **THEN** the selected identity receives a valid normalized palette and title without modifying the source badge

#### Scenario: Invalid color sequence
- **WHEN** a fixture includes unsupported escapes or an invalid color value
- **THEN** import rejects it without executing or emitting the sequence

### Requirement: Provider parity for session identity and checkout in standalone-setup
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
