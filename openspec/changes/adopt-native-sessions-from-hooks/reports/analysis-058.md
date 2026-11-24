> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-058); client: Claude Code 2.1.331, code-analyzer subagent; mode: read-only analysis; model: claude-opus-5-5.
> Refusal (as reported by the reviewer): the client refused the write of this report; the reviewer did not retry.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-058 (task 6.4 final analysis review)

## Review target

- Reviewer / role: ANALYSIS-058 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331, code-analyzer subagent (read-only analysis; the only write is this report), claude-opus-5-5
- Date: 2026-11-24T20:35Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-058 (task 6.4)
- Task and requirement references: tasks 4.1, 4.3, 4.4, 4.5 to 4.12, 5.1 to 5.3 ([tasks](../tasks.md)); [design](../design.md) decisions 1 to 13, including the decision 7 revision of 2026-11-24; [specs](../specs/) (attention-notifications "Viewed wait timer", session-identity, session-tracking)
- Scope and relevant neighboring paths: the final state of the change's history `37a1b75^..HEAD` plus the uncommitted DEV-063..065 and DEV-067 work. The focus is the post-reduction slice since `1c80a7d`:
  - `src/BellTower.Ingress/Program.cs` and its linked sources
  - `src/BellTower.Protocol/{HookTrayStart,TrayProcess,DesktopShell,HookStartTask}.cs`
  - `src/BellTower/Core/{NativeAdoption,NativePresentation,CausalSessionReducer}.cs`
  - `src/BellTower/Providers/{AgentModeClassifier,CodexBackendCheck,TranscriptEvidence,ClaudeDeclineWatcher}.cs`
  - `src/BellTower/Sessions/{CodexPermissionPump,CodexRuntimeOpener}.cs`
  - `src/BellTower/Hosts/{AttachmentCapture,NavigationReasons,TerminalTitleService,TerminalTitlePump,TerminalPresentationResolver}.cs`
  - `src/BellTower/IO/ProviderTitleState.cs`
  - `src/BellTower/Tray/{TrayApplication,NavigationOutcome}.cs`
  - `src/BellTower/Setup/{IngressProvision,IntegrationRefresh}.cs` and `src/BellTower/Updates/UpdateTransaction.cs`
  - `src/BellTower/Diagnostics/{CapabilityMatrix,DiagnosticsReporter,HookStartReporter,HookDeliveryReporter,ConfirmationCoverageReporter}.cs`
  - `docs/{compatibility,installation}.md`
  - `scripts/install-dev.ps1`, read for its dev-switch behavior only
