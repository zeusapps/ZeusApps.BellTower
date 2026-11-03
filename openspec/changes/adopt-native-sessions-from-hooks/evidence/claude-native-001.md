# Claude native SessionStart/process probe 001

Date: 2026-10-29. Scope: task 1.1 Claude Code native SessionStart delivery and exact launched-process ancestry for the stable ingress/capture implementation. This is headless process evidence only; it does not establish a terminal binding.

## Current CLI and documentation

The installed Claude Code reports version 2.1.312. Its local CLI help confirms `--print`, `--settings`, `--setting-sources`, `--tools`, and `--no-session-persistence`.

Context7 resolved the official Claude Code library `/anthropics/claude-code`. I checked the current [Hooks reference](https://code.claude.com/docs/en/hooks), [CLI reference](https://code.claude.com/docs/en/cli-reference), and [Settings files and precedence](https://code.claude.com/docs/en/settings). The hook reference says `SessionStart` fires when a session begins or resumes and that command-hook input is JSON on stdin. The CLI reference documents print mode as noninteractive. The settings reference documents the user settings scope; the probe redirected it to an isolated `CLAUDE_CONFIG_DIR` and restricted loaded sources to `user`.

## Synthetic declared-shell validation

Before native launch, I invoked the exact generated Claude ingress command through the installed Git Bash executable specified by the generated `shell: "bash"` handler. The event used a fresh synthetic SessionStart ID, an existing nested fixture working directory, and a separate fresh capture directory. The environment contained no inherited Bell Tower, Codex, terminal, or Claude session ownership metadata.

Result: Bash exited 0 with the expected empty JSON response and no stderr; exactly one fresh capture was written, with provider `claude` and registration source `startup`. This is a synthetic handler-shell check, not native hook evidence. Its data is under ignored `artifacts/claude-native-001/synthetic-*`.

## Bounded native print-mode launch

One Claude Code 2.1.312 process was launched in `-p` mode from the existing nested fixture working directory, with an isolated user configuration and explicit settings file, `--setting-sources user`, `--tools ""`, and `--no-session-persistence`. The sole configured hook was the generated SessionStart ingress handler. The launch environment was constructed explicitly and contained a dummy API key plus a closed loopback model endpoint; no account credentials, user settings, or inherited Bell Tower/provider/terminal ownership variables were available to the child. No trust bypass, permission bypass, or administrator action was used.

The isolated capture directory had no prior captures. Claude emitted one fresh `SessionStart` registration with source `startup` and a nonempty provider-generated session ID. The captured ancestor list contains `claude.exe` with the exact PID and creation ticks recorded for this launch; its image basename matches the launched image. The recorded hook-shell identity matches a `bash.exe` ancestor, and the attachment process identity matches the launched Claude PID and creation time. The matched process is the launched Claude child, not the more distant outer Codex process. Capture time is after child creation.

The bounded ConPTY helper returned exit code 124 at the configured 25-second limit. The capture had already been created and passed the identity checks. The helper runs the target in a private Job object and its identity record confirms the launch image and creation time; the exact child was no longer running after the helper returned. No model response was required or observed. Raw process IDs, session ID, settings paths, environment values, and captured JSON are retained only in ignored artifacts, not this evidence file.

The process ran in print mode and the capture reported an unknown host. No terminal was present or claimed. VS Code/Windows Terminal binding, Codex behavior, provider UI trust flows, and mixed-client correlation are outside this Claude-only probe.
