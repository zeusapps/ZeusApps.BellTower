# Release operations and updates

Bell Tower is distributed from [zeusapps/ZeusApps.BellTower](https://github.com/zeusapps/ZeusApps.BellTower). The primary branch is main. Ordinary pushes and pull requests run Windows validation. A matching immutable vMAJOR.MINOR.PATCH tag calls the same validation before publishing its exact checked ZIP, SHA-256 file, and small release-metadata JSON sidecar. Automatic application checks never publish source or install an update.

## Install a versioned release

Download the ZIP and .sha256 from the official release, compare Get-FileHash -Algorithm SHA256, and extract the complete ZIP into a stable user-writable directory such as %LOCALAPPDATA%/Programs/ZeusApps.BellTower. Keep %LOCALAPPDATA%/ZeusApps.BellTower (or your custom --data-dir) separate. Invoke the root bell-tower.exe; do not register the executable under versions/ directly.

The root launcher selects a verified complete payload through current.json. installation.json declares the bootstrap/layout protocol. Each versions/VERSION directory contains the application, matching VSIX, installation docs/helpers, and release.json file inventory/contracts. New commands use the selected version. Existing runtime managers and long-running launch commands stay on their original payload until they finish naturally. Ordinary updates do not overwrite the root launcher.

Run setup --desktop without --apply first to review integration changes, then apply deliberately. Review provider trust as usual. Setup uses the stable entry point and resolves the matching VSIX from its selected payload.

## Check and install

The installed tray checks the official stable feed in the background, at most once per day for processes sharing a data directory. Checks use bounded requests, coordinated cache state, server retry guidance, and seven-day notice expiry. They transmit application identification and HTTP metadata, not session contents, paths, or runtime tokens. Disable automatic checks by setting updates.enabled to false in the selected data directory's config.json. Source builds do not start automatic discovery.

Use Check for updates in the tray or:

~~~powershell
bell-tower update --check
bell-tower update
bell-tower update --status
~~~

--check fetches release metadata only. update explicitly downloads, verifies, and stages a newer compatible version. It returns before the separate coordinator finishes; wait for recovery and then inspect --status, using the same --data-dir when applicable. --status is local and offline, and reports running/selected versions independently. A staged result does not mean monitoring has recovered. Installation from the tray's Install update action follows the same path. Discovery never installs unattended.

During activation, only the tray for the selected data directory restarts. Claude/Codex agents, terminal hosts, dedicated runtime managers/backends, and other data-directory trays remain running. Hooks still write to the inbox through the launcher. The new tray reloads state and verified runtime endpoints. Existing due waits restore silently; names, palettes, episode timing, dismissal, and viewed timers survive. If no tray was running when a CLI update started, activation leaves it stopped.

Owned shell functions and the owned bridge are refreshed without enabling other providers or adopting unowned extensions. New shells load refreshed functions. Reload VS Code when convenient to load a new extension. User edits can leave integration refresh pending; status reports that partial result. Editors, shells, and agent sessions are never restarted automatically.

## Compatibility, rollback, and retained versions

The initial versioned release uses layout/bootstrap/state/runtime/shell/bridge contract 1 and keeps the existing state schema. A release must support coexistence with old helpers and loaded integrations before selection changes. Unsupported bootstrap/data contracts require a maintenance update; they are not forced over an active session. Package verification covers official assets, checksum/sidecar agreement, bounded safe extraction, declared file inventory hashes, and a read-only executable contract/version probe.

Each activation records its ID and staged/activated/monitoring-restored/failed/rolled-back outcome under installation/updates. The coordinator retains the previous selection and restores it when activation/readiness fails, then attempts to reopen its tray. It never rolls user data back. Status reports incomplete recovery with its reason. Keep the corresponding updates/ID directory and both payloads until inspected. A stale pending.json after an interrupted coordinator deliberately prevents another installation: inspect its plan/outcome and process identity first. With no coordinator running, preserve a copy of recovery metadata, restore a known-good current.json selection if necessary, remove only the stale pending/restart markers, and start the stable launcher. Never remove an active coordinator's markers or edit state snapshots as an application rollback.

Cleanup retains current/previous payloads, active launch/runtime leases, and pending/failed recovery references. It preserves unowned/modified files and retries busy versions later. There is no need to stop a session merely to reclaim disk space.

## One-time migration from the legacy executable

A legacy installation has a full application at its registered bell-tower.exe path. That file can be held open by the tray, a runtime manager, or launch command. Establishing a launcher at the identical path may need one planned window after sessions finish naturally. Bootstrap replacement in a future release can likewise need maintenance; routine compatible payload updates do not.

A separate installation directory is an alternative: extract the new complete release there, retain the existing data root, preview setup from the new root launcher, and deliberately refresh owned paths. Existing sessions may still have cached old hook/module paths; retain the old installation until those sessions naturally end. Do not replace or delete a locked legacy executable, stop an agent/backend, or bypass trust to complete migration. Source builds and unsupported layouts receive this manual guidance instead of self-replacement.

## Maintainer release checklist

1. Verify the authenticated account, repository identity/visibility, and intended origin. Review outgoing tracked files and history; keep runtime data, private integration backups, test output, caches, and generated packages ignored. Establish main without rewriting history.
2. Select an unpublished version and synchronize Directory.Build.props and vscode/package.json. Never reuse/move a public tag. Run locked restore, Release build/test, JavaScript tests, formatting, package creation, and scripts/update-acceptance.ps1.
3. The first acceptance baseline is current source compiled separately with fixture version 0.1.7; it proves two compiled versions and the new layout/process contracts, not historical compatibility with the legacy executable-only 0.1.7. On later releases, CI downloads the previous official stable version as the baseline. Locally pass -BaselineArchive to test that real package.
4. Commit and push main, observe validation, then push the matching version tag. The release workflow validates the exact tag and downloads the already validated artifact for publication; its publisher alone receives contents-write permission.
5. Independently download all three public assets, verify hashes/contracts, extract and run --version and --distribution-info through the launcher, and check update --check against isolated data. Confirm the latest stable endpoint and record release/workflow links plus actual evidence before declaring the release complete.

Packaged acceptance uses disposable hidden Windows consoles, a fixture authenticated loopback backend (no model calls), a real dedicated runtime manager, a live fake agent/launch process, and isolated state. It exercises version activation, reconnection, event delivery, state preservation, stopped-tray activation, and injected candidate readiness failure/rollback. The fixture terminates only its own processes during cleanup. Existing authenticated provider/host acceptance gaps remain documented separately.
