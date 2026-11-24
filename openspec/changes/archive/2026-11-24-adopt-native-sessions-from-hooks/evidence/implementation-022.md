# Implementation 022: QA-1500 hook ingestion timing

Developer assignment DEV-022 (third fix cycle, limited to QA-1500), on top of [implementation-021](implementation-021.md). Baseline `2f1de7f`. No product or test code changed in this cycle. The slowdown reported by the A/B is a measurement artifact, not a hook-path regression.

## Cause

`HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks` builds two installation payloads by copying `src/BellTower/bin/Release/net10.0-windows` and hashing every file in them. The QA A/B compared the working tree, whose Release output folder has accumulated 502 files (178.5 MB, including stale `win-x64` publish output from earlier builds), against a fresh baseline worktree whose folder has 24 files (3.9 MB). The test therefore copied and hashed about 45 times more data on the target. The measured 8-9 s against 4 s follows the folder size, not the hook code.

## Measurements

Temporary detached worktrees at `2f1de7f` (baseline) and at `2f1de7f` with this change applied (target), both built clean in Release (24 files in the output folder each; `global.json` set to the installed SDK 10.0.114 in the worktrees only). Both worktrees were removed afterwards.

1. The same test, run in isolation with trees alternating, six runs each, per-test durations:

| Tree (clean, equal output folder) | Durations |
| --- | --- |
| baseline | 4 s, 5 s, 4 s, 4 s, 4 s, 4 s |
| target | 5 s, 4 s, 4 s, 5 s, 4 s, 4 s |

   There is no measurable difference. In the working tree (502-file output folder) the same test takes 8-9 s, which reproduces the QA figure and isolates the folder size as the variable.

2. Per-hook cost of `bell-tower.exe event` (one SessionStart on an empty data folder, ten alternating runs after one warm-up, median wall time including process start):

| Provider | baseline | target |
| --- | --- | --- |
| Claude Code | 464 ms | 483 ms |
| Codex | 450 ms | 431 ms |

   Differences are within run-to-run noise. This matches the code analyzer's isolated microbenchmarks (command-line read about 2 us, parent identification about 11 us).

## Decision

No behavior-preserving optimization was made. A shared Toolhelp ancestry snapshot (ANALYSIS-605) would save one snapshot per ingress hook, but the measurements show no regression to recover, and changing the capture structure now would add risk for no measured benefit. It stays deferred. Launch-store reads remain limited to provisional captures.

## Recommendation for the reviewers' A/B method

Compare trees whose Release output folders match, or build the target in a clean worktree. The 3-second per-hook budget margin on the target is the same as at baseline (the hook itself takes about 0.45 s).

## Correction applied

`implementation-021.md` now states that when a pre-change tray persists the shared record, the inert `evidence` object (including the candidate, launch name and change text, and process and host identities) can survive as extension data in the local attachment file, and is never promoted.

## Checks

Build, full test suite and format verification were re-run on the unchanged code; see the handoff for results.
