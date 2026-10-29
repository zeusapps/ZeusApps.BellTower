# Analysis report: add-idle-attention-reminders / 001

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / delegated review / `gpt-6.1-sol` (role default).
- Date: 2026-10-28, completed 18:52 UTC.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-001.
- Task and requirement references: tasks 1.1–4.2; all four requirements in [attention-reminders spec](../specs/attention-reminders/spec.md).
- Scope: all 26 files in [the complete frozen manifest and per-file hashes](../evidence/review-target-001.txt), plus relevant `SessionState`, native audio sink, JSON and persistence neighbors.
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked diff SHA-256 `dd04597165e18ddc6ea86e4502f4369032c26cdbd58ff759aadda880952c8263`; untracked digest `471dd618c9a9`; 13 untracked implementation files.
- Before/after identity: both `head=47dfca9 baseline=47dfca9 files=26 diff=dd04597165e1 untracked=13/471dd618c9a9`; full output matched the reserved target exactly before and after. No implementation mutation observed.
- Prior reports / rechecks: none; initial review.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared policy and layer boundaries | Inspected core scheduler/DTOs, IO locking/persistence, tray controls and provider projections | Source at frozen identity | Shared implementation covers both providers; no duplicated provider scheduler. Eligibility and lifecycle retention are conflated, causing ANALYSIS-001. |
| Receipt continuity and replay identity | Silent scratch executable, manual-clock same-episode omission/recovery for Claude and Codex | [Probe evidence](../evidence/analysis-001.md) | Failed required continuity: ordinal and opaque ID reset. |
| Escalation interface | Scratch persisted provisional Codex permission with transient native confirmation, then reload/config disable | [Probe evidence](../evidence/analysis-001.md) | Exported API returns a candidate without current actionable confirmation; ANALYSIS-002. |
| Win32 contracts and conservative freshness | Context7 resolve/docs, current official Microsoft Learn references, declarations and executable tick/future-age probes | [Documentation and boundary evidence](../evidence/analysis-001.md) | Structure and pointer declarations inspected without a native ABI mismatch found; time-validation failures in ANALYSIS-003. No native input or RDP trial. |
| Playback and aggregate cancellation | Inspected queue/native sink callbacks and invoked only the tray validity predicate with injected current input | [Probe evidence](../evidence/analysis-001.md) | Aggregate speech has an all-members validity guard; the tray's cached idle check still permits playback after input, ANALYSIS-004. No audio played. |
| Configuration and compatibility | Inspected validated candidate reload, JSON extension fields and dialog save mapping; executed exact save replacement mapping | [Probe evidence](../evidence/analysis-001.md) | In-place config round trips preserve extensions; dialog replacement erases them, ANALYSIS-005. |
| Complexity, boundedness and performance | Source inspection of receipt cap, single in-flight idle task and per-tick IO/locks | Source observation | Storage/task counts have explicit bounds. No performance benchmark or latency measurement performed; no optional optimization findings raised. |
| Regression/build/format | Reserved to QA; developer's passing build/format/focused assertions read as handoff only | [Developer handoff](../evidence/implementation-001.md) | Not independently rerun by analyzer; no overlapping broad suite. |
| Content hygiene and target integrity | Inspected newly written report/evidence for sensitive values; exact before/after manifest comparison | This report, evidence and identity comparison | Passed for analyzer outputs. Comprehensive change scan belongs to QA. |

## Findings

### ANALYSIS-001: Temporary ineligibility destroys same-episode reminder history

- Severity: warning.
- Related tasks: 2.2, 2.3, 2.4, 3.1; persistent bounded reminder receipts and stable escalation identity.
- Locations: `src/BellTower/Core/ReminderPolicy.cs:62`, `:68`, `:87`; `src/BellTower/IO/StateStore.cs:32` at frozen target.
- Observation: receipt pruning treats absence from the currently eligible collection as proof that the episode ended. A transient unverified attachment or unavailable runtime confirmation removes the receipt even while the persisted session/episode is unchanged. Recovery creates ordinal zero and a new random ID. The scratch probe reproduces this for both providers. Runtime-confirmed Codex permissions can also be temporarily absent immediately after restart because native flags are deliberately not persisted.
- Proposed improvement and benefit: separate durable episode retention from momentary delivery eligibility. Suspend scheduling/escalation when evidence is unavailable, retain the ordinal/deadline/stable ID, and prune only from actual lifecycle/acknowledgment/replacement evidence under a bounded policy. This preserves caps and future webhook idempotency.
- Behavior-preservation risks: keep new episodes independent; do not let retained history authorize playback during an unverified interval.
- Evidence type: source observation and executed synthetic reproductions, both providers.
- Verification: issue a reminder, transiently lose verification/current confirmation, restore the same episode, and verify unchanged ID/counter; repeat after cap and across restart. A resolved-then-new episode must still get a new identity.
- Relationship: no earlier canonical finding supplied.

### ANALYSIS-002: Escalation API exports stale eligibility without current confirmation

