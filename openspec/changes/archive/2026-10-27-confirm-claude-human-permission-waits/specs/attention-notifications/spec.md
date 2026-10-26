# Human-confirmed attention delta

## MODIFIED Requirements

### Requirement: Completion and permission grace
Bell Tower SHALL delay completion attention for 20 seconds after eligibility. For both providers, approval-flow starts SHALL be tracked immediately as provisional and SHALL NOT by themselves establish human-wait attention. A current verified human-wait signal SHALL confirm permission eligibility. The shared five-second human-wait grace SHALL account for an already elapsed verified provider notification delay rather than adding another fixed delay; Claude's delayed permission_prompt can therefore be eligible on arrival while Codex's immediately observed approval status retains its confirmed grace. Missing confirmation SHALL report unverified coverage without asserting a human wait. When the current provider mode has no supported confirmation signal for a session, a request still pending after 15 seconds SHALL show a visibly unverified "possibly waiting for approval" chip that plays no sound and is not eligible for reminders or escalation; it SHALL never be presented as a confirmed human wait. Questions and terminal errors SHALL remain immediately actionable. Duplicate observations SHALL NOT extend deadlines and distinct episodes SHALL remain independent.

#### Scenario: A new prompt arrives during grace
- **WHEN** another prompt arrives before the completion grace expires
- **THEN** no completion notification is emitted for the old turn

#### Scenario: Permission is automatically approved
- **WHEN** approval-flow hooks arrive but execution resumes without verified human-wait evidence
- **THEN** neither client emits a human-permission chip, sound, reminder or escalation solely from elapsed hook time

#### Scenario: Permission is required
- **WHEN** a current Codex owning runtime immediately confirms human approval is pending
- **THEN** shared human-wait grace applies with the existing safe native transport and episode isolation

#### Scenario: Delayed human notification
- **WHEN** a current Claude human-prompt notification arrives after its provider delay already satisfies shared grace
- **THEN** it becomes eligible without another fixed debounce

#### Scenario: Native approval verification becomes unavailable
- **WHEN** the current provider mode cannot supply fresh human-wait confirmation
- **THEN** coverage is reported unverified, cached or timing-only evidence is not presented as confirmation, and a hard restriction requires version/mode proof and an honest fallback

#### Scenario: Confirmation after a dismissed unverified chip
- **WHEN** the user dismissed the unverified "possibly waiting for approval" chip and supported human-wait evidence later confirms the same request
- **THEN** the confirmed wait starts a fresh attention episode with normal announcement, and the earlier dismissal does not suppress it

#### Scenario: Claude session without permission prompt notifications
- **WHEN** a Claude approval request stays pending for 15 seconds in a session where no permission_prompt notification has been observed (for example because the provider's permission-prompt notification hooks are disabled)
- **THEN** the same silent unverified chip appears, and diagnostics name the disabled-notification setting as a known way the confirmation signal becomes unavailable

#### Scenario: Codex session without a verified owning runtime
- **WHEN** a Codex approval request stays pending for 15 seconds in a session whose owning runtime is unavailable, unverified or fails its status read
- **THEN** a visibly unverified "possibly waiting for approval" chip appears without sound, reminder or escalation eligibility, and it clears on the request's correlated resolution, stop or end

#### Scenario: A parallel permission request resolves quickly
- **WHEN** one of several approval requests resolves
- **THEN** only its supported correlated wait clears and the other request keeps its own deadline, dismissal and announcement receipts

#### Scenario: A managed Codex command is automatically reviewed or already executing
- **WHEN** the verified owning runtime reports no pending human approval while a hook request is provisional or stale
- **THEN** shared policy suppresses human-permission attention without answering the request or waiting for an unrelated result

## ADDED Requirements

### Requirement: Shared human-wait confirmation policy
Claude and Codex SHALL normalize their supported approval signals into the same provisional, confirmed, resolved and unverified states. Provider adapters SHALL retain exact attachment/turn targeting and evidence freshness. Existing native APIs and snapshot data SHALL be migrated safely, but historical timing-only behavior SHALL NOT be frozen as an exception to the shared feature.

#### Scenario: Matched approval lifecycle
- **WHEN** equivalent confirmed human waits and automatically resolved requests occur in both clients
- **THEN** the shared policy produces equivalent actionable outcomes with only evidenced source-timing differences

#### Scenario: Late confirmation
- **WHEN** a prompt notification or runtime result arrives after the supported wait or turn resolved
- **THEN** it does not recreate the old episode

#### Scenario: Restart
- **WHEN** confirmed attention is restored after tray restart
- **THEN** each provider restores it silently without inventing a new wait

### Requirement: Question-tool approval flows remain question waits
For both providers, an approval-flow or human-prompt signal correlated to a pending question-tool wait SHALL keep that wait a question and MAY confirm it; it SHALL NOT replace it with human-permission attention or restart its timing, announcement or dismissal receipts. Correlation SHALL use supported operation IDs, tool name plus input fingerprint, or prompt/turn scope. When a question wait and an uncorrelated permission request are both pending in the same scope, an unattributed prompt notification SHALL be recorded as ambiguous and SHALL NOT be assigned to either by guess, including by elimination after the question resolves; a permission request relies on its own later human-prompt evidence. A question-tool start that the provider rejects without showing a dialog SHALL clear on the turn's stop without leaving residual attention.

#### Scenario: Claude question dialog
- **WHEN** PreToolUse(AskUserQuestion) is followed by PermissionRequest(AskUserQuestion) without a tool_use_id but with the same input, and later by a permission_prompt notification
- **THEN** exactly one question episode exists, no human-permission chip or sound is emitted, no second announcement occurs, and the correlated PostToolUse clears it

#### Scenario: Codex question dialog
- **WHEN** PreToolUse(request_user_input) arrives with no approval-flow hook
- **THEN** a question episode exists and the correlated PostToolUse clears it

#### Scenario: Question and permission pending together
- **WHEN** a question wait and an uncorrelated permission request are pending in the same scope and an unattributed permission_prompt arrives
- **THEN** the question remains a question, the permission confirmation is recorded as ambiguous, and neither wait is cleared or reassigned

#### Scenario: Question answered while an ambiguous prompt is held
- **WHEN** the question resolves first while a permission request remains pending
- **THEN** the earlier prompt is not credited to the permission request by elimination, and the request becomes confirmed only from its own later human-prompt evidence

#### Scenario: Sequential question and permission dialogs
- **WHEN** Claude presents a question dialog and then, after it resolves, a permission dialog for a tool requested in the same message
- **THEN** the question's own prompt confirms only the question, and the permission is tracked from its own approval-flow start

#### Scenario: Codex question rejected in Default mode
- **WHEN** PreToolUse(request_user_input) is followed by Stop without PostToolUse
- **THEN** the question wait clears at Stop and is not restored, reminded or escalated

#### Scenario: Mixed question and permission sessions
- **WHEN** a Claude question and a Codex permission are pending simultaneously
- **THEN** each keeps its own kind and independent episode

### Requirement: Provider parity for human-wait attention in attention-notifications
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
