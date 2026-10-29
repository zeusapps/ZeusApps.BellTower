# Next release implementation handoff

Created 2026-10-23 for the release after 0.1.8. This is an implementation handoff for fresh Codex/Claude sessions, not evidence that any feature has been implemented.

## Fast ingress implementation follow-up

The speed-up change now exports the [final capture-v1/consumer contract](../hook-ingress.md) for identity, permission and liveness work. Use shared normalized ingress/source evidence, compact candidates and idempotent receipts; preserve the legacy normalized inbox and immutable ingress images through compatible updates. Do not reintroduce the PowerShell/launcher/full-app chain for capture or persist raw commands/responses.

The [verification record](../../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/verification.md) retains actual local benchmark and packaged lifecycle evidence. The [parity audit](../../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/parity-audit.md) covers both providers and mixed sessions. Claude 2.1.306 timeout records were obtained in an isolated SDK probe. Codex 0.164.0 exec and local terminal probes prove that the tested SessionStart timeout is absent from chat history; complete retrospective transcript counts use an explicit unavailable status and the common health/native-status fallback. This is a narrow evidenced surface restriction, not a capture/identity exemption. Unperformed authenticated/RDP field acceptance remains in the release acceptance change.

## Release commitment and current state

The user requires **the retained items in both field reports, the original VS Code foreground-activation defect, and the legacy badge behavior described below in the next release**. The work is divided into independently reviewable OpenSpec changes, not deferred across releases. Microphone-aware speech, local reminders and signing remain mandatory release scope.

**Scope cancellation 2026-10-29:** the user dropped remote webhook notifications and all Teams integration plans. The unimplemented `add-secret-backed-alert-webhooks` proposal was removed, not completed or deferred. Remove endpoint provisioning, credential storage, delivery and remote acceptance requirements from the release gate. Field finding 12 retains local reminders. Existing reminder receipt/candidate metadata is retained for compatibility and creates no remote delivery commitment.

**Amendment 2026-10-27, reconciled with the 2026-10-29 scope cancellation:** the user authorized an early stable release, **v0.1.9**, from the completed work (repository subagents, dev builds, badge identity, VS Code window activation, human-confirmed permission waits) so it can be used on the field machine. This is a deliberate exception: the retained changes (reminders, microphone-aware speech, native-session adoption, signing/update policy, field acceptance and `fix-codex-hook-command-shell`) move to the following release; read "next release" below as that one. v0.1.9 knowingly ships the unverified Codex Windows hook-shell defect (H-1); the field machine does not use Codex.

The baseline is the single published GitHub release v0.1.8. The source at planning time is commit 74e1f41 (documentation after the v0.1.8 tag), with no implementation changes made during this planning work. The next version is not reserved here; 0.1.9 is provisional until the release step verifies it is unused. Do not publish or move the v0.1.8 tag/assets.

This workspace is D:/Code/ZeusApps.BellTower. The field machine is a different Windows machine. Its C:/Code/D360 paths below are illustrative field facts, not paths to create or inspect on this machine.

The original plan created ten changes with proposal.md, design.md, specs/<capability>/spec.md and tasks.md. Nine remain in this execution sequence after the scope cancellation; completed changes may now be archived. Do not interpret OpenSpec's planning-complete status as implemented or accepted.

The companion [field report notes](claude-field-reports-0.1.8.md) preserve the supplied evidence, old badge rules and corrections. Each change repeats the context and constraints it needs; reading the original chat is unnecessary.

## Product parity rule (2026-10-23 amendment)

The user explicitly requires one Bell Tower product with feature parity between Claude Code and Codex by default. This supersedes the earlier report constraint to keep Codex features unchanged. The reports remain evidence of Claude failures; they do not limit the scope of fixes to that provider.

- Implement one feature model, identity/work resolver, lifecycle and attention policy, configuration meaning, UI/control flow and persistence contract.
- Provider adapters translate native events, history, title ownership and supported presentation mechanisms. A different native API is not a reason for different product behavior.
- A feature added or repaired for one provider must include the other in the same release by default.
- Preserve existing user data, explicit overrides, safe authenticated runtime ownership and active sessions. Compatibility means extending safely, not freezing one integration.
- A hard exception must identify exact feature and operation, provider version/mode, primary contract or reproducible probe, unavailable capability, shared fallback and regression test.
- Missing implementation, missing fixtures, unavailable developer credentials or historical divergence are unfinished work, not hard restrictions.
- Test both clients and mixed simultaneous sessions. The final release gate rejects unexplained parity gaps.

