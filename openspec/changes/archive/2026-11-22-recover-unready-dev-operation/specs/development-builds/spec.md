## MODIFIED Requirements

### Requirement: Supported dev installation with rollback
The repository SHALL provide a supported dev-install procedure with separate preflight, switch, rollback, finalize and retire steps.
- **Preflight** SHALL verify the candidate package and its hash and contract. It SHALL check compatibility with the current data and selection, derived from the installed and candidate contracts rather than fixed versions. Preflight SHALL NOT change the installation or the data.
- **Switch** SHALL first snapshot the version selection and monitoring state into a private workspace that only the current user, SYSTEM and Administrators can access. It SHALL then install the candidate next to the existing payloads, stop only the verified Bell Tower tray, select the candidate and restart the tray from the stable launcher.
- **Rollback** SHALL restore the previous selection and the monitoring snapshot.
- **Finalize** SHALL end an unfinished Rollback whose original selection and monitoring snapshot are durably restored but whose restarted tray never confirmed readiness. It SHALL run only while no tray of the installation is running or starting. It SHALL remove only the coordination records that the operation owns. It SHALL record a terminal finalized state with the reason. Finalize SHALL NOT stop any process, start a tray, change the selection, payloads or launcher, or change monitoring state.

When a restarted tray does not confirm readiness, or a tray-instance object exists without verifiable presence, the procedure SHALL report the observable cause: whether the tray process is alive, missing presence, the monitoring state size against its budget, and recent repeated refresh failures. It SHALL also give the supported next step with the operation record path.

The procedure SHALL NOT stop agent sessions, terminals or provider runtimes. It SHALL NOT change the stable launcher or existing payload bytes. It SHALL NOT modify provider configuration unless the user separately runs integration setup. These rules apply identically when the procedure is run from Claude Code or from Codex.

#### Scenario: Preflight only
- **WHEN** a maintainer runs preflight for a valid dev package
- **THEN** the package is verified and a private operation record is written, and the installation selection, payloads, data directory and tray are unchanged

#### Scenario: Switch to a dev build
- **WHEN** a maintainer runs switch after a passing preflight
- **THEN** the dev payload is added and selected, the verified tray restarts on it, running agent sessions continue, and `update --status` reports the dev version as running and selected

#### Scenario: Roll back
- **WHEN** a maintainer runs rollback after a switch
- **THEN** the previous version is selected again, the monitoring snapshot is restored, the tray restarts on the previous version, and the original payload and launcher bytes are unchanged

#### Scenario: Unexpected owner or changed state
- **WHEN** the tray owner, selection or original payload bytes differ from what preflight recorded
- **THEN** switch or rollback stops without changing the selection and reports what differs

#### Scenario: Restarted tray never confirms readiness
- **WHEN** switch or rollback restarts the tray and the tray stays alive without writing presence until the readiness deadline
- **THEN** the operation fails with a message that names the missing presence, the monitoring state size against its budget and any repeated refresh failures, and directs the maintainer to quit the tray from its menu and run finalize or rollback with the stated operation record
- **AND** the selection, monitoring state and coordination records are left as they were, and the operation record stores the same facts

#### Scenario: Finalize an operation whose tray never became ready
- **WHEN** a rollback has restored the original selection and the monitoring snapshot, its restarted tray never confirmed readiness, the maintainer has quit that tray, and finalize runs with the operation record
- **THEN** only the operation's own coordination records are removed and the operation is recorded as finalized with its reason
- **AND** the selection, payloads, launcher and monitoring state are unchanged, no tray is started, and a following preflight and switch for a newer dev build are no longer refused for the interrupted operation

#### Scenario: Switch whose candidate never became ready
- **WHEN** a switch selected the candidate and the candidate tray never confirmed readiness
- **THEN** finalize refuses that switch and names rollback with the same operation record as the recovery; if the restored tray then also never confirms readiness, finalize applies to that rollback

#### Scenario: Finalize refuses while a tray runs or starts
- **WHEN** finalize runs while a tray of the installation is running, starting or holds the tray-instance object
- **THEN** finalize refuses before removing anything, names the process, and directs the maintainer to quit the tray from its menu first
- **AND** no process is stopped

#### Scenario: Finalize refuses an incomplete or foreign operation
- **WHEN** finalize runs for an operation whose selection or monitoring step is incomplete, whose selection differs from the operation's recorded destination, or when the coordination records belong to another operation or to a product update
- **THEN** finalize refuses without changing anything, names the reason, and names the supported recovery: rollback for an incomplete step, or inspection under the release documentation for foreign records

#### Scenario: Same procedure from both clients
- **WHEN** a maintainer follows the install-dev-build skill from Claude Code or from Codex while sessions of both providers are running
- **THEN** both skill copies describe the same finalize step and diagnostics, the same script runs, and no session of either provider is stopped or changed
