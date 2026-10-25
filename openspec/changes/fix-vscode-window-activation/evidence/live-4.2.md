# Task 4.2 live acceptance (user-performed)

- Date: 2026-10-25, Windows 11 (user's workstation), VS Code 1.143.1 (one window, 2 terminals), Bell Tower dev build `0.1.9011 dev 8167c5d dirty (2026-10-25 18:21 UTC)` installed via `install-dev.ps1 -Action Switch` (operation `bell-tower-dev-84491df0db45401180ba9de05731a920`); bridge `zeusapps.bell-tower-0.1.9011` installed by user-authorized `setup --desktop --apply`.
- Performed by: the user. Observations below are user-reported plus read-only artifacts captured by the orchestrator. No automation took the foreground.

## Timeline (local time, from data-directory artifacts)

| Time | Evidence | Reading |
| --- | --- | --- |
| 19:24:03 | setup applied; `doctor` showed window `ca160297` as `partial` (older bridge) | Pre-reload state reported honestly |
| 19:28:42 | new extension host (VS Code output-log folder); registration `87da8fa3` (protocol 2) | Window reloaded onto the new bridge |
| 19:30:15–16 | two receipts: `{"success":true,"precision":"exact terminal","protocol":2,"message":"The registered terminal was revealed."}` and `{"success":false,"precision":"unavailable","message":"The focus target is stale or unavailable."}` | Clicks before the window was identified: terminal revealed, window not raised; user needed several clicks |
| 19:34:13 | registration `window.observedAt` | First window identification, ~5.5 min after reload |
| ~19:37 | user retried: "this time it's much better"; `doctor` window `87da8fa3` = `verified`; no receipt left in `focus\`; the 19:30 receipts were swept | Verified window was brought forward; cleanup and sweep worked live |

## User verdict

First attempt after reload: "it work, but it took time and several clicks". Retry: "this time it's much better. let's count it". The user accepted task 4.2 on this evidence.

## Not separately reported (accepted by the user without separate evidence)

Minimized restore, maximized/fullscreen preservation, two windows with duplicate terminal names, floating editor window (ANALYSIS-003), and `WindowState.active` on a background window (ANALYSIS-103). These remain unverified live; fixture and native-window coverage is in QA 002–004.

## Live observations recorded as follow-ups

- LIVE-001: a window focused at reload was not identified until a later focus change (~5.5 min); the QA-003 path did not trigger. Clicks in that period are terminal-only.
- LIVE-002: receipts from overlapping early clicks were not consumed by the tray; they were removed by the next request's 1-minute sweep (bounded, not a leak).
- LIVE-003: no navigation diagnostics. `bell-tower.log` has no navigation codes and the bridge output channel was empty, so slow clicks cannot be explained afterwards.
- A stale `.tmp` from the pre-change bridge (11:07) remains in `hosts\`. New code prevents new leaks but does not remove pre-existing orphans.
