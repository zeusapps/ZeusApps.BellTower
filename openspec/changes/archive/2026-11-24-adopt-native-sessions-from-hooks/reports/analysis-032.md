<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-032 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-sonnet-5-5 (cost-efficient override recorded in the ledger); Refusal: the client refused the report write (as reported by the reviewer). -->

DEV-044 is sound and I found no blocking defects. I could not save the report: the client refused the write to reports/analysis-032.md, so the full text is inline below for the orchestrator to save (no retry, per role). I did not build or run tests, because the concurrent QA reviewer may rebuild. I relied on the build and test runs recorded in evidence/implementation-044.md instead.

```
status: done
role / client / model: code-analyzer / Claude Code Agent SDK subagent, non-interactive (client version not exposed; claude/codex not launched) / claude-sonnet-5-5
task / finding IDs: ANALYSIS-032 (light review of DEV-044) / ANALYSIS-3200 (suggestion), ANALYSIS-3201 (suggestion)
changed or report paths: none written. The write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-032.md was refused; full text is inline below.
checks: review-identity.ps1 before and after -> passed (identical)
        dotnet build, dotnet test, dotnet format -> not run (concurrent QA may rebuild; evidence 044 already records 20x and 5x runs and a killed mutant)
target identity: before: head=479c321 baseline=479c321 files=5 diff=8a3d27b47744 untracked=1/d33f941d85c1
                 after: head=479c321 baseline=479c321 files=5 diff=8a3d27b47744 untracked=1/d33f941d85c1 (matches expected, no mutation)
limitations / decisions needed: report write refused (text inline). Light review: correctness and safety only, test infrastructure only (no provider parity impact).
next: the orchestrator saves the report text below as reports/analysis-032.md and triages ANALYSIS-3200 and ANALYSIS-3201 as optional.
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-032 (light review of DEV-044)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent, non-interactive delegated mode; `claude`/`codex` not launched; client version not exposed to the subagent) / claude-sonnet-5-5 (cost-efficient model override recorded by the orchestrator)
- Date: 2026-11-05 (local)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-032
- Task and requirement references: DEV-044; QA-3800 and QA-3700 fixes; [qa-038](qa-038.md); [implementation-044](../evidence/implementation-044.md)
- Scope and relevant neighboring paths:
  - Reviewed files: `tests/BellTower.Tests/BellTower.Tests.csproj`, `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs`, `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs`, `tests/BellTower.Tests/Providers/CodexStandInServer.cs`, evidence 044.
  - Neighbors read: `src/BellTower.Launcher/BellTower.Launcher.csproj`, `src/BellTower.Ingress/BellTower.Ingress.csproj`, `Directory.Build.props`, `tests/BellTower.Tests/packages.lock.json`, `.github/workflows/validate.yml`, `scripts/package.ps1`, `tests/BellTower.Tests/Testing/BuiltApplication.cs`, and other `Accepted`/`BehaviorMode` users under `tests/`.
- Baseline commit: 479c321
- Implementation identity (before): `head=479c321 baseline=479c321 files=5 diff=8a3d27b47744 untracked=1/d33f941d85c1` (matches expected)
- Before/after identity: identical after the analysis. No unexpected mutation.
- Prior reports / rechecked findings: none. QA-3800 and QA-3700 are QA findings; this is a first review of the fix.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Received counter placement | Source read of `CodexStandInServer.Serve` | `Methods.Add` runs under `gate` (lines 142-146), before the `Silent` check (line 147). `Received` reads `Methods.Count` under the same `gate`. | Correct for its purpose: every read message is counted before the decision to answer. |
| Residual race | Source read, ordering analysis | The lock is released after `Add`. `BehaviorMode` is read afterwards at line 147 with no lock, and the property is not volatile. | ANALYSIS-3200: narrow window, not blocking. |
| Test 1 synchronization | Read `AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain` | X (already timed out and disposed) and A each send one `initialize` under Silent. The test waits for `Received >= 2`, then asserts `Received == 2` and `Accepted == 2`. | Sound apart from the residual race. A wait timeout fails on a clear count instead of a confusing downstream failure. |
| Test 2 synchronization | Read `AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess` | Waits for `Received >= 1`, asserts it, then switches to Normal. | Same conclusion as test 1. |
| Other accept-vs-message users | `rg "\.Accepted\|Behavior\.Silent\|BehaviorMode" tests` | The only mode switches after a wait are `CodexSharedAccessTests.cs` 267 (after all four opens already failed, safe), 313 and 340 (both fixed). `CodexTransportTests` and `CodexSharedRuntimeServiceTests` assert `Accepted` only after operations complete. `ManagementOwnershipProcessTests.cs:428` waits on `Closed >= Accepted && Accepted > 0` (conservative, no mode switch). | No other instance of the race pattern. Agrees with implementation-044. |
| Project reference pattern | Compare the csproj lines | The Launcher `ProjectReference` with `ReferenceOutputAssembly="false"` has the same form as the Ingress line. | Follows the existing pattern. |
| Build and packaging effects | Read both csproj files, `Directory.Build.props`, lock file, `validate.yml`, `package.ps1` | `ReferenceOutputAssembly="false"` copies nothing into the test output. Launcher uses `RuntimeIdentifiers` (plural), so output lands in `bin/<config>/net10.0-windows`, the exact path `CreateInstallation` copies. The test `packages.lock.json` is unchanged and lists no Ingress either, so a reference without output adds no lock entry. CI runs `restore --locked-mode`, `build -c Release`, `test --no-build`; the solution already contains the Launcher, so Release CI behaviour is unchanged and Debug or single-project builds now build the launcher. `package.ps1` restores and publishes the Launcher separately and is unaffected. | No unwanted effects found. Relies on evidence 044 for the runs (20/20 Debug and Release, `restore --locked-mode` ok). |
| Named failure in `CreateInstallation` | Read lines 559-568 | A `FileNotFoundException` naming the missing `bell-tower.exe` is thrown before `CopyDirectory`. | Correct. It replaces the opaque "Unsupported installation" and checks the file the installation needs. |
| Format / hygiene | Visual inspection of diff and evidence | The brace style of the lock block matches the file's convention. Evidence uses repository-relative names only. | No issue. `dotnet format --verify-no-changes` not run (QA owns it). |

## Findings

No blocking defects.

### ANALYSIS-3200: Residual narrow race between the Received count and the Silent check

- Severity: suggestion
- Related task / requirement: DEV-044, QA-3800
- Locations: `tests/BellTower.Tests/Providers/CodexStandInServer.cs` lines 142-147 (count under the lock, `BehaviorMode == Silent` read after the lock is released); `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs` lines 305-313 and 334-340
- Observation: `Received` becomes visible when the lock is released after `Methods.Add`. The serving thread then reads `BehaviorMode == Behavior.Silent` a few instructions later, outside the lock, and `BehaviorMode` is a plain auto-property with no barrier. A test thread polling exactly in that gap could see the count and set `Normal`, so that one message would be answered. The window is microseconds against a 10 ms poll, orders of magnitude narrower than the original accept-vs-message race. It is effectively negligible but not strictly closed.
- Proposed improvement and benefit: capture `var mode = BehaviorMode;` inside the same `lock (gate)` that adds the method, and use `mode` for the Silent test (or count `Received` after the Silent decision). That makes "received while Silent" exact. Optional.
- Behavior-preservation risks: the other mode checks (lines 152-171) can keep reading the property, or also use the snapshot, with no change for tests that do not switch mode mid-message.
- Evidence type: source observation (not reproduced; no failure observed, and evidence 044 reports 20/20 and 5x class runs)
- Verification / measurement plan: optionally run the two tests in a loop (for example 50x) after the change. Evidence 044 already shows a mutant killing both tests.
- Relationship: new

### ANALYSIS-3201: Exact-count assertions assume one message per connection

- Severity: suggestion
- Related task / requirement: DEV-044
- Locations: `tests/BellTower.Tests/Providers/CodexSharedAccessTests.cs` lines 310 and 339 (`Assert.Equal(2, server.Received)`, `Assert.Equal(1, server.Received)`)
- Observation: the equality assertions are correct today because a Silent connection only carries the `initialize` request (notifications follow a reply that never comes). If `CodexSharedAccess` later adds a pre-initialize message or a retry, these tests fail on a count mismatch rather than a behavior regression.
- Proposed improvement and benefit: optionally keep the wait on `Received` but assert `>=`, or assert via `RecordedMethods()` containing only `initialize`. Marginal; the current form states the intent and fails loudly, which is acceptable.
- Behavior-preservation risks: none.
- Evidence type: source observation
- Verification / measurement plan: none needed.
- Relationship: new

## Re-verification

Not applicable (first review of DEV-044). QA owns closure of QA-3800 and QA-3700.

## Assessment and limitations

- `Received` is incremented under the lock before the Silent check. That removes the accept-before-read race behind QA-3800, and no other test uses the accept-then-switch-mode pattern.
- The Launcher reference mirrors the Ingress one, with no output copying, no lock-file change and no CI change. The new guard turns an opaque setup failure into a named one (QA-3700).
- Neither finding blocks acceptance.
- Not performed: builds, test runs and `dotnet format`, for the reasons above. Claude-side scenarios were not run either, because this is test infrastructure only (Codex stand-in) and does not affect provider parity.
- No writes were made other than the refused report; no commits; review identity unchanged before and after.
