# Tasks

## 1. Evidence

- [x] 1.1 Probe Codex 0.164.0's Windows hook-shell selection through installed Codex with isolated `CODEX_HOME` under at least two shell environments, and check for an argument-vector hook form; verify with a retained redacted evidence record naming version, mode, environment and observed shell.
- [x] 1.2 Read-only check of the user's real configuration: whether owned Codex hook entries use the command-shell form, and whether the production data has any Codex captures; verify with a redacted summary (counts and categories only, no settings values) and record whether H-1 affects the field.
- [x] 1.3 Run the same real-shell probe for Claude 2.1.310's generated hook command; verify that the current form delivers a capture, or record the failure as in scope.

## 2. Hook command generation

- [x] 2.1 Generate Codex hook commands for the verified rule from 1.1 (prefer the argument-vector form; otherwise the proven shell-valid form), keeping direct ingress; verify with tests over paths containing spaces, apostrophes and Unicode, run through the real shells.
- [x] 2.2 Make the setup provisioning probe run through each provider's real hook shell and fail visibly when no durable capture results; verify for both clients with a failing-command fixture and a passing one.
- [x] 2.3 Measure both providers with the new hook form using the user-requested arithmetic mean(full hook elapsed) minus arithmetic mean(echo 1 elapsed) through the same provider shell; verify estimated Bell Tower added runtime stays below 300 ms, retain raw timings/distributions, and preserve durable-count and three-second raw-duration checks.

## 3. Migration and diagnostics

- [x] 3.1 Detect owned Codex entries with the broken form and offer the existing previewed explicit migration; verify that user timeout/async edits are surfaced, unrelated hooks preserved and a repeat apply is idempotent.
- [x] 3.2 Add per-provider configured/delivered/unverified hook delivery reporting to doctor and the capability matrix; verify matched Claude and Codex cases and document the doctor output change.

## 4. Out-of-order approval delivery (H-2)

- [x] 4.1 Add fixtures for both providers where an approval request is consumed before its own tool start under asynchronous delivery; verify the current behavior with a failing regression first, or record that it does not reproduce.
- [x] 4.2 If 4.1 reproduces, fix shared correlation so a late start never resolves the request; verify both reducer paths, the shuffled-delivery fixtures and mixed-session cases.

## 5. Documentation and acceptance

- [x] 5.1 Update `docs/hook-ingress.md`, `docs/installation.md`, `docs/compatibility.md` and `docs/follow-ups.md` for the verified shell rule and migration, and replace the stale "live acceptance not performed" wording (docs and doctor text) with the 2026-10-27 user-operated isolated-harness trials and their scope; verify wording against the archived evidence and run the formatter and Release build.
- [x] 5.2 Parity acceptance: on a packaged candidate, deliver Codex and Claude hook events through each provider's real runner, including a mixed simultaneous session, and confirm Codex chips appear in a user-operated trial after the migration; retain version/mode evidence separately from fixtures.

## Evidence reconciliation (2026-10-28)

Final reviewed working-tree identity: `head=aadbfb3 baseline=aadbfb3 files=22 diff=659f1706b2bc untracked=3/cce5a39616ce`. [QA 011](reports/qa-011.md) and [Analysis 009](reports/analysis-009.md) validate native cmd evidence and the final functional source; the last documentation-only correction is independently approved by [QA 012](reports/qa-012.md) and [Analysis 011](reports/analysis-011.md). Earlier Core and benchmark coverage is explicitly mapped forward. Development package 0.1.9018 remains installed with the verified hook behavior; later source changes update capability evidence wording, its test and documentation. No later package installation is implied. See [installation evidence](evidence/installation-001.md) and [both-client live acceptance](evidence/user-live-acceptance-001.md).

- Task 1.1 records the two observed PowerShell environments, rejected command-array form, and pinned selected-turn-shell rule. This does not claim every fallback or alternate handler was tested.
- Task 1.2 is based on the recorded redacted read-only field observation, reviewed as evidence rather than a repeated independent production inspection. Affected command forms were found; current production event loss was not asserted from historical counts.
- Task 2.1 is complete for the targeted Codex 0.164.0 local Windows resolver: native PowerShell delivery and exact-target verification passed, and [native probe 009](evidence/native-investigation-009.md) delivered a protocol-valid capture through the actual cmd fallback. The capture's OS-derived shell identity matched its cmd ancestor; special-character helper/data paths and literal percent text were preserved. Both final reviewers independently decoded and audited the evidence. [Source review](evidence/analysis-008-source-checks.md) proves there is no default Bash-selection branch in this precise local Windows mode, so that scenario is not applicable. External-host Bash, other versions and untested path-priority combinations remain unverified; no universal provider restriction or scope waiver is inferred.
- Task 2.3 is complete under the user-requested metric: mean(full hook elapsed) minus mean(literal `echo 1` elapsed). Fresh matched trial 002 used 121 samples per leg and concurrency eight: Claude 204.911 minus 118.628 = +86.283 ms; Codex 481.118 minus 530.764 = -49.646 ms, unclamped. Both independent reviews recomputed the raw arrays and verified the thresholds, 242 native/50 legacy events, eight sessions and replay result. The negative estimate is not negative helper processing cost. See [benchmark evidence 002](evidence/benchmark-mean-002.md). Historical raw-median failures remain valid under their original definition and are not relabeled as new passes.
- Task 5.2 is complete: the user reported both clients worked and supplied a successful Codex `echo 1` screenshot with two finished chips. Fresh Claude and Codex delivery receipts independently postdate migration. [Live acceptance](evidence/user-live-acceptance-001.md) distinguishes user observations, screenshot evidence and installed-version/receipt probes. The earlier failed [investigation 004](evidence/native-investigation-004.md) remains historical and is not relabeled as successful. No separate trust-dialog or permission-wait interaction is claimed.
- QA 011's final default parallel full suite passed 1005/1005, and its focused matrix checks passed 6/6. The only later implementation change was the one-sentence documentation correction reviewed by QA 012 and Analysis 011; no tests were needlessly repeated. Historical QA 007 results (serial 1004/1004, default parallel 1003/1004 and isolated 1/1) remain recorded. The latest pass does not prove the earlier timeout's cause or erase earlier failed checks.
