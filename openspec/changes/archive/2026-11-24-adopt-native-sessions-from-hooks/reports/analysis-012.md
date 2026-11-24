<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-012 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-012 (recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent); CLI version not probed; role-default mode; `claude-opus-5-5`
- Date: 2026-11-04T09:07Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-012, recheck of DEV-024 against ANALYSIS-900 to ANALYSIS-906 ([analysis-010](analysis-010.md)), plus regression in test duplication and design, process lifetime, and fixture readiness for the design decision 7 drift safeguard
- Task and requirement references: task 2.2 (QA-1304); [implementation-024](../evidence/implementation-024.md)
- Scope: the 24-path target below. QA-1700 git isolation was read only for duplication and design regressions; QA owns its correctness.
- Baseline commit: `b6c0b45`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline b6c0b45 -Paths src,tests,tools,global.json,docs/compatibility.md,openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-023.md,openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-024.md` -> HEAD `b6c0b452025f`, 13 modified and 11 untracked, tracked diff SHA-256 `e22e69d5e2d7...`, summary `head=b6c0b45 baseline=b6c0b45 files=24 diff=e22e69d5e2d7 untracked=11/667f1ea97744`
- Before/after identity: identical summary after the recheck; HEAD unchanged; no mutation in scope
- Prior reports / rechecked findings: [analysis-010](analysis-010.md): ANALYSIS-900, 901, 902, 903, 904, 905, 906

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Build | `dotnet build -c Release --no-restore` | 0 errors (incremental, up to date) | passed |
| Native tests, determinism and lifetime | `dotnet test -c Release --no-build --no-restore --filter <class>` twice each for `NativeReplayTests`, `NativeAgentResolveProcessTests`, `NativeAdoption` (covers both NativeAdoption classes); after each run, sampled leftover `ping.exe -n 60`, `claude.exe`, `codex.exe` and `bell-tower-ingress.exe` under `artifacts\unit-tests` | Replay 5/5, 2 s wall time (was 62 s); Resolve 28/28, 9-11 s; NativeAdoption 75/75, 3 s; 0 orphans after every run | passed |
| Fixture readiness (decision 7) | Temporarily added `claude-2.1.311-*` and `codex-0.165.0-*` copies to the ignored test output folder; the Codex copies had a found agent (`agentArguments: ["exec","<redacted>"]`, expected headless, dedicated, agent-process). Ran replay and Resolve with no code edit, then removed the copies | 37/37 passed (9 replay, 28 Resolve); output folder back to 4 files | Refresh needs no test edit, including the first Codex found-agent fixture |
| Codex no-argument rows | Classified `codex.exe  /s /c "..."` and `codex.exe /s /c "<inner.cmd>"` with the built `bell-tower.dll` | `embedded-runtime` with first argument `/s` (3 arguments) | ANALYSIS-1101 |
| Fixture hygiene guard | `[regex]::IsMatch('Users\name\file','[\/:]')` | `False`; only `/` and `:` match | ANALYSIS-1100 |
| Sleeper swap semantics | Grep of `NativeAdoption`, `CaptureConsumer`, `HookIngestor` for image-name checks | Promotion and reconciliation use process identity and liveness, not the image name, so moving from `cmd.exe` to `ping.exe` does not change what the tests prove | no regression |
| Duplication / design | Diff review of `Testing/*`, `NativeAdoption*Tests`, `CaptureTests`, `GitTests`, `IdentityPrecedenceTests`, `Scripts/*` | One sleeper, one fixture loader, one git helper and `BuiltApplication.Ingress` replace the earlier copies. The parallel replay cases are gone. `HookMaintenanceTests.AddPayload` keeps its own path, which is justified because it copies a payload layout | no regression |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <15 changed test files>` | exit 0 | passed |
| Git isolation tests | not run | WorktreeFamily commits by design. Per the assignment's no-commit-risk rule they were not executed; read only | not performed; QA owns QA-1700 |

## Findings

### ANALYSIS-1100: The fixture hygiene guard does not reject backslash paths

- Severity: suggestion (low-impact defect: the guard is weaker than its own stated contract)
- Related task / requirement: 2.2; content hygiene for committed fixtures
- Locations: `tests/BellTower.Tests/Providers/NativeReplayTests.cs:36-37`
- Observation: the comment promises "no path, home folder or prompt text" in `agentArguments`, but `@"[\/:]"` is a character class of `/` and `:` only. The `\/` is an escaped slash, not a backslash. Measured: `Users\name\file` does not match. Relative or UNC Windows paths such as `Users\<name>\...` or `\\host\share` would pass, and prompt text is not checked at all.
- Proposed improvement and benefit: use `@"[\\/:]"` so backslashes are rejected, and either enforce the `<redacted>` convention for non-option tokens or narrow the comment to what is actually checked.
- Behavior-preservation risks: none; the current fixtures contain no backslash.
- Evidence type: measured result (regex evaluation)
- Verification / measurement plan: add a temporary `agentArguments` token `Users\x` and confirm the fact fails.
- Relationship: regression introduced by the ANALYSIS-902 fix

### ANALYSIS-1101: The Codex "no argument" real-process rows exercise the positional-prompt branch, not the no-argument branch

- Severity: suggestion (low-impact defect: the coverage claim is inaccurate)
- Related task / requirement: 2.2; AGENTS.md honest capability and coverage statements; residual of ANALYSIS-900
- Locations: `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs:26,66,205,216`; `src/BellTower/Providers/AgentModeClassifier.cs:96-103`; implementation-024 table row "codex | none"
- Observation: with empty provider arguments, the copied image's command line is ` /s /c "..."`, so the classifier sees `["/s","/c",...]`. It treats `/s` as a positional prompt and returns `embedded-runtime` through the prompt branch (line 100), the same branch the `"fixture prompt"` row covers. The no-argument and flags-only branch (line 103) is reached by no real-process row: a mutation of that line would survive every row. `ACodexHookAboveANestedClaudeImageClaimsTheNearestCodexProcess` asserts `EmbeddedRuntime` on the same `/s` basis. cmd.exe always needs `/c`, so a true no-argument Codex process cannot be shaped with this stand-in. That is an accepted limit of the stand-in, but the evidence states the row as "none".
- Proposed improvement and benefit: state the true shape. Either relabel the row and its evidence as a positional token produced by the stand-in, or drop it as a duplicate of the prompt row. Record that the no-argument branch is covered only by the unit row `{ "codex", [], EmbeddedRuntime }` in `NativeAdoptionTests.Modes`. Coverage claims then match what runs. No new test machinery is needed.
- Behavior-preservation risks: none (test labels and evidence only).
- Evidence type: measured result (classifier invocation through the built assembly)
- Verification / measurement plan: re-read the row table and evidence; optionally mutate line 103 to `Unknown` and confirm only `NativeAdoptionTests` fails.
- Relationship: residual of ANALYSIS-900; the ANALYSIS-900 defect itself is fixed (see Re-verification)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-900 | `untracked=11/667f1ea97744` | Provider arguments now precede `/s /c`; rows for `exec`, `e`, `review`, `--future-option value`, `"fixture prompt"` and Claude `-- -p` pass through both entry points (28/28 twice). The marker is now a real first token, and implementation-024 records a failing mutation for `exec`. The implementation-023 restriction is corrected in evidence | verified; residual labelling issue tracked as ANALYSIS-1101 |
| ANALYSIS-901 | same | `DisposableSleeper` starts `ping.exe` directly with drained redirected output and kills the tree on dispose; replay wall time 2 s (was 62 s); 0 orphans; all former `Sleeper()` callers migrated, and the old helper is removed | verified |
| ANALYSIS-902 | same | Per provider, version and event files carrying `providerVersion`, `source`, `note`, `agentArguments`, `ancestorNames` and `expected`. The classifier reads `agentArguments`; session IDs come from the fixtures; there are no `.Single` or exact-provider assertions. Temporary next-version and Codex found-agent fixtures passed with no code edit (37/37) | verified; ANALYSIS-1100 is a regression in the new guard |
| ANALYSIS-903 | same | Parallel replay cases replaced by one data-driven theory; the remaining `NativeAdoptionTests` ingestion cases cover distinct injected scenarios (nested and managed metadata) | verified |
| ANALYSIS-904 | same | Every Resolve row runs through `event` and through `BuiltApplication.Ingress` with a `StateStore` drain; `CaptureTests` uses the shared helper | verified |
| ANALYSIS-905 | same | Claude under a Codex image (`app-server` and embedded outer) claims only its own process; a Codex hook above a nested `claude.exe` claims the nearest `codex.exe`; both entry points pass | verified (the Codex-above-Claude mode assertion shares the ANALYSIS-1101 caveat) |
| ANALYSIS-906 | same | The `finally` kills the process tree; assertion messages carry the exit code, stdout and stderr; no orphaned images after the runs | verified |

## Assessment and limitations

- All seven ANALYSIS-010 findings are verified. Two low-impact defects are new, and neither blocks: ANALYSIS-1100 (hygiene regex misses backslashes) and ANALYSIS-1101 (Codex "no argument" rows actually test the prompt branch; the evidence label is inaccurate).
- The fixture set is ready for the decision 7 drift safeguard: a version refresh is an added file, measured with a temporary Codex found-agent fixture. One observation, given no ID because it is not a current defect: `NativeFixture.SessionStart` picks the "newest" file by ordinal name, which misorders versions with a different digit count (for example `2.1.999` against `2.1.1000`). It only selects which payload the Resolve rows pipe in.
- There are no process-lifetime regressions; parity is matched for Claude and Codex, including mixed nested rows through both entry points.
- Not performed: the full suite, and executing the git isolation tests (no-commit-risk rule; QA owns QA-1700). No provider was launched. The temporary fixture copies were written only to the ignored test output folder and removed.
- The orchestrator owns triage.
