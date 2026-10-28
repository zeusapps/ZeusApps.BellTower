# Analysis 001 reproductions

These checks used disposable data under ignored `artifacts/analysis-hook-fix/`. No user settings, installation, processes or source files were changed.

## Core and diagnostics probe

An isolated console project referenced the current Release `bell-tower` and `BellTower.Protocol` assemblies. Its final command was:

```text
dotnet run --project artifacts/analysis-hook-fix/AnalysisProbe.csproj --no-restore -c Release
```

Result: exit 0. The output was:

```json
{"Probe":"subagent-start","Provider":"claude","Captured":false,"Before":1,"After":0,"ExpectedAfter":1}
{"Probe":"subagent-start","Provider":"claude","Captured":true,"Before":1,"After":0,"ExpectedAfter":1}
{"Probe":"subagent-start","Provider":"codex","Captured":false,"Before":1,"After":0,"ExpectedAfter":1}
{"Probe":"subagent-start","Provider":"codex","Captured":true,"Before":1,"After":0,"ExpectedAfter":1}
{"Probe":"array receipt","Result":"threw","ErrorType":"InvalidOperationException"}
{"Probe":"numeric timestamp receipt","Result":"threw","ErrorType":"InvalidOperationException"}
```

### ANALYSIS-001 sequence

For each provider and each captured/normalized variant, create one `AppState` and send the following observations to `SessionReducer.Apply` in source-time order. Use one session and attachment, distinct observation IDs, and the current `AdapterRevision`:

| Source offset | Kind | Operation | Main thread |
| --- | --- | --- | --- |
| 0 seconds | Register | none | true |
| 1 second | Prompt | none | true |
| 2 seconds | Permission | foreground-operation | true |
| 3 seconds | ToolStart | unrelated-subagent-operation | false |

Set `OperationFingerprint` to the respective operation string. For the captured variant, set `IngressAt` equal to `At`; for normalized observations, leave it null. Read `state.Sessions[provider + ":analysis"]` after every apply, rather than holding an older causal replay snapshot. The Permission observation creates one wait. The unrelated subagent ToolStart removes it in all four variants.

### ANALYSIS-003 inputs

Create a disposable `DataPaths` root and pass explicit disposable Claude and Codex configuration roots to `HookDeliveryReporter`, so no personal settings are read. Write each of these bodies to the disposable `codex-observed.json`, then call `Report()`:

```json
[]
```

```json
{"at":1}
```

Each call throws `InvalidOperationException`. The first fails because the receipt root is not an object; the second because the timestamp is not a string.

## ANALYSIS-002 native parser check

The probe constructs its project key by replacing an apostrophe with two apostrophes and surrounding the key with single quotes. A representative generated table header is:

```toml
[projects.'<temp>/O''Brien/project']
trust_level = 'trusted'
```

The check wrote this shape to a disposable `CODEX_HOME/config.toml`, ran the installed Codex `features list` command, and restored the process-local `CODEX_HOME` afterward. No model operation or personal configuration access was required.

Result: exit 1, with bootstrap configuration failure and a TOML parse error: an unclosed table with an expected closing bracket. Actual temporary paths were omitted from this record. Therefore a valid temporary directory containing an apostrophe prevents the native setup probe from starting.

## Verification boundary and sandbox observation

The first disposable Core probe compile lacked the `BellTower.Protocol` assembly reference; only the disposable project was corrected. The final probe compiled and ran successfully.

Scoped `dotnet format ... --verify-no-changes --include ...` initially failed inside the sandbox with `UnauthorizedAccessException` at `NamedPipeClientStream.TryConnect` in the MSBuild build host. An auto-reviewed retry outside the sandbox passed. This is a named-pipe access failure, not evidence of a repository source-write denial. `git diff --check` passed.

The implementation identity remained `head=aadbfb3 baseline=aadbfb3 files=16 diff=0d0d08a73d7c untracked=3/eaa17b92e513` before and after these checks.