- Severity: warning.
- Related tasks: 3.1, 4.1; idle-gated eligibility and escalation invalidation.
- Location: `src/BellTower/IO/StateStore.cs:47`.
- Observation: `Escalations()` checks persisted episode, acknowledgment and health only. It has no current provider projection or config input and does not require current actionable attention. The probe creates a candidate from a runtime-confirmed Codex provisional wait, reloads its snapshot with no transient native evidence (`RequiresAttention=false`), and still receives the candidate. Disabling reminder config before another tray tick also leaves it exported. Thus a consumer cannot treat the documented API result as a currently verified eligible send candidate.
- Proposed improvement and benefit: validate exported candidates through the same current eligibility/ownership/configuration boundary as scheduling, including fresh native permission evidence where needed. Keep the durable receipt independent from whether the candidate is currently exposed.
- Behavior-preservation risks: Mute or local audio failure must continue to permit otherwise valid escalation eligibility; do not require successful playback or invent provider confirmation from persisted history.
- Evidence type: source observation and executed isolated persistence/API probes.
- Verification: both-provider matched candidates and a Codex transient confirmation path; withdrawal on missing/negative current confirmation, ownership loss, acknowledgment/resolution, and disable before another scheduling pass. Retain valid muted candidates.
- Relationship: related to the retention/eligibility separation in ANALYSIS-001; distinct exported-consumer defect.

### ANALYSIS-003: Future tick and future observation can be accepted as inactivity

- Severity: warning.
- Related tasks: 1.3, 2.1, 2.4; conservative current-session input observation.
- Locations: `src/BellTower/Tray/WindowsUserIdleSource.cs:48`, `:37`, `:43`; `src/BellTower/Tray/TrayApplication.cs:357`, `:371`.
- Observation: comparing an unsigned 32-bit elapsed result with full 64-bit uptime only rejects future ticks before the first wrap. At uptime `4294977296`, last-input tick `11000` is 1000 ms ahead of the low uptime word, yet `Elapsed` returns about 49.71 days idle. Microsoft documents that input ticks can be nonmonotonic or synthetic. Separately, snapshot freshness tests only an upper age bound: a timestamp one hour in the future is accepted by playback, so clock rollback or an inconsistent source can keep stale inactivity eligible.
- Proposed improvement and benefit: reject ambiguous/future tick deltas conservatively while preserving supported wrap cases; require nonnegative bounded observation age throughout source and playback, preferably with one shared freshness predicate. These checks prevent recent/unknown input from looking idle.
- Behavior-preservation risks: legitimate uint-wrap elapsed values and the supported maximum idle threshold must remain usable. Unknown results should withhold reminders without affecting ordinary alerts.
- Evidence type: official contract inspection and executed arithmetic/predicate probes; no synthetic input injected into Windows.
- Verification: known wrap, future tick before/after wrap, future observation, stale observation and clock rollback fixtures with explicit clocks.
- Relationship: distinct from ANALYSIS-004's cache reuse when a newer valid observation is already available.

### ANALYSIS-004: Playback skips current input observation while the tray cache is young

- Severity: warning.
- Related tasks: 2.4, 2.5, 3.3; scheduling/playback input revalidation.
- Location: `src/BellTower/Tray/TrayApplication.cs:367`.
- Observation: `CurrentIdle` calls the injected idle source only after its cache exceeds 500 ms. A queued reminder validated 100 ms after an idle observation remains valid even when the source already reports recent input. The silent predicate probe returned true and made zero source reads. Queue and sink cancellation therefore reuse old input eligibility throughout that window instead of observing the user's return before tone/speech.
- Proposed improvement and benefit: ensure reminder playback/cancellation asks the current input boundary for its latest evidence rather than bypassing it based on the tray cache; provide a bounded way for that boundary to reject or refresh obsolete idle evidence after input. Preserve conservative unknown behavior.
- Behavior-preservation risks: native calls and callback time must remain bounded; stale/unknown input should discard that reminder without catch-up and without suppressing unrelated ordinary alerts.
- Evidence type: executed injected-source playback-validity probe, with no UI construction or sound.
- Verification: queue a reminder with idle evidence, update the source to recent input before playback and during a tone, then assert no reminder speech and correct mixed-batch behavior using synchronized silent sinks.
- Relationship: no earlier canonical finding supplied.

### ANALYSIS-005: Settings dialog replacement drops unknown reminder properties

- Severity: warning.
- Related tasks: 1.2, 3.2, 4.2; unknown-field preservation and shared controls.
- Location: `src/BellTower/Tray/ReminderSettingsForm.cs:93`.
- Observation: Save replaces `value.Reminders` with a fresh instance that copies only known controls. Existing nested `AdditionalData` is discarded. The exact replacement mapping, executed against disposable config with `futureReminder`, removed that field. The passing storage test edits an existing object and does not cover this UI mapping.
- Proposed improvement and benefit: preserve the latest reminder object/extension data while applying only the edited known settings, so the user control does not erase metadata written by another compatible version.
- Behavior-preservation risks: validation, unrelated settings, and current stored fields must remain intact; avoid overwriting a concurrent unknown-field change from an older form snapshot.
- Evidence type: source-confirmed mapping plus executable configuration reproduction; graphical dialog not invoked.
- Verification: save through the actual form mapping/service with root and nested unknown properties and verify both survive reload; retain invalid-bounds rejection and independent provider availability.
- Relationship: no earlier canonical finding supplied.

## Re-verification

Initial review; no developer corrections were assigned to this reviewer yet. The orchestrator owns severity gates, duplicate relationships and ledger states.

## Assessment and limitations

The shared architecture and explicit receipt-before-enqueue transaction provide a useful foundation, but five concrete acceptance defects remain on this target. All findings apply to shared behavior; ANALYSIS-002 additionally exercises Codex's runtime-only confirmation path. No provider restriction is claimed. See [supporting evidence](../evidence/analysis-001.md) for commands, fixture construction, observed outputs and official documentation.

No broad test/build/format run, native PInvoke execution, user-operated desktop/RDP trial, installed-build trial or physical audio check was performed by the analyzer. QA owns the full regression result. The reviewed implementation identity remained unchanged, and the analyzer wrote only its reserved report/evidence plus ignored scratch artifacts.
