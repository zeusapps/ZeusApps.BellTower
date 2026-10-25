# QA006 preparation (not executed)

This directory contains QA-only reusable fixture and runner preparation for the next stable implementation handoff. No script here has been run against the changing source. Runtime work must wait for the orchestrator's explicit stable-target go-ahead and use only fresh descendants of `artifacts/qa-006/`.

## Prepared components

- `fixture.ps1`: copies a supplied baseline installation/data tree into a unique QA006 disposable root; supports a self-hosted delayed-start apphost only when explicitly requested.
- `prepare-hooks.ps1`: creates fake Claude/Codex homes and records legacy and optional NativeAOT generated hook commands. `-LegacyOnly0_1_8` is a distinct capture-0 path and refuses capture-1 setup; capture-compatible source fixtures use the default path.
- `prepare-published-018.ps1`: prepares a fresh capture-0 fixture from the actual published 0.1.8 archive after verifying the SHA sidecar, release descriptor and every extracted payload file. The read-only archive inspection during preparation found matching SHA-256 `d23702de5d05c1eed5a4575d0d11b0d45267ec3aa01360b0d582ad19c6a36b67`. It is a separate fixture and must not receive capture-1 provisioning.
- `hook-trial.ps1`: copied runner with QA006-only result paths. Its stopwatch starts before process launch/input and result evaluation checks each child's own three-second budget, including the launch and write interval. `LegacyClaude`, `LegacyCodex`, `LegacyMixed`, and `NativeMixed` remain separate cases.
- `FileBarrier.cs`: small compiled callback for the native Cleanup/Retire checkpoint. It blocks on entered/release files without invoking a PowerShell runspace callback from the worker thread.

## Intended post-freeze run sequence

1. Record the exact stable identity. Build/package once, then rebuild the final Release assembly after packaging side effects and run Release suite, scoped format and Node suite.
2. Create two distinct disposable baselines: (a) released 0.1.8, capture 0, legacy hooks only; (b) current-source capture-compatible install/data for NativeAOT capture-1 and mixed trials. Never run capture-1 and then use that state as the capture-0 rollback baseline.
3. On fresh isolated capture-1 fixtures run the clean native Switch -> Rollback -> explicit Retire lifecycle and current-package benchmark. Keep Preflight read-only and record all before/after selection, launcher, payload and tray identities.
4. For each of Switch, Rollback, Cleanup and Retire, wait for the phase's explicit entered marker. While held, run in order: individual Claude legacy, individual Codex legacy, simultaneous Claude+Codex legacy pair, simultaneous NativeAOT Claude+Codex pair. Measure each process from launch through stdin write, exit and durable response against 3000 ms; record per-provider result and durable inbox/capture deltas. Keep each phase blocked for at least 3.5 seconds, confirm the operation has not passed its barrier, then release in `finally` and join/reap only QA-owned workers and shell trees.
5. While testing Cleanup/Retire, exercise selected, previous, recovery-retained and actively leased payloads. Verify hooks remain usable while the lease prevents removal, then release only the QA-owned lease and finish the explicitly authorized disposable operation.
6. Run bounded negative cases from the approved fix plan: Rollback drift while each of setup/state/presentation locks is contended; post-acquisition guard; pre/post monitoring-state barrier and retry; exact marker byte/semantic/unknown-field/missing partner/live-or-unknown owner/expiry/PID reuse; process argv rooted and relative/unknown cases; delayed old-order child; crash publication order and nested recovery retry; interrupted candidate-start receipt and mismatched activation/version/PID/start/data/lineage; actual stale tray presence. Preserve hashes and prove foreign processes/records remain untouched.
7. Inject one hook assertion failure and one timeout into owned QA workers. Teardown must release file barriers, join workers, reap owned descendants, release leases and preserve the primary error plus cleanup errors.

## Current limitations

The phase driver and negative probes still need adapting to the exact stable-006 source interfaces. The known installer barriers are `switch-before-selection` and `rollback-before-selection`; Cleanup/Retire use `UpdateTransaction.PayloadRemovalTestCheckpoint`. Do not interpret this prepared matrix or its scripts as acceptance evidence. Capture counts, wall-clock timings, cleanup, and all runtime checks remain not performed until go-ahead.
