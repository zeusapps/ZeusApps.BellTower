# Native adoption probe follow-up 002

Date: 2026-10-29. Scope: current-version headless process and hook probes for task 1.1, plus the exact installed Codex CLI invocation and hook-trust boundary. All provider homes, settings, dummy credentials, loopback-only model endpoints and probe data were private and disposable. No production daemon, user session, user provider settings, real credential, model service, or user terminal was used.

## Installed versions and command validation

Installed clients reported Claude Code 2.1.312 and Codex CLI 0.165.0. Codex 0.165.0 `exec --help` places `--json`, `--sandbox`, and `--skip-git-repo-check` on the `exec` subcommand. Top-level help places `--no-daemon` and `--ask-for-approval` before the subcommand. An earlier harness incorrectly put `--ask-for-approval never` after `exec`; the installed binary returned `unexpected argument '--ask-for-approval'`, exit 2. This was a harness argument-order error, not provider evidence. The corrected argv was checked against installed help.

The generated PowerShell hook handler was separately run through `pwsh -NoLogo -NoProfile -NonInteractive -File` with synthetic `SessionStart` input for each provider. Both handlers exited 0 and parsed the event and session ID. These are handler-shell checks only; synthetic ancestry contained the test host process and does not prove provider ancestry or native hook dispatch.

## Current headless probe status

| Client and mode | Probe outcome | Evidence boundary |
| --- | --- | --- |
| Claude Code 2.1.312, `--print`, explicit isolated `--settings` and `--setting-sources`, no tools, dummy key, loopback endpoint | A bounded launch was attempted. The existing capture file was not per-run and its recorded ancestor chain matched the synthetic handler invocation. It cannot establish that this Claude process emitted the hook or that Claude was in its ancestry. | Current native Claude hook dispatch and direct-process ancestry remain unverified. No terminal is claimed. |
| Codex CLI 0.165.0, version-matched native executable, `--no-daemon exec`, read-only sandbox, dummy key, private `CODEX_HOME`, loopback endpoint | A bounded 45-second run after correcting global option order ended without a fresh per-run hook capture. The exact launched process was terminated only after matching its process creation time and executable path. | Current native Codex hook dispatch and direct-process ancestry remain unverified. No terminal is claimed. |
| Codex CLI 0.165.0, npm PowerShell shim, same private probe setup | A bounded run was attempted, but the apparent capture was an older synthetic record with no per-run identity. It is not native evidence. | Current npm-shim dispatch and ancestry remain unverified. |

Current primary Codex documentation retrieved through Context7 describes the headless `exec` hook integration test using `--dangerously-bypass-hook-trust`, and documents a non-interactive trust-bypass path. That option was not used because the probe must retain native hook trust. See the [Codex exec hook test](https://github.com/openai/codex/blob/main/codex-rs/exec/tests/suite/hooks.rs) and [hook input schema](https://github.com/openai/codex/blob/main/codex-rs/hooks/src/schema.rs). Therefore no-capture in this headless run is not evidence of a provider limitation.

I checked for an existing private pseudo-console harness before stopping this bounded probe. The relevant repository test, `tests/BellTower.Tests/Hosts/ConsoleTitleProcessTests.cs`, creates a hidden console with `AllocConsole` but does not provide an interactive TTY. No ConPTY helper was found under `scripts`, `tests`, or `src`; `node-pty` and `winpty` are unavailable. An attempt to inspect the available WSL pseudo-terminal path was denied by the host (`Wsl/E_ACCESSDENIED`). I did not build a new terminal-control harness in this evidence-only assignment. The next required probe is a private interactive TUI that accepts the isolated hook through Codex's native trust UI, with no trust bypass. It must record a fresh per-run capture and match the captured ancestor PID, creation time and executable path to the exact launched provider process before claiming ancestry.

## Disposition

The corrected invocation identified and fixed a probe bug. The available per-run identity probe did not receive a native hook event before its bounded timeout, so it proves neither native classification nor a provider restriction. Task 1.1 remains incomplete for current Claude and Codex direct-process ancestry, and for the Codex npm shim. Headless observations cannot establish a terminal. Continue with private pseudo-console native trust acceptance and a fresh identity-bound capture; keep the shared-Codex client-to-terminal binder as unfinished research until an exact current-version host correlation is demonstrated.
