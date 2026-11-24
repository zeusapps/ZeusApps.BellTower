# Analysis report: adopt-native-sessions-from-hooks / 001

## Review target

- Reviewer / role: setup_analysis / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / delegated Default mode / gpt-6.1-sol (high).
- Date: 2026-10-29T16:41:12Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-001.
- Task and requirement references: task 5.1 and [Native-command integration without dedicated profiles](../specs/standalone-setup/spec.md); tasks 1.1-1.3 are reviewed only for evidence boundaries.
- Scope: the eight files in the identity below. Relevant read-only neighbors: `TerminalProfilesEditor.cs`, `VsCodeBridgeSetup.cs`, `DesktopOwnership.cs`, `SetupPlan.cs`, `IntegrationRefresh.cs`, `vscode/extension.js`, and `docs/development.md`.
- Baseline commit: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Prior reports / rechecked findings: none.
- Implementation identity before analysis:

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md src/BellTower/Cli/SetupCommand.cs src/BellTower/Setup/DesktopSetup.cs src/BellTower/Setup/IntegrationOwnership.cs src/BellTower/Setup/IntegrationSetup.cs tests/BellTower.Tests/Cli/NativeSetupCliTests.cs tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
Manifest (A added, M modified, D deleted, ? untracked):
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md
  ? tests/BellTower.Tests/Cli/NativeSetupCliTests.cs
  ? tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
  M src/BellTower/Cli/SetupCommand.cs
  M src/BellTower/Setup/DesktopSetup.cs
  M src/BellTower/Setup/IntegrationOwnership.cs
  M src/BellTower/Setup/IntegrationSetup.cs
Tracked diff SHA-256: 0a836f57c76806cd372410860c01424c0cd34fd5f488716616eb3cb822551392
Untracked file SHA-256:
  8006e8bc263cf34a8df88647113f6c70ae6b0905f2ddca3e1a7569ef4729c68a  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  9ab6d3dbaed1fcee5799928ad9bed449fa17421ef670b5c6d0b2b099207cabcb  openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md
  6b28f51683703d148db7259a2d2d91ea0218e00fd978ba8d17d9402f216244b5  tests/BellTower.Tests/Cli/NativeSetupCliTests.cs
  8935a6a79e8c363df440532a1455cf848c3a7465cd28258e882ada6875a63369  tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
