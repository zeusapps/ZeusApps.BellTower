# Field-state recovery probe (orchestrator)

Orchestrator probe, 2026-11-21. Supports task 3.3 and the scenario "State at the budget recovers". It does not replace the installed check.

## Method

- Target: the DEV-003 sources (`head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/5e050d0888dd`). `src/BellTower`, `BellTower.Protocol` and `BellTower.Distribution` were copied into a session scratch directory outside the repository and built in Release with the pinned SDK 10.0.114.
- Data: a private copy of the maintainer's real data directory, taken while the wedged tray was running: `state.json`, `state.json.bak`, the capture spool, the inbox, and the attachments, identities, launches, hosts, runtime and presentation records. The real data directory and the installation were not written.
- Harness: a scratch console that calls the public `StateStore.Drain()` on the copy in a loop, the way the tray does each tick, and reports the snapshot size, session counts and remaining captures after each drain. No tray, no audio and no terminal writes.
- Clients: the copy held Claude Code and Codex sessions and captures from both providers. The probe exercises the shared store for both providers together; it does not separate them.

## Result

| Step | state.json bytes | Sessions (live / ended) | Pending captures | Drain time |
| --- | --- | --- | --- | --- |
| Start | 16,772,891 | 95 (2 / 93) | 2,624 | — |
| Drain 1 | 528,970 | 94 (2 / 92) | 2,496 | about 4.0 s |
| Drains 2-20 | 702,814 to 1,025,898 | 94 (2 / 92) | 2,368 down to 64 | 3.8 to 10.4 s each |
| Drain 21 | 1,015,795 | 94 (2 / 92) | 0 | 3.8 s |
| Drain 22 (quiet) | 1,015,795 (unchanged) | 94 (2 / 92) | 0 | 5 ms |

- The first drain saved within the budget. The over-budget save ran the relief compaction even though a backlog was present (ANALYSIS-103 behavior). One ended session was removed by the existing retention rules, and both live sessions were kept.
- All queued captures were consumed and deleted, and the quiet drain did not write.
- Passed for the recovery scenario on real data. The installed check (3.3) is still separate.

## Observation (not a finding of this change)

A drain of one full capture batch (128) took about 4 s on this machine. A quiet drain takes 5 ms. The cost comes from the existing causal replay of the live sessions' 256-event journals for each accepted capture, which this change did not alter. On the installed tray this means roughly 85 s of slow ticks while this backlog is consumed once. This is recorded as a follow-up candidate for the causal reducer or capture batching, outside this change's scope.
