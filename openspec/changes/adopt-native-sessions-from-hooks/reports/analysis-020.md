<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-020 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-020 (recheck of DEV-032)

## Review target

- Reviewer / role: Claude Code delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent). The client version is not visible to the subagent. Default permission mode. Model `claude-opus-5-5`.
- Date: 2026-11-05
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-020 (IDs ANALYSIS-1900 to 1999)
- Task and requirement references: task 2.5 (owned runtime); [implementation-032](../evidence/implementation-032.md); ANALYSIS-1800, 1801 and 1802 in [analysis 019](analysis-019.md); QA-2500 in [QA 025](qa-025.md) and QA-2601 in [QA 026](qa-026.md)
- Scope: the 8 assigned paths. Neighbouring code I read:
  - `src/BellTower/IO/ProcessTable.cs` (`Ancestors`, `Neighbors`)
  - `NativeAdoption.CrossCheck`, `Needs` and `ManagerBehindAgent`
  - the linked files in `src/BellTower.Ingress/BellTower.Ingress.csproj` and `src/BellTower.Protocol/BellTower.Protocol.csproj`
  - `Directory.Build.props`, `global.json`, `src/*/packages.lock.json`
  - `.github/workflows/validate.yml` and `scripts/package.ps1` (restore options only)
- Baseline commit: `4ccc963`
- Identity before: `head=4ccc963 baseline=4ccc963 files=8 diff=bef2c56154ec untracked=1/a51c86cc3803`. The tracked diff SHA-256 is `bef2c56154ec6addb6e96ad27d06309b5b551e967fa6294ac81ab54cb5e9482f`, and implementation-032 hashes to `993c089b…`. This matches the assignment.
- Identity after: `head=4ccc963 baseline=4ccc963 files=8 diff=bef2c56154ec untracked=1/a51c86cc3803`. It is identical to the before line. `git status` shows the same entries, including the untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/`, and no `packages.lock.json` change.
- Prior reports and rechecked findings: [analysis 019](analysis-019.md) (ANALYSIS-1800, 1801, 1802)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Is the Release build current? | Compared output and source timestamps | `bell-tower.dll` was built at 09:50:07 and `bell-tower-ingress.dll` at 09:50:03, both after `AttachmentCapture.cs` (09:50:00). `BellTower.Protocol.dll` (09:48:02) is after the Core and Protocol edits (09:46:38). `BellTower.Tests.dll` (09:48:05) is after the test edits (09:47:53) | Current. Nothing was rebuilt; the tests ran with `--no-build` |
| ANALYSIS-1800 policy probe (re-run) | A scratch console app referencing copies of the Release `bell-tower.dll` and `BellTower.Protocol.dll`. It runs `Apply`, then `Promote`, then `Reconcile` with a live, matching owned-runtime record (Codex, mode unknown, managed) | 1. Lineages after the Hosts cut: the nested direct child `[]` gives `bound=False shared-runtime`; the nested process behind a tool shell `[tool]` gives `bound=False shared-runtime`. 2. Real backends: `[manager]`, `[node, manager]`, `[powershell, manager]` and `[node, powershell, manager]` give `bound=True managed`. 3. Depth 4 is shared. 4. Control: the uncut tool-shell lineage `[tool, backend, manager]` gives `bound=True`. `AttachmentEvidence` no longer has an `AgentParentIsAgent` property | Both nested shapes are shared once Hosts cuts the lineage, and all four launch shapes still bind. The control shows that Core alone does not reject this shape: the Hosts cut is the only guard. That matches the design: Hosts rejects, and Core keeps the record-based proof |
| ANALYSIS-1800: the Hosts cut on the tool-shell shape | Focused tests (below); source reading of `AttachmentCapture.cs:66-86` | The 4 `throughToolShell: True` runs (event and ingress, both hook orders) pass with real stand-ins for manager, backend `codex.exe`, `powershell.exe` and nested `codex.exe`. Given the control above, the nested-first runs can pass only if the cut removed the manager from the nested process's lineage. The developer's mutation agrees: with the cut removed, the 4 nested-first runs fail | Verified. I did not run a mutation; QA-027 owns that |
| Does the cut only reject? | Source reading | The cut is a `break` in the walk, so it can only shorten the lineage. `ManagerBehindAgent` still matches the manager by pid and creation time, with the per-hop time check. `AgentParent` is `chain[0]`, so it is null when the direct parent is a provider image. The profile-shell proof compares it with a recorded shell, which is never a provider image | Verified: nothing is accepted by image name |
| Wire compatibility | `git tag --contains e0a69e6`; `git merge-base --is-ancestor e0a69e6 main`; repository search | The DEV-031 field `AgentParentIsAgent` is in no tag and not on `main`, so no released producer or consumer ever carried it. No reference remains in `src`, `tests`, `docs` or `openspec/specs`. The Protocol and ingress binaries contain no `AgentParentIsAgent` text (ASCII and UTF-16 scan) | No compatibility concern. The extra key on every record (analysis 019) is gone |
| Regression: `Reconcile` complexity | Read `NativeAdoption.cs:183-206` | The owned-runtime condition is back to 8 conjuncts, the same shape as at DEV-030. `Apply` lost its extra parameter, and `Verified` (`:227-232`) clears three fields | Simpler than DEV-031. No finding |
| Regression: duplication between the Hosts cut and the CrossCheck relation reads | Read `AttachmentCapture.cs:57,68,78` and `ProcessTable.Neighbors` | Both look for a provider image above the agent, but they answer different questions. `Neighbors` reads only the direct parent, adds a creation-time check, and runs only when `Needs` is not `None` (mode shared-backend or client). The cut walks the ancestor list Hosts already holds, up to `MaxOwnedDepth` hops, and skips the `Identify` call for the hop it cuts. The owned path runs exactly when the mode is `unknown`, so it cannot reuse the mode-gated read. The `provider + ".exe"` expression now appears three times in the file | No finding. The repeated expression is cosmetic |
| NativeAOT and the source generator | Read the linked files in the Ingress and Protocol projects; checked the build output; scanned the metadata | `AttachmentCapture.cs` and `NativeAdoption.cs` are linked into the `PublishAot` ingress; the change removes an out parameter and adds a string comparison, with no reflection and no new types. `AttachmentEvidence.cs` is linked into Protocol (`IsAotCompatible`). A property was removed, so the generated metadata shrinks. `CaptureProtocol.MaxAncestors` is a `const int`. The ingress and Protocol were built after the edits with `TreatWarningsAsErrors` | Compatible. No native publish was run |
| Focused tests and runtime | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter <4 classes>` with TRX, while QA ran in parallel | 137/137 passed in 31 s. By class: `NativeOwnedRuntimeProcessTests` 36 runs, 31.7 s; `CaptureTests` 47 runs, 3.8 s; `NativeAdoptionReconcileTests` 26 runs, 2.3 s; `OwnedRuntimeReconcileTests` 28 runs, about 0 s. The 8 nested runs took 0.9 to 2.2 s each (about 9.6 s in total). The 2 new policy rows passed | Passed, with the counts in implementation-032. The class ran faster than in analysis 019 (52 s for 32 runs), but both were single runs under different parallel load, so this is not a measured improvement |
| Test duplication | Read the diff of `NativeOwnedRuntimeProcessTests.cs:37-80,149-196,270-360` | `WriteWaitThenHook`, `ManagerStart` and `WriteRecord` are class-local, and both theories use them. The tool-shell variant adds one `tool.cmd` line and a conditional start string, not a third copy of the setup | Resolved. No finding |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 (17 s) | Passed (whole solution, dirty tree) |
| Lock files and the SDK pin (read-only) | Read `global.json`, the SDK's `Microsoft.NETCoreSdk.BundledVersions.props`, the lock files and `obj/project.assets.json`; `git log` of `global.json` and the lock files; read `validate.yml` and `package.ps1` | See ANALYSIS-1900 | Real drift, outside this change |
| Parity | Inspection plus the test rows | The cut uses the hook's own provider image. The lineage and parent are stored only on provisional evidence, and Claude captures are never provisional, so a cut at `claude.exe` changes nothing. The Claude rows of the stand-in class pass. A Codex agent under a Claude process is not cut, because the image differs. Codex is covered by the probe, the policy rows and the stand-in rows, including two simultaneous Codex sessions | No parity gap introduced |

