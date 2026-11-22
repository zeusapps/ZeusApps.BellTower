# Proposal

## Why

A dev Switch or Rollback finishes only after the tray it restarted confirms it is ready. A tray that can never become ready therefore leaves the operation permanently unfinished, and its coordination records then block every later Preflight and Switch. On 2026-11-22 this blocked the maintainer's installation completely. `state.json` was at its 16 MiB budget, so the pre-fix trays (0.1.9022, 0.1.9023) never completed a refresh and never wrote presence. The 0.1.9022 → 0.1.9023 Switch of 2026-11-21 stopped after selecting 9023. Rolling it back restored the 0.1.9022 selection and the monitoring snapshot, then failed with "Selected tray did not confirm ownership" and kept `updates/restart.json`. Every new Switch is now refused ("An interrupted update needs inspection"), and retrying the Rollback waits for the same readiness. The only build that repairs the state (0.1.9026, which includes `bound-ended-session-state`) cannot be selected.

## What Changes

- New dev-install action **Finalize** (`install-dev.ps1 -Action Finalize -OperationManifest <path>`). It ends an unfinished Rollback whose original selection and monitoring snapshot are durably restored, without a tray confirmation. A Switch whose candidate never becomes ready keeps Rollback as its recovery, and Finalize applies if that rollback's tray cannot become ready either. It runs only when no installation tray is running or starting, verified under the operation's tray-instance reservation and launch gate. It removes only the coordination records whose ID and token match the manifest, and records a terminal `finalized-without-tray` state with the reason. It never stops a process. It never changes the selection, payloads, launcher or monitoring state, and never starts a tray.
- Finalize refuses anything else: a Switch, a rollback whose selection or monitoring step is incomplete (Rollback remains the recovery for those), coordination records it does not own, a product update's records, a running or starting tray, or a selection that differs from the operation's recorded destination. Each refusal names the reason and the supported next step.
- **Not-ready diagnostics.** When a restarted tray does not confirm in time, or Preflight finds a tray-instance object without verifiable presence, the message names what can be observed: the tray process is alive or gone, presence is missing, the `state.json` size against the budget, and recent repeated `tray-refresh-failed` log lines. It states the next step: quit the tray from its menu, then run Finalize or Rollback with the given manifest. The operation record stores the same facts.
- The install-dev-build skill (identical Claude Code and Codex copies), `docs/development.md` and `docs/releases.md` describe Finalize, when to use it, and that the maintainer starts the tray afterwards (`bell-tower start`, or the next Switch to a build that recovers the state).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `development-builds`: the requirement "Supported dev installation with rollback" gains the Finalize step for an operation whose restarted tray never confirms readiness, plus not-ready diagnostics. Existing Preflight, Switch, Rollback and Retire behavior is unchanged.

## Impact

- `scripts/install-dev.ps1`: the new `Finalize` action and the not-ready diagnostics in the readiness wait and the owner check.
- `tests/BellTower.Tests/Scripts/` (InstallDevScriptTests, Recovery and the function harness): disposable fixtures for a tray that never confirms readiness after Switch and after Rollback, Finalize success, each refusal including a Switch target, and a Preflight and Switch that run after Finalize.
- `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md` (kept identical), `docs/development.md`, `docs/releases.md`.
- Claude Code and Codex: both use the same script and the same skill text, so there are no provider-specific paths. Agent sessions of either provider, and mixed concurrent sessions, are never stopped or changed by Finalize.
- Out of scope: the product's own stable-update coordinator. A stable update to a build that recovers the state becomes ready by itself. Changing when the tray writes presence (a tray that cannot refresh is correctly not ready). Editing or deleting monitoring state.
