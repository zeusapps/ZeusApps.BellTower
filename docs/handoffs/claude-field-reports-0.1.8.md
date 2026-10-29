# Claude field report source notes: release 0.1.8

Supplied by the user on 2026-10-23 after testing on the work machine. This file preserves relevant facts from two reports for fresh implementation contexts. It contains no raw transcripts, credentials or production state files. The [release handoff](next-release-plan.md) owns execution order and confirmed decisions; each OpenSpec change owns normative requirements.

## Scope amendment: provider parity

The user subsequently requires one product with Claude/Codex feature parity by default. Earlier instructions in the reports or prior plan to keep Codex features unchanged are superseded. The field measurements remain Claude-specific evidence; each repair/addition now covers both integrations through shared policy, with only version/mode-scoped hard restrictions accepted after proof and an honest fallback. Missing captures or implementation is not proof of a restriction.

## Evidence provenance

- Field analysis was reported as read-only against installed 0.1.8, its app data, Claude transcripts and source.
- Local planning inspected the repository and current official documentation. It did not repeat the field performance or authenticated provider tests.
- Old live-acceptance records describe earlier accepted behavior. The new work-machine report is contrary evidence for this environment, especially naming and VS Code foreground activation.
- The retained requirements in these reports, the original activation issue and legacy badge parity were explicitly requested for the next release. Splitting changes does not defer them. The user's 2026-10-29 scope decision cancelled remote webhook notifications, including Teams, while retaining local reminders; this supersedes the earlier remote notification request.

## Field environment and observed result

Windows 11 via RDP; Windows PowerShell 5.1; VS Code 1.141; native Claude Code 2.1.30x; OpenSpec 1.13.2. No Codex is installed on the field machine.

Four or five Claude sessions use the profile Claude PM (D360), which calls Start-ClaudeCodePM and launches plain claude. A user setting selects the custom main agent pm. All start in C:/Code/D360, while ticket work may belong to C:/Code/D360-worktrees/DEP-XXXX. Changes are under openspec/changes/<id>/.

Installation was bell-tower setup --desktop --clients claude --apply on 2026-10-22 at 14:39Z. The user restarted current conversations after setup. Every badge displayed D360 and the main checkout branch, with no ticket worktree.

Chip clicks selected the correct VS Code terminal but left its editor window behind another application or minimized. The user explicitly clarified this is foreground activation failure.

## Hook model relevant to the repair

The report says Claude's Windows command hooks are run through Git Bash, adding a shell before the configured command. Synchronous hooks block Claude until completion/timeout. Async command hooks return control immediately; current official documentation confirms timeout is not enforced for async hooks, so async alone does not establish fast/durable delivery.

Common hook fields include session_id, transcript_path, cwd, permission_mode and hook_event_name. Tool events include tool_name, tool_input, tool_use_id and post-tool tool_response. Whether a custom main-agent session supplies agent_id was not captured; verify before treating every agent_id as subagent context.

SessionStart distinguishes startup/resume/clear/compact. Resume retains the native conversation ID. The transcript is append-only JSONL with incomplete trailing lines possible.

Claude uses Bash and a separate PowerShell tool on Windows. File work uses Write/Edit file_path; the legacy mod also handles NotebookEdit. Skill calls use skill/args. Relevant workflows include opsx:propose, opsx:apply, opsx:update, opsx:archive and explain-change. A literal first argument may identify the change folder; its semantics still determine whether it is work or inspection.

Stop carries last_assistant_message, background_tasks and possibly session_crons. Known background types subagent/workflow/monitor suppress completion; shell work alone does not. Stop does not represent user interruption. Preserve existing background/foreground distinction.

PermissionRequest starts an approval flow that can resolve automatically. Notification/permission_prompt is delayed by roughly six seconds of no typing after a human prompt; keystrokes postpone it in terminal sessions. idle_prompt is about sixty seconds after completion, and elicitation_dialog covers question forms. Current documentation also notes notification delivery may lack operation IDs or be delayed behind another dialog. A late notification is not proof a particular unresolved parallel tool still exists.

## Findings 1-6: event and process reliability

### 1. Slow startup loses hooks

Reported full-chain median 5.0 s, maximum 6.5 s; actual event work about 0.1 s. Configured timeout three seconds lost approximately 80 of 110 pre-tool hooks and 68 of 103 post-tool hooks. At ten seconds, successful hooks still took roughly 6.3-6.7 s and about fifteen percent timed out.

The source uses Git Bash/provider execution, an encoded PowerShell wrapper, the stable launcher and the full versioned application. Reported executable sizes were about 73 MB launcher and 175 MB payload. The launcher serializes selection/lease startup through its installation gate.

