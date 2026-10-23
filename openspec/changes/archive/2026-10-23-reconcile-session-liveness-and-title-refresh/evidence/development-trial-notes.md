# Unpublished local development trial

This is a user-authorized local trial, not a published release or completion of later queued features. Source version remains 0.1.8. The main session verified the remote tags/releases contained only v0.1.8 and authorized an unpublished compiled/package version 0.1.9 to route existing stable-launcher calls safely. No production install/data/provider/settings action was performed by the developer.

## Source and package identity

Before packaging, all 58 reviewed file hashes and the round-002 diff identity `93e78e0726311a6f3551e6a8f6ce4d8a732a71df469df9ad66274f9739f43eb9` matched. The unchanged reviewed source was first packaged separately under `artifacts/development-trial/trial-1792777572388-w25ah4/before-fix/`. Its real packaged payload failed startup against a migrated health-v1 snapshot, despite advertising health capability 1. Retained reproduction: `artifacts/development-trial/trial-1792777572388-w25ah4/startup-reproduction.json`.

**QA-200 correction:** `Program.Main` now passes its actual health capability to `EnsureTrayInvocation`, matching its distribution probe and the stable launcher. Two real-executable Claude/Codex regression cases verify initial startup and restart with committed health-v1 state while preserving episode, wait and palette. The prior package and reviewer evidence remain unchanged.

Candidate archive:

```text
artifacts/update-acceptance/development-trial/trial-1792777572388-w25ah4/bell-tower-0.1.9-win-x64.zip
SHA-256 186bfbd47ad41659bc221f32a1b5c98f2ff0b64cc54a3f0e2ad73d31a6bdceaa
```

Descriptor:

```text
artifacts/update-acceptance/development-trial/trial-1792777572388-w25ah4/bell-tower-0.1.9-win-x64.release.json
SHA-256 4d5c5c920712daa370d50bc660bd4cef60c58979c8346804cbdda70ecedd2dd5
```

All 22 declared payload inventory entries matched their SHA-256 values. Probes report version 0.1.9, attachmentHealth 1, capture 1 and NativeAOT protocol 1. Packaging used normal locked restore and NuGet auditing; neither auditing nor native compilation was bypassed. Package command:

```powershell
./scripts/package.ps1 -FixtureVersion 0.1.9 -OutputDirectory artifacts/update-acceptance/development-trial/trial-1792777572388-w25ah4
```

The existing fixture-version option confines these generated artifacts to its permitted root. No new product/version option, source-version reservation, tag, feed or publication was added. This package is hash-verified local output, not publisher-signed output; signing remains separate queued work.

## Executed checks

- Final Release solution build: zero warnings/errors. Formatting verification, strict OpenSpec validation and `git diff --check`: passed. The unchanged eight Node cases retain their earlier pass; no Node implementation changed.
- Full .NET final rerun: **371 passed, zero failed/skipped, 31 seconds**. This includes the eight round-002 regressions and two QA-200 startup/restart cases.
- A prior full run passed 370 and failed one existing selection replay case with an intermittent filesystem error. An isolated matched-provider rerun passed 2/2; the subsequent full run passed 371/371 without changing IdentityStore, AtomicFile or that test. The underlying lock/contention cause remains unproven; it was not dismissed by weakening an assertion or implementation.

Retained failure identification and stack:

```text
CaptureTests.SelectionReplayPreservesNewerIntentAndManualIdentityInEitherProvider(provider: "claude")
System.IO.IOException: Unable to remove the file to be replaced.
System.IO.FileSystem.ReplaceFile(...)
BellTower.IO.AtomicFile.Write(...) at AtomicFile.cs:22
BellTower.IO.IdentityStore.SaveCore(...) at IdentityStore.cs:82
BellTower.IO.IdentityStore.Update(...) at IdentityStore.cs:70
BellTower.Providers.OpenSpecSelectionObserver.Apply(...) at OpenSpecSelectionObserver.cs:53
BellTower.Providers.OpenSpecSelectionObserver.Observe(...) at OpenSpecSelectionObserver.cs:31
CaptureTests.SelectionReplayPreservesNewerIntentAndManualIdentityInEitherProvider(...) at CaptureTests.cs:304
```

Packaged mixed-provider smoke passed against fresh isolated data: two legacy normalized conversations plus two actual packaged capture-v1 invocations; normal stable-launcher tray startup; committed health-v1 restart; unchanged identities/episodes/palettes; a pre-existing private host surviving both starts; refusal of a declared incapable consumer with no migrated-state mutation. All playback used silent sinks/muted isolated configuration. That first refusal fixture is explicitly a declared-capability fixture, not a historical release.

Actual historical baseline rehearsals subsequently used the downloaded published v0.1.8 archive, SHA-256 `d23702de5d05c1eed5a4575d0d11b0d45267ec3aa01360b0d582ad19c6a36b67`. Both normal switch/rollback and injected post-health-commit readiness failure/recovery passed. They verified original launcher/payload bytes, private host survival, trial monitoring snapshots retained aside, health-0 restoration before the actual old tray restarted, current manual/config overrides and a runtime-token canary retained, and a real legacy event queued while monitoring was stopped delivered after rollback. Only fixture-owned processes were created; the operational script never kills a provider/runtime or modifies its token/record.

## Reproduction and dependencies

Normal generated outputs and disposable harnesses remain under the unique ignored root `artifacts/development-trial/trial-1792777572388-w25ah4/`:

| File | Purpose / result |
| --- | --- |
| `source-before.json`, `provenance.json` | Exact initial reviewed source identity |
| `candidate-identity.json`, `final-provenance.json` | Candidate/descriptor/inventory/API/harness identities |
| `smoke.ps1`, `candidate-smoke/results.json` | Actual packaged mixed ingress and migrated restart; passed |
| `operational-rehearsal.ps1`, `queue-during-rollback.ps1` | Actual published baseline, normal and failed-readiness recovery with queued events |
| `operational-rehearsal-queues-results.json` | Final normal/failure cases both passed |

