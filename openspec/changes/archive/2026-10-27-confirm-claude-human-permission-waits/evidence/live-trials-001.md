# User-operated native trials 001 (tasks 3.4, 4.1)

Operated by the user on 2026-10-27 (UTC 2026-10-26 23:02–23:10) with the Evidence harness 011 ([implementation-011-live-harness.md](implementation-011-live-harness.md)). Recorded by the Claude Code orchestrator from the user's per-scenario verdicts and the harness observer timelines (ignored `artifacts/permission-live-011/work/s<N>-20261027-*/obs/`).

Setup: real provider clients (Claude Code 2.1.310 harness-pinned copy; Codex CLI 0.164.0), isolated client configuration, disposable projects, loopback fake model endpoint. Hooks invoke the installed Bell Tower 0.1.9012 ingress (source `754aed5` plus frozen identity `diff=518bc5fdeb23 untracked=15/c0940d0a9277`) into an isolated data directory observed by a second, silent tray instance. A human decision means the user answered the provider's native approval dialog; automatic decisions are made by the provider's real automatic reviewer (Claude auto-mode classifier; Codex `auto_review`), delayed about 20 s by the fake endpoint.

Kinds of evidence, kept distinct: **user** = the user's verdict on the visible test strip; **observer** = persisted wait provenance and rendered chip text (UI Automation); **derived** = `announce` computed from `AlertPolicy` over persisted state (0.1.9012 has no announcement log; audio was silent by the user's log-only choice, so no physical sound is claimed).

| # | Scenario | User | Observer timeline (relative to the request) | Result |
| --- | --- | --- | --- | --- |
| 1 | Claude, default mode, human approval | ok | Provisional at the request; `permission_prompt` +6.0 s → Confirmed, chip "needs permission", announce (derived); approved ~+40 s → Done → ended | pass |
| 2 | Claude, auto mode, slow automatic approval (~20 s) | ok | No approval-flow hook from the provider; no Permission wait, no chip, no announcement; Done after completion | pass (no false alert) |
| 3 | Codex, managed owning runtime, human approval | ok | Provisional; runtime status confirmed +1 s; chip "needs permission" and announce (derived) after the 5 s grace; approved → cleared | pass |
| 4 | Codex, managed owning runtime, slow automatic approval (~20 s) | ok | Provisional ~22 s; no rendered chip, no announcement; resolved → Done | pass (no false alert); see note |
| 5 | Codex `--no-daemon`, human approval | ok | Provisional; at +15 s rendered chip "possibly waiting for approval" (unverified), no announcement; approved → cleared | pass (documented fallback) |
| 6 | Claude + Codex concurrently, human approval | ok | Claude confirmed by `permission_prompt` (+6 s), Codex by runtime status; two independent "needs permission" chips, each announced (derived); both cleared independently | pass |

Note on 4: the observer's derived `unverifiedChipDue` flag turned true at +15 s, while no chip was rendered and the user saw none. The observer derives that flag from persisted state only and cannot see the tray's transient owning-runtime status, so this is a limitation of the derived column, not a product observation. Rendered and user evidence agree: no chip.

Not covered: physical audio (silent by choice), chip-click terminal activation (the user reported the chip did not bring the harness console forward; outside this change), the product-generated Codex hook command string (the harness uses a call-form hook; see H-1 in the ledger), asynchronous Codex hook registration (H-2), and the user's own day-to-day sessions.
