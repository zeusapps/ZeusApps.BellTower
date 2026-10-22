# Design

## Context

See proposal.md for motivation and scope. The checkout currently uses `master`, has no remote, and packages version 0.1.7. `scripts/package.ps1` produces a self-contained single-file Windows x64 executable, VSIX, docs/helpers, release.json, ZIP, and checksum. Application and extension versions are maintained separately. `global.json` selects SDK 10.0.113 with latest-patch roll-forward; NuGet lock files are tracked. No GitHub workflow exists.

The tray drains a durable inbox into persisted session state. Identities, attachments, host registrations, focus requests, runtime records/tokens, and attention state live under `DataPaths`, independently of the executable. `TrayApplication.ExitThreadCore` disposes monitoring and UI components; it does not stop the dedicated runtimes. `CodexNameService` and `CodexPermissionPump` can reconnect through stored runtime endpoints after verifying process/shell ownership. Existing silent-restoration and viewed-episode tests cover parts of restart behavior, not a complete packaged version change.

Dedicated Codex runtime managers execute `bell-tower.exe codex-runtime-host`; they own the backend lifetime until their shell/session ends. The optional `AgentLauncher` also remains running while its child does. These processes lock an in-place executable. Killing a runtime manager can terminate its backend and interrupt the user's session. `Environment.ProcessPath` is currently used for internal startup and setup path generation, so a versioned installation needs an explicit distinction between stable entry point and pinned payload path.

WorkLog's current updater is an inspected reference, not a dependency. Its pending `fix-update-status-read-race` plan documents a status-reader/loaded-image race; normal Bell Tower payload activation must not inherit its global-idle prerequisite. Its requested versioned-installation handoff was added separately under WorkLog's docs/handoffs.

## Goals / Non-Goals

**Goals:** Coexistence of active old runtime managers and a new tray; continued hook ingestion during restart; durable and recoverable version selection; honest update status; validated GitHub distribution; stable integration paths and bounded operational work.

**Non-Goals:** Transferring an agent or backend into a new process; automatically installing discovered updates; changing Codex APIs or its TUI; a system service; WorkLog implementation or coupling; package-manager/installer/Marketplace publication; prerelease/private authenticated feeds; ARM64 or non-Windows builds; uninterrupted upgrades across arbitrary incompatible state formats; replacing a loaded bootstrap without maintenance.

## Decisions

### 1. Use a small stable bootstrap and immutable payload directories

Add a focused launcher executable project to the solution. Package it as the root `bell-tower.exe`; keep the existing application executable under `versions/<semver>/bell-tower.exe`. The root also contains a bounded installation manifest and `current.json` holding the selected and prior known-good version plus activation identity. User data remains in the existing separate data directory.

```text
installation/
  bell-tower.exe                   stable bootstrap
  installation.json                bootstrap/layout protocol
  current.json                     selected/previous payload + activation id
  versions/
    0.1.8/                         retained while referenced
      bell-tower.exe
      release.json
      bell-tower-focus-0.1.8.vsix
      docs/ and helpers/
    0.1.9/                         candidate/current payload

data directory/
  state.json, identities/, inbox/, attachments/, codex-runtimes/, ...
  updates/                         cache, staging, outcome, recovery
```

The launcher resolves only a supported version beneath its own installation root. Reject malformed selection, escaping paths, missing/incomplete payloads, reparse-point traversal, and incompatible bootstrap protocols. Fail with actionable stderr/exit status rather than choosing an arbitrary directory. Ordinary updates install only new payloads and atomically replace selection metadata; they do not overwrite the launcher or an existing version directory.

Dispatch preserves exact arguments, working directory, standard handles, relevant environment, and exit status. Prefer inherited handles, and retain the existing synchronous STA application entry for the actual tray. The launcher passes a validated installation-root contract to the payload; services verify it against the directory layout instead of treating an environment variable as permission to update any path. Diagnostic/help/version behavior must remain compatible with current commands.