Summary: head=a69392e baseline=a69392e files=8 diff=0a836f57c768 untracked=4/c366fb8460b0
```

- The same command and exact eight paths produced identical full output after analysis, including every untracked file hash. No unexpected change occurred in this target. Concurrent native-process probes outside these paths are excluded as assigned.
- Before and after summary: `Summary: head=a69392e baseline=a69392e files=8 diff=0a836f57c768 untracked=4/c366fb8460b0`.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared bridge setup configuration, both providers | Source trace from the new host-bridge branch through extension provisioning and extension activation | Bridge settings remain exclusively in terminal-profile editing; ANALYSIS-001 below | Failed acceptance by source inspection. No extension or native client was launched. |
| Provider and mode ownership, both providers and mixed installation | Source trace for add/remove, desktop/native coexistence, legacy records and final bridge action | One Boolean tracks every native provider; ANALYSIS-002 below | Failed acceptance by source inspection. Existing tests cover provider removal only while desktop ownership masks this case. |
| Plan/apply and failed-action ownership | Inspected `SetupPlan.Apply` and manifest placement | Removal commits ownership after actions; existing transaction boundary is reused | Passed source check for retaining removal retry metadata; action failure was not executed by this analyzer. |
| Responsibility boundaries and duplication | Compared changed code with `docs/development.md` and adjacent setup services | CLI delegates to setup; bridge provisioning is centralized; required bridge settings remain coupled to profiles | Passed CLI boundary; settings/consumer ownership defects require correction. No speculative abstraction or performance change recommended. |
| Schema evidence accuracy | Read `evidence/native-adoption-001.md` and retained change context | Report labels native two-client and desktop trials unperformed, and labels missing binder an evidence gap rather than a hard restriction | Passed evidence-boundary review. The schema generation and provider commands were not rerun by this analyzer. |
| Runtime/native acceptance | No runtime or host operation | Developer handoff explicitly leaves native hook trust and actual bridge installation unverified | Not performed; this partial task 5.1 implementation does not establish native adoption acceptance. |
| Functional tests, build and formatter | Not rerun by this analyzer | Independent QA owns functional verification; developer reports 12 scoped tests and format passing | Not performed by analyzer; developer results are not independent evidence. |
| Target integrity | `scripts/review-identity.ps1` before and after with the exact scoped paths and baseline above | Full outputs matched | Passed. Read-only commands required escalation because sandbox Git ownership differs. |
| Performance | Source-only setup change; no performance issue claimed | No timing or throughput measurements were taken | Not applicable to the two correctness/ownership findings. |

## Findings

### ANALYSIS-001: Bridge-only setup omits the bridge's required connection settings

- Severity: warning.
- Related task / requirement: task 5.1; Native-command integration without dedicated profiles; preserving independent native and desktop setup.
- Locations at the reviewed identity: `src/BellTower/Setup/IntegrationSetup.cs:101` and `:116`; neighboring `src/BellTower/Setup/TerminalProfilesEditor.cs:70`, `vscode/extension.js:15` and `:17`; regression assertion `tests/BellTower.Tests/Setup/NativeHostSetupTests.cs:24`.
- Observation: `hostBridge: true` bypasses `DesktopSetup` and only calls `VsCodeBridgeSetup.Plan`. That helper installs/removes the extension; it does not write `bellTower.executablePath` or `bellTower.dataDirectory`. Those writes are still inside `TerminalProfilesEditor.VsCode`. The extension therefore falls back to a bare executable name and its default/environment data directory. A user selecting an alternate Bell Tower data directory, or whose executable is not on PATH, can install the bridge successfully while it registers in the wrong location or cannot start. The new test explicitly rejects any VS Code settings-file change, so it enforces the omission instead of validating usable bridge configuration.
- Reproduction / evidence: source-trace reproduction, not executed live: preview `setup --host-bridge` against an isolated home with a Code settings file and a nondefault data directory. No plan entry configures the two bridge settings, although the extension reads them to locate its executable and registration root. A related coexistence failure follows when desktop setup is removed while native ownership remains: terminal-profile removal restores these settings even though the bridge is retained.
- Proposed improvement and benefit: separate the existing owned bridge-setting edits from terminal-profile edits and apply them for either native or desktop bridge consumers. Restore them only after the last applicable consumer is removed, preserving edited or explicitly pre-existing values. Keep profile definitions and tab-presentation settings outside native-only setup. This makes the selected data directory and executable usable without importing a wrapper.
- Behavior-preservation risks: existing adopted settings, manual edits, desktop/native removal order, extension installation failure, and conditional restoration must remain protected by the existing ownership rules.
- Evidence type: source observation; no native or performance measurement.
- Verification plan: isolated fixtures for Claude, Codex and both must assert the selected executable/data settings are present, unrelated settings and profiles are unchanged, both install orders work, removal of either setup mode preserves settings still required by the other, and last-consumer removal restores only unchanged owned values.
- Relationship: new finding; no canonical predecessor known.

### ANALYSIS-002: A single native bridge flag cannot preserve the other provider on removal

- Severity: warning.
- Related task / requirement: task 5.1; preserving other-provider setup and shared bridge ownership for Claude Code, Codex and mixed configurations.
- Locations at the reviewed identity: `src/BellTower/Setup/IntegrationOwnership.cs:9`; `src/BellTower/Setup/IntegrationSetup.cs:106` through `:117`; relevant test `tests/BellTower.Tests/Setup/NativeHostSetupTests.cs:88`.
- Observation: all native bridge consumers share `NativeHostBridge`. Every native removal assigns `false` regardless of the selected providers or remaining hooks. With native-only setup for both providers and no desktop profile owner, removing only Claude releases the bridge for Codex too. If the owned extension is present and a Code installation is found, the resulting plan schedules extension removal even though Codex hooks remain. The provider-removal test first installs desktop setup, so its desktop owner hides this regression. The inverse cleanup path also lacks consistency: plain selected-provider removal does not clear the native flag, and a native-only installation can keep a stale bridge request after its hooks are gone.
- Reproduction / evidence: source-trace reproduction, not executed live: install native hook/bridge setup for `both`; with owned extension presence recorded, preview `setup --clients claude --host-bridge --remove`. The selected-provider hook edit retains Codex, but line 108 clears the only native bridge flag and lines 111-117 request removal when no desktop owner remains.
- Proposed improvement and benefit: retain native bridge consumers per provider, updating only selected consumers and deriving the shared extension requirement from the remaining native and desktop consumers. Reconcile generic removal consistently, preserve backward reads, and continue committing removal ownership only after actions succeed.
- Behavior-preservation risks: compatibility with old manifests, provider-specific removal, generic removal, repeated setup, desktop/native coexistence, and failed action retry.
- Evidence type: source observation; no live extension installation or uninstallation.
- Verification plan: add native-only mixed-provider fixtures in both installation orders, remove each provider separately and assert the other retains bridge ownership/configuration; remove the last provider and assert cleanup is scheduled. Include generic removal, legacy ownership records, and failed removal-action retry without executing a real extension action.
- Relationship: new finding; no canonical predecessor known.

## Re-verification

No prior findings were assigned for re-verification.

## Assessment and limitations

The new CLI delegates correctly and the implementation reuses the existing bridge provisioning helper. However, bridge settings and native provider ownership are not fully separated from terminal-profile ownership, leaving two task 5.1 acceptance defects. Both affect Claude Code and Codex through the shared setup policy, including mixed configurations.

This is a source-based review of a partial implementation. It does not verify native hook trust, actual VS Code installation/activation, real terminal bindings, or tasks 1.1-1.3. Those remain explicitly unperformed rather than restricted or complete. The target identity matched before and after analysis. Only this reserved report was written; no source, test, planning, ledger, session or personal configuration was changed.

