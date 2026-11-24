> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-056); client: Claude Code subagent (version not read); mode: delegated subagent, auto mode; model: claude-opus-5-5.
> Refusal (as reported by the reviewer): "Subagents should return findings as text".

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-056 (plain codex mode intermittently unknown)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Agent SDK subagent), version not read (no client process was started to query it) / delegated subagent, auto mode / claude-opus-5-5
- Date: 2026-11-24T19:30Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-056 (bounded read-only investigation)
- Task and requirement references: tasks 4.1 and 4.11 ([tasks.md](../tasks.md)); design decisions 7 and 13 ([design.md](../design.md)); attention-notifications "Viewed wait timer" ([spec](../specs/attention-notifications/)); ledger entries of 2026-11-24 ([findings.md](findings.md))
- Scope and relevant neighboring paths: `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/Providers/AgentModeClassifier.cs`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/IO/ProcessTable.cs`, `src/BellTower/Core/AttachmentEvidence.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/Hosts/NavigationReasons.cs`, `src/BellTower/Sessions/CodexRuntimeOpener.cs`, `src/BellTower/Providers/CodexBackendCheck.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `docs/compatibility.md`, `tests/BellTower.Tests/Providers/ModeCrossCheckTests.cs`, `tests/BellTower.Tests/Hosts/NativeAgentRelationshipProcessTests.cs`
- Baseline commit: `9550ced`
- Implementation identity: `./scripts/review-identity.ps1 -Baseline 9550ced -Paths` (the ten source and test paths above except the diagnostics and docs files) -> HEAD `9550ced12d19d6a2d4e0f857e64879ff1d8007c6`, manifest `M src/BellTower/Hosts/NavigationReasons.cs` (the uncommitted DEV-063..065 edit), tracked diff SHA-256 `490077988e12...`, no untracked files. Summary: `head=9550ced baseline=9550ced files=1 diff=490077988e12 untracked=0/e3b0c44298fc`. The classification files were last changed on 2026-11-14 (`5e95554`), so the installed builds 0.1.9029 and 0.1.9030 behave the same way here.
- Before/after identity: the same command after the analysis printed the same summary line. The `git status --short` line count was unchanged (27). No unexpected mutation.
- Prior reports / rechecked findings: none rechecked. Related: [implementation-028](../evidence/implementation-028.md) (rule R1 and its stated limit), [native-adoption-009](../evidence/native-adoption-009.md) (hook ancestry in shared mode).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Capture path (architecture) | Source trace: `AttachmentCapture.Resolve` -> `AgentModeClassifier.Classify` -> `CrossChecked` -> `NativeAdoption.CrossCheck` -> `NativeAdoption.Apply` -> `AttachmentStore` adopt -> `AttachmentEvidence.Merge` | The agent is the first `codex.exe` ancestor of the hook, which in shared mode is the backend (native-adoption-009). `Classify` returns `shared-backend` for any `app-server` before `--` (`AgentModeClassifier.cs:76-79`). `CrossCheck` then keeps `shared-backend` only when the backend's direct parent is a live `codex.exe` that classifies as a client (`NativeAdoption.cs:68-71`). `ProcessTable.Neighbors` returns no parent when the parent PID is absent from the snapshot, is not a `codex.exe` image, or started later (`ProcessTable.cs:54-59`). | Root cause found: rule R1 (ANALYSIS-5600) |
| Live backend (measured) | `Get-CimInstance Win32_Process` (read-only), command lines redacted | Shared backend PID 7628, created 18:30:38.81Z, `codex.exe app-server --listen unix:// --analytics-default-enabled --managed-daemon` from the managed package `releases/0.172.0`. Its parent PID 3284 no longer exists. Sibling PID 9060 `app-server daemon pid-update-loop` has the same dead parent. The plain `codex` client of today's conversation (PID 6260, created 19:19:34Z, under `node codex.js`, CLI 0.172.0) is not related to the backend by parentage. | The backend is orphaned |
| Who started the backend (measured) | Read `~/.codex/app-server-daemon/daemon-updater.stderr.log{,.previous}`, `daemon.pid` and the release folder times (read-only; only stage, event and PID fields are cited) | PID 3284 was the previous updater loop. At 18:30:14Z it ran a `scheduled` installer pass, selected `0.172.0`, logged `restart_requested`, shut down the old backend (PID 14176), spawned the replacement backend 7628 at 18:30:38.820Z (`replacement_launch`), spawned the new updater 9060, then exited. `daemon.pid` names 7628 with the matching start time. Release `0.172.0` was created at 18:30:35Z; release `0.171.0` was created at 2026-11-22 13:02:22Z. | Today's backend was started by the Codex updater, not by a client |
| Probe of the product code on the live table (measured) | Copy of the Release `bell-tower.dll` loaded in PowerShell 7.6 (.NET 10) from the session scratch folder; calls `AgentModeClassifier.Arguments/Classify`, `ProcessProbe.Identify`, `ProcessTable.Take().Neighbors(..., "codex.exe", false)` and `Ancestors` for PID 7628 | Arguments `app-server | --listen | unix:// | --analytics-default-enabled | --managed-daemon`; classify `shared-backend`; `Neighbors.Parent` null (complete true); `Ancestors(7628)` empty. `Needs(shared-backend, true)` is `Parent`, so `CrossCheck` returns `unknown`. | Reproduces the stored `unknown` with the product code |
| Stored attachments (measured) | Read-only listing of `%LOCALAPPDATA%\ZeusApps.BellTower\attachments` (shared-runtime and codex records since 11-21) | Shared-runtime mode: `shared-backend` 11-14, 11-22 12:57Z, 11-23 22:12Z and 22:29Z; `unknown` 11-21 11:40Z, 11-22 17:13Z, 11-24 19:19:47Z. Today's record `codex-session-5e4509eb...` has `mode unknown`, `lifecycleOwner shared-backend`, `runtime.access none`. The 11-22 change from `shared-backend` (12:57Z) to `unknown` (17:13Z) brackets the 0.171.0 updater install at 13:02Z. Record d07b8e46 kept `shared-backend` through hooks until 16:49Z because `Merge` never replaces an established mode. | Consistent with ANALYSIS-5600. The 11-21 cause is not proven (no updater log retained; that release folder was pruned); a launching client that exited is the other documented cause. |
| Alternative causes | Source inspection plus the probe | Unreadable command line: readable in the probe (same user, not elevated). Options outside the vocabulary (`--analytics-default-enabled`, 0.172.0): no effect, because the `app-server` check runs before option parsing. Agent ancestor not found: it would also give `unknown`, but the hook runs under the backend, which is present in the table. Windows restart at 18:17:58Z: not the cause in itself. After it a managed start relaunched the backend and the updater (the README says the updater is not reboot-persistent), and the updater's restart at 18:30 orphaned the backend. `codex --no-daemon` sessions (18:24Z, 18:30:33Z): embedded runtimes with their own in-process hooks (stored `embedded-runtime`); they are unrelated to the shared backend. | Ruled out for today |
| Later learning (goal 2) | `AttachmentEvidence.Merge` (`AttachmentEvidence.cs:167-172`), `AttachmentStore.cs:107` | A later hook can upgrade a stored `unknown` mode to `shared-backend`. A stored known mode is never replaced. | See ANALYSIS-5600 |
| Downstream effect | `NavigationReasons.ForSharedConversation` (`NavigationReasons.cs:48-51`), `CodexRuntimeOpener.Select` (`CodexRuntimeOpener.cs:57-59`) | `unknown` gives `TerminalUnverified` (a generic notice; the timer is kept by rule 4.11) instead of `SharedBackend`. The same check denies the non-owning shared access, so plain `codex` also loses name sync and permission status for that conversation, although design decision 8 promises both. | User-visible beyond the notice |
| Policy duplication | Compared `CodexBackendCheck.Verify` with the capture cross-check | The transport accepts the same backend from its command line alone (`CodexBackendCheck.cs:49`), with no parent rule. Capture rejects that same process through R1. | ANALYSIS-5601 |
| Diagnostics honesty | `CapabilityMatrix.cs:79`, `docs/compatibility.md:71` | Both describe the gap only as "a shared conversation adopted after its launching client exited". They omit the provider's own updater restart, which is the common case. | ANALYSIS-5602 |
| Primary provider documentation | `ctx7 docs /openai/codex` (3 queries) | `codex-rs/app-server-daemon/src/backend/pid_start.rs`: the daemon is spawned with `DETACHED_PROCESS \| CREATE_BREAKAWAY_FROM_JOB` and is pidfile-backed. `app-server-daemon/README.md`: "after a successful refresh, if app-server is running and the managed binary contents changed, the updater restarts app-server with that binary first and only then replaces its own process image", and "the updater loop is not reboot-persistent; a managed start after reboot starts it again". `cli/src/main.rs`: `--managed-daemon` is a hidden `app-server` flag ("Save loaded threads during managed daemon shutdown"). | By provider design, the backend outlives its launcher and is relaunched by the updater |
| Parity | Source inspection | Claude: `MayShareBackend("claude")` is false, so `Needs` is `None` and Claude modes are never cross-checked. Claude has no shared backend, so the defect is Codex-only by provider fact. The proposed rule is one Core rule that the Claude adapter never triggers. | Covered in ANALYSIS-5600 tests |
| Build, tests, format | Not run | This was a read-only investigation with no code change to verify. The probe used a scratch copy of the existing build output. | Not performed (not applicable) |

