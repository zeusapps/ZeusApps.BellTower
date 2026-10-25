# Bounded correction plan 007

This is a read-only developer plan for canonical ANALYSIS-200/201 and the associated QA recovery/synchronization cases. It is not an implementation handoff or acceptance pass. Source remains frozen for QA006 and analysis006. Another source round requires the orchestrator's explicit release after user approval. Only this plan was written; no build, test, package, native probe, personal setting, or user process was touched.

Controlling material read: proposal, design decisions 4/7, development-builds delta spec, tasks 4.1/4.3/6.1/6.3, Analysis 005, its focused fix plan, Implementation 006, and the subsequently published immutable Analysis 006. Analysis 006 confirms the assigned canonical gaps. This plan does not supersede either independent review or claim their findings closed.

Frozen identity independently read using `scripts/review-identity.ps1`:

`head=b74a12b baseline=b74a12b files=32 diff=fb2c83821602 untracked=14/62017675a458`

Tracked diff SHA-256: `fb2c83821602833cddddf968dabeca328e8bc2317fb4a0b0bf73d0c75b4303bf`.

## Confirmed defects and bounded edits

1. `install-dev.ps1:772–776` recognizes restored monitoring with the candidate pointer, but pointer restoration is nested in the following `else` at `:810–825`. The retry proceeds to an original restart without selecting original.
2. Switch checks coordination before waiting for `InstallationGate` (`:749–750`) and writes the pointer without guarded post-wait tuple/hash/freshness checks (`:752–755`). Rollback's existing guarded pointer block checks bytes/tuple after waiting, but also needs freshness handling.
3. `Enter-MonitoringCommit:291–295` acquires partial locks before its cleanup `try`. A later `TryAcquire` exception leaks earlier locks. Its cleanup loops can also stop at the first disposal exception. The adjacent Switch snapshot path constructs three locks before `try` (`:721–723`), exposing the same partial-acquisition failure.
4. The interrupted-candidate fallback compares `restartLauncher.activation` with the manifest (`:420`), but never checks the live presence's activation before accepting its lineage and subsequently requesting exit.
5. A crash after restoring original pointer (`:820`) and before saving `restored-monitoring-snapshot` (`:824–825`) leaves original pointer plus `restored-monitoring-candidate-pointer`; current dispatch rejects it. Simply accepting original for that phase would also accept selections for which no pointer commit was attempted.

Use the existing operation manifest, coordination transition, record guards, data locks, and launch gate. No dependency, lifecycle framework, stable-launcher change, timeout increase, provider-specific policy, or unrelated refactor is needed. Expected production edit is principally `scripts/install-dev.ps1`, with meaningful tests under `tests/`; any probe/evidence paths must first be included in the next assignment.

## 1. One pointer-completion path and an exact write-ahead intent

Keep monitoring restoration conditional. Move the short pointer-completion path outside that conditional so both newly restored monitoring and its existing retry state reach it. Avoid duplicate dev snapshots or repeated state restoration once completion has been durably recorded.

Before the pointer replacement, add one bounded `rollbackPointerTransition` intent to the existing private operation manifest. Persist it only after:

- complete owned records have passed guarded post-gate validation;
- current pointer bytes exactly match `devSelectionSha256`;
- the private original snapshot and its `current.json` entry have been verified against `originalMonitoringFiles` and `originalSelectionSha256`;
- the manifest already records completed monitoring restoration.

Bind this intent to the operation's ID/token, normalized installation/data paths, Rollback phase and original target, the exact committed plan and its two marker byte hashes at publication, source candidate-selection hash, destination original-selection hash, and verified snapshot selection hash. Bind the parsed **full source and destination selections**, including each `version`, `previous`, and `activation`, as well as their exact byte hashes: source must match the verified proposed dev pointer and destination the verified original snapshot, preserving original activation bytes rather than minting a new activation. References to existing manifest expectations may be used rather than duplicating a new full plan format, provided recovery validates the exact bindings. The destination must equal both the preflight original hash and the verified snapshot's hash; the source must equal the recorded dev pointer hash. Save before pointer replacement, then replace/verify original bytes, then atomically save `state=restored-monitoring-snapshot` and remove the intent. This adds one write-ahead field to the existing manifest, not another journal or framework.

At recovery entry, validate any outstanding pointer intent against the **old committed** manifest expectation and exact old marker pair with proven-ended owner, before coordinator takeover or renewal changes the plan/hashes. Reconcile only the two publication states below. Any accepted takeover must remain the existing fully validated old/new coordination transition; a pointer intent cannot authorize recreating, overwriting, or ignoring drifted records. Do not require stale publication hashes to equal newly renewed hashes later in the invocation. Preserve the validated intent binding across takeover and clear it only with the completed original-pointer phase.

Allowed dispatch (all rows additionally require current full record ownership, correct paths/contracts and existing owner safety checks):

