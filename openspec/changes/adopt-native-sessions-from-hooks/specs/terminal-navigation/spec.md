# Terminal Navigation Delta

## ADDED Requirements

### Requirement: Verified terminal binding for adopted native sessions
Bell Tower SHALL attempt to bind adopted Claude Code and Codex conversations to their existing local terminal hosts using verifiable process and host evidence. Exact navigation SHALL require an unambiguous live client or shell identity matched to fresh host evidence and a verified owning window, including a fresh bridge registration where the host integration uses one. A hook payload, provider backend process, inherited terminal variable, directory, native title or loaded-thread record alone SHALL NOT establish that binding. Unverified or ambiguous bindings SHALL disable terminal actions without disabling independent conversation tracking.

#### Scenario: Direct agent ancestry identifies a VS Code shell
- **WHEN** an adopted native conversation supplies a creation-time-verified direct agent and shell relationship that matches one fresh VS Code terminal registration
- **THEN** Bell Tower uses the existing verified window and terminal routing policy without requiring a dedicated profile or launcher environment variables

#### Scenario: Shared backend serves several terminals
- **WHEN** a Codex hook originates from a backend shared by multiple terminal clients and no exact conversation-to-client binding has been verified
- **THEN** Bell Tower reports navigation as unverified and does not use the backend's parent shell or inherited terminal variables as the current destination

#### Scenario: Shared-Codex conversation bound through its client endpoint
- **WHEN** a shared-backend Codex conversation's own client endpoint is owned by one live interactive client with a backend connection to it, and that conversation is the client's most recent binding
- **THEN** Bell Tower binds the conversation to that client's shell through the existing fresh host registration

#### Scenario: Shared-Codex client no longer verifiably displays a conversation
- **WHEN** the owning client has bound a newer conversation, another client joined the conversation without a new binding, or the endpoint owner or its backend connection is gone
- **THEN** the older binding is unverified and terminal actions are disabled while conversation tracking continues

#### Scenario: Terminal identity is replaced or moved
- **WHEN** a client disconnects, its shell PID is reused, its tab is moved, or its host registration expires
- **THEN** the former binding is invalidated and actions require fresh verification rather than a remembered name or tab index

#### Scenario: Mixed simultaneous host sessions
- **WHEN** Claude Code and Codex conversations run in multiple VS Code windows and Windows Terminal tabs with identical titles and directories
- **THEN** navigation targets only an independently verified owning destination or reports its actual partial or unavailable capability
