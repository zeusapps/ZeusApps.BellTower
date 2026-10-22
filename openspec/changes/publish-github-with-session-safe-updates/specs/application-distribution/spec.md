# Spec Delta

## Purpose

Distribute verified Windows releases through GitHub and let users activate compatible updates while running agent sessions continue uninterrupted.

## ADDED Requirements

### Requirement: Source and versioned release publication
The project SHALL support deliberate source publication to its verified GitHub repository and version-tagged Windows x64 releases. Each release SHALL provide a self-contained application package, its required launcher and integrations, installation/upgrade instructions, version and compatibility metadata, and a SHA-256 checksum. Application operation SHALL NOT create repositories, push source, or publish releases. Published tags SHALL NOT be moved or reused.

#### Scenario: Initial publication
- **WHEN** the maintainer publishes the prepared project to the verified repository
- **THEN** source is pushed without rewriting history or including local runtime data, private settings, generated artifacts, or unintended files from outgoing history

#### Scenario: Windows release download
- **WHEN** a user obtains a supported versioned release
- **THEN** the ZIP and checksum are available together and the package runs without a separately installed .NET runtime

### Requirement: Validation gates for releases
Pushes to the primary branch and pull requests SHALL run Windows validation covering locked dependency restore, Release build, .NET tests, JavaScript bridge tests, formatting verification, packaging, and packaged updater acceptance. A version-tag release SHALL validate the exact tagged commit and publish only after successful validation and agreement between tag, application, extension, and package versions. Failed checks SHALL prevent publication and retain available diagnostic results.

#### Scenario: Valid release tag
- **WHEN** a valid version tag matches all packaged versions and its commit passes required validation
- **THEN** the release workflow publishes that version's ZIP and checksum

#### Scenario: Failing check or mismatched version
- **WHEN** validation fails or any release version differs from its tag
- **THEN** no release is published and the workflow reports the failed gate

### Requirement: Stable entry point and retained running versions
A supported installation SHALL expose a stable command path that selects a complete verified application version for each new invocation. Activating a compatible update SHALL NOT overwrite files used by existing processes. Existing session runtime managers and long-running launch commands SHALL continue using their original version until natural exit, while subsequent ordinary invocations use the selected version. Dispatch SHALL preserve arguments, working directory, applicable environment, standard streams, and exit status.

#### Scenario: New version with an old runtime still running
- **WHEN** a compatible version is activated while an existing dedicated Codex runtime manager remains alive
- **THEN** that manager and its agent session continue unchanged and new ordinary invocations use the activated version

#### Scenario: Invocation during activation
- **WHEN** a command starts while the selected version changes
- **THEN** it runs one complete verified version and never observes a partially installed or missing payload

### Requirement: Nonblocking stable release discovery
Bell Tower SHALL automatically check the fixed official GitHub release source for strictly newer supported stable versions, independently of session monitoring. Checks sharing a selected data directory SHALL coordinate to attempt at most one automatic request per 24 hours, honor server retry guidance, use bounded requests and cached results, and support disabling automatic checks. Drafts, prereleases, malformed versions, incomplete packages, and incompatible releases SHALL NOT be offered for installation. Requests SHALL NOT transmit session content, project paths, runtime credentials, or private configuration.

#### Scenario: Supported newer version
- **WHEN** a valid newer compatible stable release with all required assets is discovered
- **THEN** the tray presents its version and update availability without interrupting monitoring

#### Scenario: Offline or unsuitable release
- **WHEN** GitHub is unavailable or its release is older, equal, invalid, incomplete, or incompatible
- **THEN** monitoring remains responsive and Bell Tower does not offer an unverified installation or report a failed check as successful

#### Scenario: Multiple versions share update state
- **WHEN** old helpers and the current tray use the same update cache
- **THEN** automatic checks remain coordinated and the tray distinguishes its running version from the already selected version instead of repeatedly offering an installed update

#### Scenario: Automatic checks disabled
- **WHEN** automatic checking is disabled
- **THEN** no automatic network checks or automatic update notices occur and an explicit check remains available

### Requirement: Explicit update controls and local status
The tray SHALL provide Check for updates and explicit Install update actions. The CLI SHALL provide `bell-tower update --check` for a fresh metadata check without package installation, `bell-tower update` for explicit installation, and `bell-tower update --status` for the last local outcome without network access. Background discovery SHALL NOT install updates. Status SHALL distinguish staged, activated, monitoring restored, failed, and rolled-back outcomes, and report relevant running/selected versions and deferred integration activation accurately.

