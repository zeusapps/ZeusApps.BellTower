# Development Builds

## Purpose

Lets maintainers build, install, verify, roll back and retire local development builds on their own versioned Bell Tower installation. Dev builds stay clearly marked, and no dev build blocks a later stable update.

## Requirements

### Requirement: Dev builds are visibly marked
A development package SHALL record that it is a dev build, its source commit, whether the working tree was dirty, its build time and the stable base version it was built from. Every surface that reports the running version SHALL show that it is a dev build together with the commit: the tray icon tooltip, the tray About/Sessions view, `doctor`, `update --status` and the CLI version output. A stable package SHALL NOT carry a dev marker, and its version display SHALL be unchanged.

#### Scenario: Running a dev build
- **WHEN** the selected payload is a dev build made from commit `b74a12b` with a clean tree
- **THEN** the tray tooltip, About view, `doctor`, `update --status` and the version output each show the version with a "dev" label, `b74a12b` and the build time

#### Scenario: Dirty working tree
- **WHEN** a dev package is built from a working tree with uncommitted changes
- **THEN** the dev marker records the tree as dirty, and every version surface shows that alongside the commit

#### Scenario: Stable build
- **WHEN** a stable release package is running
- **THEN** no surface shows a dev label, and the version text matches the release number

### Requirement: Dev versions never collide with stable releases
Dev builds SHALL use the reserved version range in which PATCH is 9000 or higher, within the existing MAJOR.MINOR.PATCH format. Stable packaging SHALL refuse that range. Dev packaging SHALL refuse versions outside it, and it SHALL choose a number that is not already present in the target installation. While a dev build is selected, update discovery and staging SHALL compare stable offers with the dev build's base version instead of its dev number. A stable release newer than that base version SHALL therefore be offered and installable.

#### Scenario: Stable release after a dev build
- **WHEN** a dev build `0.1.9001` with base version `0.1.8` is selected and stable `0.1.9` is published
- **THEN** update discovery offers `0.1.9`, staging does not report it as superseded, and activation selects `0.1.9`

#### Scenario: Stable packaging uses the reserved range
- **WHEN** a stable package is requested with version `0.1.9001`
- **THEN** packaging fails before publishing anything and names the reserved dev range

#### Scenario: Next dev number
- **WHEN** a dev package is requested for an installation that already holds `0.1.9001`
- **THEN** packaging chooses `0.1.9002`, or fails with a clear message if an explicitly requested number is already taken

### Requirement: Supported dev installation with rollback
The repository SHALL provide a supported dev-install procedure with separate preflight, switch, rollback and retire steps.
- **Preflight** SHALL verify the candidate package and its hash and contract. It SHALL check compatibility with the current data and selection, derived from the installed and candidate contracts rather than fixed versions. Preflight SHALL NOT change the installation or the data.
- **Switch** SHALL first snapshot the version selection and monitoring state into a private workspace that only the current user, SYSTEM and Administrators can access. It SHALL then install the candidate next to the existing payloads, stop only the verified Bell Tower tray, select the candidate and restart the tray from the stable launcher.
- **Rollback** SHALL restore the previous selection and the monitoring snapshot.

The procedure SHALL NOT stop agent sessions, terminals or provider runtimes. It SHALL NOT change the stable launcher or existing payload bytes. It SHALL NOT modify provider configuration unless the user separately runs integration setup.

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

### Requirement: Retiring dev payloads
The procedure SHALL remove only payloads that carry the dev marker. It SHALL use the same retention rule as the tray's automatic payload cleanup: it SHALL NOT remove the selected version, the recorded previous version, a version retained by an update recovery record, or a payload that a process leases. A payload without a dev marker, including a stable payload, SHALL never be removed by this procedure. Unmarked legacy dev payloads are left to the tray's automatic cleanup, which removes them once they are neither selected nor retained, and the documentation SHALL say so.

