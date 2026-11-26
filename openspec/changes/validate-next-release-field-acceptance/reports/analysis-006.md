> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125) after the reviewer's write was refused.
> Reviewer: ANALYSIS-006 code-analyzer subagent; client Claude Code 2.1.332 (delegated subagent, role default); model claude-opus-5-5.
> Refusal message as reported by the reviewer: "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-006

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.332 / delegated subagent, role default / `claude-opus-5-5`
- Date: 2026-11-25T09:48Z to 2026-11-25T09:58Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-006 (task 2.1, developer DEV-003)
- Task and requirement references:
  - [tasks.md](../tasks.md) 2.1, plus fitness for 2.2 to 2.7
  - text rechecks of 2.1, 3.4 and 4.4
  - ledger row U1
  - [implementation-003.md](../evidence/implementation-003.md) and [harness-2.1.md](../evidence/harness-2.1.md)
- Scope:
  - `tools/BellTower.FieldAcceptance/**` (Isolation, Harness, Evidence, Scenarios, `Program.cs`) and `scripts/field-acceptance.ps1`
  - `tests/BellTower.Tests/FieldAcceptance/**`, `tests/BellTower.Tests/BellTower.Tests.csproj` and its lock file
  - `ZeusApps.BellTower.slnx`, `docs/development.md`, `docs/distribution-validation.md`
- Neighbors:
  - tools and test helpers: `tools/BellTower.Smoke`, `tools/BellTower.Ingress.Bench`, `tests/BellTower.Tests.FakeLauncher`, `tests/BellTower.Tests/Testing/TestWorkspace.cs`, `tests/BellTower.Tests/Fixtures/native/*`
  - scripts: `scripts/update-acceptance.ps1`, `scripts/package.ps1`
  - product paths the harness drives:
    - `src/BellTower.Protocol/HookStartTask.cs`, `HookTrayStart.cs`, `TrayProcess.cs`
    - `src/BellTower/Setup/HookStartTaskSetup.cs`, `ProviderHookProbe.cs`
    - `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `src/BellTower/Providers/CodexSharedEndpoint.cs`, `src/BellTower/IO/ProviderTitleState.cs`
    - `src/BellTower/Updates/UpdateTransaction.cs`
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence` -> `Summary: head=02d4f56 baseline=02d4f56 files=35 diff=d616f2d8b7aa untracked=29/6a7369bdd8fe`. This matches the assignment.
- Before/after identity: the same command after the analysis printed the same summary line. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-004.md](analysis-004.md) (ANALYSIS-060), [analysis-005.md](analysis-005.md) (ANALYSIS-080, -081, -082)

