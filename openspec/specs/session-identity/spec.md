# Session Identity

## Purpose

Give every tracked session a recognizable name and color shared by its terminal presentation, attention chip, and tray entry without requiring separate mod tooling.

## Requirements

### Requirement: App-owned names and optional change association
Bell Tower SHALL use one identity policy with per-conversation full title, work association, checkout and palette. Both providers SHALL resolve explicit Bell Tower title, otherwise verified explicit native title, otherwise the automatic subject. Bell Tower SHALL NOT use a session role in names or speech; legacy role inputs (role launch shortcuts, `--role`, `BELLTOWER_ROLE`, saved role values) SHALL be accepted for compatibility and ignored. Subject SHALL use pinned change, current work, unique branch change, ticket, branch, then directory fallback. Generated or Bell Tower-managed native names SHALL NOT be mistaken for user overrides. Associations SHALL be provider/session-scoped; inspection SHALL NOT replace established work and ambiguous candidates SHALL NOT be chosen arbitrarily. Existing identity data and native-name ownership safeguards SHALL be migrated while delivering the same feature to both clients.

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

#### Scenario: Legacy role input and work subject
- **WHEN** a Claude or Codex session launched through a legacy role shortcut has active change ttyd-download-chips and no title override
- **THEN** its shared title is ttyd-download-chips and its announcements contain no role

### Requirement: Shared readable session palette
Bell Tower SHALL assign each new session a palette from an ordered series of perceptually distinct session colors. Each palette has a dark background, a brighter related chip accent and readable foreground colors. Allocation SHALL consider the sessions that are live now and launch palettes prepared but not yet consumed, not ended sessions. It SHALL choose the first series color that no such session uses. When every series color is in use, it SHALL choose the color used by the fewest of them, then the one whose accent is perceptually farthest from all of them, with a deterministic tie-break. The terminal backgrounds of the series SHALL also be visibly distinct from each other, not only the accents. A session that lost process verification SHALL count only while it was observed recently. Color difference SHALL be measured perceptually, not as raw RGB distance. The same short name and, where custom colors are supported, the same accent SHALL identify the session in Bell Tower and in supported terminal identity displays. Native provider displays that cannot accept the session accent SHALL retain their native styling and SHALL NOT require a patched renderer. Automatic metadata refreshes, Bell Tower restarts and conversation resume SHALL preserve an existing session palette, and existing saved palettes SHALL NOT be rewritten by this rule. Both clients SHALL expose equivalent named/hex/random/reset controls. Proven native styling restrictions SHALL affect only that surface, while shared identity and color controls remain available in Bell Tower and supported displays.

#### Scenario: A new identity is created
- **WHEN** Bell Tower first registers a session without an existing palette
- **THEN** it assigns a series palette with a dark background, a brighter related accent and readable text colors

#### Scenario: Concurrent sessions get distinct colors
- **WHEN** several Claude Code and Codex sessions, managed and native, are live at the same time and fewer of them exist than series colors
- **THEN** every live session has a different series color, regardless of how many ended sessions were saved before

#### Scenario: Ended sessions do not block colors
- **WHEN** many ended sessions hold palettes and a new session starts while only a few sessions are live
- **THEN** the new session receives the first series color not used by a live session or a pending launch

#### Scenario: More live sessions than series colors
- **WHEN** every series color is already in use by a live session or pending launch
- **THEN** the new session receives the series color shared by the fewest live sessions, then the one perceptually farthest from all live accents, then the earliest in series order, so no color is used three times before every color is used twice

#### Scenario: A chip and terminal badge are displayed
- **WHEN** a terminal integration supports the session badge with custom colors
- **THEN** its name chip and Bell Tower's attention chip use the same short name and accent

#### Scenario: Bell Tower restarts
- **WHEN** the same live session is restored
- **THEN** its palette matches its pre-restart palette

#### Scenario: A native Codex footer is displayed
- **WHEN** Codex supports the session name as a native footer item but cannot accept Bell Tower's accent for that item
- **THEN** the footer shows the session's name using native styling and Bell Tower preserves the session palette for its own displays

#### Scenario: Concurrent palette creation
- **WHEN** two sessions from either provider register concurrently with multiple palette choices available
- **THEN** their saved accents are different series colors, and resume preserves them

