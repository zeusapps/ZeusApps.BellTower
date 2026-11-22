# Session Identity Delta

## ADDED Requirements

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
Bell Tower SHALL write its own terminal title for an adopted direct session only when the provider's own terminal title is disabled through Bell Tower's opt-in setup step. Without that step, the session SHALL keep its color and badge, Bell Tower SHALL write no title, and diagnostics SHALL state that titles are provider-owned. The rule SHALL apply equally to Claude Code and Codex.

#### Scenario: Title step applied
- **WHEN** the user has applied the opt-in title step for a provider and a direct session of that provider is adopted
- **THEN** Bell Tower's session title appears and persists while the session runs

#### Scenario: Title step not applied
- **WHEN** the opt-in title step is not applied for a provider
- **THEN** the session receives color and badge only, and doctor reports the title as provider-owned
