<!--
Saved by the orchestrator. Reviewer: code-analyzer subagent (ANALYSIS-041), Claude Code 2.1.325, Agent SDK subagent, auto mode, model claude-opus-5-5.
The reviewer's native write to this path was refused; the reviewer reported: "The client refused the write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-041.md. The full report text is below for the orchestrator to save."
The return block and report text below are saved verbatim.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.325 (Agent SDK subagent, auto mode) / claude-opus-5-5
task / finding IDs: task 4.5 (DEV-054); ANALYSIS-4100 (warning), ANALYSIS-4101 (warning), ANALYSIS-4102 to ANALYSIS-4107 (suggestions)
changed or report paths: none written. The client refused the write to D:\Code\ZeusApps.BellTower\openspec\changes\adopt-native-sessions-from-hooks\reports\analysis-041.md. The full report text is below for the orchestrator to save.
checks: scripts/review-identity.ps1 before/after -> passed (identical); dotnet format --verify-no-changes -> not run (global.json pins SDK 10.0.114 with roll-forward disabled; only 10.0.115 and 9.0.321 are installed); build/test -> not run (per assignment, to avoid disturbing the concurrent QA outputs); latency -> not measured (developer measurements cited, everything else labeled as hypothesis)
target identity: before head=5e95554 baseline=5e95554 files=22 diff=aefda8372d4e untracked=7/4d14cbb776f7; after the same
limitations / decisions needed: the report write was refused and the report text is inline below. No elevation or job-object probe was run. Decisions needed: whether 4100 (elevated terminals break the user stop and start an elevated tray) and 4101 (the in-job fallback when breakaway is refused; Codex job unprobed and untested) must be fixed before trial 6.1 or recorded as capability-matrix limits; whether the environment part of 4104 is in scope.
next: orchestrator saves the report, triages 4100 and 4101 with the developer, defers or accepts the suggestions
```

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-041 (DEV-054, task 4.5)

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.325, Agent SDK subagent, non-interactive (auto) mode, model `claude-opus-5-5`
- Date: 2026-11-15T12:30Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, assignment ANALYSIS-041
- Task and requirement references: [tasks.md](../tasks.md) task 4.5; [design.md](../design.md) decision 9, decision 6 and the new risk line; [specs/session-tracking/spec.md](../specs/session-tracking/spec.md) requirement "Hook-triggered adoption of native sessions" and its three new scenarios; developer evidence [evidence/implementation-054.md](../evidence/implementation-054.md)
- Scope and relevant neighboring paths: the 22 manifest paths below. Neighboring paths read for context: `src/BellTower/Updates/UpdateTransaction.cs`, `src/BellTower.Launcher/Program.cs`, `src/BellTower.Distribution/Installation.cs`, `src/BellTower/Setup/ProviderHookProbe.cs`, `src/BellTower.Protocol/CaptureFactory.cs`, `scripts/install-dev.ps1`, `tests/BellTower.Tests/Hosts/ManagementOwnershipProcessTests.cs`, `docs/development.md`, all project files under `src/`
- Baseline commit: 5e95554
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 5e95554 -Paths src tests docs scripts ZeusApps.BellTower.slnx`
  - HEAD 5e955542e430397bbc82291395d00c48e5d7cba8, baseline the same
  - Manifest: 7 untracked (`src/BellTower.Protocol/HookTrayStart.cs`, `src/BellTower.Protocol/TrayProcess.cs`, `src/BellTower/Diagnostics/HookStartReporter.cs`, `tests/BellTower.Tests.FakeLauncher/{BellTower.Tests.FakeLauncher.csproj,Program.cs,packages.lock.json}`, `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs`), 15 modified (`ZeusApps.BellTower.slnx`, `docs/{compatibility,hook-ingress,installation}.md`, `src/BellTower.Ingress/Program.cs`, `src/BellTower/Cli/ExitCommand.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/IO/DataPaths.cs`, `src/BellTower/Setup/{IngressProvision,IntegrationRefresh}.cs`, `src/BellTower/Tray/{TrayApplication,TrayController,TrayHost}.cs`, `tests/BellTower.Tests/BellTower.Tests.csproj`, `tests/BellTower.Tests/Testing/BuiltApplication.cs`)
  - Tracked diff SHA-256 aefda8372d4e979eec798f65c90a36bd0443aea3b041528b9a1418a48b9a0261
  - Summary: `head=5e95554 baseline=5e95554 files=22 diff=aefda8372d4e untracked=7/4d14cbb776f7`
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=aefda8372d4e untracked=7/4d14cbb776f7`. It matches. No unexpected mutation.
- Prior reports / rechecked findings: none. This is a first analysis of DEV-054.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication | Read the tray-running, mutex-name, update-pending and tray-start paths | `HookTrayStart.MutexName` is now the only mutex-name computation (`DataPaths.LockName` delegates, and the normalization matches `DataPaths` because `Root` is already full and trimmed, so the name is unchanged for running older trays). `TrayController.IsRunning` and `HookTrayStart.TrayRunning` repeat the same check with different access-denied handling. The `"tray-instance"` literal is repeated in three places. There are three tray-start paths. | ANALYSIS-4103, ANALYSIS-4104 |
| Simplification / complexity | Inspected the burst guard and attempt-record cleanup | Two fixed buckets, a previous-bucket age check and pruning. Records are bounded: each successful claim prunes buckets older than n-1. A `*.tmp` from `WriteRecord` stays behind if the hook is killed during a write. | ANALYSIS-4107 |
| Architecture | Checked against `docs/development.md` and `docs/hook-ingress.md` | Windows process creation and tray lifecycle policy now live in `BellTower.Protocol`. The ingress doc describes Protocol as the capture contract and says native process code is linked separately. | ANALYSIS-4105 |
| Safety: P/Invoke | Checked struct layout, access rights and handle lifetimes by hand against Win32 definitions | `STARTUPINFOW` is 104 bytes on x64 and `PROCESS_INFORMATION` is 24, both correct. The command line is a writable HGlobal, as required. The token rights `TOKEN_QUERY \| TOKEN_DUPLICATE \| TOKEN_IMPERSONATE` are what `CreateEnvironmentBlock` needs. The offsets read from `TOKEN_STATISTICS.AuthenticationId` (8) and `SECURITY_LOGON_SESSION_DATA.LogonTime` (80, guarded by `Size >= 88`) are correct for x64. Every handle and buffer is released in `finally` (token, environment block, command line, LSA buffer), and the process and thread handles are closed on success. `bInheritHandles=false`, with no `STARTF_USESTDHANDLES`. `DllImport` matches the repository's interop style, and the developer reports a Native AOT publish with no warnings (not re-run here). | No defect found. The breakaway fallback is ANALYSIS-4101. |
| Safety: bounded hook | Traced every step of `TryStart` for waits | No wait on the tray. The mutex open is non-blocking. File reads are limited to 2048 bytes. `CreateProcessW` returns without waiting. Everything is inside a catch-all except OOM. One unbounded case: path attribute reads on a mapped network drive. | ANALYSIS-4102 |
| Safety: trust boundary | Traced who writes `hook-start/launcher.txt` and what the data directory already holds | The data directory already holds the ingress executable that providers run on every hook (`integrations/provider-ingress/1/<hash>/bell-tower-ingress.exe`, `IngressProvision.Plan`) and the imported `shell/BellTower.psm1`. Write access to it is already code execution as the user, so the launcher record adds no new boundary. The check-to-use gap needs write access to the launcher's folder, which already lets an attacker replace the file directly. I agree it is out of scope. The validator does not enforce "stable launcher only". | ANALYSIS-4102 |
| User-stop semantics | Grepped every `RequestExit`/`RequestUserExit`/`exit` caller | A stop is recorded only by the tray **Exit** menu (`TrayApplication.cs:175-180`) and the `exit` CLI (`ExitCommand.cs:10`). Update (`UpdateTransaction.cs:119,162`), the dev switch (`install-dev.ps1:132`), the smoke tool and tests call `RequestExit` and record nothing. The stop is written before the exit request, so no hook can see "no tray" before the record exists. A Windows sign-out does not record a stop. | Correct |
| Sign-in identity | Read the source and applied known UAC behavior | LUID plus logon time is correct for one token. Under UAC the elevated and filtered tokens of one sign-in belong to different logon sessions. This is the reason the `EnableLinkedConnections` policy exists for mapped drives. | ANALYSIS-4100 (not probed) |
| Update interaction | Compared `HookTrayStart.UpdatePending` with `UpdateTransaction.RestartPending` and `CanStartTray`, and read the order of steps in install-dev | The marker's last-write time is never earlier than the `plan.Created` it contains, so the ingress window covers every case where `RestartPending` is true (more conservative). The dev switch also holds a tray-instance reservation, which the ingress sees as "tray running". `Apply` writes the marker before it stops the tray. A crashed coordinator blocks hook starts for at most 10 minutes. | Acceptable, no finding |
| Diagnostics honesty | Read `HookStartReporter` and the docs | `Started` means only that the launcher process was created. The launcher, the payload or `CanStartTray` can still refuse. A doctor run under a different token misclassifies the stop record. The docs say "local path", but the code also accepts a mapped drive. | ANALYSIS-4106, ANALYSIS-4102, ANALYSIS-4100 |
| Test isolation | Read `HookTrayStartTests`, `FakeLauncher` and the tests that use a synthetic installation | The stand-in holds the mutex, ends itself after 90 s, and only stand-ins the tests started are killed. `ManagementOwnershipProcessTests` (update with the real launcher) does record a launcher through `RegisterStart`, but no ingress hook can start it: the marker blocks during the update, and the cleanup `exit` now records a user stop. This safety is implicit. | Holds. A note is in the assessment. |
| Parity | Checked which code paths each provider uses | One ingress path for both providers, and the matched theory rows cover Claude and Codex. The Codex job object was not probed, and the fallback inside the job is untested. | ANALYSIS-4101 |
| Performance | Read the developer's latency table. I took no measurement. | Developer measurement: with a tray running or a refusal the added cost is within noise. A real attempt adds about 10 to 14 ms at the median, at most once per 30 s. Source: with a tray running, the cost is `GetFullPath`, SHA-256 of the path and one `OpenMutex`. `SignInToken` (an LSA call) runs only when a stop record exists, because the check short-circuits. | No finding. Anything further is a hypothesis only. |
| Format verify | `dotnet format ... --verify-no-changes` | `global.json` pins SDK 10.0.114 with roll-forward disabled. Only 10.0.115 and 9.0.321 are installed. | Not run (pinned SDK missing). No build, per the assignment, so the shared outputs used by concurrent QA are untouched. |

## Findings

### ANALYSIS-4100: Elevated provider sessions break the user-stop identity and can start an elevated tray

- Severity: warning
- Related task / requirement: 4.5; spec scenario "User stopped the tray"; design decision 9 (Suppression)
- Locations: `src/BellTower.Protocol/TrayProcess.cs:77-120` (`SignInToken`), `src/BellTower.Protocol/HookTrayStart.cs:177-202`, `src/BellTower.Protocol/TrayProcess.cs:134-151` (the child inherits the hook's token)
- Observation: the identity is the process token's `AuthenticationId` plus that session's logon time. With UAC, an administrator's elevated and filtered tokens belong to two linked logon sessions that have different LUIDs. Mapped drives are kept per logon session for the same reason, which is why `EnableLinkedConnections` exists. This causes three problems:
  1. **Exit** in a non-elevated tray records LUID A. A hook from an elevated terminal (an elevated `claude` or `codex`) computes LUID B, sees the record as stale and restarts the tray in the same Windows sign-in. That is the case the scenario forbids.
  2. The tray it starts inherits the elevated token, so an elevated tray now starts implicitly. Its tray-instance mutex has an elevated default DACL, so a later non-elevated `TrayController.IsRunning` can throw `UnauthorizedAccessException`. That method does not catch it; `HookTrayStart.TrayRunning` does.
  3. A doctor run under the other token labels a live stop as "from another sign-in (stale, ignored)".
- Proposed improvement and benefit: (a) Do not start from an elevated hook. Read `TokenElevationType` (one `GetTokenInformation` call on the token already open), return a distinct outcome such as `Elevated` and document it. (b) Key the stop to the sign-in rather than the token: use the Terminal Services session id (`ProcessIdToSessionId`) plus the session's logon time (`WTSQuerySessionInformation`/`WTSSessionInfo`), which both tokens share. Alternatively, use `TokenOrigin`, but only if a probe shows it is the same for both linked tokens. Either way the scenario holds for elevated terminals, and no implicit elevated tray appears.
- Behavior-preservation risks: existing stop records in the `luid:` format become stale once (fails open, so the tray restarts once). A Remote Desktop reconnect keeps the session, so the stop survives it, which is the intended sign-in scope. Fast user switching gives separate sessions, which is correct.
- Evidence type: source observation plus documented Windows logon-session behavior. Not probed.
- Verification / measurement plan: a one-time manual probe. Print `SignInToken()` from an elevated and a non-elevated process of one sign-in and compare. Add a unit test for the elevation refusal through an injectable token reader, or document it as manual if injection is not worth it. Include an elevated-terminal row in the 6.1 trial.
- Relationship: new

### ANALYSIS-4101: When a job refuses breakaway, the tray starts inside it even if the job will kill it; the path is untested and Codex is unprobed

- Severity: warning
- Related task / requirement: 4.5 ("outside the provider's job"); design decision 9 (Process creation); parity policy (matched Codex scenario)
- Locations: `src/BellTower.Protocol/TrayProcess.cs:146-163`; `docs/hook-ingress.md:18`; `docs/compatibility.md:94`; evidence "Job object evidence" (Codex not probed, fallback not covered)
- Observation: when `CREATE_BREAKAWAY_FROM_JOB` fails with access denied, the code always retries inside the job and reports `StartedWithoutBreakaway`. The code never checks whether that job has `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. If a provider runs each hook command in a job that kills on close and is closed when the hook ends, the tray dies at the end of the hook. Its attempt has already been claimed, so the next hook after 30 s starts it again. The result is a tray that flaps every 30 s, is killed with no clean shutdown (mutex abandoned, `tray.json.starting` left behind, queue only partly drained), and consumes the very captures it is meant to show. Only Claude Code 2.1.325 in print mode was probed, and its job allows silent breakaway. Codex 0.167.2 was not probed. No automated test covers the fallback.
- Proposed improvement and benefit:
  - Before the retry, call `QueryInformationJobObject(NULL, JobObjectExtendedLimitInformation)`. With a NULL handle it queries the caller's job.
  - If the job kills on close, return `Failed` with the detail `provider-job-kills-on-close` and do not start. The capture stays queued and doctor states why.
  - Retry inside the job only when it does not kill on close.
  - Add a test helper mode: for example, the stand-in project creates a job with kill-on-close and without breakaway, assigns itself, then runs the ingress. Use it to cover both branches. Probe the Codex hook job before 6.1, or record it as unverified in the capability matrix.

  This makes the outcome deterministic and the diagnostic accurate, and it closes the parity gap with evidence rather than hope.
