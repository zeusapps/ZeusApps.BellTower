# Design

## Context

See `proposal.md` for motivation and `specs/session-identity/spec.md` for the behavior contract.

- `SessionIdentity.Name` already resolves `ManualName ?? Change ?? DefaultName`. `IdentityStore.Get` only consumes initial metadata when creating an identity; subsequent hooks cannot change an existing association through that method.
- `HookIngestor` receives provider-qualified session identity and bounded hook JSON. `HookAdapter` deliberately retains normalized lifecycle metadata rather than raw tool inputs. Hooks already cover tool completion and distinguish background agents.
- `SessionCatalog.Rename` edits identity independently of attention state. Its separate read/save calls will need an atomic update path to avoid races between automatic selection and manual edits.
- Claude's bundled `StatusLineReader`, the overview, and managed terminal titles already read Bell Tower identity. `GitReader` detects branch/detached HEAD and linked-worktree directory names. Git context currently uses the recorded session directory.
- Setup owns Claude's command-backed status line but currently writes only Codex hooks, not Codex footer settings. Ownership and conflict handling already exist for installed configuration.
- Documentation lookup found native `thread-name` and `git-branch` footer items and the `thread/name/set` API. The installed Codex CLI 0.163.0 binary contains `thread-name`, `thread-title`, `thread/name/set`, and `thread/name/updated`. These are evidence of available concepts, not evidence that an external writer can refresh a running TUI.
- Managed local Codex launches currently use `--no-daemon` to establish a reliable terminal attachment. A separate app-server may update stored metadata without notifying that running TUI. This must be checked before promising live footer synchronization.

## Goals / Non-Goals

**Goals:**
- Reuse existing session identity and display paths, adding a bounded selection observer and an optional Codex presentation adapter.
- Keep session identity, checkout context, native presentation ownership, and attention state separate.
- Verify live transport and footer behavior before enabling the Codex integration on an installation.

**Non-Goals:**
- Parse arbitrary conversational text, evaluate shell programs, or infer selection from the newest change directory.
- Install project instructions, add a Bell Tower MCP service, depend on WorkLog, or modify OpenSpec's skills.
- Patch or draw over Codex's renderer, inject keystrokes, replace its session storage files, or attach new managed sessions to an unrelated shared runtime.
- Require custom native footer colors or make Git branch/worktree labels part of the stored change association.

## Decisions

### 1. Observe explicit selections through the session's existing hooks

Add a provider selection reader alongside lifecycle normalization. For successful main-session tool completions, recognize a narrowly defined direct OpenSpec invocation that explicitly identifies one change. Initially support `new change <name>` and `status`/`instructions` with a literal `--change <name>` or `--change=<name>`, including ordinary quoting and a literal `--store` where present. Retain only the selected change, resolved planning root/store reference, provider/session key, and ordering receipt. Validate the selection against successful structured command results or the resolved change metadata. Do not invoke OpenSpec on every hook.

Ignore listing, capability reads, failures, background-agent selections, ambiguous command sequences, substitutions, and unsupported shell wrappers. Do not execute or expand input to recognize a command. A read-only invocation that explicitly addresses one change counts as selection; this is a proposed initial convention for the feature. Keep the existing explicit `rename --session ... --change ...` path available when selection is not observable.

Use authenticated local hook shape examples from both installed providers during the first implementation check to fix the exact success, tool-name, command, and working-directory fields. Nested orchestration that does not expose an unambiguous underlying command remains unsupported and must be documented rather than guessed. Do not persist raw command strings, full tool responses, or transcripts.

Alternative: a repository-wide active-change file is easy to inspect but cannot distinguish parallel sessions. Agent-managed instructions or MCP calls could express selections more broadly, but add installation and behavioral dependencies outside this change. General transcript inference has both ambiguity and privacy costs.

### 2. Update identity atomically, independently of lifecycle reduction

Add an identity-store update operation that holds the identity lock across read, merge, and write. Automatic selection updates `Change` and its minimal provenance/ordering receipt, without clearing `ManualName`, replacing `Palette`, or publishing a new attention event. Reject stale or duplicate receipts. If the provider supplies no reliable ordering, serialize selection handling for that session and accept only a confirmed result for the latest known selection operation.

Persist only additive optional fields so existing identity JSON remains valid. `ManualName > Change > DefaultName` remains the resolution rule. A manual name can be cleared through the existing UI/CLI to reveal the latest association. Selection alone does not change directory: refresh Git context from verified session/command checkout metadata, never from the location of an external OpenSpec store.

Alternative: updating launch environment variables cannot reach already-running sessions and would not update existing identities. Routing metadata through the attention reducer would unnecessarily couple naming to wait transitions.

### 3. Format context separately for each presentation surface

Retain the concise Bell Tower name for attention chips. Claude's badge and managed tab titles continue to append Git context through their existing presentation paths.

For native Codex, use `thread-name` for the resolved short name, with ` | worktree: <name>` appended to the native presentation name only for linked worktrees when no verified dedicated worktree item exists. Use native `git-branch` for the branch, avoiding duplication in the chat name. Preserve the underlying manual name and `Change` exactly as identity fields; keep any last-written native name in separate presentation bookkeeping.

Illustrative footer content, using Codex's own separators and styling:

```text
follow-openspec-session-titles | worktree: title-fix   feature/titles
```