Proposed repair: direct small NativeAOT ingress, neutral response, durable inbox, deferred tray processing and async Claude setup. Target installed median below 300 ms. Test the whole installed path and durable event count, not only an executable from the build tree.

### 2. Processing order matters

HookAdapter assigns time at processing. OpenSpec selection demands matching pre receipt before successful post. Killed-hook tests reportedly lost an event at 1.2 s and wrote one late at 2.5 s.

Repair must retain source/ingress evidence, accept sufficient successful post-only work evidence and make late/duplicate events causally safe. Merely assigning another processing-time timestamp does not solve async reordering.

### 3. Title refresh churn

A new sync-title process is started per live session per overview refresh, and each creates a new GitReader, defeating shared caching. The report counted 691 title-unavailable messages, about 95 per ten minutes.

Cache/schedule in the tray, skip unchanged/unverified destinations and back off failures. Preserve isolated console writing because AttachConsole is process-wide. Check zero children/Git work on unchanged ticks and bounded logs/helper cleanup.

### 4. Unowned sessions stay working

Two attachment records lacked both agent and shell process identities after VS Code terminal restart. No detach ran and the reconciler had no known process death, so they remained working and navigation unavailable.

Later-event agent recapture is already partially present in source. Durable shell ownership needs separation from the consumed/deleted palette launch record. Incomplete managed Claude attachments become unavailable after a configurable evidence grace, with fresh verified repair possible; inactivity alone must not declare a known-live agent dead. Classify intentionally unowned modes consistently for both clients, preserving safety and exposing evidenced capability limits.

### 5. Bridge temp-file leak

The report counted 114 orphan .tmp files in hosts. atomic() does not clean up after rename failure, and the exception aborts the tick before focus handling.

Check cleanup on injected rename failure, later-tick recovery and no indefinite focus starvation. This is separate from proving owning-window foreground activation.

### 6. Opaque rejection logs

Six hook-rejected lines had no cause. Retain sanitized exception category, not the exception message. doctor should show recent verified transcript timeout counts and unverified process health, using bounded read-only scanning.

## Hand edits on the field machine

All twelve owned Claude hook handlers were changed to timeout:3 and async:true. Setup originally wrote timeout:3 synchronously.

The report assumed setup would recognize these as user edits. Source inspection found handler ownership currently compares command text, so it may replace edited fields silently. The new migration must compare owned handler structure and offer an explicit previewed replacement while preserving unrelated hooks, backups and repeatability. Do not run setup against production to test this.

## Findings 7-9: Claude identity

### 7. Native custom title

The user ran /rename dep-7070 at 14:47:35Z, and the report found three custom-title transcript records. Bell Tower reads none. Exact record property names remain unverified; obtain a minimal fixture first.

Confirmed planned precedence is Bell Tower explicit full title > explicit Claude custom title > automatic role/subject. Provider-generated session_name is not automatically equivalent to an explicit user title.

### 8. Real work uses unsupported forms

Out of roughly 265 main-session OpenSpec calls since October 8, sixteen matched accepted direct forms. Reported unsupported shapes:

| Shape | Count |
|---|---:|
| cd X && openspec ... | 113 |
| Other chains | 36 |
| Pipe to head/grep | 32 |
| Redirected output | 22 |
| PowerShell tool | 10 |

OpenSpec 1.13.2 status/instructions JSON did supply changeName, schemaName and absolute root.path; a replay of a matching command selected correctly. Failure is not necessarily the JSON schema.

The resolver must support verified successful work and explicit bounded command forms, edit/skill evidence and transcript recovery. A DEP-7070 conversation's older October 20 evidence should be recoverable on first-install resume. Branch/ticket fallback may use directory names or explicit Branch:/Ticket: headers in review.md, bugs.md or craft.md; arbitrary proposal mentions do not establish ownership.

The user subsequently confirmed inspecting another change must not replace the established badge association. Do not blindly expand every read-only CLI call into a rename.

### 9. Work branch differs from launch branch

The DEP-7070 conversation starts in D360, whose branch was feature/DEP-7483-..., while its work belongs to D360-worktrees/DEP-7070 on feature/DEP-7070-....

Display a separately associated verified working checkout. Git context should not be changed by substituting an external OpenSpec planning store or moving process ownership to a path.

## Findings 10-13: attention and trust

### 10. Human approval confirmation

Use current Claude permission_prompt notification as human-wait confirmation rather than automatically alerting fifteen seconds after PermissionRequest. Preserve the provider's notification delay, provisional tracking, causal clearing, parallel ambiguity and the shared provider confirmation policy using each supported signal adapter.

