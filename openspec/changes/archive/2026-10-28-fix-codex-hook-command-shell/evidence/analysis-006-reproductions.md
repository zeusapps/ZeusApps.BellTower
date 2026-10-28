# Analysis 006 bounded reproductions

Target summary: `head=aadbfb3 baseline=aadbfb3 files=20 diff=ff9951a31d79 untracked=3/d9d7e40c7d6d`. Full identity and findings are in [analysis-006](../reports/analysis-006.md).

## ANALYSIS-250: capture followed immediately by process exit

Ignored fixture project: `artifacts/analysis-hook-shell-006/Analysis006.csproj`. It references the existing Release product/protocol DLLs and rebuilds only its own scratch output, avoiding a product build while QA runs. The fixture apphost is copied as `codex.exe` in that same ignored output directory. A process-local PATH prefix makes the current probe resolver find it; the original PATH is restored in a finally block. No global/user environment or provider configuration is changed.

In its synthetic runner branch, the fixture reads the isolated CODEX_HOME supplied by ProviderHookProbe, derives the corresponding disposable `data/captures-v1` directory, builds a synthetic Codex Register envelope, serializes it through `CaptureProtocol.Encode` (which validates it), writes the capture, records a synthetic receipt, and exits 0 immediately. It does not run a real provider, contact a model endpoint or claim to verify command parsing.

The parent invokes current `ProviderHookProbe.Verify` through reflection with provider Codex, explicit PowerShell candidate and a two-second timeout. This isolates the process-completion boundary rather than attempting to force a native provider's shell.

Command:

```text
dotnet run --project artifacts/analysis-hook-shell-006/Analysis006.csproj --no-restore -c Release
```

Two completed outside-sandbox runs each produced:

| Observation | Count per run |
| --- | ---: |
| Validated capture written by synthetic runner | 8 |
| Probe accepted | 1 |
| Probe rejected despite valid capture and child exit 0 | 7 |

The rejected calls reported the sanitized native-delivery failure with exit code 0 and no stdout/stderr. Accepted indices varied by timing. The source explains the difference: the loop tests child liveness before checking the directory, and the final check requires the earlier `observed` flag. A final durable capture can therefore be ignored after normal exit. Capture existence alone, with existing protocol/provider validation, must be evaluated after termination as well.

An initial default-sandbox scratch run returned invalid-handle errors and zero valid receipts. That run is excluded from the reproduction result; it is recorded as an environment-limited attempt, not a successful native or synthetic delivery check.

## ANALYSIS-251: project-only removal loses shell evidence

The same ignored fixture seeds an isolated integration manifest with `SelectedCodexHookShell = Cmd`, a Codex desktop provider, and one registered disposable project. It previews:

```text
IntegrationSetup(..., isolatedHome).Plan("codex", remove: true, project: registeredProject)
```

It decodes the planned manifest change and counts planned Codex `hooks.json` changes. It does not Apply.

Observed:

```json
{"Probe":"remove-project-retains-provider-shell","Before":"Cmd","PlannedAfter":null,"ProviderSettingsChanges":0,"Applied":false}
```

The global hook settings are intentionally untouched by project-profile removal, so their selection evidence should remain. The clear-selection branch currently runs for both global removal and project-only removal.

## Focused regression check

```text
dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~FastHookSetupTests&FullyQualifiedName!~OptIn)|FullyQualifiedName~HookDeliveryReporterTests|FullyQualifiedName~ProjectProfileTests' --verbosity minimal
```

Outside-sandbox result: **18 passed, 0 failed, 0 skipped**. Opt-in native checks were excluded; these passes are not native provider evidence and do not cover the two new boundary reproductions. QA independently owns full/native checks.

The 0.1.9016 ZIP/helper hashes independently match the handoff. No new benchmark, installation, migration, user configuration edit or user-process termination was performed. The before/after source identity matched, whitespace check passed, and the assigned-output hygiene scan found no targeted personal/secret patterns.