- Baseline commit: 9550ced (change history from 37a1b75)
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 9550ced -Paths src tests docs vscode scripts`, HEAD 9550ced12d19d6a2d4e0f857e64879ff1d8007c6. The manifest has 17 modified files: `docs/compatibility.md`, `docs/installation.md`, `src/BellTower/Core/NativeAdoption.cs`, `src/BellTower/Diagnostics/CapabilityMatrix.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/Hosts/NavigationReasons.cs`, `src/BellTower/Providers/AgentModeClassifier.cs`, `src/BellTower/Providers/CodexBackendCheck.cs`, `src/BellTower/Tray/NavigationOutcome.cs`, `src/BellTower/Tray/TrayApplication.cs`, and six test files. Tracked diff SHA-256 e340e18da2195c27de0aa6b2e317d233b4bc43069fc3a3f2611c60b536ab92e0; no untracked files. Summary `head=9550ced baseline=9550ced files=17 diff=e340e18da219 untracked=0/e3b0c44298fc`
- Before/after identity: the after run gives the same summary `head=9550ced baseline=9550ced files=17 diff=e340e18da219 untracked=0/e3b0c44298fc`. It is unchanged, and no unexpected mutation was seen.
- Prior reports / rechecked findings: [analysis 041](analysis-041.md) to [analysis 057](analysis-057.md) for context. No finding was assigned for recheck. Deferred items were not re-raised: ANALYSIS-4102..4107, 4707, 4710, 4900, 5100, 5401, QA-5800, QA-6200, the ~4 s hook-to-color latency, ANALYSIS-3302..3305 and QA-4002.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Architecture: shared policy vs thin adapters (4.12) | Inspection of the uncommitted diff and of every mode-classification call site (`grep CrossCheck(`, `AgentModeClassifier.Classify(`) | The managed-daemon shape is a Codex adapter fact (`AgentModeClassifier.LauncherIndependentBackend`). One Core rule consumes it (`NativeAdoption.Needs/CrossCheck`). Capture (`AttachmentCapture.cs:22-23,49-60`) is the only cross-check caller. The transport gate (`CodexBackendCheck.cs:49-51`) is a superset by construction. Claude is false at the adapter and `MayShareBackend` is false. The owned runtime arguments (`CodexRuntimeHost.cs:75`) carry no `--managed-daemon` | No duplicated per-provider policy. Parity is honest: Claude has no shared backend |
| Architecture: 4.11 shared outcome rule | Inspection of `NavigationReasons.IsPermanentLimit`, `NavigationOutcome.ApplyViewed/SaveFailedStatus` and `TrayApplication.Navigate/MarkViewed` (`TrayApplication.cs:406-465`) | One provider-neutral decision point. A success hides at once; a permanent limit hides only after `ShowNotice`. Overview and CLI clicks pass `hideTimer` false | Matches the spec delta. The text match is deferred (ANALYSIS-5100) |
| Architecture: where capture policy runs | `BellTower.Ingress.csproj:11-18`; `IngressProvision.cs:31`; `IntegrationRefresh.cs:7-38`; `UpdateTransaction.cs:291-297`; `grep -i ingress scripts/install-dev.ps1` (no match) | Classification, the cross-check and the hook tray start are compiled into the hash-addressed ingress copy that provider hooks call. Neither an update nor a dev switch provisions a new copy | ANALYSIS-5800 |
| Diagnostics and matrix honesty | Inspection of `HookStartReporter.cs:9-36`, `HookDeliveryReporter.cs`, `ConfirmationCoverageReporter.cs:86-95`, the `CapabilityMatrix.cs` rows, `DiagnosticsReporter.cs:96-110` and the docs diff | The matrix and doctor wording for 4.12 and the first-hook timing agree with the code. `CapabilityMatrixTests` pins the status words and the managed-daemon text in the matrix and in `docs/compatibility.md`. Doctor's `hookStart.available` does not consider which ingress image the hooks call | ANALYSIS-5800 (doctor part) |
| User docs vs spec | `grep -i timer docs/*.md`; read `docs/installation.md:67` and `docs/compatibility.md:98` | `installation.md:67` says failed navigation leaves the timer visible, which contradicts the 4.11 behavior and spec | ANALYSIS-5801 |
| Wait evidence parity after 4.12 | Inspection of `CodexPermissionPump.cs:96-148` and `CodexRuntimeOpener.cs:41-60` | The transcript signal runs only when no access is selected. Conversations that 4.12 moves from unknown to shared-backend lose that fallback when their shared read is refused, unanswered or backed off | ANALYSIS-5802 (suggestion) |
| Performance: tray path | Inspection of `CodexPermissionPump.Update/RefreshAsync/VerifyAsync`, the backoff and log handling, and the [analysis 024](analysis-024.md) ANALYSIS-2405 text | Each waiting shared conversation still costs one peer-verified open per second. 4.12 makes this the normal path for plain `codex`. ANALYSIS-2405 went out of scope with removed task 4.2 and is tracked nowhere else | ANALYSIS-5803 (suggestion, tracking) |
| Performance: hook path | Inspection of `BellTower.Ingress/Program.cs:15-68`, `HookTrayStart.TryStart`, the 4.12 additions in `AttachmentCapture`; latency tables in [implementation 054](../evidence/implementation-054.md) and [implementation 060](../evidence/implementation-060.md) | The capture is persisted before the start. With a tray running the added cost is one mutex open, within noise (measured by DEV-054 and DEV-060). 4.12 adds only an in-memory argument scan, and for a managed daemon it skips the parent command-line read. The shell route after decision 10 was not re-timed (stated in implementation-059) | No finding. The shell-route timing is a stated measurement gap that 6.1 covers |
| Performance: tray tick | Inspection of `TrayApplication.Tick` (`ProviderTitleState.ObserveBoth` every 2 s), `ProviderTitleState.Observe` (stamp cache), `TerminalPresentationResolver`, `TerminalTitlePump.RestoreUnpresentedAsync`, `ClaudeDeclineWatcher` | Two `FileInfo` stats and one small record read every 2 s. The settings files are parsed only when their stamp changes. Records are written without a lock. A lost update only delays a stamp or re-sights an entry with the same write time, and the answer always follows the current reading. Transcript tails are covered by deferred ANALYSIS-4707 | No new finding |
| Duplication | Inspection and `grep` of provider-home resolution, TrayProcess constants and interop | Provider-home resolution exists in four places (`ProviderTitleState`, `IntegrationSetup`, `HookDeliveryReporter`, `CodexSharedEndpoint`). `TrayProcess` keeps three declaration-only constants (`TokenTypePrimary`, `SecurityImpersonation`, `TokenAssignPrimary`). Nothing grew beyond what ANALYSIS-4710 recorded | Folds into deferred ANALYSIS-4710 |
| Complexity / simplification | Inspection of `LauncherIndependentBackend` and the optional `launcherIndependent` parameters | The predicate re-classifies the scanned prefix, and the parameter defaults to false. Capture is the only caller | Proportionate. No finding |
| Deferred items becoming blocking | Ledger review of the listed deferrals against the final code | None has become blocking. ANALYSIS-4707 matters less for Codex after 4.12, because fewer sessions use the transcript path | No escalation |
| Spec validation | `openspec validate adopt-native-sessions-from-hooks --strict` (read-only) | `Change 'adopt-native-sessions-from-hooks' is valid` | Passed |
| Build, tests, format verify | Not run | Owned by QA-063. Running them concurrently on the same tree would contend with QA's build outputs | Not performed (assignment) |

Client coverage: the architecture, docs and diagnostics checks cover Claude Code and Codex. ANALYSIS-5800 affects both providers, because they share the ingress. ANALYSIS-5802 and ANALYSIS-5803 are Codex-only, because only Codex has a shared backend. Claude's counterpart is a hook and needs no runtime read, so no parity gap follows. ANALYSIS-5801 affects both providers' user documentation.

## Findings

### ANALYSIS-5800: An update or dev switch keeps the hooks on the old ingress copy, and doctor cannot tell

- Severity: warning (non-blocking for this change's acceptance, because the 6.1 trial runs after a setup re-apply; decision needed for the follow-up)
- Related task / requirement: 4.5, 4.6 and 4.12 (behavior that lives in the ingress); 5.2 (doctor reports hook-start availability); 5.3; design decisions 6, 7 and 9
- Locations (reviewed identity 9550ced plus the uncommitted diff):
  - `src/BellTower.Ingress/BellTower.Ingress.csproj` 11-18: links `AttachmentCapture.cs`, `NativeAdoption.cs`, `AgentModeClassifier.cs`, `ProcessTable.cs`, and `BellTower.Protocol` (`HookTrayStart`, `TrayProcess`, `DesktopShell`, `HookStartTask`)
  - `src/BellTower/Setup/IngressProvision.cs` 31: the hooks call `integrations/provider-ingress/1/<hash>/bell-tower-ingress.exe`
  - `src/BellTower/Setup/IntegrationRefresh.cs` 7-38: the update refresh re-records the launcher and refreshes the shell module and the VS Code bridge, but never the ingress copy or the owned hook commands
  - `src/BellTower/Updates/UpdateTransaction.cs` 291-297
  - `scripts/install-dev.ps1`: no ingress or integration refresh
  - `src/BellTower/Diagnostics/HookStartReporter.cs` 21: `Available = launcher is not null`
  - `src/BellTower/Diagnostics/ConfirmationCoverageReporter.cs` 86-95: a narrow precedent that detects one legacy ingress revision
  - `docs/installation.md` 83 ("Setup apply, an update and every tray start record that launcher")
- Observation:
  - This change puts the mode classification, the decision 7 cross-check with its 4.12 revision, and the whole hook tray start (decisions 9 and 10) into the ingress binary.
  - Provider hooks call an immutable, hash-addressed copy of that binary. Only `setup ... --apply` writes it.
  - After an update, `IntegrationRefresh` records the new stable launcher, so doctor's `hookStart.available` reads true. The hooks still run the copy from the last setup. For a user whose hooks were set up with the released v0.1.9 (`aadbfb3`, which has no hook start and no native adoption), no hook ever starts a tray. Plain `codex` under an updater-restarted daemon stays `unknown`. Doctor gives no hint of either.
  - The ledger records the dev-switch form of this on 2026-11-24 (live check 1 on 0.1.9031: the new conversation was stored `unknown` until `setup --host-bridge --apply` reprovisioned the ingress).
  - `ConfirmationCoverageReporter` already tells the user to rerun setup for one older ingress revision. That shows the gap is known, but only for that one feature.
- Proposed improvement and benefit:
  - (a) Diagnostics, small and recommended: doctor compares the ingress hash in the owned hook commands recorded in `integration.json` with the selected package's `bell-tower-ingress.exe` hash from `release.json`. When they differ, it reports per provider "hooks call an older ingress image: rerun setup --host-bridge --apply (Codex may ask to trust the changed hooks)". `hookStart.available` (and, optionally, `nativeSessions`) then reads "available after setup refresh" rather than true. This needs no provider settings read and makes the matrix and doctor honest for updated installations.
  - (b) Behavior, a follow-up change that needs a user decision: re-provision the ingress and rewrite the owned hook commands during the update refresh and the dev switch, under the existing ownership and backup rules. This conflicts with Codex's hook-trust prompt, which a silent update would trigger, and with the rule that updates do not edit provider settings. A user decision should record whether (a) alone is enough.
  - Benefit: capture-side fixes from this change (and from later releases) reach updated users, or doctor at least tells them how to get them.
- Behavior-preservation risks:
  - (a) is read-only, but it must parse hook commands for each shell form (Bash, PowerShell and Cmd literals from `HookCommandBuilder`) and treat a command it cannot parse as unknown, not as stale.
  - (b) changes provider settings during update: Codex hook trust, unrelated-settings preservation, and the rollback path to an older payload whose ingress copy must still exist. Retained copies are never deleted today, which keeps rollback safe.
- Evidence type: source observation, plus the ledger's recorded live observation of 2026-11-24 for the dev switch. The update path is inferred from source and was not exercised.
- Verification / measurement plan:
  - For (a), a fixture with an `integration.json` whose owned commands name hash A and a selected package with hash B: doctor reports the stale image for Claude and Codex in each shell form. With equal hashes it reports nothing, and an unparseable command reports unknown.
  - For (b), an isolated update-transaction fixture showing the hook commands move to the new hash with backups, plus a rollback that still finds the old copy.
- Relationship: new. The precedent is the `ConfirmationCoverageReporter` legacy detection. This finding records the follow-up observed on 2026-11-24.

### ANALYSIS-5801: The user guide still says failed navigation always leaves the timer visible

- Severity: warning (a user document contradicts the implemented behavior and the spec; text only, non-blocking)
- Related task / requirement: 4.11; attention-notifications "Viewed wait timer" (scenario "A chip click shows a permanent navigation limit"); 5.3
- Locations:
  - `docs/installation.md` 67: "Failed navigation leaves the timer visible."
  - `docs/compatibility.md` 98, the "Mode cross-check" bullet: it still describes only the client rule, with no mention of the 4.12 managed-daemon exception. The table row on line 71 and the gaps list on line 106 do state the exception. This is incomplete rather than wrong.
- Observation:
  - Since DEV-063, a chip click whose result is the shared-backend limit hides that episode's timer once the notice is shown (`NavigationOutcome.cs:14-23`; `TrayApplication.cs:425-447`).
  - No user document mentions this. The one paragraph that describes the timer states the opposite for every failure.
- Proposed improvement and benefit: in `installation.md:67`, say that a click on a plain default `codex` (shared-backend) session hides the timer once its reason is shown. Every other failure (declined activation, stale or missing session, navigator failure, unverified terminal) leaves the timer visible, for both providers. Optionally add half a sentence to `compatibility.md:98`: the managed daemon (`app-server --managed-daemon`) is recognized without a parent check. The user guide then agrees with the spec and the trial record that task 6.5 checks.
- Behavior-preservation risks: none (text only). Keep the Overview and CLI sentence unchanged.
- Evidence type: source observation.
- Verification / measurement plan: run `grep -n "timer" docs/installation.md` and compare with the spec scenario. The `CapabilityMatrixTests` doc checks do not cover this paragraph.
- Relationship: new. Belongs to the same 5.3 documentation scope as DEV-065.

### ANALYSIS-5802: After 4.12, a refused shared read no longer falls back to the transcript reviewer signal

- Severity: suggestion (non-blocking; parity of confirmation evidence within Codex modes)
- Related task / requirement: 4.9 and 4.12; design decisions 7 (revision) and 12
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 106-119 (transcript only when `access is null`), 125-128 (`waiting is null` clears) and 135-144 (any failure, including backoff, clears)
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs` 57-59
- Observation:
  - Before 4.12, a plain `codex` conversation under an orphaned or updater-restarted managed daemon was stored `unknown`. `Select` returned null, so the decision 12 transcript signal (`approvals_reviewer` in `turn_context`) could confirm or suppress its approval waits.
  - After 4.12 the same conversation is `shared-backend`, so the pump only asks the shared backend. That is a better signal when it answers. When the read is refused or unanswered, the wait stays provisional and the transcript is not consulted. Refusal cases include a conversation of another Codex home read through the tray-home backend (expected to be refused, as the matrix states), a fork or delegated thread, an elevated backend, and a home backoff window.
  - The docs stay honest ("a failed status read" stays provisional). This is a lost opportunity, not a misstatement.
- Proposed improvement and benefit:
  - When the selected shared access yields no answer (null, a refusal reason or backoff), fall back to `TranscriptWaiting(check)` through the same `Record` path with `CodexTranscriptSignal`. A runtime answer stays authoritative when there is one.
  - Benefit: the shared-backend population that 4.12 created keeps the confirmation it previously had. It is one shared rule with one more evidence source, not a new per-mode policy.
- Behavior-preservation risks:
  - Whether a shared-backend (app-server) conversation's hook-named rollout carries `turn_context.approvals_reviewer` is unverified. The 4.8 probe covered `--no-daemon` only, so a probe must come first.
  - A fresh negative runtime status must still win over a transcript record.
  - The backoff must not be bypassed for the transport itself. The fallback reads only the local file.
- Evidence type: source observation. The transcript shape for shared conversations is a hypothesis.
- Verification / measurement plan:
  - An isolated-home probe of one held approval on a managed-daemon conversation, to confirm the `turn_context` record.
  - A pump fixture where the shared stand-in refuses (other home, fork) and the transcript says `user`: the wait is confirmed with `CodexTranscriptSignal`. With `auto_review` it is suppressed, and a runtime `false` still suppresses.
- Relationship: new. It is a consequence of ANALYSIS-5600 / task 4.12.

### ANALYSIS-5803: ANALYSIS-2405 (one shared open per second per waiting conversation) has no owner, and 4.12 makes its path the common one

- Severity: suggestion (tracking; performance hypothesis, not measured)
- Related task / requirement: 4.12; removed task 4.2; design decision 4 (bounded access to a backend Bell Tower does not own)
- Locations:
  - `src/BellTower/Sessions/CodexPermissionPump.cs` 27-61 (1 s cadence, up to 4 concurrent checks) and 120-123 (a new shared open per check)
  - `src/BellTower/Sessions/CodexRuntimeOpener.cs` 76-84
  - The ledger row for ANALYSIS-2405 ("folded into task 4.2") and the [tasks](../tasks.md) "Removed from this change" list ("Folded-in findings ANALYSIS-2403, 2404 and 2405 go with it")
- Observation:
  - ANALYSIS-2405 described a full peer-verified open (socket, peer ioctl, about 4 process queries, WebSocket upgrade, `initialize`) every second for each Codex conversation with a permission or question wait. Repeating per-thread refusals are logged every second.
  - It was folded into task 4.2, which decision 8 removed. It is therefore neither deferred in `docs/follow-ups.md` nor open in the ledger.
  - Before 4.12 this path served only shared conversations whose launching client was still alive. After 4.12 it serves every plain `codex` conversation under the managed daemon, the normal case per ANALYSIS-5600, so its reach grew with this change.
  - The `permission-verification-unavailable` line repeats every second for a waiting other-home or fork conversation. It can push other codes out of the 250 KB local log, as ANALYSIS-2405 noted.
- Proposed improvement and benefit: the orchestrator re-records ANALYSIS-2405 as a deferred follow-up independent of the binder (for example in `docs/follow-ups.md` with the other deferred native-session items). The fix itself stays as 2405 proposed: a per-session negative cache, log once per reason, and optionally one connection per home per poll. No code change in this change.
- Behavior-preservation risks: none for the tracking step. For the later fix, as in ANALYSIS-2405: invalidate the cache on `Replaced` and on scope changes.
- Evidence type: source observation for the rate. The cost is the ANALYSIS-2405 performance hypothesis and was not measured.
- Verification / measurement plan: the ANALYSIS-2405 plan, now with a managed-daemon stand-in. Run one waiting conversation for 60 s and count `Accepted` connections and log lines, for the tray-home case (about 60 opens expected) and for a refused other-home case (about 60 opens and 60 log lines expected).
- Relationship: ANALYSIS-2405 is canonical. This finding only records the missing owner and the wider reach after 4.12.

## Re-verification

No findings were assigned for recheck.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | not applicable | not applicable | not performed (not assigned) |

## Assessment and limitations

- Scope covered: the final 4.1 to 5.3 slice, including 4.5, 4.11 and 4.12, at `head=9550ced baseline=9550ced files=17 diff=e340e18da219 untracked=0/e3b0c44298fc`.
- Strengths: the target keeps one shared policy with thin adapters.
  - The 4.12 adapter fact and the Core rule are minimal and pinned at both ends (capture and transport).
  - The 4.11 outcome rule has one decision point for both providers.
  - The hook path keeps its measured near-zero cost while a tray runs.
  - The capability matrix and doctor wording for 4.12 and the first-hook timing match the code.
- Blocking findings: none. No deferred finding has become blocking.
- Recommendations for the orchestrator's triage:
  - ANALYSIS-5801 is a quick text correction that fits the 6.5 check of documentation against the trial record.
  - ANALYSIS-5800(a) is a small, read-only doctor addition that makes 5.2's hook-start availability honest for updated installations. Part (b) is a follow-up change that needs a user decision because of Codex hook trust.
  - ANALYSIS-5802 and ANALYSIS-5803 are deferrable suggestions. 5803 asks only that ANALYSIS-2405 be given an owner again.
- Not performed:
  - Build, tests and format verify (owned by QA-063).
  - Any live or installed check (prohibited).
  - Any measurement. The ANALYSIS-5803 cost and the ANALYSIS-5802 transcript shape are hypotheses.
  - The update path in ANALYSIS-5800 is inferred from source.
- Unexpected writes: none. The client refused the attempted write of this report, so it is returned inline. Identity was unchanged before and after.