## Findings

### ANALYSIS-5600: The shared-backend parent rule fails on the provider's normal daemon lifecycle, so plain `codex` is stored as `unknown`

- Severity: warning
- Related task / requirement: tasks 4.1 and 4.11; design decisions 7, 8 and 13; attention-notifications "Viewed wait timer"
- Locations: `src/BellTower/Core/NativeAdoption.cs:44-49` and `:68-71` (rule R1); `src/BellTower/Hosts/AttachmentCapture.cs:21-22` and `:47-60`; `src/BellTower/IO/ProcessTable.cs:48-59`; `src/BellTower/Providers/AgentModeClassifier.cs:69-79`; consumers `src/BellTower/Hosts/NavigationReasons.cs:48-51` and `src/BellTower/Sessions/CodexRuntimeOpener.cs:57-59`; test row asserting the current behavior `tests/BellTower.Tests/Providers/ModeCrossCheckTests.cs:26`
- Observation:
  - Design decision 7 cross-checks a `shared-backend` command line by requiring the backend's direct parent to be a live Codex client.
  - That parent is only whichever process last launched the detached daemon. The provider documents that process as transient: the first client, which usually exits long before the daemon does, a `daemon start`/`bootstrap` lifecycle command, or the updater loop, which restarts app-server on every changed release and then replaces itself. Native-adoption-009 already noted that the parent is a client "only because A happened to start the backend".
  - Today the updater restarted the backend at 18:30:38Z and exited. Every hook served by that backend (PID 7628) therefore classifies `unknown`, and the probe reproduces this with the product code on the live process table.
  - Implementation-028 recorded this limit when the mode was only diagnostic. Since tasks 4.1 and 4.11, the mode decides the notice text, the permanent-limit rule (timer hidden or kept) and the shared runtime access (name sync, permission status). The limit is now a user-visible defect whenever the backend has outlived or been restarted away from its launching client. That is the steady state of a daemon, which explains the 4-of-7 pattern.