- Behavior-preservation risks: none for Claude, where breakaway succeeds. For a refusing job that does not kill on close, behavior is unchanged.
- Evidence type: source observation. The provider behavior is a hypothesis until probed.
- Verification / measurement plan: the job-wrapper test for both branches, run with both provider payloads. A Codex job probe like the Claude one, recording `LimitFlags`.
- Relationship: new. It extends the developer's own "left for review" note.

### ANALYSIS-4102: The launcher validator accepts mapped network drives and any `bell-tower.exe`, including a versioned payload

- Severity: suggestion
- Related task / requirement: 4.5 ("the ingress validates it"); design decision 9 (Launcher location)
- Locations: `src/BellTower.Protocol/HookTrayStart.cs:141-172` (`LauncherProblem`), `docs/hook-ingress.md:15`
- Observation: the docs promise an "absolute local path" to "the stable `bell-tower.exe` of the installation (never a versioned payload)". The code rejects only `\\` UNC paths, so it accepts a mapped or `subst` network drive letter. On a disconnected share, `File.GetAttributes` for each ancestor (this runs on every hook while the tray is stopped, before the attempt claim) can block for the SMB timeout. That would stall the hook, which the "never delays the hook" contract rules out (latency impact is a hypothesis). The name check also accepts `<install>\versions\<v>\bell-tower.exe` or any build output. Starting such a file would skip the launcher's selection, lease and `CaptureCompatibility.EnsureTrayInvocation`. Today only trusted callers write the record, so the "never a versioned payload" guarantee rests on callers following the rule, not on the validator.
- Proposed improvement and benefit: in `LauncherProblem`, reject a drive whose type is not fixed. `GetDriveTypeW` on the root, or `DriveInfo.DriveType != Fixed`, reads no file attributes from the share. Also require an `installation.json` next to the launcher: one stat, and a payload folder does not have one. This makes the documented contract enforceable where the record is used. It also avoids a hook stall on an unreachable network drive.
- Behavior-preservation risks: an installation on a removable or network drive would lose hook start. Doctor reports the reason, and this would need a documented limit. The test harness's stand-in folder would need an `installation.json` stub.
- Evidence type: source observation. The stall is a performance hypothesis.
- Verification / measurement plan: add rows to the invalid-launcher theory for a payload path and a non-fixed drive (a `subst` drive is enough to exercise the drive-type branch).
- Relationship: new

