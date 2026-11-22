# Release publisher trust

## Purpose

Authenticate official release metadata independently of download hashes while retaining safe versioned updates and an explicit trust bootstrap for existing installations.

## ADDED Requirements

### Requirement: Publisher-authenticated release metadata
A signature-capable Bell Tower client SHALL verify an authenticated release descriptor against an embedded trusted publisher key before accepting update metadata or activating a package. The signed descriptor SHALL bind version, runtime, compatibility contracts, archive identity and archive SHA-256. Missing, tampered, wrong-key, revoked or unsupported signatures SHALL fail closed.

#### Scenario: Valid release
- **WHEN** the descriptor signature, trusted key, archive hash and version/contracts agree
- **THEN** the package can proceed through existing compatibility and activation checks

#### Scenario: Tampered or unsigned offer
- **WHEN** metadata is altered or a required signature is absent
- **THEN** the client rejects the offer without changing selection or running sessions

#### Scenario: Hash valid but publisher wrong
- **WHEN** an archive and checksum agree but the descriptor is signed by an untrusted key
- **THEN** the release is rejected

### Requirement: Signing key lifecycle
Release signing SHALL use a detached publisher-key scheme with documented key identifiers, authenticated rotation and revocation. Validation jobs SHALL use disposable fixture keys; publisher private keys SHALL remain in a protected signing environment and SHALL NOT be embedded in binaries, repository files or validation artifacts.

#### Scenario: Authorized key rotation
- **WHEN** a currently trusted key authenticates a new key under the documented policy
- **THEN** the client can accept the new key without trusting keys supplied solely by an unsigned feed

#### Scenario: Unknown key
- **WHEN** an offer advertises a new key without authenticated rotation
- **THEN** the updater refuses it and reports the trust issue

### Requirement: Unsigned baseline migration transparency
The first signed release SHALL document how an existing unsigned 0.1.8 installation obtains its initial trusted verifier. The migration SHALL preserve running agents and data and SHALL distinguish manual signature verification from the guarantees of the old hash-only updater.

#### Scenario: Upgrade from 0.1.8
- **WHEN** the baseline client installs the first signature-capable version in an isolated trial
- **THEN** the record states which checks the old client actually performed and verifies that subsequent offers enforce signatures


### Requirement: Provider parity for release trust and update policy in release-trust
This feature SHALL be available for both Claude Code and Codex by default through shared product policy, configuration meaning and controls, with provider adapters for supported mechanisms. Any exception SHALL identify the exact feature, provider version and mode, verified hard restriction, evidence and honest fallback. Missing implementation or verification SHALL NOT count as a restriction.

#### Scenario: Matched provider capability
- **WHEN** equivalent supported feature inputs occur in either client or mixed simultaneous sessions
- **THEN** the common policy produces equivalent user-facing behavior and independent per-conversation state

#### Scenario: Claimed provider restriction
- **WHEN** an adapter cannot deliver an operation
- **THEN** its version/mode evidence and fallback are recorded, or the feature remains incomplete for release rather than silently excluded
