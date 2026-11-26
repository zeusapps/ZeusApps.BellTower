# Analysis report: validate-next-release-field-acceptance / ANALYSIS-016

> Saved verbatim by the orchestrator from the ANALYSIS-016 hand-back. Reviewer: code-analyzer subagent; the client refused its report write ("Subagents should return findings as text"). Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.332, subagent, auto mode / claude-opus-5-5
- Date: 2026-11-26 21:12 UTC
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-016 (final source-only review, release essentials scope cap, user decision 2026-11-26)
- Task and requirement references: tasks 3.3, 3.4, 4.3, 4.4, 5.1, 5.2, 5.3 ([tasks.md](../tasks.md)); DEV-011 [implementation-013.md](../evidence/implementation-013.md), DEV-012 [implementation-014.md](../evidence/implementation-014.md), [coverage-ledger.md](../evidence/coverage-ledger.md) Part 6
- Scope and relevant neighboring paths:
  - Reviewed:
    - `tests/BellTower.Tests/Updates/DistributionTests.cs` (the new test)
    - `Directory.Build.props`, `vscode/package.json`
    - `src/BellTower/Diagnostics/CapabilityMatrix.cs`
    - `docs/compatibility.md`, `docs/releases.md`, `docs/release-notes-0.1.11.md`
    - ledger Part 6, implementation-013 and implementation-014
  - Neighbors read:
    - `src/BellTower/Updates/UpdateTransaction.cs`, `ReleasePackage.cs`, `ReleaseOffer.cs`
    - `src/BellTower.Distribution/CaptureCompatibility.cs`, `AttachmentHealthCompatibility.cs`, `ReleaseContract.cs`
    - `src/BellTower/IO/StateStore.cs`, `src/BellTower/Core/CheckoutPolicy.cs`, `src/BellTower/Diagnostics/HookIngressImageReporter.cs`
    - `tests/BellTower.Tests/Updates/AttachmentHealthCompatibilityTests.cs`, `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`, `tests/BellTower.Tests/Sessions/CheckoutResolutionTests.cs`
    - `scripts/package.ps1`, `.github/workflows/release.yml`, `docs/handoffs/next-release-plan.md`
    - Git tags
- Baseline commit: 02d4f56
- Implementation identity (before): `head=02d4f56 baseline=02d4f56 files=109 diff=eccfcee8d1b7 untracked=85/c7d2da3be69a`
- Before/after identity: after: `head=02d4f56 baseline=02d4f56 files=109 diff=eccfcee8d1b7 untracked=85/c7d2da3be69a`. Identical, and identical to the assigned target. No unexpected mutation.
- Prior reports / rechecked findings: none rechecked (a new review, not a recheck)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 3.3 test proves the proof map | Traced `UpdateTransaction.Stage` preconditions against the new test's fixture | `Stage` runs `CaptureCompatibility.Ensure` (line 42) before `ReleasePackage.Extract` (line 52). The fixture's contract has `AttachmentHealth = 0` and the drained state has `attachmentHealthVersion: 1` | ANALYSIS-301 |
| 4.4 versions | Diff, grep of `*.props`, `*.csproj`, `package.json`, `scripts/package.ps1` | `Directory.Build.props` and `vscode/package.json` are both 0.1.11. No other declaration. `package.ps1` line 109 rejects a mismatch for a release build. No `vscode/package-lock.json` | Correct |
| Release notes against source and tags | `git tag --contains` for the identity commits; read of the reporter, workflow and handoff | `1e63f85` (session titles) and `4db8dda` (badge identity) are in `v0.1.9`. The doctor advice string matches `HookIngressImageReporter.StaleAdvice`. The workflow uses `--generate-notes`, as `docs/releases.md` says. The 0.1.10 rationale matches implementation-001 | ANALYSIS-302. Everything else checked is accurate |
| 5.3 matrix rows and doc agreement | Read the 7 new rows in source and doc; read `CapabilityMatrixTests` (status words, restriction wording, doc row presence) | `Open` maps to `unverified`. Doc cells start with `unverified` or `supported` to match. No row is `restricted`. The unverified details use only the "not a proven restriction" form that the test strips | Correct |
| 5.1 shared policy | Read `CheckoutPolicy.Choose`, the `IdentityResolver` diff and the cited tests | No provider parameter or branch. The branch-change test is a `claude`/`codex` theory. Mixed retention is shown in H2.2 `mixed/branch-change-keeps-working-checkout` | The parity claim holds. One citation is imprecise (see known limitations) |
| 5.2 parity audit | Read the new rows and the release notes for unequal items | The Codex native-name gap and ANALYSIS-240, -265 and -242 are all labeled unfinished or unverified, none restricted | Correct |
| 4.3 ledger Part 6 | Read against the tasks, Part 5 and the handoff | The F13 row repeats the 3.3 proof claim (see ANALYSIS-301). All three Part 5 changes are visible in `docs/handoffs/next-release-plan.md` | See ANALYSIS-301 and known limitations |
| Build, tests, harness | not run | Source-only assignment; the harness is excluded by the assignment | not performed |

