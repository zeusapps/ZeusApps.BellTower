> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-001); client: Claude Code 2.1.330; mode: delegated subagent (code-analyzer role); model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: distinct-session-colors / ANALYSIS-001

## Review target

- Reviewer / role: ANALYSIS-001 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330 / delegated subagent (code-analyzer role) / claude-opus-5-5
- Date: 2026-11-22T18:27Z
- Change root / assignment: `openspec/changes/distinct-session-colors` / ANALYSIS-001
- Task and requirement references: tasks 1.1, 2.1, 2.2, 3.1 ([tasks](../tasks.md)); design decisions 1 to 5 ([design](../design.md)); requirements "Shared readable session palette" and "Shared provider presentation controls" ([spec delta](../specs/session-identity/spec.md))
- Scope and relevant neighboring paths: in the isolated worktree `.claude/worktrees/agent-a2bf97db8a9861fe4`: `src/BellTower/Core/Palette.cs`, `src/BellTower/IO/IdentityStore.cs`, `src/BellTower/Core/IdentityControls.cs`, the three palette test classes, `docs/architecture.md`, `docs/session-identity.md`, `docs/installation.md`, [implementation evidence](../evidence/implementation-001.md). Neighbors read: `src/BellTower/IO/LaunchPresentationStore.cs`, `src/BellTower/IO/StateStore.cs`, `src/BellTower/IO/StateSnapshotStore.cs`, `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/IO/DataLock.cs`, `src/BellTower/Providers/HookIngestor.cs`, `src/BellTower/Providers/CaptureConsumer.cs`, `src/BellTower/Sessions/AttachmentLifecycle.cs`, `src/BellTower/Sessions/SessionCatalog.cs`, `src/BellTower/Core/AttachmentHealth.cs`, `src/BellTower/Core/ConversationLifecycle.cs`, `src/BellTower/Core/SessionReducer.cs`, `src/BellTower/Hosts/TerminalPresentationResolver.cs`, `src/BellTower/Integration/BellTower.psm1`, `vscode/*.js`, `docs/compatibility.md`.
- Baseline commit: 605a513
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 605a513 -Paths src tests docs openspec/changes/distinct-session-colors/evidence`, run in the worktree:
  - HEAD 605a5130f590d535f1484a3f2a1896e1126179cc
  - Manifest: `? evidence/implementation-001.md`, `? tests/BellTower.Tests/Core/PaletteSeriesTests.cs`, `? tests/BellTower.Tests/IO/LiveSessionColorAllocationTests.cs`, `M docs/architecture.md`, `M docs/installation.md`, `M docs/session-identity.md`, `M src/BellTower/Core/IdentityControls.cs`, `M src/BellTower/Core/Palette.cs`, `M src/BellTower/IO/IdentityStore.cs`, `M tests/BellTower.Tests/IO/PaletteAllocationTests.cs`
  - Tracked diff SHA-256: 0b2c5d1f7065dda8874323a28ddf8d4d095b1c3799111d9763bf9fa9ffa163bb
  - Untracked: 8ca3087c... (evidence), fd1fc083... (PaletteSeriesTests), c60a1aee... (LiveSessionColorAllocationTests)
  - Summary: `head=605a513 baseline=605a513 files=10 diff=0b2c5d1f7065 untracked=3/2e2337a9802f`
- Before/after identity: after the analysis, the same command printed `head=605a513 baseline=605a513 files=10 diff=0b2c5d1f7065 untracked=3/2e2337a9802f`. It is identical to the before identity, so nothing changed unexpectedly.
- Prior reports / rechecked findings: none

Source references below are worktree paths at the identity above (baseline 605a513 plus the uncommitted diff).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness: OKLab math | Read `Palette.OkLab`, `FromOklch`, `Linear` and `Encode` and compared them with the published OKLab matrices | All six matrices and the inverse match the reference coefficients. The sRGB transfer and clamp are correct. `Math.Cbrt` handles the non-negative LMS inputs | Correct |
| Correctness: series table | Disposable probe outside the repository (not retained) that re-derives the table from hue 25 + 30k, accent L .56 C .11 and background L .22 C .035, using the shipped formulas | All 24 hex values match. Entries 1 and 10 are gamut-clipped, and the tests clip the same way. Accent/foreground contrast is 4.62 to 4.93. Background vs `#f1f5f9` is 15.61 to 16.04. Consecutive entries in series order are 0.149 to 0.221 apart | Correct and within the design thresholds |
| Determinism | Read `Palette.Allocate` (`Palette.cs:36-48`) | Pure function of the in-use set. The order of `inUse` does not matter because the rule takes a minimum. Strict `>` keeps series order as the tie-break. The table is fixed, so platform floating-point differences can only matter at exact ties with custom accents | Deterministic. For overflow behavior see ANALYSIS-002 |
| 0.04 "used" threshold | Probe: pairwise OKLab distances of the shipped accents, plus legacy HSL accents (h, .60, .42) and the eight named colors against the series | Series accent pairs: min 0.0513, max adjacent 0.0582. The "about .057" in `Palette.cs:27` and design decision 3 is the upper end, and the margin over 0.04 is 0.011. A custom accent at the midpoint of two adjacent entries is at most 0.029 from both, so it can block two entries. 27 of 360 legacy hues block a series entry. No named color does (nearest 0.072) | The threshold fits the series' own minimum spacing and works reliably for series-vs-series matches. No finding |
| Visible separation (terminal background) | Probe: pairwise OKLab distances of the shipped backgrounds | First four entries: min 0.0485. First five or more: min 0.0175 (entries 0 and 4, hues 25 and 55) | ANALYSIS-001 |
| Lock discipline | Read every `DataLock` acquisition | `StateStore.Drain` holds "state" and calls `CaptureConsumer.Prepare` -> `IdentityStore.Get` ("identity"), so the order is state -> identity. `LaunchPresentationStore.Prepare` is identity -> launch. Taking "state" in `AllocatePalette` would invert the order, so the lock-free read is required, as the design says | Correct |
| State snapshot read without lock | Read `StateSnapshotStore.Read` and `AtomicFile` | Writes use a temp file plus `File.Replace` with `.bak`, so a reader always sees a complete snapshot, current or previous. During a drain the in-memory state is ahead of disk, and the one-hour unlisted-identity rule covers exactly that gap. Degraded case: if both state files are unreadable, `Read` returns an empty state and only identities written in the last hour count. A plain `File.ReadAllText` reader can briefly make a concurrent `File.Replace` fail with a sharing violation. Other unlocked `StateStore.Read` callers (`AttachmentLifecycle`, `TerminalTitleService`) already read this way, so the risk is not new | Acceptable. No finding |
| Over-reservation: registering identities (last hour) | Read `SessionReducer.ApplyUnordered` (every applied observation creates a state session), `CaptureConsumer`, `HookIngestor` and `StateStore.PruneEndedSessions` | After its drain, a quickly ended session is listed in the state as ended and stops counting at once. It counts only between identity creation and the next drain. Remaining one-hour reservations: identities created or touched for keys the state never lists (for example `rename` or `import --badge` on an unknown key, or the hook-process `HookIngestor` path while no drain runs), and ended sessions pruned beyond 200 that are written again. All are bounded by one hour | Acceptable. No finding |
| Over-reservation: non-ended sessions | Read `StateStore.ReconcileAttachments`, `AttachmentHealth.Evaluate`, `ConversationLifecycle.Reduce` and `TerminalPresentationResolver` | Reconciliation never ends "unavailable" sessions, yet they count as live | ANALYSIS-004 |
| Pending launches | Compared the `AllocatePalette` launch loop with `LaunchPresentationStore.Read`/`Consume`, `AttachmentLifecycle.Detach` and `docs/compatibility.md:96` | The reservation lasts 1 h, consumption is valid for 24 h, and the first hook can come at the first prompt | ANALYSIS-003 |
| Bounded cost under the identity lock | Code reading only, no measurement | Each allocation decodes and validates the full state (16 MiB budget). It also enumerates every identity file ever written, because nothing prunes `paths.Identities`, and reads recently written live identities a second time | ANALYSIS-005 (hypothesis) |
| Shared path | `grep` for `AllocatePalette`, `IdentityControls`, `SetColor` and palette assignments | All four flows reach `AllocatePalette`: managed launch (`PrepareSessionCommand` -> `LaunchPresentationStore.Prepare`), native adoption and fork children (`HookIngestor`/`CaptureConsumer` -> `IdentityStore.Get`), and reset and random (`SessionCatalog` -> `IdentityStore.SetColor` -> `IdentityControls`). `LegacyImport` and explicit colors set palettes directly, by design. The module (`BellTower.psm1`) only applies the prepared palette, and `vscode/*.js` has no color logic | One path. No finding |
| Own-color exclusion | Read `Add(Accent(key))` (`IdentityStore.cs:120`) | The saved accent is counted on every allocation. On creation the file does not exist yet, so nothing is added. A live session's accent can be added twice, which does not change a minimum | Correct |
| `candidate` seam removal | `grep` for `new IdentityStore(` with a second argument, and the `PaletteAllocationTests` diff | No production or test caller remains. The barrier test that proved the lock deterministically was replaced by parallel creations: `LiveSessionColorAllocationTests.ConcurrentCreationsFromBothProvidersGetDifferentSeriesColors` (10 rounds x 4 threads) and `PaletteAllocationTests` (12 threads). Without the lock these would collide on `Series[0]`, but they are probabilistic | Acceptable simplification. Coverage note only |
| Compatibility | Read the `GetCore` validation, the `Palette` record and `LaunchPresentation`, plus the test `OldSavedPalettesStayReadableAndUnchanged` | Existing identities are returned unchanged and not rewritten (byte-equal test). New palettes keep the three-hex shape that old builds validate. `Palette.Distance` changed scale (RGB 0-441 to OKLab 0-1), but no product code outside `Palette` calls it | Compatible. No finding |
| Duplication / complexity | Code reading | `AllocatePalette` stays one method with three sources. The three existing sites do not use the new `IdentityPath` helper | ANALYSIS-006 |
| Parity (Claude Code and Codex) | Read the allocation and its callers, plus the test inventory | The policy has no provider branch. Tests cover both providers (theories, mixed managed/native, concurrent mixed). ANALYSIS-001 to ANALYSIS-005 apply equally to both providers. ANALYSIS-003 is most likely with Codex managed launches, because Codex's first hook can wait for the first prompt | Parity holds |
| Docs | Compared `docs/architecture.md`, `docs/session-identity.md` and `docs/installation.md` with the code | The docs match the implemented rule, thresholds and windows. session-identity says colors "repeat only beyond twelve" but does not say that every repeat takes the first entry (ANALYSIS-002) | See ANALYSIS-002 |
| Build, tests, format | Not run | QA runs them concurrently, per the assignment | Not performed |

