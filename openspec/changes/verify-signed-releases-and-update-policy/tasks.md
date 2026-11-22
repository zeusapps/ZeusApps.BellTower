# Tasks

## 1. Trust contract and implementation choice

- [ ] 1.1 Verify the fast ingress packaging/compatibility predecessor and read current release contracts, downloader, activation and workflow boundaries; record the actual 0.1.8 hash-only baseline.
- [ ] 1.2 Select a maintained license-reviewed minisign-compatible verifier using current primary documentation and upstream vectors; deliver dependency/format rationale and successful fixture-key validation without custom crypto.
- [ ] 1.3 Define exact signed descriptor bytes/fields, embedded trust key IDs, authenticated rotation/revocation and signature asset naming; verify golden vectors and compatibility/version parsing.

## 2. Verification and policy

- [ ] 2.1 Verify publisher signature before trusting offers, compatibility metadata or package probes; verify unsigned, tampered, wrong/revoked/unknown-key and modified-archive cases fail without selection or session changes.
- [ ] 2.2 Bind signed metadata to version/runtime/contracts/archive identity and full helper/VSIX inventory; verify mismatches and stale/downgrade offers remain rejected by existing selection policy.
- [ ] 2.3 Add typed setup/user automatic-check controls preserving policy when omitted and using existing updates.enabled; verify disabled background network behavior, explicit-check availability and no unattended install.
- [ ] 2.4 Implement ownership/backups/restoration for explicit policy changes and preserve later user edits; verify repeat setup, partial provider removal and unchanged unrelated configuration.

## 3. Signing workflow and migration

- [ ] 3.1 Extend packaging and protected release signing to sign already-validated descriptor bytes, publish a detached signature and reverify before publication; verify fixture signing cannot access publisher secrets and untrusted PR validation cannot enter the protected signing environment.
- [ ] 3.2 Document publisher public-key fingerprint, secret provisioning and authenticated key rotation/revocation procedures; verify rotation fixtures and record actual key provisioning as an external prerequisite rather than generating production keys in tests.
- [ ] 3.3 Document/test the first signed-release migration from actual 0.1.8 and its unsigned bootstrap boundary, including manual independent verification; verify subsequent signature-capable clients reject unsigned offers.
- [ ] 3.4 Run isolated versioned upgrade/rollback and old runtime/ingress coexistence tests; verify active Claude/Codex processes and data survive and stable bootstrap/immutable helper images are not overwritten.

## 4. Acceptance and release handoff

- [ ] 4.1 Run signature/policy tests with disposable keys plus existing package/distribution/update acceptance; verify no test uses a production key or feed write.
- [ ] 4.2 Update release/install/managed-policy docs and hand off signing prerequisites and verified asset contract to final release acceptance; verify formatting, Release build and relevant setup/distribution tests.


## 5. Provider parity acceptance

- [ ] 5.1 Implement and verify this feature for both Claude Code and Codex using shared policy and provider adapters; run matched provider fixtures plus a mixed-session case and retain version/mode-specific evidence.
- [ ] 5.2 Audit setup, defaults, configuration, diagnostics and controls for equal feature availability; document any hard provider restriction with its proof, honest shared fallback and regression check, and treat missing implementation or unverified capability as incomplete work.
