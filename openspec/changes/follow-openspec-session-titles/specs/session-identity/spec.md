# Session Identity Delta

## MODIFIED Requirements

### Requirement: App-owned names and optional change association
Bell Tower SHALL use one identity policy with per-conversation full title, role, work association, checkout and palette. Both providers SHALL resolve explicit Bell Tower title, otherwise verified explicit native title, otherwise optional role plus automatic subject. Subject SHALL use pinned change, current work, unique branch change, ticket, branch, then directory fallback. Generated or Bell Tower-managed native names SHALL NOT be mistaken for user overrides. Associations SHALL be provider/session-scoped; inspection SHALL NOT replace established work and ambiguous candidates SHALL NOT be chosen arbitrarily. Existing identity data and native-name ownership safeguards SHALL be migrated while delivering the same feature to both clients.

#### Scenario: No OpenSpec is present
- **WHEN** a session starts in a project without OpenSpec and has no higher-priority title, ticket or branch fallback under its provider's policy
- **THEN** it receives a usable directory-derived default name that the user can replace

#### Scenario: A user associates a change
- **WHEN** a user explicitly associates a session with an OpenSpec change and has no higher-priority full-title override
- **THEN** the short display name reflects that change

#### Scenario: A conversation selects and switches changes
- **WHEN** eligible successful main-session work or explicit user association selects a change and later selects a different change
- **THEN** that session's association follows each selection and its display name follows the latest selection unless a higher-priority full title is set

#### Scenario: Parallel sessions share a project
- **WHEN** two sessions in the same repository select different changes
- **THEN** each session retains its own association and title without changing the other session

#### Scenario: A manual name overrides automatic selection
- **WHEN** a session without a provider-native custom-title override has a manual name and selects a different change
- **THEN** the association updates while the displayed manual name remains unchanged
- **AND** clearing the manual name reveals the latest selected change

#### Scenario: A session resumes
- **WHEN** an existing session resumes without a new explicit selection
- **THEN** its saved association and manual override are retained

#### Scenario: A Codex profile is reopened and a conversation resumed
- **WHEN** the user opens an installed Codex terminal profile after a session, terminal, or machine restart and resumes a saved conversation
- **THEN** the launcher loads the integration automatically and supported presentations restore that conversation's saved identity without another selection or a user-run Bell Tower command
- **AND** native synchronization retains its saved ownership or paused state in the new owning runtime

#### Scenario: There is no trustworthy selection
- **WHEN** an operation fails, names multiple possible changes, only lists changes or reads a capability, or originates from a background agent
- **THEN** Bell Tower leaves the main session's association unchanged

#### Scenario: Provider role and work subject
- **WHEN** a Claude or Codex session with role PM has active change ttyd-download-chips and no title override
- **THEN** its shared title is PM: ttyd-download-chips

#### Scenario: Explicit native rename
- **WHEN** verified explicit native-title evidence exists and no Bell Tower title overrides it
- **THEN** the explicit provider-native title wins over automatic subject resolution

#### Scenario: Inspect another change
- **WHEN** a conversation working on change A reads or requests read-only status/instructions for change B
- **THEN** its title and association remain A

#### Scenario: No work association yet
- **WHEN** a successful read-only OpenSpec operation identifies a single change but provides no work evidence or explicit user association
- **THEN** the shared resolver retains its ticket, branch or directory fallback until eligible work or an explicit association is established

#### Scenario: Matched work and inspection policy
- **WHEN** either provider works on A and subsequently inspects B
- **THEN** it retains A under the same shared policy with native presentation supplied through its adapter

### Requirement: Shared readable session palette
Bell Tower SHALL assign a random dark session background, a brighter related chip accent, and readable foreground colors. The same short name and, where custom colors are supported, the same accent SHALL identify the session in Bell Tower and supported terminal identity displays. Native provider displays that cannot accept the session accent SHALL retain their native styling and SHALL NOT require a patched renderer. Automatic metadata refreshes and Bell Tower restarts SHALL preserve an existing session palette. Both clients SHALL expose equivalent named/hex/random/reset controls. Proven native styling restrictions SHALL affect only that surface, while shared identity/color controls remain available in Bell Tower and supported displays.

