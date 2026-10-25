# Focused corrective plan: ANALYSIS-200/201/202 and maintenance-test cleanup

Orchestrator-requested read-only plan after the user authorized one additional focused round. Target remains `head=b74a12b baseline=b74a12b files=27 diff=41ba2dbe9660 untracked=10/88d345b46144`. This document recommends implementation and checks; it is not executed acceptance or an implementation approval. No implementation, earlier report or ledger was edited.

## Keep the change bounded

Retain the non-owning tray-instance reservation, shared updater serialization, short launch sections, stable launcher/old payload bytes and existing NativeAOT ingress. Correct the current installer helpers rather than adding a lifecycle framework. Keep all provider/session policy shared. Do not increase any product hook deadline or mutate real provider settings.

## 1. Exact boundary ordering (ANALYSIS-200)

Use one explicit boundary helper for owned-record validation/renewal, conservative all-tray-start drain and final validation. Invoke it outside launch and data locks:

1. After reserving the named tray-instance object, creating/taking over only owned records and stopping the exact verified tray, before acquiring shared data locks or taking/restoring monitoring state.
2. After slow candidate copy/hash, original-byte assertions and the Switch test barrier, immediately **before** the short launch section that compares/writes `current.json`.
3. In Rollback, immediately before shared monitoring restoration; after the Rollback barrier, again immediately **before** the short original-pointer restore. A child or marker problem injected after state restoration must still refuse before pointer mutation; tests must distinguish that from the pre-restoration refusal which preserves both state and pointer.
4. After all slow post-selection verification, immediately **before** releasing update ownership/reservation for restart. Keep an exact fresh owned restart record across the bounded authorized restart.

At each boundary: assert both current records still match their expected bytes and complete operation tuple; renew the allowed timestamp/owner transition; update expected hashes; drain all relevant live starts, including starts from an expired interval; reassert record identity/freshness after the drain. If the drain consumes the freshness margin, renew and validate again before proceeding. Keep a generous remaining margin for the existing bounded startup wait. Do not release the reservation on an unchecked expired/unknown state.

All waits/probes remain outside launch/data locks. No payload copying/hashing, process enumeration or polling goes into the launch section. Refusals preserve the actual point reached in the manifest; do not falsely claim monitoring was unchanged if it was already restored.

## 2. Actual argv and conservative process classification (ANALYSIS-200)

- Parse the **complete Unicode command line** with the native Windows parser or an equivalently trusted, tested implementation. Prefer a small reusable Windows argv helper over regex approximation. Validate argv[0] against the live executable image; do not merely trim a path prefix.
- Resolve the effective command/options according to Bell Tower's actual CLI syntax. Cover quoted `tray`, recursive `--data-dir` before/after the command and accepted assignment syntax. Use the existing nonexecuting parser if practical; otherwise recognize a bounded set of proven forms and refuse ambiguous forms. Never invoke command actions while classifying.
- Identify the live image independently of CIM's optional `ExecutablePath` when necessary. A missing CIM image must not silently exclude a potentially relevant process. A limited-access image query can supply a fallback. Provably outside-installation images can be excluded; unresolved potentially relevant identity fails closed, without touching that process.
- Tie image/argv inspection to one exact PID/start-time tuple, revalidate afterward and while waiting. Keep CIM's microsecond correlation separate from the exact authoritative `ProcessProbe` tuple. Proven-ended processes can disappear harmlessly; PID reuse or unknown identity cannot be accepted as the former child.
- Drain relevant tray processes for the operation's data directory. Do not wait for or kill normal, conclusively classified event/session helpers simply because they use the same payload image. Treat unknown commands/options/data roots as unknown rather than harmless non-tray commands. Different known data roots must be handled deliberately and must never be stopped as this operation's tray.
- Keep command lines only in memory. Diagnostics name sanitized categories/PIDs, never raw argv, private values or tokens.

Primary documentation was fetched through Context7 for [CommandLineToArgvW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-commandlinetoargvw): it parses full Unicode argv, including quoted program names and Windows quote/backslash rules; failure returns NULL and the returned allocation requires one `LocalFree` call. Do not replace those rules with a hand-written regular expression.

## 3. Both-direction restart identity (ANALYSIS-200)

Record the launcher PID/creation tuple for **every** authorized restart, including original-version Rollback. Require the returned tray's exact process identity, installation image, selected version, data root, activation ID and ancestry from that live operation-owned launcher. Version alone is insufficient. Retain the operation marker until that receipt is verified. No foreign/newly discovered tray is signalled or adopted to make the check pass. Keep no-tray operations stopped as already specified.

## 4. Complete marker and phase binding (ANALYSIS-201)

- Define one expected immutable operation tuple: ID, token, normalized installation/data roots, original version, phase-specific target, contract binding and `HadTray`, plus the exact recorded owner identity and expected publication state. Both pending and restart records must agree with this expected operation. Persist enough phase/expected-record information in the private manifest to resume safely.
- For records created/renewed by the current process, compare the current byte hashes with the last expected hashes **before** overwrite or deletion, in addition to semantic identity. An ID/token match does not authorize overwriting changed roots, versions, owner or extra/unknown fields. A renewal may change only explicitly allowed timestamp fields; a dead-owner takeover is a separate validated transition.
- Validate **both** prior records before changing either. For recovery, require the full matching operation/phase tuple and a proven-ended prior owner; require the restart's prior owner to match the pending record's prior owner. Live or unverified owner, missing partner, mismatched fields/hash or unrelated marker: refuse and preserve bytes before data/selection changes. Ordinary Stage/Apply callers must keep their current refusal behavior.
- A failed Switch has target=candidate. A failed Rollback has target=original. Recovery must use the recorded phase's expected target, rather than always expecting candidate. Do not weaken checks by accepting either arbitrary version.
- Retry-state handling is also required: after a Rollback already restored the original pointer, its manifest may be `restored-monitoring-snapshot` or a later original phase. Resume that validated phase idempotently, avoiding a second monitoring restore/snapshot, and complete only the authorized original restart/owned cleanup. Fixing only the target comparison would still reject the current state branch.
- Retain recoverable own records after post-selection failure. For before-selection refusal, remove only records demonstrably still owned/unchanged; preserve any drifted bytes and provide the actual recovery state. Partial or externally changed records must not be silently reconstructed from ID/token alone.

