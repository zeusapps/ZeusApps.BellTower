# Design

## Context

See proposal.md (Why) for the field case. These are the relevant facts about `scripts/install-dev.ps1`:

- The tray writes its presence record (`updates/tray.json`) only after its first successful refresh. `Read-Owner` treats a tray-instance object without verifiable presence as an unknown owner and refuses.
- Every Switch and Rollback publishes an owned coordination record pair: the pending record and `updates/restart.json`, carrying the operation's ID and token. After the selection and monitoring steps, it releases the reservation and the launch gate, then calls `Start-SelectedTray`, which waits up to the deadline for a presence record of the selected version with the operation's activation. Only after that does it remove its own records.
- When the wait fails, the records are kept on purpose (a tray may be running unverified). `EnsureNoCoordinator` then refuses every new operation, and a Rollback retry on the `original-retry` route repeats the same wait.
- The disposable-only `-NoTray` switch already shows that the end-of-operation path is safe without a tray. It skips `Start-SelectedTray` and removes the owned records. It is rejected for the real installation, and it covers only operations started that way.

## Goals / Non-Goals

**Goals:**
- One safe, explicit exit from the "restored but never confirmed" rollback state, for the real installation.
- Messages that let a maintainer tell a stuck tray from a tray that is merely slow or owned by something else.

**Non-Goals:**
- Making the script stop a tray that has no presence. Its identity is not verifiable by the existing rules, and the maintainer can quit it from the tray menu.
- Finalizing a Switch. Rollback stays its recovery.
- Changing the product update coordinator, or when the tray writes presence.

## Decisions

1. **A separate `Finalize` action instead of a Rollback flag.** A flag such as `-SkipTrayConfirmation` on Rollback would mix the normal path with an exception and make it easy to skip readiness when it is not needed. A separate action has one precondition set and one effect, and its name appears in the readiness-failure message. Alternative rejected: enabling `-NoTray` for real installations. That switch is a test hook, applies to the whole operation and would skip the tray restart even on successful runs.

2. **Preconditions, checked in this order. Any failure means nothing is changed.**
   1. The manifest is a Rollback whose state is `restored-monitoring-snapshot` or `running-original`, and the current selection hash equals `originalSelectionSha256`. Any other state or selection refuses: for a Switch or an incomplete step it names Rollback with the same manifest, and otherwise it names the exact mismatch.
   2. Both coordination records exist and parse, and their ID and token equal the manifest's `coordinationId` and `coordinationToken` (the existing `Assert-OwnedRecordPair` contract). Foreign or product-update records refuse with the release-documentation pointer.
   3. The script takes the tray-instance reservation and the launch gate exactly as Switch does before mutating anything. Taking the reservation fails while any tray holds the tray-instance object. Under the reservation, `Wait-InstallationTrayStartsDrained` (short deadline) confirms that no installation tray process is starting or unclassifiable. A running tray refuses with its PID and the "quit it from the tray menu" instruction.
   4. The original payload bytes still match (`Assert-OriginalBytes`).

3. **Effect.** Under the reservation and gate: write the manifest with `state = finalized-without-tray`, `finalizedAt` and `finalizeReason` (from the stored not-ready facts, or `operator-finalize` when none were stored). Then call `Remove-OwnedCoordinationRecords`, then release the gate and reservation. The manifest is written first, so an interruption between the two steps leaves a manifest that a Finalize retry recognizes (state `finalized-without-tray` with the owned records still present → remove them). Nothing else is written. The JSON result states `trayStarted: false` and tells the maintainer to start the tray or switch to a build that recovers the state.

4. **Not-ready diagnostics through one helper** (`Describe-NotReadyTray`), used by the `Start-SelectedTray` deadline failure and by the `Read-Owner` unknown-owner refusals. It reads only:
   - whether the operation-owned launcher and any installation tray process are alive (existing `ProcessProbe` and scan);
   - presence absent or ended;
   - the `state.json` length against the snapshot budget constant the product uses;
   - the count and time span of `tray-refresh-failed` lines in the last bounded tail of `bell-tower.log` (for example 64 KiB). It quotes only the code and the optional sanitized reason that newer trays write, never raw paths.

   The readiness failure stores the same facts in the manifest as `trayNotReady`, so Finalize can record them as its reason. Neither path reads or writes any other data.

5. **Parity.** The script is the single implementation for both clients. Both skill copies get the same new section, and the existing skill-copy sync test keeps them identical. Finalize is provider-neutral, and none of its inputs depend on which client runs it.

## Risks / Trade-offs

- [A maintainer runs Finalize while the restored tray is merely slow] → Finalize requires that no tray is running or starting, so a slow tray that is still alive is refused. After Finalize, the selection is the original one the rollback already committed, so a tray started later simply runs on it.
- [Finalize leaves no tray running] → Stated in the result and in the skill. The maintainer runs `bell-tower start`, or the next Switch to a build that recovers the state starts it. That Switch records `originalTray = null`, so it does not restart a tray; the skill tells the maintainer to start the tray after such a Switch.
- [Log tail parsing is heuristic] → Diagnostics only. No decision depends on them.

## Migration Plan

No data migration. For the maintainer's current installation, after the change is implemented and reviewed:

1. Quit the stuck tray from its menu.
2. Run `Finalize` with the existing manifest `artifacts/dev-builds/operations/bell-tower-dev-b338eecdeb964fccb06b27d9b316fa1e/operation.json`.
3. Run Preflight and Switch for a new dev build that includes `bound-ended-session-state`.
4. Start the tray and confirm the state recovers (task 3.3 of `bound-ended-session-state`).

If Finalize misbehaves, the records it removed are documented coordination files. A rollback with the manifest does not apply after finalization. Recovery then follows `docs/releases.md` for a known-good `current.json`, which Finalize never changes.
