# Implementation 001: install-dev-builds

Developer handoff for tasks 1.1 to 6.2. Baseline `b74a12b`, branch `add-claude-repository-subagents`, nothing committed, `tasks.md` not ticked.

## Target identity

`head=b74a12b baseline=b74a12b files=24 diff=d7215fdec6d9 untracked=8/78c150b1a77e` (`scripts/review-identity.ps1 -Baseline b74a12b` over the changed paths listed below).

## Changed files

Modified:
- `src/BellTower.Distribution/ReleaseContract.cs` (new `ReleaseBuild` record, optional `Build` member that is omitted from JSON when null, `DevPatchStart`, `IsDevVersion`, `Effective`, `EffectiveVersion`, `IsDev`, build validation)
- `src/BellTower.Distribution/Installation.cs` (`Contract`, `EffectiveVersion`, `DisplayVersion`)
- `src/BellTower/Cli/ApplicationInfo.cs` (`Version` strips `-dev+commit`; `Display`, `TrayText`)
- `src/BellTower/Cli/ApplicationVersionAction.cs`, `Cli/UpdateCommand.cs`, `Diagnostics/DiagnosticsReporter.cs`, `Tray/TrayApplication.cs`, `Tray/OverviewForm.cs`
- `src/BellTower/Updates/UpdateDiscovery.cs`, `UpdateTransaction.cs`, `ReleaseOffer.cs`, `ReleaseClient.cs`
- `scripts/package.ps1`
- `docs/development.md`, `docs/releases.md`, `docs/installation.md`

New:
- `src/BellTower.Distribution/VersionDisplay.cs`
- `scripts/install-dev.ps1`
- `.claude/skills/install-dev-build/SKILL.md` and `.agents/skills/install-dev-build/SKILL.md` (byte-identical)
- `tests/BellTower.Tests/Updates/DevBuildTests.cs`
- `tests/BellTower.Tests/Scripts/PackageScriptTests.cs`, `InstallDevScriptTests.cs`, `InstallDevBuildSkillTests.cs`

Not changed: `src/BellTower.Launcher/Program.cs` and the launcher/bootstrap contract.

## Behavior summary

- Dev range is PATCH >= 9000. `release.json` gains optional `build` (`channel`, `commit`, `dirty`, `builtAt`, `baseVersion`). Stable contracts serialize without it; older readers ignore it.
- `VersionDisplay` is the single formatter: stable `0.1.8`; dev `0.1.9001 dev b74a12b (2026-10-24 20:07 UTC)` (`dirty` after the commit); unmarked `0.1.9001 dev (unmarked)`. The informational version (`0.1.9001-dev+b74a12b[.dirty]`) is the fallback.
- Surfaces using it: `bell-tower --version`, `update --status`, `update --check`, `doctor` (`version`), tray tooltip (dev only; stable stays "Bell Tower"), tray menu header, Sessions window title (dev only).
- `Installation.EffectiveVersion()` (dev selection counts as `build.baseVersion`; unmarked dev range counts as MAJOR.MINOR.0) is used by `UpdateDiscovery.Hint`, `update --check` and `UpdateTransaction.Stage`.
- `ReleaseOffer.EnsureStable()` rejects dev-range offers in the GitHub client, the cache hint and `Stage`. `Validate()` still accepts them so local packages can be extracted by the install script.
- `package.ps1`: `-Dev`, `-DevVersion`, `-TargetInstallation`, `-VsWherePath`, `-PlanOnly`, `-CheckToolchain`. It finds `vswhere.exe` and the Windows SDK itself, prepends the installer folder to PATH for child processes only, and restores PATH in `finally`. Stable mode refuses the dev range before building. Fixture mode is unchanged.
- `install-dev.ps1`: `Preflight`, `Switch`, `Rollback`, `Retire`; options as documented in `docs/development.md`.

## Check results

| Check | Result |
|---|---|
| `dotnet build -c Release --no-restore` | passed, 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | passed, 744/744 (baseline tests plus 25 `DevBuildTests` cases, 9 `PackageScriptTests`, 15 `InstallDevScriptTests` cases, 3 skill tests). One earlier full run failed 1 test (stack trace only showed async frames; I did not capture the name); 4 re-runs afterwards passed 744/744. Cause not identified, so treat as one unexplained flake. |
| `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <all changed/new .cs files>` | passed (exit 0) |
| `node --test vscode/service.test.js vscode/focus.test.js` | passed, 8/8 (`vscode/` unchanged) |

## Task evidence

