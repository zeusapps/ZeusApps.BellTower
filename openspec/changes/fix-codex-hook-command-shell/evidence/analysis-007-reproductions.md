# Analysis 007 independent recheck evidence

Source summary: `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758 untracked=3/cce5a39616ce`. Full hashes and review boundaries: [analysis-007](../reports/analysis-007.md).

## Original reproductions against current binaries

The ignored harness `artifacts/analysis-hook-shell-006/Analysis006.csproj` references the existing current Release product/protocol assemblies. Only the harness itself was rebuilt. Its original valid-capture/immediate-exit sequence and project-only preview were rerun unchanged before adding the extra failure controls.

```text
dotnet run --project artifacts/analysis-hook-shell-006/Analysis006.csproj --no-restore -c Release
```

Outside-sandbox results:

- Valid fast-exit runner: eight accepted, zero rejected; all eight protocol-validated captures written. The old target rejected seven of eight in each of two completed runs.
- Project-only removal preview: recorded Cmd before and Cmd after; zero provider hook changes; Apply false. The old target planned null.

## Additional process controls

The same harness was extended only in ignored scratch, retaining its original eight-case sequence. A process-local mode makes the synthetic executable either write no capture, write `{}`, write a protocol-valid Claude capture when Codex is requested, or remain alive past a 250 ms deadline without delivery. These controls exercise the actual current Verify method and cleanup path through reflection, not just a duplicate completion formula.

| Synthetic case | Observed | Required |
| --- | --- | --- |
| Valid Codex capture then immediate exit 0, eight repetitions | Eight accepted | Accept validated delivery |
| Exit 0 with no capture | Rejected, exit-code diagnostic | Reject |
| Exit 0 with invalid capture `{}` | Rejected, exit-code diagnostic | Reject |
| Exit 0 with valid wrong-provider capture | Rejected, exit-code diagnostic | Reject |
| Still-running runner past deadline, no capture | Rejected, timeout diagnostic | Reject and stop owned runner |
| Temporary directory cleanup | 12 logged child roots, zero remaining | Remove isolated probe data |

The direct completion-predicate fixture in the repository also covers final capture acceptance after normal exit, timeout rejection even with a valid file, wrong provider, invalid file and non-exited runner rejection. Source inspection confirms `timedOut` is set only when delivery was not observed, the runner is still alive and the deadline has passed. The final capture check does not accept from exit status alone. The analyzer did not run a precisely timed sub-millisecond deadline race experiment.

These are synthetic process fixtures. The fake executable does not dispatch a real provider hook or prove a shell adapter; no native CLI acceptance is inferred.

## Focused repository regressions

```text
dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~FastHookSetupTests&FullyQualifiedName!~OptIn)|FullyQualifiedName~HookDeliveryReporterTests|FullyQualifiedName~ProjectProfileTests' --verbosity minimal
```

Outside-sandbox result: **20 passed, 0 failed, 0 skipped**. Native opt-in checks were excluded; independent QA owns those and the full suite.

The new project-profile fixture actually applies the isolated project removal and checks exact global Codex hook bytes, selected Cmd and unknown manifest metadata. It then applies Claude-only setup and checks the same preservation, and finally performs full Codex removal and checks that only the relevant shell field clears while unknown metadata remains. This closes the distinction missing from the earlier fixture.

## Package and hygiene

- Independently hashed 0.1.9018 ZIP: `fe5e164d189c6e423d6d79c2115a291bbd81f64c6c7cd8716cf540910f327a79`.
- Independently hashed packaged ingress: `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707`.
- Both match the handoff; pre/post package source binding is recorded in developer evidence, not re-created by an analyzer product build.
- Before/after source identity matched; whitespace check passed; assigned-output hygiene scan found zero targeted personal/secret patterns.
- No product build, new benchmark, native provider trial, user installation/configuration edit or user-process operation was performed by the analyzer. Prior Core, metric and direct-shell evidence remains under its original report scope.
