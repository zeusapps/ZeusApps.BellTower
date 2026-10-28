# Spec Delta

## MODIFIED Requirements

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