The existing change IDs containing claude are retained for continuity with their field-report origin. They now plan both integrations. Do not create parallel provider product pipelines or duplicate the policy into two sets of controls.

### Capability and acceptance matrix

Each implementation session updates evidence for its rows. Supported, proven-restricted and unfinished/unverified must remain distinct.

| Feature | Default scope | Native mechanism / evidence work | Permitted exception boundary |
|---|---|---|---|
| Fast capture and ordering | Both clients | One ingress/protocol; verify hook schemas and async support for each version | Proven transport restriction changes the adapter, while durable/latency goals remain |
| Identity/work/inspection rules | Both clients | Normalize edits/patches/skills into one resolver | No provider-wide exception from missing adapter work |
| First-install/resume history | Both clients | Claude verified JSONL; Codex supported history API/read-only adapter | Only documented/probed history access restrictions for exact mode/version |
| Explicit native title read/write | Both where supported | Verify both APIs and ownership; distinguish user/generated/managed names | Exact unsupported native operation, with shared Bell Tower title/control fallback |
| Work checkout and color/reset controls | Both clients | Shared model, Git resolver and controls | Only native surface rendering may differ under proof |
| Native footer styling | Shared identity with supported native rendering | Existing Codex 0.163.0 trial records native footer fields/styling limits in docs/compatibility.md; revalidate candidate version | Styling limitation affects that surface only, never shared identity or controls |
| Human-wait confirmation | Both clients | Claude prompt notification; Codex verified runtime status; common state/grace | Prove signal/ownership restrictions by mode; never claim elapsed time confirms a human |
| Process/terminal binding | Both clients | Shared identity checks and provider launch evidence | No verified safe target means unavailable navigation with honest fallback, not guessed ownership |
| Speech/reminders/updates | Both clients | Shared product services and configuration | Host-level restriction affects both consistently and needs evidence |

The release ledger must include version/mode results per provider, not a single Claude result plus a generic Codex regression run. Unknown capability blocks the affected release acceptance until implemented or a hard restriction is proven.

## Start from a fresh context

1. Read this handoff and the target change's proposal, design, delta specs and tasks.
2. Run git status --short and inspect the actual implementation/evidence of required predecessors. Preserve unrelated user changes.
3. Run openspec status --change <change-id> --json and openspec instructions apply --change <change-id> --json. Read the files identified by the CLI, rather than assuming planning completeness means code exists.
4. Use the apply skill when implementation is requested: $openspec-apply-change <change-id> in Codex, or /openspec-apply-change <change-id> in other agents. Limit the session to that change and its stated prerequisite reconciliation.
5. Recheck named source boundaries and use current documentation through the repository's Context7 CLI workflow for library/platform-specific implementation. Run Context7 outside the default sandbox as required by the user's AGENTS instructions.
6. Implement and verify the listed tasks. Record actual commands, results, fixture provenance and remaining targeted live evidence. Do not substitute fabricated hook captures or a successful mock for user acceptance.
7. Leave a brief completion note identifying delivered shared interfaces, compatible data/protocol versions, evidence paths and any required successor adjustment. Revalidate the affected specs before handing off.

Do not automatically start a later change, archive unrelated work, update the installed app or publish a release merely because a predecessor finished.

## Execution order

Use the following serial order. Some changes have fewer hard dependencies, but this order avoids overlapping edits to Core, configuration, audio and setup while establishing the contracts used later.

