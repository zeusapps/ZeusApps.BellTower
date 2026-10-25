# Bounded startup-fence assessment for ANALYSIS-150

Read-only follow-up requested by the orchestrator, 2026-10-24 UTC. No implementation edits and no overwrite of Analysis 003. Assessed target: `head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768`.

## Decision

A temporary reference to the named `tray-instance` object, owned restart/pending identities and a bounded drain of verified tray-start processes can cover the concrete old-payload delayed-start race without changing old payload bytes, blocking hook launch or killing processes. A background heartbeat framework is not necessary if renewal, drain and freshness checks occur at the mutation/release boundaries below. This is a design assessment, not runtime verification or approval of a future implementation.

## Required ordering and proof

1. Take the existing installation update gate and refuse unrelated pending/restart records. Establish the operation's marker ownership without overwriting unrelated bytes. Retain a non-owning reference to the existing named tray-instance object **before** stopping the recorded tray; create the object if none exists. Keep that reference across all slow work. The reservation is object lifetime, not mutex ownership: `initiallyOwned:false`, dispose only.
2. While this reservation exists, `TrayController.IsRunning` intentionally sees an object even when no tray process remains. Therefore verified tray stop must wait on the exact recorded PID/creation identity and ownership evidence, not object disappearance. Do not mistake reservation existence for an unexpected live tray or wait for the reservation itself to disappear.
3. Publish/renew only the operation's exact pending/restart identity. Drain all potentially in-flight installation **tray invocations** before shared monitoring state or selection mutation. Classify by verified executable installation, actual parsed command and data directory, PID and creation identity. Do not include agent processes or ordinary event/session helpers. Unknown or mismatched evidence causes conservative refusal before mutation; nothing is killed. A marker initially published by non-atomic CreateNew writes is not a safe cutoff until its complete contents are readable; classification must not falsely exclude children that could have passed the check during publication.
4. A child paused after old `CanStartTray` succeeds remains a live process. When resumed while the reservation exists, the old mutex constructor sees `createdNew:false` and returns without becoming the tray. Wait for that child to exit before releasing the reservation. If it stays paused or cannot be classified safely within the bounded wait, refuse while keeping the original selection/state and unrelated owner intact.
5. Slow copy/hash work can outlast a marker's ten-minute validity without opening tray ownership while the reservation is retained. Before any first shared-state/pointer mutation, and again before reservation release, renew the owned records and perform a final drain of **all** relevant in-flight tray starts, including children created during any expired-marker interval. Do not limit the drain to processes older than the initial plan timestamp. An expired or replaced marker, unknown startup identity or failed drain must not be ignored.
6. Release update serialization before waiting for the authorized tray, because its cleanup uses that gate; matching pending records continue to exclude other coordinators. Keep a fresh owned restart marker across reservation release and the bounded authorized restart. Release the object reference only after the final drain. Launch through the stable launcher with the matching activation identity, then verify exact process lineage/presence/version/data root/activation before clearing only owned records. Renewal immediately before the final drain/release leaves ample lifetime for the existing 30-second startup wait; do not proceed with an almost-expired marker.
7. Preserve ownership/recovery records on post-selection failures so the same operation can recover safely. Before-selection refusal must not delete someone else's marker or signal an unexpected tray. Successful cleanup deletes only records whose owned identity still matches. Do not restore the long launcher-gate hold or increase hook deadlines.

This proof is for normal supported launch scheduling plus deliberately paused startup tests. It does not assume marker presence can retroactively revoke a successful check. Reservation and process drain supply that missing boundary. No arbitrary user process is terminated, and uncertainty is a refused operation rather than a provider/platform exception.

## Focused checks needed for the future implementation

- Paused old-order child: successful `CanStartTray`, then pause before named-mutex construction; start the operation; release child; assert it exits under reservation before any pointer/state mutation.
- Unknown/paused startup that does not drain: refuse safely, leave marker bytes and selection/state as specified, never kill that process.
- New normal `start` requests during reserved Switch/Rollback: no new monitoring owner; legacy and native Claude/Codex/mixed hooks still complete and remain durable.
- Expiry injection while reserved: renew and drain children from the expired interval before mutation/release, or refuse safely.
- Authorized restart on unchanged old 0.1.8 and current dev payloads: matching activation may start after reservation release; unrelated markers remain byte-identical; operation records permit bounded recovery.

## Current primary documentation

Context7 CLI resolved `.NET`, then queried the official Learn index. [Mutex constructor documentation for .NET 10](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex.-ctor?view=net-10.0) states that `createdNew` is false when the named system object already exists; named-object lifetime extends while representative mutex handles remain. Requesting initial ownership only grants it for a newly created named object. These semantics support a non-owning reservation; they do not prove the process inventory or future implementation correct.

Reviewer: code-analyzer / Codex CLI 0.164.0 delegated subagent / `gpt-6.1-sol`. Checks: source-order reasoning and primary constructor documentation; no code/runtime execution, provider setup or personal-state changes.