Source references are repository-relative at the reviewed identity. The harness files are untracked, so their line numbers refer to the working tree captured by the identity above.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture | Read `IScenario`, `ScenarioCatalog`, `RunContext`, `Instance`, `AgentSession`, `FakeAgent`, `TowerReader` and the scenario; compared them with the text of tasks 2.2 to 2.7 | The layering is sound. Scenarios never start processes; every start goes through `Instance` or `AgentSession` and the guard. The per-provider modeling (hook shell, payload, settings path, image path) is private helper code inside the one scenario | ANALYSIS-107 |
| Product policy in the tool | Searched the harness for re-implemented product rules | The harness reads product outputs (`doctor`, `list`, identity stores, `integration.json`). It calls product APIs for staging (`UpdateTransaction.Stage`), config (`ConfigStore`) and hook-start state (`HookTrayStart.ClearUserStop`). It re-implements no policy. Writes that current source makes into the 0.1.9 data are not labeled | No duplicated policy. The labeling gap is ANALYSIS-106 |
| Duplication | Compared with Smoke, Ingress.Bench, FakeLauncher, `TestWorkspace` and the native fixtures | Smoke (run by CI, linear, cmd-copy agents) and FakeLauncher (stands in for the tray) serve other purposes. Not reusing them was the right call. Two neighbors are worth reusing: `tools/BellTower.Ingress.Bench`, which already does the 2.3 load measurement, and `tests/BellTower.Tests/Fixtures/native/*`, which holds captured provider payloads and process ancestry | ANALYSIS-104, ANALYSIS-107. No consolidation with Smoke or FakeLauncher recommended |
| Safety: guard | Read `PathRelation`, `ProductionGuard`, `ProductionLocations`, `GuardSelfTest` and their tests. Ran a junction probe with PowerShell 7.6.10 on .NET 10.0.15, in a scratch directory outside the repository | Measured: `DirectoryInfo(<base>\link\child).ResolveLinkTarget(true)` returns null when the junction is an intermediate component. It resolves only when the junction is the last existing component. `Path.GetFullPath("C:\PROGRA~1")` returned the long name. The layout and executable checks are allow-lists; the other argument checks are substring deny-lists | ANALYSIS-103 |
| Safety: environment | Traced the hook-started tray route in the product | `TrayProcess.Start` builds the tray's environment block for the desktop shell's token (`CreateEnvironmentBlock`). It does not copy it from the hook | ANALYSIS-100 |
| Safety: Task Scheduler | Read `HookStartTask`, `HookStartTaskSetup`, `Instance.Dispose`, the `finally` block in `Program.Execute` and the snapshot diff | The task name is `HookStart-` plus a 16-character hash of the upper-cased data path, so it cannot collide with the real data directory's task. Cleanup runs only in the managed `finally` block | ANALYSIS-101 |
| Safety: snapshot | Read `ProductionSnapshot.Diff` | Only production processes that disappeared count as a change. New ones do not | ANALYSIS-102 |
| Process trees | Read `Instance.Run`, `AgentSession.Dispose`, `FakeAgent.RunChild` and the sweep in `Instance.Dispose` | Timeouts use `Kill(true)`. The sweep finds processes by image path under the sandbox root, so it also catches the hook-started tray that runs under explorer. Nothing runs on Ctrl+C or a crash | Adequate in a normal run. The interruption gap is part of ANALYSIS-101 |
| Fake agents | Compared the shell and payload models with `tests/BellTower.Tests/Fixtures/native/*.json` and the archived `fix-codex-hook-command-shell` evidence | Archived native Codex 0.164.0 ran hooks through `pwsh.exe`, with ancestry `pwsh.exe, codex.exe, codex.exe`. The harness uses Windows PowerShell 5.1 and one `codex.exe`. Its payloads omit `transcript_path`, `model` and `permission_mode` | ANALYSIS-104 |
| Evidence honesty | Read every check predicate in the scenario, and the sanitizer | Several checks are self-fulfilling or are aggregates of other checks. The stale Codex captures use a modeled cmd fallback and the check does not say so | ANALYSIS-104, -105, -106 |
| Performance | Read the wait and poll code. Timed two read-only operations in PowerShell on this machine | Deterministic from the code: the stale hook-start check waits 2 x 8 s. Measured in PowerShell, not in the harness: enumerating 377 processes with `MainModule` took 56 ms, and `schtasks /query /fo csv /nh` took 761 ms. The harness stores no per-command timing | ANALYSIS-108. The breakdown of the 70 s runtime was not measured |
| Parity | Checked that each check has matched claude, codex and mixed rows | Every functional check has claude, codex and mixed rows; the Codex command-form check is Codex-only by nature. The parity weakness is how faithfully Codex is modeled, not missing coverage | ANALYSIS-104 |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~FieldAcceptance"` | 28 passed, 0 failed | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <21 harness and harness-test .cs files>` | exit code 0 | passed |
| Harness run | Not run | A run registers a task in the real Task Scheduler and, by product design, starts hook-started trays with the real profile environment (ANALYSIS-100). The findings rest on source, archived evidence and the committed run summary | not performed (deliberate) |

## Findings

### ANALYSIS-100: Hook-started trays run with the user's real profile environment, not the sandbox environment

- Severity: warning
- Related task / requirement:
  - 2.1 isolation: "verify the harness never writes production settings or data"
  - 2.7: plain shared-backend `codex` plus "a stopped tray started by the first hook"
  - parity of provider-home resolution
