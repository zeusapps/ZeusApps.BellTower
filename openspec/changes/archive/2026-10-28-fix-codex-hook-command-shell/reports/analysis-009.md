# Analysis report: fix-codex-hook-command-shell / 009

## Review target

- Reviewer / role: analyze_fallback_008 / code-analyzer, Analysis009 continuation.
- Client / version / mode / model (resolved ID): Codex host version unavailable; native evidence targets installed Codex CLI0.164.0; delegated Default-mode review; assigned `gpt-6.1-sol`, high effort. Runtime model self-inspection unavailable.
- Date: 2026-10-28 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`; recheck ANALYSIS-350 and ORCH-001, independently audit native009 provenance, and review final capability evidence/docs against the agreed requirements. New IDs ANALYSIS-400 through ANALYSIS-449 reserved for gated regressions; no suggestions authorized.
- Task and requirement references: [tasks](../tasks.md)2.1/5.1/5.2, [setup delta](../specs/standalone-setup/spec.md), [design](../design.md), [ledger](findings.md).
- Scope and relevant neighboring paths: new capability detail and focused matrix test; four scoped docs; native009 retained raw data; QA009 echo and QA010 deny-observer evidence. No source/configuration/install/plan/ledger write ownership. No heavy checks during QA011's independent full suite.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior reports / rechecked findings: [Analysis008](analysis-008.md), [QA009](qa-009.md), [Analysis007](analysis-007.md), [QA007](qa-007.md), [Analysis005](analysis-005.md), [QA005](qa-005.md); ANALYSIS-350, ORCH-001, QA-003 acceptance.

Before/after identity command:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests docs/compatibility.md docs/follow-ups.md docs/hook-ingress.md docs/installation.md tools/BellTower.Ingress.Bench
```