## Findings

### ANALYSIS-001: From five concurrent sessions on, two terminal backgrounds are nearly indistinguishable

- Severity: warning
- Related task / requirement: 1.1, 4.3; design decision 1; "Shared readable session palette" (scenario "Concurrent sessions get distinct colors"); proposal Why (T-11)
- Locations: `src/BellTower/Core/Palette.cs:7-23` (series constants and table); `tests/BellTower.Tests/Core/PaletteSeriesTests.cs:45-57` (separation is asserted for accents only)
- Observation:
  - T-11 is about terminal background colors, and the background is what Windows Terminal and VS Code show.
  - The backgrounds use chroma 0.035 at lightness 0.22. Two entries 30 degrees apart therefore differ by only about 0.017 OKLab (measured 0.0172 to 0.0175), roughly the smallest difference a person can notice.
  - Twelve hues 30 degrees apart allow only four entries at 90 degrees. Any five or more concurrent sessions therefore include a 30-degree pair. With six sessions, entries 0 (hue 25) and 4 (hue 55) have backgrounds 0.0175 apart.
  - For comparison, the old random HSL backgrounds 30 degrees apart were about 0.0375 apart. Old accents 30 degrees apart were about 0.137 apart; the new ones are 0.051 apart.
  - The allocation rule is now correct, but the six-session acceptance scenario of task 4.3 may still show two near-identical backgrounds.
