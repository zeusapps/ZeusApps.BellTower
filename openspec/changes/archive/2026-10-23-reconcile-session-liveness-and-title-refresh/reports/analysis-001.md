# Analysis report: reconcile-session-liveness-and-title-refresh / 001

## Review target

- Reviewer / role: `liveness_analysis` / code-analyzer, delegated through the current Codex session; internal service version/mode unavailable. The orchestrator observed Codex CLI 0.164.0 and .NET SDK 10.0.113.
- Date: 2026-10-23; final identity check at 17:24:20 UTC.
- Change root: `D:/Code/ZeusApps.BellTower/openspec/changes/reconcile-session-liveness-and-title-refresh`.
- Assignment: initial independent analysis; all tasks 1.1–5.2 and both delta specifications.
- Scope: all 58 explicit paths in [implementation-paths.txt](../evidence/implementation-paths.txt), including new untracked implementation, test, documentation and evidence files. Relevant unchanged neighbors inspected: `AttachedConsoleTitleWriter`, `AttachedSessionConsole`, `AttachmentLifecycle`, `WindowsTerminalNavigator`, `VsCodeNavigator`, `DataLock`, `AtomicFile` and `SessionViewFactory`.
- Baseline commit: `0a7445a506e5306cca515c251e1fadad38fd99de`. Supplementary workspace HEAD: `64d3e94626b636ca38a3ab0c5b3789686c8fa15b`; unrelated workflow changes in that commit are outside this implementation scope.
- Implementation identity: [review-target-001.json](review-target-001.json). Independently reproduced tracked diff SHA-256 `64a57144336c9da8569bd745aed61f715e472e7c423316b7cbc14ece2a4d77a7` using `git diff --binary <baseline> -- <explicit paths>`, stdout lines joined with LF plus a final LF for nonempty output, UTF-8 without BOM. Independently checked every recorded SHA-256, including untracked files.
- Before/after identity: same diff hash and all 58 file hashes; no implementation mutations. Current tracked implementation changes under `docs`, `scripts`, `src` and `tests` have no omitted paths in the supplied manifest. Pre-existing untracked handoffs/planning and orchestrator reports are excluded deliberately.
- Prior reports / rechecked findings: none.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Target integrity | Scoped Git diff hash and `Get-FileHash -Algorithm SHA256` against the frozen JSON before/after review; `git status --short` and scoped `git diff --name-only HEAD` | Exact identity above; zero mismatches and omitted tracked implementation paths | Passed |
| Shared lifecycle policy | Source inspection of `AttachmentHealth`, `AttachmentStore`, `ProcessProbe`, ingestion adapters, reconciliation and causal replay | One shared health policy; access-denied remains unverified; live shell alone does not verify agent; unavailable retains waits; health and epoch persist | Coherent shared boundary, with replacement-generation defect ANALYSIS-002 |
| Presentation architecture | Source inspection of tray, resolver, scheduler, desired request, command and writer | Shared cache serves overview/title/native-name pumps; console attachment remains in helper; normal desired-value path reads ownership but does not reload session/identity state or Git | Appropriate responsibility boundaries; receipt defect ANALYSIS-001 |
| Bounded work / failure policy | Source inspection of checkout semaphore, 256-entry cache eviction, pump gate/destination pruning, backoff schedule, owned-child runner and detach lock | Serialized discovery; cached negative results; one running title refresh; backoff 2/5/15/30/60; helper killed/joined and unreaped child blocks another launch | Source contract coherent; no independent performance measurement claimed |
| Persistence / compatibility | Source inspection of extension-data records, snapshot health requirement, release/probe capability agreement, launcher and stage/apply/rollback preflight | Atomic state health requirement; old helpers remain readers/publishers; guarded consumer transitions reject missing health capability | No new concern found; historical executable and installed-package acceptance remains explicitly unverified |
| Controls / parity | Source inspection of common configuration, doctor, session summary, overview, navigator and matched/mixed tests | One shared bounded grace setting and health/control meaning; provider differences remain in existing adapters | No provider-specific exception claimed |
| Concrete edge cases | Source traces cross-checked against QA's executed [probe output](qa-001-partial-write.txt) and retained [probe](qa-001-partial-write.ps1) | A/B/A partial write and same-ID two-owner resume reproduce the two findings below | Executed fixtures are QA evidence; this analyzer did not run those fixtures itself |
| Broad verification | Read developer evidence and test sources; coordinated with QA | Developer reports 361 .NET and eight Node passes, format/build/strict validation; QA owns independent functional execution | Not repeated by analyzer to avoid build contention; no independent full-suite claim |

## Findings

