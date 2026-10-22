# Tasks

## 1. Establish installation contracts and bootstrap

- [x] 1.1 Add installation/release metadata with separate layout/bootstrap, shared-state, runtime-record, and shell/bridge compatibility contracts plus a read-only packaged probe; verify schema-neutral baseline records round-trip and malformed/unsupported declarations are rejected without state mutation.
- [x] 1.2 Add the stable launcher project and version-selection resolver to the solution; verify version/help/command dispatch, exact quoted/Unicode arguments, stdin/stdout/stderr, exit status, working directory, and environment through real subprocesses without changing the application's STA tray entry.
- [x] 1.3 Add validated stable-root versus pinned-payload path resolution; verify hooks, setup, shell functions, VS Code settings, and tray starts use the stable entry point while existing runtime managers/internal helpers remain pinned to their owning version.
- [x] 1.4 Implement per-installation mutation coordination, per-version lifetime leases, and a launch/cleanup handoff; verify controlled races cannot delete a selected, starting, running, prior-known-good, or recovery-referenced version and no process is terminated by cleanup.

## 2. Produce compatible Windows packages

- [x] 2.1 Extend scripts/package.ps1 for fresh-install bootstrap/current/versioned payload layout, explicit matching versions, and required executable/VSIX/docs/helpers manifests; verify fresh extraction works without a separately installed runtime and mismatched application/extension/package versions fail packaging.
- [x] 2.2 Emit the ZIP, SHA-256 file, and bounded release-metadata sidecar from the same package bytes; verify sidecar/hash/packaged-probe agreement and that reused output directories contribute no unowned or stale files.
- [x] 2.3 Add bounded package validation/extraction and immutable payload import for updates; verify traversal, duplicate/reparse entries, missing files, oversize content, incorrect version/runtime/hash/contracts, and incompatible bootstrap rejection leave current selection unchanged and never import archive-provided current.json over an existing install.

## 3. Discover releases and expose update controls

- [x] 3.1 Implement official stable-release metadata discovery with fixed verified repository identity and small compatibility sidecar reads; verify valid-newer, equal/older, prerelease/draft, missing assets, incompatible versions, timeout/offline, and rate-limit responses without application-package downloads during check-only commands.
- [x] 3.2 Add cancellable tray-owned automatic discovery, an atomic per-data-directory cache/attempt lease, and updates.enabled; verify at most one automatic attempt per 24 hours across sharing processes, bounded stale-notice expiry, server backoff, disabled behavior, and responsive startup/monitoring/shutdown.
- [x] 3.3 Add update, --check, and offline --status commands plus tray Check for updates and explicit Install update controls; verify argument validation, running/selected/available version presentation, accurate outcome messages, and that discovery alone never installs or repeatedly offers an already selected release.

## 4. Activate and recover without stopping sessions

- [x] 4.1 Add token-scoped durable staging/activation plans and an independently running coordinator with serialized selection changes; verify candidate publication, concurrent updates, stale old-process offers, superseded outcome writes, unwritable/source layouts, interruption, and failed preflight preserve the selected version and user data.
- [x] 4.2 Add data-directory-scoped tray restart coordination and verified process shutdown/readiness handshakes; verify hooks still enqueue events, racing starts cannot create old/duplicate trays, other independent data-directory trays remain running, and a stopped tray is not started implicitly by CLI installation.
- [x] 4.3 Activate the verified payload and restore monitoring against the same data directory; verify existing agent/runtime process identities and authenticated endpoints survive, name ownership/permission verification reconnect, queued events replay idempotently, and session palettes/episodes/timestamps/viewed/dismissed state and silent grace restoration are preserved.
- [x] 4.4 Implement conditional prior-selection rollback and monitoring recovery for shutdown/activation/readiness failures; verify token ownership prevents rolling back a later update, no state snapshot overwrites concurrent events, and status distinguishes staged, activated, monitoring-restored, rolled-back, and failed recovery.
- [x] 4.5 Implement bounded old-version retention/cleanup after natural process exits; verify live runtime managers and long-running launch commands keep their payload, pending launches and recovery references remain protected, and locked/unowned files are preserved for later retry.