- Proposed improvement and benefit:
  - Decide the series constants before task 4.3. The orchestrator and user own the parameter choice.
  - One option keeps the design's shape: alternate background lightness between odd and even hue steps, and raise background chroma within gamut.
  - Probe result for background C .05, with L .22 on even steps and .27 on odd steps: minimum background distance 0.045. Background vs terminal text stays at 13.5:1 or better, and accent/foreground at 4.61:1 or better.
  - Add a test that bounds the minimum background distance among the first six series entries.
- Behavior-preservation risks: changing constants affects only new allocations and resets; saved palettes stay. Gamut clipping increases with chroma (3 to 5 clipped entries in the probe options), and the re-derivation test already covers that. The 4.5:1 and 7:1 thresholds must still hold.
- Evidence type: measured result for the OKLab distances of the shipped table (disposable probe with the shipped formulas, not retained). How it looks in a real terminal is a hypothesis.
- Verification / measurement plan: a unit test on the minimum background distance of the first six entries, and the task 4.3 user check with six sessions that explicitly includes a 30-degree pair (series entries 0 and 4).
- Relationship: none

### ANALYSIS-002: Every session beyond twelve gets the first series color

- Severity: warning
- Related task / requirement: 2.1; design decision 3 and risk "More than twelve live sessions"; scenario "More live sessions than series colors"
- Locations: `src/BellTower/Core/Palette.cs:36-48`; `docs/session-identity.md:65`
- Observation:
  - When all twelve series accents are in use exactly, every minimum distance is 0 and the tie-break returns `Series[0]`. Adding that accent again changes nothing, so sessions 13, 14, 15 and later all receive `Series[0]`.
  - The probe's allocation for 20 sessions was `0..11, 0, 0, 0, 0, 0, 0, 0, 0`.
  - The spec wording (farthest, with a deterministic tie-break) is met literally. The design's risk mitigation ("keeps them as different as the series allows") is not: with 14 sessions, three share one color while all others are unique.
