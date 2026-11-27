# Implementation 002 (DEV-002): fix cycle 1

Developer handoff, 2026-11-25, baseline `02d4f56`. Fixes ANALYSIS-040, ANALYSIS-041 and ANALYSIS-042 (awaiting independent verification). No checkbox, planning artifact, handoff or spec was changed. Nothing was committed.

## ANALYSIS-040: version declarations reverted

User decision 2026-11-25: keep the declarations at the published 0.1.9 until candidate packaging (task 4.4); 0.1.11 stays the selected candidate, recorded and not applied.

- Restored from `02d4f56`: `Directory.Build.props`, `vscode/package.json`, `README.md`, `tests/BellTower.Tests/packages.lock.json`, `tools/BellTower.Ingress.Bench/packages.lock.json`, `tools/BellTower.Smoke/packages.lock.json`. `git diff 02d4f56 -- <those files>` is empty.
- `pwsh scripts/package.ps1 -Dev -PlanOnly` reports `baseVersion` 0.1.9 (dev version 0.1.9033).
- `evidence/implementation-001.md` ("Task 1.3") and the ledger header now record 0.1.11 as the selected candidate with its rationale, state that task 4.4 synchronizes the declarations and lists them, and cite the user decision. The DEV-001 build and test results are marked as run before the revert.

## ANALYSIS-041: upgrade risk U1

Ledger changes: new "Upgrade risks" section with row U1; F1 and F4 link to it; G8 now treats ANALYSIS-5800(b) as decided (no automatic re-provision this release, user decision 2026-11-25) and points to U1; the Part 4 conclusion references U1.

Claims checked against sources:

- `src/BellTower/Diagnostics/HookIngressImageReporter.cs`: hook commands carry the image content hash, an update leaves them alone until setup is rerun, status `stale` with the advice "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks" (surfaced as `hookStart.ingressImage` per the adopt archive and the DEV-001 record).
- `src/BellTower.Ingress/Program.cs`: the current file calls `AttachmentCapture.Resolve` (line 36) and `HookTrayStart.TryStart` (line 65). At `v0.1.9` the ingress calls only `AttachmentCapture.Capture`, and `src/BellTower.Protocol/HookTrayStart.cs` does not exist in that tree.
- Repaired Codex hook command: `HookCommandBuilder.cs` changed after the tag (commit 3a757de).
- Adopt archive task 6.1 ran "after `setup --host-bridge --apply`", so its live trial did not cover the upgraded-without-setup state.

Owners recorded in U1: task 2.1 (upgrade without and with the setup rerun, both providers and mixed), task 2.4 (rollback to 0.1.9 while hooks reference the candidate image), task 4.4 (release notes with the setup step and the possible Codex trust prompt), and the user decision. I did not run the doctor on the installation. Provider coverage today is stated as none for upgraded-without-setup.

## ANALYSIS-042: shipped versus archived

- Ledger header now defines "Shipped in" versus "archived".
- F7 and the Part 2 table: `follow-openspec-session-titles` (commit 1e63f85) and `restore-claude-badge-identity` (commit 4db8dda) shipped in 0.1.9 (both commits are ancestors of `v0.1.9`; archived 2026-10-29). `publish-github-with-session-safe-updates` shipped in 0.1.8 (archived 2026-10-29; `ReleasePackage.cs` and `UpdateTransaction.cs` exist in `v0.1.8`). Archive dates were added to the other rows from the archive directory names. The remaining "after 0.1.9" labels were not re-derived beyond a spot check (`IMicrophoneActivitySource.cs` and `ReminderPolicy.cs` are absent from `v0.1.9`; `AudioQueue.cs` is present, which predates the microphone work).
- `git cat-file -e v0.1.9:<path>` succeeds for `IdentityControls.cs`, `NativeNamePolicy.cs`, `IdentityResolver.cs`, `CheckoutPolicy.cs`, `WorkClassifier.cs`, `ShellGrammar.cs`, `LegacyImport.cs`, `Palette.cs`, `OpenSpecSelection.cs`, `IdentityPresentationStore.cs` and `IdentityPresentation.cs`. Only commit dc0fe90 (distinct colors) touched `Palette.cs` and `IdentityPresentationStore.cs` after the tag.
- New baseline-data note under Part 2: 0.1.9 already writes identity stores, pins, presentation records and palettes; the candidate preserves them and a rollback lets 0.1.9 read them (tasks 2.1 and 2.4).

## Proposed planning changes for the orchestrator

- Handoff `docs/handoffs/next-release-plan.md` row 3 and the "Post-0.1.9 changes" list still label the two identity changes as post-0.1.9, and the proposal "Why" says four retained changes; both belong to the orchestrator (ANALYSIS-042 proposal text).
- Tasks 2.1 and 2.4 should name the 0.1.9 identity stores as baseline data (ledger now says so).

## Checks

- `git diff 02d4f56 -- <six version files>`: empty.
- `pwsh scripts/package.ps1 -Dev -PlanOnly`: `baseVersion` 0.1.9.
- Build, tests and formatter: not run (no source changed; the version files equal baseline).
- Hygiene: no home paths, personal data or secrets added.
