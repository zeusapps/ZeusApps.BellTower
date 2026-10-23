# Session Identity

## Purpose

Give every tracked session a recognizable name and color shared by its terminal presentation, attention chip, and tray entry without requiring separate mod tooling.

## Requirements

### Requirement: App-owned names and optional change association
Bell Tower SHALL own an editable short display name and an optional explicit OpenSpec change association for each session. It SHALL support names derived from that association and manual names. Sessions without OpenSpec SHALL remain fully usable; multiple changes in a repository SHALL NOT cause an arbitrary change to be assigned.

#### Scenario: No OpenSpec is present
- **WHEN** a session starts in a project without OpenSpec
- **THEN** it receives a usable directory-derived default name that the user can replace

#### Scenario: A user associates a change
- **WHEN** a user explicitly associates a session with an OpenSpec change and has not selected a manual name
- **THEN** the short display name reflects that change

### Requirement: Shared readable session palette
Bell Tower SHALL assign a random dark session background, a brighter related chip accent, and readable foreground colors. The same accent and name SHALL identify the session in Bell Tower and every supported terminal identity display. Automatic metadata refreshes and Bell Tower restarts SHALL preserve an existing session palette.

#### Scenario: A new identity is created
- **WHEN** Bell Tower first registers a session without an existing palette
- **THEN** it assigns a dark background and brighter related accent with readable text colors

#### Scenario: A chip and terminal badge are displayed
- **WHEN** a terminal integration supports the session badge
- **THEN** its name chip and Bell Tower's attention chip use the same name and accent

#### Scenario: Bell Tower restarts
- **WHEN** the same live session is restored
- **THEN** its palette matches its pre-restart palette

### Requirement: Git branch and worktree context
Bell Tower SHALL expose the current branch for Git sessions, or a short commit identifier for detached HEAD. A session in a linked worktree SHALL additionally expose a readable worktree name. Git context SHALL be supplementary to the session name and SHALL NOT become session identity.

#### Scenario: A linked worktree is used
- **WHEN** a session runs inside a linked Git worktree
- **THEN** its detailed identity display and tray entry include both branch and worktree name

#### Scenario: The checkout changes branch
- **WHEN** the session's checkout changes branch
- **THEN** the displayed Git context refreshes without changing the session's color or identity

#### Scenario: The directory is not a Git checkout
- **WHEN** no Git metadata is available
- **THEN** the session remains usable with its name and palette and omits unavailable Git context

### Requirement: Metadata updates preserve attention episodes
Bell Tower SHALL propagate name, change association, palette, and Git context updates to supported displays without creating a new attention episode, resetting elapsed wait time, or replaying sound.

#### Scenario: A waiting session is renamed
- **WHEN** the user changes its name while an attention chip is visible
- **THEN** the chip updates in place and preserves its wait time and acknowledgment status

### Requirement: Bundled and optional terminal presentation
Bell Tower SHALL provide its own identity metadata and bundled presentation integrations. Supported presentations SHALL show the short name chip with supplementary Git context and apply the session background where the host supports it. If a provider cannot support an in-session chip or host background, Bell Tower SHALL expose that limitation and retain tracking, notifications, and identity in its own UI without requiring a patched agent binary.

#### Scenario: No external Claude mod is installed
- **WHEN** Bell Tower's supported Claude integration is installed on a clean setup
- **THEN** identity metadata and the supported presentation work without the user's separate badge mod

#### Scenario: Codex does not support the requested footer
- **WHEN** the installed Codex version has no supported mechanism for the custom name chip
- **THEN** Bell Tower retains the name and palette in its chips and overview, uses only verified supported terminal presentation, and reports the footer limitation

### Requirement: Change-driven terminal presentation
Managed terminal presentation SHALL refresh only when its resolved title, palette or verified destination changes. Unchanged refresh ticks SHALL NOT spawn writer or Git child processes. Failed or unverified destinations SHALL use bounded backoff and diagnostics, and console attachment SHALL remain isolated from the tray and hook processes.

#### Scenario: Unchanged presentation
- **WHEN** several refresh cycles see unchanged identity and a fresh shared checkout cache
- **THEN** no title writer or Git child is launched

#### Scenario: Dead destination
- **WHEN** a managed attachment cannot be verified across repeated refreshes
- **THEN** no repeated full-rate writer launches or log flood occurs

#### Scenario: Title changed
- **WHEN** a live verified session changes its display identity
- **THEN** one isolated bounded write updates its terminal while preserving attention and detach restoration

### Requirement: Provider parity for attachment health and title refresh in session-identity
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
