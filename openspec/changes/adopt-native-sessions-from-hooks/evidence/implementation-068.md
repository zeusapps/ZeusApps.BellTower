# Implementation evidence 068 (DEV-068)

Approved final round for tasks 5.2 and 5.3 ahead of 6.5. Baseline 9550ced plus the uncommitted 4.11 and 4.12 work. Awaiting independent verification; no finding is closed here.

## What changed (both providers, one rule)

- QA-6300: the exit-reset wording in the capability matrix (Claude and Codex rows), doctor `nativeSessions.*.titleAndColor` and `docs/compatibility.md` / `docs/installation.md` now states the observed trial 6.1 result: exit restoration (prior title restored, background reset to the terminal default, other tabs unchanged) in Windows Terminal for plain `claude` (2026-11-22) and the mixed case 7 (2026-11-23, host mix not itemized). A VS Code-specific and a Codex-specific reset were not itemized (not performed); no provider restriction is claimed. The matrix and doctor share one constant (`CapabilityMatrix.ExitResetEvidence`); `CapabilityMatrixTests` pin it and the absence of "assumed".
- ANALYSIS-5801: `docs/installation.md` states that a chip click on a plain default `codex` (shared-backend) session hides the wait's timer once the reason is shown, and that every other failed click leaves it visible, for both providers; `docs/compatibility.md` "Mode cross-check" adds that the managed daemon (`app-server --managed-daemon`) is recognized without the parent check.
- ANALYSIS-5800(a): new read-only `HookIngressImageReporter` (`src/BellTower/Diagnostics`). It reads the owned hook commands recorded in `integration.json` (via `HookDeliveryReporter.RecordedCommands`), extracts the image hash from `provider-ingress/1/<hash>/bell-tower-ingress.exe` (the same extraction serves the Claude bash literal and the Codex PowerShell, cmd and bash forms) and compares it with the selected payload's `release.json` ingress hash. Per provider `hookStart.ingressImage.<provider>` is `current`, `stale` (with the advice "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks") or `unknown` (no record, unparseable, legacy encoded command, or no usable selected hash; never reported as stale). `hookStart.availability` reads "needs a setup refresh: ..." when either provider is stale; `hookStart.available` is unchanged. No provider settings are written and update and dev-switch behavior is unchanged (ANALYSIS-5800(b) is deferred). Docs: `docs/installation.md` hook-start paragraph and the doctor description.
- Follow-ups added to `docs/follow-ups.md` (new section for this change): ANALYSIS-5800(b), 5802, 5803 with 2405, the about 4 s hook-to-color latency, 5100 and 5401.

## Tests

`tests/BellTower.Tests/Diagnostics/HookIngressImageReporterTests.cs`: equal hash is current and stale for a different hash, each for Claude and for Codex in PowerShell, cmd and bash forms; one stale provider only; no manifest, legacy encoded command, hashless path, null or malformed selected hash are unknown; availability with and without a launcher; upper-case hash comparison. `CapabilityMatrixTests` updated for the observed wording.

## Checks

See the handoff block for the commands and results. The doctor change was exercised through fixtures with disposable data directories only; no live Bell Tower, installed build, provider settings or tray was touched.