| Order | Change | Hard implementation predecessors | Completion contract for the next session |
|---|---|---|---|
| 1 | [speed-up-claude-hook-ingestion](../../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/proposal.md) | None | Shared fast immutable ingress for both clients, versioned minimal envelopes, durable consumer, causal receipts, direct provider-aware setup with async where supported and migration/diagnostics |
| 2 | [reconcile-session-liveness-and-title-refresh](../../openspec/changes/reconcile-session-liveness-and-title-refresh/proposal.md) | 1 | Durable shell ownership, shared managed-provider attachment health, shared checkout cache and change-driven isolated title writer |
| 3 | [restore-claude-badge-identity](../../openspec/changes/restore-claude-badge-identity/proposal.md) | 1, 2 | Shared resolver with provider history adapters, working checkout, shared presentation, override/color/reset/import controls |
| 4 | [fix-vscode-window-activation](../../openspec/changes/fix-vscode-window-activation/proposal.md) | 2 | Instance-verified editor activation/reveal receipts and recoverable temp-file writes |
| 5 | [confirm-claude-human-permission-waits](../../openspec/changes/confirm-claude-human-permission-waits/proposal.md) | 1, 2 | Shared provisional/confirmed approval policy with provider signal adapters |
| 6 | [suppress-voice-during-microphone-use](../../openspec/changes/archive/2026-10-28-suppress-voice-during-microphone-use/proposal.md) | None | Playback-time active/inactive/unknown microphone policy reusable by reminders |
| 7 | [add-idle-attention-reminders](../../openspec/changes/archive/2026-10-29-add-idle-attention-reminders/proposal.md) | 2, 5, 6 | Persistent idle-gated reminder scheduler and compatible per-episode receipts |
| 8 | [verify-signed-releases-and-update-policy](../../openspec/changes/verify-signed-releases-and-update-policy/proposal.md) | 1 | Signed descriptor/asset contract, embedded trust, protected signing handoff and exposed update-check policy |
| 9 | [validate-next-release-field-acceptance](../../openspec/changes/validate-next-release-field-acceptance/proposal.md) | All 1-8 | Combined installed candidate, report coverage ledger, actual-baseline upgrade/rollback and targeted field evidence |

The order is an implementation order. Package and ship one final release after the integrated gate.

## Existing active changes and spec composition

Two older changes were already present:

- follow-openspec-session-titles: 15/17 implementation tasks recorded complete. Most Codex native-name/runtime/footer work shipped; tasks 1.1 (authenticated hook schemas) and 5.3 (full Claude/host trial) remain unverified. Its prior automatic-selection policy considers read-only status/instructions change selection; that policy is superseded for both clients by the confirmed shared rule below.
- publish-github-with-session-safe-updates: 27/27 tasks complete and release 0.1.8 published. It introduced application-distribution, versioned payloads, safe update/rollback and current update policy. Its durable specs may still await synchronization.

Do not reimplement their shipped work or silently mark old evidence gaps passed. Before composing/syncing affected new specs, reconcile the older deltas with the shipped source and recorded limitations. Sync older completed behavior first, then newer deltas in this handoff order. Preserve safe native transports/data while replacing divergent naming policies with the shared policy for both clients. Never archive an older identity delta after a new one if doing so would restore the superseded rule. Archiving is bookkeeping after deliberate reconciliation and appropriate authorization, not an automatic way to turn unchecked acceptance into success.

This planning session creates new artifacts and handoff documents only; it does not edit, sync or archive those older changes.

## Field diagnosis and acceptance target

The work install runs Windows 11 via RDP, Windows PowerShell 5.1, VS Code 1.141 and native Claude Code 2.1.30x, with OpenSpec 1.13.2 and a custom main agent configured as pm. Codex is not installed there.

Four or five conversations start from the main checkout C:/Code/D360 using Start-ClaudeCodePM. Ticket work may belong to C:/Code/D360-worktrees/DEP-XXXX. All showed D360 and the main checkout's branch after installation/restart. The correct VS Code terminal was selected on chip click, while the editor stayed behind another application or minimized.

The strongest explanation is a combination:
- Synchronous wrapper/launcher startup exceeded the three-second hook timeout, losing most eligible tool events.
- Successful change association required a pre-tool receipt, and the parser rejected most real command forms.
- Existing conversations had no Bell Tower identity/history recovery on first installation.
- The old mod had richer work-touch/ticket/branch resolution.
- Reading Git at the launch directory cannot identify a distinct ticket worktree without a separate association.

These are code-supported defects plus user-reported field measurements. No new performance/production-provider reproduction was performed during planning.