## Findings

### ANALYSIS-1900: The lock files are stale against the pinned SDK, so a locked restore under `global.json` would fail

- Severity: warning (outside this change; recorded because the assignment asked)
- Related task / requirement: build hygiene; none in this change
- Locations:
  - `global.json`: SDK `10.0.114`, `rollForward: disable`
  - The `packages.lock.json` files in `src/BellTower`, `src/BellTower.Ingress`, `src/BellTower.Launcher` and `src/BellTower.Protocol`: `Microsoft.NET.ILLink.Tasks` is requested as `[10.0.13, )` and resolved to `10.0.13`
  - `.github/workflows/validate.yml:26` and `scripts/package.ps1:112`: `--locked-mode`
- Observation:
  - `Microsoft.NET.ILLink.Tasks` is the reference the SDK adds implicitly to the AOT and trimming projects, so its version comes from the SDK. In SDK 10.0.114, `Microsoft.NETCoreSdk.BundledVersions.props` sets `KnownILLinkPack` for `net10.0` to `10.0.14`.
  - Commit `bc3545d` (2026-11-04) moved `global.json` from 10.0.113 to 10.0.114. The lock files last changed in `0a7445a` (2026-10-23) and still say 10.0.13.
  - The local `obj/project.assets.json` files already resolve `10.0.14`, so any implicit restore rewrites the lock files. That is what the developer saw and reverted.
  - CI installs the SDK from `global.json` and then runs `dotnet restore --locked-mode`. Release packaging also restores in locked mode. A locked restore fails when the lock file does not match the project's dependencies, so both are expected to fail at the pinned SDK.
  - `bc3545d` is not on `main` yet, so CI has not run this combination.
  - Developer builds that use `--no-restore` are not affected.
