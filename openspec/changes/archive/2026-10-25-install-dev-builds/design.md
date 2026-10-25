# Design

## Context

- A versioned installation (`%LOCALAPPDATA%\Programs\ZeusApps.BellTower`) has:
  - a stable launcher `bell-tower.exe`, which here is the 0.1.8-era build;
  - the selection file `current.json` (`version`, `previous`, `activation`);
  - payloads under `versions/<MAJOR.MINOR.PATCH>/`, each with a `release.json` contract and a file inventory.
- The launcher and every payload validate versions with the strict `^\d+\.\d+\.\d+$` rule (`ReleaseContract.Parse`) and use the version string as the payload folder name. A changed version syntax would break installed launchers, which this change must not replace.
- `DistributionJson` ignores unknown JSON members, so older launchers and payloads tolerate new optional `release.json` fields.
- `UpdateTransaction.Stage` refuses an offer that is not newer than the selected version, and refuses an offer whose payload folder already exists.
- The archived `development-trial-operations.ps1` (reconcile-session-liveness change):
  - hard-codes the original and candidate versions, attachment-health consumer level 0, and its own archive path;
  - loads Bell Tower assemblies from the repository Release output;
  - otherwise has the right safety model: private ACL-checked workspace, verified tray owner, lease-aware selection change, snapshot and rollback, original-bytes assertions.
- The 2026-10-24 install of dev 0.1.10 used an adapted copy of it and succeeded.
- MSVC Build Tools live in `D:\Tools\BellTowerBuildTools` and the Windows SDK in `D:\Windows Kits\10`. ILCompiler calls `vswhere.exe`, which is not on PATH, so the ingress link failed until the Visual Studio Installer folder was added to PATH by hand.

## Goals / Non-Goals

**Goals:**
- One supported, repeatable dev-install path with rollback.
- Unmistakable dev identity on every version surface.
- Stable updates keep working after any number of dev installs.
- Identical maintainer tooling for Claude Code and Codex.

**Non-Goals:**
- Publishing dev builds, or a dev update feed in the tray.
- Changing the launcher or bootstrap contract.
- Automatic integration setup after a switch.
- Remote or CI installs.

## Decisions

### 1. Reserved PATCH range ≥ 9000 instead of a prerelease suffix

Dev versions are `MAJOR.MINOR.(9000+n)`, built from the base version in `Directory.Build.props`. For example, base `0.1.8` gives `0.1.9001`, `0.1.9002` and so on.