New hooks, shell functions, external helpers, and VS Code executable settings refer to the stable launcher. Internal commands that belong to an existing session, including a runtime manager and its backend, remain pinned to their own payload. New tray startup resolves the current selection, including when requested from an old helper. Resolve the installation root explicitly in setup and CLI/update services; do not propagate a payload's `Environment.ProcessPath` into persistent registrations. Replacing one in-use monolithic executable was rejected because it recreates the requirement to stop sessions.

### 2. Coordinate launches, updates, and cleanup per installation

Use an installation update lease to serialize staging publication/selection and to exclude destructive cleanup, independently of the existing per-data-directory tray mutex and state locks. Establish shared per-version lifetime leases for every payload process, including directly started internal helpers. A short launch/cleanup gate covers reading selection and handing lifetime ownership to the child; cleanup requires exclusive access to a candidate version's lease. This closes the resolve/start/delete race that a one-time process scan would miss.

Retain current, prior-known-good, staged/activating, and recovery-referenced versions regardless of process counts. Retain any other payload still leased or locked; retry cleanup later without forcing exit. Bound cleanup to validated owned version directories, never arbitrary installation contents or data. Concurrent starts choose a complete old or new payload. Non-mutating status checks do not take the exclusive activation lease or block selection changes. Scope all outcome writes to activation IDs so an old helper cannot overwrite a later operation's status.

### 3. Introduce an explicit compatibility contract

Add release metadata declaring application version, Windows runtime, package/layout version, bootstrap protocol, shared-data contract, runtime-record contract, and shell/bridge protocol compatibility. Publish a small `bell-tower-<version>-win-x64.release.json` asset alongside the ZIP and checksum so discovery can inspect compatibility without downloading an application package. Its declared version, assets, package hash, and contracts must agree with the checksum and packaged manifest before activation. The first versioned releases retain existing data/event/runtime formats and support the baseline shell/bridge behavior. Add a bounded, read-only packaged probe that reports version/contracts without starting a tray, changing state, or contacting a provider.

Compatibility requires bidirectional coexistence: new tray/new hooks must consume data from old helpers, and old helpers must safely read/write shared records after activation. Preserve unknown fields where supported older writers can save; do not rely only on a new version's ability to read old JSON. Keep state rollback distinct from application rollback. Before selection, compare the candidate against active payload/integration contracts and data; unsupported or unknown compatibility defers activation while retaining monitoring. Future incompatible transitions require an explicit maintenance release and separate migration plan.

Do not change provider runtime transports in this change. The reopened tray reads existing authenticated endpoint/token records, validates manager/shell identity, resumes name ownership and permission verification, and reports unavailable capabilities honestly. No old runtime credential is logged, copied into release metadata, or sent to GitHub.

### 4. Separate discovery from installation

Use the verified official repository's latest stable release metadata. Proposed operational defaults match WorkLog: public personal-account `zeusapps/ZeusApps.BellTower`, primary branch `main`. These are planning defaults; verify account, destination, repository visibility, and unexpected existing remote/repository state before external publication. An unexpected destination is an operational conflict, not an overwrite target. The installed feed is fixed to the verified official repository, without arbitrary user-supplied download URLs.

Automatic discovery runs as cancellable background work owned by the tray and never blocks its tick, hook ingestion, startup, or shutdown. Share a bounded atomically written cache and attempt timestamp per data directory. Attempt at most once per 24 hours across processes sharing that directory; honor Retry-After/rate-limit metadata and expire unvalidated notices after seven days. Timeout and malformed metadata errors remain nonfatal. Validate release URLs, stable semantic version, expected assets, and compatibility. An explicit check can bypass the daily interval while honoring server backoff. `updates.enabled` defaults to true; disabling it suppresses automatic requests/notices but keeps explicit checks/install commands usable.

