# Design

## Context

0.1.8 publishes ZIP, SHA-256 and .release.json; ReleasePackage verifies hashes/contracts but has no independent publisher signature. Updates are already explicit-install only; UpdateSettings.Enabled defaults true and disables background discovery when false. Setup has no dedicated exposed policy option. The immutable layout and stable launcher retain active sessions through version leases. The new shared provider ingress adds an owned immutable helper that must be covered by the signed package inventory.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `speed-up-claude-hook-ingestion`.

**Primary source boundaries:** Distribution release contracts and metadata; Updates/ReleaseClient, ReleaseOffer, ReleasePackage and UpdateTransaction; setup/config; scripts/package.ps1, GitHub release workflow, signature fixtures and upgrade acceptance.

## Goals / Non-Goals

**Goals:** Authenticate official release identity and expose managed-machine discovery controls while retaining current safe activation, rollback and agent lifetime.

**Non-Goals:** Do not invent custom cryptography, store private publisher keys in the repo, enable unattended installation, silently replace the stable bootstrap, or claim old 0.1.8 clients verify new signatures.

## Decisions

### 1. Detached minisign-compatible signed descriptor

Use a maintained, license-reviewed minisign-compatible verifier for detached signatures over the exact bytes of the public .release.json descriptor. The descriptor binds archive filename/hash, version, runtime and all compatibility contracts; the package inventory also covers VSIX and ingress/helper bytes. A detached .minisig plus embedded trusted public key authenticates the offer before compatibility/activation. Preserve SHA-256 as the content-integrity check. Verify real upstream format vectors and current documentation before choosing a .NET package; do not implement Ed25519/minisign parsing ad hoc. A missing/invalid/unsupported/wrong-key signature fails closed without selecting a payload.

### 2. Trust root and rotation

Embed trusted key IDs/public keys in the verifier build. Release metadata cannot self-authorize an arbitrary replacement key. Rotation requires a descriptor/rotation statement authenticated by an already-trusted key, a documented validity/revocation policy and a tested transition window; removing compromised keys requires a trusted client update or separately verified maintenance path. Keep publisher private key provisioning as an external release prerequisite with a public fingerprint record. Use disposable keys for fixtures and no real signing secret in PR validation.

### 3. Sign validated bytes without rebuilding

Extend package and release workflows so unprivileged validation produces the exact tested ZIP/descriptor, a protected signing step authenticates those bytes, verification rechecks the signature/hash before publishing and public assets include the detached signature. Restrict secrets/job permissions and do not execute untrusted PR code in the signing environment. Update release contracts/probes and downloader bounds/asset names. New clients authenticate descriptors before trusting them or downloading/executing untrusted probe binaries.

### 4. First signed release and rollback

An unsigned 0.1.8 updater cannot retroactively authenticate the verifier it downloads. Document an initial manual verification path using the published independent public-key fingerprint and an approved verifier; record the trust boundary if a user chooses the old updater route. After the new verifier is active, unsigned offers fail. Preserve old agent/runtime managers and data, and define which older payloads remain trusted rollback targets via their already-verified provenance. Do not overwrite stable bootstrap or immutable ingress images as an ordinary update.

### 5. Expose existing discovery policy

Add a typed setup option, provisionally --update-checks enabled|disabled, plus clear status/user controls. Omitted means preserve current setting; an explicit choice applies only to the selected data directory and is previewed/backed up/owned. Disabled suppresses automatic requests/notices but explicit checks/install commands remain available under signature enforcement. Removal restores the unchanged owned prior setting and preserves later user edits. Clarify UI text says automatic checks: no unattended application update feature currently exists.

## Risks / Trade-offs

- [Signing key is not provisioned] -> Complete fixture verification and document the external provisioning prerequisite; do not publish an unsigned substitute as the signed release.
- [Initial trust supplied by the same compromised channel] -> Explain the bootstrap limit and document independent fingerprint verification.
- [Signature enforcement breaks older clients/feed] -> Preserve published 0.1.8 assets, version descriptor/signature expectations and test the real baseline upgrade.

## Migration Plan

Generate/review disposable format vectors first, add verifier and descriptor contract, expose policy controls, wire protected signing for the release candidate and document trusted key fingerprint. Test baseline upgrade/rollback without altering live installs. Actual publisher key generation/import, secret provisioning and public publication require the appropriate explicit operational authorization at execution time.

## Verification and fresh-context notes

Fixture-key tests: valid descriptor, tampered bytes, wrong/revoked/unknown key, modified ZIP with valid checksum sidecar, wrong version/runtime, signed key rotation, unsigned feed refusal, no selection change on failure, policy omission/disable/removal and no automatic network access when disabled. Packaged baseline checks verify active sessions survive and new ingress/VSIX hashes are covered. Preserve existing update acceptance and Codex runtime regression tests.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 13. Existing checks are already opt-out; this adds exposed controls and publisher authentication.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
