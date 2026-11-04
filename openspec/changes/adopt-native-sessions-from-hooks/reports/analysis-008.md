<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-008 / code-analyzer; Client: Claude Code delegated subagent; Model: claude-opus-5-5; Refusal: the client refused the reviewer's write of this path (as reported by the reviewer); the reviewer did not retry. -->

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-008 (recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (Claude Agent SDK subagent; client version not exposed) / delegated non-interactive subagent / `claude-opus-5-5`
- Date: 2026-11-03 20:43 UTC
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-008 (recheck of ANALYSIS-007)
- Task and requirement references: [tasks.md](../tasks.md) 2.1 and 2.2; [design.md](../design.md) decisions 1-5 and Risks; [implementation 020](../evidence/implementation-020.md)
- Scope and relevant neighboring paths: the 23-file target, mainly `src/BellTower/Core/{NativeAdoption,AttachmentEvidence,HostTarget}.cs`, `src/BellTower/Providers/{AgentModeClassifier,CaptureConsumer,HookIngestor}.cs`, `src/BellTower/Hosts/AttachmentCapture.cs`, `src/BellTower/IO/AttachmentStore.cs`, `src/BellTower/Sessions/AgentLauncher.cs`, the csproj files, `tools/BellTower.{Ingress.Bench,Smoke}/Program.cs` and the tests. Neighbors read: `IO/StateStore.cs:160-180` (attachment re-save), `docs/hook-ingress.md:44,50` (ingress images and compatible rollback).
- Baseline commit: `2f1de7f`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2f1de7f -Paths src tests tools .../implementation-019.md .../implementation-020.md`
  - HEAD `2f1de7fc45c54c27a7bc236b9daac1bbc003b665`
  - Tracked diff SHA-256 `54389c90fe0392adc3e4e68a7b884ea43890d83b36ea88185fa3609034c1e248`
  - 8 untracked files: implementation-019 c334bdb4..., implementation-020 e7a969df..., `AttachmentEvidence.cs` 2d2ee0a6..., `NativeAdoption.cs` 084de61d..., `AgentModeClassifier.cs` a17acde8..., `AttachmentEvidenceTests.cs` 1714e2a7..., `NativeAdoptionReconcileTests.cs` c069d08c..., `NativeAdoptionTests.cs` 90462fdc...
  - Summary: `head=2f1de7f baseline=2f1de7f files=23 diff=54389c90fe03 untracked=8/aba7032643a6`
- Before/after identity: identical after the recheck. No target mutation.
- Prior reports / rechecked findings: [analysis 007](analysis-007.md) ANALYSIS-600, 601, 602, 603, 604 and 606. ANALYSIS-605 was deferred to 6.3 and not rechecked.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Finding recheck | Read the corrected sources and the new and extended tests | See Re-verification | 5 verified, 1 partial. |
| Duplication regression | Compared `Apply`, `Shared`, `Reconcile` and the callers | The `{provider}-{pid}-{ticks}` id now comes only from `NativeAdoption.OwnId`. Both callers use one shared `Reconcile`. The redundant ownership override was removed from `HookIngestor`. | No regression. |
| Layering regression | Checked the csproj links and namespaces | The vocabulary lives in `Providers/AgentModeClassifier.cs`. `NativeAdoption` and the classifier are linked into Ingress, not Protocol. Hosts now depends on Providers (capture calls the adapter), which fits `docs/development.md`. `NativeAdoption.cs` still imports `BellTower.IO`. | Partial 604; no new defect. |
| Architecture vs 2.3/3.x/4.2 | Checked generation stamping on every writer that changes `Attachment.Process` (`Adopt`, `AgentLauncher`; `StateStore` re-saves evaluated records without changing Process). Checked the reserved `TerminalEvidence` fields and how `Merge` and `Of` handle the stamp. | Every writer re-stamps evidence. The 4.2 fields are additive and unwritten. Shared-runtime records always carry a null generation, so 4.2 must still re-validate at the action boundary, as already planned. | No regression. |
| Compatibility of new semantics | Traced a provisional capture through a pre-change capture-v1 consumer | Provisional captures are sent as `Dedicated=true`; an older consumer ignores `evidence.provisional` | ANALYSIS-700. |
| Performance regression | Read the hot paths | `Adopt` calls `Reconcile` on every hook. Its liveness probe runs only on the managed-mismatch or provisional path. The classifier adds a few small list allocations. Ingress work is unchanged apart from classification. Both benchmark legs (baseline and hook) now pass through the fixture `cmd.exe` wrapper, so the adjusted mean stays unbiased, but raw medians include one extra process start. | No defect. Raw-median comparisons with earlier records are noted under Assessment. |
| Build / test / format | Not run | Building was optional, and `global.json` had again been modified by another session | Not run; QA owns functional checks. |

## Findings

### ANALYSIS-700: Provisional captures are dedicated on the wire, so an older capture-v1 consumer adopts unverified Codex captures as direct

- Severity: warning
- Related task / requirement: 2.2 (QA-1307 fix), 2.4; design decision 1 and the risk "gate incompatible consumers using the existing mechanism before persisting new semantics"
- Locations:
  - `src/BellTower/Core/NativeAdoption.cs:35-40` (provisional capture returned as `Dedicated=true` with the process, the launch host and `ManagedTitle`/`ManagedBackground` intact)
  - `src/BellTower/Core/AttachmentEvidence.cs:71`
  - implementation-020, Compatibility section ("The provisional record exists only inside a capture envelope and is resolved before anything is persisted")
  - `docs/hook-ingress.md:44,50` (immutable ingress images stay callable for cached hooks; compatible rollback is any capture-v1 consumer)
- Observation:
  - A Codex hook with an unknown mode, a managed identifier and an agent process now leaves the ingress dedicated, with only `evidence.provisional = true` marking it as unverified.
  - Only the new consumer resolves that flag, through `Reconcile`. A pre-change capture-v1 tray is a supported rollback target, and it may receive captures from the newer cached ingress image. That tray keeps `evidence` as extension data and adopts the attachment as a direct process attachment with the captured host and presentation consent.
  - One case that reaches this path is the ANALYSIS-500 shape: the shared backend's command line cannot be read (for example, an elevated backend) and its inherited launcher environment supplies `BELLTOWER_ATTACHMENT`. The old tray then binds the backend's conversations to the launcher's shell, provided no managed record already exists. If one does, its existing late-hook rule keeps the record unchanged but still attributes the conversation to that attachment.
  - Under DEV-019 this capture failed closed to the shared runtime for every consumer. DEV-020 therefore reopens a fail-open path for older consumers.
  - The "no gate change" statement rests on the inaccurate claim quoted above.
- Proposed improvement and benefit, either of:
  - Encode provisional captures fail closed on the wire: `Dedicated=false` and the shared-runtime shape. Carry the direct candidate (process, host and presentation flags) inside evidence, and let the new consumer promote it only after `Reconcile` verifies the launch record. Older consumers then see a shared-runtime conversation.
  - Record the semantic change and gate it under task 2.4 before release.

  Either way, correct the compatibility statement in the evidence.
- Behavior-preservation risks: a verified managed launch under the new consumer must keep its host, presentation and palette, as `ALegitimateManagedLaunchStillResolvesItsPresentationAndIdentity` covers. Claude never becomes provisional.
- Evidence type: source observation. A mixed-version run was not performed.
- Verification / measurement plan: extend the "older reader ignores it" envelope test (`AttachmentEvidenceTests`) with a provisional Codex capture decoded by a pre-change consumer model, and assert it is adopted with no process, no host and no presentation. Keep the paired Claude row unchanged.
- Relationship: regression introduced by the QA-1307 fix; same exposure class as ANALYSIS-500 and ANALYSIS-600.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-600 | `diff=54389c90fe03 untracked=8/aba7032643a6` | `NativeAdoption.Reconcile` (Core) runs under the attachment lock in `AttachmentStore.Adopt`. On re-identification it clears `ManagedTitle`/`ManagedBackground`. It keeps the host only when `HostTarget.ShellSource == "ancestry"`; `AttachmentCapture` marks a shell named only by `BELLTOWER_SHELL_PID` as `launch-env`. `CaptureConsumer` and `HookIngestor` suppress the inherited name, change and palette when the adopted id differs. `NativeAdoptionReconcileTests` covers both providers across interactive, embedded and headless modes and both shell sources, including a null `TerminalPresentationResolver.Resolve` and a legitimate-launch control. | Verified |
| ANALYSIS-601 | same | The Codex vocabulary detects `app-server` anywhere before `--`. It maps a non-session subcommand table to `unknown`, makes any option outside the known flag and value-option lists `unknown`, recognizes `--name=value`, and leaves `embedded-runtime` only for the TUI shapes. The remaining fail-open edge is a future subcommand read as a prompt; implementation 020 documents it. | Verified |
| ANALYSIS-602 | same | `AttachmentEvidence.LifecycleFor` is the single derivation, used by `Apply`, `Of` and `Reconcile`. `Merge` learns an unknown owner with the same rule as `Mode`. | Verified |
| ANALYSIS-603 | same | Evidence carries a `Revision` and a `Generation`. `Of` treats a mismatched stamp as absent. `Adopt` and `AgentLauncher` re-stamp, and no other writer changes `Process`. `TerminalEvidence` reserves `Client`, `Shell`, `OrderingKey` and `ObservedAt`. The old-tray round-trip test is reported. | Verified |
| ANALYSIS-604 | same | The vocabulary and `MayShareBackend` moved to Providers. `Apply` takes the may-share fact as a parameter. The policy is linked into Ingress rather than Protocol. `NativeAdoption.cs:1` still imports `BellTower.IO` for `ContentHash`, which was part of the original observation. | Partially verified (Core-to-IO residual) |
| ANALYSIS-606 | same | `AdoptionOutcome.Mode` was removed (`NativeAdoption.Outcome`), the `HookIngestor` override was removed and `OwnId` is the single id builder. | Verified |
| ANALYSIS-605 | not assigned | Deferred to task 6.3 | Not performed |

## Assessment and limitations

- All of ANALYSIS-600 to 606 are addressed except 605, which was deferred, and the Core-to-IO import left in 604. Duplication and layering did not regress, and the evidence model now fits 2.3, 3.x and 4.2 without rework.
- The single new defect, ANALYSIS-700, concerns how a new consumer-side semantic (`provisional`) is put on the wire. It needs either a fail-closed encoding or a 2.4 gate decision.
- Benchmark note, not a defect: both legs now include the fixture `cmd.exe` wrapper. The adjusted-mean comparison remains valid, but raw medians are not directly comparable with the historical records in `docs/hook-ingress.md`. Neither tool was run.
- Not performed: build, test, format, the tools, a mixed-version trial and any provider run. My only write attempt was this report, which the client refused, so the text is inline.