### ANALYSIS-001: A failed partial title write leaves an obsolete successful receipt

- Severity: warning.
- Related task / requirement: 3.2 and 4.2; change-driven terminal presentation, including restoration to a previous desired title.
- Locations: `src/BellTower/Hosts/TerminalTitlePump.cs:83`, `:90`, `:99`; relevant existing writer `src/BellTower/Hosts/AttachedConsoleTitleWriter.cs:15`.
- Observation: `Successful` remains A when an attempted B write returns false or throws. The concrete native writer sets the title first and then applies the optional background. A failed background write therefore can return false after title B was already applied. A timeout after mutation also has an uncertain result. If the desired title returns to A, the pump's `Successful == desired` check suppresses restoration, leaving B indefinitely until another desired change or pump restart.
- Reproduction / evidence: initial successful A; change to B and let the writer mutate the title but report false; revert to A with a succeeding writer. QA's retained fixture reports `requests=A,B; desired=A; actual=B; expectedRequests=A,B,A; defect=True`. This is a recording writer modeling the actual native method's partial-write ordering, not a production terminal observation.
- Proposed improvement and benefit: invalidate the prior successful receipt before an attempted write whose result can be partial/unknown, retaining normal backoff on failure. A successful receipt should describe the most recent confirmed terminal state. Add matched provider regression coverage for success A → partial/unknown B → desired A, verifying a restoration attempt and final deduplication after success.
- Behavior-preservation risks: preserve unchanged-success suppression, per-destination retry timing, cancellation/cleanup and independent session state. Do not assume failure guarantees zero mutation.
- Evidence type: source observation with independently executed QA fixture. This is a correctness defect, not a performance hypothesis.
- Relationship: canonical finding; QA references this ID without allocating a duplicate.

### ANALYSIS-002: Explicit attachment replacement mixes new process ownership with old host destination

- Severity: warning.
- Related task / requirement: 2.3–2.4; fresh verified resume, safe navigation and rejoining the correct attachment.
- Locations: `src/BellTower/IO/AttachmentStore.cs:40`–`:49`; downstream destination consumer `src/BellTower/Hosts/WindowsTerminalNavigator.cs:20`.
- Observation: the explicit same-ID resume replacement branch replaces the agent, shell, epoch and end state, but retains nonnull old window process, nonzero window handle, terminal session and an established known host kind. These fields now describe two ownership generations. A still-live former Windows Terminal destination can remain actionable, or a stale destination can make a valid new attachment unavailable. The ordinary fresh managed launch path generates a new GUID; the defect concerns the supported explicit replacement branch, not an asserted change to that launch flow.
- Reproduction / evidence: save an ended same-ID attachment with one valid live process identity and Windows Terminal destination metadata; explicitly resume using a different valid live process identity and captured VS Code destination. QA's isolated current-process/owned-hidden-helper fixture reports `newAgent=True; newShell=True; newWindow=False; kind=windows-terminal; handle=111; terminal=old-terminal` instead of the captured VS Code/222/new-terminal metadata. Window handles are synthetic, no focus was executed, and no authenticated provider session was involved.
- Proposed improvement and benefit: on an accepted replacement, replace or clear all destination-generation fields together from the fresh captured host, preserving unknown extension data according to the existing compatibility policy. Continue retaining durable user opt-in controls unless the agreed ownership policy explicitly changes them; this finding does not request copying new captured flags over user intent. Add matched provider tests for changed owner/host and a new host with missing destination fields, proving old values cannot leak into the new generation.
- Behavior-preservation risks: ordinary same-generation repair must continue preserving established ownership, unknown host modes and extension data; stale/conflicting hooks must remain unable to steal ownership. Keep explicit title/background opt-ins separate from destination evidence.
- Evidence type: source observation with independently executed QA fixture. Incorrect routing is established at the stored destination level; actual desktop focus was not tested.
- Relationship: canonical finding; QA references this ID without allocating a duplicate.

## Re-verification

Not performed in this initial round. Both findings require developer correction followed by a new independent report against the corrected target.

## Assessment and limitations

The implementation uses appropriate shared policy and isolated native boundaries without unnecessary new provider flows. Two concrete edge cases require correction before acceptance. No additional complexity, duplication or performance finding is justified by this review. Cache/backoff/native-process claims above distinguish source inspection from the developer's measurements and QA's fixtures.

No implementation, tests, configuration, documentation, planning, ledger or QA output was changed by this analyzer. Only this reserved report was written. No production data/settings, authenticated provider, live user terminal, physical audio, publication or historical-package migration was exercised. Optional package restoration remained blocked by the recorded NuGet auditing/network attempt; this report does not convert it into a successful package check.