## 5. Ended vs unknown tray presence (ANALYSIS-202)

Inspect the recorded process with the tri-state identity probe before asking for its live image:

- Proven-ended exact identity with no other live startup/owner: treat presence as stale/no live tray; preserve the file unless existing ownership rules authorize cleanup.
- Matching live identity: verify data root/image/version and normal ownership.
- Access denied, missing evidence that cannot prove exit, or an existing non-reserved tray object without verifiable presence: refuse as unknown ownership. Do not interpret failure to inspect as proof of exit. A verified reused PID can prove that the *recorded* identity ended, but never authorizes adopting or signalling the new process; treat old presence as stale only when the surrounding object/startup checks also establish no other live tray owner.
- During reservation, object existence is expected maintenance coordination. Outside reservation, an existing object with absent/unverifiable presence still requires safe refusal. Correlate process/object evidence so an old ended presence cannot hide a different currently starting tray; the conservative start drain remains necessary.

This must support native stopped-tray Preflight and both directions' restart-failure recovery without manually deleting `tray.json`.

## 6. Maintenance-test cleanup and controlled scheduling

The current test releases its checkpoint in `finally` but awaits the Cleanup task afterward. An assertion exception skips that await, allowing workspace disposal while the worker still owns a lease. Move release/reset/**join** into exception-safe teardown before any workspace/event disposal. Preserve and report the primary hook/assertion failure; if cleanup also fails, retain both errors instead of masking the primary one.

For a timed-out test-owned shell, termination request is not proof of exit. Reap the owned shell and its recorded owned launcher/payload descendants and finish pipe tasks before deleting the disposable installation. Never stop a user or unrelated process. Cover intentional assertion/timeout injection to prove lease release, joined worker and no lingering owned descendants.

Run the controlled legacy cases as individual Claude, individual Codex, then one simultaneous Claude+Codex pair while the slow phase remains blocked. That is still four events and explicitly covers mixed sessions without launching all four managed shell chains together. Isolate these heavy process cases from unrelated concurrently starting native fixtures, narrowly rather than globally disabling ordinary tests. Keep the actual three-second deadline measured across process launch, input, completion and durable response; if a controlled case still exceeds it, report failure rather than retrying away samples or changing the product deadline.

The eight-way legacy stress measurements and AOT budget are separate evidence. Do not claim arbitrary-load legacy timing is fixed by removing the gate. Preserve the native benchmark's current median and per-sample timing/durability gates; its successful 242-capture/50-legacy result does not erase a failed maintenance fixture.

## Focused regression matrix before stable handoff

| Area | Required cases and observable result |
| --- | --- |
| Pre-mutation boundaries | Inject unknown/paused child, marker drift and expired marker during Switch slow barrier: reject before pointer change; inject before Rollback state restoration: state/pointer unchanged; inject after its restoration barrier: pointer unchanged and manifest honestly records restored state |
| Old-order child | Pause actual/disposable old-order startup after successful `CanStartTray` and before mutex creation; resume under reservation; it exits before mutation/release. An undrained child is refused without a kill |
| Process parsing | Verb-first, quoted `tray`, recursive option before/after, accepted equals syntax, spaces/apostrophes/Unicode roots; valid event/session helpers are excluded; missing image/argv, PID reuse and unknown forms refuse safely |
| Marker drift | Independently change each semantic field of pending and restart while retaining ID/token; add an unknown field/byte change; neither record may be overwritten/deleted. Test missing partner and live/unverified prior owner |
| Phase recovery | Native Switch restart failure → same-manifest original recovery; native Rollback restart failure after pointer already original → idempotent same-manifest completion. Correct phase target and manifest state, no repeated state restore, only own records removed |
| Presence | Absent; proven-ended stale after real tray exit; live verified; live unknown; reused PID; existing unreserved object without presence. Stopped old 0.1.8 Preflight succeeds; ambiguous owner remains untouched |
| Authorized restart | Both old and dev payloads, matching activation and recorded launcher ancestry; wrong activation/lineage/version/data root refused, foreign owner never stopped |
| Hooks/retention | Switch, Rollback, Cleanup and Retire blocked phases: individual Claude/Codex then mixed pair complete/durably persist before release; native mixed captures remain callable; selected/previous/recovery/leased payloads stay protected |
| Test failure teardown | Deliberately fail a hook assertion and a timeout; primary error retained, Cleanup worker joined, owned descendants reaped, lease closed and disposable directory removable |

After focused cases pass, freeze source and hand off exact identity for both independent reviews. QA then runs final native package, full suite with rebuilt post-package ingress output, scoped format, JavaScript tests, disposable lifecycle, read-only maintainer Preflight and current-package ingress benchmark. Distinguish generated-shell/native fixture evidence from authenticated field behavior. No live Switch or real integration apply is implied.

Reviewer: code-analyzer / Codex CLI 0.164.0 / `gpt-6.1-sol`. Checks performed for this plan: stable identity and source inspection; current primary Windows argv documentation. No code edits, build/test execution or runtime claims.