- Proposed improvement and benefit: in the fallback, compare entries in this order:
  1. the number of in-use accents within `SameColorDistance` of the entry (fewest first);
  2. the largest minimum distance;
  3. series order.

  Repeats then go 0, 1, 2, ..., and no color is shared twice before every color is shared once. This is a few lines in `Allocate` and stays deterministic.
- Behavior-preservation risks: with twelve or fewer live sessions the result does not change, because the first-free branch handles them. Custom accents still count by distance. The existing tests `AFullSeriesTiesBreakInSeriesOrder` and `AThirteenthSessionGetsTheFarthestColorDeterministically` keep their outcomes.
- Evidence type: source observation, confirmed by the probe that replicates `Allocate`.
- Verification / measurement plan: a unit test in which 13 live sessions use the full series plus `Series[0]`, and the 14th gets `Series[1]`. Update the docs sentence on repeats.
- Relationship: none

### ANALYSIS-003: A managed launch stops reserving its color after one hour, though it can be consumed for 24 hours

- Severity: warning
- Related task / requirement: 2.1, 2.2; design decision 3 ("launch palettes prepared but not yet consumed within a short window"); scenario "Concurrent sessions get distinct colors"
- Locations: `src/BellTower/IO/IdentityStore.cs:128-136,151`; `src/BellTower/IO/LaunchPresentationStore.cs:51`; `src/BellTower/Sessions/AttachmentLifecycle.cs:12`; `docs/compatibility.md:96`
- Observation:
  - The wrapper applies the prepared background before the agent starts. The first accepted hook, which "can be at the first prompt", consumes the launch, which `LaunchPresentationStore.Read` accepts within 24 hours.
  - `AllocatePalette` stops counting the unconsumed launch after `PendingWindow` (one hour).
  - If a managed terminal waits more than an hour before its first prompt, another session can take the same series color while the terminal already shows it. The later hook then adopts the launch palette, and two live sessions share one color.
  - An unconsumed launch file exists only while the launch is pending: consumption marks it, and `Detach` removes it when the agent exits. A longer window therefore mostly matters for crashed wrappers.
