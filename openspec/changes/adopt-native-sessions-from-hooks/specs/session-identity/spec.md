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
Bell Tower SHALL change an adopted terminal's title or colors only when the user's configured presentation policy allows that surface and its destination, write mechanism and restoration behavior are verified. Without an applicable opt-in, adoption SHALL track the conversation without acquiring terminal presentation ownership. A supported first-hook presentation SHALL be described as occurring after the first accepted event rather than before native startup. Loss of a verified destination SHALL stop further writes; restoration SHALL NOT write to a reused or unrelated shell.

#### Scenario: Hook delivery is delayed until the first prompt
- **WHEN** the first accepted hook arrives after the native client has already displayed its initial interface
- **THEN** any permitted decoration begins after adoption and diagnostics do not claim pre-launch coloring

#### Scenario: No safe restoration mechanism is available
- **WHEN** an adopted host can accept a color write but its prior presentation or safe restoration cannot be established
- **THEN** Bell Tower leaves that surface unchanged and reports the presentation limitation independently of tracking

#### Scenario: Existing managed launch has explicit presentation controls
- **WHEN** an existing Bell Tower launch supplies valid title/background ownership and opt-out controls
- **THEN** adoption preserves those controls and existing verified pre-launch and exit restoration behavior
