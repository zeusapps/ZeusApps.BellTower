# Tasks

## 1. Contract and version policy

- [x] 1.1 Add optional `build` metadata (channel, commit, dirty, builtAt, baseVersion) to the release contract and a shared dev-range helper (PATCH ≥ 9000); verify unit tests round-trip the field, older-shaped `release.json` without it still validates, and the helper classifies 0.1.8 / 0.1.10 / 0.1.9001 correctly.
- [x] 1.2 Add the shared "effective installed version" (dev selection → baseVersion; unmarked dev-range → MAJOR.MINOR.0) and use it in update discovery, `update --check` and `UpdateTransaction.Stage`; verify tests: dev 0.1.9001 (base 0.1.8) is offered and stages stable 0.1.9, a stable selection keeps today's superseded rule, and `previous` may be a dev version.

## 2. Visible dev identity

- [x] 2.1 Add one shared version display formatter (contract first, informational version fallback, "dev (unmarked)" for a dev-range payload without metadata) and use it in the tray tooltip, About/Sessions view, `doctor`, `update --status` and CLI version output; verify tests cover dev clean, dev dirty, unmarked and stable, and a stable build shows no dev label.

## 3. Packaging

- [x] 3.1 Make `scripts/package.ps1` locate `vswhere.exe` itself and fail with a clear Build Tools / Windows SDK message when absent; verify a package build succeeds in a plain shell without the Visual Studio Installer folder on PATH.
- [x] 3.2 Add `package.ps1 -Dev` (optional `-DevVersion`): output under `artifacts/dev-builds/`, next free dev number above both that folder and the default installation's `versions/`, `build` metadata from git (commit, dirty) and build time, `-p:InformationalVersion`; stable mode rejects the dev range; fixture mode unchanged; verify script tests for number selection, range enforcement both ways and descriptor contents, plus one real dev package build.

## 4. Dev installation

- [x] 4.1 Create `scripts/install-dev.ps1` (Preflight/Switch/Rollback) generalized from the archived trial script per design decision 4: versions from selection and package, compatibility checks from the candidate contract (attachment health, capture), dev-range/marker requirement, private ACL workspace, verified tray owner, gates and locks, snapshots, original-bytes assertions, stale-build guard; verify script tests against a disposable installation and data directory under `artifacts/` with a fake tray presence, covering preflight no-mutation, switch, rollback, refused owner/selection drift and refused non-dev candidate.
- [x] 4.2 Add `Retire` using the tray cleanup's retention rule (selected, previous, recovery-retained and leased payloads refused) and removing only dev-marked payloads (unmarked and stable payloads always refused; amended 2026-10-24 per QA-001/ANALYSIS-001); verify script tests for each refusal and a successful retire on a disposable installation.
- [x] 4.3 Troubleshoot hook timeouts and eliminate slow launcher-gate work from Switch/Rollback and automatic Cleanup/Retire per design decision 7 (user request 2026-10-24, ANALYSIS-006). Preserve stable launcher bytes, short safe selection/lease sections, update serialization and pending-update refusal. Verify deterministic delayed-phase concurrent generated hooks for Claude Code, Codex and mixed sessions complete while slow work remains blocked; retain leased payloads and all existing safety checks; record baseline mechanism reproduction separately from unverified field causation.

## 5. Skill and docs

- [x] 5.1 Add byte-identical `install-dev-build` skills at `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md` (build, preflight, switch, verify, rollback, retire, optional setup preview; authorization and safety rules) and a repository test that fails when the copies differ; verify the test passes and fails on a deliberate one-byte difference in a temporary copy.
- [x] 5.2 Document the dev channel, reserved range, effective-version update rule, install/rollback/retire procedure and legacy 0.1.9/0.1.10 cleanup in `docs/development.md` and `docs/releases.md` with a pointer from `docs/installation.md`; verify links resolve and the text matches script parameters.

## 6. Acceptance

- [x] 6.1 Run Release build, full tests, format verify and JavaScript tests; build a real dev package and run `install-dev.ps1` Preflight against the maintainer installation (read-only) and the full Switch/Rollback/Retire cycle against a disposable installation; record evidence. A live Switch on the maintainer installation is a separate user-authorized step after review.
- [x] 6.2 Verify Claude Code and Codex parity: both clients discover the skill and describe the same procedure; record client versions and evidence.
- [x] 6.3 Run the existing packaged NativeAOT ingress benchmark on the final dev package with disposable data/settings; record neutral responses, durable counts, per-provider median/p95/p99/max, the below-three-second samples and below-300-ms median gate. Include mixed capture during slow installation/cleanup trials and distinguish generated-shell evidence from authenticated field behavior.
