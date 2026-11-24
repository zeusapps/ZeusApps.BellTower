> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-055); client: Claude Code 2.1.331, subagent; mode: auto mode; model: claude-opus-5-5.
> Refusal (as reported by the reviewer): "Subagents should return findings as text".

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-055

Recheck of ANALYSIS-5400 against the DEV-066 fix. This is a standalone approved bugfix outside this change's requirements; the change root holds the ledger only.

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331, subagent (auto mode), claude-opus-5-5
- Date: 2026-11-24T19:01Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-055
- Task and requirement references: none (standalone bugfix for ANALYSIS-5400); evidence [implementation-066.md](../evidence/implementation-066.md)
- Scope and relevant neighboring paths: `src/BellTower/Program.cs`, `tests/BellTower.Tests/ProgramConsoleTests.cs`; neighbors checked for no change: `src/BellTower.Launcher`, `src/BellTower.Ingress`, `src/BellTower/Setup` (status-line route `HookCommandBuilder.cs`, `ProviderSettingsEditor.cs`), `src/BellTower/Cli`
- Baseline commit: 76beb52
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 76beb52 -Paths src/BellTower/Program.cs tests/BellTower.Tests/ProgramConsoleTests.cs`
  - HEAD 76beb5216df7b50d74fd54dd800fb8197fd0792f, baseline the same
  - Manifest: `? tests/BellTower.Tests/ProgramConsoleTests.cs`, `M src/BellTower/Program.cs`
  - Tracked diff SHA-256: a6aacf873686aad2c35de6223abe0ab5f51c224a014f3d5cc92c593472919179
  - Untracked: ef2f9dc1e51fdc4b7704537b6a1067ee854d7cad60d0bb75db4f3c5bf4ff762a `tests/BellTower.Tests/ProgramConsoleTests.cs`
  - `Summary: head=76beb52 baseline=76beb52 files=2 diff=a6aacf873686 untracked=1/9c70ea7ee483` (matches the expected line)
- Before/after identity: after the analysis the same command printed the same summary line. `git status --short` showed the same 27 entries as the session's starting snapshot. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-054.md](analysis-054.md) ANALYSIS-5400 (rechecked). ANALYSIS-5401 is deferred by the assignment and was checked only to confirm nothing changed.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Match to proposed guard | `git diff 76beb52 -- src/BellTower/Program.cs`; read the whole file | `Main` now calls `UseUtf8Console(static e => Console.OutputEncoding = e, static e => Console.InputEncoding = e)` before the `try`. `UseUtf8Console` creates one `UTF8Encoding(false)` and runs each setter in its own `try`/`catch (IOException) { }` | Matches the ANALYSIS-5400 proposal. One difference: the delegates are required parameters passed in by `Main`, not optional parameters with built-in defaults. This is simpler (no nullable fallback in product code) and keeps the real setters visible at the call site |
| Catch breadth | Inspection | Exactly two `catch (IOException)` clauses, one per setter. There is no general catch and no wider filter. Subclasses of `IOException` are also tolerated, which is the intended family for console I/O failures | As proposed |
| Order and healthy behaviour | Inspection | Output then input, both before `CaptureCompatibility.EnsureTrayInvocation` and `Installation.Lease`, as at baseline. Both setters receive BOM-less UTF-8. Sharing one encoding instance instead of two changes no behaviour (the .NET setters store a clone) | Preserved. The setters stay outside the existing `try`, so a disconnected console no longer ends in either a crash or the exit-1 path |
| Out-of-scope paths unchanged | `git diff --stat 76beb52 -- src/BellTower.Launcher src/BellTower.Ingress src/BellTower/Setup src/BellTower/Cli`; `git diff --stat 76beb52 -- src tests` | Empty for the launcher, ingress, setup and CLI. Besides `Program.cs`, the overall `src`/`tests` diff lists only unrelated pending change files (Diagnostics, Hosts, Tray, NavigationOutcomeTests) | No launcher, ingress, status-line route or setup change; ANALYSIS-5401 stays deferred |
| Test design | Read `tests/BellTower.Tests/ProgramConsoleTests.cs`; checked `InternalsVisibleTo` in `src/BellTower/BellTower.csproj` and `ImplicitUsings` in `Directory.Build.props` | Four tests: (1) the output setter throws `IOException` with HRESULT 0x800700E9 and the input setter is still called; (2) the input setter throws and output was set; (3) the healthy case gives code page 65001 with an empty preamble on both; (4) an `InvalidOperationException` from either setter propagates | Covers all four cases planned in ANALYSIS-5400. That `Main` passes the real `Console` setters is checked by inspection only. A process-level regression test that always reproduces the race needs a timing hook in product code, as ANALYSIS-5400 recorded |
| Format (verify only) | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/Program.cs tests/BellTower.Tests/ProgramConsoleTests.cs` | Exit 0 | Passed |
| Build / tests | not run | Owned by QA-061. The DEV-066 evidence reports a clean Release build and passing tests | Not performed by this reviewer |
| Architecture / parity | Inspection against `docs/development.md` layering | The change is confined to the shared payload entry point. No provider adapter is touched and no policy is duplicated | Covers Claude Code and Codex equally (shared startup). No parity gap introduced |
| Duplication / complexity / performance | Inspection | One small helper and two guarded calls; startup cost and allocations are unchanged in practice | Not applicable |

## Findings

**No findings.** This recheck raises no new suggestions.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-5400 | `head=76beb52 baseline=76beb52 files=2 diff=a6aacf873686 untracked=1/9c70ea7ee483` | Diff inspection: each setter has its own `IOException`-only guard, order and placement are preserved, and `Main` passes the real setters. The four unit tests match the planned cases. Out-of-scope paths are unchanged. Verify-only format passed | Verified by analysis. Build and tests are left to QA-061. The post-install event-log check from the ANALYSIS-5400 plan was not performed; it needs a dev install |
| ANALYSIS-5401 | same | `git diff --stat` is empty for `src/BellTower/Setup` and the launcher | Not rechecked (deferred by assignment); confirmed untouched |

## Assessment and limitations

- Scope covered: the two target files, the guarded startup path in `Main`, and confirmation that the launcher, ingress, setup (status-line route) and CLI paths are unchanged.
- The fix matches the proposed guard, catches only `IOException` per setter, keeps the order and healthy behaviour, and needs no simplification. No architectural concern.
- Not performed:
  - Build and tests (owned by QA-061).
  - The live check from the ANALYSIS-5400 plan: after a dev install, count new `.NET Runtime` 1026 `set_OutputEncoding`/`set_InputEncoding` events over normal Claude Code and Codex use. It needs an authorized dev install and is outside this assignment.
- Unexpected writes: none. The client refused the write of this report, so the text is returned inline.
- The orchestrator owns closing ANALYSIS-5400 and the ledger status.