| Monitoring/operation phase | Exact pointer bytes | Allowed action |
| --- | --- | --- |
| `prepared`, `original-stopped`, `ready-to-select` before candidate activation | original hash | Existing never-selected recovery; compatibility/owner checks, then finish original restart/cleanup. No new broad phase acceptance. |
| `ready-to-select`, `selected-candidate`, `running-candidate` | dev hash | Stop only verified candidate, save dev snapshot once, restore monitoring, persist completed-monitoring phase, then enter shared pointer completion. |
| `restoring-monitoring-snapshot` | dev hash | Finish possibly partial monitoring restoration from verified original snapshot; retain the existing saved dev snapshot; then pointer completion. |
| `restored-monitoring-candidate-pointer`, no pointer intent | dev hash | Skip monitoring writes and dev snapshot; enter shared pointer completion and publish the intent. |
| Completed-monitoring phase with exact pointer intent | intent's source/dev hash | Pointer write did not commit; finish only the guarded pointer step. |
| Completed-monitoring phase with exact pointer intent | intent's destination/original hash | Pointer committed before phase save; verify destination, commit original-pointer phase, skip both monitoring and pointer rewrite, then restart/cleanup. |
| `restored-monitoring-snapshot`, `running-original` | original hash | Existing original-pointer retry; skip snapshots/monitoring/pointer replacement and finish only remaining restart/cleanup. |
| Any other phase/hash combination, missing required intent, mismatched intent, or drifted pair | any | Refuse before mutation/signal; preserve evidence and records. In particular, original hash plus `restored-monitoring-candidate-pointer` **without** intent is not sufficient. |

Keep `monitoringMutationStarted` true on all restored-monitoring/pending-pointer retry paths so failure cleanup retains records. Once pointer intent exists, keep recoverability even if a later exception interrupts the phase save. The selection write itself is atomic today; the intent covers the crash before its subsequent manifest publication.

## 2. Shared final pointer guard after gate acquisition

Apply the existing Rollback guard pattern to both pointer directions, with a common narrow helper only if that removes duplicated policy. The outside boundary performs renewal and process drain. After the potentially blocking launch-gate acquisition, acquire both read guards, bound each metadata file to 16 KiB, revalidate exact byte hashes and complete plans, and check the current freshness threshold. Hold guards through selection verification/replacement and the bounded phase/intent publication required above.

If freshness is insufficient, release all guards and the launch gate before renewal/drain/retry. Use the existing bounded operation wait policy; exhaustion refuses with records retained. Never perform process enumeration, child drains, renewal, sleep, payload hashing/copy, or tray waits under the launch gate. No data lock is held during gate acquisition. A drift/sharing failure refuses before pointer mutation. Guard acquisition must itself be exception-safe when only the first file has opened.

The pre-gate `Assert-OperationBoundary` remains necessary; a post-gate read-only check complements it. Repeating slow boundary work inside the launch gate would reintroduce the hook problem.

## 3. Exception-safe acquisition and cleanup

Place acquisition of **every** setup/state/presentation lock and record guard inside one ownership scope. Maintain a success/transfer flag: only a complete validated bundle transfers ownership to its caller. In `finally`, release every acquired guard and lock unless ownership transferred; locks release in reverse acquisition order. Busy, stale, acquisition-throw, guard-open failure, tuple/hash failure, and return/timeout must all use this cleanup.

Reuse the existing all-attempt cleanup behavior in `Release-MonitoringCommit`: one disposal exception must not skip later handles. Preserve the acquisition/validation exception as primary and report cleanup failures without replacing it; if cleanup alone fails, surface that failure. Only after all releases may a busy/stale path delay and retry outside locks. No background thread or asynchronous mutex ownership is introduced.

Apply this same scope to Switch's snapshot acquisition. Reusing the corrected existing bundle helper is acceptable if it preserves its zero-wait/retry policy, short snapshot-only commit, and renewal/drain outside locks. Otherwise use the same narrow `try/finally` ownership discipline locally; do not leave blocking constructor acquisitions outside cleanup.

`Wait-InstallationTrayStartsDrained:370` also still says monitoring was left intact after restoration. Make this existing failure report phase-accurate, including the truthful restored-monitoring/pending-pointer condition. This is part of canonical ANALYSIS-200's recovery reporting, not an optional wording change.

## 4. Bind live candidate presence before any signal

Before choosing the saved `candidateTray` or interrupted `restartLauncher` path, require the live owner version, data directory and activation to match the expected candidate and `manifest.coordinationId`. For the fallback, require receipt version/activation to equal those same expected values and the **live** owner's activation. Retain stable-launcher image, live launcher PID/start, tray image/PID/start, ancestry, and identity rechecks. A valid lineage must never compensate for a wrong live activation.