Sanitize external text using the existing bounded identity conventions. Avoid truncating a long short name in a way that silently removes the entire worktree label: reserve bounded space for the optional suffix. Omit absent Git context. Refresh the derived presentation on identity or checkout changes; native-only styling does not replace the saved Bell Tower palette.

Alternative: `current-dir` is useful optional context but does not reliably name the checkout root when the session works in a subdirectory. Embedding the branch into the chat name would duplicate a field Codex already maintains and create unnecessary rename writes.

### 4. Verify and capability-gate Codex live name synchronization

Implement a small adapter using Codex's supported native name API, targeting the exact provider thread ID through a verified connection to the owning runtime. Prefer `thread/name/set` and consume/read the corresponding native name state. Debounce and coalesce changes, skip already-applied values, bound timeouts, and make failures independent of hook ingestion and notifications.

Before enabling writes, run an isolated experiment with a disposable Codex session in the current managed launch mode. Prove that an external native-name update reaches the live TUI, that the selected footer item refreshes without restart, and that two sessions cannot receive one another's names. Confirm whether the owning `--no-daemon` runtime exposes an accessible supported endpoint. A new server that merely changes persisted metadata is not proof of live synchronization.

The initial `--no-daemon` probe did not establish an external update connection. The user subsequently authorized investigating and, if supported, adding a connectable runtime for new managed sessions. First test a dedicated local app-server with a loopback WebSocket and capability-token authentication, using isolated configuration and no model turn. Demonstrate live name updates in an attached native TUI before integrating launch changes. Each new managed session must own a separate runtime/attachment and stop only its own runtime on exit; existing sessions and explicit remote connections remain untouched. Backend and shell console membership must remain verifiable for managed title/background behavior. If the experiment fails, retain the explicit unavailable status and record the concrete blocker rather than claiming live synchronization.

Presentation bookkeeping records whether sync is enabled, the exact native target, the last successful managed value, and conflict/error status. Before taking ownership, read the current native name. Preserve a pre-existing explicit native name as a conflict until the user explicitly enables management for that session; after ownership, a name differing from the last successful managed value pauses writes. Expose an explicit resume/enable action in the session UI or CLI. Repeated retries must not fight native `/rename`.

Alternative: native `/rename` is useful manually, but typing it into a live terminal is unsafe. Direct store edits do not guarantee runtime refresh and bypass provider contracts.

### 5. Keep setup changes reversible and scoped

Enable native presentation through the existing explicit Bell Tower desktop/status-line setup choices. Preview the exact Codex user configuration file and owned keys before applying. Add `thread-name` and `git-branch` while preserving existing footer items/order where possible; do not reset unrelated TUI settings. A future verified native worktree field can replace the derived suffix without changing identity semantics.

Record prior and installed values using the ownership manifest. Restore an unchanged owned value on removal; preserve a later user edit and report it. Capability detection must distinguish native fields, native naming API, live runtime reachability, and actual terminal attachment. Enabling footer configuration does not establish that name synchronization works.

Alternative: launch-only global overrides are simple but can hide user choices and are harder to inspect/remove consistently with existing setup ownership.

## Risks / Trade-offs

- [Hook shape or shell wrapping hides a selection] -> Recognize only verified literal forms, provide bounded fixtures for supported forms, preserve explicit association, and document unsupported forms.
- [Asynchronous hooks reorder selections] -> Use provider operation/turn identity and ordering receipts; reject stale completions and test a delayed old selection arriving after a newer one.
- [Codex fields exist but the running TUI cannot receive updates] -> Perform the exact managed-mode experiment first and report the transport limitation separately from field availability.
- [Automatic writes overwrite a manual edit] -> Merge identity under a lock, preserve Bell Tower manual overrides, and pause native writes on ownership conflicts.
- [Git context belongs to a different directory or a standalone planning store] -> Use verified session checkout metadata and keep planning location separate from checkout location.
- [A metadata refresh changes notification behavior] -> Keep it out of lifecycle events and assert preservation of episode, timestamps, acknowledgment/viewed state, and palette.

## Migration Plan

1. Verify hook contracts and the installed Codex live-name path in isolated disposable sessions before changing personal configuration. Record observed support and remaining limitations.
2. Add backward-compatible metadata updates and selection observation. Existing sessions keep saved names/palettes until a trustworthy new selection or explicit edit arrives.
3. Add presentation synchronization and opt-in setup ownership. Reuse current attachment validation and leave existing agents running.
4. Run targeted regressions and live provider/host acceptance, with supported and unavailable Codex outcomes recorded separately.
5. Roll back through owned setup removal and disabling sync. Preserve Bell Tower identity/palette and never restart or terminate user agent sessions as part of rollback. Stop native-name writes on disable; retain the current native name rather than guessing a previous user preference.

## Evidence References

- Current implementation: `src/BellTower/Core/SessionIdentity.cs`, `IO/IdentityStore.cs`, `Providers/HookIngestor.cs`, `Providers/HookAdapter.cs`, `Sessions/SessionCatalog.cs`, `IO/GitReader.cs`, `Hosts/TerminalTitleService.cs`, `Setup/IntegrationSetup.cs`.
- [Official Codex commands](https://learn.chatgpt.com/docs/developer-commands): native rename, footer, and terminal title controls.
- [Official app-server documentation](https://learn.chatgpt.com/docs/app-server): `thread/name/set` and native name update notifications.
- Context7 `/openai/codex`, consulted during exploration: native status-line item enumeration and `thread/name/set` handler. Indexed source and binary symbol checks remain distinct from live acceptance.
