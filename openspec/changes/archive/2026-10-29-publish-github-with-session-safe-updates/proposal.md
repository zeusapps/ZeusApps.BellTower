# Proposal

## Why

Bell Tower has local Windows packaging but no GitHub remote, automated release pipeline, or updater. Publishing releases should let users update the tray and resume monitoring without closing their Claude/Codex sessions, including sessions whose dedicated Codex runtime managers still use the previous executable.

## What Changes

- Publish source and versioned Windows x64 ZIP/checksum releases through GitHub, following WorkLog's validation and version-tag gates. The proposed repository is the public personal-account `zeusapps/ZeusApps.BellTower`, with `main` as the primary branch; verify the destination and visibility before initial publication.
- Add Windows CI for locked restore, Release build, .NET/JavaScript tests, formatting, packaging, and packaged updater acceptance. Release publication must validate the exact tagged commit and matching app/extension/package versions.
- Introduce a stable launcher, immutable version directories, and an atomically selected current version. Existing runtime managers and optional long-running launch commands retain their original payload until they finish naturally.
- Automatically discover newer compatible stable releases without blocking monitoring. Show update availability in the tray and offer explicit check/install actions plus `bell-tower update`, `--check`, and `--status`; unattended installation is outside this change.
- Stage and verify an update before switching versions, restart only the tray, reconnect to surviving attachments/runtimes, preserve queued events and attention state, and support rollback and safe old-version cleanup.
- Refresh owned shell/extension integration deliberately and report deferred activation instead of restarting agents, terminals, or VS Code. Document the first migration from the current executable-only layout and bootstrap maintenance limits.

## Capabilities

### New Capabilities

- `application-distribution`: GitHub source publication, CI/release gates, versioned Windows installation, release discovery, explicit recoverable updates, and preservation of running sessions across version activation.

### Modified Capabilities

- `standalone-setup`: Extend independent tray lifetime and safe management to updates that restart monitoring while keeping agent sessions, terminal hosts, and their runtime managers alive.
- `session-tracking`: Extend lifecycle reconciliation across version changes, including queued events, existing authenticated runtime endpoints, and incompatible-state refusal before activation.

## Impact

- New launcher project and installation/update services; packaging must distinguish the stable entry point from versioned application payloads. Setup, CLI, tray lifetime, helper startup, diagnostics, and integration path resolution are affected.
- New GitHub workflows and release documentation; existing `global.json`, NuGet lock files, .NET tests, JavaScript tests, formatting script, and packaging checks remain inputs.
- Add isolated launcher, compatibility, rollback, mixed-version, and packaged process-lifetime acceptance coverage. First adoption may require one controlled maintenance window; future compatible updates must not require stopping active sessions.
- WorkLog supplies design references only. Its requested handoff is a separate documentation deliverable, with no WorkLog implementation, shared runtime, or project dependency. The existing session-title change and its remaining live evidence tasks remain separate.
