# Hook timeout investigation at Codex handoff

Date: 2026-10-24 UTC. Evidence here is source inspection and historical report review, not a reproduction on the user's work machine.

## Reported symptom

The user sees a hook reported as timed out, without a retained error message. The work machine was believed to use released 0.1.8. The user recalls the NativeAOT work and also raises the Claude-built 0.1.10 trial as a possible contributor. Those observations do not select a single failure mechanism.

## Mechanisms distinguished

1. **Legacy startup cost.** [The earlier field report](../../../../docs/handoffs/claude-field-reports-0.1.8.md), "Slow startup loses hooks", records reported full-chain median 5.0 seconds and maximum 6.5 seconds with a three-second deadline. The chain included provider shell, encoded PowerShell, stable launcher and desktop payload. These are historical field figures, not fresh measurements.
2. **Long launcher-gate ownership.** At takeover, `scripts/install-dev.ps1` acquires the launch gate before verified tray stop and retains it through snapshot, payload copy and file verification. `UpdateTransaction.Cleanup` and `Retire` acquire it before recovery-plan enumeration, hashing and deletion. `InstallationGate` has a 10,000-ms default acquisition deadline. `ProviderSettingsEditor` emits `timeout: 3` for both clients. A slow gate owner can therefore outlast a hook's budget, independently of managed startup cost. This is a source-proven mechanism; its incidence on the work machine remains unverified.
3. **Native capture bypass.** `HookCommandBuilder` emits direct immutable ingress commands for fast capture; `IngressProvision` verifies payload inventory bytes and copies them into a content-addressed data directory. `docs/hook-ingress.md` records the previous packaged trials and the existing benchmark. Fast capture bypasses launcher selection and its gate, does not wait for the desktop consumer, and does not establish successful delivery solely by running asynchronously.

## Safety and ownership

`BellTower.Launcher/Program.cs` already resolves selected version, obtains a shared payload lease and starts the child inside one launch critical section. The fix keeps that launcher unchanged. Payload removal still needs an exclusive lease and selected/previous/recovery retention. Move slow work out of the launch critical section while retaining update-operation serialization; do not remove synchronization or increase hook timeouts.

Changing selection alone does not install fast hook settings. The current change tests provisioning with disposable provider homes and cannot claim that existing work-machine hooks migrated. A real integration preview/apply or field timing check remains a separate operation.

## Current documentation lookup

Per repository instructions, Context7 resolved `.NET` to `/dotnet/docs` and fetched named mutex documentation. [Official mutex documentation](https://github.com/dotnet/docs/blob/main/docs/standard/threading/mutexes.md) confirms acquisition blocks until availability or timeout and release must run on the owning thread. No new platform API is needed.

## Required evidence

Task 4.3: baseline blocking-mechanism reproduction; fixed slow-phase barriers with successful concurrent Claude, Codex and mixed legacy hooks before release; payload lease retention; pending-update refusal; preserved owner, drift, original-bytes and rollback checks.

Task 6.3: existing final-package benchmark, generated real provider shells, neutral response and all durable counts, per-provider latency distribution and current thresholds. Include native mixed captures while maintenance is blocked. Label generated-shell, native executable and authenticated field evidence separately. Final evidence belongs to the developer handoff and QA 003 report; no experimental pass is asserted here.
