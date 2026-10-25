# Proposal

## Why

Maintainers test unreleased Bell Tower builds against their own versioned installation, and each attempt has been ad hoc:

- The 0.1.9 trial used a one-off script in an archived change.
- The 0.1.10 install on 2026-10-24 used a hand-adapted copy of that script, because the original hard-codes its versions and its attachment-health level.

Nothing marks a dev build. The tray, `doctor` and `update --status` show a plain number such as "0.1.10", which looks like a stable release. That number also collides with future stable releases:

- A stable 0.1.9 is rejected as "already selected or superseded".
- A stable 0.1.10 fails with "Candidate version already exists".

Packaging also needs `vswhere.exe` on PATH, so the NativeAOT ingress link fails in a normal shell.

## What Changes

- **Visible dev marker.**
  - Dev packages record `build` metadata in `release.json`: channel `dev`, source commit, dirty flag, build time and base version.
  - The executables carry an informational version such as `0.1.9001-dev+b74a12b`.
  - The tray icon tooltip, About/Sessions UI, `doctor`, `update --status` and `bell-tower --version` show "dev", the commit and the build time.
  - Older payloads and launchers ignore the new field, and stable packages are unchanged.
- **Reserved dev version range.**
  - Dev builds take PATCH numbers of 9000 and above (for example `0.1.9001`), which stays inside the existing MAJOR.MINOR.PATCH contract that installed launchers already parse.
  - Stable packaging refuses that range, and dev packaging requires it.
  - While a dev build is selected, update discovery compares stable offers with the dev build's base version, so a stable release can replace a dev build without colliding.
- **`scripts/package.ps1 -Dev`.** Builds a dev package outside `artifacts/update-acceptance/` with an automatically chosen free dev number. Both stable and dev packaging locate `vswhere.exe` themselves.
- **Supported `scripts/install-dev.ps1`.** Generalized from the archived trial script:
  - actions `Preflight`, `Switch`, `Rollback` and `Retire`;
  - no hard-coded versions or health level;
  - compatibility derived from the installed and candidate contracts;
  - a private backup workspace, verified tray stop and start, and a selection and state snapshot;
  - `Retire` removes only dev-marked payloads, under the same retention rule as the tray's automatic cleanup. Unmarked and stable payloads are never removed by it.
- **`install-dev-build` skill for both clients.** Identical skill files in `.claude/skills/` (Claude Code) and `.agents/skills/` (Codex). They describe building, previewing, switching, verifying, rolling back and retiring, and the safety boundaries: explicit user authorization, no stopping agent sessions, and `setup --apply` only on request.
- **Docs.** `docs/development.md` and `docs/releases.md` describe the dev channel, the reserved range and recovery.
- **Hook continuity (user-requested 2026-10-24).** Troubleshoot timeouts reported with released 0.1.8 and the Claude-built 0.1.10 trial without assuming a confirmed field cause. Keep payload copy/hash/delete and tray-stop waits outside the launcher gate during dev operations and automatic cleanup. Verify the current package's NativeAOT capture path and durable timing for both providers, plus concurrent legacy launcher hooks and mixed sessions during deliberately slow installation/cleanup phases.

## Capabilities

### New Capabilities
- `development-builds`: marking, packaging, installing, rolling back and retiring local development builds on a versioned installation, without blocking stable updates, with equal maintainer support from Claude Code and Codex.

### Modified Capabilities
- None. The versioned release and update behavior is not yet in the main specs. The new capability defines the dev-specific rules and leaves stable update behavior unchanged.

## Impact

- **Code:**
  - `src/BellTower.Distribution/ReleaseContract.cs`: optional build metadata and the reserved-range helper.
  - `src/BellTower/Updates/{UpdateDiscovery,ReleaseCheck,UpdateTransaction}.cs`: base-version comparison while a dev build is selected.
  - The tray tooltip and About/Sessions UI, `DiagnosticsReporter`, the `update --status` output and the version output.
- **Scripts:** `scripts/package.ps1` gets dev mode and `vswhere` discovery. `scripts/install-dev.ps1` is new and replaces the archived trial-script workflow.
- **Agent tooling:** new `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md`.
- **Docs:** `docs/development.md`, `docs/releases.md` and `docs/installation.md` (pointer).
- **Compatibility:**
  - No change to the launcher or bootstrap. Installed 0.1.8 and 0.1.9 launchers and payloads keep working.
  - Stable releases are unaffected. Existing unmarked dev payloads 0.1.9 and 0.1.10 on the maintainer's machine are reclaimed by the tray's automatic cleanup once they are no longer selected or retained, as the migration note explains.
- **No dependency changes.** The tooling still needs the existing MSVC Build Tools and Windows SDK.