## Confirmed shared naming policy

The user explicitly agreed: **inspecting another change must preserve the established work association**. In particular, reads/searches and read-only status/instructions for B must not rename a conversation working on A. For both clients, work comes from successful verified main-session work evidence or explicit user association, not arbitrary repository-wide state.

Shared display precedence for Claude and Codex:
1. Explicit Bell Tower full-title override.
2. Verified explicit provider-native title, such as /rename, distinguished from generated or Bell Tower-managed names.
3. Optional role prefix plus automatic subject.

Automatic subject precedence:
1. Explicit pinned change.
2. Current successful work association.
3. Unique branch/ticket-to-change match.
4. Branch ticket.
5. Branch.
6. Directory fallback; Claude only when no useful fallback exists.

A full title is not automatically prefixed again. Adapters must distinguish Claude custom-title records and Codex explicit native name changes from generated or last-managed values. Verify actual record fields with a redacted fixture. Reset clears Bell Tower title/change/color pins and re-resolves automatic state; it does not rewrite Claude transcripts or erase an explicit native rename behind the user's back.

Work evidence includes successful Write/Edit/NotebookEdit under openspec/changes/<id>/, excluding archive, and classified successful workflow skills. A read/explain-only skill can never replace established work. Support reported shell shapes through an explicit bounded grammar and success/root evidence; folder existence alone is not proof of a successful command. Never execute captured transcript commands.

Branch changes invalidate automatic touched/branch evidence tied to that branch. Explicit pins, native explicit titles, session colors and attention receipts remain intact. Multiple matches are ambiguity, not permission to pick the first directory.

Both clients use the shared precedence and inspection rules. Existing native-name ownership protects user edits and safe transport; it does not freeze a divergent Codex resolver.

## Checkout, process and planning identities

Keep these distinct:
- Conversation: provider plus native session ID.
- Attachment: current agent/shell process identities, including creation ticks.
- Launch directory: where the native agent started.
- Working checkout: user-attached or uniquely verified checkout whose branch/worktree should be displayed.
- Planning root/store: where OpenSpec artifacts live; it may be external to the checkout.

Navigation always uses the verified attachment, never branch/name/ticket. An agent launched in the main checkout can display the associated DEP-7070 working checkout without moving its process or opening another terminal. Ambiguous worktree mapping needs explicit attach/clear controls and honest status.

## Shared implementation contracts

### Ingress

A small NativeAOT ingress bypasses the heavy desktop launcher. Provision it as verified immutable owned bytes; do not point at a deletable payload without retention or overwrite a referenced helper. The design uses a content-addressed copy under the selected data root, packaged/signed through the release inventory.

Capture protocol fields include protocol/envelope ID, provider/session/hook, ingress time, available provider/source order, hook-shell creation evidence, process creation identities, attachment/approved launch metadata, transcript location and minimal derived tool/identity evidence. Do not retain full private command/response/transcript bodies in the spool or logs.

Keep the old normalized inbox usable for compatible old helpers of either provider. Declare consumer compatibility before enabling a new protocol or claiming rollback. Persist before returning {}, and commit state before deleting receipts.

Early timestamps are useful but async process scheduling may still reorder starts. Completion/turn/end causal receipts, not processing time alone, prevent resurrection. A successful post result can stand alone.

### Lifecycle and title writes

Retain durable launch-shell identity separately from the palette record, which is currently consumed/deleted. Recapture repairs incomplete managed ownership for both clients. Intentionally unowned/shared modes are classified consistently with evidenced capability limits and are not mislabeled failed managed captures.

Default stale-unverified grace is ten minutes, configurable from one minute to 24 hours. A stale unknown managed attachment from either provider becomes unavailable, not proven dead; known-live idle agents never expire solely for inactivity. Preserve grace origin/status across restart.

Compute/cache desired identity and Git context in the tray. Unchanged refresh ticks launch no title writers or Git commands. Console attachment stays in an isolated bounded helper because it is process-wide. Back off failures, rate-limit logs, preserve detach/title-lock ordering and clean up only owned writers on timeout. Canceling a wait or disposing a Process is not itself proof the child stopped.

### Presentation