- 1.1: `DevBuildTests`: classification of 0.1.8, 0.1.10, 0.1.8999, 0.1.9000, 0.1.9001; build metadata round trip and absence from stable JSON; older-shaped and future-field JSON validates; six invalid-metadata cases.
- 1.2: `DevBuildTests`: dev 0.1.9001 (base 0.1.8) lets stable 0.1.9 pass the superseded check in `Stage` (it proceeds to the missing archive and throws `FileNotFoundException`), stable 0.1.8 stays "already selected or superseded" for both a dev and a stable selection, `previous` may be a dev version and `Cleanup` retains it. `Hint` and `update --check` call the same `EffectiveVersion()`; they were not given their own unit test because `Hint` reads `Environment.ProcessPath` (see limitations).
- 2.1: `DevBuildTests` covers dev clean, dev dirty, unmarked, stable (no label), the informational fallback and `Installation.DisplayVersion`. Real package: see 6.1.
- 3.1: a real `package.ps1 -Dev` ran in a plain shell with no Visual Studio Installer folder on PATH and linked the NativeAOT ingress (log below). `-CheckToolchain` reports `D:\Tools\BellTowerBuildTools` and `D:\Windows Kits\10\`; with a bogus `-VsWherePath` it fails with a message naming Build Tools and the Windows SDK (`PackageScriptTests`).
- 3.2: `PackageScriptTests` (`-PlanOnly`): first number, skipping numbers present in the output folder and in the target installation, explicit `-DevVersion` accepted/taken/out of range/wrong MAJOR.MINOR, `-Version 0.1.9001` refused with "reserved development range", `-Dev` with `-Version`/`-FixtureVersion` refused, output confined to `artifacts/dev-builds`, descriptor content (channel, commit equals `git rev-parse --short=7 HEAD`, dirty flag, build time, base version, informational version with `.dirty` iff dirty), stable plan has `build: null`, fixture mode unchanged.
- 4.1/4.2: `InstallDevScriptTests` against disposable installations under `artifacts/unit-tests/` (fake tray via mutex plus presence file, `-NoTray -SkipProbe -Disposable`): preflight changes nothing (hash snapshot of installation and data); switch then rollback restores `current.json` and `state.json` bytes and leaves the launcher and original payload bytes unchanged; refused changed selection, refused tray that appeared after preflight, refused unverifiable owner at preflight, refused changed dev selection at rollback, refused stable-unmarked, stable-marked and dev-unmarked candidates, refused stale build unless `-AllowStaleBuild`, refused data the original cannot read (derived from the original contract), confinement of test-only switches and paths; retire: success for an idle dev payload, refusal for selected, previous, leased, stable, legacy without `-Legacy`, stable-at-or-below-base with `-Legacy`, success for legacy 0.1.10 with `-Legacy`, `-Legacy` rejected for a dev-range payload.
- 5.1: `InstallDevBuildSkillTests`: both copies exist and are byte-identical, a deliberate one-byte change in a temporary copy is detected (`Identical` returns false), required procedure and safety text is present. SHA-256 of both: `02d598c7ee1e17b335ce2625498def5602803ed0aa340fb9c031185c10a3f7bd`.
- 5.2: docs written. Link `development.md#development-builds` is the heading in `docs/development.md`; relative links `releases.md` and `development.md` resolve inside `docs/`. Parameter names in the docs and the skill were compared by hand with the `param(...)` blocks of both scripts.

## 6.1 Real package and disposable cycle

Command (plain shell): `pwsh -NoProfile -File scripts/package.ps1 -Dev`, exit 0.

- Package: `D:\Code\ZeusApps.BellTower\artifacts\dev-builds\bell-tower-0.1.9001-win-x64.zip` (107,938,485 bytes), plus `.zip.sha256` and `.release.json`.
- Version `0.1.9001`, build metadata `channel=dev, commit=b74a12b, dirty=true, builtAt=2026-10-24T20:07:11Z, baseVersion=0.1.8`. Archive SHA-256 `364780fcb855535725dcebd05de74e7f23f1947e7632c0993f306d5c7e616c98`. The tree was dirty because this work is uncommitted, and the package says so.
- The packaged launcher reported a version starting `0.1.9001 dev ` (script assertion passed).

