# Shell-selection correction 002

## ANALYSIS-250 / QA-250 — accept a final validated capture after runner exit

Codex probe polling can miss a valid capture if the short-lived CLI writes it and exits between the last directory check and the next loop condition. After the runner exits, the probe now checks the capture inbox again and accepts only a protocol-decoded capture for the requested provider. The in-loop observation is still used to terminate a runner that remains active after delivery. A probe that reaches its deadline without observing delivery remains a timeout, even if a capture appears after the timeout kill.

The deterministic regression fixture writes a real `CaptureProtocol`-validated Codex SessionStart envelope and calls the same completion predicate used after process termination with `observedWhileRunning=false`. It verifies acceptance after a normal exit and rejection for timeout, wrong provider, invalid capture, and a runner that has not exited. It does not depend on scheduler timing.

## ANALYSIS-251 / QA-251 — preserve shell ownership on project-only removal

Global `SelectedCodexHookShell` metadata is now cleared only during full Codex integration removal. Removing a registered project retains the shell selection and unknown manifest fields while leaving the global Codex hooks file byte-identical. A Claude-only setup also retains that metadata. The fixture then performs full Codex removal and verifies the selection clears while the unknown field remains.

## Checks

- Scoped format on `ProviderHookProbe.cs`, `IntegrationSetup.cs`, `FastHookSetupTests.cs`, and `ProjectProfileTests.cs` -> passed.
- `dotnet build tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore -m:1` -> passed, 0 warnings and 0 errors.
- Focused setup, diagnostics and project-profile tests -> 20 passed, 0 failed.
- Broader existing Setup and Diagnostics groups -> 118 passed, 0 failed.
- `git diff --check` -> passed.
- Opt-in native tests with the hash-matched 0.1.9018 helper -> 4 passed, 0 failed. This covers Codex's selected PowerShell runner, Claude's configured Bash command, successful isolated setup, and failure-before-settings-write fixtures. The Codex probe uses an isolated `CODEX_HOME` and loopback fake endpoint.

## Package and source binding

Built with `./scripts/package.ps1 -Dev -Offline`; no installation or production settings change was made.

- Candidate: `0.1.9018`
- ZIP SHA-256: `fe5e164d189c6e423d6d79c2115a291bbd81f64c6c7cd8716cf540910f327a79`
- Standalone ingress SHA-256: `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707`
- Source identity before and after packaging: `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758 untracked=3/cce5a39616ce`
- Source-file hashes before and after packaging matched: `HookDeliveryReporter.cs` `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`; `ProviderHookProbe.cs` `3e6a758b76fe766d46b142ea9d9adb0c4a81ce6d4beecff907c24a0919657a6e`; `HookDeliveryReporterTests.cs` `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Full before/after identity manifests are retained in ignored scratch at `artifacts/codex-hook-shell/continuation-002/source-pre-package-9018.txt` and `source-post-package-9018.txt`.

Native Codex cmd/Bash turn selection and user-operated post-migration trial remain unverified. Direct shell controls do not prove those provider branches. The setup remains fail-closed when the native runner cannot produce a valid capture through a candidate.