- Goal 2 (could a later hook learn it; why not today):
  - Yes in principle. `AttachmentEvidence.Merge` takes `incoming.Mode` while the stored mode is `unknown`.
  - It did not happen today because every later hook of `codex-session-5e4509eb...` is captured under the same orphaned backend, whose parent cannot become live again. Only a backend replacement started by a still-running client, with the thread reloaded there before that client exits, could teach `shared-backend`. The next replacement is normally another updater restart, which orphans the backend again.
  - Once the fix below is installed, stored `unknown` shared conversations learn `shared-backend` at their next hook through the existing `Merge` rule, without a data migration.
- Proposed improvement and benefit: one shared Core rule, with the provider fact supplied by the adapter.
  1. Provider adapter (`AgentModeClassifier`): add `LauncherIndependentBackend(string provider, IReadOnlyList<string>? arguments)`. It is true only for Codex when the arguments before `--` contain both `app-server` and `--managed-daemon`, which is the launch shape of the provider's detached managed daemon. It was verified on 0.166.0 (implementation-034) and live on 0.172.0 here, and it is what `codex-rs/cli/src/main.rs` declares. It is false for every Claude invocation, for Bell Tower's owned runtime (`app-server --listen ws://... --ws-auth ...`, `CodexRuntimeHost.cs:75`), for IDE/stdio `app-server`, and for the updater `app-server daemon pid-update-loop`.
  2. Core (`NativeAdoption.Needs`/`CrossCheck`): add a `launcherIndependent` input. A `shared-backend` whose invocation is launcher-independent needs no relationship (`Relation.None`) and keeps its mode. Every other mode and shape keeps R1-R3 unchanged. State the rule once in the Core comment: "a backend that the provider documents as a detached, relaunched daemon is not judged by its launcher".
  3. Hosts (`AttachmentCapture.Resolve`): compute the fact from the arguments already read and pass it through. No extra process or file read is needed, and the hook budget does not change.
  - Benefit: plain default `codex` gets the stated `SharedBackend` notice, the hidden timer of rule 4.11 and its shared name and status access, regardless of daemon age, restarts or reboots. No new provider-internal dependency is added (the pidfile and updater logs are not read by the product).
