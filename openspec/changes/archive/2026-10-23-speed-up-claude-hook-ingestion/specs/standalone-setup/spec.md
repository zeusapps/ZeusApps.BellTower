# Provider hook setup delta

## ADDED Requirements

### Requirement: Direct capability-aware provider hooks
Setup for either provider SHALL generate direct fast hooks without the heavy PowerShell/launcher chain, preserving Unicode and quoted paths. Async SHALL be enabled where supported; a verified restriction SHALL use an adapter with the same durable-delivery and latency goals. Unrelated settings and safe runtime ownership SHALL be preserved.

#### Scenario: Claude setup preview
- **WHEN** the user previews setup for either client
- **THEN** the planned hook commands invoke the dedicated ingress directly with asynchronous execution where supported

#### Scenario: Codex fast setup
- **WHEN** setup targets Codex
- **THEN** the same fast durable capture feature is installed through its verified adapter, preserving unrelated runtime settings

### Requirement: Edited provider hook migration
Setup SHALL retain sufficient handler ownership metadata to detect edits to an owned hook. Replacing edited entries with the new fast form SHALL require an explicit previewed migration action, preserve unrelated handlers and avoid duplicate Bell Tower handlers.

#### Scenario: Existing async hand edit
- **WHEN** the prior owned command remains but timeout or async fields were changed by the user
- **THEN** ordinary setup identifies the edit and offers an explicit replacement rather than silently erasing it

#### Scenario: Migration is applied twice
- **WHEN** the user applies the previewed migration and then repeats it
- **THEN** exactly one appropriate Bell Tower handler remains per supported event and unrelated hooks remain intact

### Requirement: Actionable provider hook diagnostics
Diagnostics SHALL report sanitized exception categories, ingest/consumer health and sessions with unverified process ownership. A read-only, bounded optional transcript inspection SHALL summarize recent hook timeout counts from verified timedOut/timeoutMs records without logging transcript contents.

#### Scenario: Hook rejection
- **WHEN** a hook fails parsing or persistence
- **THEN** the diagnostic identifies its safe failure category without recording commands, prompts or credentials

#### Scenario: Transcript timeout scan
- **WHEN** a fixture contains known timeout records and private conversation records
- **THEN** doctor reports the timeout count and time range while private contents remain absent from output and logs


### Requirement: Provider parity for fast hook delivery in standalone-setup
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