Disposable full cycle (`artifacts\dev-cycle\20261024T200826Z`, real stable 0.1.8 package extracted as the installation, its own silent tray on a disposable data directory; none of the maintainer's paths used):

1. Preflight: `preflight-passed`, `trayRunning: true`, display `0.1.9001 dev b74a12b dirty (2026-10-24 20:07 UTC)`; this ran the real package probe.
2. Switch: `running-candidate`, selection `{version 0.1.9001, previous 0.1.8}`, new tray owned by `versions\0.1.9001\bell-tower.exe`, tray presence version `0.1.9001`.
3. Verification through the launcher with the disposable data directory: `--version`, `update --status` (running and selected both show the dev label) and `doctor` `version` all printed `0.1.9001 dev b74a12b dirty (2026-10-24 20:07 UTC)`.
4. Rollback: `running-original`, selection `{version 0.1.8}` (original bytes), tray restarted on 0.1.8, `--version` printed `0.1.8`, `update --status` showed `Running 0.1.8; selected 0.1.8.`.
5. Retire `0.1.9001`: `retired`, `versions` left with `0.1.8` only, a retire record written in the workspace.
6. The disposable tray was stopped with `exit --data-dir <disposable>`; a process listing showed nothing left under `artifacts\dev-cycle`.

Read-only Preflight on the maintainer installation (`install-dev.ps1 -Action Preflight`, defaults): `preflight-passed`, original `0.1.10`, candidate `0.1.9001`, `trayRunning: true`, `mutation: none`. `current.json` and `state.json` hashes and the installation file listing (path, size, timestamp) were identical before and after. The only writes were under `artifacts\dev-builds\operations\` (about 100 MB of extracted package plus `operation.json`). No Switch, Rollback or Retire was run against the real installation or data.

Not verified: the tray icon tooltip text and the Sessions window title were not looked at on screen (headless run). They use `ApplicationInfo.TrayText` and `ApplicationInfo.Display`, the same value that `--version` printed, but a person should confirm them once during the live switch.

## 6.2 Claude Code and Codex parity

- Both files exist and `cmp` reports them byte-identical (hash above, 6,432 bytes). The repository test enforces it.
- Claude Code 2.1.310: the skill appeared as `install-dev-build` in this session's available-skill listing after the file was created, with the same description, so Claude Code discovers `.claude/skills/install-dev-build/SKILL.md`.
- Codex 0.164.0: installed, and the Codex copy sits in `.agents/skills/install-dev-build/SKILL.md` beside the existing Codex OpenSpec skills. I did not run a live Codex session to confirm discovery (that would be a model call and I have no non-interactive "list skills" command), so Codex discovery is documented-location only and not verified live. The frontmatter uses only `name` and `description`.
- Both clients get one procedure, one script pair and the same safety rules; there is no provider-specific policy in this change.

## Design issues and decisions for the orchestrator

1. `-Legacy` cannot tell a legacy dev build from a released stable payload by its files. I added a guard beyond the spec: a legacy retire is accepted only for versions newer than the stable base of the selected build (`EffectiveVersion`). Stable payloads at or below it are refused even with `-Legacy`. This keeps "never retires stable payloads" true for the normal cases, but it is a heuristic. Please confirm or choose another rule.
2. The tray's existing `UpdateTransaction.Cleanup` already deletes unretained verified payloads every 15 minutes. After the first dev switch from `0.1.10` (previous `0.1.10`), `0.1.9` will be removed by the tray without `Retire`; after a later dev switch the old `0.1.10` goes the same way. The migration step in `design.md` (retire legacy payloads with `-Legacy`) is therefore mostly automatic. `Retire` stays useful for dev payloads that are neither selected nor previous but still retained by recovery references.
3. Scope additions not in tasks.md: `ReleaseOffer.EnsureStable()` (published offers in the dev range are refused); package.ps1 parameters `-TargetInstallation`, `-VsWherePath`, `-PlanOnly`, `-CheckToolchain`; install-dev.ps1 test-only switches `-Disposable`, `-NoTray`, `-SkipProbe`, plus `-AssemblyDirectory`. They are needed for testable, safe scripts. `ApplicationInfo.Version` now also strips a `-dev` suffix, because the tray presence and selection comparisons need the plain version.
4. The dirty flag only looks at `src`, `tests`, `scripts`, `vscode`, `docs`, `README.md`, `Directory.Build.props` and the solution file, so unrelated untracked OpenSpec notes do not mark a build dirty.
5. Dev build times are shown in UTC with a "UTC" suffix, which differs slightly from the example in design.md (`2026-10-24 21:30`).
6. Switch and Rollback restart a tray only when the preflight found one running (matching how updates treat `HadTray`); without a running tray they report `trayRestarted: false`. The design text says "restart the tray" unconditionally.
7. Rollback restores only `current.json`, `state.json` and `state.json.bak`, as designed: monitoring data written by the dev tray after the switch is replaced by the snapshot.
8. `package.ps1` copies all of `docs/` into the payload, including the untracked `docs/handoffs/` files, which is existing behavior and not changed here.
9. `Hint()`/`update --check` effective-version wiring has no dedicated unit test (it depends on `Environment.ProcessPath`); `Stage` and `EffectiveVersion` are covered.

## Safety record

No Switch, Rollback or Retire against `%LOCALAPPDATA%\Programs\ZeusApps.BellTower` or `%LOCALAPPDATA%\ZeusApps.BellTower`; only the read-only Preflight above. No tray or agent session of the maintainer was stopped (the cycle stopped only its own disposable tray). `~/.claude` and `~/.codex` untouched, `setup --apply` never run, no audio (trays ran with `--silent`). Nothing committed.
