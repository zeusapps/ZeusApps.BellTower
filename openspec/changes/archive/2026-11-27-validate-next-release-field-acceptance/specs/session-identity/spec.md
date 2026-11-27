## MODIFIED Requirements

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
