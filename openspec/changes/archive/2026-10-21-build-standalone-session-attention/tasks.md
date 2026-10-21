# Tasks

Scope boundary: implement standalone session attention and the hooks/terminal bridges it needs. MCP tools, direct WorkLog integration, shared helper components, per-repository managed instructions, and release/update automation are deferred in handoff.md and MUST NOT be added to this checklist during implementation without a later scope decision.

## 1. Verify provider and terminal capabilities

- [x] 1.1 Record the available Claude, Codex, VS Code, Windows Terminal, and .NET versions; select the supported .NET target and produce a versioned compatibility matrix that clearly distinguishes observed behavior from untested support.
- [ ] 1.2 Capture sanitized Claude hook fixtures for registration, new prompt, permission approval/denial, question, stop, stop failure, subagent/background activity, and normal exit; verify the matrix identifies an actual source or an explicit unavailable status for each transition.
- [ ] 1.3 Capture equivalent Codex fixtures using the installed hook contract, including question tools, approval resolution without a successful tool, interruption, and background work; verify events can be correlated and reduced coverage is recorded where transitions cannot be observed.
- [x] 1.4 Probe supported Claude/Codex footer and terminal title/background surfaces in disposable sessions; deliver a presentation matrix documenting the custom chip, scoped background, or specified fallback without modifying agent binaries or unrelated terminals.
- [x] 1.5 Probe Windows Terminal attachment-to-window mapping and exact tab/pane resolution across reorder, split, close, and multiple windows; deliver a tested exact-target mechanism or the specified verified-window fallback, with unavailable targets explicitly handled.

## 2. Establish the application and session core

- [x] 2.1 Create the Windows .NET solution, tray/CLI entry points, responsibility-based folders, and separate test project using WorkLog's small-helper organization as a reference; verify the solution builds with zero warnings, a silent CLI invocation succeeds, and no WorkLog project/runtime dependency exists.
- [x] 2.2 Implement provider session keys, process attachments with creation-time checks, and host registrations; verify independent sessions with identical names and reused process IDs remain separate.
- [x] 2.3 Implement validated atomic inbox publication, single-writer reduction, snapshots, and idempotent replay; verify restart/crash recovery and malformed/partial records neither lose accepted observations nor corrupt other sessions.
- [x] 2.4 Implement independently tracked foreground activity, background tasks, and correlated attention; verify a question during subagent work stays visible, unrelated tool output cannot clear another wait, shell tasks do not suppress completion, and old-turn events cannot revive resolved waits.
- [x] 2.5 Implement liveness reconciliation and end-event handling; verify quiet live waits remain present while confirmed ended attachments leave the overview and strip without any process being signaled.

## 3. Own and present session identity

- [x] 3.1 Implement app-owned display names, explicitly user-selected change associations, directory defaults, and tray/CLI editing; verify use without OpenSpec or MCP and ensure multiple changes are never assigned arbitrarily.
- [x] 3.2 Implement persisted random dark backgrounds, brighter related accents, and readable foreground selection; verify contrast, restart/resume stability, and the same accent values in identity metadata and notification presentation.
- [x] 3.3 Implement branch, detached-HEAD commit, and linked-worktree discovery with bounded refresh; verify normal checkout, linked worktree, subdirectory, non-Git, and branch-change cases without changing session identity.
- [x] 3.4 Implement the supported bundled Claude name-chip renderer and verified Codex presentation or fallback from task 1.4; verify operation without the personal mod, matching name/accent, supplementary Git context, scoped host changes, and accurate unsupported-decoration reporting.

## 4. Connect agent observations

- [ ] 4.1 Implement the Claude event adapter and bundled launcher/registration helper using task 1.2 fixtures; verify normalization covers supported attention and background transitions and hook output does not alter approvals or agent control flow.
- [ ] 4.2 Implement the Codex event adapter and bundled launcher/registration helper using task 1.3 fixtures; verify supported events normalize to the shared model and unsupported question/failure observations are explicitly diagnosed.
- [x] 4.3 Implement bounded hook ingestion and provider trust/setup detection; verify hook commands complete promptly, obey event-specific success output contracts, and report pending trust without bypassing review.

## 5. Implement the tray and attention experience

