> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's writes ("Subagents should return findings as text"); see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`.

# Analysis 003 evidence: probes

Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Reviewed identity: `head=8167c5d baseline=9b26088 files=33 diff=3a6df874e2c9 untracked=9/7e8916580128` (same before and after). All probes used fakes and disposable scratch data. No user window, VS Code instance, setting or data directory was touched.

## Probe set 1: guard reason, self-heal, invariant fuzz and unlink handling

- Script: `%USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\analysis-003-probe.js`. It extends analysis-002 probes D and E to also read the published `windowUnavailable` field.
- Command: `node <script> D:/Code/ZeusApps.BellTower/vscode/service.js <scratchpad>/probe3-data`, run with Node v24.13.0.
- Fakes: a real `Bridge` with `expectedOwnerPid: 500` and a scripted sampler.
  - Handle A (`1111`) and handle B (`2222`) share owner 500 and class `Chrome_WidgetWin_1`.
  - Foreign is `3333` with owner 999.
  - The registration is read from `hosts/<id>.json` after `idle()` and `tick()`.
- In the Output column, `(absent)` means the property is not present in the JSON at all.

| Case | Sequence | Output | Reading |
| --- | --- | --- | --- |
| D_lagThenGuard (re-run of probe D) | Published before any focus. Sample 1 is lagged and returns B. Three real focus gains then sample A. After that, self-heal requests name 2222, then 1111 | Before: `{null, (absent)}`. Lagged: `{2222, (absent)}`. Each of the three gains: `{null, "multiple"}`. After the heal attempts: `{null, "multiple"}`. `seen [2222, 1111]`; 4 samples | The reason is published exactly when the guard trips, never before. Self-heal cannot clear it |
| D2_guardThenForeign | B, then A (the guard trips), then a foreign sample | `{null, "multiple"}`; 3 samples | An invalid sample after the guard keeps the reason |
| E_selfHealBeforeRefocus (re-run of probe E) | Lagged B, then a failed reveal naming 2222 while unfocused, then a refocus on A | Lagged: `{2222, (absent)}`. Healed: `{null, (absent)}`. After refocus: `{1111, (absent)}`. `seen [1111]` | Self-heal still recovers before the guard trips, and no reason leaks |
| Fuzz | 300 runs of 25 random steps (7,500 steps). Steps: focus or blur with random `active`, `active`-only changes, self-heal naming 1111 or 2222. Samples are drawn from A, B, foreign or unavailable. A fake clock advances 4 s per call | 229 runs tripped the guard; **0 violations** | Invariants hold after every step: (1) reason `multiple` if and only if `seenHandles.size > 1`; (2) never both a window and a reason; (3) once tripped, no window is ever published again |
| Unlink_ENOENT | Unlinking the request after the receipt fails with a real ENOENT, because the file was already removed | Receipt written; request gone; 0 `A focus request ...` log lines | The ANALYSIS-005 residual log is gone |
| Unlink_EPERM | Unlinking the request throws EPERM | Receipt written; request left queued; 1 log line | Other errors are still logged and the request stays queued |

## Probe set 2: regression re-run (analysis-002 script, unchanged)

- Script: `%USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\analysis-002-probe.js`, the same command against the current `service.js`.

| Case | Output (round 2 → round 3) | Reading |
| --- | --- | --- |
| A_bounce | `samples 2, recorded 1111, dirty false` → identical | ANALYSIS-001 has not regressed |
| A_noteFocusOnly | `samples 2, recorded 1111` → identical | Not regressed |
| C_blurThenRefocus | after blur `1, null, dirty true`; after refocus `2, 1111` → identical | Not regressed |
| D_lagThenGuard | `2222` then `null ×3`; 4 samples → identical | Window is still null; the reason is in set 1 |
| E_selfHealBeforeRefocus | `2222`, `null`, `1111` → identical | Not regressed |
| F_interactionAtLoad | `first 2222`, `afterRealFocusGain null`; 2 samples → identical | ANALYSIS-103 exposure unchanged |
| G_interactionForeign | `recorded null` → identical | Not regressed |
| H_activeChurn | `samples 2, recorded 1111` → identical | Not regressed |

## Checks

- `node --test vscode/service.test.js vscode/focus.test.js`: 39 passed, 0 failed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`, limited with `--include` to this round's C# files:
  - Source: `src/BellTower/IO/AttachmentStore.cs`, `Diagnostics/VsCodeBridgeReporter.cs`, `Tray/TrayApplication.cs`, `Tray/NavigationOutcome.cs`, `Hosts/VsCodeWindowOwnership.cs`, `Hosts/HostRegistration.cs`, `Tray/ChipView.cs`, `Setup/VsCodeBridgeSetup.cs`.
  - Tests: `tests/BellTower.Tests/Hosts/VsCodeWindowNavigationTests.cs`.
  - Result: exit 0.
- Leftovers: each probe deletes its own temporary root in a `finally` block.