### Requirement: Git branch and worktree context
Bell Tower SHALL expose the current branch for Git sessions, or a short commit identifier for detached HEAD. A session in a linked worktree SHALL additionally expose a readable worktree name. Git context SHALL be supplementary to the session name and SHALL NOT become the stored OpenSpec association or manual name. Managed tab titles and supported in-session presentations SHALL expose branch and linked-worktree context. Codex SHALL use verified native footer fields where available; a linked-worktree label SHALL be included in its synchronized presentation when no dedicated native field is available. Context SHALL describe the verified session checkout, and unavailable context SHALL be omitted. For either provider, an explicitly associated or uniquely verified working checkout SHALL supply displayed branch/worktree context even when the hosting process was launched in the main checkout. Process ownership, launch directory and external planning roots SHALL remain independent. A user SHALL be able to attach or clear a working checkout; absent or ambiguous mappings SHALL be reported without inventing a checkout. When a branch change in a uniquely verified working checkout clears the work that verified it, that checkout SHALL remain the conversation's working checkout, labeled as retained, until a user pin, provider evidence or new work supersedes it; it SHALL NOT fall back to the launch checkout for that reason alone. A checkout still verified by a pinned change keeps its unique-work basis and is not relabeled as retained. A failed Git worktree listing SHALL NOT release a retained checkout while its directory exists; a successful listing without it, a missing directory, clearing the checkout or a reset releases it.

#### Scenario: A linked worktree is used
- **WHEN** a session runs inside a linked Git worktree
- **THEN** its detailed identity display and tray entry include both branch and worktree name
- **AND** its managed tab title and supported in-session presentation expose the same checkout context

#### Scenario: The checkout changes branch
- **WHEN** the session's checkout changes branch
- **THEN** the displayed Git context refreshes without changing the session's color or stable conversation identity, while the shared branch-bound automatic subject is re-resolved as specified

#### Scenario: The directory is not a Git checkout
- **WHEN** no Git metadata is available
- **THEN** the session remains usable with its name and palette and omits unavailable Git context

#### Scenario: Codex exposes a branch but no native worktree field
- **WHEN** native name synchronization and the branch footer field are supported for a Codex session in a linked worktree
- **THEN** its footer shows the session name, native branch context, and a readable worktree label supplied through the synchronized presentation
- **AND** neither the worktree label nor the branch is appended to the stored OpenSpec association or manual name

#### Scenario: A session reports a different checkout
- **WHEN** supported session metadata establishes that the session now works in a different worktree of its launch repository
- **THEN** subsequent Git context describes that checkout without altering other sessions in the repository
- **AND** a reported directory in an unrelated repository does not change the displayed checkout unless the user attaches it explicitly

#### Scenario: Ticket worktree differs from launch directory
- **WHEN** a conversation launches in D360 but has a verified working checkout D360-worktrees/DEP-7070
- **THEN** its badge, title and details show that checkout's branch and worktree while navigation still targets the original owning terminal

#### Scenario: Verified checkout changes branch after its work is cleared
- **WHEN** a conversation whose working checkout was uniquely verified by its work changes branch in that same checkout, and the branch-bound work is cleared
- **THEN** for either provider its badge, title, chip and details keep that checkout, show its new branch and worktree, and do not fall back to the launch checkout
- **AND** the automatic subject is re-resolved from the new branch, and a later user pin, provider evidence or new work replaces the retained checkout as before

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

#### Scenario: Codex does not support the requested footer
- **WHEN** the installed Codex version has no supported mechanism for the requested native session-name footer
- **THEN** Bell Tower retains the name and palette in its chips and overview, uses only verified supported terminal presentation, and reports the footer limitation

#### Scenario: A supported Codex session selects a change
- **WHEN** a Codex session with native presentation enabled selects a change and has no manual override
- **THEN** its native chat name and footer reflect that change without restarting the session or injecting terminal input

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

### Requirement: Provider active work evidence
Automatic work associations for both clients SHALL derive from verified successful main-session edits under a non-archive change directory, supported workflow skills naming an existing change, or unambiguous successful work operations. Supported literal shell wrappers, PowerShell calls and bounded pipe/redirection forms SHALL be recognized only with sufficient success and root evidence. Reads, searches, background agents, failed work and ambiguous candidates SHALL NOT select another change.