### ANALYSIS-4103: The tray-running check and the area name are still duplicated

- Severity: suggestion
- Related task / requirement: 4.5; focus "one shared policy"
- Locations: `src/BellTower/Tray/TrayController.cs:8-17` (`IsRunning`), `src/BellTower.Protocol/HookTrayStart.cs:45-64` (`TrayRunning`); the `"tray-instance"` literal in `src/BellTower/Tray/TrayController.cs:10`, `src/BellTower/Tray/TrayHost.cs:11` and `scripts/install-dev.ps1:996`, next to `HookTrayStart.TrayArea`
- Observation: the mutex-name computation is now shared, which is good, but there are still two "is a tray running" implementations. The CLI, update and dev-switch path throws on access denied (see ANALYSIS-4100). The hook path returns true. The area string is repeated, so a rename would split the tray and the hook again.
- Proposed improvement and benefit: use `HookTrayStart.TrayArea` (or `[BellTower.Protocol.HookTrayStart]::TrayArea` in PowerShell) at the three sites. Consider having `TrayController.IsRunning` delegate to `HookTrayStart.TrayRunning`. The result is one definition of "running" for the hook, CLI, doctor and update.
- Behavior-preservation risks: delegating changes access denied from a throw to `true` for `UpdateTransaction` waits and the `install-dev.ps1` owner checks. An update would then wait for a tray it cannot see stop and time out, instead of failing at once. So limit the change to the constant, or make the switch only with update and install-dev tests.
- Evidence type: source observation
- Verification / measurement plan: the existing suite plus `InstallDevScriptTests`. No new test is needed for the constant.
- Relationship: new

