# Implementation evidence 003

Developer role model: `gpt-6-luna`.

## Final correction round

This round addresses canonical `ANALYSIS-050` (also reported as QA-050). The code change is shared in `SessionReducer`:

- Any main-thread ToolStart can no longer clear an operation-attributed or candidate permission wait just because its operation ID differs, including when the fingerprints are equal or different.
- The existing fallback still clears a genuinely uncorrelated notification wait with no operation ID, candidates or fingerprint when foreground activity resumes. Prompt, stop, end and subagent behavior remain separate.
- A matching or unrelated ToolStart does not narrow candidate scope. Correlated ToolEnd removes completed IDs from a candidate set, increments permission-scope revision when that set changes, and resolves the wait when every candidate has completed.

The seven previously failing tests were reconciled to the correlation requirement rather than changing their expected outcomes to mirror the old implementation:

- Matching identified/fingerprint-only starts keep the permission alongside a separate question wait; the correlated ToolEnd removes only the permission.
- Grace-period PreToolUse keeps a provisional wait silent; correlated PostToolUse resolves it.
- For candidate waits, a candidate ToolStart does not complete or narrow the wait; one candidate ToolEnd narrows it and the last candidate ToolEnd resolves it.
- An unrelated same-fingerprint ToolStart and then the matching ToolStart both preserve an identified wait until its correlated result.
- The backdated Codex restore test retains its more-than-window unrelated-start load and now confirms the restored wait remains silently present.
- Matched Claude/Codex captured and normalized provider tests preserve direct and candidate waits after subagent starts and unrelated main-thread starts, then close them with correlated results.
- The delayed Codex runtime-status test confirms a late candidate start leaves candidates and approval revision unchanged; only the candidate's ToolEnd narrows the scope and invalidates the stale read.

## Verification

- `dotnet build -c Release --no-restore` -> passed, zero warnings and errors.
- Targeted affected Core/provider classes (`ReducerTests`, `PermissionNotificationTests`, `HumanWaitCorrectionTests`, `CaptureTests`, `CodexPermissionTests`) -> 177 passed.
- Broader Core, Providers and Sessions filter -> 533 passed.
- Full suite `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --verbosity minimal` -> **1000 passed**. It ran outside the sandbox after the previously observed testhost IPC failures. Beforeward, the documented NativeAOT packaging path produced the standalone helper and the package's helper was copied to the ignored expected test-bin path; its SHA-256 matched the packaged helper and `--protocol-info` returned the capture-v1 contract. No placeholder executable was used.
- Opt-in native delivery/setup snapshot tests using the fresh package 0.1.9015 helper -> 4 passed across Claude and Codex. Both delivery probes passed; for both providers the invalid helper failed native verification while preserving settings and ownership bytes, and the valid helper applied the planned changes.
- Scoped formatter for the five changed C# files -> passed after the known MSBuild named-pipe sandbox denial on the identical formatter command.
- `git diff --check` -> passed.

The package build was preceded and followed by the same implementation identity: `head=aadbfb3 baseline=aadbfb3 files=19 diff=262b223e8a79 untracked=3/1b89a893996b`. The ignored dev package is version `0.1.9015`; release metadata records commit `aadbfb3` and `dirty: true`. ZIP SHA-256: `B6F4A1C1184190C4527C3423DF3146E4EA4335AE17CFC25C88971C0601A12488`. The package was not installed.

## Provider-native evidence and remaining acceptance

- Task 1.1: [native investigation 001](native-investigation-001.md) records Codex 0.164.0 rejecting the tested command-array config shape (`invalid type: sequence, expected a string`). [Native investigation 002](native-investigation-002.md) records the pinned source behavior: hook commands inherit the turn's selected shell; the Windows default shell type is PowerShell, with `cmd.exe` as the ultimate fallback, and `COMSPEC` applies only if no shell program was passed. Native PowerShell 7 command delivery was observed in two environments. Exact shell path priority and fallback branches remain unverified; the product-generated command is not claimed to work in every possible shell, and setup fails before settings writes if the selected shell cannot run it.
- Task 1.3: native investigation 001 records Claude Code 2.1.310's own CLI delivering SessionStart through configured Git Bash with the packaged helper and isolated settings/data. This verifies that tested version and mode; other versions and Git Bash overrides remain unverified.
- Task 2.3: the existing Codex 300 ms median gate remains failed. The recorded 121-sample run has median 385.4529 ms, p95 492.4313 ms, p99 594.4172 ms, max 595.8976 ms, 121 durable captures and zero failures. The matched PowerShell no-op median was 343.9491 ms; that comparison suggests shell startup contributes but does not isolate per-call helper cost. No new 120-sample benchmark was run because ingress performance code did not change.
- Task 5.2: user-operated post-migration packaged acceptance, mixed simultaneous sessions and trust review remain pending. The isolated native probes are not presented as user acceptance.
- No provider settings, user installation, production data or active user processes were changed.