- Proposed improvement and benefit: count unconsumed launches for the same validity `Read` uses (24 hours, from one shared constant). Optionally, count them only while the recorded shell is still live (`ProcessProbe.Identify(shell.Pid) == shell`, the check `Read` already makes), so a crashed wrapper does not hold a color. The registering-identity window can stay at one hour.
- Behavior-preservation risks: without a liveness check, a crashed wrapper would reserve its color for up to 24 hours. One process probe per pending launch under the identity lock is a small cost (few files). `ConsumedAndExpiredLaunchPalettesDoNotReserveColors` would need its "expired" fixture moved beyond the new window, or given a dead shell.
- Evidence type: source observation
- Verification / measurement plan: a test that a launch created two hours ago, unconsumed, with a live shell still reserves its color, and a test that one with a dead shell does not.
- Relationship: none

### ANALYSIS-004: Sessions stuck in "unavailable" reserve colors indefinitely

- Severity: warning
- Related task / requirement: 2.1; design decision 3 ("sessions not ended in the current monitoring state"); scenario "Ended sessions do not block colors"
- Locations: `src/BellTower/IO/IdentityStore.cs:118-119`; `src/BellTower/IO/StateStore.cs:182-226`; `src/BellTower/Core/AttachmentHealth.cs:13-33`; `src/BellTower/Core/ConversationLifecycle.cs:92-106`; `src/BellTower/Hosts/TerminalPresentationResolver.cs:16`; `src/BellTower/Core/SessionState.cs:122-126`
- Observation:
  - Reconciliation ends a session only on `confirmed-ended`.
  - A shared-backend conversation is only ever reduced to `unverified` or `unavailable`. A direct session whose agent process cannot be verified moves to `unavailable` after the grace period. Both stay not ended until an End event arrives.
  - Bell Tower already treats these sessions as not live elsewhere: no terminal presentation, no attention, activity "unavailable".
  - `AllocatePalette` still counts them as live. Stale conversations, for example closed VS Code conversations that never sent a SessionEnd, can each block a series color indefinitely, and new sessions drift down the series.
  - This is the T-11 mechanism (colors blocked by sessions that are not open) in a smaller form.
- Proposed improvement and benefit: decide the policy explicitly. For example, count an `unavailable` session only while its `LastObserved` (or `AttachmentUnverifiedSince`) is within the pending window, and always count `verified-live` and `unverified` sessions. This keeps the "live now" intent and still protects a conversation that briefly lost verification.
- Behavior-preservation risks: an `unavailable` conversation can revive (a hook moves it back to `unverified`). It keeps its saved palette and may then share a color with a session allocated in the meantime. That is the trade-off to accept or reject. The policy is shared, so both providers change together.
- Evidence type: source observation. How often stale `unavailable` sessions occur in real data is a hypothesis; it was not measured, and the user's data directory was not read.
- Verification / measurement plan: a unit test with a stale `unavailable` session (old `LastObserved`) holding `Series[0]`, where a new session gets `Series[0]`. Optionally, with user authorization, count non-ended sessions by `AttachmentHealth` in a disposable copy of a real state file.
- Relationship: none

