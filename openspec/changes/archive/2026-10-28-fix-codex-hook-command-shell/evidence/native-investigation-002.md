# Native provider hook evidence 002

## Scope

Read-only source investigation for OpenSpec task 1.1. The source inspected is OpenAI's official Codex repository tag `rust-v0.164.0`, matching installed Codex CLI 0.164.0. No CLI/model probe or product edit was performed. Three targeted source lookup/fetch attempts were used; all relevant source URLs resolved.

## Version-pinned source findings

1. In [`codex-rs/core/src/session/mod.rs` at `rust-v0.164.0`](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/core/src/session/mod.rs#L5340-L5372), `build_hooks_config` takes `environment.shell`, derives its execution arguments, and stores the program and arguments in `HooksConfig.shell_program` / `shell_args`. This ties command hooks to the shell selected for the turn environment when that environment carries a shell.
2. In [`codex-rs/shell-command/src/shell_detect.rs` at `rust-v0.164.0`](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L332-L353), `default_user_shell_from_path` on Windows selects `ShellType::PowerShell` and falls back to the platform's ultimate fallback shell if PowerShell cannot be resolved. In this tag, that ultimate Windows fallback is `cmd.exe`.
3. In [`codex-rs/hooks/src/engine/command_runner.rs` at `rust-v0.164.0`](https://raw.githubusercontent.com/openai/codex/rust-v0.164.0/codex-rs/hooks/src/engine/command_runner.rs#L381-L455), `build_command` starts the configured shell and passes the hook command line to it. If no shell program is configured, Windows reads `COMSPEC` from the session environment and otherwise uses `cmd.exe`, adding `/C`. If a shell program and args were supplied, it uses those instead; the `cmd /c` branch receives a raw quoted command argument.

## Task 1.1 implications

- **Supported by pinned source:** the hook runner inherits the shell program and arguments from the selected turn environment; the normal Windows default shell type is PowerShell, with `cmd.exe` as the ultimate fallback. `COMSPEC` controls the runner only when the passed shell program is empty.
- **Proven by native evidence:** installed Codex CLI 0.164.0 ran hooks through PowerShell 7 in the two isolated environment variants previously recorded in `implementation-001.md`; the PowerShell call-form hook delivered. The installed parser rejected `hooks...command` as an argv array in `native-investigation-001.md`.
- **Still unverified:** the exact resolved PowerShell executable path for the historical probe environment and all path-priority/fallback branches are not established by these source excerpts. The source explains why changing `COMSPEC` alone need not change a hook's shell when `environment.shell` supplies a program. No support is shown for an alternate argv or per-hook shell field in the inspected runner/config path; only the `command` string-array shape was directly parser-tested. This is not evidence that every possible alternate exec handler form is rejected.

The exact shell-selection mechanism is therefore available in the official version-pinned source at the program/configuration level, while exact executable path selection for a particular host still depends on that host's shell environment and resolver inputs.