Commands (run in PowerShell 7):

```powershell
& artifacts/development-trial/trial-1792777572388-w25ah4/smoke.ps1 -InstallationRoot artifacts/update-acceptance/development-trial/trial-1792777572388-w25ah4/bell-tower-0.1.9-win-x64 -TrialRoot <new-disposable-root>
& artifacts/development-trial/trial-1792777572388-w25ah4/operational-rehearsal.ps1 -RunTag <new-unique-tag>
```

The maintained [operational script](development-trial-operations.ps1) loads these reviewed development-build API assemblies; their frozen SHA-256 values are:

| Path | SHA-256 |
| --- | --- |
| `src/BellTower/bin/Release/net10.0-windows/bell-tower.dll` | `084f7ac75bced6ca00bd6c287c0decd798c908d4dd86058b8b61fdc7f1ed74dd` |
| `src/BellTower/bin/Release/net10.0-windows/BellTower.Distribution.dll` | `58795098d3ffe9519dace189f94217a0c1c6d6e5ff1fc792c01af722bc172498` |
| `src/BellTower/bin/Release/net10.0-windows/BellTower.Protocol.dll` | `ae796b742e79def6dadb7587fe58b38e2d2f8fd699a666775ec7eab9ef5e27db` |

The full API/harness hashes are retained in `final-provenance.json`. Actual execution used PowerShell 7.6.6 and .NET 10.0.12. Context7 resolved PowerShell to `/microsoftdocs/powershell-docs`; the three operation-specific requests covered [ACL inheritance](https://github.com/MicrosoftDocs/PowerShell-Docs/blob/main/reference/7.7/Microsoft.PowerShell.Security/Set-Acl.md), [recursive literal copying](https://github.com/MicrosoftDocs/PowerShell-Docs/blob/main/reference/docs-conceptual/samples/Manipulating-Items-Directly.md) and [bounded process waits](https://github.com/MicrosoftDocs/PowerShell-Docs/blob/main/reference/7.7/Microsoft.PowerShell.Management/Wait-Process.md). The index returned 7.7 docs; successful local execution, rather than an assumed installed version, supplies the 7.6.6 evidence. The script uses existing Bell Tower verification, atomic persistence and installation/data gates.

## Reviewed switch and recovery recipe

Only the orchestrator may execute production operations after current QA/code analysis. Default `Preflight` creates a private preparation workspace and extracts/verifies the candidate there; it does not stop a tray or change installation/data. Parameters are explicit: installation root, data directory, archive, pinned archive SHA-256, protected backup workspace and the resulting operation manifest. This is a one-off trial procedure, not a new updater surface. Current `update` CLI has no local `--archive`/`--file` option; full `UpdateTransaction.Apply` also refreshes integrations, so it is intentionally not used for this minimal tray trial.

1. Verify all three API DLL hashes above, candidate/descriptor hashes, script identity and the chosen literal paths. The backup workspace must already permit only the current user, SYSTEM and/or Administrators. Preflight enforces that ACL, unchanged original selection, absent fast capture and a new candidate-version directory. It records every original payload file and launcher hash, the original tray PID/creation/executable/data-root identity and candidate probes. The main's earlier read-only production selection hash was `a53e6fb9a8dc1ae58bc21b17378aa93dd52caba621563fbfabdfb3ccd986fd07`; preflight must reconfirm actual bytes.
2. `Switch` conditionally rechecks preflight. Under the installation gate, request exit only from that verified tray; after it exits, acquire setup/state/presentation locks, privately snapshot primary/backup monitoring state and exact selection, record/check hashes, copy and reverify only the new versioned payload, persist the planned selection identity, and atomically select it. Release the installation gate before invoking the original stable launcher. Existing loaded modules therefore route to the selected local candidate. No hook, shell module, profile, startup entry, VSIX, original payload or stable launcher is rewritten; normal voice/mute preferences remain current and no sound test is requested.
3. Readiness requires correct data-root/version/executable ownership. Candidate launcher PID/creation is persisted before waiting; candidate ancestry is checked so another operator's tray is not adopted. Partial original-stopped/ready-to-select/selected-but-not-ready phases remain explicitly recoverable. Test-only readiness failure is restricted to disposable `artifacts/` data.
4. `Rollback` refuses a changed selection, changed original bytes, different monitoring owner or newly enabled/pending capture-v1. It requests exit only from our proven candidate, archives current trial monitoring files aside, verifies the private pretrial snapshot, and restores only `state.json`, `state.json.bak` and exact original `current.json` under locks. If the old backup was absent, the old primary supplies both files. Hash and health-0 checks complete before the old stable-launcher entry is started. If selection never changed and health remains 0, recovery can restart the original monitor without replacing current state. Both original and candidate payloads remain retained; no version cleanup occurs.

The fallback is explicitly point-in-time monitoring state: trial-processed waits/episodes are retained in the archived trial snapshot, not merged into an older reducer. Current identities, explicit overrides, configuration, runtime tokens/records and all event queues remain in place. Newly queued legacy events can be consumed after recovery, as rehearsed. No old monitor may be started independently on health-v1 state. No concurrent setup/version operation should be initiated during the controlled switch; setup is serialized during snapshot/restore and selection is conditional. The script fails closed rather than signalling a provider, guessing another tray owner or silently rolling state backward after an ownership conflict.

Production activation, real provider/user-terminal observations, optional sound and subsequent release publication are not claimed by these isolated checks. The user authorized the local trial; independent review of the corrected source and operational script is still required before the orchestrator acts.
