# Design

## Context

See proposal.md for motivation and the five capability specs for acceptance behavior.

The repository currently contains OpenSpec and agent workflow configuration, with no application source or existing capability specs. The user supplied a behavioral description of a .NET Windows tray PoC running on a separate work machine. Its source, custom Claude badge, launcher profiles, and reported tests were not available at the supplied path here. This change therefore specifies a standalone implementation; available PoC source can be reused after inspection without making it a prerequisite.

Confirmed user priorities are an attention-only strip, a useful tray overview, support for Claude Code and Codex in both VS Code terminals and Windows Terminal, and a strong visual connection through the same session name and brighter accent over a dark session background. The user is considering branch, worktree, or both; the proposed default is branch plus linked-worktree name, with both kept out of compact chip text.

The former PoC's storage under ~/.claude/ is a legacy deployment convention. This design proposes app-owned storage so a Codex-only user needs no Claude installation. No existing installation is moved by planning or by normal startup.

## Goals / Non-Goals

**Goals:**

- Separate provider observations, session identity, alert policy, terminal presentation, and navigation.
- Preserve causal attention state when hooks overlap, background agents run, the tray restarts, or terminals disappear.
- Make integrations installable and diagnosable as part of Bell Tower.
- Keep unsupported decoration and navigation visible as capability limits.

**Non-Goals:**

- Hosting an agent loop, taking over approvals, or replacing either agent's terminal UI.
- Patching agent binaries to obtain an identical footer.
- Interpreting arbitrary chat prose as proof of a question or inferring completion from inactivity.
- Requiring OpenSpec, network services, or a separate mod framework.
- Adding an MCP server, WorkLog connectivity, cross-project shared libraries/services, per-repository managed instruction initialization, or release discovery/self-update automation in this change.

## Decisions

### 1. Retain a Windows .NET desktop implementation

Use a small Windows tray process with WinForms for the strip and tray controls, a separately testable core, and one-shot CLI commands for events, identity management, setup, and process management. Retain the PoC's single-instance behavior and Windows speech approach when source and dependency inspection confirm compatibility.

Suggested source areas are Core, Providers, Hosts, IO, Tray, and Setup, plus a VS Code bridge and behavior-focused tests. Use D:/Code/ZeusApps.WorkLog as a structural reference: one executable project, responsibility-based folders, a separate test project, and isolated validation. Bell Tower remains an independent product with no WorkLog runtime or source-project dependency. Select the concrete supported .NET target during implementation after checking installed tooling; this plan does not assert a target framework was already chosen.

Alternatives considered: a browser dashboard adds no value for the immediate strip; Electron would introduce a larger runtime; embedding Codex app-server would change session ownership. A native tray fits the established interaction.

### 2. Bell Tower owns identity

Store provider-neutral metadata independently of adapter event state:

| Record | Key information |
| --- | --- |
| Session | Provider and provider session ID; display name; optional change association; palette |
| Attachment | App-generated attachment ID; process identity and creation time; launcher shell; host registration; current checkout |
| Observation | Provider event; attachment; available turn/tool/subagent IDs; source time and receipt time; deduplication data |
| Activity | Foreground activity and known background tasks |
| Attention | Outstanding waits; selected visible reason; wait start; completion eligibility; episode and acknowledgment |
| Host capability | Supported badge/background/title surfaces; exact, window-only, or unavailable navigation |

Use provider plus session ID as conversation identity, and a separate attachment ID for a running launch. A launcher PID is evidence for an attachment, not a durable conversation key. Resuming the conversation restores its stored name and palette; a new conversation receives a new palette. Reject stale attachment traffic after liveness reconciliation.

The primary palette is a random hue with a dark background and a brighter related chip accent. Choose foreground colors using a contrast check; proposed implementation threshold is 4.5:1 for ordinary label text. Avoid visually identical accents among currently running sessions where practical. Do not encode waiting/working state by replacing the session's identifying accent.

Name precedence is explicit manual name, explicitly selected OpenSpec change, then checkout directory. Do not guess among multiple active changes or parse prompts to discover one. Offer identity editing and change association in the tray overview and CLI; automatic agent-driven naming through MCP or managed instructions is deferred in handoff.md. Git metadata is queried for the attachment's checkout, including subdirectories and linked worktrees; refresh on relevant events and overview access using a bounded cache. Show short commit for detached HEAD, and omit absent Git fields.

Alternative: treating the existing badge file as the owner would keep the external dependency and give Codex an agent-specific storage contract. Legacy files are import inputs only.

### 3. Normalize observations through a durable local inbox

Keep hook work bounded: validate, extract necessary fields, and atomically publish a small event record to an app-owned inbox. Hooks do not return approval decisions or control agent execution; any event-specific successful output must follow that provider's current contract.