## Findings

ANALYSIS-300 is not used. That ID already names the archived restore-claude-badge-identity fork parity gap, which this change cites in task 6.4, `docs/compatibility.md`, `CapabilityMatrix.cs` and the release notes. Findings start at ANALYSIS-301.

### ANALYSIS-301: The new Stage test passes on the attachment-health gate, not on the sidecar or modified-archive check

- Severity: warning
- Related task / requirement: 3.3. Implementation-013 proof map rows "Modified archive rejected", "Mismatched sidecar rejected" and "No selection or session change". Ledger Part 6 F13 row. Matrix row "Publisher-signed updates" ("SHA-256-verified (DistributionTests)").
- Locations:
  - `tests/BellTower.Tests/Updates/DistributionTests.cs` lines 104-139 (`ModifiedMismatchedAndInvalidOffersAreRejectedWithoutChangingSelectionOrSessions`)
  - `src/BellTower/Updates/UpdateTransaction.cs` lines 36-52
  - `src/BellTower.Distribution/AttachmentHealthCompatibility.cs` lines 29-32
  - `src/BellTower/IO/StateStore.cs` lines 102-105
  - `DistributionTests.AddPayload`, which builds `new ReleaseContract(version, Files: files)`, so `AttachmentHealth = 0`
- Observation:
  - The test publishes `ConfirmedPermission` and drains. Because there is a session, `StateStore.Drain` writes `attachmentHealthVersion = 1`.
  - The candidate contract comes from `AddPayload` and has the default `AttachmentHealth = 0`.
  - `Stage` calls `CaptureCompatibility.Ensure(paths.Root, offer.Contract.Capture, offer.Contract.AttachmentHealth)` at line 42. That calls `AttachmentHealthCompatibility.Ensure(..., 0)`, which throws `InvalidDataException("Attachment health requires a health-v1 consumer ...")`. This happens before `ReleasePackage.Extract` runs at line 52.
  - `AttachmentHealthCompatibilityTests.CommittedHealthRequirementRejectsLegacySelectionWithoutChangingSessionsOrInstallation` asserts exactly this rejection. It uses the same setup and a contract whose `AttachmentHealth` is 0.
  - So the sidecar-mismatch case and the modified-archive case both throw the health-v1 error. Their `Assert.Throws<InvalidDataException>` assertions pass without ever reaching `Release package checksum mismatch.` or `Checksum file mismatch.`.
  - Only the `not-a-digest` case is a real proof: `EnsureStable` calls `Validate`, which throws `Invalid release hash.`.
  - No other test covers the sidecar comparison. Despite its name, `SafeExtractionChecksSidecarAndDoesNotOverwriteInstalledSelection` only uses a wrong offer hash.
  - The production checks exist (`ReleasePackage.cs` lines 12-16), and the packaged smoke run exercised a valid sidecar. But the proof map's "gap closed" claim for 3.3 does not hold for two of the three cases. The F13 ledger row and the matrix row repeat the same claim.
- Proposed improvement and benefit:
  - Make the contract a health-v1 consumer. Either write the candidate `release.json` with `AttachmentHealth: 1`, or build the offer from `contract with { AttachmentHealth = 1 }` and repackage.
  - Assert each exception's message (`Checksum file mismatch.`, `Invalid release hash.`, `Release package checksum mismatch.`), so an earlier gate cannot satisfy the assertion.
  - Optionally add a fourth assertion: the unmodified, matching offer gets past `Extract`. For example, assert that the only failure comes from `Probe` of the fixture executable, or call `ReleasePackage.Extract` directly.
  - If the test changes, correct implementation-013 and the F13 row.
