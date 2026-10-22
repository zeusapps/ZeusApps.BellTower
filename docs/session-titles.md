# Session titles

Bell Tower resolves **manual name > session-selected OpenSpec change > project directory**. Two sessions in one repository can select different changes and keep separate titles. Selection updates preserve the palette, wait episode, elapsed time, acknowledgment, and viewed state.

## Normal launch and resume

After desktop setup, open your usual **Codex** terminal profile and use `/resume` as usual. The profile loads the installed launcher automatically, including after a terminal or machine restart. No Bell Tower command or repeated setup is needed for each session. OpenSpec operations performed by the agent select the change; you do not need to run a command yourself to set the title.

Resuming the same conversation restores its saved change, manual override, palette, and native-name ownership. It does not require another OpenSpec selection. Later agent selections update that conversation again. A native name edited outside Bell Tower remains protected across restarts.

An already-running session launched by the older standalone module keeps its original runtime until it exits. Reopening the Codex profile loads the updated integration; resuming an old conversation from there uses its saved identity. Merely using `/resume` inside an old standalone process does not replace that process's runtime.

## Automatic selection

Both providers use the existing main-session `Bash` tool hooks with matching `tool_use_id` values. Recognized literal invocations include:

```text
openspec new change my-change
openspec status --change my-change --json
openspec instructions apply --change=my-change --json
openspec status --change my-change --store my-store --json
```

Ordinary single/double quoting and a leading PowerShell call operator for a literal executable path are supported. Change names are kebab-case. A successful `status` or `instructions` response must be JSON with matching `changeName`, a schema, and an absolute resolved root. For local `new change`, Bell Tower requires the exact success marker and scaffold metadata. Store-based `new change` needs a later JSON `status`/`instructions` operation before it is associated.

Bell Tower ignores failures, listings, capability reads, background-agent selections, missing pre-tool receipts, ambiguous shell programs, substitutions, pipes, and hidden nested orchestration that does not expose a literal underlying command. It never guesses from prompt text, the only active change, or directory modification time. An explicit operation addressing one change counts as selection, including read-only status/instructions requests.

If selection is not observable, associate it through **Sessions > Change** or:

```text
bell-tower rename --session "codex:SESSION_ID" --change "my-change"
```

Set **Sessions > Name** for a manual title, or clear that field to show the latest associated change. Existing launch `-Name`/`-Change` values remain supported; a saved identity wins over stale launch defaults on resume. The planning root/store is saved separately and never becomes the session's Git checkout.

## Branch and worktree

Claude's bundled badge and managed terminal tabs append available branch and linked-worktree context. Bell Tower chips keep the short name; their tooltip and overview expose the Git details. A linked worktree's readable name is its checkout-root directory name, including when the session is in a subdirectory. Missing Git context is omitted.

Codex's native footer can show `thread-name` and `git-branch` with native styling. Preview `bell-tower setup --clients codex --status-line`, then use the existing explicit setup flow to apply it. Setup preserves existing footer items/order and unrelated settings. Removal restores an unchanged owned value; user edits are retained and reported. A sealed inline `tui` table without a footer key is reported before any write.

Updated local **Start-Codex** launches create one dedicated app-server with a random loopback port and capability-token authentication. Bell Tower updates the exact native thread through its owned runtime. The backend and shell share a verified console; the interactive TUI remains a direct shell child. The runtime ends after that client exits or its shell disappears. Ordinary Bell Tower tray exit/detach management does not stop agents.

Existing sessions and explicit remote connections are unchanged. An explicit `--no-daemon` agent argument retains standalone operation, whose native name cannot be updated externally. Dedicated-startup failures fall back to standalone tracking. Native executables and the official npm bootstrap are supported; custom shims may require standalone mode.

A pre-existing explicit native name or native `/rename` pauses automatic writes. **Sessions > Use name in Codex** explicitly takes ownership/resumes; **Pause Codex names** disables writes. CLI equivalents:

```text
bell-tower native-sync --session "codex:SESSION_ID" --mode enable
bell-tower native-sync --session "codex:SESSION_ID" --mode disable
```

Codex's existing native name may also be a provider-generated title. The name API does not establish its author, so the first-ownership safeguard can preserve such a title too. The current conversation's saved `Show OpenSpec change name` title triggered that safeguard; explicit managed-name ownership replaced it with `follow-openspec-session-titles`, and the successful ownership record is retained across resumes.

`bell-tower list` exposes the derived `nativePresentationName` and actual `nativeNameSynchronization` status. Native names add a readable linked-worktree suffix while branch uses a separate field. Stored manual names/change associations remain clean. No native database/JSONL writes or production terminal-input injection are used.

Isolated native TUI trials verified live switches, native rename pause/resume, two managed sessions in one project, a real linked-worktree label/branch, and backend/shell console membership. They used dummy credentials and explicitly supplied Bell Tower registration/identity inputs without model turns. Authenticated tool-hook captures and the full Claude/VS Code/Windows Terminal matrix remain open.

A separate restart trial closed the native client, opened the installed launcher in a fresh shell/runtime, and used the native `/resume` picker. The saved `restart-retained-change · master` footer returned without a new selection. This used isolated data, explicit fixture metadata, and a disposable user turn directed exclusively to an unreachable loopback model endpoint, which was interrupted; no real model request was made. Automated tests also cover ended/resumed lifecycle registration and continued native-name ownership through a replacement runtime.
