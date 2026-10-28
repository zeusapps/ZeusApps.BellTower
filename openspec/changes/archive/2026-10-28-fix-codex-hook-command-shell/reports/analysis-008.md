# Analysis report: fix-codex-hook-command-shell / 008

## Review target

- Reviewer / role: analyze_fallback_008 / code-analyzer.
- Client / version / mode / model (resolved ID): Codex host version unavailable; reviewed native target Codex CLI 0.164.0; delegated Default-mode evidence review; assigned `gpt-6.1-sol`, high effort. Runtime model self-inspection is unavailable.
- Date: 2026-10-28 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`; independent Analysis008 of remaining task 2.1 / QA-003, new fallback investigation, and acceptance applicability, including orchestrator-requested late QA008 evidence-label review. New allocation ANALYSIS-350 through ANALYSIS-399; no suggestions authorized.
- Task and requirement references: [tasks](../tasks.md) 2.1 and 5.2, [design](../design.md) decisions 2–3, [setup requirement](../specs/standalone-setup/spec.md), [session delta](../specs/session-tracking/spec.md), and orchestrator-owned [ledger](findings.md).
- Scope and relevant neighboring paths: investigations 005/006/007, live acceptance 001, pinned Codex shell/session/environment/MXC source, prior 007 reports and benchmark 005. Product source, tests, plans, ledger, settings and installation are outside write ownership.
- Baseline commit / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior reports / rechecked findings: [Analysis007](analysis-007.md), [QA007](qa-007.md); QA-003 remaining acceptance only. No new implementation recheck.

Implementation identity command before and after:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests docs/compatibility.md docs/follow-ups.md docs/hook-ingress.md docs/installation.md tools/BellTower.Ingress.Bench
```

Manifest (same as Analysis007):