- **Rejected: semver prerelease (`0.1.9-dev.1`).** Installed launchers and payloads fail strict parsing, and the folder name equals the version.
- **Rejected: a separate dev installation root.** It would not exercise the real installation, hooks and data that the maintainer wants to test.
- **Rejected: plain next patch (today's 0.1.10).** It collides with future stable folders and blocks updates.

The range needs no launcher change, sorts above any realistic stable patch, and is easy to recognize. A shared helper (`ReleaseContract.IsDevVersion`) defines the range. `package.ps1` enforces it in both directions.

### 2. Dev marker as optional `release.json` build metadata plus informational version

- **`release.json`** gains an optional `build` object: `{ channel: "dev", commit, dirty, builtAt, baseVersion }`.
- **Assemblies** get `InformationalVersion` = `<version>-dev+<commit>[.dirty]` through `-p:InformationalVersion`, so surfaces that read only the executable also see the marker.
- **Version surfaces** resolve dev status from the selected payload contract, falling back to the informational version. They then format one shared display string, `0.1.9001 dev b74a12b (2026-10-24 21:30)`, in a single helper used by the tray tooltip and About/Sessions view, `doctor`, `update --status` and the CLI version output.
- **Range without a marker:** a payload in the dev range with no `build` object is still shown as dev, labelled "dev (unmarked)".
- **Rejected: a separate marker file.** It would need inventory changes and is easier to lose.

### 3. Base-version comparison while a dev build is selected

`ReleaseCheck`/`UpdateDiscovery` and `UpdateTransaction.Stage` use a shared "effective installed version" function:
- For a stable selection, it returns the selected version.
- For a dev selection, it returns the dev build's `build.baseVersion`. For an unmarked dev-range build, it uses the same MAJOR.MINOR with PATCH 0.

Stable offers newer than the effective version are offered and staged. The folder-exists check is unchanged, because stable numbers never fall in the dev range.

The `previous` field of `current.json` may name a dev version. Rollback after a stable update therefore still works.

### 4. `scripts/install-dev.ps1` replaces the archived trial script

- **Generalization:**
  - parameters `-Action Preflight|Switch|Rollback|Retire`, `-Package <zip>` (default: newest dev package under `artifacts/dev-builds/`), `-InstallationRoot` and `-DataDirectory` (defaults: the standard paths), and `-Workspace` (default: a new ACL-restricted folder under `artifacts/dev-builds/operations/`);
  - the original version is read from the selection, and the candidate from the package descriptor;
  - the attachment-health and capture compatibility checks call the existing `AttachmentHealthCompatibility` and `CaptureCompatibility` APIs, using the candidate contract's levels instead of a constant;
  - the script refuses a candidate that is not in the dev range or has no dev marker.
- **Kept behavior:**
  - the private workspace ACL check, verified tray owner, `InstallationGate` plus data locks, the selection and `state.json` snapshot, and original launcher and payload byte assertions;
  - the tray is started only through the stable launcher, and the operation manifest records state transitions.
- **Rollback scope:** rollback restores only `current.json`, `state.json` and `state.json.bak`, as before. Identity and other app data use forward-compatible writers and are left alone.
- **Fast capture:** the archived recipe refused fast capture because its 0.1.8 original could not read captures. The new script allows a switch or rollback when the target contract's `capture` level is at least the data's level.
- **Retire (amended 2026-10-24 after QA-001 and ANALYSIS-001: `-Legacy` dropped; one shared C# retention predicate with `UpdateTransaction.Cleanup`; dev marker required):**
  - Removes only a payload that is not the selected version, not `previous`, and not leased (`Installation.Lease(version, exclusive: true)` succeeds).
  - Requires the dev marker. Unmarked payloads (stable, or the earlier unmarked 0.1.9 and 0.1.10 dev builds) are always refused; the tray's automatic cleanup reclaims them once they are neither selected nor retained.
  - Never removes a stable payload.
- **Location:** the script ships in `scripts/` (repository tooling, not in the payload). It loads assemblies from the repository Release build, as before, after checking that the build is fresh relative to the package.

### 5. Packaging changes

- `package.ps1 -Dev` builds into `artifacts/dev-builds/` and computes the next free dev number. Free means above the highest dev number in that folder and in the default installation's `versions/`. The script also accepts an explicit `-DevVersion` and validates it.
- The script resolves `vswhere.exe` from `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` and prepends that folder to PATH for the child publish processes only.
- If `vswhere.exe` is missing, or reports no instance with the C++ x64 tools, the script fails with a message naming the Build Tools workload and the Windows SDK.
- Stable mode rejects the dev range. Fixture mode (`-FixtureVersion`) stays unchanged for update acceptance.

### 6. One skill, two client copies, enforced equal

- **Location:** `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md`, byte-identical. A test in `tests/BellTower.Tests/Scripts` compares them, in the same style as existing repository-tooling checks.
- **Content:**
  - prerequisites;
  - commands for build, preflight, switch and verification (`update --status`, `doctor`, tray tooltip);
  - rollback, retire, and the optional integration setup preview;
  - authorization and safety rules: explicit user authorization to install, never stop agents, `setup --apply` only on request, and report where the operation manifest is.
- **Delegation:** the skill is operations guidance and does not bypass the repository's delegated workflow. Building and installing are maintainer operations; code changes still go through OpenSpec delegation.

### 7. Keep slow maintenance outside the hook launch path (2026-10-24 user addition)

Two timeout mechanisms need distinct evidence. The prior 0.1.8 field report records a slow shell/PowerShell/desktop startup chain; the existing NativeAOT ingress bypasses that chain. Separately, the trial installer and automatic cleanup hold the launcher gate across slow work. The user's reported timeout has not been reproduced on the work machine; do not label the gate as its proven cause.

- Keep the installed stable launcher unchanged. Its selection resolution, shared payload lease and child start are already protected by a short launch critical section.
- Serialize Switch/Rollback and cleanup/Retire with the installation update gate, using the updater's pending-operation policy. Refuse active or interrupted pending updates before tray-stop or selection mutation, retaining recovery records.
- Copy and verify candidates, wait for verified tray shutdown and inspect original bytes outside the launch gate. Take the launch gate only for short final selection verification and atomic pointer replacement. Keep data locks limited to snapshot/restore work; never hold them over payload copying/hashing or tray waits.
- Coordinate tray startup with the existing updater restart-marker/activation handshake while the launch gate is free. Released 0.1.8 already honors `RestartPending`/`CanStartTray`. Prevent both new launches and in-flight children from acquiring unexpected monitoring ownership during slow phases, recheck ownership before pointer mutation, and only clear operation-owned markers. A final owner check alone does not cover delayed child startup. Refuse unrelated active/interrupted coordination and preserve recoverability on failure; do not overwrite another operation's marker or broaden the launcher critical section.
- Fence old-order startup by keeping a handle to the named tray-instance object before stopping the recorded tray; the released tray exits when creation reports the object already exists. Verify actual PID/start-time exit rather than treating object existence as a running tray during reservation. Before shared-state/pointer mutation and before releasing the reservation, renew exact owned restart/pending markers and drain all matching installation tray invocations, including children created during any expired-marker interval. Query process image/argv only in memory, verify PID/start identity, and refuse safely on unknown or non-exiting startup children without killing them. Keep the marker fresh across the bounded operation-owned restart and verify its activation/process/version before clearing only owned records. Boundary renewal plus all-child draining is sufficient; a new background heartbeat framework is not required. The temporary reservation's apparent `IsRunning` state is maintenance coordination, not proof of an actual monitoring owner.
- Cleanup/Retire use a short launch section to recheck current selection/previous and acquire an exclusive payload lease. Keep that lease and update serialization while hashing/deleting outside the launch gate. Preserve shared retention and owned-file verification rules.
- Regression trials deliberately block a slow phase for over three seconds using disposable-only barriers and run real generated commands during the block. A baseline launch-gate hold demonstrates the failure mechanism; fixed-path proof must show hooks finish before the slow phase is released. Cover each provider, mixed concurrent sessions, legacy launcher commands, native ingress and lease retention.
- Run the existing `tools/BellTower.Ingress.Bench` on the final package with disposable settings/data. Preserve its durable-count and timing gates. No personal provider settings are changed and no authenticated field reproduction is claimed.

## Risks / Trade-offs

- [The PATCH ≥ 9000 range looks odd] → Every surface shows the explicit "dev" label and commit. The docs explain the range.
- [A tray started from an older payload after rollback shows an unmarked dev payload as a plain version] → Expected. Older code cannot read the marker. Rollback reports which version is now running.
- [The repository Release build used by the script differs from the packaged build] → The script compares the package's source commit with the repository HEAD and Release assembly time, and refuses a stale build unless overridden.
- [The existing unmarked 0.1.9 and 0.1.10 payloads keep colliding with future stable 0.1.9 and 0.1.10] → The tray automatically reclaims them once they are no longer selected or retained. `Retire` always refuses them. Until cleanup, docs and the skill warn about the collision.

## Migration Plan

1. Implement, review and commit through the delegated workflow.
2. Build a marked dev package (`package.ps1 -Dev` gives `0.1.9001` from base 0.1.8 when no higher number exists) and run `install-dev.ps1` Preflight. A live Switch on the maintainer installation is a separately authorized step after review, as task 6.1 specifies.
3. The legacy unmarked dev payloads need no manual step. After the switch, the tray's automatic cleanup removes `0.1.9` (neither selected nor `previous`). It removes `0.1.10` after the next dev switch or stable update, when 0.1.10 is no longer `previous`. Until then a stable 0.1.10 cannot be staged, and the docs and skill say so.
4. Rollback for any step: `install-dev.ps1 -Action Rollback`. Stable releases update normally from any dev selection.