### ANALYSIS-005: The allocation cost under the identity lock was not measured, though the design requires it

- Severity: suggestion
- Related task / requirement: 2.1; design "Risks / Trade-offs" ("Measure it in the tests")
- Locations: `src/BellTower/IO/IdentityStore.cs:111,117-127`; `src/BellTower/IO/StateSnapshotStore.cs:18-36`
- Observation:
  - Each allocation:
    - decodes and validates the whole state snapshot (16 MiB budget);
    - reads one identity per non-ended session;
    - enumerates every identity file ever written, because no code prunes `paths.Identities`;
    - reads every identity written in the last hour, including the live ones the state loop already read.
  - All of this happens under the "identity" mutex, which every hook ingest also takes, with a 5-second acquisition timeout.
  - Allocation only runs on identity creation, launch preparation, reset and random, so the cost is bounded and probably small. Neither the evidence nor the tests record a measurement.
- Proposed improvement and benefit: before reading, skip recent files whose name is `ContentHash.Compute(key)` of a key the state lists; this removes the double reads. Record one bounded measurement in the evidence, for example timing `AllocatePalette` with a state of 200 ended and 12 live sessions plus 5,000 identity files. No benchmark framework is needed.
- Behavior-preservation risks: the name filter must match `IdentityPath` exactly. An identity file whose stored key does not match its name would then be skipped rather than counted. That matches the `saved.Key == identityKey` check already used for state sessions.
- Evidence type: performance hypothesis
- Verification / measurement plan: one timed test, or a probe recorded in the evidence. Accept the cost if it stays well below the 5-second lock timeout at the 16 MiB budget.
- Relationship: none

### ANALYSIS-006: The new identity path helper is not reused by the existing call sites

- Severity: suggestion
- Related task / requirement: 2.1
- Locations: `src/BellTower/IO/IdentityStore.cs:17,64,140,160`
- Observation: the change adds `IdentityPath(key)` for the allocation, but `GetCore`, `Find` and `SaveCore` still build the same `Path.Combine(paths.Identities, ContentHash.Compute(key) + ".json")`. That makes four copies of the path rule in one class, and ANALYSIS-005's name filter would add one more place that depends on it.
- Proposed improvement and benefit: use `IdentityPath` at the three existing sites, so the file-name rule has a single source.
- Behavior-preservation risks: none (same expression).
- Evidence type: source observation
- Verification / measurement plan: the existing identity tests.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | - | - | not applicable |

## Assessment and limitations

What holds up:
- The series and OKLab math are correct and deterministic. The table matches its OKLCH derivation and meets the contrast thresholds.
- The lock-free state read is necessary, because the lock order is state, then identity.
- Managed launches, native adoption, forks, reset and random color all use one shared allocation path with no provider branch, so Claude Code and Codex parity holds.
- Removing the `candidate` seam is safe.
- Saved palettes are untouched and stay readable by old builds.

Four warnings remain:
- ANALYSIS-001: two terminal backgrounds are nearly indistinguishable from five concurrent sessions on, which is the T-11 symptom itself.
- ANALYSIS-002: every session beyond twelve gets the first color.
- ANALYSIS-003: the launch reservation window is shorter than the consumption window.
- ANALYSIS-004: stale `unavailable` sessions count as live.

ANALYSIS-001 and ANALYSIS-004 are policy choices for the orchestrator or user. ANALYSIS-002 and ANALYSIS-003 are small, local corrections. ANALYSIS-005 and ANALYSIS-006 are suggestions.

Not performed:
- Build, tests and format verification; QA owns them in this round.
- Any measurement of allocation latency.
- Inspection of real user data.

The OKLab probes were disposable computations outside the repository. They used the shipped formulas and table and were not kept. Nothing was written to the repository, and the target identity is unchanged.
