# Implementation verification

Implementation scope and finalized capture-v1/consumer compatibility are exported in [docs/hook-ingress.md](../../../../../docs/hook-ingress.md). Dependent identity, permission and liveness changes should consume that normalized evidence and retain its privacy, ordering and replay bounds. They must not restart provider processes or return to raw payload spooling.

## Executed evidence

- NativeAOT Release win-x64 publication and packaged read-only protocol probe: passed, executable approximately 3 MB.
- First successful installed-shell benchmark: `artifacts/hook-ingress/trial-03/results.json`, 121 native samples per provider at concurrency eight, all 242 durable captures and all under three seconds. Claude median 115.12 ms, p95 131.37 ms, p99 139.80 ms, max 140.63 ms; Codex median 83.19 ms, p95 89.44 ms, p99 102.40 ms, max 111.53 ms. Legacy-chain medians 1535.03 / 1502.01 ms, 25 controls per provider. No OS-cache reset or reproduction of the field machine's timeout rate is claimed.
- Packaged lifecycle run: `artifacts/update-acceptance/run-5422462fc6d943a3b06c8e6437082182`, passed stopped-tray capture, monitoring restart, mixed-provider delivery during selected-version change, immutable image refresh/retention, readiness-failure rollback and existing disposable agent/runtime survival. Fixture baseline 0.1.7 and candidate 0.1.8 were compiled from this change's source; they are not historical-release compatibility evidence.
- Actual redacted provider probes and narrow Codex transcript restriction: [provider-timeout-probes.md](provider-timeout-probes.md).
- Relevant capture/IO/selection/setup/CLI suites, formatting, Release build and final package are checked again after final source edits; final results are appended below when complete.

The initial benchmark harness failed on Windows PowerShell first-use progress CLIXML on the legacy control's stderr. Its unhandled failure caused a Windows error dialog. The harness now catches errors, reports nonzero failure without a dialog and excludes those runs from performance passes. Native ingress still requires silent stderr; every path must produce neutral output and the exact durable count.

One full test run exposed a transient Windows lock on an existing PowerShell fixture startup snapshot (331 passed, one failed). That fixture already renamed a complete temporary file; its reader now waits for readability within the existing startup deadline. Production ingestion was unaffected. The full suite is rerun after that correction.

## Final verification

- Release solution build: zero warnings, zero errors. Full .NET suite: 332 passed, zero failures. All eight VS Code bridge tests passed. Formatting verification, strict OpenSpec validation and `git diff --check` passed.
- Final isolated package: `artifacts/hook-ingress/final-releases/bell-tower-0.1.8-win-x64.zip`, SHA-256 `7e99a482e24a59ffb004165e858925171da77406bbddaaae84f6bb0cdbdce067`. Native helper: 2,990,592 bytes. Build/package and neutral protocol probe passed. This is a development fixture package at the repository's existing version, not a published release.
- Final native benchmark, run alongside the packaged lifecycle trial: `artifacts/hook-ingress/trial-final/results.json`. At concurrency eight, 121 native samples per provider produced 242 durable captures and zero failures or three-second overruns. Claude median/p95/p99/max: 122.77/136.62/152.65/153.55 ms; Codex: 89.63/107.22/112.78/113.89 ms. Legacy control medians: 1618.55 / 1581.27 ms. All 50 legacy controls persisted, eight conversations remained independent, and crash replay passed.
- Final rebuilt package lifecycle: `artifacts/update-acceptance/run-d02076a966e947dc98ad3a97f4d3f65b`, passed mixed capture during update/refresh, stopped-tray ingestion, compatible rollback, immutable image retention, state/ownership preservation and disposable provider/runtime survival.
- Actual packaged doctor against the isolated native captures: Claude reported two timeouts with only time bounds and scan limits; Codex terminal history reported the verified unavailable-count status, null count and common fallback.
- User-approved compiler/SDK prerequisites installed under D:/Tools, with cache/temp/shared SDK paths on D:. Final observed free space: C: 3.66 GiB, D: 71.40 GiB.

## Remaining live release acceptance

No production setup, transcript mutation, authenticated model request, physical sound test or existing provider termination was performed. The separate release-field acceptance change retains the real provider/host matrix. Neither synthetic hook inputs nor the local fake-model clients are described as authenticated acceptance.
