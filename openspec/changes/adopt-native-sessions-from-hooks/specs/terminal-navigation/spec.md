# Terminal Navigation Delta

## ADDED Requirements

### Requirement: Verified terminal binding for adopted native sessions
Bell Tower SHALL attempt to bind adopted Claude Code and Codex conversations to their existing local terminal hosts using verifiable process and host evidence. Exact navigation SHALL require an unambiguous live client or shell identity matched to fresh host evidence and a verified owning window, including a fresh bridge registration where the host integration uses one. A navigation request for an adopted session SHALL always produce a visible result: focus at the reported precision or a stated reason. A conversation on a Codex shared backend SHALL be reported as terminal-unverified in this change. A hook payload, provider backend process, inherited terminal variable, directory, native title or loaded-thread record alone SHALL NOT establish that binding. Unverified or ambiguous bindings SHALL disable terminal actions without disabling independent conversation tracking.

#### Scenario: Direct agent ancestry identifies a VS Code shell
- **WHEN** an adopted native conversation supplies a creation-time-verified direct agent and shell relationship that matches one fresh VS Code terminal registration
- **THEN** Bell Tower uses the existing verified window and terminal routing policy without requiring a dedicated profile or launcher environment variables

#### Scenario: Shared backend serves several terminals
- **WHEN** a Codex hook originates from a shared backend
- **THEN** Bell Tower reports navigation as unverified, says so when the user clicks the session, and does not use the backend's parent shell or inherited terminal variables as the current destination

#### Scenario: Adopted direct session in Windows Terminal without a managed title
- **WHEN** the user clicks an adopted plain Claude Code or standalone Codex session whose tab title Bell Tower does not manage
- **THEN** Bell Tower brings the owning Windows Terminal window forward and reports window-only precision, or states why it could not

#### Scenario: Adopted direct session in Windows Terminal with a managed title
- **WHEN** native presentation is enabled and the user clicks an adopted direct session
- **THEN** Bell Tower selects that session's tab through its managed title under the existing tab-selection rules

#### Scenario: One Terminal process owns several windows
- **WHEN** the Windows Terminal process that hosts an adopted session has more than one visible window
- **THEN** the click focuses the verified owning window or reports a stated reason, and never ends without a visible result

#### Scenario: Terminal identity is replaced or moved
- **WHEN** a client disconnects, its shell PID is reused, its tab is moved, or its host registration expires
- **THEN** the former binding is invalidated and actions require fresh verification rather than a remembered name or tab index

#### Scenario: Mixed simultaneous host sessions
- **WHEN** Claude Code and Codex conversations run in multiple VS Code windows and Windows Terminal tabs with identical titles and directories
- **THEN** navigation targets only an independently verified owning destination or reports its actual partial or unavailable capability

### Requirement: Click results are always visible
Every navigation request that does not reach its exact destination SHALL show its reason in Bell Tower's own notice, independent of Windows notification settings. The notice SHALL be topmost without taking keyboard focus from the window the click focused. Clicks whose session is no longer known, or whose navigator fails, SHALL be logged and explained as well. The rule SHALL apply equally to Claude Code and Codex in Windows Terminal and VS Code.

#### Scenario: Notifications suppressed
- **WHEN** Windows Focus or Do Not Disturb is on and a click produces a window-only or refused result
- **THEN** Bell Tower's notice shows the reason and the focused terminal keeps the keyboard

#### Scenario: Stale or failing click
- **WHEN** a click refers to a session that is no longer tracked, or its navigator fails
- **THEN** a reason is shown and one log line records the outcome
