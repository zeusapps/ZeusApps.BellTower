> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-053); client: Claude Code 2.1.331, subagent; mode: non-interactive, auto mode; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-053 (DEV-065 light recheck, task 4.11 fix cycle 2)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331 / subagent, non-interactive, auto mode / claude-opus-5-5
- Date: 2026-11-24 (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-053 (light recheck)
- Task and requirement references: task 4.11 and the task 4.3 note ([tasks.md](../tasks.md)); [implementation-065](../evidence/implementation-065.md); [findings.md](findings.md) entries of 2026-11-24 (ANALYSIS-5200 observation 2 and the user decision)
- Scope and relevant neighboring paths: `docs/compatibility.md`, `docs/installation.md`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/Hosts/NavigationReasons.cs`, `tests/BellTower.Tests/Tray/NavigationOutcomeTests.cs`, and the earlier 4.11 files `src/BellTower/Tray/NavigationOutcome.cs` and `src/BellTower/Tray/TrayApplication.cs`. Neighbors: [implementation-058](../evidence/implementation-058.md) (Codex hook order probe), `docs/follow-ups.md`
- Baseline commit: 76beb52
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 76beb52 -Paths src tests docs vscode` printed HEAD 76beb5216df7 and manifest `M` for the 8 files above. Tracked diff SHA-256: `87b1155781285c28b8112941104aabab733d5c8f66644e0395b64e525dd7fd30`. No untracked files. Summary: `head=76beb52 baseline=76beb52 files=8 diff=87b115578128 untracked=0/e3b0c44298fc`. This matches the expected value.
- Before/after identity: the after summary was `head=76beb52 baseline=76beb52 files=8 diff=87b115578128 untracked=0/e3b0c44298fc`. It is identical to the before summary, so nothing changed during the analysis.
- Prior reports / rechecked findings: [analysis-052](analysis-052.md) ANALYSIS-5200 and ANALYSIS-5201; the ANALYSIS-5101 residual recorded there ([analysis-051](analysis-051.md))

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-5200 wording against the measurement | Compared the diff of `docs/compatibility.md:80,96`, `docs/installation.md:80`, `CapabilityMatrix.cs:105-106` and `DiagnosticsReporter.cs:101` with findings.md observation 2 and the user decision | All five surfaces say: Claude Code sends its first hook at launch, so the color appears a few seconds after launch; `codex --no-daemon` sends none at launch, so its color appears a few seconds after the first prompt submit, during the first turn; about 4 s measured. `compatibility.md:96` states the 4 s as hook-to-color for both providers, a known latency, deferred, with no provider-restriction claim | Matches the measurement (Codex 4.4 s, Claude Code 4.2 s) and the decision. Covers both clients in each surface |
| Stale wording | `grep -rni "end of the first turn\|12-54\|can be at the first prompt\|not a Bell Tower gap"` over `src docs vscode tests` (sources only) | No matches | Removed from all product surfaces |
| Codex "embedded" scope | Codex column of `compatibility.md:80` and `CapabilityMatrix.cs:106` against [implementation-058](../evidence/implementation-058.md) | 058 classifies `--no-daemon` hooks as `embedded-runtime` and records `SessionStart` at the first prompt submit | Consistent |
| Planning artifact consistency | Read the task 4.3 note, `tasks.md:32` | It still says the matrix, doctor and documentation "must say the Codex color can arrive as late as the end of the first turn" and "Codex sends that hook late" | Contradicts the corrected product text and the user decision. Recorded as a residual under ANALYSIS-5200 (orchestrator-owned planning artifact, outside the identity scope) |
| ANALYSIS-5201 tests | Inspection of `NavigationOutcomeTests.cs:146-163` against `NavigationOutcome.cs:29-31` | `ASuccessSaveFailureStatesTheFocusAndTheSaveFailure` checks the success text. `ASaveFailureKeepsTheStatedRefusalForEitherProvider` (`claude`, `codex`) uses a real shared-backend `SharedClick` result and checks the `SharedBackend` prefix, the save-failure suffix and the absence of "Session focused" | Tests exist and pin the ANALYSIS-5102 behavior for both providers. Not run (QA-060 owns tests) |
| ANALYSIS-5101 residual remark | Read `NavigationReasons.cs:36-43` | The unregistered-host clause is now scoped to "a direct capture whose host is learned later"; a headless run is stated as an unstated permanent limit that keeps the timer | Accurate. Comment only, `IsPermanentLimit` unchanged |
| Regression of changed lines | Inspection of the diff | Only string constants, doc text, an XML remark and two tests changed. `IsPermanentLimit`, `ApplyViewed`, `SaveFailedStatus` and the tray call sites are unchanged from DEV-064 | No regression. No new duplicated per-provider policy |
| Build / tests / format | Not run | Owned by QA-060 | Not performed by assignment |

## Findings

**No findings.** No new finding IDs were used. A light recheck raises no new suggestions; the residual below belongs to ANALYSIS-5200.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-5200 | head 76beb52 + diff 87b115578128 | The product wording in all five surfaces matches findings.md observation 2 and the user decision for both providers. The hook-to-color latency is stated as Bell Tower's, measured and deferred, not as a provider fact. Residual: the task 4.3 note (`tasks.md:32`) still requires the withdrawn "as late as the end of the first turn" wording and says Codex sends its hook late. The orchestrator should reconcile it (planning artifact). Precision note: in the parentheticals "(about 4 s measured)" after "a few seconds after launch" and "after the first prompt is submitted", the measured interval is first accepted hook to applied color, not launch or submit to color. `compatibility.md:96` states this correctly, so this is not a defect | Verified in product surfaces. Planning note still contradicts them |
| ANALYSIS-5201 (and duplicate QA-5900) | same | `grep SaveFailed tests` finds `NavigationOutcomeTests.cs:149,159`. The cases are as described in implementation-065. implementation-065 also corrects the false claim in implementation-064 | Verified by source. Test execution belongs to QA-060 |
| ANALYSIS-5101 (residual remark) | same | `NavigationReasons.cs:36-43` no longer claims that unregistered-host refusals resolve for a headless run. It states the headless case separately | Verified |

## Assessment and limitations

- Scope covered: the DEV-065 diff and the earlier 4.11 lines in the same 8 files, for both providers.
  - Claude Code: the at-launch color statement and the shared-backend save-failure test case.
  - Codex: the color statement at the first prompt submit for `--no-daemon` and embedded clients, and the save-failure test case.
- Decisions or actions for the orchestrator:
  - Reconcile the task 4.3 note in `tasks.md:32` with the 2026-11-24 decision.
  - The deferred ~4 s hook-to-color latency is recorded in findings.md and in `docs/compatibility.md:96` ("a known latency, deferred"), but `docs/follow-ups.md` and tasks.md have no entry for it. Record it where follow-ups are tracked, if that is intended.
- Not performed: build, tests and format verification (owned by QA-060); the live tray; any timing measurement.
- Unexpected writes: none. The one attempted write (this report) was refused by the client. The before and after identity match. No MCP or WorkLog call was made.