#### Scenario: Write or edit change artifact
- **WHEN** a supported edit or patch tool successfully changes a path under openspec/changes/change-a outside archive
- **THEN** the main conversation associates change-a

#### Scenario: Workflow skill
- **WHEN** a verified main-session provider workflow/skill, including supported opsx:* or explain-change forms, successfully addresses an existing literal change folder
- **THEN** the folder becomes eligible work evidence while explain/read-only inspection of another change preserves an established association

#### Scenario: Shell command shapes
- **WHEN** a successful supported work command uses literal cd-and-chain, PowerShell or bounded redirection with independently verified root and result
- **THEN** the resolver recognizes its exact change without evaluating arbitrary shell code

#### Scenario: Ambiguous branch mapping
- **WHEN** several folders match the current branch ticket
- **THEN** the resolver retains a stronger association or reports ambiguity and never chooses arbitrarily

#### Scenario: Branch switches
- **WHEN** the working checkout branch changes
- **THEN** branch-derived and touched-work evidence bound to the prior branch is invalidated while explicit title/change pins remain

### Requirement: Provider conversation identity recovery
On first registration and resume, Bell Tower SHALL recover eligible main-conversation identity using a supported provider history API or bounded verified read-only adapter. Both clients SHALL share recovery/evidence rules, preserve pins, handle partial/rotated/unavailable history and retain only derived metadata and receipts. Native-title provenance and history schemas SHALL be verified. A hard history-access restriction SHALL name its version/mode, proof and honest fallback; absent captures or implementation SHALL remain unfinished work.

#### Scenario: First-install resume
- **WHEN** a conversation predates Bell Tower and its latest eligible work evidence is in older transcript records
- **THEN** background scanning recovers that association without another user command or synthetic lifecycle event

#### Scenario: Unrelated recent read
- **WHEN** the newest transcript call inspects change B while older successful work selected A
- **THEN** recovery retains A

#### Scenario: Interrupted transcript write
- **WHEN** the last transcript line is incomplete
- **THEN** the reader defers that line and continues safely on a later pass

### Requirement: Shared provider presentation controls
Subject, checkout and palette SHALL be stored as distinct metadata and projected consistently into badge, terminal title, chip and speech. Speech SHALL pronounce at most four subject words after replacing hyphens and underscores with spaces. Users SHALL be able to pin or clear a title/change, set a named or hex color, choose random color and reset pins. Choosing random color or resetting SHALL select a series color not used by another live session when one is available, otherwise one chosen by the allocation fallback, and SHALL never return the session's own current color; an explicit named or hex color SHALL be applied as given even if another session uses a similar color.

#### Scenario: Speech formatting
- **WHEN** a session with subject ttyd-download-chips waits for permission
- **THEN** the phrase is ttyd download chips, needs permission

#### Scenario: Reset automatic identity
- **WHEN** the user resets an identity in either integration
- **THEN** title/change/color pins clear, a distinct series palette is selected and automatic evidence is re-resolved without resolving attention

#### Scenario: Random color
- **WHEN** the user chooses random color for a session in either integration while other sessions are live
- **THEN** the session receives a series color that no other live session uses when one is available, and never its own previous color, even when every series color is in use

#### Scenario: Explicit color
- **WHEN** the user sets a named or hex color that is close to another live session's color
- **THEN** that exact color is applied and pinned

#### Scenario: Resume palette
- **WHEN** the same provider conversation resumes
- **THEN** its saved palette and explicit pins remain unless the user resets them

### Requirement: Provider parity for session identity and checkout in session-identity
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded

### Requirement: Identity continuity during native adoption
Native adoption, terminal disconnection, runtime reconnection and verified terminal rebinding SHALL preserve the conversation's existing name, palette, change association, native-name ownership controls and attention episode state for both providers. Native-name access and terminal presentation SHALL be evaluated separately. Connection to an existing shared backend SHALL NOT overwrite a native user title or grant permission to decorate an unverified terminal.

#### Scenario: Native conversation is resumed in another terminal
- **WHEN** the same conversation is resumed through the native client in a newly verified terminal
- **THEN** its saved identity and explicit overrides return while its former terminal destination is not reused

#### Scenario: Shared native name access exists without a terminal binding
- **WHEN** a verified runtime supports reading or updating the exact conversation's native name but its terminal is unverified
- **THEN** existing native-name ownership controls remain effective and no terminal color or console-title write is attempted