Compare available, selected, and running versions separately. An old runtime process is expected after activation; it is not evidence that another installation is required. Re-read selected version under the update lease immediately before committing selection and refuse equal/older stale plans. Show availability via tray menu/status, with Check for updates and explicit Install update; add CLI update/check/status using the current command-per-class convention. No installation occurs merely because an update was found.

### 5. Stage, activate, and restore monitoring as a transaction

The coordinator runs outside the tray/payload directory being retired, identified by an activation token and validated plan. It can be a pinned candidate/helper invocation; it must hold its own lifetime lease and survive closing the tray. Record installation root, selected/original version, target version/hash/contracts, selected data directory, original tray process identity, and whether monitoring was running. Do not confuse process IDs with identities or record agent secrets in the plan.

Activation sequence:

1. Download bounded ZIP/checksum content to separate staging. Verify exact official asset identity and SHA-256, extraction paths/entry allowlist/size limits, version metadata, runtime, read-only executable probe, and compatibility. The full ZIP supports fresh installs with a bootstrap and initial selection; an update extracts and validates that layout but imports only the new payload. Never copy archive-provided selection, installation identity, or bootstrap over an existing installation. A candidate requiring an unsupported bootstrap is a maintenance update and cannot activate through this path. Publish a complete immutable candidate version under the installation lease. Never use the old WorkLog flat-file allowlist unchanged: Bell Tower payloads contain a VSIX, docs, and helpers.
2. If a tray is running for the chosen data directory, establish an update/restart marker and request only that tray's controlled exit. Wait for its verified exit and bounded transient helpers. During this window, normal tray-start requests defer to the coordinator instead of starting an old or duplicate tray. Hook ingestion remains available through the stable launcher and durable inbox. Other independent data-directory trays are left running on their original payloads and participate in compatibility/retention checks.
3. Atomically switch current selection while retaining its prior value and activation ID. Launch the selected tray with the same data directory only if it was previously running. Readiness must confirm the expected payload version, process identity, data directory, successful initial state/inbox reconciliation, and single-instance ownership, using a bounded local handshake. Existing long-lived runtimes/agents remain untouched.
4. Report `activated` before readiness and `monitoring-restored` only after the handshake. If the tray was stopped initially, report activation without starting one. Distinguish deferred integration refresh from failed monitoring.
5. On shutdown timeout, stop before selection changes and clear the restart marker safely. On activation/readiness failure, restore prior selection only if this activation still owns it, restart the old tray if appropriate, retain evidence/candidate for diagnosis, and record `rolled-back` or actionable `failed` when recovery is incomplete. Clear transaction markers in bounded recovery paths. Never restore old state snapshots over events written during the attempt.

The restored tray uses existing `StateStore.Drain`, attachment liveness checks, identity records, runtime reconnection, and silent restoration rules. Preserve episodes/timestamps/viewed and dismissed state. Already-due waits, including waits accumulated during the gap, restore silently; remaining grace still elapses normally. Validate the complete path, since existing unit tests cover only components of this behavior.

### 6. Refresh integrations through their existing ownership model

Versioned payloads contain exactly their matching VSIX/helper sources. Resolve these from the selected payload rather than enumerating old VSIX files lexicographically at the installation root. Preserve selected providers, registered projects, custom data directories, ownership manifests, unrelated settings, and user edits. Refresh only existing owned integrations as an explicit part of installation; do not run a broad desktop setup that enables previously unused providers or adopts unowned extensions. Use the current setup plan/conflict detection rather than bypassing it.

Old loaded shells/extensions remain supported by the initial protocol contract; refreshes may install the new owned VSIX/module for future activation. Report needs-new-shell or editor-reload status, with no forced reload or session restart. Conflicting user edits block the refresh with a clear partial-outcome record; required incompatible integration changes block payload activation during preflight. Keep obsolete version-specific VSIX files confined to their retained payload directories.

### 7. Reuse existing checks in GitHub workflows

