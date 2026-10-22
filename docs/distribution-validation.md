# Distribution validation

Change: publish-github-with-session-safe-updates. Intended first versioned release: 0.1.8. Validation date: 2026-10-22.

## Local implementation evidence

- Release build: zero warnings/errors with SDK 10.0.113.
- Full .NET suite: 296 passed, zero failed/skipped. The updater tests cover contracts, unknown shared fields, stable path resolution, package checksum/sidecar agreement, traversal rejection, version leases and cleanup, thread-coordinated installation gates, recovery retention, shared discovery attempts, explicit disabled-check behavior, rate-limit backoff, and owned integration preservation.
- VS Code bridge: all 8 Node tests passed with Node 24.13.0.
- Formatting verification passed; actionlint 1.7.12 and strict OpenSpec validation passed.
- Explicit mismatched package -Version is rejected before packaging. Ordinary packaging verifies application/extension agreement and launcher --version.
- Packaged acceptance passed a distinct compiled fixture 0.1.7 -> 0.1.8 transition with a real dedicated runtime-manager process, authenticated fixture backend, live fake agent/launch process, reconnecting tray, hook delivery, native-name ownership, viewed/dismissed episodes, custom data directory, and stopped-tray activation. Injecting missing candidate metadata after selection restored the prior selection and tray while preserving data.

The initial baseline is this implementation compiled separately as fixture 0.1.7. It establishes the new layout and version-transition/process contracts; it does not establish hot migration from the old executable-only release. Subsequent CI releases use the previous public stable ZIP as baseline. Fixtures use isolated hidden Windows consoles and a local fake provider; no authenticated model request, real client reconfiguration, production upgrade, physical sound, or user-session termination is part of these checks.

## Publication checks

The authenticated GitHub account was verified as zeusapps; the planned repository did not exist. Outgoing tracked files/history and common credential patterns were reviewed; generated packages, runtime state, private backups, test results, and tooling remain ignored. The publication plan uses public zeusapps/ZeusApps.BellTower and main without rewriting history. Official current action revisions were resolved and pinned, and their artifact/setup inputs were checked.

Clean checkout of implementation commit ed16b9f passed locked restore with normal vulnerability auditing enabled, warning-free Release build, all 296 .NET tests, all 8 bridge tests, formatting, and scripts/update-acceptance.ps1. That acceptance also asserted literal/Unicode argument forwarding, inherited working directory/environment, child exit status, an event queued while the tray was stopped, racing tray starts, overlapping status reads, superseded update refusal, active-version cleanup retention, and rollback. Fixture evidence is retained locally under artifacts/clean-checkout/artifacts/update-acceptance/run-ebae0354ed734424aff217c3ef921ab8.

Observed remote workflows and independent published-asset verification remain pending until actual evidence is added below. Existing session-title/provider live-acceptance gaps remain separate.
