# Implementation 010 (DEV-010) - task 2.4: fault injection, tray downtime, upgrade and rollback

Client: Claude Code developer subagent. Not committed. Starting identity `head=02d4f56 baseline=02d4f56 files=83 diff=9ab7066ad5de untracked=65/fb48073de8b3`. No product code was changed; one product finding is recorded below for triage.

## What was built

Scenario `faults-and-rollback` (`tools/BellTower.FieldAcceptance/Scenarios/FaultsAndRollbackScenario.cs`, per-group run in `FaultGroupRun.cs`), registered in `ScenarioCatalog`. It runs three instances (Claude Code, Codex, both together; `FIELD_ACCEPTANCE_GROUPS` is a developer filter), 36 (Claude Code), 39 (Codex) and 45 (mixed) checks. Each instance moves through five phases and keeps its fake agents running throughout:

1. The published 0.1.9 with its own setup and tray (one-minute attachment grace, the product's minimum): a work conversation with historical work in its own ticket worktree (launched in the main checkout) and a wait conversation with one resolved operation and one pending wait.
2. The real update transaction upgrades to the fixture-version candidate with the tray running, while post-only hooks keep arriving through the 0.1.9 image (every one accepted and applied once); then `setup --host-bridge --apply` makes the hooks name the candidate's ingress image.
3. Fault injection on the candidate: null-process startups (a second fake agent image whose name the product's ancestry walk does not recognize as the provider; one conversation is later hit by a hook from the identifiable agent process), lost pre (post only), late pre (a pre hook and a permission request after the post), the ordinary pre/permission/post flow, duplicated pre and post hooks, tray downtime (the user's Exit, so no hook can start a tray) with durable spooling, a shuffled and partly duplicated spool drained by the restarted tray, and a branch change that leaves a `retained` checkout with the additive `checkoutWork` field.
4. Rollback to the real 0.1.9 while the hooks still name the candidate's ingress image: tray exit, the product's own capture and attachment-health compatibility check, a manual rewrite of `current.json` to the retained 0.1.9 payload (the documented manual restore; the product has no rollback command; the update coordinator's own recovery does the same two steps), no tray yet. The first reads are 0.1.9's own `list`, `doctor` and `statusline`; then hooks while the 0.1.9 tray is down, the 0.1.9 tray starts and drains the candidate-written spool, the faults again, liveness and the retained-checkout effects.
5. The candidate is selected again and the faults, waits, identity and retained effects are checked again, followed by stable-state, no-process-termination and real-environment-exposure checks.

Reused: `CandidateInstall.Command`, `AgentSession.Hook` (the only hook path), `TowerReader`, `SandboxGit`, `TranscriptModel`, `ProviderModel`, `UpdateTransaction` through `Instance.WithProcessEnvironment`, `Unfinished`/exit-code conventions. No scenario code branches on a provider name: provider differences come from new `ProviderModel` members (`ToolPayload`, `HistoryRecords`, `UnclassifiedStartupFailsClosed`, `LaunchKinds`).

## Per-check results (final runs, evidence `harness-2.4.md`)

Claude Code, Codex and mixed rows are the three groups; the final run has 36 (Claude Code), 39 (Codex) and 45 (mixed) checks (the fixture grew by one wait conversation per real launch kind, per-kind delivery checks and the 0.1.9 setup rerun).

| Check | Claude Code | Codex | Mixed |
| --- | --- | --- | --- |
| `baseline/installed`, `baseline/sessions` | pass | pass | pass |
| `upgrade/under-load` (update with tray running; hooks during the window accepted and applied once; pending wait kept; no resolved wait back; agents alive) | pass | pass | pass |
| `upgrade/hooks-reference-candidate-image` | pass | pass | pass |
| `candidate/work-identity-kept` | pass | pass | pass |
| `candidate/null-process-startup` (no process identity, hook accepted, capture durable, not verified live) | pass | pass | pass |
| `candidate/null-process-repaired-or-unavailable` | pass (repaired) | pass (unavailable, by adapter policy) | pass (one of each) |
| `candidate/null-process-unavailable` (the unrepaired one reads unavailable after the grace, never working) | pass | pass | pass |
| `candidate/lost-pre-hook`, `late-pre-hook`, `pre-permission-post-flow`, `duplicate-hooks`, `live-fault-hooks-accepted` | pass | pass | pass |
| `candidate/tray-downtime-durable` (captures wait durably, exact count, no hook-started tray) | pass | pass | pass |
| `candidate/shuffled-and-duplicate-delivery` (causal result; journal gains exactly the hooks sent) | pass | pass | pass |
| `candidate/retained-checkout-established` (`retained` source, `checkoutWork` stored) | pass | pass | pass |
| `rollback/selected` (0.1.9 selected, hooks still name the candidate image, agents untouched) | pass | pass | pass |
| `rollback/reads-candidate-identity` (0.1.9's own `list`, `doctor`, `statusline` before any 0.1.9 tray pass: identity fields, names, colors, pins, `retained` source, `checkoutWork`) | pass | pass | pass |
| `rollback/tray-and-delivery` (0.1.9 tray only; spool written by candidate ingress drained; no hook-started tray) | pass | pass | pass |
| `rollback/candidate-hooks-applied-by-0.1.9/<kind>` and `upgrade/candidate-hooks-applied-after-setup-refresh/<kind>` (per launch kind) | pass (2 kinds) | plain `codex --no-daemon`: **expected failure observed** (documented limitation); managed and shared pass | same, Codex part only |
| `rollback/setup-rerun-points-hooks-at-0.1.9-image`, `rollback/setup-rerun-restores-delivery/<kind>` | pass | pass | pass |
| `rollback/faults-under-0.1.9` (lost, late, duplicate against 0.1.9) | pass | pass | pass |
| `rollback/no-resurrected-waits` | pass | pass | pass |
| `rollback/liveness` (0.1.9 reads the same verdicts) | pass | pass | pass |
| `rollback/retained-checkout-effects` (expected results, ANALYSIS-186) | pass | pass | pass |
| `reupgrade/selected`, `reupgrade/faults-and-waits`, `reupgrade/identity-and-retained-effects` | pass | pass | pass |
| `final/stable-state`, `final/no-process-termination`, `final/real-environment-exposure` | pass | pass | pass |

## Product finding 1 and 2 (one root cause; open for triage, no product code changed)

Follow-up result (orchestrator question: which real Codex launches are affected). The first version of the fixture gave a fake Codex a managed attachment id without `BELLTOWER_DEDICATED=1`, a combination no real launch produces. The fixture now models each real launch kind faithfully (`ProviderModel.LaunchKinds`) and one wait conversation per kind runs the whole scenario.

Source facts (file:line):
- Bell Tower managed Codex launch: `Integration/BellTower.psm1:40` exports `BELLTOWER_ATTACHMENT`, `:55-58` prepends `--no-daemon` and sets `BELLTOWER_DEDICATED=1` unless `--remote`; `Sessions/AgentLauncher.cs:32-33` sets both (the marker only with `--no-daemon`). Claude Code gets the attachment id only (psm1:40; no dedicated concept). A plain `codex`/`claude` carries no `BELLTOWER_*` variable.
- 0.1.9 ingress (`git show v0.1.9:src/BellTower.Ingress/Program.cs:33-41`): a Codex hook without `BELLTOWER_DEDICATED=1` is not dedicated and gets the id `codex-session-<hash(session)>`; with the marker it keeps the managed id. 0.1.9 `AttachmentCapture.cs:30-31`: id = `BELLTOWER_ATTACHMENT`, else `<provider>-<pid>-<start ticks>`.
- Candidate ingress: id from `Hosts/AttachmentCapture.cs:110` (env, else `NativeAdoption.OwnId`, `Core/NativeAdoption.cs:275`); `Providers/AgentModeClassifier.cs:93` (`app-server` is the shared backend), `:120` (any other interactive invocation, including `--no-daemon`, is `embedded-runtime`); `NativeAdoption.Apply` `:101` keeps a direct mode's own id, otherwise `Shared()` `:161-170` with `SharedId` `:272` (the same `codex-session-<hash>` formula as 0.1.9).
- Reducer, 0.1.9 and candidate alike (`Core/SessionReducer.cs:37`, `git show v0.1.9:...:37`): an event whose attachment id differs from the session's is ignored unless it is a registration. The candidate's consumer rewrites the observation to the adopted attachment (`Providers/CaptureConsumer.cs:71-75`); 0.1.9's has no rewrite.

Per real launch kind (installed-harness result, three runs of each group; all of these were run with the candidate image and again with 0.1.9):

| Launch kind | 0.1.9 registers it as | Candidate ingress emits | Candidate applies hooks after the setup rerun | 0.1.9 applies the candidate's hooks after rollback | Delivery after 0.1.9's own `setup --clients both --apply` |
| --- | --- | --- | --- | --- | --- |
| Claude Code, Bell Tower launch (attachment env) | env id | env id | yes | yes | yes |
| Claude Code, plain | `claude-<pid>-<ticks>` | same | yes | yes | yes |
| Codex, Bell Tower launch (`--no-daemon`, attachment id and `BELLTOWER_DEDICATED=1`) | env id | env id | yes | yes | yes |
| Codex, plain shared backend (hook under `codex app-server`) | `codex-session-<hash>` | `codex-session-<hash>` (`Shared`) | yes | yes | yes |
| **Codex, plain `codex --no-daemon`** | `codex-session-<hash>` (not dedicated) | `codex-<pid>-<ticks>` (direct, own id) | **no** | **no** | yes (hooks name the 0.1.9 image again, which emits the session id) |

CORRECTED by implementation-011 (ANALYSIS-240): the sentence that followed here, that finding 1 is not rollback-specific and not reachable through Bell Tower's own launches, was true only for the modeled launch kinds. The default Bell Tower Codex launch (owned `--remote` runtime) is followed by the candidate but dropped by a rolled-back 0.1.9. Original text: finding 1 (rollback) is not a rollback-specific hazard and not reachable through Bell Tower's own managed launches. It is one case of a forward-path behavior (finding 2): a plain `codex --no-daemon` conversation that 0.1.9 registered keeps its attachment `codex-session-<hash>`; once its hooks run the candidate's image (after `setup --host-bridge --apply`), the candidate ingress emits the process id, the reducer ignores every non-registration event (permission requests, completions) in the candidate tray as well as in a rolled-back 0.1.9, until the conversation registers again (a resume). Conversations started after the setup rerun have the process id from their first event and are followed by both versions (the `fault`, `work`, `ghost` and `heal` conversations and every Claude Code conversation). A Bell Tower launched Codex that runs through the owned `--remote` runtime (psm1:105) was not modeled (it needs a runtime manager record); by source its id is the launch's managed id when `Reconcile` verifies the owned runtime record (`Core/NativeAdoption.cs:178-205`) and `codex-session-<hash>` otherwise, so a mismatch is possible only if that verification fails; unverified here.
- Whether a running Codex process picks up the rewritten hook command without restarting is not established by the archived Codex evidence (`fix-codex-hook-command-shell` evidence covers the command form and shell, not live reload; setup output says Codex may ask to trust the changed hooks through `/hooks`). If a running `--no-daemon` session keeps its 0.1.9 command, it keeps emitting the session id and is followed; the drop then only concerns a session that reloads hooks mid-conversation. This is the reachability caveat.
- Recovery in the harness: rerunning 0.1.9's own `setup --clients both --apply` after the rollback (0.1.9 has no `--host-bridge`) points the hooks at the 0.1.9 image and delivery is whole again for every kind; the check `rollback/setup-rerun-restores-delivery/*` passes for all five kinds.
- Checks (final run, `harness-2.4.md`): `upgrade/candidate-hooks-applied-after-setup-refresh/wait-plain` and `rollback/candidate-hooks-applied-by-0.1.9/wait-plain` fail for Codex and mixed (4 failures), and every other kind passes. User decision 2026-11-26: documented, not fixed, in 0.1.11. The two checks are declared expected failures (`LaunchKind.HooksLostAcrossUpgrade`, same reporting style as the U1 expected failure): they pass while the loss is observed and fail, with "UNEXPECTED, the documented loss no longer occurs", if it stops. User-facing recovery: rerun setup, then resume or restart that conversation. Recorded in `docs/distribution-validation.md` and the ledger rows F2, F4 and U1; 4.4 owns the release note.

## Observation (policy, not a defect): unclassified startup per provider

A startup whose hook captured no agent process is repaired by a later hook from the identifiable process for Claude Code, but not for Codex: an unclassified Codex startup fails closed to the shared runtime (`AgentModeClassifier.MayShareBackend`, `NativeAdoption.Apply`) because Codex may share a backend, so the same conversation reads honestly unavailable (no process, activity unavailable, not working). The check expects the documented outcome per provider (`ProviderModel.UnclassifiedStartupFailsClosed`, pinned to `MayShareBackend` by a unit test). The task text accepts "repaired or unavailable"; if the parity rule needs the Codex outcome treated as a gap, that is a decision for the orchestrator.

## Deferred findings handled

| Finding | Resolution |
| --- | --- |
| ANALYSIS-122 (exposure guard and tray replacement must be enforced) | Every hook goes through `AgentSession.Hook` (so the instance exposure notes a hook-started tray); every tray-down phase follows the user's Exit (user stop recorded and checked); after each such phase `TrayStayedDown` ends a running tray at once (exit, then direct end) and fails the phase check; `final/real-environment-exposure` asserts no hook started a tray. CORRECTED by implementation-012 (ANALYSIS-244, -265): the Codex and mixed groups do run a modeled shared-backend Codex (a fake `app-server` agent, no real backend), which the guard allows only while no hook starts a tray in the same instance; Codex sessions use `--no-daemon` except that kind and the owned-runtime kinds. Original text: asserts no hook started a tray and no shared-backend Codex ran. Codex sessions use `--no-daemon`. |
| ANALYSIS-170 (fixture and surfaces reader reuse) | `Harness/ConversationFixture` (worktrees, change folders, modeled history); the transcript branch moved into `ProviderModel.HistoryRecords`; `FiveConversationsScenario` uses both (check ids and details unchanged); `ConversationSurfaces.ReadSettled` now takes the candidate version and `RequireCandidate` refuses any other installed version with a clear message. |
| ANALYSIS-171 (`WithProcessEnvironment` contract) | `Harness/ProcessEnvironmentSwap`: the swap is single-threaded and enforced; `Instance.Create`, `Instance.Run` and `AgentSession.Start` throw on another thread while it runs, swaps do not nest, the restore also removes variables the call created. The benchmark scenario restores the thread-pool minimums in a `finally`. Unit tests cover all of it. |
| ANALYSIS-186 (rollback effects on retained checkouts) | Recorded as expected results by `rollback/retained-checkout-effects` and `reupgrade/identity-and-retained-effects`: 0.1.9 reads the `retained` source and keeps `checkoutWork`; its first passes record the launch checkout; upgrading again does not restore the retained checkout. Release-note text for 4.4. |
| ANALYSIS-162 note (do not judge 0.1.9 state with the candidate's policy) | 0.1.9 state is read only through 0.1.9's own commands and raw JSON (`Harness/StateView`); `ConversationSurfaces` refuses anything but the candidate. |
| ANALYSIS-142 (hook starts through `Instance.Run`) | Unchanged (convention documented); this scenario has no such path. |

## Changed and new paths

- New: `tools/BellTower.FieldAcceptance/Scenarios/FaultsAndRollbackScenario.cs`, `FaultGroupRun.cs`; `Harness/ProcessEnvironmentSwap.cs`, `ConversationFixture.cs`, `SpoolInjection.cs`, `InstallSwitch.cs`, `StateView.cs`, `IngressImage.cs`; `tests/BellTower.Tests/FieldAcceptance/FaultInjectionTests.cs`.
- Changed: `tools/BellTower.FieldAcceptance/Scenarios/RunContext.cs` (catalog), `FiveConversationsScenario.cs`, `PackagedHookBenchmarkScenario.cs`; `Harness/Instance.cs`, `AgentSession.cs`, `FakeAgent.cs`, `ProviderModel.cs`, `ConversationSurfaces.cs`, `TowerReader.cs`; `docs/distribution-validation.md`, `docs/development.md`.
- Evidence: `evidence/harness-2.4.md`, `evidence/harness-all-010.md`, `evidence/implementation-010.md`, `evidence/coverage-ledger.md` (rows F2, F4, U1 and the baseline data note).

## Checks run

- Release build (`dotnet build -c Release --no-restore`): 0 warnings, 0 errors.
- FieldAcceptance unit tests (`--filter FullyQualifiedName~FieldAcceptance`): 102 passed, 0 failed (new: `FaultInjectionTests`, 11 test methods).
- Format verification on every changed `.cs` file (`dotnet format ... --verify-no-changes --include <files>`): clean.
- New scenario `faults-and-rollback`, all three groups, with `-RebuildCandidate` (`harness-2.4.md`): 132 of 132 checks passed, exit 0 (11 guard self-test, 1 production-unchanged, 120 in the three groups: 36 Claude Code, 39 Codex, 45 mixed). Four of them are declared expected failures (plain `codex --no-daemon`, Codex and mixed, upgrade and rollback; see below). Production fingerprint unchanged. Before the user decision the same scenario ran at 128 of 132 (the same four as plain failures), and earlier at 100 of 102 with the first fixture model.
- `--scenario all` once (`harness-all-010.md`): 279 of 281 passed, 2 unfinished parity items (the known Codex native-title tier of `five-conversations`), 0 harness failures, 5 expected-failure checks observed (the known U1 defect plus the four above), exit 3 ("passed with unfinished items").
- Release build after the runs: 0 warnings, 0 errors. Full suite once: 2237 passed, 1 failed: `BellTower.Tests.Providers.CodexTransportTests.ASupersedingListenerIsSeenByTheNextOpenAndTheVerifiedConnectionKeepsItsBackend` (a loopback transport test, load-sensitive); rerun alone: passed. Unrelated to this task. (An earlier suite run, before the follow-ups, failed `CodexSharedAccessTests.ARootThreadWhoseIdentifiersAgreeIsMappedAndReadWithoutTurnsOrResume` and passed alone; the failing test differs between runs.)
- Processes: only the harness's own sandbox processes were started and ended; the user's trays (8060, 28388) were not touched; no leftover process remained after the runs.
- Not run: node tests (nothing under `vscode/` changed); `./scripts/format.ps1` on the whole solution (dirty tree; only the changed files were formatted and verified).

## Decisions needed

1. (Decided 2026-11-26: documented, not fixed; expected failures declared.) Findings 1 and 2 (a plain `codex --no-daemon` conversation registered by 0.1.9 is not followed after the setup rerun, nor by a rolled-back 0.1.9, until it registers again): fix in the product (the candidate adopts the session's existing attachment id for such a conversation), or accept as a documented upgrade limitation (4.4 release note; rerunning the matching version's setup is the recovery), then flip the two per-kind checks to expected failures. Plain `codex --no-daemon` only; the default owned-runtime Bell Tower Codex launch has a separate rollback-only loss (implementation-011, ANALYSIS-240).
2. Codex null-process startups are not repairable by a later hook (fail-closed to the shared runtime, adapter policy): accept as the documented outcome (the check pins it) or treat as a parity gap.
3. The rollback is the manual selection of the retained payload because the product has no rollback command; confirm this stand-in is acceptable for 2.4 and for 4.4 on the release-commit package.
