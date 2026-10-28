# Native provider hook evidence 003

## Scope

Bounded investigation of whether installed Codex CLI 0.164.0 can be instructed through a supported, process-local CLI setting to select Windows `cmd.exe` or Git Bash for native command hooks. This is an evidence-only task 2.1 investigation. No provider settings, global shell settings, PATH, system files, real data, or active sessions were changed. No native cmd/Bash hook run was attempted after pinned-source tracing showed no supported local-CLI selection control to use safely.

## Version-pinned source trace

The inspected upstream source is the official Codex tag [`rust-v0.164.0`](https://github.com/openai/codex/tree/rust-v0.164.0).

1. [`shell_detect.rs`, `get_user_shell_path`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L125-L128) returns `None` on non-Unix platforms. On Windows, [`default_user_shell_from_path`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L332-L353) ignores the supplied path and tries PowerShell, falling back to `cmd.exe` only if PowerShell discovery fails.
2. PowerShell discovery searches the process `PATH`, then checks fixed fallback locations for PowerShell 7 and Windows PowerShell: [`get_shell_path`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L189-L211), [`get_powershell_shell`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L245-L272). Therefore removing PowerShell from PATH alone does not prove the cmd fallback; the pinned fixed paths can still resolve PowerShell.
3. Core initializes the local shell from its internal session `user_shell_override`, when present, or `default_user_shell()` otherwise: [`session.rs`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/session/session.rs#L1393-L1396). The local environment resolver attaches that `local_shell` to the environment: [`environment_selection.rs`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/environment_selection.rs#L907-L943). Hook configuration receives the selected turn environment's shell program and arguments: [`session/mod.rs`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/session/mod.rs#L5340-L5372).
4. The per-tool optional `exec_command.shell` argument is resolved into that one tool invocation's command: [`unified_exec.rs`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/tools/handlers/unified_exec.rs#L22-L44), [`get_command`](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/tools/handlers/unified_exec.rs#L90-L115). It is not evidence that hook configuration or the turn environment's shell changes. This route was excluded as an unproven hook-shell override.

The installed CLI's `--help` exposes general `--config key=value` overrides but no shell-selection option. The current Context7 Codex CLI documentation lookup likewise describes Windows shell resolution as a PowerShell-first resolver with a cmd fallback, without a user-shell config key. The pinned 0.164.0 source above is the version-specific basis for this finding; the current docs are not used to infer 0.164.0 behavior.

## Result and limits

- **Verified local mode:** Codex CLI 0.164.0 on Windows chooses its discovered PowerShell shell for the local turn environment when available. Prior isolated native trials recorded PowerShell 7 hook execution in two environment variants and delivery with the PowerShell call form; see [native investigation 001](native-investigation-001.md) and [implementation evidence 001](implementation-001.md).
- **Fallback rule:** Pinned source establishes `cmd.exe` as the Windows fallback when PowerShell discovery fails. This investigation did not make PowerShell disappear from its fixed system locations and did not observe native hook dispatch through that fallback.
- **Git Bash:** The pinned Windows `default_user_shell_from_path` path does not consult the user-shell path argument and tries PowerShell before cmd; this evidence does not establish Git Bash selection by the native local CLI. A separate executor reporting Bash would be a distinct mode and was not exercised here.
- **No hard restriction claim:** A public, supported process-local switch to force the 0.164.0 local hook shell was not identified. The source-backed resolver and missing documented CLI option are not proof that every app-server, external executor, or future-version mode lacks such a control. No exception or universal support claim follows.

No acceptance is claimed for a native cmd-fallback or Git Bash hook run. An actual cmd invocation probe, if used, is shell-adapter evidence only and must be labeled separately from execution through Codex's native hook runner.
