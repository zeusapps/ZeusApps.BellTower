# Implementation 012 (DEV-010, fix cycle 2, the last for task 2.4)

Client: Claude Code developer subagent. Not committed. No product code changed.

## QA-260: the null-process model no longer depends on the host

How the earlier runs were launched: all of fix cycle 1's runs started the harness as `( pwsh ... ) &` inside a backgrounded Git Bash command. The subshell's parent exited at once, so the harness's own ancestry was cut and no real `claude.exe` was among its ancestors: that is why they passed (161 of 161) while QA, who ran the harness in the foreground under the live Claude Code host (`pwsh <- cmd/bash <- claude.exe`), got 151 of 161. The product's ancestry walk (`Hosts/AttachmentCapture.cs:67-69`, `IO/ProcessTable.cs:21-40`) takes the first `claude.exe` ancestor among up to 32, so an unidentified fake agent that was a descendant of the harness found the real host and read verified live.

Fix: an unidentified fake agent is started through a spawner (the harness executable in `--spawn-detached` mode) that exits at once, so the agent has a dead parent and the walk stops there whatever launched the harness. The agent serves hooks over a loopback socket (it has no inherited streams). The scenario asserts the ancestry it relies on: `candidate/null-process-ancestry` lists each unidentified agent's ancestry (empty, dead parent), asserts no provider image in it, and reports the harness's own host chain. `AgentSession.Start` also throws if an unidentified agent still has a provider ancestor.

