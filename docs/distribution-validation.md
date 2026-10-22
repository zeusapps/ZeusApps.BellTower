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

The public repository was created with main tracking origin/main. [Initial remote Windows validation](https://github.com/zeusapps/ZeusApps.BellTower/actions/runs/39765936367) passed on commit 23172350a1468e30b52b4eb9b5b8f97f038a352b, including all tests, formatting, packaging, updater acceptance, and artifact upload. The immutable v0.1.8 tag targets that validated commit. [Tagged validation and publication](https://github.com/zeusapps/ZeusApps.BellTower/actions/runs/39766500039) both passed and published [release v0.1.8](https://github.com/zeusapps/ZeusApps.BellTower/releases/tag/v0.1.8).

All three public assets were independently downloaded after publication. The ZIP's SHA-256 matched its checksum and release metadata:

~~~text
d23702de5d05c1eed5a4575d0d11b0d45267ec3aa01360b0d582ad19c6a36b67
~~~

Every payload file matched its manifest hash. The extracted root launcher reported 0.1.8; the read-only distribution probe reported supported layout/bootstrap/state contracts. An explicit update --check against isolated data successfully read the public GitHub feed and reported running/selected 0.1.8 with no newer compatible release. Offline update --status reported no prior outcome as expected for the new installation. The public latest-release endpoint returned stable v0.1.8 with exactly the ZIP, checksum, and release-metadata assets. Evidence is retained under artifacts/published-verification-0c9869dd77964f14a7a8b2a2f651d499.

The user's active legacy installation was not upgraded or replaced. Its one-time migration remains a separate explicit operational action documented in releases.md. Existing session-title/provider live-acceptance gaps remain separate.