- [x] 5.1 Implement completion grace, immediate actionable waits, episode acknowledgment, and silent restoration using a controllable clock; verify grace cancellation, delayed background completion, duplicate events, dismissal persistence, and waits becoming due while the tray is stopped.
- [x] 5.2 Implement the nonactivating attention strip with shared accent/name, reason, HH:mm:ss, tooltip Git details, right-click dismissal, and left-click navigation requests; verify working sessions are absent and metadata updates preserve wait time and acknowledgment.
- [x] 5.3 Implement the tray overview with all live sessions, provider/activity/attention, Git details, identity editing, and navigation precision; verify working sessions are visible there and renaming updates the strip without another alert.
- [x] 5.4 Implement single-instance detached tray lifetime, draggable position persistence, and display-bound refitting; verify closing a disposable starting shell leaves the tray running and remote-desktop/display resize keeps the strip reachable.
- [x] 5.5 Implement the serialized in-memory chime/speech queue, Zira preference with chime-only fallback, four-word subject limit, stale-wait cancellation, and aggregation above two pending phrases; verify parameters and queue behavior with a fake audio sink that produces no sound.
- [x] 5.6 Implement Mute, live persisted Voice preference, Test sound, Exit, and Bell Tower-only management commands; verify mute/unmute does not replay stale waits, voice=false preserves simulated chimes, and automatic checks remain silent.

## 6. Route terminal navigation

- [x] 6.1 Implement the VS Code bridge with window/terminal registration and expiring acknowledged focus requests; verify two windows with identical terminal names select only the matching live attachment.
- [x] 6.2 Implement the Windows Terminal mechanism selected by task 1.5 and label exact, window-only, or unavailable navigation; verify reorder/close/pane changes cannot focus the wrong terminal or accidentally create another window.
- [x] 6.3 Connect strip and overview actions to validated host routing; verify Claude and Codex both route by host, expired registrations fail visibly, and successful focus does not resolve or acknowledge the wait.

## 7. Package standalone setup and migration

- [x] 7.1 Package the executable, launcher helpers, hooks, and host bridges with basic explicit reversible setup and per-surface diagnostics; verify installation/removal against fixture configurations preserves unrelated hooks, status lines, profiles, and provider trust requirements without adding MCP registration or WorkLog-style managed instruction initialization.
- [x] 7.2 Implement app-owned config/state under the proposed %LOCALAPPDATA%/ZeusApps.BellTower default and explicit legacy name/palette/voice/position import; verify a clean Codex-only profile works without Claude directories or WorkLog and imported metadata works after legacy input files are removed.
- [x] 7.3 Implement bounded sanitized logs; verify the log cap and absence of prompt/tool payloads. Keep release checking, update staging, replacement helpers, and self-update commands deferred to handoff.md.
- [x] 7.4 Document setup, manual identity/change naming, capability limits, clean installation, optional legacy migration, rollback, and manual executable replacement; verify the procedure stops/restarts Bell Tower only, matches the packaged CLI, and requires neither WorkLog nor the personal PoC/mod.

## 8. Verify integrated behavior

- [x] 8.1 Run the relevant build and automated suites with silent audio substitutes; verify coverage of causal event races, restoration, attention episodes, palette consistency, Git context, navigation validation, and reversible setup, and record the results.
- [ ] 8.2 Exercise Claude/Codex in VS Code/Windows Terminal using disposable sessions; record the four-way compatibility matrix for completion, permission, question, background work, error, rename, dismissal, restart, and navigation, explicitly recording supported fallbacks and observation gaps.
- [x] 8.3 Review strip dragging, simultaneous waits, tray survival after the starting shell closes, and voice/chime queue output through a silent sink; record confirmed visual/lifecycle evidence. Live display/RDP resize acceptance is deferred and nonblocking by the user; physical sound testing remains separate.


## Archive reconciliation (2026-10-21)

The user requested archival after reviewing live acceptance and authorizing the final commit. See ../../../../docs/live-acceptance.md for their confirmed startup, completion, backgrounds, Claude badge, VS Code focus, Windows Terminal fullscreen/reorder/multiple-window navigation, timer hiding, dragging, dismissal, parallel chips and detached tray lifetime results.

Five tasks remain unchecked: 1.2, 1.3, 4.1, 4.2 and 8.2 require comprehensive retained authenticated hook captures or coverage based on them. Adapters and launch support are implemented and automated tests pass; missing captures and unobserved rare scenarios are not claimed as completed. Archive with these explicit evidence gaps under the user's wrap-up instruction rather than require repeating accepted checks.

Repeated-permission live retesting and live display/RDP resize acceptance are explicitly nonblocking follow-ups. Automatic agent-driven change/worktree association remains deferred scope. These follow-ups do not mean their untested scenarios passed.