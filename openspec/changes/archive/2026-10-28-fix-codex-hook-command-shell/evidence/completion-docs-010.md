# Completion documentation 010

## Scope

Updated hook-delivery documentation and the capability matrix for the pinned Codex 0.164.0 native cmd fallback result. The wording is limited to the tested local Windows resolver path and leaves external-host Bash, other Codex versions, and other path-priority combinations unverified. User-operated post-migration 0.1.9018 acceptance is recorded separately and is not attributed to a shell. The matrix capability remains supported; no provider restriction was introduced.

Changed files:

- `docs/hook-ingress.md`
- `docs/compatibility.md`
- `docs/installation.md`
- `docs/follow-ups.md`
- `src/BellTower/Diagnostics/CapabilityMatrix.cs` (Codex hook evidence text only)
- `tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs`

Native capture and source findings are recorded in [native investigation 009](native-investigation-009.md) and [source checks 008](analysis-008-source-checks.md). The separate user acceptance is [user live acceptance 001](user-live-acceptance-001.md).

## Checks

- `dotnet build -c Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --include src/BellTower/Diagnostics/CapabilityMatrix.cs tests/BellTower.Tests/Diagnostics/CapabilityMatrixTests.cs` — passed.
- `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CapabilityMatrixTests' --verbosity minimal` — passed, 6 passed, 0 failed, 0 skipped.
- `git diff --check` on the six changed implementation/documentation paths — passed.

The in-sandbox test attempt failed at Windows named-pipe IPC; its recorded test-host process exited. The same focused test command passed outside that sandbox. The earlier full solution test command returned exit code 1 without diagnostic output and was not used as a result for this scoped change. No full suite was run here; independent QA owns that check.

## Identity

Standard source identity against baseline `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`:

`head=aadbfb3 baseline=aadbfb3 files=22 diff=545dc036a481 untracked=3/cce5a39616ce`

The 22-file standard scope includes concurrent work elsewhere in the tree. Its three untracked reporter/probe/test files retain identity `cce5a39616ce`; this task did not edit them. The standard tracked diff hash includes the capability-matrix evidence/test edits in this report and should not be compared to the earlier pre-documentation hash.

## Remaining evidence boundaries

Native cmd fallback is established for the pinned Codex 0.164.0 case described above. The pinned local Windows source has no Bash selection branch; this does not establish behavior for external-host Bash, other Codex versions, or untested shell/path-priority combinations. The benchmark remains awaiting independent review, and hook-trust review remains open.
