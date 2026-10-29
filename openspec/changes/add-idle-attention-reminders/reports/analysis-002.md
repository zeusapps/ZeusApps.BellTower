# Analysis report: add-idle-attention-reminders / 002

## Review target

- Reviewer / role: Codex independent analyzer / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / delegated recheck / `gpt-6.1-sol` (role default).
- Date: 2026-10-28, 19:13 UTC.
- Change root / assignment: `openspec/changes/add-idle-attention-reminders` / analysis-002.
- Task and requirement references: ANALYSIS-001–005 and affected regression, with QA-006–008 coverage inspected where it supports those fixes. No new optional review scope.
- Scope: `src/BellTower`, `tests/BellTower.Tests`, `README.md`, `docs/compatibility.md`, `docs/attention-reminders.md`; complete 29-file manifest and per-file hashes in [target 002](../evidence/review-target-002.txt).
- Baseline and HEAD: `47dfca9fd46b309017d4d785a1f91fd249d42041`.
- Implementation identity: tracked SHA-256 `2cabb1effe533ce8f545f5f7b109e436656497058bf1c28e3786acb1b0cd1007`; 16 untracked files/digest `08007d8b2afe`.
- Before/after identity: both `head=47dfca9 baseline=47dfca9 files=29 diff=2cabb1effe53 untracked=16/08007d8b2afe`; exact full-output comparisons matched target 002. No implementation mutation observed.
- Prior reports: [analysis-001](analysis-001.md), [initial evidence](../evidence/analysis-001.md), [developer correction handoff](../evidence/implementation-002.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Receipt continuity and lifecycle retention | Isolated serialized store probes: cap three, absent current projection, fresh store/new run after 90 days; Claude and Codex | [Analysis 002 evidence](../evidence/analysis-002.md) | Passed for ANALYSIS-001. Core/IO now separate retained episode history from scheduling eligibility. |
| Current escalation validation | Earn provisional Codex candidate with current runtime confirmation; enumerate confirmed/unconfirmed/negative projections and changed Mute/disable config | [Analysis 002 evidence](../evidence/analysis-002.md) | Passed for ANALYSIS-002. No new idle gate is imposed on an already earned candidate. |
| Conservative clock/tick boundaries | Arithmetic and snapshot predicate probes; inspect half-uint and nonnegative-age checks | [Analysis 002 evidence](../evidence/analysis-002.md) | Passed for ANALYSIS-003. Valid wrap and idle above 24 hours remain usable. |
| Playback freshness through actual source | Low-level observer/manual-clock seam inside real `WindowsUserIdleSource`, then real tray validity predicate without constructor | [Analysis 002 evidence](../evidence/analysis-002.md) | Failed ANALYSIS-004: native wrapper still supplies cached pre-input eligibility. |
| Form mapping and unknown fields | Invoke actual `ReminderSettingsForm.ApplySettings` within isolated config transaction; inspect actual Save call and clone-before-validation | [Analysis 002 evidence](../evidence/analysis-002.md) | Passed for ANALYSIS-005; nested unknown fields retained. |
| Supporting regression fixtures | Inspected new native-state/ABI/manual-clock/single-flight, serialized restart/continuity/pruning and form fixtures | Source at target 002 | Coverage added for QA-006–008; not represented here as independently executed xUnit tests. Actual source-wrapper playback gap is absent from the mutable-source playback fixture. |
| Performance/boundedness | Inspected single in-flight task and 250 ms retry throttle; no benchmark | Source and deterministic call counters | Bound remains; no performance claim or optional optimization finding. Fresh playback must preserve this bound. |
| Broad tests, build and formatting | Owned by QA; not rerun here | Developer handoff read as reported evidence only | Not independently performed; no overlapping broad run. |
| Integrity/hygiene | Before/after exact target comparison and inspection of own report/evidence | Reserved manifest, report and evidence | Passed for target integrity and analyzer outputs. Full content sweep belongs to QA. |

## Findings

### ANALYSIS-004 remains: real idle source still reuses pre-input playback eligibility

- Severity: warning, unchanged acceptance blocker.
- Related tasks: 2.4, 2.5, 3.3.
- Locations at target 002: `src/BellTower/Tray/WindowsUserIdleSource.cs:49`, `:55`; `src/BellTower/Tray/TrayApplication.cs:373`.
- Observation: replacing the tray cache bypass with `userIdle.GetSnapshot()` fixes the mutable fake-source scenario, but the production wrapper itself throttles new observations for 250 ms and permits its cached sample for 500 ms. After harvesting 20-minute idle evidence at T, change the injected low-level observation to recent input and advance to T+100 ms. The real source still returns 20 minutes idle, its low-level counter remains one, and the real tray reminder predicate returns true.
- Proposed correction and benefit: provide a bounded fresh-read path for playback/cancellation so a pre-request sample cannot grant reminder eligibility. Keep potentially blocking WTS queries off the callback. A single-flight worker request with a fixed completion budget and unknown on timeout is one narrow option; an equivalent bounded fresh local-input boundary is acceptable. Ordinary periodic status may retain caching.
- Behavior-preservation risks: no unbounded wait/native session query on the audio thread, no overlapping workers, and no later replay of a reminder dropped on unknown input. Preserve ordinary alerts and mixed-batch tone behavior.
- Evidence type: executed silent production-wrapper/predicate reproduction, no actual native input, UI constructor or audio.
- Verification: extend the source-wrapper fixture to change the low-level result from idle to input before playback, plus blocked-observer completion budget/single-flight and before-playback/during-tone mixed silent-sink checks. The existing mutable `IUserIdleSource` test alone cannot verify this correction.
- Relationship: existing ANALYSIS-004, also represented by duplicate QA-004; no new finding ID allocated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `2cabb1effe53` / `08007d8b2afe` | Both-provider serialized cap/omission/restart probes retain ordinal three, stable ID and future deadline; retained lifecycle set inspected | Verified on this target; orchestrator owns ledger state. |
| ANALYSIS-002 | Same target | Current confirmation permits; missing/negative confirmation and disabled config withhold; Mute permits | Verified on this target at the agreed earned-candidate contract. |
| ANALYSIS-003 | Same target | Future post-wrap tick rejected; short wrap preserved; 25-hour inactivity permitted; future observation rejected | Verified on this target. |
| ANALYSIS-004 | Same target | Actual source wrapper returns pre-input idle, one low-level read, tray validity true at T+100 ms | Still failing; requires another bounded correction and recheck. |
| ANALYSIS-005 | Same target | Actual Save mapping retains nested extension data under isolated ConfigStore transaction; callsite/validation inspected | Verified on this target. |

## Assessment and limitations

Four of five assigned findings are independently verified. ANALYSIS-004 remains a concrete failure of the original playback revalidation requirement, rather than a request for zero delay between sampling and sound: the reproduced playback check makes no fresh low-level observation attempt and authorizes from an earlier sample. No new optional suggestions or finding IDs were raised.

The implementation stayed frozen. These checks are synthetic, isolated and silent. No broad suite, native desktop/RDP trial, physical audio/input, installed-build operation or webhook send was performed by the analyzer. Current QA owns the broader regression result. The orchestrator owns acceptance/triage and any next correction assignment.