#### Scenario: Retire an old dev payload
- **WHEN** a maintainer retires dev payload `0.1.9001` while `0.1.9002` is selected and nothing retains or leases `0.1.9001`
- **THEN** the `0.1.9001` payload folder is removed, and the selection and other payloads are unchanged

#### Scenario: Refuse a selected, retained or leased payload
- **WHEN** a maintainer tries to retire the selected payload, the previous version, a recovery-retained version or a leased one
- **THEN** nothing is removed and the reason is reported

#### Scenario: Refuse an unmarked payload
- **WHEN** a maintainer tries to retire a payload with no dev marker, such as stable `0.1.9` or the earlier unmarked dev build `0.1.10`
- **THEN** nothing is removed, and the message explains that unmarked payloads are reclaimed by the tray's automatic cleanup once they are neither selected nor retained

### Requirement: Installation and cleanup preserve hook delivery
Dev Switch and Rollback, automatic payload cleanup and Retire SHALL NOT hold the launcher gate during tray-stop waits, payload copying, payload hashing, payload enumeration or payload deletion. Short selection-pointer verification and replacement and payload lease acquisition SHALL remain serialized safely, and payloads leased by running invocations SHALL remain protected. Dev selection changes SHALL serialize with update operations and refuse an active or interrupted pending update before stopping the tray or changing the selection. Hook deadlines SHALL NOT be increased as a substitute for removing blocking work. The stable launcher and existing payload bytes SHALL remain unchanged.

#### Scenario: Hooks during slow maintenance
- **WHEN** a disposable installation deliberately blocks a slow Switch, Rollback, cleanup or Retire phase for longer than the configured three-second hook budget
- **THEN** generated launcher-based hook commands for Claude Code and Codex, including mixed simultaneous sessions, complete successfully while that phase remains blocked, durable events are retained, and selected/previous/recovery-retained/leased payload protections still hold

#### Scenario: Pending update during dev operation
- **WHEN** an active or interrupted pending update exists before Switch or Rollback
- **THEN** the operation refuses with an actionable reason before stopping the tray or changing selection, and preserves the update's recovery files

#### Scenario: Packaged fast capture
- **WHEN** both providers use generated native ingress commands from the current dev package on disposable data
- **THEN** they bypass the desktop launcher, return neutral output, preserve every accepted durable capture and complete within three seconds; the existing isolated benchmark records per-provider median/p95/p99/max and enforces its median-below-300-ms target
- **AND** the evidence distinguishes generated-shell trials from authenticated field trials; neither asynchronous hooks nor absence of an error message alone proves successful delivery

### Requirement: Packaging locates its native toolchain
Stable and dev packaging SHALL locate the installed Visual Studio Build Tools and Windows SDK, including installations outside the default paths, without requiring the maintainer to change PATH. When the toolchain is missing, packaging SHALL fail with a message naming the missing component, and it SHALL never fall back to a managed ingress.

#### Scenario: Build Tools outside the default path
- **WHEN** the Build Tools are installed under a custom directory and `vswhere.exe` is not on PATH
- **THEN** packaging finds the toolchain and links the NativeAOT ingress

### Requirement: Dev-build skill parity for Claude Code and Codex
The repository SHALL provide an `install-dev-build` skill with equivalent content for Claude Code and Codex. The skill SHALL cover building, preflight, switch, verification, rollback and retirement. It SHALL state these safety rules:
- Installing on the maintainer's machine needs the user's explicit authorization.
- Agent sessions are never stopped.
- Integration setup with `--apply` runs only on separate request.
- Results, including any rollback path, are reported honestly.

#### Scenario: Skill available in both clients
- **WHEN** a maintainer asks Claude Code or Codex to install the current dev build
- **THEN** each client finds the `install-dev-build` skill, and both copies describe the same procedure and safety rules

#### Scenario: Skill copies stay in sync
- **WHEN** repository validation runs
- **THEN** it fails if the Claude Code and Codex copies of the skill differ in content
