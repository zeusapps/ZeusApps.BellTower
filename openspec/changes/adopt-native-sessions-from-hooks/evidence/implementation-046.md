# Implementation evidence DEV-046 (tasks 5.2 and 5.3)

Baseline `1c80a7d`, on top of DEV-045 (uncommitted). Awaiting independent verification.

## 5.2 Capability matrix and doctor

`Diagnostics/CapabilityMatrix.cs` keeps the vocabulary (supported, restricted, unverified, not applicable) and adds four rows, each stating both providers:
- Terminal navigation for adopted direct sessions: supported; window only in Windows Terminal with the setting off, the tab with it on; VS Code through the bridge; balloon for partial results; live trial 6.1 open.
- Title and color for adopted direct sessions: supported behind the off-by-default setting, after the first accepted hook, with the exit, switch-off or identity-loss reset to the terminal default (OSC 110/111). The reset effect in Windows Terminal and the VS Code terminal is stated as assumed until the 6.1 trial.
- Plain default codex, tracking, alerts, name sync and permission status: supported (Claude Code: not applicable).
- Plain default codex, navigation, title and color: unverified, "unfinished: user scope decision 2026-11-13, not a provider restriction", naming the Codex profile and `codex --no-daemon` as the supported routes. No proof/fallback fields are used, so nothing is claimed as restricted.

The removed task 4.2 is no longer cited as pending work: the shared-conversation rows, `CodexNameModes`, the human-wait-without-runtime row and the doctor `attachment` and `confirmationCoverage` texts now cite the 2026-11-13 user scope decision. ANALYSIS-3100: the human-wait row and doctor `codex.approvalResolution` name the shared backend alongside the owning runtime as a `waitingOnApproval` confirmer.

Doctor gains `nativeSessions`: the setting state read from the data directory's configuration, the navigation precision now and the two precisions per provider (Claude direct and Codex `--no-daemon`), the presentation text, the plain default Codex supported and unfinished texts and routes, and the Claude mode cross-check stated as not applicable. Not added: version table, unrecognized shapes, cross-check evidence field, `CodexNameModes` refactor.

Tests (`CapabilityMatrixTests`): new matrix-row test for both providers, doctor test for the setting off and on, and updated pinned texts (`task 4.2` became the scope decision). `CompatibilityDocumentStatesMatchTheMatrixSource` checks that `docs/compatibility.md` has every new row with matching statuses.

## 5.3 Documentation

- `docs/compatibility.md`: the four rows, a "Native sessions: plain claude and codex" section (modes, setting, first-hook timing, plain default codex and its two routes, Codex cross-check versus the Claude cross-check not applicable, QA-3600), the shared backend as a human-wait confirmer in the prose, and the 4.2 references replaced by the scope decision.
- `docs/installation.md`: `setup --host-bridge --apply` (options match `SetupCommand`: `--host-bridge`, `--clients`, `--remove`, `--apply`), Codex `/hooks` re-trust, a "Plain claude and codex" section (click result, the **Title and color native sessions** setting, first-hook timing, no pre-launch coloring for native sessions, plain default codex and the routes, doctor), and `nativePresentation` in the configuration description.
- `docs/attachment-health.md`: the grant rule, the `title-restore` record, restoration rules and the unfinished shared-backend note.

No text promises shared-Codex terminal support, pre-launch coloring for native sessions or verified host reset behavior. Hygiene: no user names, home paths or machine details were added.

## Limitations

The 6.1 trial remains the acceptance evidence for the reset behavior and live precisions. `docs/session-titles.md` and `docs/architecture.md` were not changed (no stale 4.2 or native-presentation statements found there).
