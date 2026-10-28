# Codex hook latency options 001

## Scope and candidate

Bounded, read-only investigation for the remaining task 2.3 performance gate. The source target is the frozen 0.1.9015 candidate described by the orchestrator: 19 changed files, tracked diff identity `262b223e8a79` from baseline `aadbfb3`, independently reviewed for the correlation changes. No product, test, config, documentation, or planning files were changed. No CLI, model, provider, or MCP server was launched.

## Existing measured result

The matched Codex PowerShell no-op benchmark at concurrency 8 measured median startup of **343.9491 ms**. Direct Codex ingress on the corrected hook command measured **385.4529 ms median** over 121 calls (including cold), with 121 captures and no failed calls. The direct path therefore exceeds the agreed 300 ms median gate; even the no-op shell baseline exceeds it. See `implementation-002.md` for the full trial details. Claude's matched native Bash result was **142.4750 ms median** and remains below the gate.

These measurements do not establish that a different command form or native MCP handler will meet 300 ms.

## Version-pinned API findings

The inspected source is OpenAI Codex's official `rust-v0.164.0` tag, matching the installed CLI version.

- The config tests accept a native hook handler of type `mcp_tool` with `server`, `tool`, JSON `input`, and optional `timeout` / `statusMessage`. The test shows input template strings such as `${tool_input.file_path}`. [Pinned config hook tests](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/config/src/hooks_tests.rs#L56-L94)
- The hook dispatcher routes `ConfiguredHandlerKind::McpTool` to `run_mcp_tool` through the engine's `HookMcpExecutor`, bypassing `run_command` and the per-event command-shell launch path. The inspected engine represents MCP handlers with `server`, `tool`, and JSON `input`; MCP handlers are synchronous. [Pinned dispatcher](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/hooks/src/engine/dispatcher.rs#L182-L217), [pinned handler types and mode](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/hooks/src/engine/mod.rs#L112-L124)
- Command hooks remain string-based. The versioned config tests show a Windows-specific `command_windows` / `commandWindows` command-string override, not a per-hook shell selector. The native argv-array `command` form was separately rejected by the actual CLI parser in `native-investigation-001.md`. [Pinned Windows command override test](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/config/src/hooks_tests.rs#L264-L329)
- The pinned command runner starts the shell carried by the turn environment (or, when absent, `COMSPEC` / `cmd.exe` on Windows) and runs the command string through that shell. [Pinned command runner](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/hooks/src/engine/command_runner.rs#L381-L455)

The `mcp_tool` capability is therefore an available shell-independent hook-dispatch candidate in this Codex version. The source inspected here does **not** establish that an arbitrary configured local MCP server is persistent across hook calls, nor its startup/call latency. No transport prototype or performance measurement was made. This is not evidence that Claude has an equivalent native MCP hook; Claude's tested route remains its existing Bash command path.

## Decision options

| Option | What it means | Cost, risk, and status |
| --- | --- | --- |
| Keep the 0.1.9015 candidate as functionally corrected development work and leave task 2.3 open | Retain the proven PowerShell-call Codex hook while recording the 300 ms release gate as failed. | No architecture change. It cannot be called a performance pass or complete release acceptance. |
| Commission a separate Codex MCP-hook design using `mcp_tool` | Evaluate a local MCP adapter that calls the existing shared capture protocol through Codex's native hook dispatch; keep Claude on its measured Bash adapter. | Requires a new product transport and MCP server lifecycle/ownership design, plus failure-before-settings-write behavior, mixed-provider coverage, and end-to-end latency measurements. It is outside the current approved design and needs an architecture decision before implementation. It may remove per-hook shell startup, but meeting 300 ms is unproven. |
| Explicitly revise the latency acceptance budget | Accept a documented threshold informed by the measured 385.45 ms Codex median (and account for the 343.95 ms no-op baseline), while preserving the current direct ingress. | Requires explicit user approval and revised acceptance criteria; it does not turn the existing 300 ms trial into a pass. No budget change is assumed here. |

The pinned-source result makes the MCP route concrete enough to consider, but it does not justify implementing a new transport or waiving the existing gate automatically. No implementation should begin until the orchestrator/user resolves whether to leave the gate unmet, authorize an MCP architecture proposal, or explicitly revise the budget.