- Locations:
  - `src/BellTower.Protocol/TrayProcess.cs:208-216` (the doc comment says the environment block is built for the shell's token, not copied from the hook) and `:242` (`CreateEnvironmentBlock(out environment, token, false)`)
  - `src/BellTower.Protocol/HookTrayStart.cs:130-136` (the shell route first, then the recorded task as fallback; the task also runs with the user's own environment)
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs:38,59` (shared access uses `CodexSharedEndpoint.DefaultHome`)
  - `src/BellTower/Providers/CodexSharedEndpoint.cs:32-34` (`CODEX_HOME`, otherwise `%USERPROFILE%\.codex`)
  - `src/BellTower/IO/ProviderTitleState.cs:46-48` (reads the provider settings under the resolved home on every tick)
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:41-46` (the `isolation-environment` detail: "Child processes get a sandbox USERPROFILE ...") and `:287-313` (the hook-started tray probe)
  - the "Isolation" bullet in `docs/distribution-validation.md` ("no other variables are inherited ... The one effect outside the sandbox directories is the on-demand tray-start task")
- Observation:
  - In the `current/hook-started-tray` probes the tray is a child of the desktop shell, and its environment comes from the shell's token:
    - `USERPROFILE`, `APPDATA`, `LOCALAPPDATA` and `TEMP` are the real user's;
    - `CLAUDE_CONFIG_DIR` and `CODEX_HOME` are the user's global values, if any;
    - only `--data-dir` is sandboxed, because it is on the command line.
  - That tray stays up until `Instance.Dispose`. For its whole lifetime it reads provider titles from the real `~/.claude` and `~/.codex` and writes its temp files to the real `%TEMP%`.
  - In 2.1 the Codex shared-backend opener fails closed: the plain Codex attachments are `unknown` (stale) or `embedded-runtime` (current), and `Select` requires `SharedBackend`. That conclusion comes from reading the source; I did not measure it.
  - Task 2.7 puts plain shared-backend `codex` and the hook-started tray in the same installed instance. A hook-started tray would then return `CodexAccess.Shared(<real ~/.codex>)` and could connect to the user's live Codex shared backend.
  - Neither the guard nor the snapshot can see any of this. Reads do not touch watched files, and a socket connection changes no file.
  - The evidence and the docs claim the opposite: that every child gets the allow-listed environment, and that the task is the only effect outside the sandbox.
  - The run does not record which route started the tray. A fallback to the scheduled task, which also uses the real environment, would pass the same check.
- Proposed improvement and benefit:
  - Harness, now:
    1. In the hook-started tray check, read the detail in `last-attempt.txt` and require `started-via-shell` (`TrayProcess.RouteShell`). Fail on a task route.
    2. Right after the check, exit that tray and start a new one with `start --silent` under the sandbox environment, so later steps never run a tray with the real environment.
    3. State in the `isolation-environment` detail, the evidence notes and `docs/distribution-validation.md` that hook-started trays run with the real profile environment by product design. List this as a second effect outside the sandbox.
  - Before 2.7: never combine a shared-backend Codex fixture with a hook-started tray until either a product seam or a sandbox Windows user exists. The run's Task Scheduler and desktop shell belong to the user, so only a separate user would also isolate these routes.
  - Product (described only, needs a decision): should the shell route carry a short allow-list of Bell Tower-relevant variables from the hook, namely `CODEX_HOME` and `CLAUDE_CONFIG_DIR`? This is also a correctness and parity question for real users. Someone who sets `CODEX_HOME` only in the terminal that runs Codex gets a hook-started tray that resolves a different Codex home than the session that triggered it. The same applies to `CLAUDE_CONFIG_DIR`.
- Behavior-preservation risks:
  - The harness changes affect only the probe. The restart must keep the stale-state "no tray" expectation intact.
  - The product option touches design decision 10 (token and job inheritance). It needs its own proposal and must not copy arbitrary hook variables.
- Evidence type: source observation for the environment route; hypothesis for the 2.7 connection to a live backend, which was not exercised
- Verification / measurement plan:
  - After the change, the detail of the hook-started tray check names `started-via-shell`.
  - A bounded probe in a disposable instance: start a tray from a hook and, while it runs, check which settings file it recorded in `provider-titles.json` in the sandbox data. A real-profile path there shows the exposure without connecting to anything.
- Relationship: none

### ANALYSIS-101: Task and tray cleanup runs only on the managed path, and leftovers from an interrupted run are invisible to the next run

- Severity: warning
- Related task / requirement: 2.1 isolation, and every later scenario (fault injection in 2.4 and the soak in 2.5 make interruptions likelier)
- Locations:
  - `tools/BellTower.FieldAcceptance/Program.cs:130-155` (cleanup in `finally`: one loop with no per-instance guard; `Finish` runs after it)
  - `tools/BellTower.FieldAcceptance/Harness/Instance.cs:222-267` (`TaskExists` and `Run` can throw `Win32Exception` or `IOException` outside the catch around `exit`)
  - `tools/BellTower.FieldAcceptance/Isolation/ProductionSnapshot.cs:58-86` (`Diff` excludes only the current run's own task paths; a task present both before and after is not a change)
  - `src/BellTower.Protocol/HookStartTask.cs:25-38` (the name is `HookStart-` plus a hash of the upper-cased data path; any folder name of `[A-Za-z0-9._-]` is accepted)
  - `src/BellTower/Setup/HookStartTaskSetup.cs:76-95` (`Register` and `Unregister` already take a `folder` parameter)
- Observation:
  - Any of these skips the remaining instances and `Finish`: Ctrl+C, a closed console, a killed `dotnet`, or an exception thrown from one instance's `Dispose`.
  - The sandbox task `\ZeusApps.BellTower\HookStart-<hash>` then stays in the user's Task Scheduler. Silent sandbox trays keep running, including the hook-started one under explorer.
  - The next run takes its "before" snapshot with that task already present, so the leftover never shows as a change and is never removed. Leftovers pile up silently.
  - Name collision: impossible, because the hash covers the full data path. The sandbox task does share the user's `\ZeusApps.BellTower\` folder, though. If the user has no Bell Tower task, `schtasks /delete` leaves an empty folder behind, and the snapshot does not record it.
- Proposed improvement and benefit:
  1. At the start of every run, before the "before" snapshot, sweep leftovers the harness owns:
     - `\ZeusApps.BellTower\HookStart-*` tasks whose action, read with `schtasks /query /tn <task> /xml`, runs `tray --data-dir` under `artifacts\field-acceptance\`;
     - processes whose image is under `artifacts\field-acceptance\runs\`.
     Report each removal in the isolation section.
  2. Wrap each instance's cleanup in its own try/catch, and write `result.json` even when cleanup fails.
  3. Register `Console.CancelKeyPress` and `AppDomain.ProcessExit` handlers that run the same cleanup.
  4. Product (described only): a setup option or environment variable that sets the existing `folder` parameter, for example to `\ZeusApps.BellTower.FieldAcceptance\`.
     - The ingress already accepts any valid folder through `IsBellTowerTask`, so only setup changes.
     - Sandbox tasks would never share the user's folder, and the sweep becomes "remove that folder".
     - A switch that skips task registration is the weaker choice, because it drops the fallback route from coverage.
- Behavior-preservation risks:
  - The sweep must match only actions under the harness artifacts base, never a task whose data directory is elsewhere.
  - The product option must keep the default folder.
  - `doctor` and `setup --remove` must still find recorded tasks in another folder. They read the record, so they should.
- Evidence type: source observation
- Verification / measurement plan:
  - Unit-test the sweep's selector on invented task XML: a harness path, a production path and an unrelated path.
  - In a disposable run, press Ctrl+C after `setup --host-bridge`, then confirm the next run removes the leftover and reports it.
- Relationship: none

### ANALYSIS-102: The production snapshot does not flag production processes that start during a run

- Severity: warning
- Related task / requirement: 2.1 "verify the harness never writes production settings or data"; the isolation check in every scenario
- Locations:
  - `tools/BellTower.FieldAcceptance/Isolation/ProductionSnapshot.cs:87-90` (only `before.Processes.Where(process => !after.Processes.Contains(process))`)
  - `tests/BellTower.Tests/FieldAcceptance/ProductionSnapshotTests.cs:91` (only the "process stopped" case is tested)
- Observation:
  - The most likely visible sign of a leak is a new production process: a hook or `start` resolving to the production launcher, or the user's production tray being started.
  - A production process that appears during the run is not treated as a gating change.
  - A process that starts and exits between the two snapshots is invisible either way.
- Proposed improvement and benefit: also gate on a production-image process that is present after the run but not before, compared by pid plus start time. Optionally sample after each scenario so short-lived processes are likelier to be caught. This adds the missing direction of the check.
- Behavior-preservation risks: if the user restarts their own tray during a run, the run fails. That is a safe false positive; a rerun clears it.
- Evidence type: source observation
- Verification / measurement plan: a unit test that adds a production process to the "after" snapshot and expects a gating change.
- Relationship: none

### ANALYSIS-103: The guard resolves links only on the last existing path component, and its argument checks are substring deny-lists

- Severity: suggestion
- Related task / requirement: 2.1 isolation
- Locations:
  - `tools/BellTower.FieldAcceptance/Isolation/PathRelation.cs:7-38` (`Canonical` calls `ResolveLinkTarget(true)` once, on the deepest existing component) and `:52-57` (`Mentions` is a literal substring test)
  - `tools/BellTower.FieldAcceptance/Isolation/ProductionGuard.cs:124-134` (arguments other than `--data-dir`) and `:150-152` (`IsHome` requires an exact match)
  - `tests/BellTower.Tests/FieldAcceptance/ProductionGuardTests.cs` (no junction, UNC or relative-path cases)
- Observation:
  - Measured with a scratch junction: `ResolveLinkTarget` returns null for `<base>\link\child`, where `link` is a junction and `child` exists. So once a sandbox subdirectory exists, a junction higher up that points into production is not resolved.
  - The production side has the same gap when a whole profile folder is moved behind a junction above the guarded root, for example `AppData` moved to another drive.
  - The allow-lists still hold, and any unusual path form fails closed against them:
    - sandbox paths must be under the artifacts base;
    - executables must be in the sandbox or be one of the four allowed tools;
    - `--data-dir` must be under the sandbox data directory.
  - Other arguments are checked only by literal substring against production paths: `--home`, `--checkout` and hook command strings. Environment values are checked the same way. None of these forms would match:
    - a relative path;
    - a `\\?\` prefix;
    - an admin-share UNC path such as `\\localhost\C$\...`;
    - an 8.3 short name. `GetFullPath` expands short names only in the path it parses, and `Mentions` compares the raw text.
  - Today the harness builds these arguments itself from sandbox paths, so the exposure is to future scenario code, not to the 2.1 run.
- Proposed improvement and benefit:
  - Canonicalize with `GetFinalPathNameByHandle` on the deepest existing ancestor (opened with backup semantics), or resolve each ancestor in turn. Canonicalize the production roots the same way.
  - For path arguments the harness controls (`--home`, `--checkout`, `--data-dir`), require them to be under the sandbox root, as `--data-dir` already must be, instead of only deny-listing production.
  - Add tests for an intermediate junction, a `\\?\` path, an admin-share UNC path and a relative path.
- Behavior-preservation risks: allow-list-only arguments could reject a legitimate future argument outside the sandbox, such as a system tool path. Add such allowances explicitly.
- Evidence type: measured result for the `ResolveLinkTarget` behavior; source observation for the rest
- Verification / measurement plan: the new unit tests. The junction test needs a temporary junction (`mklink /J`, or `Directory.CreateSymbolicLink` for directories) that `Dispose` removes.
- Relationship: none

### ANALYSIS-104: The Codex hook model diverges from the archived native evidence, and the stale Codex captures use an unlabeled cmd model

- Severity: warning
- Related task / requirement: 2.1 (U1, "0.1.9-image captures still accepted" for both providers), 2.2, 2.3, 2.7; the 4.4 release notes; provider parity
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:8-16` (the enum doc says Windows PowerShell is "the shell the Codex hook runner selects by default ... archived native evidence") and `:87-96`
  - `tools/BellTower.FieldAcceptance/Harness/FakeAgent.cs:51-62` (the probe always uses `System32\WindowsPowerShell\v1.0\powershell.exe`)
  - `tools/BellTower.FieldAcceptance/Harness/Tooling.cs:6-15` (no `pwsh`)
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:368-370` (payloads), `:375` (stale Codex uses `Cmd`) and `:203-211` (the stale Codex captures and the mixed "14 of 14")
  - `tests/BellTower.Tests/Fixtures/native/codex-0.164.0-*.json` and `claude-2.1.310-*.json`
  - `openspec/changes/archive/2026-10-28-fix-codex-hook-command-shell/evidence/native-investigation-002.md` and `-003.md`
- Observation:
  - Shell edition:
    - In the archived probes, Codex 0.164.0 ran hooks through PowerShell 7. Its resolver searches `PATH`, then the fixed PowerShell 7 path, then Windows PowerShell.
    - The captured Codex ancestry is `pwsh.exe, codex.exe, codex.exe`, with two `codex.exe` levels.
    - The harness always uses Windows PowerShell 5.1 with a single fake `codex.exe`.
    - The product's setup probe picks the first shell through which the fake Codex delivers the hook, so the `current/codex-command-form` result follows directly from this model.
  - Payloads:
    - The harness builds minimal payloads by hand. They lack `transcript_path` (both providers) and `model` and `permission_mode` (Codex), all of which appear in the native fixtures.
    - The product reads `transcript_path` (`src/BellTower/Providers/HookIngestor.cs:37`, `src/BellTower.Protocol/CaptureFactory.cs:49`).
    - The harness also adds `turn_id`, which the Codex 0.164.0 fixture does not contain.
    - The naming and identity checks in 2.2 depend on these fields.
  - Stale captures:
    - The stale Codex captures run through `cmd`. The handoff calls this a model of a Codex that falls back to cmd.
    - The committed evidence rows say only "through Cmd", and the mixed row reports "14 of 14 accepted".
    - Under the archived default shell, all 7 stale Codex captures would fail. `stale/codex-command-form` shows the parser error, but the capture rows suggest the opposite U1 outcome for Codex.
  - Claude:
    - The Git Bash model agrees with the archived evidence: Claude Code 2.1.310 delivered hooks through Git Bash.
    - The native ancestry `bash.exe, bash.exe` matches only if `Git\bin\bash.exe` starts `usr\bin\bash.exe`. I did not verify that.
- Proposed improvement and benefit:
  1. Move the per-provider hook model into one harness adapter (see ANALYSIS-107).
     - Codex shell discovery should follow 0.164.0: a PowerShell found on `PATH`, else `pwsh` 7 at its fixed path, else Windows PowerShell, else cmd.
     - Add `pwsh` to `SystemTools`.
     - Optionally run the Codex captures under both PowerShell editions.
  2. Build payloads from the native fixtures: their `payload` object, with `session_id`, `cwd` and `transcript_path` pointing into the sandbox. Drop the invented `turn_id` unless a native capture shows it.
  3. Rework the stale-state Codex rows:
     - make the primary Codex capture check run under the default shell and expect it to fail (the known defect, which is U1's real Codex outcome);
     - label the cmd captures in their detail as a "modeled cmd fallback";
     - have the mixed row report both counts.
     This puts the conclusion "a setup rerun is required for Codex to deliver hooks at all" into the same evidence that the 4.4 release notes will cite.
- Behavior-preservation risks:
  - `pwsh` may be missing on some machines. The resolver must then fall back as Codex does, and the evidence must name the edition used.
  - Report expected-failure checks separately from harness failures.
- Evidence type: source observation plus archived native evidence
- Verification / measurement plan:
  - The result shows which shell executable ran each Codex capture.
  - A stale-state run shows the default-shell Codex captures failing with the parser error and the labeled cmd fallback passing.
  - A unit test compares the payload builder's field names with the native fixtures.
- Relationship: extends the handoff's own "Findings to carry" on the Codex shell model

### ANALYSIS-105: Several checks are self-fulfilling, tautological or aggregates counted as passes

- Severity: suggestion
- Related task / requirement: 2.1 evidence; all later scenarios
- Locations:
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs`:
    - `:41-46` (`isolation-environment` compares the values that `BuildEnvironment` just assigned)
    - `:268-269` (the mixed `native-adoption` row checks only that two rows are listed, while its detail claims they are "evaluated per provider by the same shared policy")
    - `:314-316` (`inactive-features` aggregates the preceding rows)
  - `tools/BellTower.FieldAcceptance/Evidence/EvidenceSanitizer.cs:54-64` (`ContainsPrivateValue` re-tests the same patterns `Sanitize` just replaced)
  - `tools/BellTower.FieldAcceptance/Scenarios/RunContext.cs:34-45` and `Passed` in `Evidence/RunReport.cs` (no list of expected checks)
- Observation:
  - `isolation-environment` cannot fail unless the harness contradicts itself. The hook-started tray also shows its claim is not true for every child (ANALYSIS-100).
  - The sanitizer check fires only if a replacement re-creates a match. So the guard that "refuses to write if a private value remains" cannot catch a value the sanitizer does not already know, such as an 8.3 short profile path, a user folder on another drive or a domain name.
  - The two `inactive-features` rows and the mixed `native-adoption` rows count toward "56 of 56" without independent evidence.
  - A scenario that skips a branch simply reports fewer checks and still passes.
- Proposed improvement and benefit:
  - Base `isolation-environment` on observed values: have the fake agent report the environment keys and home values it actually received, or use the installed launcher's resolved provider homes where `doctor` reports them.
  - After sanitizing, run an independent leak scan for:
    - a drive-rooted `\Users\<name>` not followed by a placeholder;
    - `~1` path segments;
    - the raw user and host names, case-insensitively, in any context.
  - Mark aggregate rows as summaries and leave them out of the pass count.
  - Give each scenario a manifest of expected check IDs, so a missing check fails the run.
- Behavior-preservation risks: a stricter leak scan may reject current evidence, for example an OS description or a public repository URL that contains a matched word. Allow-list such values explicitly.
- Evidence type: source observation
- Verification / measurement plan:
  - A unit test where the sanitizer misses a short-form profile path and the leak scan catches it.
  - A scenario test where a missing check ID fails the run.
- Relationship: ANALYSIS-100 (the environment claim)

### ANALYSIS-106: The candidate's provenance is not tied to the reviewed source, and the script hard-codes the fixture label (ANALYSIS-060 residual for 4.4)

- Severity: suggestion
- Related task / requirement: 2.1 (fixture-version label), 4.4 (harness rerun on the synchronized package), 3.4
- Locations:
  - `scripts/field-acceptance.ps1:47-52` (reuses an existing candidate unless `-RebuildCandidate` is passed), plus `:49` and `:61-63` (the label `fixture-version candidate` and the `-FixtureVersion` build command are fixed strings)
  - `scripts/package.ps1:78` (a fixture build's `informationalVersion` is the plain version, with no commit or dirty marker)
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:54` (current-source `ConfigStore` writes `config.json` into the data directory before the 0.1.9 tray starts) and `:102-107` (staging through the current-source `UpdateTransaction.Stage` compiled into the harness)
- Observation:
  - A run silently reuses whatever `bell-tower-0.1.11-win-x64.zip` sits in the shared `artifacts/update-acceptance/packages`.
  - Neither the descriptor nor the launcher's `--version` carries a source revision, so the evidence cannot show the candidate was built from the reviewed tree. As tasks 2.2 to 2.7 land fixes, a stale candidate becomes likely.
  - For 4.4, the harness can be pointed at the synchronized release-commit package with `-CandidateArchive`. The evidence would still say "fixture-version candidate, built with `scripts/package.ps1 -FixtureVersion 0.1.11`".
  - The 0.1.9 data is not purely written by 0.1.9. `config.json` (muted, update checks off) and the staging step come from current source.
  - The coordinator that activates the update is the candidate payload's, as in the field. A real 0.1.9 tray, however, stages with 0.1.9 code, so this step is modeled and should be labeled. Smoke uses the same pattern.
- Proposed improvement and benefit:
  - Record the HEAD commit, the dirty state of the package inputs and the archive build time beside the candidate, for example in a stamp file the script writes after `package.ps1`. Rebuild, or refuse, when the stamp does not match the current tree.
  - Derive the label and build command from that stamp or from parameters: a `-CandidateLabel`, or "release-commit package" when the archive comes from `artifacts/releases`.
  - Label `config.json` and the staging step as current-source fixture steps in the scenario notes.
- Behavior-preservation risks: always rebuilding by default costs a package build per run. A stamp check keeps reuse when the tree is unchanged.
- Evidence type: source observation
- Verification / measurement plan:
  - The run summary names the candidate's source revision and dirty state.
  - A run with a modified `src/` file and an old candidate either rebuilds or refuses.
- Relationship: residual of ANALYSIS-060 (labeling of the 4.4 harness rerun)

### ANALYSIS-107: Readiness of the foundation for 2.2 to 2.7: provider modeling, thread safety, timing data and the existing ingress benchmark

- Severity: suggestion
- Related task / requirement: 2.2, 2.3, 2.5, 2.7; parity (one per-provider model)
- Locations:
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:319-397` (scenario-local `Fire`, `Payload`, `Accepted`, `InstalledShell`, `SettingsPath`, `ImagePath`, `WaitForAttachment` and `CreateWorkDirectory`, plus repeated `provider == Claude ? ... : ...` branches)
  - `tools/BellTower.FieldAcceptance/Harness/Instance.cs:21-22` (`CommandLog` and `Sessions` are plain `List<>`) and `:151-178` (`WithProcessEnvironment` swaps the whole process environment)
  - `Add` in `Evidence/RunReport.cs` (an unsynchronized `List`)
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:97-105` (hooks run one at a time per session; the agent's own `HookResponse.Milliseconds` is discarded)
  - `Harness/TowerReader.cs:41-55` (`list` polled every 500 ms)
  - `tools/BellTower.Ingress.Bench/Program.cs` (per provider: 120 concurrent samples at parallelism 8, a shell-echo baseline, median, p95, p99 and durable counts)
- Observation:
  - Structure:
    - The `IScenario`, `ScenarioCatalog` and `Instance` layering fits new scenarios well.
    - The provider-specific pieces every scenario needs sit inside one scenario class: shell, payload, settings file, owned-command lookup and the acceptance predicate.
    - Tasks 2.2 to 2.7 would either copy them or grow their own provider branches. That is the duplicated per-provider policy the parity rules warn against.
  - 2.3, parallel hooks:
    - The API is synchronous and single-threaded.
    - Parallel hooks across sessions would race on `CommandLog`, `Sessions` and `RunReport.Checks`.
    - `WithProcessEnvironment` cannot overlap with anything else.
  - 2.3, timing:
    - The harness times the JSON round trip but discards the agent's own measurement, and it persists no per-command elapsed time.
    - `list` polling every 500 ms, with a launcher start per poll, cannot resolve a 3 s durable-completion bound.
    - `Ingress.Bench` already counts durable captures and computes the 2.3 statistics against an isolated package, although it provisions through in-process setup rather than the installed `setup`.
  - 2.5: polling processes cannot catch a short-lived `git.exe` for the "no Git launches" check. The harness puts `fake-bin` first on `PATH`, so a counting `git` shim would work if the product finds Git through `PATH`. I did not check that.
- Proposed improvement and benefit:
  1. Before 2.2, extract a `ProviderModel` (claude, codex) in `Harness/`. It holds the shell resolution (ANALYSIS-104), the payload builder based on the native fixtures, the settings path, the owned-command lookup and the acceptance predicate.
  2. Make the report, the command log and the session list thread-safe. Keep `WithProcessEnvironment` single-threaded by contract.
  3. Persist raw samples for 2.3: in-agent and round-trip milliseconds per hook, plus each command's `Elapsed` in `result.json`.
  4. For 2.3, reuse the Bench's sampling and statistics instead of writing a second statistics implementation. Either move them into a small shared source file, or port them into a FieldAcceptance load scenario that drives the installed hooks through parallel fake sessions. Measure durable completion from capture files, as the Bench does.
  5. For 2.5, consider a counting `git` shim in `fake-bin` after confirming how the product finds Git.
- Behavior-preservation risks:
  - Refactoring the helpers must keep all 56 check IDs and their predicates.
  - Sharing code with the Bench must not change its output or its isolated-trial rules.
- Evidence type: source observation; Git resolution and polling resolution are hypotheses
- Verification / measurement plan:
  - After step 1, a 2.1 run gives identical check IDs and outcomes.
  - For 2.3, the result holds at least 100 raw samples per provider, and the statistics can be recomputed from them.
- Relationship: ANALYSIS-104 (content of the provider model)

### ANALYSIS-108: About 16 s of the run is fixed negative waiting, and the rest of the runtime is not attributed

- Severity: suggestion
- Related task / requirement: 2.1 runtime; run lengths in 2.4 and 2.5
- Locations:
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:303` (`WaitFor(TrayRunning, stale ? 8000 : 25000)`)
  - `tools/BellTower.FieldAcceptance/Harness/TowerReader.cs:38,57-68` (each poll starts `doctor`, every 300 ms)
- Observation:
  - In the stale state the tray never starts, so each provider's probe waits the full 8 s. The code thus spends a fixed 16 s of the reported 67 s run (09:45:35 to 09:46:42).
  - Each `WaitFor(TrayRunning)` iteration starts a full `doctor` process.
  - Two read-only timings in PowerShell on this machine, not inside the harness: enumerating processes with `MainModule` took 56 ms, and the full `schtasks` listing took 761 ms. At two snapshots per run, neither is a bottleneck.
  - The other ~50 s cannot be attributed, because the harness stores no per-command timing (ANALYSIS-107).
- Proposed improvement and benefit:
  - The stale negative check can use a short bound, for example 2 s, plus an immediate record check. The hook process has already exited when the probe starts waiting, and the candidate image writes `last-attempt.txt` before it returns. This saves about 12 s per run.
  - Store per-command elapsed time so further tuning rests on measurements.
- Behavior-preservation risks: a 0.1.9 image that started a tray asynchronously after the hook exited would be missed with the shorter bound. 0.1.9 has no hook-start route, so this is unlikely. Keep the bound configurable.
- Evidence type: source observation for the fixed waits; measured result for the two PowerShell timings, taken outside the harness; hypothesis for the rest of the time breakdown
- Verification / measurement plan: compare `startedAt` and `finishedAt` over two runs before and two after the change, using the stored per-command timings.
- Relationship: ANALYSIS-107 (timing data)

## Re-verification

All four rechecks ran against the same target: `head=02d4f56 baseline=02d4f56 files=35 diff=d616f2d8b7aa untracked=29/6a7369bdd8fe`, with the text of [tasks.md](../tasks.md) 2.1, 3.4 and 4.4.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-060 | 2.1 names the `-FixtureVersion 0.1.11` candidate under `artifacts/update-acceptance/`, its label, and that it is never the release candidate. 4.4 requires rerunning `update-acceptance.ps1 -BaselineArchive <real 0.1.9>` and the harness scenarios on the synchronized package. 3.4 has a runnable real-baseline check. The harness implements the 2.1 path (`scripts/field-acceptance.ps1:45-52`) | verified: the text is workable and unambiguous. Code-level residual: the harness labels every candidate as a fixture-version candidate, so the 4.4 rerun would be mislabeled (ANALYSIS-106) |
| ANALYSIS-080 | 3.4 now uses option (b): `package.ps1 -FixtureVersion 0.1.11 -OutputDirectory artifacts/update-acceptance/packages`, then `dotnet run --project tools/BellTower.Smoke -c Release --no-build -- <real 0.1.9 zip> <0.1.11 fixture zip>`. Smoke is in `ZeusApps.BellTower.slnx`, so the 3.4 Release build produces what `--no-build` needs. Versions 0.1.9 and 0.1.11 satisfy Smoke's distinct-version assert. The output path follows `package.ps1`'s fixture rule. The `-BaselineArchive` script run moved to 4.4, where the declarations read 0.1.11 | verified |
| ANALYSIS-081 | 4.4 now says the rerun "covers the release-commit build, while the tag CI run of the packaged update acceptance is the check on the published bytes and its log is retained" | verified |
| ANALYSIS-082 | 2.1 now says the fixture candidate "is installed only under disposable harness roots, never into a real installation; live checks use `-Dev` builds". The harness extracts it only into the guarded sandbox `install` directory (`Instance.ExtractInstallation`, behind the layout guard) | verified |

Regression check on the rechecked text: apart from ANALYSIS-106, I found no contradiction between 2.1, 3.4, 4.4, the harness script and `docs/distribution-validation.md` on candidate version, location or label.

## Assessment and limitations

The foundation is sound in shape:
- Scenarios run on a guarded `Instance`, and the layout, executables and data directory are all allow-listed.
- The baseline is the real published release, checked by hash.
- Production is fingerprinted before and after each run.
- Claude, Codex and mixed rows run throughout.
- The tool duplicates no product policy, and Smoke and FakeLauncher are rightly not reused.

The main problems are outside the harness's own process tree:
- Hook-started trays run with the real profile environment by product design. For 2.7's shared-backend Codex, such a tray could reach the user's live Codex backend (ANALYSIS-100).
- Task and tray cleanup is not guaranteed when a run is interrupted, and the next run cannot see the leftovers (ANALYSIS-101).
- The snapshot misses production processes that start during a run (ANALYSIS-102).

The Codex hook model and payloads should follow the archived native evidence before 2.2, 2.3 and 2.7 build on them. The stale Codex rows also need labels so that U1 reads correctly for the release notes (ANALYSIS-104).

The remaining items improve evidence strength, provenance and the foundation: ANALYSIS-103, -105, -106, -107 and -108. ANALYSIS-107 recommends reusing `tools/BellTower.Ingress.Bench` for 2.3 rather than writing a second load generator.

Not performed:
- A harness run. It registers a real Task Scheduler task and starts trays with the real environment (ANALYSIS-100), and QA owns functional runs.
- Any measurement of the 70 s breakdown beyond the fixed waits.
- Any live provider check.

Measurements are limited to the junction and short-name probe and two read-only PowerShell timings, all labeled above.

Writes and identity: no repository writes; the report write was refused, so this text is inline. The scratch junction was removed, and empty probe directories remain in the session scratchpad outside the repository. The review identity is unchanged.

Decisions needed: the product options in ANALYSIS-100 and ANALYSIS-101 are design questions for the orchestrator and the user. The orchestrator owns triage and ledger status.
