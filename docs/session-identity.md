# Session identity

Bell Tower gives every Claude Code and Codex conversation one identity: a name, a working checkout and a color palette. The badge, terminal tab title, tray chip, overview row, speech and (for Codex) the provider's own chat name are all rendered from that single record, so they agree. The policy is shared; only the adapters that read provider events and write provider names differ. This page replaces the earlier status-driven selection described in [session titles](session-titles.md) (which still documents Codex name transport).

## What decides the name

The first match wins:

1. **Bell Tower title.** A name you set (`rename --name`, or Sessions > Name). Clearing it reveals the next tier.
2. **Verified native user title.** A chat name you set in the provider. Claude: only `custom-title` transcript records (a `/rename`) count, and not Bell Tower's own, not the Agent SDK's generated `<title> (fork)` title and not one you released with **Use Bell Tower name**; a generated `ai-title` never counts. Codex: the shipped pause-only policy applies (an externally set thread name pauses Bell Tower's writes and is never overwritten), but it is not promoted to a user title because whether it was typed by you or generated is unverified.
3. **Subject.** Bell Tower has no session role. The subject is, in order:
   1. a pinned change (`rename --change`),
   2. **active work**, the change the conversation is actually working on,
   3. the **automatic subject**: the one change whose name matches the current branch, otherwise a ticket id found in the branch or worktree name, otherwise the branch,
   4. the launch directory name.

A `Change` saved by an older build as an automatic selection (it carries a selection root and event and no origin marker) is classified once as a legacy selection: it ranks below active work and yields to newer verified work or an explicit association. Once strong verified work replaces it the legacy value is marked superseded, bound to its value: it stays on disk so a rollback still sees it, but it never becomes the subject, a checkout mapping or a fork inheritance again, even after that work is cleared by a branch change or a transcript restart, while a change you pinned (`rename --change`) or launched with stays a pin. A change written automatically by 0.1.8 after a rollback is classified again when it differs from the recorded one. Unknown fields are preserved.

Spoken and chip text come from the same record: hyphens, underscores and colons become spaces and at most four subject words are spoken before the reason (`ttyd download chips, needs permission`).

Legacy role inputs are accepted and ignored so existing scripts keep working: the `Start-ClaudeCodePM/Dev/QA/APM` and `Start-CodexPM/Dev/QA/APM` shortcuts launch normally, `launch --role` and `rename --role` are hidden deprecated options, and `BELLTOWER_ROLE` is not read by hooks, ingress or capture consumption. A `Role` value already saved in an identity file stays on disk (rollback-safe) but is never shown, spoken or inherited. A legacy badge title such as `PM: subject` is imported as the manual title as-is.

### Evidence for active work

Active work is a successful, main-session, non-archive operation on one change, normalized by one classifier for both clients:

| Evidence | Claude Code | Codex |
| --- | --- | --- |
| Edit of a file under `openspec/changes/<name>/` | `Write`, `Edit`, `MultiEdit`, `NotebookEdit` | `apply_patch` (patch text in `tool_input.command`) |
| Literal `openspec new change <name>` | `Bash`, `PowerShell` | `exec_command` |
| Workflow skill with a literal change argument | `Skill` | not available (see [limits](#provider-limits)) |

Rules that apply to both:

- Only the main agent counts. Subagent events (`agent_id`) never change the association.
- A failed, interrupted or ambiguous operation is not evidence. A patch touching several changes is ambiguous.
- Shell evidence uses a bounded grammar: an optional `pwsh`/`powershell -Command` wrapper, at most one literal `cd`/`Set-Location` joined with `&&` or `;`, and at most one terminal filter pipe or one redirection. Variables, substitution, subshells, `||`, several `cd`s and other chains are rejected. `new change` additionally needs its `Created change '...'` marker (or, for redirected output, a scaffold created within ten minutes) at the `cd` root.
- **Inspection never renames.** `openspec status`, `instructions`, `show`, `list`, reads, searches and the explore/explain skills do not select a change. They can only fill an association that is still empty (weak evidence) and are replaced by real work. Inspecting change B while working on change A leaves A, live and during history recovery.
- Later real work replaces earlier real work; a branch change in the same checkout clears only branch-bound automatic evidence. Pins, the native title and the palette survive.

## History recovery

When Bell Tower first sees an old conversation, or after a restart, it recovers the same evidence from the provider's transcript:

- Read-only, incremental and bounded: 256 KiB per tick across a backward cursor (older history, newest first) and a tail cursor (new growth), partial last lines deferred, oversized records skipped. Idle conversations cost one small read.
- Weak evidence (explore/explain) never ends the backward walk, so older real work is still recovered; a branch change in the same checkout records a time floor (`WorkFloorAt`, the provider-clock moment of the switch), so history stamped before the switch (or with an unparseable timestamp) is never selected again, however the history cursors move or whether the transcript is re-read. Work stamped after the switch is accepted. Live hook evidence is ordered by the observer and is not filtered by the floor.
- A forked Claude conversation (Agent SDK `forkSession`) proves its parent through its own `forkedFrom` records and inherits the parent's title and change pins and work evidence once, with its own palette allocated like any new session. Its generated `(fork)` title is ignored and, because an ignored title has no effect, it never clears an earlier valid native title. Interactive `--fork-session` and `/branch` are unverified: such a conversation resolves automatically and a `(fork)`-suffixed title is still ignored.
- Only derived values are stored (title, change, root, offsets and at most 64 pending call/result receipts). Transcript text is never copied. A rewrite, truncation, `/clear` or a new transcript path restarts the derived state; pins and palette are kept. Records that carry another session id are ignored, so a forked copy does not borrow another conversation's evidence.
- Claude: `custom-title` records, `forkedFrom` proof and `tool_use` / `tool_result` pairs. Codex: `exec_command` and freeform `apply_patch` call/result pairs (a patch counts only with the exact `Exit code: 0 ... Success. Updated the following files:` result).
- Doctor prints the exact verified and unverified history capabilities (`History` field).

## Working checkout

The launch directory, the planning root or store (where change folders live) and the working checkout (where Git branch and worktree are read) are kept separately, with provenance. The checkout is chosen in this order:

1. a checkout you attached (`rename --checkout PATH`, Sessions > Apply checkout; stored as the worktree root, must be an existing Git directory),
2. the hook `cwd` of the main agent, only when it is one of the launch repository's listed worktrees (a `cwd` in an unrelated repository is ignored; attach it explicitly instead),
3. a unique work mapping, preferring the worktree named after the ticket,
4. the retained checkout: when a branch change in the checkout a unique work mapping verified clears the work that verified it (the change folder may disappear in the same switch), that checkout stays the working checkout, labeled "retained" (`list` prints the stored token `retained`, the overview prints "retained: branch changed in the verified checkout") and its new branch and worktree are shown. Any new work, an attached checkout, provider evidence, Clear checkout and Reset release it, the tray's Clear checkout always sends an empty checkout so it releases an unpinned retained checkout too, the final retention decision is made against the locked stored state (a Clear, Reset or pin that lands while a pass is reading Git wins), the case of a vanished change folder applies only to the work recorded as having verified the checkout (the optional identity field `CheckoutWork`, kept by 0.1.9 through its additional data), a failed or timed-out Git listing never does and is cached as a failure for 10 s (the checkout is kept while its directory exists; only a successful listing without it or a missing directory releases it), and the checkout source token is a stored data contract,
5. the launch directory.

An ambiguous mapping stays visibly ambiguous until you attach a checkout; `rename --checkout ""` (or Clear checkout) clears it. An external planning store never becomes Git context or process ownership, and the working checkout never changes which terminal the conversation owns.

## Color, reset and legacy import

- Each new conversation gets a palette from an ordered series of twelve perceptually spaced colors (a very dark background of chroma 0.05 whose lightness alternates between 0.22 and 0.27 on neighbouring hues so adjacent backgrounds stay visibly different, a brighter accent of the same hue and a black or white foreground; accent text contrast at least 4.5:1, background against the terminal text at least 7:1). The allocation is one shared policy for Claude Code and Codex, managed and native sessions, Windows Terminal and VS Code, and runs under the identity lock so concurrent sessions do not collide. It takes the first series color that no live session uses. Live means a session the monitoring state does not mark ended (one whose attachment is unavailable counts only while it was observed in the last hour), an identity registered in the last hour that the state does not list yet, and a launch palette that is not yet consumed, is within the 24 hours a launch can still be adopted and whose recorded shell is still running; ended sessions and older saved identities never block a color. Accents within an OKLab distance of 0.04 count as the same color, so a custom hex accent blocks the series color it is near. When all twelve are in use, the color with the fewest live accents within that distance is chosen, then the one farthest (OKLab distance) from every live accent, then the earliest in the series, so colors repeat only beyond twelve concurrent sessions and repeats spread: the thirteenth session takes the first color again, the fourteenth the second, and no color is shared three times before every color is shared twice. Resumed conversations, restarts and metadata refreshes keep their palette, and palettes saved by earlier builds are never rewritten.
- `rename --color <name|#RRGGBB|random>` sets or releases a color; an explicit named or hex color is applied exactly and pinned even when close to another session's. `random` (or releasing the color) and `rename --reset` (or Sessions > Reset pins) pick a series color that no other live session uses, never the session's own current color, or the farthest one when all are in use. Reset also clears the title, change, checkout and color pins. It keeps native titles, live work evidence and provider ownership.
- None of these edits touch wait state: attention episodes, acknowledgments, viewed timers and sound receipts are unchanged.
- `import --badge FILE --provider P --session ID` imports one old badge file on explicit request. Accepted: an OSC 11 `rgb:` color (1-4 hex digits per component) or `#hex` with BEL or ST terminator, plus an optional title, kept as the manual title as-is (no role is parsed). Anything else is rejected, the source file is only read (64 KiB cap), and the foreground is always readable on the accent.

## Native chat names

- **Codex** (shipped, unchanged). With synchronization enabled, Bell Tower writes the resolved name into the Codex thread name through the dedicated authenticated runtime of new managed local sessions or, as a non-owning observer, an existing shared backend of the resolved Codex home; `--no-daemon`, remote sessions and shared conversations the shared access does not yet reach (task 4.2) report `unavailable`. Fork and delegated threads are refused by a conservative policy until the task 6.1 probe, and an elevated backend is not observed. The first time, an existing non-empty native name is treated as yours and synchronization pauses (`paused: native chat name was set outside Bell Tower`) until you take ownership. A name Bell Tower is about to write is recorded before the write, so its own name is never read back as an override. Work happens only when the desired name changes, every 10 seconds to verify, when you ask for ownership, or after a backoff (2, 5, 15, 30, 60 seconds). `native-sync --mode enable|disable` and the Sessions buttons control it.
- **Claude Code.** Bell Tower reads the native `/resume` title (see precedence above) but does not write it: that is unfinished work, deferred to the `sync-native-session-titles` change, and not a provider restriction. `native-sync` and the Sessions native-sync buttons refuse or are disabled for Claude, and `list`/the overview state the deferral instead of a sync status. Bell Tower's own surfaces (status line badge, managed tab, chip, speech) show the shared identity.
- **Use Bell Tower name** (`rename --use-bell-tower-name`, Sessions) is shared by both clients. It drops the native user title in favor of the automatic or Bell Tower name, remembering the released title so it is not applied again until a newer, different explicit title appears; for Codex it also takes ownership so the Bell Tower name replaces the provider's name. **Reset pins** deliberately keeps native user titles, so this control is the way back.

## Provider limits

The authoritative, per-surface status is the capability matrix in [compatibility](compatibility.md) and in `doctor`. In short:

- Claude's native `/resume` title write is unfinished (deferred to `sync-native-session-titles`); it is not a provider restriction.
- Codex workflow-skill invocation is unverified at 0.164.0: no dedicated hook event exists, but the user prompt and the shell tool payloads were not probed. Until then a skill alone does not associate a Codex conversation; edits, patches and shell creates do. No surface is currently recorded as a proven restriction.
- A Codex thread name is not promoted to a user title: whether it was typed by the user or generated is unverified.
- Codex rollouts at 0.164.0 do not record the native title; it is read through the thread name service for owned runtimes and for a shared backend of the resolved Codex home (see the per-mode rows in [compatibility](compatibility.md)).
- Claude fork inheritance is verified for the SDK `forkSession` operation only; interactive `--fork-session` and `/branch` are unverified.
- Codex fork inheritance follows `session_meta.forked_from_id` in the fork's own first rollout line (openai/codex rust-v0.164.0 `SessionMeta`, rollout recorder and `fork_thread_from_history` sources, a synthetic fixture and tests); there is no live `codex fork` capture yet, and `parent_thread_id` (spawned sub-agents) is not treated as a fork. A fork also keeps the parent's branch-invalidation time floor (never lowered), so it does not re-derive work the parent's branch switch invalidated. Claude fork records keep their original timestamps; Codex restamps copied records with the fork time, so when a branch invalidation is known, the Codex adapter excludes records before the first `thread_settings_applied` event owned by the fork's thread id (copied snapshots keep the original owner's id) and retracts history work applied from them before that event was found; with no known invalidation (parent unknown or not yet read) a Codex fork uses its copied history exactly as a Claude fork does. Copied history is then ordered against the work inherited from the parent by the shared work rule (newer evidence wins) on both clients; parent work older than the copied records' original stamps loses to the copies on both clients. The clients differ in one narrow window only: parent live work that is newer than the copied records' original stamps, older than the fork time, and has no copied twin (the same tool call is absent from the copied history). There a Claude fork (original stamps) keeps the parent work while a Codex fork (copies restamped with the fork time) takes the copies. It needs the twin to be missing; a possible route is a fork from an earlier turn, which is unverified for both clients. It self-corrects on the fork's first own work, and a manual name, native title or pin hides it. This is an unverified, unfinished parity gap, not a proven provider restriction (see `docs/compatibility.md`). Parent work newer than the fork time is kept by both. A rollout without that event falls back to the floor only and stays unverified.
- Live trials of badge rendering, tab titles and installed-client behavior are listed as open in [compatibility](compatibility.md); automated tests use fixtures and disposable data.