- Behavior-preservation risks:
  - Owned runtime proof (QA-2400, implementation-030) relies on an `app-server` under a non-client launcher staying `unknown`, so that a managed capture becomes provisional. The discriminator keeps that path, because the owned backend has no `--managed-daemon`. Pin it with a regression row.
  - ANALYSIS-500 (inherited `BELLTOWER_ATTACHMENT` in the daemon environment): `shared-backend` goes straight to `NativeAdoption.Shared` without a candidate, which is the same or a safer outcome than today's provisional path that Reconcile rejects.
  - `--managed-daemon` is a hidden flag. If a later Codex renames it, the shape falls back to today's R1 behavior (`unknown`, fail closed). Diagnostics should name that case (ANALYSIS-5602).
  - A user-run `codex app-server --listen unix://` without the flag under a shell stays `unknown`, which is unchanged.
- Evidence type: measured result (live process table, provider logs, product-code probe) plus source observation; primary provider documentation via Context7.
- Verification / measurement plan (both providers):
  - Unit, classifier: the 0.172.0 line (`app-server --listen unix:// --analytics-default-enabled --managed-daemon`) and the 0.166.0 line (`app-server --listen unix:// --managed-daemon`) -> launcher-independent. Owned `app-server --listen ws://127.0.0.1:1 --ws-auth capability-token --ws-token-file x`, bare `app-server`, `app-server --stdio`, `app-server daemon pid-update-loop`, `app-server -- --managed-daemon` and `exec --managed-daemon` -> not launcher-independent. Every Claude row (`""`, `-p`, `--print`, an arbitrary line containing `app-server --managed-daemon`) -> false.
  - Unit, `ModeCrossCheckTests`: launcher-independent `shared-backend` with parent null, with an updater parent (`shared-backend`), with an `unknown` parent and with unreadable relations (null) -> `shared-backend`, and the relations delegate is not called. The existing row at line 26 and the other R1 rows stay `unknown` for the non-independent shape. Claude rows stay unchanged with the flag false.
  - Real process (`NativeAgentRelationshipProcessTests`, both `event` and `bell-tower-ingress.exe` entries): stand-in `codex` `app-server --listen unix:// --managed-daemon` under (a) a non-Codex launcher, (b) a Codex `app-server daemon pid-update-loop` launcher, and (c) a launcher that exits before the hook (orphan) -> `shared:shared-backend`. Regression: `app-server --listen ws://...` under a non-client launcher with a managed identifier and a live owned-runtime record -> still owned (QA-2400 rows); without a record -> `shared:unknown`. Claude rows at the same positions unchanged.
  - Mixed simultaneous: extend `MixedSimultaneousSessionsAreJudgedIndependently` with an orphaned managed backend beside a Codex `--no-daemon` client and a Claude client. Each keeps its own mode and process claim.
  - Consumers: `NavigationReasons.ForSharedConversation` for a capture from an orphaned managed backend -> `SharedBackend`, and `IsPermanentLimit` -> true, so the chip timer hides (4.11). `CodexRuntimeOpener.Select` -> `Shared`. A Claude direct session click result is unchanged.
  - Store: a stored `unknown` shared attachment adopts `shared-backend` from the next capture (`Merge`), and a stored `shared-backend` is not downgraded.
  - Live acceptance (task 4.11 / 6.1): on the next dev build, a plain `codex` conversation on a backend whose launcher has exited or that the updater restarted shows the shared-backend notice, and its timer hides.
- Relationship: new. It supersedes the limit stated in implementation-028 (line 51) for the managed-daemon shape. It is the cause of the 2026-11-24 task 4.11 live-check result.