#### Scenario: Waiting conversation is rebound
- **WHEN** a waiting conversation acquires a new verified terminal destination
- **THEN** its wait time, acknowledgment, viewed state and palette remain unchanged and no duplicate alert is created

### Requirement: Presentation ownership for adopted terminals
Bell Tower SHALL change an adopted terminal's title or colors only when the user's configured presentation policy allows that surface and its destination, write mechanism and restoration behavior are verified. The control for adopted sessions SHALL be one global native-presentation setting that is on by default, can be switched off, keeps an explicitly saved value and applies equally to Claude Code and Codex direct sessions. When the setting is off or the policy does not allow a surface, adoption SHALL track the conversation without acquiring terminal presentation ownership. A conversation on a Codex shared backend SHALL NOT acquire terminal presentation in this change. A supported first-hook presentation SHALL be described as occurring after the first accepted event rather than before native startup. Loss of a verified destination SHALL stop further writes; restoration SHALL NOT write to a reused or unrelated shell. For an adopted direct session the title SHALL be restored to its prior value and a background Bell Tower wrote SHALL be reset to the terminal default rather than restored, because its prior value is not recorded.

#### Scenario: Native presentation default
- **WHEN** no native-presentation value has been saved
- **THEN** the setting is on for both Claude Code and Codex direct sessions, and an explicitly saved off value is kept

#### Scenario: Native presentation is off
- **WHEN** a plain Claude Code or standalone Codex session is adopted and the native-presentation setting is off
- **THEN** Bell Tower tracks and alerts for the session and leaves its terminal title and colors untouched

#### Scenario: Native presentation is on
- **WHEN** the setting is on and an adopted direct session has a verified agent process and shell
- **THEN** Bell Tower applies the session's title and its own identity palette background after the first accepted hook; when the session ends or the setting is switched off it restores the prior title and resets the background it wrote to the terminal default

#### Scenario: Shared-backend Codex with native presentation on
- **WHEN** the setting is on and a conversation runs on a Codex shared backend
- **THEN** no title or color is written and the limitation is reported separately from tracking

#### Scenario: Hook delivery is delayed until the first prompt
- **WHEN** the first accepted hook arrives after the native client has already displayed its initial interface
- **THEN** any permitted decoration begins after adoption and diagnostics do not claim pre-launch coloring

#### Scenario: Simultaneous native sessions are told apart
- **WHEN** the setting is on and two adopted direct sessions of either provider run at the same time
- **THEN** each tab shows its own session's palette background, matching that session's chip and badge

#### Scenario: Restoration target is gone or reused
- **WHEN** a session ends after its recorded shell has exited or its PID was reused by another process
- **THEN** Bell Tower discards the restoration record without writing any title or color

#### Scenario: Existing managed launch has explicit presentation controls
- **WHEN** an existing Bell Tower launch supplies valid title/background ownership and opt-out controls
- **THEN** adoption preserves those controls and existing verified pre-launch and exit restoration behavior

### Requirement: Provider-owned terminal titles
Bell Tower SHALL write its own terminal title for an adopted direct session only when the provider's own terminal title is in effect disabled for that session: the disabling setting is present in the provider's actual settings file, whether Bell Tower's opt-in step or the user set it, and the session started after it took effect. Without that step, the session SHALL keep its color and badge, Bell Tower SHALL write no title, and diagnostics SHALL state that titles are provider-owned. The rule SHALL apply equally to Claude Code and Codex.

#### Scenario: Title step applied
- **WHEN** the user has applied the opt-in title step for a provider and a direct session of that provider is adopted
- **THEN** Bell Tower's session title appears and persists while the session runs

#### Scenario: Title step not applied
- **WHEN** the opt-in title step is not applied for a provider
- **THEN** the session receives color and badge only, and doctor reports the title as provider-owned

#### Scenario: Setting reset or applied while a session runs
- **WHEN** the disabling setting is removed by the user, or applied while a session is already running
- **THEN** Bell Tower writes no title for sessions whose provider still writes its own, reports window-only navigation for them, and titles only sessions started after the setting took effect
- **AND** a removal and re-add made while the tray is not running is not observed, so a session started in that gap may still be titled; this limitation is stated in the installation and compatibility documentation