### 11. Speech during calls

When current-session microphone use is detected, keep tones and suppress speech. Use playback-time checks, explicit unknown/failure behavior and fake probes in tests. Do not capture microphone audio.

### 12. Local reminders

Repeat unresolved unacknowledged attention after configurable time plus no keyboard/mouse input in the current interactive session. Persist per-episode receipts and preserve silent restart.

The remote notification portion of the original request was cancelled by the user on 2026-10-29. No remote sender, endpoint credential storage or delivery acceptance is planned. Local reminder behavior remains required for both providers.

### 13. Publisher authentication and managed checks

Add signature verification beyond SHA-256 and expose a setup control for automatic discovery on managed machines. Current source already requires explicit install and supports updates.enabled:false for discovery; these are not new unattended-install semantics.

The plan chooses detached minisign-compatible authenticated metadata, an embedded trust root, protected publishing and a documented unsigned 0.1.8 bootstrap boundary. Production signing key provisioning is an external release prerequisite.

## Legacy session-badge behavior reference

This report describes the previous personal mod/PoC, not runtime code present in standalone 0.1.8.

### Launch and shared file

Personal launchers claude-pm/dev/qa/apm call Invoke-ClaudeSession in Documents/WindowsPowerShell/Microsoft.PowerShell_profile.ps1. They set CLAUDE_BADGE_ROLE, CLAUDE_BADGE_FILE under TEMP/claude-badges/<launcher PID>.json and CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1.

The mod lives in ~/.claude/mods/session-badge, hooks/badge.ts and hooks/register.tsx. It writes title/background/accent; a nonlauncher session has no badge file.

### Naming

Pinned /badge title wins. Otherwise title is role:subject, with subject change > ticket > branch, falling back to Claude if no role/subject. Control characters are removed, text trimmed and title limited to eighty characters.

Change precedence:
1. Pinned /badge change.
2. Latest change touched by this conversation's successful Write/Edit/NotebookEdit path under openspec/changes/<id>/ outside archive, or an eligible workflow Skill naming an existing folder.
3. Unique branch-change mapping by ticket-containing folder or explicit Branch:/Ticket: header in review.md, bugs.md or craft.md.

Reads/searches do not count. Start/resume scans latest tool history first. Branch change clears touched evidence. Ticket syntax is an uppercase project-like prefix and a hyphen-number, for example DEP-7483. Branch is from the session checkout.

Example: PM on feature/DEP-7483-ttyd-download-chips with an appropriate metadata header displays PM: ttyd-download-chips.

The new shared design preserves verified explicit native user titles as a tier for both clients and extends the same work/inspection and recovery policy to both, with safe native ownership.

### Color and refresh

/badge color accepts a named color, hex or random. Random avoids accents already used by other badge files when possible. Pinned metadata uses native session ID and resume retains its color. The old store retained fifty sessions; that arbitrary retention count is implementation history, not permission to drop still-useful resumed pins.

Background uses OSC rgb form and accent is hex. /badge reset clears pinned title/change and selects another color.

Recompute at start/resume/clear/fork, after eligible work and at turn end. Write only changed content. Standalone Bell Tower should own equivalent controls/identity and does not need a dependency on the mod or its temp-file watcher.

### Consumers

The old watcher polls every 500 ms and emits terminal title/background OSC sequences. VS Code uses the sequence title template and profiles without overrideName. The mod's band chip displays colored title plus dim ticket/branch.

Old alert state points to badgeFile; its tray reads title/accent and falls back to cwd/gray. New Bell Tower already has an internal shared identity store, so preserve the unified behavior through its own resolver/projection.

Speech splits role at colon, turns hyphens/underscores into spaces, uses at most four subject words and appends reason. Example: PM, ttyd download chips, needs permission. Avoid duplicating roles when migrating legacy full titles.

Explicit import currently accepts only hex backgrounds; add normalization for supported OSC rgb strings without emitting arbitrary escape sequences.

## Facts still requiring targeted verification

- Actual Windows hook shell/quoting and timeout teardown behavior for the installed Claude version.
- Custom main-agent agent_id/agent_type fields versus true subagent fields.
- Exact custom-title and timedOut/timeoutMs transcript record fields.
- Platform microphone registry freshness and current RDP session coverage.
- Instance-bound VS Code HWND ownership and actual foreground permission behavior.
- Current minisign verifier format/dependency.
- Representative installed NativeAOT ingress performance.

These are specific contract/acceptance tasks in the changes, not a reason to re-run all previously confirmed app behavior or defer required release features.
