> Saved by the orchestrator (Claude Code, main session, `claude-opus-5-5`) verbatim from the code-analyzer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.310, auto mode, `claude-opus-5-5`. Refusal: the client refused the reviewer's write to this path ("subagents return reports as text"); see the known Claude Code analyzer write restriction in `docs/agent-workflow.md`.

# Analysis 001 evidence: probes

Reviewed identity: `head=9b26088 baseline=9b26088 files=27 diff=6146e35f8ede untracked=6/4ffcd8e5d034`. All probes used disposable scratch data.

**Probe A (focus bounce).** Script: `%USERPROFILE%\AppData\Local\Temp\claude\D--Code-ZeusApps-BellTower\c119dd5f-7598-4282-8833-10399c12c348\scratchpad\bounce-probe.js`, run with `node <script> D:/Code/ZeusApps.BellTower/vscode/service.js <scratch>/probe-data`.
1. It builds a real `Bridge` with `expectedOwnerPid: 500`, `isFocused: () => state.focused`, and a `sampleWindow` whose first call stays pending until released, then resolves to B `{handle:'2222', className:'Chrome_WidgetWin_1', ownerPid:500, ownerStartedUtcTicks:'7'}`. Later calls resolve to A (`'1111'`).
2. Sequence: `noteFocus()` (sample in flight) → `focused=false` → `focused=true` → `noteFocus()` → release → await both → `tick()` → read `hosts/window-a.json`.
3. Output: `{"samples":1,"recordedHandle":"2222","windowDirty":false}`. The second focus gain was dropped, and A published B's handle.

**Probe B (spawn latency).**
- Command: `D:\Code\ZeusApps.BellTower\src\BellTower\bin\Release\net10.0-windows\bell-tower.exe foreground-window --data-dir <empty scratch dir>`, 10 sequential runs, PowerShell 7 Stopwatch.
- Result: min 152 ms, max 180 ms, avg 161.3 ms, all exit 0. The 30 ms internal sleep is included. `process-info` took 158 ms. The scratch data dir was still empty afterwards.
- Not measured: the single-file installed payload, the launcher hop, cold start, antivirus, CPU time.

**Node suites:** `node --test vscode/service.test.js vscode/focus.test.js` → 25 passed, 0 failed.
