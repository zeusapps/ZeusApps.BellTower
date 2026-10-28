# Implementation evidence 001

Developer role model: `gpt-6-luna`.

## Scope and candidate

Implemented the Codex Windows hook command correction, provider-runner setup probes, owned-command migration diagnostics, and the shared H-2 correlation fix. Built isolated package candidate 0.1.9013 under ignored `artifacts/dev-builds/`; it was not installed and no user provider settings or data were changed. The packaged helper was used only with disposable provider settings, projects and data. The package identifies itself as `0.1.9013-dev+aadbfb3.dirty`; a frozen file-identity manifest was not captured at package time, so exact package-to-final-source mapping is unverified.

The earlier read-only field check found 10 Bell Tower-owned Codex entries in the old command-shell form. The referenced data held 9 Codex event captures and 34 empty event objects, with no post-0.1.9-release captures at the time checked. This history is consistent with H-1 but does not establish current loss of events.

## Evidence by task

| Task | Implementation and evidence | Status for orchestrator |
| --- | --- | --- |
| 1.1 | Codex 0.164.0 native hook-runner probes in two shell environments observed PowerShell 7; the corrected packaged candidate also delivered SessionStart through the runner. The `command` property is still emitted as the provider's string command. A separate argv-array attempt remained running without a capture or a parser error, so argv-array acceptance/rejection is **unverified**. | Partial; source selection proven for tested environments, argv form unresolved |
| 1.2 | Read-only settings/data summary above; no settings were modified. | Evidence recorded |
| 1.3 | Direct Claude command delivered a durable capture through Git for Windows Bash. `CLAUDE_CODE_GIT_BASH_PATH` is preferred when configured; otherwise the standard Git Bash path is preferred over PATH entries that can resolve to a WSL relay. The Claude CLI itself was not launched by this shell probe. | Shell probe passed; native Claude CLI rule remains unverified |
| 2.1 | Codex Windows commands now use PowerShell call syntax; Claude retains Bash quoting. Tests exercise spaces, apostrophes and Unicode. The real-runner probes used a packaged helper copied under a `Δ O'Brien helper` path; both providers delivered durable captures. | Command and quoting checks passed |
| 2.2 | Provisioning stages the immutable helper at its retained target and probes that exact path before provider-settings writes. Claude uses configured/default Git Bash and a synthetic event; Codex uses isolated `CODEX_HOME`, a native SessionStart hook, a disposable data root and a loopback fake Responses endpoint. Both produced durable captures. `SetupPlan.Apply` runs provisioning before writing settings. | Passing probes; failure-before-write is enforced by call order, but no dedicated failing-setup settings snapshot fixture was added |
| 2.3 | Corrected benchmark uses the verified provider shells and records a matched no-op startup baseline. At 120 samples and concurrency 8, Claude native (121 including cold): median 142.4750 ms, p95 156.2648 ms, p99 169.0512 ms, max 176.1528 ms, 0 failures. Codex PowerShell no-op (120): median 343.9491 ms, p95 410.0838 ms, max 434.9241 ms. Codex direct ingress (121 including cold): median 385.4529 ms, p95 492.4313 ms, p99 594.4172 ms, max 595.8976 ms, 0 failures and 121 durable captures. The Codex median exceeds the required 300 ms gate. The no-op comparison suggests most observed time is shell startup; this is an inference from separate sample distributions, not a measured per-call helper cost. | **Failed latency gate**; no performance pass |
| 3.1 | Doctor counts configured commands only when they exactly match recorded `integration.json` ownership. It identifies the known old quoted Codex form and reports the previewed `setup --clients codex --migrate-edited-hooks` action, with `--apply` called out as the backup-writing step. Existing migration regressions cover surfaced timeout/async edits, unrelated settings, backups, and repeat-apply idempotence. | Automated checks passed |
| 3.2 | Doctor reports per-provider configured/delivered/unverified status, last delivery time, known broken owned command count and migration action. Tests cover both providers and ensure lookalike unowned commands and private command text are not reported. Capability matrix and docs describe the output. | Automated checks passed |
| 4.1 | Existing request-before-start fixtures reproduced H-2 for Claude and Codex on captured and normalized ingress before the fix. | Reproduced |
| 4.2 | Shared reducer uses operation identity and candidate membership so a late matching start keeps the wait; a later different operation can establish resumption. Results, stop and end continue to resolve through their existing correlated paths. Matched captured/normalized tests cover both providers, candidate waits and resolution. | Focused tests passed |
| 5.1 | Updated hook ingress, installation, compatibility and follow-up docs; replaced stale doctor trial wording with the archived user-operated 2026-10-27 scenarios 1–6 and their scope. Those prior trials validate human/automatic wait behavior only; their PowerShell-call Codex hook does not accept this generated-command fix. Formatter and Release build passed. | Documentation and checks passed |
| 5.2 | Current package candidate passed isolated real-runner probes with special-character helper paths. The user-operated post-migration packaged trial, mixed simultaneous real sessions and trust review were not performed. | Pending user-operated acceptance |

## Commands and results

- `.\scripts\package.ps1 -CheckToolchain` -> passed; repository setup found the native build toolchain.
- `.\scripts\package.ps1 -Dev -Offline` -> passed; produced ignored candidate 0.1.9013.
- `BELLTOWER_RUN_NATIVE_HOOK_PROBES=1` with `BELLTOWER_HOOK_PROBE_INGRESS` set to the package helper, filtered `OptInNativeProbeConfirmsRealProviderHookDelivery` -> 2/2 passed through Claude Git Bash and Codex 0.164.0 native SessionStart runner.
- `dotnet build -c Release --no-restore` -> passed, zero warnings and errors.
- Focused `CaptureTests|FastHookSetupTests|HookDeliveryReporterTests` -> 49/49 passed.
- Native-probe filter after formatting -> 2/2 passed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include ...` -> passed for the owned source, test and benchmark paths.
- Full package benchmark -> stopped at the intended Codex 300 ms latency gate. It did not enter the subsequent state-drain/crash-replay stage; no replay failure was observed.

Raw benchmark output remains in ignored `artifacts/codex-hook-shell/latency-20261027-07/latency-report.json` and `artifacts/codex-hook-shell/latency-20261027-08/latency-report.json`. Both runs were outside the default sandbox. Run 07 recorded Codex direct median 384.58 ms and no-op median 344.02 ms; run 08 recorded 385.45 ms and 343.95 ms respectively. The second full run is the reported candidate result.

## Limits

The exact Codex argument-vector hook form remains unverified, as does shell selection outside the two probed Codex environments and the Claude CLI's own runner invocation. The direct Codex form fails the specified latency budget despite delivering all captures. Task 5.2 remains pending; no provider settings, user installation, active agent process or user data were changed.