### ANALYSIS-4104: Three tray-start paths each build the tray command and choose the environment separately

- Severity: suggestion
- Related task / requirement: 4.5; focus "TrayController.Start, UpdateTransaction.StartTray and the new hook start should share what they can"
- Locations: `src/BellTower/Tray/TrayController.cs:19-49` (ShellExecute, inherits the caller's environment), `src/BellTower/Updates/UpdateTransaction.cs:298-304` (launcher, adds `BELLTOWER_UPDATE_ACTIVATION`, waits), `src/BellTower.Protocol/TrayProcess.cs:127-181` (CreateProcessW, clean environment)
- Observation: all three build `tray --data-dir <root>` against the stable launcher. Only the hook path keeps `CLAUDECODE`, `CODEX_*` and `BELLTOWER_*` away from the tray. `bell-tower start`, or a managed launch run from inside a provider session, still passes the caller's environment to the tray and to the agents it launches. That is the same leak decision 9 guards against, but it is older than this change.
- Proposed improvement and benefit: a small shared `TrayCommand.Arguments(root)` (in Protocol, next to `MutexName`) used by all three paths. Separately, consider routing `TrayController.Start` through `TrayProcess.Start` so manual starts get the same clean environment. Leave `UpdateTransaction.StartTray` alone: it needs the activation variable and its wait.
- Behavior-preservation risks: moving `TrayController.Start` off ShellExecute changes the working directory from `AppContext.BaseDirectory` to the launcher folder (which matches the development rule). The ShellExecute behavior around App Paths and SmartScreen also goes away. Do not change the update path.
- Evidence type: source observation
- Verification / measurement plan: CLI start tests plus an environment assertion like the one in `TheStartedTrayHoldsNoHookHandleAndSeesNoProviderEnvironment`, run against `start`.
- Relationship: new. The environment part is outside task 4.5, so the orchestrator may defer it.

### ANALYSIS-4105: Tray lifecycle and Windows process creation now live in the capture-contract project, contrary to its documented role

- Severity: suggestion
- Related task / requirement: 4.5; architecture
- Locations: `src/BellTower.Protocol/HookTrayStart.cs`, `src/BellTower.Protocol/TrayProcess.cs`, `docs/hook-ingress.md:24` ("`BellTower.Protocol` owns the shared DTOs, parser, validation, source-generated serializer and atomic spool ... Native process inspection is linked separately into the Windows ingress executable")
- Observation: `BellTower.Protocol` targets plain `net10.0` and is documented as the capture-v1 contract. It now also holds user-stop and launcher policy for the tray, CLI, setup and doctor, plus Windows `CreateProcessW` and LSA interop. That breaks the documented pattern of linking native code from `src/BellTower/IO` into the ingress, as `ProcessProbe` and `ProcessTable` are. The placement works: the tray, ingress and stand-in all reference Protocol. But the doc now contradicts the structure.
- Proposed improvement and benefit: the cheapest fix is to update the `docs/hook-ingress.md` contract paragraph to name the hook-start state and its native start as a deliberate shared part of Protocol. The alternative is to move both files to `src/BellTower/IO` and link them into the ingress and the stand-in like `ProcessProbe`. Either removes the contradictory rule.
- Behavior-preservation risks: the doc-only option has none. Moving the files changes the build graph of the stand-in project.
- Evidence type: source observation
- Verification / measurement plan: for the move, re-run the build and the ingress AOT publish. For the doc option, nothing.
- Relationship: new

### ANALYSIS-4106: Doctor's `Started` reports only that the launcher process was created

- Severity: suggestion
- Related task / requirement: 4.5 ("Doctor reports availability, the last attempt and suppression"); diagnostics honesty
- Locations: `src/BellTower.Protocol/TrayProcess.cs:151-156`, `src/BellTower/Diagnostics/HookStartReporter.cs:17-26`
- Observation: `Started` or `started` is written when `CreateProcessW` succeeds for the launcher. After that the launcher can still fail (selection, payload, `EnsureTrayInvocation`), and so can the payload (`CanStartTray`, which refuses with no message during an update). In those cases doctor shows `LatestAttempt.Result: Started` with no tray. `TrayRunning` sits next to it in the doctor output, but nothing links the two, and the next attempt waits 30 s.
- Proposed improvement and benefit: record the detail as `launcher-started pid=<n>`, with the creation time from the process handle before it is closed, so it is a verifiable identity. Have doctor derive `TrayCameUp` from the presence record (`updates/tray.json`) when its start time is later than the attempt. Alternatively, rename the outcome text to "launcher started". Either way the user can tell "started and running" from "started and gone".
- Behavior-preservation risks: none for the hook. The change is diagnostic text only, within the record's bounded line.
- Evidence type: source observation
- Verification / measurement plan: a stand-in mode that exits at once, with doctor asserting that the tray did not come up.
- Relationship: new

### ANALYSIS-4107: The burst guard can be simplified; temporary records are never pruned

- Severity: suggestion
- Related task / requirement: 4.5 ("At most one attempt per bounded interval"); design decision 9 (Burst control)
- Locations: `src/BellTower.Protocol/HookTrayStart.cs:227-270` (`TryClaimAttempt`, `Prune`), `src/BellTower.Protocol/HookTrayStart.cs:294-310` (`WriteRecord`)
- Observation:
  - **The boundary race is real but harmless.** Between hook A taking `now` in bucket n and closing `attempt-n.lock`, hook B in bucket n+1 can read that file as missing, locked (`FileShare.None` turns the read into `IOException`, so `null`) or empty (parse fails), and claim n+1. The window covers `CreateDirectory`, a read and a create plus write, about a millisecond (a hypothesis). The tray mutex keeps one tray, and the second payload exits silently in `TrayHost.Run`.
  - **The attempt records stay bounded.** Each claim prunes buckets older than n-1, at most 64 per pass. After a backward clock jump, buckets ahead of the clock linger until time catches up, but there is still only one file per claim.
  - **Temporary files are the one leftover that is never cleaned.** A hook killed by the provider timeout between `WriteAllText` and `Move` leaves `hook-start/*.tmp`, and nothing removes it.
- Proposed improvement and benefit: replace the buckets with a named mutex `MutexName(root, "hook-start")`, using `WaitOne(0)` and treating `AbandonedMutexException` as acquired. Under it, read a single `last-claim` timestamp, compare it with the interval, write it and release. That gives exact interval semantics with no race, no pruning and one file. Have the prune pass, or `RecordLauncher`, delete `*.tmp` files in `hook-start` that are older than the interval.
- Behavior-preservation risks: the mutex is scoped to the session namespace. Hooks for the same data directory from another Windows session would no longer be serialized, but the tray mutex has the same scope. `ManyConcurrentClaimsInOneIntervalHaveOneWinner` and the boundary test need rewriting.
- Evidence type: source observation. The race window size is a hypothesis.
- Verification / measurement plan: the existing concurrency tests, plus a boundary test that calls the claim with times on both sides of the old boundary.
- Relationship: new. This is optional, because the current design meets the acceptance text.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not applicable |

## Assessment and limitations

**Scope covered.** All 22 manifest paths and the neighbors listed above, for both providers. The ingress path is shared, so every observation applies to Claude Code and Codex alike. The exceptions are ANALYSIS-4101, where the Codex job is unprobed, and ANALYSIS-4100, which applies to any elevated terminal of either provider.

**The developer's defaults:**

- *Start only after a durable write or a benign no-envelope event.* Agree. The spec requires a start after a persisted capture and does not forbid one after a benign event. The setup hook probe uses a temporary data directory with no launcher record, so it never starts a tray. Does not break acceptance.
- *Fail open when the LUID is unreadable.* Acceptable. `OpenProcessToken` on the process's own token practically never fails. The real identity gap is elevation (ANALYSIS-4100), not readability.
- *30 s window.* Sound. A slow start just past 30 s produces at most one extra launcher, which exits because of the mutex. Does not break acceptance.
- *`restart.json` younger than 10 minutes, without an owner check.* Covers every case where `RestartPending` is true, so it is more conservative. Its cost is at most 10 minutes with no hook start after a crashed coordinator. The dev switch is also fenced by its tray-instance reservation. Does not break acceptance.
- *No `--silent`.* Correct. `--silent` selects the silent audio sink (`CommonOptions.Silent`), so passing it would mute alerts. Design decision 9's "quiet start" is met by having no window. Does not break acceptance.
- *Only real attempts recorded.* Acceptable. Doctor can work out every refusal except `Throttled`, which is transient, from the current state. See ANALYSIS-4106 for what `Started` means.

**Acceptance.** No default breaks acceptance on the evidence available. Two things are open for 6.1:

- the "User stopped the tray" scenario fails for elevated terminals (ANALYSIS-4100);
- "outside the provider's job" is unverified for Codex, and its fallback is not covered (ANALYSIS-4101).

**Trust boundary.** The launcher record adds no new boundary, because the data directory already holds the hook executable and the imported module. I agree the check-to-use gap is out of scope. ANALYSIS-4102 makes the documented "stable launcher, local path" contract enforceable.

**Test isolation.** No test can start a real tray. In `ManagementOwnershipProcessTests`, the update test with the real launcher records that launcher through `RegisterStart`. It stays safe only because the update marker blocks hook starts during the transaction, and because its cleanup `exit` now records a user stop. Consider asserting in that test that no launcher process outlives it (`StandIns.KillUnder(install)` already cleans up).

**Not performed:**

- No build, test or format run. The pinned SDK 10.0.114 is not installed, and the assignment asked me not to disturb the outputs QA is using.
- No latency measurement of my own. Performance statements are the developer's measurements or labeled hypotheses.
- No elevation or job probe.

**Writes.** The client refused my write of this report to the reserved path, and I made no other writes. The before and after identities match.

**Decisions for the orchestrator.** Whether ANALYSIS-4100 and ANALYSIS-4101 must be fixed before 6.1 or can be recorded as limits in the capability matrix. Whether the environment part of ANALYSIS-4104 belongs in this change.
