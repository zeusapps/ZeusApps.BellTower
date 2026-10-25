---
name: install-dev-build
description: Build, preview, install, verify, roll back and retire a local Bell Tower development build on the maintainer's versioned installation. Use when the user asks to install, try, test, roll back or remove the current dev build, or says "install-dev-build". Installing needs the user's explicit authorization.
---

# Install a Bell Tower development build

Maintainer operations for trying an unreleased build on the real versioned installation, with a way back. This skill is operations guidance only. It does not replace the repository's delegated OpenSpec workflow: code changes still go through that workflow.

Run everything from the repository root in PowerShell 7. Paths below use the default per-user installation (`%LOCALAPPDATA%\Programs\ZeusApps.BellTower`) and data directory (`%LOCALAPPDATA%\ZeusApps.BellTower`).

## Rules that always apply

1. **Explicit authorization.** Build and Preflight are safe to run when asked. `Switch`, `Rollback` and `Retire` change the maintainer's installation, so run them only after the user clearly says so for this operation. Earlier approval for a different build or action does not carry over. Show the Preflight result first (version, commit, dirty flag, current tray state).
2. **Never stop agent sessions.** The scripts stop only the verified Bell Tower tray and restart it from the stable launcher. Do not stop terminals, Claude Code or Codex processes, VS Code or provider runtimes. Do not stop the tray by any other means.
3. **No integration changes.** Never edit `~/.claude` or `~/.codex`. Never run `bell-tower setup ... --apply` unless the user asks for it separately. A `setup --desktop` preview without `--apply` is fine.
4. **No stable-launcher or payload changes.** Do not edit files under `versions\` or the launcher by hand. Do not pass `-AllowStaleBuild` or the disposable test switches (`-Disposable`, `-NoTray`, `-SkipProbe`) against the real installation.
5. **Report honestly.** State what ran, the version now selected, whether the tray restarted, where the operation record is, and how to roll back. Say plainly if a step failed or was skipped.

## 1. Build

```powershell
dotnet build -c Release
./scripts/package.ps1 -Dev
```

`package.ps1 -Dev` picks the next free dev number (`MAJOR.MINOR.9001` and up, above anything already under `artifacts\dev-builds\` and in the installation), finds `vswhere.exe` and the Windows SDK itself, and writes `artifacts\dev-builds\bell-tower-<version>-win-x64.zip` plus its `.sha256` and `.release.json`. It prints `Packaged <path>`. The release metadata records the dev channel, the source commit, whether the working tree was dirty, the build time and the stable base version. If the toolchain is missing, the message names the Build Tools or the Windows SDK; install them rather than looking for a fallback (none exists).

Preview the plan without building: `./scripts/package.ps1 -Dev -PlanOnly`.

## 2. Preflight (read-only)

```powershell
./scripts/install-dev.ps1 -Action Preflight
```

Uses the newest package under `artifacts\dev-builds\` (or pass `-Package <zip>`). It checks the package hash and contract, refuses anything that is not a marked dev build, refuses a stale build (package commit differs from HEAD, or sources are newer than the Release build), checks compatibility with the current data, and writes a private operation record. It prints JSON with `operationManifest`. Keep that path. The installation, the data directory and the tray stay unchanged.

## 3. Switch (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Switch -OperationManifest <operationManifest from Preflight>
```

It snapshots the selection and monitoring state into the private workspace, adds the dev payload next to the existing ones, stops only the verified tray, selects the dev build (the previous version is recorded for rollback) and restarts the tray through the stable launcher. An operation-owned restart marker and temporary tray-instance reservation fence new and in-flight tray starts while the selection changes, without holding the hook launch gate during copy or verification. It stops without changing the selection if the tray owner, the selection, original payload bytes or coordination markers differ from the reviewed operation, or if startup process identity cannot be verified.

## 4. Verify

```powershell
$launcher = "$env:LOCALAPPDATA\Programs\ZeusApps.BellTower\bell-tower.exe"
& $launcher --version          # 0.1.9001 dev b74a12b (2026-10-24 21:30 UTC)
& $launcher update --status    # Running ... dev ...; selected ... dev ...
& $launcher doctor             # JSON "version" field shows the dev label
```

The tray icon tooltip and the Sessions window title show the same dev label with the commit. A stable build shows no label. Dev builds compare stable releases against their base version, so a later stable release is still offered: `& $launcher update --check`.

Optional: preview (never apply) integration changes with `& $launcher setup --desktop`.

## 5. Roll back (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Rollback -OperationManifest <same operationManifest>
```

Restores the selection and the `state.json` / `state.json.bak` snapshot from immediately before the switch, then restarts the tray on that version. This is a one-level rollback. Matching dead-operation records can be recovered from the same manifest; unrelated marker bytes are preserved. Monitoring state written by the dev tray since the switch is not kept. Identity data and configuration are left alone. It refuses if the selection or tray owner changed since the switch. The restored tray may automatically reclaim the dev payload once it is no longer selected or retained.

## 6. Retire an old dev payload (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Retire -Version 0.1.9001
./scripts/install-dev.ps1 -Action Retire -Version 0.1.9001
```

Removes a dev payload only if it has the `dev` marker and is not selected, not the recorded previous version, not leased by a running process, and not needed by an unfinished update. The same retention and file-integrity checks protect automatic cleanup and explicit Retire. Unmarked payloads, including the old trial builds `0.1.9` and `0.1.10`, are always refused by Retire; the tray's automatic cleanup reclaims verified unmarked payloads once they are neither selected nor retained.

## Where things are

- Packages and the default private workspace: `artifacts\dev-builds\` (the workspace is `artifacts\dev-builds\operations\`, restricted to the current user, SYSTEM and Administrators).
- Procedure and recovery details: `docs/development.md` ("Development builds") and `docs/releases.md`.
- Both Claude Code (`.claude/skills/`) and Codex (`.agents/skills/`) carry this identical file.