Use one derived record for title, role/subject, palette, checkout details and provenance. Badge/chip share its core identity; terminal titles can append Git details. Speech normalizes hyphens/underscores, uses at most four subject words and avoids duplicate roles.

Persist palette/pins across resume, expose named/hex/random/reset controls and avoid live color collisions when possible. No dependency on the legacy mod or its temporary badge files. Explicit import must accept supported OSC rgb as well as hex background formats and reject arbitrary escape sequences.

### Attention and audio

PermissionRequest is provisional for Claude; permission_prompt confirms human-wait eligibility after Claude's own roughly six-second inactivity gate. Add no further fixed Claude debounce. Notifications may lack operation IDs; delayed/unrelated evidence must not resurrect completed waits. Both clients use shared provisional/confirmed/resolved/unverified semantics and a common human-wait grace that accounts for a verified provider notification delay. Timing-only fallback is not human confirmation; unavailable signals require precise capability evidence and honest status.

Microphone-aware voice defaults enabled. Active or unknown microphone state yields tone-only playback; explicit opt-out restores normal Voice behavior. Recheck at playback and do not replay suppressed speech after calls. Never open or record a microphone in tests.

### Local reminders

Proposed defaults, recorded explicitly for implementation:
- Reminders enabled.
- Initial reminder after five minutes; user idle threshold five minutes.
- Repeat every ten minutes, maximum three per episode.
- Unknown/disconnected idle observation is ineligible for automatic reminders.

Recent input postpones reminders until idle again. Viewed is distinct from acknowledged: still-unanswered viewed waits may remind after inactivity; acknowledged waits do not. Preserve wait start/episode and avoid restart catch-up bursts.

Persist reminder ordinal/deadline/receipt separately from the normal one-announcement set. Retain already implemented candidate metadata and its stable opaque receipt IDs for compatibility. There is no planned remote sender or endpoint setup; acknowledged, resolved or unavailable episodes cancel or suspend local reminders according to the implemented policy.

### Signed releases and policy

Use a maintained minisign-compatible verifier and detached signature over the exact public release descriptor bytes. The descriptor authenticates version/runtime/contracts/archive identity and SHA-256, with helper/VSIX inventory covered by the signed package. Embedded key IDs establish trust; an unsigned feed cannot authorize its own new key.

Protected signing signs already-validated bytes. PR validation uses disposable keys and cannot access publisher secrets. Document authenticated rotation/revocation and a public fingerprint.

0.1.8 cannot retroactively authenticate the verifier it downloads. Provide a manual independently verified first-signed-release path and report the old updater's actual hash-only checks honestly. Later signature-capable updates reject unsigned/tampered/unknown-key offers.

Automatic application installation is already absent. Expose existing automatic discovery policy through setup/user controls (provisional --update-checks enabled|disabled), preserve omitted values, and retain explicit check/install actions under signature verification.

## Constraints for every implementing session

- Tests use isolated --data-dir, homes, settings, transcripts and disposable checkouts/hosts.
- Never modify the user's production data directory, Claude/VS Code settings or live transcripts for validation.
- Never stop/restart agents or terminal hosts, type into a production session or interfere with real approvals.
- No physical tones/speech or microphone capture in automated tests. Use fake/recording/silent sinks.
- No access to real publisher secrets in tests.
- Deliver every feature for both clients through shared policies and thin adapters. Preserve safe runtime/footer ownership and user data while extending capabilities; restrictions require version/mode proof, supported fallback and matched/mixed-session acceptance.
- Preserve unknown JSON fields and declare compatibility for old/new writers. No untested rollback/data downgrade claims.
- Ordinary logs contain bounded sanitized codes/categories, not raw exceptions, commands, transcripts, endpoints or credentials.
- Keep Core free of provider JSON, disk, UI and native API calls; follow docs/development.md small-helper boundaries.
- Fetch current primary documentation with Context7 before implementing library/platform-specific behavior. Report quota/network limitations rather than silently relying on memory.
- Setup previews changes and preserves ownership/backups, unrelated settings and user edits. The user's edited async hooks need an explicit migration, not silent replacement.
- Each task carries a verification criterion. Mark it done only with actual evidence.

## Verification commands and evidence

