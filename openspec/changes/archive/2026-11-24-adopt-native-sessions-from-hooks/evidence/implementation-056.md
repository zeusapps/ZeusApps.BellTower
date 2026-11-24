# DEV-056 implementation evidence: verified token and job-safe hook startup

## Scope

This final implementation cycle addresses the remaining parts of ANALYSIS-4100 and ANALYSIS-4101. It retains the common Claude/Codex ingress and existing user Exit behavior.

## Changes

- `TrayProcess` reads both `TokenElevationType` and `TOKEN_ELEVATION.TokenIsElevated`. It permits ordinary Default and non-elevated Limited tokens, selects a linked token only from an elevated Full token, and verifies that linked token is Limited and non-elevated before process creation. Elevated Default, inconsistent, or unknown token state fails with a bounded diagnostic. Sign-in identity selection uses the same verified linked Limited token for split UAC sessions.
- Both process-creation APIs include `CREATE_SUSPENDED`. Before any launcher instruction can run, the hook requires `IsProcessInJob(child, NULL)` to succeed and report false. Membership, query failure, `ResumeThread` failure, or an unexpected previous suspend count causes an attempt to terminate only the newly created suspended child. Process and thread handles close on every created-child path. The hook does not wait for tray readiness.
- The fake launcher now creates either an immediate non-breakaway kill-on-close job or nested jobs where the inner job permits breakaway but the outer kill-on-close job does not. Matched Claude and Codex cases assert neutral `{}` output, exit 0, one durable capture, no fake tray execution, bounded completion, and the respective refusal diagnostic.
- Selector regressions cover Default elevated/non-elevated, Full and Limited states, inconsistent states, and unknown query results. Documentation now describes the verified token and suspended-child checks.

## Primary API references

- [TOKEN_ELEVATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_elevation)
- [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw)
- [IsProcessInJob](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob)
- [ResumeThread](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-resumethread)
- [Nested Jobs](https://learn.microsoft.com/en-us/windows/win32/procthread/nested-jobs)

## Verification and limits

- The orchestrator ran the focused DEV-056 selection against the snapshot DLL outside the sandbox: 37 passed, 11 failed out of 48. All 11 failures were ordinary-start assertions that expected a fake tray. The orchestrator's separate isolated ingress probe retained `last-attempt.txt` and reported `{}` with exit 0, `Failed/provider-job-breakaway-incomplete`, and no fake execution under the test execution host's ancestor job. This is the designed fail-closed result when the child remains in a job; it does not establish ordinary-start success outside that host job. The full suite was not rerun in this cycle.
- A Release build was attempted with the installed .NET SDK 10.0.115 through its explicit `MSBuild.dll` entry point from outside the repository SDK pin. Protocol compiled, then the build failed copying its assembly into the shared BellTower output because another process held that file open (MSB3027/MSB3021). No process was stopped. A fresh build and focused VSTest remain for the orchestrator after the shared output is available.
- To avoid that shared-output lock, a source snapshot was copied to the ignored `artifacts/DEV056-snapshot` hierarchy (no `.git` contents or user settings; an empty `.git` barrier prevents fixture Git discovery from reaching the working checkout). It contains the final scoped source, tests, project files, solution and build props. SHA-256 comparison confirmed the copied `TrayProcess.cs`, `FakeLauncher/Program.cs`, Protocol project file and the then-current hook-start test matched the working target; after the authorized public theory-signature correction below, the test file's final hash is `5259cc3844b0437f19db6a39d4f23c82d8e9a288a1566559430c24f243349c0e`.
- Scratch-only restore used SDK 10.0.115 with `RestoreLockedMode=false`, `RestoreIgnoreFailedSources=true` and `NuGetAudit=false`; the installed SDK advances ILLink/AOT dependency floors from 10.0.14 to 10.0.15. Lockfile changes were confined to the snapshot. The first snapshot build exposed CS0051 because the public xUnit theory method exposed an internal enum. The theory now accepts an `int` expected value and casts it for assertion; no production behavior changed. The final Release build succeeded with `dotnet 'C:/Program Files/dotnet/sdk/10.0.115/MSBuild.dll' tests/BellTower.Tests/BellTower.Tests.csproj -t:Build -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -p:Configuration=Release -v:minimal` from the snapshot root. The resulting test DLL is `artifacts/DEV056-snapshot/tests/BellTower.Tests/bin/Release/net10.0-windows/BellTower.Tests.dll`; ingress, tray and fake-launcher apphosts were also present in the snapshot at the paths resolved by `BuiltApplication`.
- The installed SDK 10.0.115 folder-only whitespace formatter ran on the three changed C# files and its `--verify-no-changes` pass succeeded. The solution-aware `dotnet format` invocation failed while opening Roslyn's MSBuild build-host pipe with `UnauthorizedAccessException`; it was not retried with elevated permissions.
- NativeAOT publish was attempted with the explicit SDK 10.0.115 MSBuild entry point for `BellTower.Ingress.csproj`, `win-x64`, and a separate `artifacts/DEV056-snapshot/publish/ingress-win-x64/` destination. Managed compilation succeeded, but native linking failed with `Platform linker not found`; the SDK output also showed `Access is denied` while checking linker locations. No publish output was produced. The environment lacks the required C++ Desktop Development workload/toolchain, so NativeAOT publish remains unverified.
- Protocol smoke passed by running the snapshot ingress apphost with `--protocol-info`; it printed `{"protocol":1,"minimumConsumer":1,"maximumConsumer":1}` and did not access Bell Tower data.
- Review identity for the final scoped paths: `head=5e95554 baseline=5e95554 files=23 diff=bceaf5143001 untracked=7/a5924b210bfa`.
- Focused VSTest against the final snapshot DLL is delegated to the orchestrator and is not claimed here.
- Native UAC token-pair behavior, actual elevated tray creation, and the Codex provider's native job remain unperformed; the selectors and synthetic nested-job fixture are not claims of native provider acceptance.
- No live provider settings, default Bell Tower data directory, real tray, or user process was used.