Immediately before `RequestExit`, re-read and validate the same candidate ownership requirements, preserving its exact PID/start/image checks. This closes drift between receipt validation and the existing stop helper's narrower owner comparison. Wrong/missing activation and every unverifiable owner refuse without writing `exit.request`, replacing records/pointer/state, or killing/adopting the foreign process.

## Decisive regression and acceptance plan

These are checks to run after authorization, not checks executed for this plan.

- **Actual intermediate retry:** complete monitoring restoration, stop the test-owned installer at `rollback-before-selection`, then resume the same manifest. Assert original pointer is selected **before** original launch, monitoring files and saved dev snapshot are not rewritten, original tray is proven, records are removed only on success. A pre-launch failure-only smoke is insufficient.
- **Pointer publication crash matrix:** disposable interruption after intent save/before pointer, immediately after atomic original pointer replacement/before phase save, and after phase save. Same-manifest retries succeed in each allowed state without duplicate monitoring restore. Native tests must end only the exact test-owned installer. Negative fixtures cover original pointer with no intent, wrong source/destination/snapshot/operation/phase bindings, source or destination activation/previous-field drift, mismatched marker bytes/tuple, live or unknown old owner, and an unrelated exact-version selection. Assert no mutation and preserved record bytes.
- **Post-gate Switch and Rollback:** hold launch gate in a separate test-owned process, wait until the installer is actually blocked acquiring it, mutate either marker's bytes/unknown field/full tuple, release gate, assert pointer unchanged and drifted markers preserved. Include aging beyond the existing freshness margin, proving gate/guards release before renewal/drain, and attempt mutation under acquired guards to prove replacement/write/delete is denied through the short commit. Use barriers outside the gate for slow hook tests; never introduce a long test pause under the production launch critical section.
- **Resource paths:** targeted function/behavior tests inject acquisition exceptions for second and third data locks, second guard open, and validation after a complete bundle; inject a disposal failure and assert all later disposals are attempted and primary error retained. A same-thread check after the failed call must demonstrate no recursive mutex ownership remains, while the test process stays alive; process exit/abandonment alone is not proof of cleanup. Cover busy/stale retry and Switch snapshot failure as well as Rollback. Use disposable opt-in injection or an extracted-function test harness; no general injection framework.
- **Live activation negative:** use the real presence-before-candidate-receipt interval with valid launcher ancestry; change only live presence activation while keeping receipt/lineage valid. Rollback must refuse before exit signal and preserve selection/state/markers with the tray still alive. Retain positive exact activation and negative receipt version/activation/PID/start/root/image/ended-launcher/foreign-tray controls. Add a second check that changes activation between the first ownership validation and attempted stop.
- Preserve the prior six coordinator-transition crash/drift cases, normal lock-wait drift, nested recovery failure, relative-root classifier refusal, stale/unknown presence cases, shared retention and launcher-byte protections. Both providers and mixed sessions use this same policy.
- On the final stable handoff, QA owns the fresh Release build/full suite/format/node, real package/plain-shell toolchain proof, final packaged NativeAOT benchmark, all four maintenance phases with Claude, Codex and mixed generated legacy/native commands while a slow barrier stays blocked beyond 3.5 seconds, whole-command three-second timing and durable counts, actual published 0.1.8 capture-0 proof separate from capture-1, and remaining process/receipt/teardown negatives. Existing QA006 results remain valuable but do not certify a future changed package. Keep the unrelated known font/STA failure separate if it recurs; no renderer fix is proposed.

Current primary API/path references were already retrieved through Context7 in the controlling focused plan: mutex ownership/thread affinity, Windows file-sharing semantics, and fully qualified path handling. This plan introduces no new API choice and makes no additional API claim requiring another lookup. Future implementation must fetch current primary documentation if an API uncertainty arises.

## Handoff and authorization boundary

Source identity above matched the assigned freeze. The only new path is this plan. Planning artifacts, tasks, ledger, reviewer reports, prior evidence, unrelated staged work and personal settings remain untouched. No checks beyond read-only source/evidence inspection and identity reporting were run. Await user-approved source release and the final QA006 findings before implementing or producing shared build outputs; immutable analysis006 is now incorporated.

```text
status: done
role / client / model: developer / Codex delegated subagent / gpt-6.1-sol
task / finding IDs: read-only plan 007; 4.1/4.3/6.1/6.3; canonical ANALYSIS-200/201 and related QA gaps
changed or report paths: openspec/changes/install-dev-builds/evidence/implementation-007-plan.md
checks: frozen source/evidence inspection -> completed; review-identity -> matched; build/tests/format/native/package -> not run (source freeze; plan-only assignment)
target identity: head=b74a12b baseline=b74a12b files=32 diff=fb2c83821602 untracked=14/62017675a458
limitations / decisions needed: source edits require orchestrator release after user approval; final independent 006 review is not replaced by this plan
next: await authorization and stable source ownership; then implement only the approved bounded correction with decisive regressions
```