Use repository docs/current CLI help for exact options added during implementation. Existing baseline checks:

    ./scripts/format.ps1 -Verify
    dotnet build -c Release --no-restore
    dotnet test -c Release --no-build --no-restore
    node --test vscode/service.test.js vscode/focus.test.js
    openspec validate <change-id> --strict
    ./scripts/package.ps1
    ./scripts/update-acceptance.ps1

Restore locked dependencies as needed using existing development instructions. Do not weaken normal auditing or claim desktop/process fixtures passed when sandbox access prevented them. The final change adds the installed field harness and evidence ledger.

The hook timing gate measures each provider with at least 100 samples through the actual packaged provider shell/command: median below 300 ms, median/p95/p99/max recorded, no lost accepted envelopes, and ordinary durable capture within three seconds. Async ignoring timeout is not a performance result.

Use actual historical 0.1.8 bytes for baseline update/rollback, not a rebuilt current application with an old fixture version. Every candidate feature must be tested together through its installed layout, helper provisioning, VSIX and signed metadata.

## Field finding ownership ledger

| Report item | Implementing owner(s) | Required distinguishing check |
|---|---|---|
| 1 Hook startup/timeout | 1 | Full installed shell chain latency and durable counts; no PowerShell Claude event wrapper |
| 2 Ordering/lost pre | 1, 3, 5 | Shuffled/duplicate/post-only receipts converge without resurrecting waits |
| 3 Title CPU | 2 | Zero unchanged-tick child/Git work, bounded failed-target soak |
| 4 Dead/null-process sessions | 2 | Repair then exit/unavailable reconciliation survives restart |
| 5 Bridge temp leak | 4 | Failing rename leaves zero temp files and later tick recovers |
| 6 Diagnostics | 1, 2 | Safe exception category, timeout summary and ownership health |
| 7 Claude custom title | 3 | Verified custom-title fixture, full-title override precedence |
| 8 Change command/workflow forms | 3 | Every reported shape, successful work evidence and no subagent/inspection rename |
| 9 Ticket worktree branch | 3 | Main launch with distinct working checkout and safe ambiguity |
| 10 Real human permission signal | 5 | Long auto review stays silent; current human prompt alerts |
| 11 Voice during microphone use | 6 | Playback race, active/unknown tone-only and no speech catch-up |
| 12 Local reminders | 7 | Idle gates/caps/dismissal/restart receipts |
| 12 Remote notification portion | Cancelled by user 2026-10-29 | Removed from implementation and acceptance scope; local reminder coverage remains |
| 13 Signature and managed update policy | 8 | Wrong-key/tamper/unsigned rejection and disabled automatic checks |
| Original VS Code foreground defect | 4 | Correct owner foreground when behind/minimized, duplicate-window safety |
| Legacy badge naming/color/voice parity | 3 | Resume/work-touch/branch fallback, controls, shared identity, normalized speech |
| Integrated installed release | 9 | All owners above implemented and evidence ledger complete |

## External prerequisites and honest completion

These do not prevent artifact creation or independent fixture work, but may be required to accept/publish the final release:
- Small real redacted Claude hook/transcript schemas, especially custom main-agent agent_id and custom-title/timeout fields. Do not invent field names.
- Targeted Windows 11/RDP live foreground and resumed-worktree identity checks after an updated owned VSIX is activated. Existing reports remain valid failures until specifically fixed.
- Representative loaded-machine hook timing evidence.
- A publisher-controlled signing key and protected CI signing configuration, with reviewed public fingerprint.

Finish every independently testable task. Record any missing live evidence or external provisioning precisely; never replace required release features with deferred follow-ups or mark them accepted solely from synthetic fixtures.

## Completion and publication

The last change produces the integrated ledger, candidate package, release notes, trust instructions and a concrete publication handoff. Planning or implementation does not itself authorize sending production messages, importing a production private key or publishing GitHub assets. Complete the reviewable candidate and checks first, then perform publication only with the necessary explicit operational authorization.

After all accepted work is complete, reconcile/sync/archive in the deliberate order above and leave durable specs that describe the shipped combined behavior. Preserve outstanding historical evidence as historical, not fabricated passes.
