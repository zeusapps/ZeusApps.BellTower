# Tasks

## Historical task interpretation (2026-10-23)

The checked tasks below record the original shipped implementation. `restore-claude-badge-identity` supersedes the original read-only status/instructions selection behavior and naming precedence for both providers; do not reintroduce it when reconciling or archiving this older change. Its overlapping delta requirements now use the approved current contract. Tasks 1.1 and 5.3 remain unchecked; newer fixtures or planning reconciliation do not retroactively satisfy those live acceptance gaps.

## 1. Verify provider contracts and live Codex support

- [ ] 1.1 Capture minimal redacted successful/failed OpenSpec tool-hook examples from the installed Claude and Codex versions; verify the command, success, main/background identity, operation ordering, and working-directory fields and record supported direct invocation forms without retaining transcripts or raw private payloads.
- [x] 1.2 Use disposable Codex sessions and isolated configuration to test native `thread-name`/`git-branch` rendering and `thread/name/set` delivery to the live TUI. Record the current `--no-daemon` limitation, then test the user-approved dedicated authenticated loopback app-server path and two-session isolation. Do not count a persisted-name change without visible live refresh as success.
- [x] 1.3 Record the resulting capability contract for native fields, name API, live transport, and host attachment in compatibility documentation; verify unsupported cases have an explicit fallback outcome and the approved runtime extension preserves per-session ownership and live acceptance criteria.

## 2. Follow session-specific OpenSpec selections

- [x] 2.1 Add a bounded selection reader for verified successful main-session direct `openspec new change`, `status --change`, and `instructions --change` invocations, including literal quoting/store forms; verify fixtures reject failures, listings, spec reads, substitutions, ambiguous sequences, and background-agent updates.
- [x] 2.2 Add an atomic identity update path and backward-compatible selection provenance/ordering receipts; verify loading old identity files, parallel sessions in one repository, duplicate/reordered deliveries, and concurrent manual edits preserve the correct association and palette.
- [x] 2.3 Integrate selection updates into provider ingestion without retaining raw commands or publishing synthetic lifecycle events; verify selecting change A then B updates only the originating session, preserves a manual name, and clearing that name reveals B.
- [x] 2.4 Preserve association on resume and keep session checkout metadata separate from the OpenSpec planning root/store; verify an external planning store does not replace checkout context and explicit CLI/tray association remains usable when a hook form is unsupported.

## 3. Present name, branch, and linked worktree

- [x] 3.1 Add shared derived Codex presentation formatting using the resolved name plus an optional bounded linked-worktree suffix, with branch supplied through its native field; verify ordinary checkouts, linked-worktree subdirectories, detached HEAD, non-Git directories, missing fields, and sanitized/long labels without modifying stored manual names or change associations.
- [x] 3.2 Refresh existing Claude, Bell Tower, and managed-title surfaces from updated identity and verified checkout context; verify branch/checkout changes update details while short attention-chip names remain concise.
- [x] 3.3 Add a timeout-bounded, coalescing Codex native-name adapter for the supported owning-runtime transport established in 1.2, with exact session targeting and persisted last-success bookkeeping; if the dedicated runtime experiment succeeds, integrate per-managed-session runtime startup, authenticated connection, verifiable console attachment, and cleanup without stopping existing sessions. Verify duplicate suppression, isolation, timeout/failure fallback, and live refresh when available. If the transport is unavailable, retain the explicit unavailable path without claiming live acceptance.
- [x] 3.4 Add per-session native-sync enable/resume controls and conflict detection for pre-existing or subsequently user-renamed Codex chats; verify native `/rename` pauses automatic writes, explicit resume restores management, and Bell Tower manual names remain authoritative when synchronization is enabled.

## 4. Configure and diagnose native presentation

- [x] 4.1 Extend explicit desktop/status-line setup to preview and reversibly manage Codex native footer fields through the existing ownership mechanism; verify missing config, existing footer item order, repeated setup, unrelated settings, concurrent edits, and removal after user edits in isolated setup tests.
- [x] 4.2 Report selection-observation coverage, native footer fields, live sync state/conflicts, and unavailable transport separately in diagnostics and session controls; verify tracking/notifications remain operational when any optional presentation capability fails.

## 5. Verify behavior and document the trial

- [x] 5.1 Extend metadata/attention integration coverage to assert that selection, native presentation, and Git refreshes preserve episode, wait timestamps, acknowledgment/viewed state, and palette and do not replay sound; run the relevant provider, identity, presentation, setup, and host tests plus the required repository build checks.
- [x] 5.2 Update installation, architecture, compatibility, and follow-up documentation with supported selection forms, native footer styling, manual/native overrides, setup/removal, and any live transport limitation; verify the documented commands and capability claims match the implementation evidence.
- [ ] 5.3 Perform a silent live trial with two sessions in one project selecting different changes, then switch one selection and test a manual override; record correct Claude badge/Bell Tower/tab updates and linked-worktree/branch context in VS Code and Windows Terminal without stopping existing user sessions.
- [x] 5.4 When the installed Codex live transport is supported, verify that same trial visibly updates the native footer without restart and respects native rename conflicts. Keep this acceptance task unchecked if live updates are unavailable, and record the observed limitation rather than marking the Codex objective complete.

## Verification record

Final review: all 276 .NET tests and 8 VS Code bridge tests pass, with a warning-free Release build, formatting verification, and strict change/attention-spec validation. Additional regressions cover Unicode footer round trips, independent permission verification, stable fallback visibility across retries, and viewed/dismissed state when restoring an older blocker. Runtime startup/drain failure cleanup was hardened. Desktop automation fixtures passed with desktop/process access after the sandbox run could not discover its disposable tabs. Tasks 1.1 and 5.3 remain open.

Release build and formatting verification pass; all 242 .NET tests and 8 VS Code bridge tests pass. Strict OpenSpec validation passes. Isolated Codex 0.163.0 native TUI trials verified two-session isolation, live name switches, native rename pause/resume, a real linked-worktree label with branch, and backend/shell console membership. A final launcher-exit trial removed the dedicated runtime record/token before returning to the shell.

The native trials used dummy credentials and explicit Bell Tower metadata inputs, without model turns. Tasks 1.1 and 5.3 remain unchecked: authenticated successful/failed provider tool-hook captures and the full live Claude/VS Code/Windows Terminal acceptance matrix still require validation. Native footer evidence does not stand in for those checks. See docs/compatibility.md and docs/session-titles.md for the observed scope and activation steps.

Follow-up activation/restart trial: the build and desktop integration were installed at the existing path and the production tray restarted successfully. A real agent-run Codex OpenSpec status operation saved this conversation's association automatically. A separate isolated native `/resume` picker trial restored the saved change footer in a fresh shell/runtime without selecting it again. The resume fixture used explicit initial metadata and an interrupted user turn against an unreachable loopback model endpoint, without a real model request. Added regression coverage verifies saved identity on provider resume and native ownership on runtime replacement: all 35 selection/native-name tests, formatting verification, and strict OpenSpec validation pass. The broader capture/desktop matrix tasks remain open.
