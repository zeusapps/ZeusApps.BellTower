# sync-native-session-titles

Research and implement Bell Tower writing its shared identity into provider-native session titles for both Claude Code and Codex.

## Origin and research inputs

Deferred from `restore-claude-badge-identity` by user decision on 2026-10-24 ("Writing native session title for both claude and codex would be a plus, but it deserves its own change and research"). Not started; no proposal yet.

Inputs to carry forward:
- An isolated probe of the official Claude Agent SDK 0.3.309 (`renameSession`, `getSessionInfo`) against a disposable copied Claude 2.1.306 transcript, which appended a `custom-title` record. It needs a bundled Node runtime (~36 MB zipped, ~90 MB installed). The draft helper was removed from `restore-claude-badge-identity`; see that change's `evidence/implementation-004.md` and `reports/qa-001.md`.
- The SDK `customTitle` falls back to the generated `aiTitle`, so it cannot tell generated titles from user titles (QA-001 in `restore-claude-badge-identity`). Read provenance from `custom-title` transcript records instead.
- The SDK `forkSession` rewrites `sessionId` and appends a "<title> (fork)" custom title.
- Codex native-name sync already ships through the authenticated runtime thread-name service, with ownership and pause safeguards.
- Open questions: does an open interactive client refresh its title visibly; what is the runtime-dependency budget; are there alternatives to bundling Node; and what is the shared write policy for both clients.