#### Scenario: A new identity is created
- **WHEN** Bell Tower first registers a session without an existing palette
- **THEN** it assigns a dark background and brighter related accent with readable text colors

#### Scenario: A chip and terminal badge are displayed
- **WHEN** a terminal integration supports the session badge with custom colors
- **THEN** its name chip and Bell Tower's attention chip use the same short name and accent

#### Scenario: A native Codex footer is displayed
- **WHEN** Codex supports the session name as a native footer item but cannot accept Bell Tower's accent for that item
- **THEN** the footer shows the session's name using native styling and Bell Tower preserves the session palette for its own displays

#### Scenario: Bell Tower restarts
- **WHEN** the same live session is restored
- **THEN** its palette matches its pre-restart palette

#### Scenario: Concurrent palette creation
- **WHEN** two sessions from either provider register concurrently with multiple palette choices available
- **THEN** their saved accents remain distinguishable and resume preserves them

### Requirement: Git branch and worktree context
Bell Tower SHALL expose the current branch for Git sessions, or a short commit identifier for detached HEAD. A session in a linked worktree SHALL additionally expose a readable worktree name. Git context SHALL be supplementary to the session name and SHALL NOT become the stored OpenSpec association or manual name. Managed tab titles and supported in-session presentations SHALL expose branch and linked-worktree context. Codex SHALL use verified native footer fields where available; a linked-worktree label SHALL be included in its synchronized presentation when no dedicated native field is available. Context SHALL describe the verified session checkout, and unavailable context SHALL be omitted. For either provider, an explicitly associated or uniquely verified working checkout SHALL supply displayed branch/worktree context even when the hosting process was launched in the main checkout. Process ownership, launch directory and external planning roots SHALL remain independent. A user SHALL be able to attach or clear a working checkout; absent or ambiguous mappings SHALL be reported without inventing a checkout.

#### Scenario: A linked worktree is used
- **WHEN** a session runs inside a linked Git worktree
- **THEN** its detailed identity display and tray entry include both branch and worktree name
- **AND** its managed tab title and supported in-session presentation expose the same checkout context

#### Scenario: Codex exposes a branch but no native worktree field
- **WHEN** native name synchronization and the branch footer field are supported for a Codex session in a linked worktree
- **THEN** its footer shows the session name, native branch context, and a readable worktree label supplied through the synchronized presentation
- **AND** neither the worktree label nor the branch is appended to the stored OpenSpec association or manual name

#### Scenario: The checkout changes branch
- **WHEN** the session's checkout changes branch
- **THEN** the displayed Git context refreshes without changing the session's color or stable conversation identity, while the shared branch-bound automatic subject is re-resolved as specified

#### Scenario: A session reports a different checkout
- **WHEN** supported session metadata establishes that the session now works in a different checkout
- **THEN** subsequent Git context describes that checkout without altering other sessions in the repository

#### Scenario: The directory is not a Git checkout
- **WHEN** no Git metadata is available
- **THEN** the session remains usable with its name and palette and omits unavailable Git context

#### Scenario: Ticket worktree differs from launch directory
- **WHEN** a conversation launches in D360 but has a verified working checkout D360-worktrees/DEP-7070
- **THEN** its badge, title and details show that checkout's branch and worktree while navigation still targets the original owning terminal

#### Scenario: Checkout mapping ambiguous
- **WHEN** the same change exists in several candidate worktrees without stronger evidence
- **THEN** the resolver requests explicit checkout association through its controls and retains honest available context

