# Analysis report: fix-codex-hook-command-shell / 001

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 local tooling; delegated analysis in Default mode; role-configured resolved model `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`, Analysis 001; reserved IDs ANALYSIS-001 through ANALYSIS-049.
- Task and requirement references: [tasks](../tasks.md), [setup delta](../specs/standalone-setup/spec.md), [ordering delta](../specs/session-tracking/spec.md), [design](../design.md).
- Scope: the source/test diff, four scoped documentation files, and `tools/BellTower.Ingress.Bench`; neighboring reducer, observation, setup ownership and hook adapter code as needed. No implementation edits were made.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior reports / rechecked findings: none.

Identity command, before and after analysis:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests scripts tools/BellTower.Ingress.Bench docs/hook-ingress.md docs/installation.md docs/compatibility.md docs/follow-ups.md
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
M src/BellTower/Setup/IntegrationSetup.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `0d0d08a73d7cd1c11e04ef7d44eaf8baab150f660ca40e8d3b3f5d37d531cf34`.
- Untracked hashes: HookDeliveryReporter `b717f06d134ab6e7a52006c67868bb1ef38997d1cb4ebf5126f00d122a99f9c6`; ProviderHookProbe `30d17e9394fe3e620db1f051cf5152ce528f55b15fcfabd97abbf5e25a05ed4e`; HookDeliveryReporterTests `bc7932ecc939c76b4668afd5c47bf9fa0ac58ba009a1b821a6b95bb52a4166df`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=16 diff=0d0d08a73d7c untracked=3/eaa17b92e513`; matched. Change-local reports/evidence and orchestrator tracking are excluded from implementation identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared policy and parity | Inspected `SessionReducer.ResumeForeground`, `Observation`, causal replay and `HookAdapter` | Shared implementation covers both providers and both ingress paths; isolated Core sequence reproduced an unrelated-subagent regression | Failed; ANALYSIS-001 |
| Setup safety and isolation | Inspected staging/hash cleanup, `SetupPlan.Apply`, native Codex probe, Bash resolution and temporary config generation | Provider settings writes occur after provisioning; helper images are hash-checked; malformed generated TOML blocks some valid paths | Failed path handling; ANALYSIS-002. Actual provider calls not re-run in this analysis |
| Diagnostics robustness and ownership | Inspected exact ownership matching and bounded file reads; executed malformed receipt checks with explicit disposable config roots | No command text is returned by the reporter, but valid JSON of the wrong shape crashes it | Failed; ANALYSIS-003 |
| Complexity and duplication | Compared changed responsibilities with `docs/development.md` | Provider-specific probing remains in Setup; shared attention policy remains in Core; no additional abstraction or provider-policy split is recommended | Passed architectural boundary inspection; no measured complexity claim |
| Performance methodology | Inspected benchmark shell arguments, sample counts, percentile calculation, no-op control and gate placement; read run 08 metrics | Codex native 121 samples, median 385.4529 ms, p95 492.4313 ms, p99 594.4172 ms, max 595.8976 ms, zero failed calls; no-op median 343.9491 ms | Existing 300 ms gate remains failed. These are developer-run measurements independently read from the retained artifact, not a new analyst benchmark |
| Whitespace | `git diff --check` | Exit 0 | Passed |
| Scoped formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include` the changed C# files | Default-sandbox MSBuild named-pipe denial; same verify-only command passed after auto-reviewed escalation | Passed outside sandbox; default sandbox failure retained in [reproductions](../evidence/analysis-001-reproductions.md) |
| Full suite / live trials | Not performed by analyzer | Independent QA has functional checks; developer evidence is classified separately | Not performed; no source inspection is presented as a functional pass |

## Findings

### ANALYSIS-001: Unrelated subagent work clears a foreground permission wait

