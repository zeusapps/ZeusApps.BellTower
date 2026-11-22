## MODIFIED Requirements

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
