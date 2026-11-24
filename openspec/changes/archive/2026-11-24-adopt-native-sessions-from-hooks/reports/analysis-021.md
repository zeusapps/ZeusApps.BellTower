<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-021 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: "Subagents should return findings as text" (as reported by the reviewer). -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-021 (DEV-033 lock-file refresh)

## Review target

- Reviewer / role: Claude Code delegated subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent). The subagent cannot see the client version. Default permission mode. Model `claude-opus-5-5`.
- Date: 2026-11-05 (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-021 (IDs ANALYSIS-2000 to 2099)
- Task and requirement references: ANALYSIS-1900 in [analysis 020](analysis-020.md); DEV-033 in [findings](findings.md); [implementation-033](../evidence/implementation-033.md); design decision 7 and task 6.6 (the scheduled drift job), for the safeguard question only
- Scope:
  - In the target:
    - the seven `packages.lock.json` files in `src/BellTower`, `src/BellTower.Ingress`, `src/BellTower.Launcher`, `src/BellTower.Protocol`, `tests/BellTower.Tests`, `tools/BellTower.Ingress.Bench` and `tools/BellTower.Smoke`
    - `evidence/implementation-033.md`
  - Neighbouring files I read:
    - `global.json`, `Directory.Build.props`, `ZeusApps.BellTower.slnx` and the project files
    - `src/BellTower.Distribution/packages.lock.json`
    - `.github/workflows/validate.yml` and `.github/workflows/release.yml`
    - `scripts/package.ps1` (restore block only)
    - `README.md`, `docs/development.md`, `docs/compatibility.md`, and the maintainer checklist in `docs/releases.md`
- Baseline commit: `3e5eccf`
- Implementation identity (before): `head=3e5eccf baseline=3e5eccf files=8 diff=2e017f3d81de untracked=1/35edf158061c`. The tracked diff SHA-256 is `2e017f3d81de6849894a528c87febfb6c5280079da688c2c2bbdd78678fa5ec1`, and implementation-033 hashes to `2029e1772f3f…`. This matches the assignment.
- Before/after identity: after the analysis, the same command printed `head=3e5eccf baseline=3e5eccf files=8 diff=2e017f3d81de untracked=1/35edf158061c`, which is identical. `git status --short` shows the same entries as before, including the untracked `verify-codex-repository-subagents/reports/` and `verify-signed-releases-and-update-policy/`.
- Prior reports / rechecked findings: [analysis 020](analysis-020.md) (ANALYSIS-1900, the source of DEV-033). This is not a formal recheck of 1900. QA-028 owns the restore, build and AOT verdict.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Is the diff minimal? | `git diff -- '*.lock.json'`, `git diff --check` | 27 insertions and 27 deletions. For the three packages the SDK adds implicitly, only `requested`, `resolved`, `contentHash` and one transitive dependency version change. The other changes are six project-reference range lines. No entry is added, removed or reordered. `System.CommandLine`, `Tomlyn` and all other entries are untouched. The whitespace check is clean | Minimal |
| What changed in each file | Diff inspection | `Microsoft.NET.ILLink.Tasks` moves from 10.0.13 to 10.0.14 in the four projects that use AOT or trim analysis (BellTower, Ingress, Launcher, Protocol). In Ingress only, `Microsoft.DotNet.ILCompiler` (in both the `net10.0-windows7.0` and `/win-x64` sections) and `runtime.win-x64.Microsoft.DotNet.ILCompiler` also move from 10.0.13 to 10.0.14. In Tests, Bench and Smoke, the `bell-tower` project entry changes `BellTower.Distribution` and `BellTower.Protocol` from `[0.1.8, )` to `[0.1.9, )` (2 lines in each file). `src/BellTower.Distribution/packages.lock.json` has no packages and is unchanged | Matches the project graph. The implicit references are `PrivateAssets=all`, so they do not appear in Tests, Bench or Smoke, even though Tests references Ingress |
| Are versions and hashes the same in every file? | Compared every occurrence. Compared each hash with the package's `.nupkg.metadata` in the NuGet global packages folder (`%USERPROFILE%\.nuget\packages`) | ILLink 10.0.14 has the same hash `YlTamXpp…AHkQ==` in all four files. ILCompiler 10.0.14 has the same hash `CDQ/+XRT…NXg==` in both Ingress sections. The runtime ILCompiler hash is `B28xsyrO…Nw==`. All three match the cached `contentHash` of the 10.0.14 packages, whose recorded source is `api.nuget.org`. The removed 10.0.13 hashes also match their cached 10.0.13 values. No tracked product lock file still contains `10.0.13` or `[0.1.8, )` | Consistent. The hashes are the real nuget.org content hashes |
| Do the versions match the pinned SDK? | Read `sdk/10.0.114/Microsoft.NETCoreSdk.BundledVersions.props` of the installed SDK | For net10.0, `KnownILLinkPack` has `ILLinkPackVersion="10.0.14"` and `KnownILCompilerPack` has `ILCompilerPackVersion="10.0.14"`. `BundledNETCoreAppPackageVersion` is 10.0.14 and `NETCoreSdkVersion` is 10.0.114 | The new lock versions are exactly the versions the SDK adds implicitly |
| Do the project-reference lines match `Directory.Build.props`? | `Directory.Build.props:7` (`<Version>0.1.9</Version>`); `git log` of the props file | For a project reference, NuGet writes `[<Version of the referenced project>, )`. The version moved to 0.1.9 in `a6b5cac`, which is on `main`. Tag `v0.1.9` points at the next commit, `aadbfb3`. The lock files last changed in `0a7445a`, before the version bump | The 0.1.9 lines match the props file. The 0.1.8 lines had been stale since `a6b5cac` |
| Why didn't locked mode flag the stale 0.1.8 lines? | Read `src/NuGet.Core/NuGet.ProjectModel/ProjectLockFile/PackagesLockFileUtilities.cs` in NuGet.Client (main at `9a3254f3ed`, fetched read-only through `gh api`). The installed SDK bundles NuGet 7.0.3. Ran `gh run list --workflow validate.yml` | `HasP2PDependencyChanged` compares the package dependencies of a referenced project by version range. For project references it only checks that the name exists. Line 526 says `// Do not check the version for the projects, or else https://github.com/nuget/home/issues/7935`. CI agrees: the `Validate` run on `aadbfb3` (push to `main`, Version 0.1.9, lock lines still 0.1.8, `dotnet restore --locked-mode`) ended in `success` | Explained: NuGet ignores project-reference versions in lock validation on purpose. Any restore without locked mode rewrites them, which is why they appeared in DEV-033. See ANALYSIS-2001 |
| How does an SDK pin change reach the lock files? | `git log` of `global.json` and the lock files; `git show aadbfb3 bc3545d`; read `validate.yml`, `release.yml` and `scripts/package.ps1:112-118`; `git rev-list --count origin/main..main` | The same NU1004 failure has now happened twice:<br>1. `a6b5cac` failed CI on 2026-10-27. With `rollForward: latestPatch`, CI picked up a newer SDK whose packs were 10.0.14.<br>2. `aadbfb3` fixed it by pinning 10.0.113 with `rollForward: disable`. Its commit message says "the SDK and lock files now move together".<br>3. `bc3545d` then moved the pin to 10.0.114 without regenerating the lock files.<br>No doc, script or test in the repository requires a lock refresh when `global.json` changes. CI runs the locked restore only on pull requests and pushes to `main`. Local `main` is 12 commits ahead of `origin/main`, so this drift would only have shown up at the next push | Process gap. See ANALYSIS-2000 |
| Does this fit decision 7 / task 6.6? | Read decision 7 in `design.md` and task 6.6 in `tasks.md` | The scheduled drift job is limited to provider CLI drift: Claude Code and Codex help output, the Codex protocol schema and the no-quota probe. With `rollForward: disable`, the SDK never changes on its own; only a deliberate `global.json` edit changes it | Do not fold the lock check into 6.6. See ANALYSIS-2000 |
| Docs | `git grep` for `global.json`, `locked-mode`, `packages.lock`, `force-evaluate` and `NU1004`, outside archives and reports | Only these mention restore or lock files:<br>- `README.md` (`dotnet restore --locked-mode`)<br>- `docs/installation.md` (offline locked restore)<br>- `validate.yml` and `package.ps1`<br>`docs/development.md` ("Formatting and checks") says only "After restoring dependencies". `docs/compatibility.md` row 17 records the observed SDK 10.0.114, which is correct. Step 2 of the checklist in `docs/releases.md` bumps `Directory.Build.props` and runs a locked restore, which still passes when the project-reference lines are stale | The procedure belongs in `docs/development.md` and `docs/releases.md`. `docs/compatibility.md` is an evidence matrix and needs no procedure. See ANALYSIS-2000 and 2001 |
| Is the evidence accurate? | Compared implementation-033 with the diff | "ILLink.Tasks … (7 files)" is really 4 files. "(3 lines each)" is really 2 lines in each of 3 files. The 27/27 total, "Distribution unchanged" and the cause given for the deviation are correct | See ANALYSIS-2002 |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` | exit 0 (12 s) | Passed (whole solution, dirty tree) |
| Parity | Inspection | Build tooling only. No provider code, policy or capability changed | No parity impact for Claude Code or Codex |
| Restore, build, AOT publish | Not run, per the assignment | QA-028 owns them. Implementation-033 reports that the native link step failed locally because of the environment | Not performed |

## Findings

### ANALYSIS-2000: Changing the SDK pin does not require refreshing the lock files, and the drift has now happened twice

- Severity: warning. This is a process gap. It does not block DEV-033, which fixes the current instance.
- Related task / requirement: ANALYSIS-1900, DEV-033; build hygiene
- Locations:
  - `global.json` (the pin, `rollForward: disable`)
  - `docs/development.md:54-62` ("After restoring dependencies:" with no restore command)
  - `.github/workflows/validate.yml:25-26` (`dotnet restore --locked-mode`, run only on `pull_request` and on `push` to `main`)
  - `.agents/roles/project-facts.md`, Commands section (build and test use `--no-restore`; there is no restore check)
- Observation:
  - The SDK sets the versions of the implicit `Microsoft.NET.ILLink.Tasks` and `Microsoft.DotNet.ILCompiler` references (`KnownILLinkPack` and `KnownILCompilerPack`). So every SDK servicing update changes the lock files. `aadbfb3` explained this, but only in its commit message.
  - `bc3545d` repeated the failure eleven days later.
  - Nothing local fails loudly. Local workflows build with `--no-restore`, and an implicit restore silently rewrites the lock files; the developer saw this and reverted it in implementation-032.
  - CI does fail loudly, but only when the branch reaches a PR or `main`, and `main` is currently pushed in batches.
  - The NU1004 message names the package, not the SDK pin, so the CI log does not point at the cause.
  - Task 6.6 cannot help. The pinned SDK never changes on its own, and the job covers only providers.
- Proposed improvement and benefit (a proposal only, ordered by cost):
  1. In `docs/development.md` ("Formatting and checks"), show the restore command explicitly (`dotnet restore --locked-mode`) and add one rule. When `global.json` changes:
     - run `dotnet restore ZeusApps.BellTower.slnx --force-evaluate`;
     - commit the lock files in the same commit;
     - confirm that `dotnet restore --locked-mode` exits 0.

     `docs/compatibility.md` needs no change.
  2. Owned by the orchestrator: in `.agents/roles/project-facts.md`, require reviewers to run `dotnet restore --locked-mode` when the target touches `global.json`, `Directory.Build.props`, a project file or a lock file. QA already does this for DEV-033; this makes it standing practice.
  3. Optional, within the existing CI and with no new job: add a failure hint to the "Restore locked dependencies" step. For example, an `if: failure()` step after it could print: "The lock files do not match the SDK in global.json or the project files; regenerate them with `dotnet restore ZeusApps.BellTower.slnx --force-evaluate`."
  4. Only if the drift happens again: a fast unit test that compares the ILLink and ILCompiler `resolved` versions in the lock files with the SDK's pack versions captured at build time (for example, the `@(KnownILLinkPack)` metadata written into the test assembly). This catches the drift in `dotnet test --no-restore` before a push. It needs a small MSBuild target and depends on SDK item metadata, so it is not the minimal step.
  - Keep task 6.6 limited to providers. With `rollForward: disable`, security patches need a manual pin bump. If the maintainers want to be told when a new SDK servicing release is out, that is a separate scheduled check whose issue text points to rule 1. It is not part of 6.6's acceptance.
- Behavior-preservation risks:
  - Options 1 to 3 carry none; they are documentation and a diagnostic step.
  - Option 4 could fail falsely if a future SDK shipped ILLink at a version different from its pack metadata. That is why it reads the SDK items instead of assuming the runtime version.
- Evidence type: source and history observation (commits `a6b5cac`, `aadbfb3` and `bc3545d`, the CI run list, and the workflow and docs text). Not measured.
- Verification / measurement plan:
  - After rule 1 lands, rehearse a pin bump on a scratch branch: edit `global.json` and follow the rule. `dotnet restore --locked-mode` exits 0, and `git diff` shows only the implicit pack entries.
  - For option 3, a PR with a deliberately stale lock file shows the hint in the step log.
- Relationship: process follow-up to ANALYSIS-1900; independent of the native adoption tasks

### ANALYSIS-2001: A release version bump leaves the project-reference lines stale without any failure

- Severity: suggestion
- Related task / requirement: the DEV-033 deviation; the release checklist
- Locations:
  - `docs/releases.md:62` (checklist step 2)
  - `Directory.Build.props:7`
  - the `bell-tower` project entry in:
    - `tests/BellTower.Tests/packages.lock.json:106-113`
    - `tools/BellTower.Ingress.Bench/packages.lock.json:15-22`
    - `tools/BellTower.Smoke/packages.lock.json:15-22`
- Observation:
  - NuGet writes `[<Version>, )` for project references but does not check it in locked mode (`PackagesLockFileUtilities.cs:526` in NuGet.Client; nuget/home#7935). CI passed on `aadbfb3` with Version 0.1.9 and lock lines at 0.1.8.
  - The stale lines then come back as unrelated diffs the next time anyone restores without locked mode, as happened in DEV-033, and reviewers have to explain them.
  - Checklist step 2 runs "locked restore", which cannot catch this.
  - Outside this target: the same version bump also left `README.md:20` pointing at `bell-tower-0.1.8-win-x64`.
- Proposed improvement and benefit: in step 2 of `docs/releases.md`, after synchronizing the version, run `dotnet restore ZeusApps.BellTower.slnx --force-evaluate` and commit the lock files with the bump. This is the same command as rule 1 of ANALYSIS-2000. It keeps the lock files canonical, so later reviews see only intended diffs.
- Behavior-preservation risks: none. NuGet ignores these lines during validation, so the refresh changes no restore result. `scripts/package.ps1 -Dev` passes `-p:Version` only to publish, so dev builds do not change the lock files and need no change.
- Evidence type: source observation (NuGet.Client) plus a measured CI result (`Validate` on `aadbfb3`: success)
- Verification / measurement plan: at the next version bump, `git diff` after `--force-evaluate` shows exactly the six project-reference lines, and `dotnet restore --locked-mode` exits 0.
- Relationship: explains the DEV-033 deviation; complements ANALYSIS-2000

### ANALYSIS-2002: implementation-033 gets the per-file scope of the lock diff wrong

- Severity: suggestion (evidence accuracy)
- Related task / requirement: DEV-033
- Locations: `openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-033.md:13` and `:16`
- Observation:
  - Line 13 says ILLink.Tasks changed "(7 files)". It changed in 4: BellTower, Ingress, Launcher and Protocol. Tests, Bench and Smoke changed only their project-reference lines.
  - Line 16 says "(3 lines each)". Each of those 3 files changed 2 lines.
  - Correct as written: the 27/27 total, the Ingress-only ILCompiler change, "Distribution unchanged" and the cause given for the deviation.
- Proposed improvement and benefit: correct the two counts before the evidence is committed (the file is still untracked), so the ledger and the QA cross-checks rely on the right scope.
- Behavior-preservation risks: none (evidence text only)
- Evidence type: source observation (`git diff`)
- Verification / measurement plan: the per-file `git diff --stat` for the lock files matches the corrected text.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-1900 (for information; not assigned as a recheck) | `head=3e5eccf baseline=3e5eccf files=8 diff=2e017f3d81de untracked=1/35edf158061c` | 1. The lock versions equal the SDK 10.0.114 pack versions.<br>2. The hashes equal the nuget.org content hashes.<br>3. No product lock file still contains `10.0.13`.<br>4. Analysis 020 predicted that only the four ILLink entries would change. The diff also covers the Ingress ILCompiler entries, which analysis 020 missed, and the six stale project-reference lines, which are explained above | Consistent with the fix. The locked-restore verdict belongs to QA-028 |

## Assessment and limitations

- The lock-file diff is minimal and consistent:
  - each package has one version and one content hash, the same everywhere it appears;
  - the versions match the pinned SDK's pack versions;
  - the hashes match the cached nuget.org hashes.
- The 0.1.9 project-reference lines correctly reflect `Directory.Build.props`. Locked mode ignores them by NuGet's design, and CI history confirms this.
- The root cause is a process gap, not tooling. Nothing documents or checks that the SDK pin and the lock files must change together, and the drift has happened twice.
- The minimal safeguard is a documentation rule plus a standing reviewer restore check, optionally with a CI failure hint. Add a unit test only if the drift happens again. Task 6.6 should stay limited to providers.
- Checks:
  - Passed: `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`.
  - Not performed, per the assignment: restore, build, tests and the NativeAOT publish (QA-028 owns them).
  - Not used: real `claude` or `codex`, model services or the live instance.
- Writes:
  - My write of this report was refused, so it is returned inline for the orchestrator to save.
  - The NuGet source file was fetched read-only into the session scratch folder.
  - I started only `git`, `gh`, `dotnet format` and the identity script, and none of them is still running.
  - Nothing was committed.
