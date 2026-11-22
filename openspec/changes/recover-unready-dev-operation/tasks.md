# Tasks

## 1. Diagnostics

- [x] 1.1 Add the not-ready diagnostic helper (design decision 4) and use it in the readiness-deadline failure and in both `Read-Owner` unknown-owner refusals. Store the facts as `trayNotReady` in the manifest on a readiness failure. Verify with disposable fixtures: a fake tray that stays alive without presence, with an over-budget `state.json` and repeated `tray-refresh-failed` lines, yields a message naming the live process, missing presence, size against budget, the failure count and span, the quit-from-menu step and the manifest path. A fixture with no log and a small state reports those facts honestly. The message never contains the data-directory or profile path.

## 2. Finalize

- [x] 2.1 Add `Finalize` to the `-Action` set and the script help, with the preconditions and effect of design decisions 2 and 3. Verify with disposable fixtures: a rollback in `restored-monitoring-snapshot` whose tray never confirmed, with no tray running, is finalized; only its two owned records are removed; the manifest records `finalized-without-tray` with the stored reason; the selection, payloads, launcher, `state.json` and `state.json.bak` are byte-identical before and after; no process is started or stopped.
- [x] 2.2 Verify each refusal changes nothing and names its next step: a Switch manifest (any state) names Rollback; a rollback with an incomplete monitoring or pointer step names Rollback; a selection that differs from `originalSelectionSha256`; foreign or product-update coordination records; a running tray holding the tray-instance object (its PID and the quit-from-menu step); a tray start in progress under the reservation; changed original payload bytes.
- [x] 2.3 Verify the interrupted-finalize retry: with the manifest already `finalized-without-tray` and the owned records still present, Finalize removes them; with neither present it reports that the operation is already finalized and changes nothing.
- [x] 2.4 Verify the end-to-end recovery on disposable data. A switch whose candidate never confirms readiness is followed by a rollback whose original also never confirms, and then by Finalize. After that, Preflight and Switch for a new candidate succeed, are not refused by `EnsureNoCoordinator`, and report that no tray was restarted because none was running.

## 3. Documentation and parity

- [x] 3.1 Document Finalize and the diagnostics in `docs/development.md` (Development builds, Recovery) and `docs/releases.md` (interrupted switch). Add the same section to `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md`: when to use it, that it needs explicit authorization like Switch and Rollback, and that the maintainer starts the tray afterwards. Verify that the existing skill-copy sync test passes, and that the docs name the same preconditions as the script help.

## 4. Acceptance

- [x] 4.1 Run the Release build, the install-dev script test classes and the full .NET test suite, plus format verification and `openspec validate recover-unready-dev-operation --strict`. Record the results and name any known unrelated failures.
- [x] 4.2 One QA review and one code-analyzer review of the stable implementation target. Resolve blocking findings and record suggestions as deferred.
- [ ] 4.3 Installed recovery on the maintainer's installation, with the user's authorization for each step. Quit the stuck tray, run Finalize with the 2026-11-21 operation manifest, then Preflight and Switch to a dev build containing `bound-ended-session-state` and start the tray. Confirm the selection is the new build, the tray writes presence, `update --status` reports it running, and no agent session of either provider was stopped. Record the result, or Not performed.