- Severity: warning.
- Related task / requirement: 4.2; approval resolves only on its correlated result, genuine resumption, stop or end; preserve concurrent work and matched provider behavior.
- Locations: `src/BellTower/Core/SessionReducer.cs:226`, `:233`, `:245`, `:307`; `ToolStart` calls ResumeForeground before its later `IsMainThread` gate at `:271`.
- Observation: the new branches interpret any later different operation ID as permission resumption. They run for `IsMainThread=false` as well as foreground actions. A subagent starting unrelated work is not evidence that the foreground approval request resolved.
- Proposed improvement and benefit: restrict inference of foreground resumption to foreground evidence, while preserving correctly correlated result handling and late-start protection. This keeps the permission chip and runtime confirmation scope intact during unrelated subagent work.
- Behavior-preservation risks: retain the intended later distinct foreground operation behavior; do not prevent a subagent's own correlated result from resolving its relevant request. Cover candidate waits as well as directly attributed waits.
- Evidence type: executed isolated Core reproduction, four matched variants; one wait became zero after an unrelated subagent start for both Claude and Codex, with captured and normalized observations.
- Verification plan: add the sequence in [reproductions](../evidence/analysis-001-reproductions.md) as a regression, plus candidate membership and genuine foreground resumption checks in both ingress paths.
- Relationship: canonical analysis finding; QA may record an independent source observation against this ID.

### ANALYSIS-002: The isolated Codex config is invalid when its temporary path contains an apostrophe

- Severity: warning.
- Related task / requirement: 2.1 and 2.2; valid quoted paths and a reliable native setup probe.
- Locations: `src/BellTower/Setup/ProviderHookProbe.cs:93`, `:122`.
- Observation: replacing an apostrophe with two apostrophes and embedding the result in a single-quoted TOML table key produces a config rejected by installed Codex. A valid `TEMP` path containing an apostrophe therefore makes the new setup gate fail before the hook runs. Testing an apostrophe only in the helper path does not exercise the generated project key.
- Proposed improvement and benefit: encode the table key with valid TOML serialization; the product already has a TOML dependency and uses it in `CodexFooterSettings`. Preserve the isolated configuration and fake endpoint while fixing its encoding.
- Behavior-preservation risks: retain Windows separator handling, Unicode and spaces; test quotes in the temporary project path independently of quotes in the ingress path.
- Evidence type: native Codex parser check using a disposable `CODEX_HOME`. `features list` returned exit 1 with a bootstrap TOML parse error for the generated shape; no model request was required.
- Verification plan: parse or natively load a generated config whose isolated root contains an apostrophe and Unicode, then confirm the native probe can deliver a capture. Keep the existing special-character helper-path case.
- Relationship: no earlier finding.

### ANALYSIS-003: Malformed delivery receipt shapes crash the diagnostics reporter

- Severity: warning.
- Related task / requirement: 3.2; actionable, sanitized hook delivery diagnostics.
- Locations: `src/BellTower/Diagnostics/HookDeliveryReporter.cs:125`, `:127`, `:129`, `:132`; integrated unconditionally by `DiagnosticsReporter`.
- Observation: a JSON array root or a numeric `at` value raises `InvalidOperationException`, which the current catch filter does not handle. The new reporter therefore prevents the containing doctor report from completing when its small delivery receipt has an unexpected but syntactically valid JSON shape.
- Proposed improvement and benefit: validate object/string kinds before accessing them and classify invalid receipt content as unverified rather than terminating diagnostics. Preserve the receipt bytes and avoid logging its content.
- Behavior-preservation risks: preserve both `at` and legacy `At` support and valid delivery timestamps; do not turn invalid data into a delivered claim.
- Evidence type: executed isolated calls for `[]` and a numeric timestamp receipt; both threw `InvalidOperationException`.
- Verification plan: add matched-provider receipt cases for wrong root kind, wrong timestamp kind, absent/invalid timestamp and a valid timestamp, asserting a safe report without private receipt contents.
- Relationship: no earlier finding.

## Re-verification

Not applicable: first analysis round; no developer correction has been independently rechecked here.

## Assessment and limitations

The implementation maintains the intended layer split and correctly keeps probing before provider settings writes. It is not ready for a completion claim: the three concrete defects above require triage, the measured Codex latency gate fails, and the exact resolver/argv evidence, actual Claude CLI runner check, failing-setup snapshot fixture, package-to-final-source identity and user-operated post-migration/mixed acceptance remain incomplete as recorded in [implementation evidence](../evidence/implementation-001.md). Those evidence gaps are not proven provider restrictions or an accepted budget exception.

The no-op versus native distribution supports a shell-startup contribution hypothesis; subtracting their medians is not a per-call component measurement. The benchmark gate fires before state drain/replay, so this analysis makes no replay-failure claim.

No implementation, personal configuration, installation or active user process was modified. Only the assigned report/evidence and ignored disposable analysis outputs were written. The orchestrator owns triage and completion decisions.
