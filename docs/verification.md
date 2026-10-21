# Change close-out verification

Verified on 2026-10-21 for version 0.1.7 and change `build-standalone-session-attention`.

| Check | Result |
| --- | --- |
| Release build | Passed, zero warnings and errors |
| .NET suite | 199 passed, zero failed/skipped |
| VS Code bridge suite | 8 passed |
| Formatting verification | Passed |
| OpenSpec change validation | Passed in strict mode |
| Main specs | All five capability specs synced, content compared against the final deltas and strict validation passed |
| User acceptance | Recorded in live-acceptance.md; includes both hosts, fullscreen/reorder/multiple windows, immediate colors, timer hiding, dragging, dismissal, parallel chips and independent tray lifetime |

The implementation separates provider normalization, the reducer and alert policy, local storage, host navigation, session identity, setup, CLI commands, UI and physical/silent audio. Regression suites cover the reported correlation, interruption, WAV conversion, focus, layout and timer-race defects. Source assets, standalone packaging and reversible setup are included; generated release/test artifacts remain ignored.

The archive is explicitly authorized with five incomplete evidence tasks: **1.2, 1.3, 4.1, 4.2 and 8.2**. Comprehensive retained authenticated hook recordings and all provider/host scenario combinations are not available. Adapter and launcher implementations exist and pass automated contract tests, while user observations establish the recorded live behavior. Missing captures and rare unobserved paths are not claimed as tested or complete.

The original planning records were reconciled with the accepted compact layout, generated icon, bundled event tones, viewed-episode timer and distinct-blocker notification behavior. Completion is 28/33 tasks; this is an archive with documented evidence gaps, not a claim that every original acceptance scenario passed.

Repeated-permission live retesting and display/RDP resize acceptance are nonblocking by explicit user decision. Automatic agent-selected change/worktree association remains future scope. See follow-ups.md and the archived change's handoff.md. No physical sound, authenticated agent probe, approval override or user-session termination was performed during final verification.