### Requirement: Metadata updates preserve attention episodes
Bell Tower SHALL propagate name, change association, palette, and Git context updates to supported displays without creating a new attention episode, resetting elapsed wait time, changing acknowledgment or viewed status, or replaying sound. Repeated observations of the same metadata SHALL be idempotent.

#### Scenario: A waiting session is renamed
- **WHEN** the user changes its name while an attention chip is visible
- **THEN** the chip updates in place and preserves its wait time and acknowledgment status

#### Scenario: A waiting session automatically changes title
- **WHEN** its selected change or Git context changes while an attention episode is active
- **THEN** supported displays update while the episode, elapsed wait time, acknowledgment, viewed status, and palette remain unchanged and no sound is replayed

#### Scenario: A selection is delivered more than once
- **WHEN** the same successful selection observation is replayed
- **THEN** the resulting title is unchanged and no redundant provider-name update is required

### Requirement: Bundled and optional terminal presentation
Bell Tower SHALL provide its own identity metadata and bundled presentation integrations. Supported presentations SHALL show the short name with supplementary Git context and apply the session background where the host supports it. For Codex versions with verified native chat-name synchronization and footer support, Bell Tower SHALL present the resolved session name through native footer items and update it when the session's selection or manual override changes. Codex presentation configuration SHALL be opt-in through reversible Bell Tower setup, preserve unrelated settings, and retain user edits on removal. Bell Tower SHALL preserve a user-selected native Codex name when it differs from the last Bell Tower-managed name and report that synchronization is paused. If a provider cannot support an in-session name, live name updates, or host background, Bell Tower SHALL expose the specific limitation and retain tracking, notifications, and identity in its own UI without requiring a patched agent binary.

#### Scenario: No external Claude mod is installed
- **WHEN** Bell Tower's supported Claude integration is installed on a clean setup
- **THEN** identity metadata and the supported presentation work without the user's separate badge mod

#### Scenario: A supported Codex session selects a change
- **WHEN** a Codex session with native presentation enabled selects a change and has no manual override
- **THEN** its native chat name and footer reflect that change without restarting the session or injecting terminal input

#### Scenario: Codex does not support the requested footer
- **WHEN** the installed Codex version has no supported mechanism for the requested native session-name footer
- **THEN** Bell Tower retains the name and palette in its chips and overview, uses only verified supported terminal presentation, and reports the footer limitation

#### Scenario: A live Codex connection cannot receive name updates
- **WHEN** the native name field exists but Bell Tower cannot safely update the owning live session
- **THEN** Bell Tower reports live name synchronization as unavailable, continues other supported identity displays, and does not claim that the live footer was updated

#### Scenario: A user renames the chat in Codex
- **WHEN** the current native name differs from the last name successfully applied by Bell Tower
- **THEN** automatic native-name writes pause and Bell Tower preserves that name until the user explicitly re-enables synchronization

#### Scenario: Presentation setup is removed after user edits
- **WHEN** the user removes Bell Tower presentation setup after modifying its installed footer configuration
- **THEN** removal preserves those edits and unrelated provider configuration and reports any retained customized setting

#### Scenario: A new managed Codex session needs live updates
- **WHEN** the updated launcher starts a supported new local Codex session without an explicit standalone or remote option
- **THEN** it creates a dedicated runtime authenticated with a per-launch capability token on loopback and scopes native-name updates to that session's verified thread
- **AND** the interactive client remains a shell child and the backend shares the verified owning console

#### Scenario: A managed Codex client exits
- **WHEN** the owning interactive client exits or its shell disappears
- **THEN** its dedicated runtime is cleaned up without stopping another session's runtime
- **AND** ordinary Bell Tower tray exit or attachment detachment does not itself stop a native agent

#### Scenario: A standalone or external remote session is used
- **WHEN** the user explicitly selects standalone mode or an external remote runtime, or dedicated startup is unsupported
- **THEN** Bell Tower preserves tracking and the applicable prior launch behavior and reports unavailable native-name synchronization without replacing or stopping an existing runtime