- Proposed improvement and benefit:
  - In a separate maintenance change, regenerate the lock files with SDK 10.0.114, using a forced restore that re-evaluates them.
  - Check that only the ILLink entries change.
  - Commit the regenerated files before or together with the `global.json` pin reaching `main`.
  - Run `dotnet restore --locked-mode` once to confirm.
  - This keeps CI and release packaging working.
- Behavior-preservation risks: build tooling only. ILLink 10.0.14 is the linker of the pinned SDK, and the Release build already uses it.
- Evidence type: source and tooling observation (the SDK manifest, the lock files, the assets files and the Git history). Not measured: I did not run a locked restore, because the assignment did not allow a restore.
- Verification / measurement plan: after the lock files are regenerated, `dotnet restore --locked-mode` at the pinned SDK exits 0, and `git diff` shows only the four ILLink entries.
- Relationship: follow-up to the lock-file note in implementation-032; independent of task 2.5

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1800 | `head=4ccc963 baseline=4ccc963 files=8 diff=bef2c56154ec untracked=1/a51c86cc3803` | 1. The re-run policy probe shows both nested lineages shared after the Hosts cut, and the native, npm, shim and shim+node backends bound. 2. The 4 tool-shell stand-in runs pass in both hook orders and through both entry points, alongside the 4 direct-child runs. 3. The wire field, the `Apply` parameter, the `Reconcile` conjunct and the extra clearing are gone, and nothing references them; the field was never released. 4. The cut only shortens the lineage, and Core's proof is unchanged | verified. QA-027 owns the correctness verdict and the mutation check |
| ANALYSIS-1801 | same | `CaptureProtocol.cs:72` defines `MaxAncestors = 32`, and both clauses use it (`:85`, `:89`). The comment now calls it a sanity cap equal to the envelope cap and says that policy reads at most `MaxOwnedDepth` hops. The `CaptureTests` cap rows pass (47/47) | verified |
| ANALYSIS-1802 | same | Both theories use the class-local `WriteWaitThenHook`, `ManagerStart` and `WriteRecord`, and the tool-shell row adds no third copy of the setup. 36/36 class runs pass | verified |

## Assessment and limitations

- DEV-032 resolves ANALYSIS-1800, 1801 and 1802. Cutting the lineage covers both nested shapes with less code than DEV-031, and the legitimate launch shapes still bind.
- No regression was found in:
  - `Reconcile` complexity;
  - the split of work between the Hosts cut and CrossCheck;
  - AOT and source-generator compatibility;
  - test duplication and runtime.
- ANALYSIS-1900 is a real lock/SDK drift outside this change. It will break CI's locked restore and release packaging once the 10.0.114 pin reaches `main`, so it needs a separate maintenance follow-up before that merge.
- QA-2601 (real Codex layouts with a `codex.exe` directly above the backend) is unaffected by the cut. Such a backend would now have an empty lineage and stay shared, as it already was at `e0a69e6`.
- Checks:
  - Focused tests: 137/137 passed, with `--no-build`.
  - Formatting: clean.
  - Release build: current, not rebuilt.
- Not performed:
  - a mutation run (QA-027 owns it);
  - a native AOT publish;
  - a locked restore (the assignment did not allow a restore);
  - real `claude` or `codex`, the live instance, and model services.
- Writes:
  - My write of this report was refused, so it is returned inline for the orchestrator to save.
  - The probe project and the TRX are in the session scratch folder. Building the probe restored only that scratch project.
  - I touched no process I did not start, no stand-in process remains, and nothing was committed.
