# Analysis report: reconcile-session-liveness-and-title-refresh / 003

## Review target

- Reviewer / role: `liveness_analysis` / code-analyzer through the current Codex session; internal service mode/version unavailable. Recorded environment: Codex CLI 0.164.0, SDK 10.0.113; operational evidence records PowerShell 7.6.6 / .NET 10.0.12.
- Date: 2026-10-23; before/after identity checks at 18:24:28 and 18:26:12 UTC.
- Scope: user-authorized unpublished local trial, QA-200 startup correction, maintained operational procedure, package provenance and isolated historical-baseline rehearsal. No production activation is performed by this reviewer.
- Baseline: `0a7445a506e5306cca515c251e1fadad38fd99de`; supplementary HEAD `64d3e94626b636ca38a3ab0c5b3789686c8fa15b`.
- Target: [review-target-003.json](review-target-003.json), 61 source/evidence paths plus 11 artifact/dependency hashes. Independently reproduced diff SHA-256 `62493f1c59e9be402efeccea2a1d85f02739453fac4da029d6590f876fb1b7e0`, using scoped Git stdout lines joined with LF plus final LF when nonempty, UTF-8 without BOM. All source and artifact hashes match before and after; no mutations.
- Changes from target 002: `Program.cs`, `CliTests.cs`, the scope manifest and two new trial procedure/notes files. All other reviewed implementation files remain unchanged, permitting explicit carry-forward of [analysis-002](analysis-002.md) and its unaffected initial coverage.
- Candidate archive SHA-256: `186bfbd47ad41659bc221f32a1b5c98f2ff0b64cc54a3f0e2ad73d31a6bdceaa`; descriptor `4d5c5c920712daa370d50bc660bd4cef60c58979c8346804cbdda70ecedd2dd5`.
- Maintained operations script SHA-256: `9095e36c5c3238bb2eb4b6d1925aed34b8b56bfdc8d24c6cdab7ad1979711dc5`.
- Actual downloaded published baseline SHA-256: `d23702de5d05c1eed5a4575d0d11b0d45267ec3aa01360b0d582ad19c6a36b67`.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Source/artifact integrity | Scoped Git diff hash; `Get-FileHash -Algorithm SHA256` for all 61 source and 11 artifact entries, before/after | Passed, zero mismatches |
| Package inventory | Opened frozen ZIP read-only, decoded `versions/0.1.9/release.json`, SHA-256 checked every declared payload entry directly from its ZIP stream | All 22 entries match; version 0.1.9, attachmentHealth 1, capture 1 |
| QA-200 correction | Inspected `Program.Main` passing `attachmentHealth: 1`; inspected two real-executable provider startup/restart tests | Actual entry point now agrees with advertised capability. QA owns independently executed corrected-package recheck; no duplicate finding |
| Script syntax | `System.Management.Automation.Language.Parser.ParseFile` against the maintained procedure | Passed, zero parse errors; script hash matches |
| Operational ownership | Source inspection of presence PID/creation/executable/data-root verification, expected-owner comparison and candidate launcher ancestry | Only the verified tray receives an exit request. Procedure contains no provider/runtime termination or generic process kill. Changed monitoring owner or selection refuses recovery |
| Synchronization and conditionality | Inspected installation gate; stopped-tray setup → state → presentation lock order; saved exact selection hashes and phase transitions | Shared locking order is coherent. Candidate copy is a new version directory, hash-verified before selection. Installation gate releases before stable launcher invocation. Existing loaded modules route through the selected candidate |
| Snapshot and rollback | Inspected private ACL check, state/backup/pointer snapshot hashes, allowlisted atomic restore targets and saved trial snapshot | Restores only monitoring snapshots and exact original selection. Health-0 checks precede old monitoring startup. Current identities, explicit overrides, config, runtime records/tokens and event queues remain in place; trial monitoring is retained aside |
| Failure phases | Inspected original-stopped/ready-to-select/selected-candidate recovery paths and conditional selection ownership | Prepared/no-selection-change recovery can restart health-0 original state. Selected-but-not-ready recovery verifies our candidate ancestry and restores the snapshot. Unrecognized state/owner or filesystem failure stops for inspection rather than guessing ownership or rolling data back silently |
| Filesystem/preservation | Inspected literal paths, separation checks, `EnsurePlainPath`, new-directory checks, existing verified package extraction and original file hash checks | No recursive deletion, original payload overwrite, stable launcher rewrite, module/profile/VSIX setup or integration refresh. Only temporary restore files are deleted. Both version payloads remain retained |
| Rehearsal evidence | Read frozen harnesses and `operational-rehearsal-queues-results.json`, `candidate-smoke/results.json`, provenance and maintained notes | Developer evidence records actual historical v0.1.8 normal and injected post-health-commit failure recovery, host survival, old bytes, current override/config/token retention, health-0 restore and queued event delivery. These are disposable fixture executions, not production observations. QA independently repeats execution |
| Existing coverage | Target comparison and prior reports | Shared lifecycle/cache/writer fixes remain unchanged. Full 371-test pass and prior transient IO failure are reported honestly in developer evidence; analyzer does not claim independent full-suite execution |

## Findings

**No new findings.** ANALYSIS-200 through ANALYSIS-299 remain unused. Existing QA-200 is not duplicated; functional closure belongs to the current QA evidence and orchestrator triage.

## Re-verification

| Finding | Current target / evidence | Result |
| --- | --- | --- |
| QA-200 | Actual entry-point argument now matches implemented and advertised health capability; matched provider real-process startup/restart regression source inspected; frozen package inventory agrees | Corrected by source analysis; independent packaged execution remains QA's assigned check |
| ANALYSIS-001 / ANALYSIS-002 | Their implementation and regression files are byte-identical to accepted target 002 | Prior independent analysis remains applicable; no new changes affect either correction |

## Assessment and limitations

The frozen package and procedure are coherent for the narrowly authorized local trial, subject to the current QA result and the orchestrator's runtime preflight/hash checks. Source version remains 0.1.8; 0.1.9 is an unpublished locally compiled fixture/package identity. Nothing in this analysis authorizes a tag, GitHub release, integration refresh or later queued feature.

Rollback intentionally restores point-in-time monitoring state while retaining trial monitoring aside. It does not merge trial-processed waits/episodes into the older reducer; the maintained notes state that boundary. Current user identity/config/runtime data and queued events are retained. The procedure is limited to the verified legacy-data/no-fast-capture setup and refuses newly enabled or pending capture-v1 rather than attempting an unreviewed downgrade.

No production install/data/process was touched by this reviewer. Only this report was written. Independent checks were limited to identities, package inventory and script parsing; operational execution, authenticated providers, user terminals/RDP and physical audio are not claimed. Production activation remains the orchestrator's separately authorized action after current QA and concrete preflight succeed.