```text
? src/BellTower/Diagnostics/HookDeliveryReporter.cs
? src/BellTower/Setup/ProviderHookProbe.cs
? tests/BellTower.Tests/Diagnostics/HookDeliveryReporterTests.cs
M docs/compatibility.md
M docs/follow-ups.md
M docs/hook-ingress.md
M docs/installation.md
M src/BellTower/Core/SessionReducer.cs
M src/BellTower/Diagnostics/CapabilityMatrix.cs
M src/BellTower/Diagnostics/DiagnosticsReporter.cs
M src/BellTower/Setup/HookCommandBuilder.cs
M src/BellTower/Setup/IngressProvision.cs
M src/BellTower/Setup/IntegrationOwnership.cs
M src/BellTower/Setup/IntegrationSetup.cs
M tests/BellTower.Tests/Core/PermissionNotificationTests.cs
M tests/BellTower.Tests/Core/ReducerTests.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Providers/CodexPermissionTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tests/BellTower.Tests/Setup/ProjectProfileTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `959d86f7d758195d9f52f47110ae24ea8d4f5a0fde610a8ee7ce89d6da550da3`.
- Untracked SHA-256: reporter `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`; probe `3e6a758b76fe766d46b142ea9d9adb0c4a81ce6d4beecff907c24a0919657a6e`; reporter tests `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758 untracked=3/cce5a39616ce`; matched. Assigned review outputs are outside the product identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Evidence integrity | `Get-FileHash -Algorithm SHA256` before/after for investigations 005/006/007 | All three full hashes in [source checks](../evidence/analysis-008-source-checks.md) match handoff | Passed |
| Local shell applicability | Reopen official `rust-v0.164.0` request, session, local/remote resolution and shell-discovery source | Independent trace and primary-source links in source checks | Passed source review: normal local Windows default is PS-or-cmd; its Bash selection test is narrowly not applicable, not a universal restriction |
| Public versus external shell input | Inspect pinned request shapes and executor shell conversion | Requests have no shell input; remote environment metadata can carry Bash/cmd | Passed bounded claim review; external-host Bash remains unverified |
| MXC safety and plan | Read pinned policy/native/debug launcher; inspect retained scratch config without dumping it | Denies, DACL-mutation prohibition, isolated profile, bounded identity-based lifecycle and disabled network agree with evidence | Passed static review; no actual native child launched by analyzer |
| MXC runtime result | Review investigation007; check expected marker absent | Both recorded attempts failed before a visibility result; no marker or hook capture | Failed controls in prior evidence; exact cause unestablished. This is not native cmd coverage |
| Late no-deny baseline classification | Read newly returned QA008 and baseline evidence at orchestrator request | Exit1, marker absent, syntax stderr establish whole-control failure; no independent child-creation observation | Failed evidence inference; ANALYSIS-350 below |
| User acceptance mapping | Compare task/spec acceptance with live acceptance001 and current ledger | User reports both clients worked; screenshot and fresh receipts support migrated setup only | Passed task5.2 evidence review; no inferred trust, permission-wait or fallback result |
| Architecture / duplication / complexity | Inspect evidence claims against setup responsibility and shared parity policy | New work adds investigation only; no duplicated product policy or production refactor | No new finding; source correctness scope only |
| Performance | Map [Analysis005](analysis-005.md), [metric checks](../evidence/analysis-005-metric-checks.md) through unchanged benchmark source | Revised mean-difference evidence retains +86.283 ms Claude / -49.646 ms Codex and original guards | Carried forward, not measured again; no 0.1.9018 latency claim |
| Regression | Map QA007 / Analysis007 to unchanged product identity | Native setup4/4, serial1004/1004, parallel1003/1004 and isolated timeout case1/1 remain distinct | Prior passes/failure retained; not rerun |
| Identity / hygiene | Identity before/after; `git diff --check`; targeted scan of owned report/evidence | Same product identity, no whitespace errors or targeted private/secret patterns | Passed |
| Product build / full suite / native probes | Not run: evidence-only target with unchanged source | QA has separate bounded control assignment | Not performed; avoids duplicating unaffected acceptance |

## Findings

### ANALYSIS-350: No-deny control does not establish an executor child-startup failure

- Severity: warning.
- Related task / requirement: task2.1 / QA-003 evidence correctness; source-backed safe fallback investigation.
- Locations: `openspec/changes/fix-codex-hook-command-shell/reports/qa-008.md:40` at report SHA-256 `3EC5AE05FDFF11BD7E5805B208E89A8070E8D70E008FE0F70431F8F5B04B4ED8`; supporting `evidence/qa-008-mxc-baseline.md:17` at SHA-256 `4DF12A24F23C2023AC7F1155EFC28408BC1CACB89D78E87E2C6A061F09EDCC57`.
- Observation: the no-deny baseline records syntax stderr, exit1 and no redirected marker. QA008 infers a general executor MXC child-startup problem. The evidence cannot distinguish failed child creation from a cmd process that started and rejected the marker command or redirection. Investigation007's explicit CreateProcessW error5 is separate evidence with a different failure boundary.
- Proposed improvement and benefit: reconcile in a fresh record that the baseline failed and child creation is inconclusive, then bind any launch conclusion to a quote-free stdout control. Preserve immutable QA008. This prevents an unsupported runtime diagnosis from driving the remaining coverage decision.
- Behavior-preservation risks: no product change required. Do not relabel either attempt as native fallback coverage, infer a shared cause, or weaken acceptance; retain failed controls.
- Evidence type: evidence/source observation; no reproduction or quoting-defect claim.
- Verification / measurement plan: one isolated unchanged-executable no-deny control with child argv `cmd.exe /d /c echo MXC_BASELINE_OK`, bounded lifetime and captured stdout/stderr. Correct stdout proves this baseline can launch cmd; failure must retain its exact sanitized error and cannot by itself prove a provider restriction. The orchestrator is handling this bounded control separately.
- Relationship: new evidence-label finding, not a duplicate product defect or a closure of QA-003.

No other findings or suggestions. Investigation005's inaccurate WSL count is explicitly corrected in investigation006; it does not alter the acceptance conclusion.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-003 / task2.1 | Frozen product identity and investigations005–007 | Native cmd branch exists but no native selection/capture; failed MXC control and direct-shell checks cannot substitute | Still failing acceptance gate; no restriction exception or closure |
| QA-003 / task5.2 | Installed0.1.9018 user-operated migrated setup | Live acceptance001 distinguishes user observation, screenshot, version probe and fresh provider receipts | Evidence supports recorded task5.2 acceptance; orchestrator owns ledger transition |
| ANALYSIS-250 / ANALYSIS-251 | Unchanged product identity | Analysis007 and QA007 correction checks | Carried forward, not re-executed |
| ANALYSIS-001 / 002 / 003 / 050 | Unchanged product identity | Prior explicit mapping in Analysis007 / QA007 | Carried forward with original scope; no new acceptance implication |

## Assessment and limitations

The new investigation is sound when read as source-backed route exploration plus failed runtime controls. No concrete profile, argv or lifecycle defect was found that establishes the cause of error5. Neither denial-policy support nor executor child-creation policy can be inferred as the cause from that error alone. The late no-deny control's stronger executor-startup inference is ANALYSIS-350; any follow-up stdout control is outside this snapshot.

The source is exhaustive enough for a narrow classification: normal local Windows Codex0.164.0, without an internal override or external environment, has no default Bash-selection branch. Classifying that specific scenario not applicable does not approve a provider exception, prove generic external-host Bash support, or mark native Bash as tested. Native cmd is an actual fallback and task2.1 remains open until its selected shell and durable native hook delivery are observed with required path controls. No acceptance requirement was weakened.

The post-migration live result applies to the recorded both-client setup and supports task5.2 separately. It does not supply missing fallback coverage. Existing performance arithmetic and both the passing serial suite and failed default parallel-suite run retain their historical meaning; the precise parallel timing failure remains unresolved.

Only this report and its assigned supporting evidence were written. No source/test/configuration/plan/ledger/install/agent file, user session, provider registration, host ACL or system shell was changed. Product identity and the three reviewed evidence hashes matched before/after. No new release-complete or archive recommendation is made; triage and completion decisions remain with the orchestrator.
