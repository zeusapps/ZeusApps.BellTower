> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's writes ("Subagents should return findings as text"); see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`.

# Analysis 002 evidence: probes

Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Reviewed identity: `head=8167c5d baseline=9b26088 files=32 diff=5443a2b9d46e untracked=9/80744c697558` (same before and after). All probes used fakes and disposable scratch data. No user window, VS Code instance, setting or data directory was touched.

## Probe set 1: bridge window mapping (re-run of analysis-001 probe A, adapted)

- Script: `%USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\analysis-002-probe.js`. The analysis-001 script `bounce-probe.js` in the same folder was adapted to the new `noteWindowState(state)` API.
- Command: `node <script> D:/Code/ZeusApps.BellTower/vscode/service.js <scratchpad>/probe2-data`, run with Node v24.13.0.
- Every case builds a real `Bridge` with `expectedOwnerPid: 500`. Its fake sampler returns handle A (`1111`) or handle B (`2222`), both with owner 500 and class `Chrome_WidgetWin_1`. B stands for a sibling window of the same main process. The registration is read from `hosts/<id>.json` after `idle()` and `tick()`.

| Case | Sequence | Output | Reading |
| --- | --- | --- | --- |
| A_bounce (probe A) | A gains focus, sample 1 in flight; blur event; focus event; sample 1 resolves to B | `samples 2, recorded 1111, dirty false` | Fixed. Before: `samples 1, recorded 2222` |
| A_noteFocusOnly | The same sequence through `noteFocus()` only, with no blur forwarded | `samples 2, recorded 1111` | The generation counter catches the bounce even without blur forwarding |
| C_blurThenRefocus | Focus gain, sample in flight, blur (stays on B), sample resolves to B; later refocus | After blur: `samples 1, recorded null, dirty true`. After refocus: `samples 2, recorded 1111` | The sample is discarded, nothing is recorded, and the refocus samples again |
| D_lagThenGuard | No event during the sample (the residual event-lag case), so sample 1 is B. Then three real focus gains sample A | `lagged 2222`, then `null, null, null`; `seen [2222, 1111]`; 4 samples | One mis-sample sets the multiple-handle guard for the whole bridge lifetime (ANALYSIS-100) |
| E_selfHealBeforeRefocus | Same as D, but a failed reveal while unfocused (`windowHandle: 2222`) arrives before the next focus gain | `lagged 2222, healed null, afterRefocus 1111, seen [1111]` | Self-heal recovers fully, but only when a click comes before the next focus gain |
| F_interactionAtLoad | Window focused at load; the first event is `{focused:true, active:true}` while the OS foreground is sibling B | `first 2222`, then `null` after a real focus gain; 2 samples | The QA-003 path records a sibling whenever `focused` is stale and an activity event fires (ANALYSIS-103) |
| G_interactionForeign | QA-003 path with a foreign owner (pid 999) in the foreground | `recorded null` | A foreign window is rejected |
| H_activeChurn | `active` toggles twice during a sample while focused | `samples 2, recorded 1111` | One extra spawn per burst of activity changes; bounded |

## Probe set 2: Node rename over a delete-sharing reader (ANALYSIS-004)

- Script: `%USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\a2-rename.js`. It does the same temp write and `renameSync` as `atomic()`. On failure it retries the rename at once, synchronously, up to 3 times.
- Held reader (PowerShell 7.6.6 `FileStream`, `FileAccess.Read`), one rename each:

  | Reader share mode | Rename result |
  | --- | --- |
  | `Read` | EPERM, no retry succeeded |
  | `ReadWrite, Delete` | EPERM, no retry succeeded |
  | `Read, Delete` | EPERM, no retry succeeded |
  | No reader | ok |

  This independently confirms the developer's measurement: `FileShare.Delete` does not let Node `renameSync` replace an open file.
- Contention (10 s). A PowerShell thread job opens the file with `ReadWrite, Delete`, reads it to the end and closes it, while Node renames in a tight loop:

  | Reader interval | Reader opens | Avg hold | Renames | EPERM | Healed by immediate retry |
  | --- | --- | --- | --- | --- | --- |
  | 250 ms | 38 | 8.5 ms | 7581 | 143 (1.9%) | 15 of 143 (10%), within 1.8 ms |
  | 0 ms | 9839 | 1.0 ms | 4498 | 4104 (91%) | 541 of 4104 (13%), within 2.8 ms |

- Reading the measurement:
  - A collision lasts as long as the reader holds the file.
  - An immediate synchronous retry heals only about 10–13%. An effective retry needs to wait about the length of the hold.
  - PowerShell's per-open overhead inflates the hold here. The tray's compiled C# reader is expected to hold far shorter. That is a hypothesis; the C# hold was not measured.
- Leftovers: one file (`reg.json`) in the scratch folder afterwards, so the probe leaked no temp files.

## Checks

- `node --test vscode/service.test.js vscode/focus.test.js`: 35 passed, 0 failed.
- VS Code API (ctx7 `/websites/code_visualstudio_api`): `WindowState.active` is documented only as "Whether the window has been interacted with recently. This will change immediately on activity, or after a short time of user inactivity." Which inputs count as activity is not documented.
