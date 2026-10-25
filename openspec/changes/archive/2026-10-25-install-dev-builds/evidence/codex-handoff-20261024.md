# Codex handoff and skill discovery

- Date: 2026-10-24 UTC.
- Client: `codex-cli 0.164.0` (`codex --version` executed during takeover).
- Mode: interactive root session, delegated implementation and independent QA/code analysis.
- User authorization: "yes, please finish - claude is exhausted" confirms takeover from Claude Code session `zeusapps-belltower-4f`.
- Entry target: `head=b74a12b baseline=b74a12b files=25 diff=f4aff9cd7f69 untracked=9/c5879e991a25`.

## Native discovery observation

The available-skills catalog delivered to this Codex root session includes:

`install-dev-build` at `D:/Code/ZeusApps.BellTower/.agents/skills/install-dev-build/SKILL.md`, described as "Build, preview, install, verify, roll back and retire a local Bell Tower development build on the maintainer's versioned installation. Use when the user asks to install, try, test, roll back or remove the current dev build, or says \"install-dev-build\". Installing needs the user's explicit authorization."

This is an observation of native discovery in a running Codex session, rather than an inference from the file location. The root session read that file and observed build, Preflight, Switch, verification, Rollback and Retire instructions and explicit authorization and session-preservation rules. This is not evidence of a live installation switch or provider configuration changes.

Claude Code 2.1.310 discovery in its running session is recorded in [implementation-001.md](implementation-001.md), section 6.2. The repository's byte-equality tests and final QA comparison establish that the two discovery paths deliver the same final procedure. Recheck the final equality after the developer handoff; the entry copies still describe the superseded `-Legacy` behavior and are assigned for correction.

## Boundaries

The live maintainer Switch remains a separate step after review. Disposable native operations and read-only maintainer Preflight are part of task 6.1. No provider sessions, settings or user tray may be changed to establish skill discovery or parity. Unrelated working-tree edits and the staged change to `restore-claude-badge-identity/reports/findings.md` must remain untouched.