#### Scenario: Check without installing
- **WHEN** the user selects Check for updates or invokes `update --check`
- **THEN** Bell Tower reports current/available versions or a check failure without downloading an application package or changing installed application files

#### Scenario: Inspect an update offline
- **WHEN** the user invokes `update --status` without network access
- **THEN** it reads the installation's recorded outcome and does not interfere with activation or misrepresent staging as completion

### Requirement: Verified recoverable activation
An explicitly initiated update SHALL verify the official release source, package checksum, bounded extraction, expected files, version, runtime, and compatibility before activation. It SHALL serialize competing updates per installation, reject unsupported or unwritable layouts, and prevent stale updater processes from downgrading an already selected version. A validation failure SHALL leave the prior selection and installed data unchanged. Failed activation or tray readiness SHALL retain or restore the prior known-good selection and provide recovery details. Application rollback SHALL NOT implicitly roll back user data.

#### Scenario: Bad download or package
- **WHEN** a checksum, archive path, manifest, packaged version, or compatibility check fails
- **THEN** the selected version and user data remain unchanged and the update reports an actionable failure

#### Scenario: Restart verification fails
- **WHEN** the selected new tray cannot establish readiness after activation
- **THEN** the updater restores the prior selection, attempts to restore monitoring with that version, and records whether recovery succeeded

#### Scenario: Concurrent or stale updater
- **WHEN** a competing update is active or an older process attempts to activate a superseded version
- **THEN** the operation reports busy or already superseded without downgrading selection or overwriting another update's outcome

### Requirement: Session-preserving update completion
A compatible update SHALL require no closure or restart of Claude, Codex, VS Code, Windows Terminal, or active dedicated runtime managers. It SHALL coordinate a bounded tray shutdown/restart, retain hook event ingestion through the stable entry point during the monitoring gap, and restore monitoring using the same data directory. Installation selection and successful monitoring restoration SHALL be reported separately. If no tray was running before a CLI update, successful activation SHALL NOT start one implicitly.

#### Scenario: Active sessions during installation
- **WHEN** a compatible update is explicitly installed with running Claude and managed Codex sessions
- **THEN** their processes and authenticated runtimes remain alive, queued events remain available, and the restarted tray resumes monitoring their existing identities and attachments

#### Scenario: No running tray
- **WHEN** the CLI installs an update while the tray is stopped
- **THEN** the new version is selected without starting a tray and status describes activation accurately

### Requirement: Integration and data compatibility
Release activation SHALL validate a declared shared-state and runtime/integration compatibility contract before mixed-version operation. The initial versioned updater SHALL preserve the current data formats during supported coexistence. Owned shell/extension integration refreshes SHALL preserve unrelated user settings, ownership, selected providers, and project registrations. Reload-dependent changes SHALL be reported as pending and SHALL NOT trigger editor, shell, or agent restarts. An incompatible update SHALL be deferred before activation with explicit maintenance guidance.

#### Scenario: Existing bridge or shell remains loaded
- **WHEN** an update includes refreshed integration files but the current shell or editor still uses compatible older code
- **THEN** existing sessions keep working and Bell Tower reports which refresh requires a new shell or normal editor reload

#### Scenario: Incompatible shared state
- **WHEN** a release cannot coexist with active writers, runtime records, or installed integration contracts
- **THEN** activation is refused before changing shared data or the selected version and the required maintenance step is explained

### Requirement: Safe cleanup and legacy migration
Old versions SHALL remain available while referenced by running processes, pending launches, rollback selection, or recovery state. Cleanup SHALL coordinate with launching and updates, retry busy files later, and never terminate a process to reclaim a version. The first migration from an executable-only installation and any stable-launcher replacement SHALL document required maintenance separately from ordinary compatible payload updates. Unsupported source builds SHALL receive installation guidance instead of automatic replacement.

#### Scenario: Old runtime remains active
- **WHEN** cleanup runs after a new version is selected but the old version still owns a runtime or launch lease
- **THEN** cleanup retains that version and allows the active session to finish naturally

#### Scenario: Legacy executable is in use
- **WHEN** initial migration would replace an executable still used by active sessions
- **THEN** migration explains the one-time maintenance requirement instead of terminating those sessions or claiming migration completed