The tray is the sole reducer of normalized state. File notifications wake it, with periodic reconciliation covering missed notifications. Atomic snapshots persist identity, attention episodes, acknowledgment, and relevant timestamps. Inbox records remain until included in a committed snapshot; replay is idempotent. Start with local files to retain the PoC's simplicity and delivery while the tray is down; a database is unnecessary for this scale.

Receipt order alone is not causal order. Use turn, tool-call, attachment, and subagent correlation where exposed. Obsolete events cannot overwrite a newer turn or attachment; unrelated parallel tool output cannot resolve an outstanding prompt. When the provider lacks adequate evidence, report reduced observation coverage rather than fabricate a definitive transition.

### 4. Track attention separately from background work

Foreground activity, background task membership, and attention are independent. A confirmed question or permission wait is visible even with active subagents. Completion starts its 20-second grace only once known subagent/workflow/monitor work no longer suppresses it; shell work alone does not suppress it.

Model outstanding permission/question operations separately so a tool result clears its own blocker. A user prompt clears the applicable foreground wait; generic background tool output cannot clear it. Permission candidates must account for other hooks or policies resolving a request before a prompt is actually shown. A tool's recoverable error is distinct from an agent's terminal failure.

Use one visible chip per session. The proposed reason order is terminal error, permission, question, then completion. Keep other outstanding blockers in session detail. An attention episode starts when a wait appears. A distinct new blocker at the displayed priority renews the episode, while duplicate observations and metadata updates preserve it. Persist the episode start independently of the oldest unresolved wait so stale blockers cannot silence a new request. Resolution followed by a new wait starts another. Right-click acknowledgment persists for the episode; focus alone does not acknowledge it.

### 5. Provider adapters use verified capabilities

For Claude, begin with the supplied hooks: UserPromptSubmit, PostToolUse, Stop, StopFailure, Notification, and SessionEnd, adding registration and background-task observations where supported. Verify actual input shapes against the installed version; the pasted description is a baseline, not inspected source.

