> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-045); client: Claude Code 2.1.330; mode: subagent (non-interactive); model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-045

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-045) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent (non-interactive), model `claude-opus-5-5`
- Date: 2026-11-22 (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-045, reviewing DEV-057
- Task and requirement references: task 4.5 acceptance ([tasks](../tasks.md)); task 3.3 guard (process-stop authority); developer evidence [implementation-057](../evidence/implementation-057.md)
- Scope and relevant neighboring paths:
  - In scope: `tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs` (new `Allowed` entry), `src/BellTower.Protocol/TrayProcess.cs` (read only; unchanged), `docs/follow-ups.md` (new section).
  - Neighbors read:
    - `src/BellTower.Protocol/HookTrayStart.cs`
    - `src/BellTower.Ingress/Program.cs`
    - `scripts/install-dev.ps1` (lines 99, 112, 122, 148-174, 770-790, 1276-1290)
    - the product sites of the 16 MiB literal
    - the archived `openspec/changes/archive/2026-11-22-recover-unready-dev-operation/reports/` (`analysis-001.md`, `findings.md`, `qa-001.md`)
    - prior triage of the guard: [analysis-027](analysis-027.md) (ANALYSIS-2703), [qa-034](qa-034.md) (QA-3403), [analysis-043](analysis-043.md)
- Baseline commit: `0c99801`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 0c99801 -Paths tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs src/BellTower.Protocol/TrayProcess.cs docs/follow-ups.md`
  - HEAD `0c99801b8ba79059ccfef51f410b92b1a6ab85ad`, baseline the same
  - Manifest: `M docs/follow-ups.md`, `M tests/BellTower.Tests/Core/ProcessStopAuthorityTests.cs`
  - Tracked diff SHA-256 `514ac2fd579b026d9b14915f1f9357096dc651a5468d3be9d56017857f103d1c`; no untracked files
  - Summary `head=0c99801 baseline=0c99801 files=2 diff=514ac2fd579b untracked=0/e3b0c44298fc`
- Before/after identity: after the analysis the same command printed `head=0c99801 baseline=0c99801 files=2 diff=514ac2fd579b untracked=0/e3b0c44298fc`. This matches the assigned identity, so nothing changed unexpectedly.
- Prior reports / rechecked findings: none rechecked. Related earlier findings: ANALYSIS-2703 / QA-3403 (guard limits; pattern expansion deferred).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Authority claim: every path to `TerminateProcess` | Grep of the guard regex over `src/`; read `TrayProcess.cs` in full | `TerminateProcess` matches only line 86 (the private `extern` declaration) and line 337 (the single call, in private `StopSuspended`). `StopSuspended` has exactly 4 callers (279, 283, 288, 292), all inside `if (created)` at 273 in `Start`. | Claim true |
| Authority claim: which handle | Data flow of `process` | `process` comes only from the `out ProcessInformation` of `CreateProcessWithTokenW` (271) or `CreateProcessW` (272). Both are called with `flags` that include `CreateSuspended` (269). `StopSuspended` reads only `process.Process`, and `TerminateProcess` is never given `GetCurrentProcess()`. The file declares no `OpenProcess`, `DuplicateHandle` or other PID-taking API, so no handle can come from a PID, name or record. `ProcessInformation` is a private struct. The handles are closed in `finally` (296-300), after termination. | Claim true: only the just-created suspended child |
| Authority claim: "before any of its code ran" | Read each branch | True when `IsProcessInJob` fails, when the child is in a job, and when `ResumeThread` fails (`uint.MaxValue`; nothing was resumed). Also true when the previous suspend count is 2 or more, because the thread is still suspended. When the previous count is 0, the thread was already running, which needs another actor to resume it first. The handle is still the owned child. | The ownership claim holds on every branch. The owner text slightly overstates the timing on a branch that is unreachable in practice, and it does not name the suspend-count branch separately. It is descriptive only, with no effect on behavior, so it is not raised as a finding. |
| Guard: count and key | Checked the regex against lines 86 and 337, and how the scan builds relative paths (test line 40) | The scan counts matching lines that do not start with `//`, so the declaration counts as one site and `2` is exact. The doc comment at 211-214 and the comment at 305 start with `//`, and neither would match anyway. The key `BellTower.Protocol/TrayProcess.cs` matches `Path.GetRelativePath(src, ...)` with `/`. | Correct. The test failed every time at baseline (findings history), and this entry is the minimal fix. |
| Guard: consistency with other entries | Compared with the 7 existing entries | The other entries use managed `.Kill(`, so they count call lines only. This is the first P/Invoke site, and its owner text explains why the declaration counts. Ordinal key order (`.` before `/`) puts it first, which matches the existing alphabetical order. The owner text names the process created in that operation, as the header rule requires. | Consistent |
| Guard: strength | Reasoned over the exact-equality assertion (43-44) | Per-file exact equality is kept. Adding a `TerminateProcess` line to this file (count 3), removing one (count 1), or declaring the API in a new file all fail the test. Another `StopSuspended(...)` call would not change the count, but the file's only handle source is the suspended child. Line counting and missing primitives (job objects, window messages) are the known per-file limits, already triaged as ANALYSIS-2703 / QA-3403. Grep finds no `TerminateJobObject`, kill-on-close, `ExitProcess` or `NtTerminate*` in `src/`. | Strength unchanged; nothing new weakens it |
| Docs: `docs/follow-ups.md` section | Compared each bullet with the archived `analysis-001.md`, `qa-001.md` and the summary and completion assessment in `findings.md`; checked the current source at `0c99801` | See "Docs check by item" below the table. | Accurate. One minor omission: the QA-002 bullet names only the stubbed launcher, but the harness also stubs the scan and the owner read. Not a defect. |
| Docs: hygiene and format | Read the section; `git ls-files --eol` | No paths, personal data or secrets. The heading and lead sentence follow the `bound-ended-session-state` section. The working copy is LF throughout (index LF, `core.autocrlf=true`), so the Git LF-to-CRLF warning is only a checkout note. | No issue |
| Parity: shared hook-start path | Read `BellTower.Ingress/Program.cs`, `HookTrayStart.cs` and `TrayProcess.cs` | The ingress accepts `--provider claude` or `codex` and calls `HookTrayStart.TryStart(root)` the same way after a persisted capture (63-66). Neither `HookTrayStart` nor `TrayProcess` has a provider parameter or branch, so the guard entry covers Claude Code and Codex hook starts equally. | Parity holds. No per-provider policy and no gap introduced. |
| Architecture / duplication / performance | Inspection | The change touches only a test and a doc; production code is unchanged and nothing is added to a hot path. | Not applicable |

Docs check by item:

- The five items are exactly the archived deferred set (QA-002, ANALYSIS-004, -006, -007, -011), and none of the verified findings is listed.
- QA-002: the wording matches `qa-001` (a Rollback interrupted after the pointer phase; the deadline runs only in the harness with stubs) and the "evidence gaps" in the completion assessment.
- ANALYSIS-004: the regex at `install-dev.ps1:112` and `FileShare::ReadWrite` at 122 are still present.
- ANALYSIS-006: the package hash (1282), descriptor (1286) and original-bytes (1288) checks still run before the Finalize dispatch (1290).
- ANALYSIS-007: `Get-TrayHolderPids` (770) and `Get-NotReadyFacts` (148) still both read presence and run the scan. The docs correctly leave out the "scans twice more" part, which DEV-002 fixed.
- ANALYSIS-011: the literal is in `Get-StateBudgetBytes` (99) and in three product sites: `AtomicFile.cs:46`, `UpdateTransaction.cs:287` and `AttachmentHealthCompatibility.cs:19`. `PcmWave.cs:8` is an unrelated audio cap.

Not performed:

- Builds, tests and format checks: QA runs them (see QA-052).
- Fault injection of the `StopSuspended` branches: source inspection only.
- Any access to the real installation, data directory or running tray: forbidden.

## Findings

**No findings.**

## Re-verification

None: no findings were assigned for recheck.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| n/a | n/a | n/a | not performed |

## Assessment and limitations

- **Entry is true and minimal.** Every path to `TerminateProcess` in `src/BellTower.Protocol/TrayProcess.cs` goes through `StopSuspended`. Its only argument comes from the `CREATE_SUSPENDED` creation in the same `Start` call. No PID, name or record lookup can reach it, and the file declares no API that could produce such a handle.
- **Guard is as strong as before.** The count of 2 (declaration plus call) is exact, the entry follows the guard's conventions, and exact equality keeps the guard's strength. The known structural limits (per-file line counting, missing primitives) remain as triaged in ANALYSIS-2703 / QA-3403 and are not made worse.
- **Owner-text nuance (not a finding).** "Before any of its code ran" is not guaranteed only when the previous suspend count is 0, which needs an outside resume. The ownership statement the guard protects holds on every branch.
- **Docs are accurate.** The `docs/follow-ups.md` section matches the archived deferred set and the current source at `0c99801`.
- **Parity holds.** Claude Code and Codex share one ingress path, and the same entry covers both.
- **Writes and identity.** Writes: none. The client refused the write of this report path, so the text is returned inline. The target identity was the same before and after. The orchestrator owns triage and ledger status.