- Behavior-preservation risks: None to product code; the change is test-only. The selection and state byte checks stay valid.
- Evidence type: Source observation. An existing test asserts the same gate on an identical setup.
- Verification / measurement plan: Run only the corrected test and confirm that each `Assert.Throws` message matches the intended check. As a sanity check, remove the sidecar comparison in a scratch build (no commit) and confirm that the test then fails.
- Relationship: new

### ANALYSIS-302: The release notes list session titles and badge identity as new since 0.1.9

- Severity: warning
- Related task / requirement: 4.4 (release notes)
- Locations: `docs/release-notes-0.1.11.md` line 12, section "What is new since 0.1.9", second bullet ("... session titles and badge identity ...")
- Observation:
  - `git tag --contains 1e63f85` (follow-openspec-session-titles) and `git tag --contains 4db8dda` (restore-claude-badge-identity) both list `v0.1.9`.
  - Ledger Part 2 records both changes as "0.1.9 (implementing commit ... precedes the tag)". Its baseline note says "Only distinct-session-colors ... and the changes marked 'after 0.1.9' are new in the candidate".
  - `docs/handoffs/next-release-plan.md` line 17 lists badge identity among the 0.1.9 contents.
  - The only title work after 0.1.9 is the opt-in titles and colors for adopted native sessions (`dc7eaac`, `4ce7ecb`). That work belongs to native adoption, which is already a separate item in the same bullet.
  - The release notes therefore present two features that shipped in 0.1.9 as new.
- Proposed improvement and benefit: Remove "session titles and badge identity" from the list. To name the adopted-session titles, write "opt-in titles and colors for adopted native sessions". Users then won't expect a change they already have.
- Behavior-preservation risks: None; text only.
- Evidence type: Source observation (Git tag history, ledger, handoff)
- Verification / measurement plan: Re-read the bullet against ledger Part 2. Each listed feature should map to a predecessor marked "after 0.1.9", to adopt-native-sessions-from-hooks or to distinct-session-colors.
- Relationship: new

## Re-verification

None assigned.

## Assessment and limitations

Scope covered: everything in the assigned scope list, judged only for release correctness. Two findings: ANALYSIS-301 (test proof) and ANALYSIS-302 (release-note claim). The rest checks out:

- **Versions:** the declarations are correct, and the packaging script enforces that they agree.
- **Matrix:** the seven new rows agree between source and `docs/compatibility.md`. They keep the status words that the agreement test reads, and none claims a restriction.
- **5.1:** the claim holds. There is one shared `CheckoutPolicy`/`IdentityResolver` path with no provider branch, a `claude`/`codex` theory, and mixed retention in H2.2.
- **5.2:** the claim holds. Every unequal item is labeled unfinished or unverified.
- **Other release-note statements:** accurate. This covers the doctor `stale` advice, the 0.1.10 rationale, the `--generate-notes` workflow, ANALYSIS-186, -240 and -265, deferred signing and the anchor link.

Known limitations (polish, not findings):

- The branch-change matrix row (`CapabilityMatrix.cs` line 130 and `docs/compatibility.md`) and implementation-014 section 5.1 cite `MixedSessionsInOneLaunchDirectoryResolveIndependentlyWithoutCrossReuse` as the mixed case. That test predates this change and has no branch change or retained checkout. H2.2 `mixed/branch-change-keeps-working-checkout` proves mixed retention, so the claim holds, but the unit test citation is imprecise.
- Ledger Part 6 says every Part 5 change is listed "in release-notes-0.1.11.md or the capability matrix". `verify-codex-repository-subagents` appears in neither, only in `docs/handoffs/next-release-plan.md`. That still meets Part 5's own "release handoff" wording.
- Release notes step 1 ("extract it and let the tray update, or run `update`") mixes the manual extraction path with the in-place update path.
- `SafeExtractionChecksSidecarAndDoesNotOverwriteInstalledSelection` does not exercise the sidecar, despite its name (related to ANALYSIS-301).

Not performed: build, tests, format verification and the harness (source-only assignment). Unexpected writes: none; the report write was refused, so the report is inline here. Decision needed: the reserved range starts at ANALYSIS-300, which collides with the existing archived ID that this change cites. The orchestrator may want to reserve ranges that avoid IDs already cited in the change.