Create a reusable Windows validation workflow for PRs, primary-branch pushes, and release calls. Install the SDK through global.json and a pinned supported Node runtime. Run locked restore, Release build, .NET tests with retained TRX results, existing `node --test` suites, formatting verification, package checks, and a new isolated packaged updater acceptance executable. Cache only dependency material keyed by lock files. Supply no publication credentials to validation jobs.

The tag workflow validates the exact tagged commit and strict vMAJOR.MINOR.PATCH agreement across Directory.Build.props, extension manifest, installation/release metadata, and packaged probes. Publish the verified ZIP, checksum, and release-metadata sidecar only after validation; prefer passing those checked artifacts to the publisher rather than rebuilding different bytes. Keep workflow token contents read-only except for the release publishing job. Reject duplicate published tags/assets instead of moving or overwriting them. Use fixed reviewed action revisions and current official GitHub guidance when implementing; Context7 was consulted during exploration for .NET workflows and token permissions.

Initial publication includes reviewing outgoing tracked files/history, verifying account/target, establishing `main` without rewriting history, adding the expected origin, pushing, observing CI, tagging an unpublished matching version, and observing publication. Independently download and hash the public assets, run isolated version/bootstrap checks, and verify the release endpoint before marking publication complete. Historical local tests or successful artifact creation do not count as remote publication evidence.

## Risks / Trade-offs

- [Mixed old/new writers can silently lose fields or corrupt assumptions] -> Keep the first release schema-neutral, declare contract ranges, and test actual mixed-version subprocesses and writes before activation is permitted.
- [The stable bootstrap can remain loaded for a long session] -> Ordinary payload releases never replace it; keep its contract small and document separate bootstrap maintenance.
- [Another hook/start request races the tray restart] -> Installation transaction identity plus data-directory restart coordination prevents duplicate/old tray startup while keeping ingestion available.
- [Selection switches but monitoring does not recover] -> Token-scoped readiness handshake, bounded recovery, retained prior payload, and local status distinguish activation from recovery.
- [Old payloads consume disk indefinitely] -> Retain while leased/referenced, expose retained versions, and retry bounded cleanup after natural exits.
- [Current WorkLog updater patterns contain in-place assumptions] -> Port only useful discovery/verification patterns and test status/launch/cleanup concurrency against this layout.
- [Shell or VSIX refresh cannot take effect in a running host] -> Support compatible older integrations and report deferred activation; never force a host restart.
- [Unverified source/asset/history publication] -> Review outgoing material and distribution prerequisites before public writes; resolve repository/visibility conflicts explicitly.

## Migration Plan

1. Build and validate the bootstrap/layout/updater using disposable installations before touching the user's active install. Retain the existing executable-only package behavior for source-development guidance until migration is ready.
2. Choose an unpublished release version greater than 0.1.7 and update all version-bearing inputs together. The exact number is an implementation-time release decision after checking remote state.
3. Establish the versioned layout at the intended stable installation path through an explicit migration plan/preview. Current hooks and shell modules target an executable that may be locked. If replacing it, require one planned maintenance window and natural session shutdown; do not silently stop sessions. An alternate new installation root requires explicit owned registration migration and validation. Document this as first adoption, not a routine-update limitation.
4. Generate owned integration references to the stable launcher and verify provider trust/setup independently. Preserve the existing data directory and do not reset session identities/configuration.
5. Subsequent compatible updates stage and switch payloads with the transaction above, keeping old managers and other old processes alive until natural exit. Failed activation reselects the prior payload without rolling data back.
6. Document bootstrap replacement as separate maintenance. Keep source builds/unsupported layouts on manual installation guidance. Finish with observed CI/publication and independent downloaded-package checks, then record exact versions and evidence.

## Open Questions

- Verify the proposed `zeusapps/ZeusApps.BellTower` public destination and authenticated account at publication time; no external repository has been created by this planning work.
- Select the next unpublished semantic version and first baseline package after checking existing tags/releases. The layout, compatibility policy, and acceptance gates do not depend on the numeric choice.