Manifest:

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
M tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Providers/CodexPermissionTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tests/BellTower.Tests/Setup/ProjectProfileTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `68d884a3ee0c4491215306fd84051e8e659c654675b02bc8ad569c89a26eabd0`.
- Untracked SHA-256: reporter `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`; probe `3e6a758b76fe766d46b142ea9d9adb0c4a81ce6d4beecff907c24a0919657a6e`; reporter tests `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=22 diff=68d884a3ee0c untracked=3/cce5a39616ce`; matched. Product source was frozen; assigned report/evidence are outside this identity.
- Native009 evidence SHA-256: `9C1D9A5617BFE191842BC0889262F53621DB798B1292C0EA08C84E501593EEA3`, matched before/after; further hashes in [audit](../evidence/analysis-009-native-audit.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Native command provenance | Pure reflection into current compiled resolver and builder; compare saved command and sole SessionStart hook | Direct native executable and0.1.9018 helper hashes match; builder output equals raw command, SHA `8a1a41ef83c2a21f447e5bae06c246849c8931e3454908f45bda55869b3f8dc4` | Passed; no provider launched; current DLL distinct from pre-documentation DLL |
| Durable capture | Invoke `CaptureProtocol.Decode(Stream)` on retained sole spool file | Protocol1/codex/startup; project directories match; capture hash `13df2846296c98bcd94a69aff40856d9c80ebc2df930a5b3d344a0e4b285e651` | Passed independent decode; not a fresh dispatch |
| Genuine fallback identity | Compare decoded PID/creation ticks with ancestor sequence; inspect ingress and process-probe source | Exact `cmd.exe -> codex.exe` chain; sole cmd equals HookShell identity; source obtains it through OS process metadata | Passed; no inference from command text alone |
| Deny and special-path controls | Inspect private TOML, exact helper/data locations, raw QA010 observer JSON | Both full PowerShell directories denied; host stat/read succeeds, sandbox stat/read EPERM; required path characters retained | Passed retained raw review; child environment conflict remains launching investigator's recorded fact |
| Native completion/network/cleanup | Read stdout/stderr, endpoint code and metadata; read-only identity check | Synthetic turn completed; one loopback request; retained cmd/codex identities no longer running | Passed within recorded scope; original exit0/816ms and endpoint cleanup are investigator observations |
| Evidence inference correction | Read QA009 correction and both raw echo outputs | Both quote-free echo controls pass with empty stderr; prior marker error cause remains unknown | ANALYSIS-350 correction verified |
| Architecture / complexity / duplication | Inspect final Matrix detail and focused test; compare responsibility with development conventions | Evidence-only capability text; status stays supported; no duplicated provider policy or functional path introduced | Passed; test protects evidence scope, not proof of runtime behavior |
| Documentation coherence | Read Completion010/011 and current four docs against accepted benchmark/live records | Follow-ups/compatibility correctly distinguish benchmark review, trust observation and installed/process version; hook-ingress still says review pending | ORCH-001 still failing at `docs/hook-ingress.md:64` |
| Performance/regression mapping | Read prior005/007 evidence and functional-source handoff | Helper/command path unchanged; current command reproduced exactly; Core/probe/benchmark retain previous review mapping | Carried forward with original scopes; no fresh latency or full-suite pass claim |
| Identity/hygiene | Identity/hash before/after; `git diff --check`; owned-output personal/secret scan | Same source/evidence target; no whitespace or targeted hygiene hits | Passed |
| Build/full suite/native rerun | Not run; source change after native009 is capability text/test/docs and independent QA011 owns full suite | Developer Completion010 reports build/format/matrix6/6 | Not performed here; no unnecessary contention |

## Findings

**No new findings.** No ANALYSIS-400 through ANALYSIS-449 IDs are assigned. The canonical ORCH-001 correction remains incomplete: current `docs/hook-ingress.md:64` still describes the independently reviewed literal-echo trial002 as awaiting review. Current follow-ups and Completion011 correctly say QA005/Analysis005 verified it. This is a factual evidence-status contradiction; the authentication limitation in the same sentence remains valid and separate. The orchestrator has accepted this recheck failure for a bounded documentation correction after both frozen-target reviews.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-350 | QA009 correction, existing008 preserved | Read corrected classification and raw no-deny/deny echo outputs; both exact output/empty stderr | Verified correction; does not explain QA008 error or investigation007 error5 |
| ORCH-001 | Completion011 and current four docs at identity above | Current follow-ups/compatibility fixed; exact stale benchmark statement remains in hook-ingress line64 | Still failing; no new ID, no product/native defect |
| QA-003 / task2.1 | Native009, matching helper/native hashes and current command reproduction | Protocol-decode success, exact special paths, matching cmd shell identity, independent deny-observer evidence plus pinned resolver review | Evidence supports completion for targeted Codex0.164.0 normal local Windows PS/cmd modes; orchestrator owns task/ledger transition |
| QA-003 / task5.2 | Existing post-migration0.1.9018 user record | Both-client/mixed-session/chip acceptance remains separate from native009; no new trial | Carried forward; no trust-dialog or Claude process-version inference |
| Earlier Core/probe/benchmark findings | Functional source from reviewed0.1.9018 path remains unchanged under this evidence-text continuation | Analysis007/QA007 and Analysis005/QA005 mappings; current helper hash and exact builder reproduction | Carried forward, not re-executed |

## Assessment and limitations

Native009 supplies the previously missing genuine cmd fallback evidence. The raw record is coherent with unchanged Codex0.164.0 running the product-generated Cmd hook through the retained0.1.9018 helper: it produced a protocol-valid startup capture at the exact required special-character path, and its OS-derived HookShell identity matches cmd immediately below Codex in the ancestor chain. Independent QA010 confirms the directory-deny method blocks both metadata and reads. The retained transcript and current exact-command reproduction corroborate the investigator's successful launch record without a redundant native rerun.

Together with prior native PowerShell delivery, this supports task2.1 for the targeted normal local Windows0.164.0 resolver path. Analysis008's narrowly source-backed local Bash non-applicability remains valid. External-host Bash, other versions and untested path priorities remain unverified; no blanket provider restriction, exception, or feature waiver is approved. The earlier failed controls/configuration attempts remain history and are not relabeled as passes.

The new capability evidence is correctly scoped and changes no runtime policy. The installed0.1.9018 package remains the earlier functional package; it does not acquire later diagnostic wording merely because current source was rebuilt. No repackage/reinstall is needed for the native proof. Benchmark arithmetic retains the independently accepted revised definition and raw guards; no new performance result is claimed.

ORCH-001 is the remaining failed item in this review. The report does not approve whole-change completion while that current documentation contradiction remains. QA011's final default-suite result is outside this snapshot; preserve the earlier parallel1003/1004 failure and serial1004/1004 pass unless later evidence supersedes the current result, without erasing history or inventing a cause.

Only this report and assigned supporting evidence were written. Product identity and native009 hash matched before/after. No settings, installation, live provider session, host ACL, source, test, plans or ledger were changed. Orchestrator retains triage, ledger and completion decisions.