Reproduction runs (both with the real `claude.exe` host in the harness's ancestry, evidence in the check detail):
- PowerShell tool, foreground-parented (`pwsh.exe <- pwsh.exe <- cmd.exe <- claude.exe <- powershell.exe <- WindowsTerminal.exe <- explorer.exe`): 174 of 174, and again 177 of 177 after the window guard.
- Git Bash, direct (`pwsh.exe <- bash.exe <- bash.exe <- bash.exe <- claude.exe <- powershell.exe <- WindowsTerminal.exe <- explorer.exe`): 174 of 174.

## Sandbox notifications on the desktop

The product has no supported setting that turns the notification strip off: `--silent` and `muted` silence audio only (`TrayApplication.cs:57,81`), `Reminders.Enabled` stops reminders only, and the strip (`StripForm`, the attention chips, including the "possibly waiting" chip of a pending wait) is always shown by the tray. No product switch was added. The harness instead hides, every 100 ms, each visible top-level window owned by a process whose image is under the sandbox instance's install directory (`Harness/SandboxWindowGuard.cs`), never any other process. `final/no-visible-sandbox-ui` reports the count of shows it undid (498 in a Claude Code group run) and requires that no sandbox window stays visible across two reads 400 ms apart. A window can be visible for up to one poll interval (100 ms) before it is hidden. The first version of the check read once and caught a window shown a moment earlier (one failure in the mixed group of one `all` run); the check now requires visibility across two reads.

## Fixes

| Finding | Outcome |
| --- | --- |
| ANALYSIS-260 | The changed `.cs` files are CRLF again (`dotnet format ... --include`), verified with `--verify-no-changes`; implementation-011 corrected. |
| ANALYSIS-261 | Option (a): the check waits until every conversation's last hook is past the attachment grace plus 15 s (`StableState.RemainingQuiet`, unit tested) and compares once; no retry. |
| ANALYSIS-262 | The Claude plain null-process expected failure requires the `toolu_plain_1` probe in the journal under an attachment id other than the session's, at least four events and several ids (`LossPredicates.FreshIdLoss`, fixture tests). |
| ANALYSIS-263 | Expected failures compare exact ids read from the probe's own journal event (`SharedId`, `OwnId`, the managed id): plain `--no-daemon` (session on the shared id, event on the process id), owned runtime (session managed, event shared), Codex heal (session shared, event managed) (`LossPredicates.IdLoss`, fixture tests including the reversed direction). |
| ANALYSIS-264 | A Codex `wait-fresh-owned-runtime` conversation is started in the candidate phase after the setup rerun, under a dedicated idle manager helper (`--manager`) that is both the recorded manager and shell (never the harness process). It runs through every per-kind check; the expected rollback loss applies to it as to the 0.1.9-registered owned runtime. The earlier sentence that the record was exercised was wrong for the 0.1.9-registered conversation (implementation-011 corrected). The negative control without the record was not added. |
| ANALYSIS-265 | `docs/distribution-validation.md`, ledger F4 and `implementation-010.md` corrected: the Codex null-process later-hook loss is new in 0.1.11 (0.1.9 keeps the managed id and repairs it), only the plain Claude fresh-id case is pre-existing; the expected-failure check ids are listed. User decision 2026-11-26: documented as a known 0.1.11 regression with a follow-up; workaround: restart the conversation. ANALYSIS-240 is documented too: after a manual rollback following a 0.1.11 setup, rerun the older version's setup and resume or restart open Codex conversations. |
| ANALYSIS-266 | The rollback phase no longer waits 60 s for application of kinds with a documented loss; it waits on journal presence. |
| Dead `legacyImage` assignments | Removed; the shell is derived from the installed command only. |

## Known harness limitations

- ANALYSIS-246: the `HookDriver`/`TrayDowntime`/`UpdateRun` extraction is not done; it waits for the first 2.5 to 2.7 scenario that needs a second caller.
- `AgentSession.Start` and `Instance.Run` call sites of the environment-swap guard are not separately unit tested (`Instance.Create` is).
- No negative control for the owned-runtime record (a launch without it should fail closed to the shared id).
- The null-process and plain-launch outcomes (Codex later hooks, Claude fresh ids) are expected failures pending the 5.2 parity decision and the user's decision; they are pinned, not resolved.
- The sandbox window guard allows a window to be visible for up to 100 ms; the product offers no way to avoid it. ANALYSIS-280: its ownership is read live from each window process's image on every poll (no cache keyed by process id) and the guard is disposed if instance creation fails; a unit test covers the image lookup seam.
- The Codex `--remote` owned-runtime launch is modeled by a fake backend and a runtime record, not by a real runtime manager and WebSocket.

## Checks

- FieldAcceptance unit tests: 109 passed. Format verification: CORRECTED in the last step of the cycle (ANALYSIS-260, third report): the editing tool wrote LF, so the earlier "clean" claim was wrong for several files; every `.cs` file under the harness and its tests was converted to CRLF as the very last step, `dotnet format --verify-no-changes --include <the changed files>` passes, and the CR byte counts equal the line counts (listed in the hand-back).
- `faults-and-rollback`, all groups: from the PowerShell tool 174 of 174, and again after the window guard 177 of 177 (`harness-2.4.md`, 12 expected failures plus the new fresh-owned-runtime rows); from Git Bash 174 of 174. Both with the real `claude.exe` host in the harness's ancestry (QA-260 reproduction passes). A Claude Code group alone after the final guard-check change: 55 of 55.
- `--scenario all`: the run that wrote `harness-all-010.md` (after the guard) gave 323 of 326: the only failure was the first version of `final/no-visible-sandbox-ui` (a window shown a moment before the read, mixed group); the check was changed to require visibility across two reads and verified in the Claude group run above, so the two-read form was not re-run in a full `all`. An earlier `all` attempt failed in the faults scenario with "Failure extracting contents of the application bundle" because drive D: had under 2 GB free; space was freed by deleting this task's own old run directories and the rerun completed. The 2 unfinished parity items (Codex native-title tier) are the known ones.
- Release build then full suite once: 2241 passed, 4 failed, all load-sensitive and unrelated, all passing when rerun alone: `BellTower.Tests.Cli.CliTests.GeneratedPowerShellHookCommandPreservesUnicodeAndFinishesWithinItsTimeout`, `BellTower.Tests.Updates.HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`, `BellTower.Tests.Providers.CodexTransportTests.UnavailablePeerLookupFailsClosedWithItsOwnReason`, `BellTower.Tests.Sessions.CodexSharedRuntimeServiceTests.SharedConversationNameIsWrittenThroughTheBackendToThatThreadOnly`.
- QA's killed run left 12 sandbox trays; the next harness run's own preflight sweep removed all 12 (reported `PREFLIGHT removed ...` lines; no process of that run remained). The user's trays 8060 and 28388 were not touched.
- Disk: drive D: is nearly full (3.7 GB free of 300 GB; `artifacts/update-acceptance` holds 36 GB, `artifacts/dev-builds` 17 GB); a full `all` run needs several GB of free space.