### ANALYSIS-5601: Capture and transport judge "is the shared backend" by two different rules

- Severity: suggestion
- Related task / requirement: design decisions 4 and 7
- Locations: `src/BellTower/Providers/CodexBackendCheck.cs:49` (command line only); `src/BellTower/Core/NativeAdoption.cs:68-71` (command line plus a client parent)
- Observation: the transport accepts backend 7628 as a verified shared backend (command line, image, liveness, not elevated), while capture rejects that same process through R1. The two definitions can diverge again with later provider changes.
- Proposed improvement and benefit: when ANALYSIS-5600 is implemented, have both places use the same adapter predicate for the managed-daemon shape. The transport keeps its stronger socket-peer, elevation and liveness checks. This leaves one vocabulary for "this process is the home's managed backend".
- Behavior-preservation risks: the transport must not become stricter for existing accepted backends. Keep `Classify(...) == shared-backend` as its gate unless the user decides otherwise.
- Evidence type: source observation plus the live probe.
- Verification / measurement plan: `CodexSharedAccessTests` already uses `codex app-server --listen unix:// --managed-daemon` (line 16). Add one row showing that the capture predicate and the transport accept the same 0.172.0 line.
- Relationship: depends on ANALYSIS-5600.

### ANALYSIS-5602: Capability matrix and compatibility text understate when plain `codex` is `unknown`

- Severity: suggestion
- Related task / requirement: AGENTS.md (honest capability matrix and diagnostics); design decision 6
- Locations: `src/BellTower/Diagnostics/CapabilityMatrix.cs:79`; `docs/compatibility.md:71`
- Observation: both texts give the cause only as "a shared conversation adopted after its launching client exited". The provider's own updater restart, which happens on every changed release and orphans the backend, is the common trigger and is not named. After ANALYSIS-5600 the remaining `unknown` case is narrower: a backend without the managed-daemon shape, or a renamed flag.
- Proposed improvement and benefit: after the fix, restate the row as follows. A shared backend launched as the provider's managed daemon is recognized however it was started (client, lifecycle command or updater restart). Any other `app-server` shape is recognized only while a Codex client is its direct parent, and otherwise stays `unknown` with tracking only. Keep "not a provider restriction" wording consistent with the existing row.
- Behavior-preservation risks: text only. `CapabilityMatrixTests` and the doctor snapshot need matching updates.
- Evidence type: source observation.
- Verification / measurement plan: update and run `CapabilityMatrixTests`; doctor output on an isolated data directory shows the new text.
- Relationship: follows ANALYSIS-5600.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | - | - | not applicable |

## Assessment and limitations

- Cause:
  - Plain default `codex` is stored as `unknown` whenever its first hook is served by a shared backend whose direct parent is not a live Codex client.
  - The provider's daemon design makes that the normal case. The backend is detached and pidfile-backed, outlives the client that auto-started it, and is relaunched by the updater loop on every changed release.
  - Today's conversation hit the updater case. The updater restarted the backend to 0.172.0 at 18:30:38Z and exited, 49 minutes before the conversation started.
- Fix and scope:
  - The classifier vocabulary is not at fault. The 0.172.0 line classifies `shared-backend`, and options outside the vocabulary are irrelevant after `app-server`.
  - The provider fact needed for a fix is available on the command line, so this is not a provider restriction.
  - The minimal fix is one Core rule with a Codex adapter fact (ANALYSIS-5600). Claude is unaffected by provider fact (no shared backend) and is covered by matched unchanged rows and the mixed case.
- Not established: the cause of the 2026-11-21 `unknown` record. No updater log was retained for it and its release folder is gone; it is consistent with either documented trigger.
- Not performed:
  - Build, tests and format: no code changed.
  - Live hook capture: no hook was triggered and no session, tray or process was started, stopped or signalled.
- Reads outside the repository were read-only: the Bell Tower data directory (attachments, log, captures listing), the running process list and command lines, and the Codex home's daemon package and daemon status files. Only stage, event, PID and time fields are cited, and no personal configuration is quoted.
- The probe used a copy of the existing Release `bell-tower.dll` and `BellTower.Protocol.dll` in the session scratch folder.
- Context7 was queried three times with generic questions and no personal data.
- No repository file was written (the report write was refused; text returned inline).