## 5. Preserve owned integrations and migration behavior

- [x] 5.1 Refresh only already owned providers/modules/bridges using the selected payload's sources and existing setup ownership rules; verify custom data directories, selected providers, project profiles, unowned extensions, user-edited files, and failed extension actions retain their ownership/preservation behavior without enabling unrelated integrations.
- [x] 5.2 Report compatible loaded shell/bridge activation as pending and enforce integration compatibility before selection; verify no forced editor/terminal/agent restart, no lexicographic selection of obsolete VSIX files, and incompatible releases remain deferred with actionable guidance.
- [x] 5.3 Add an explicit legacy-layout migration preview/path and unsupported-install/bootstrap-maintenance guidance; verify locked legacy executables are never forcibly replaced, migration preserves the data root, and docs distinguish one-time migration from session-preserving routine updates.

## 6. Prove the packaged update flow

- [x] 6.1 Add a disposable packaged Windows acceptance runner covering fresh installation and two genuinely distinct payload versions: keep a managed fixture runtime/agent and optional launch command alive on the old payload, update via the stable launcher, then verify the new tray and commands use the new version while old processes/endpoint/focus ownership survive. Record baseline provenance; same-version copies alone do not establish mixed-version support.
- [x] 6.2 Extend acceptance with events arriving while the tray is stopped, quiet active waits, viewed/dismissed chips, native rename ownership, status reads during activation, competing launches/updates/cleanup, rollback/readiness failure, custom data roots, and incompatible metadata; verify durable state preservation and that cleanup touches only fixture-owned processes/directories.
- [x] 6.3 Run locked restore, warning-free Release build, full .NET and JavaScript suites, formatting verification, packaging, and packaged acceptance; retain exact commands, versions, and results in validation docs and keep the existing session-title change's remaining live evidence tasks separate.

## 7. Add GitHub validation and release automation

- [x] 7.1 Add a reusable Windows validation workflow for pull requests, primary-branch pushes, and release callers using global.json, pinned supported Node/actions, locked dependency caching, all required checks, and retained diagnostics; verify workflow syntax and successful validation from a clean disposable checkout with no private client state or publish credentials.
- [x] 7.2 Add exact-tag release validation and publication of the validated ZIP/checksum/metadata sidecar using publication-only contents-write permission; verify strict tag/app/extension/package agreement, failed-check prevention, checked artifact reuse, and rejection of duplicate published tags/assets before a real release.
- [x] 7.3 Document release operations, stable installation paths, explicit check/install/status, mixed-version retention, partial integration activation, recovery, and maintenance cases in README/installation/architecture/release docs; verify commands match packaged help and the acceptance evidence supports each operational claim.

## 8. Publish and independently verify

- [x] 8.1 Verify authenticated GitHub account and the proposed public zeusapps/ZeusApps.BellTower destination/visibility, inspect tracked outgoing files/history and distribution prerequisites, and resolve unexpected remote/repository conflicts; record the verified target before making external changes.
- [x] 8.2 Establish main without rewriting history, create/connect the verified repository as needed, push prepared source, and observe remote validation; verify origin/branch and actual workflow success before tagging, preserving unrelated local work.
- [x] 8.3 Choose an unpublished version above 0.1.7 with all manifests synchronized, push its immutable matching tag, and observe the release job; independently download all assets, compare checksum/metadata, run isolated packaged bootstrap/version/update checks, and confirm the latest stable endpoint before recording publication complete. Never use the real active installation or authenticated agent sessions as test fixtures.

## Planning record

The user requested the WorkLog handoff and this proposal. The handoff is delivered at `D:/Code/ZeusApps.WorkLog/docs/handoffs/2026-10-22-session-preserving-updates.md`; WorkLog implementation remains separate. Automatic discovery with explicit installation follows the discussed WorkLog behavior. GitHub destination/visibility and the next unpublished version are operational checks before publication. No implementation, production migration, repository creation, push, tag, or release was performed while authoring these artifacts.