For Codex, official documentation currently describes lifecycle, prompt, permission, tool, and subagent hooks. Background hook invocations can finish out of order, and session-end delivery has distinct execution rules. Treat hooks as observations; trust/setup and installed-version coverage require verification. The exact observation of question prompts, approval dismissal/denial, failures, and background waits is an early bounded probe, with sanitized fixtures and explicit capability outcomes. Do not assume a Claude Notification equivalent. [OpenAI hook documentation](https://learn.chatgpt.com/docs/hooks)

Prefer documented events. A transcript reader, if ever needed, must be opt-in, version-specific, local, and separately reported; it is not the default adapter. No SDK agent session is created to observe an existing CLI conversation.

### 6. Bundle presentation bridges with graceful limits

Claude's custom chip can be reproduced through its supported status-line surface if the installed version allows the needed presentation. Inspect the existing mod when available to preserve details; implement the same small behavior within Bell Tower rather than require the personal mod installation.

Codex exposes built-in footer fields, session naming, and terminal-title composition. Those features do not establish an arbitrary colored-chip extension point. Use a supported title surface as the baseline fallback, and enable a custom footer only after a probe verifies a supported mechanism. [Codex customization commands](https://learn.chatgpt.com/docs/developer-commands)

For either provider, terminal background/title effects belong to the host integration. Apply them only when scoped to the intended session and restore the prior host setting when the integration owns that change. Never recolor unrelated terminals or replace global themes to simulate per-session backgrounds. Report unsupported footer, title, or background surfaces individually. Styling must never corrupt an agent's screen rendering.

### 7. Route focus through registered host capabilities

A host registry maps attachment IDs to live host destinations. The VS Code bridge registers a window instance and its terminals. Focus requests carry a unique request ID, destination attachment, and expiry; only the owning bridge acknowledges and reveals the matching live terminal. Do not identify a terminal by title.

For Windows Terminal, capture available session/window evidence at registration and verify a window before activating it. The documented CLI targets windows and tab indices, and can create a window if a requested target is absent; raw saved indices and blind CLI activation are insufficient. [Microsoft terminal command-line documentation](https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments)

An early probe evaluates available host APIs and, if necessary, Windows UI Automation for stable resolution without terminal input injection. Exact navigation is enabled only after tests for reorder, pane changes, close, and multiple windows. Otherwise offer labeled window-only navigation; if even the owning window is unverified, disable the action and retain the attention chip. This outcome is within the spec and does not block event tracking.

### 8. Keep alert policy and audio independent of presentation

The scheduler derives due attention from persisted times and a testable clock. Startup establishes a silent restoration boundary: already-due waits display without sound; future waits follow normal eligibility. Persist episode acknowledgment, not an audio backlog. Before playback, recheck that the wait remains actionable.

Use one audio queue. Embed the four user-supplied default tones (permission/doorbell, question/magic ring, completion/interface, error/fail), retaining the classic in-memory chime and one copied custom WAV as alternatives. Validate source and converted sizes before accepting a custom tone; aggregates use the highest-priority reason's tone; speech uses the installed Windows voice, preferring the PoC's Microsoft Zira voice when present and falling back to chime only when speech is unavailable. Do not enable invariant globalization in a way that prevents voice loading. Collapse more than two pending session phrases to an updated waiting count. Mute suppresses playback; voice=false preserves chimes. Do not replay muted or restored episodes when unmuting.

The strip is topmost and nonactivating; explicit clicks can request focus. Chips use one horizontal name/reason/time row with centered dot separators, Git context stays in tooltips, and the drag handle has three vertical dots. Successful chip navigation hides only the viewed episode timer and preserves provider attention; persist this presentation state with an episode guard. Use the generated icon for the executable, tray and overview. Clamp its saved position to current display bounds on startup and display changes. Tray lifetime must be independent of the shell that initially starts it.

### 9. App-owned setup and compatibility migration

Proposed default root: %LOCALAPPDATA%/ZeusApps.BellTower, with config.json, identity/session snapshots, position, bounded inbox/runtime files, and logs capped at the PoC's 256 KB policy. Mute defaults to unmuted and voice enabled for normal future operation; setup and validation themselves emit no audio. Runtime location can be overridden for an explicitly chosen legacy deployment.

Setup covers the event hooks, launcher registration, and supported display/focus bridges required for this standalone app. A WorkLog-style per-repository init workflow, managed agent instructions, and MCP registration are deferred; they are not prerequisites for tracking. Setup presents owned entries and backs up affected user configuration before an explicit install action. Preserve unrelated hooks, profiles, and extensions; do not overwrite an existing custom status line silently. Detect existing Bell Tower hook registrations to prevent duplicate ingestion and audio. Respect provider hook review and trust. Diagnostic UI reports incomplete setup rather than claiming unsupported observations are working.

Offer explicit import of valid legacy names, colors, voice, and position; never read legacy paths as a perpetual dependency or infer conversation identity from a stale PID filename. Keep the original installation available for rollback. Startup does not automatically scan or modify another agent's home directory.

## Risks / Trade-offs

- Codex may not expose every wait transition on the installed version -> probe explicit scenarios first, version capabilities, and report gaps; no prose-based inference.
- Hook delivery may race or end abruptly -> durable inbox, correlated reducer, idempotent replay, process-lifetime reconciliation.
- The original source and badge implementation are unavailable -> preserve the described behavior in specs and inspect any recovered source before reuse; do not assume its tests cover new integrations.
- Windows Terminal may expose only partial targeting -> verified window-only or unavailable navigation is explicit; exact pane selection is a capability, not a guessed index.
- Host-specific background support varies -> scoped decoration and capability reporting preserve operational tracking.
- Foreground activation may be restricted by Windows -> report unsuccessful navigation without marking the wait resolved.
- Multiple legacy and new hooks can duplicate notifications -> setup detection and event/episode deduplication.
- File state can be corrupt or temporarily unreadable -> atomic replacement, schema validation, last-good snapshots, bounded diagnostics without prompt/tool payloads.

## Migration Plan

1. Probe installed provider and host behavior in disposable sessions; record versioned capability results and sanitized event fixtures. Remain silent.
2. Implement the core and app-owned identity without depending on the old installation; reuse inspected PoC components where available.
3. Package hooks and host bridges, then verify all four provider/host combinations with the stated fallbacks.
4. Offer explicit legacy metadata import and integration installation with a reviewable diff. Leave the old setup untouched until the user chooses migration.
5. Document a manual update procedure: stop Bell Tower only, publish/replace, and restart it. Never stop or signal user agents or terminal hosts. Do not build a release checker, staging helper, or self-updater as part of this change.
6. Roll back by stopping the new tray, restoring backed-up owned configuration entries, and restarting the old Bell Tower if requested. Retain new metadata for subsequent migration.

## Deferred Integration Handoff

See handoff.md for candidate integrations observed while inspecting WorkLog. These are explicitly deferred discussion topics, with no corresponding implementation tasks in this change. Required Claude/Codex event hooks and VS Code/Windows Terminal bridges remain in scope. The implementation must not read WorkLog's journal, require its installation, or infer journal activity/completion from Bell Tower waits.

## Open Questions

No product decision blocks implementation. The remaining implementation probes select a supported presentation/targeting mechanism or the already-specified fallback; they do not authorize expanding scope. Final palette tuning and the installed speech voice can be settled during visual review.

