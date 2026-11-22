> Saved verbatim by the orchestrator. Reviewer: code-analyzer subagent (ANALYSIS-003), Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`. The client refused the reviewer's native report write (known restriction, see docs/agent-workflow.md#known-client-restrictions); the reviewer returned the text inline.

# Analysis report: bound-ended-session-state / ANALYSIS-003 (final recheck of fix cycle 2)

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-003) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-21T14:05Z
- Change root / assignment: `openspec/changes/bound-ended-session-state` / ANALYSIS-003 (final recheck)
- Task and requirement references:
  - tasks 1.1, 1.2, 1.4, 1.5 and 2.1 in [tasks.md](../tasks.md)
  - design decisions 1 (revised 2026-11-21), 3 and 4 in [design.md](../design.md)
  - requirement "Bounded monitoring state" in [specs/session-tracking/spec.md](../specs/session-tracking/spec.md):
    - scenarios "Delayed earlier event after compaction", "Resumed session after compaction", "State at the budget recovers" and "Live sessions alone exceed the budget"
    - "a bounded, sanitized reason without one entry per tick"
  - developer evidence [implementation-003.md](../evidence/implementation-003.md)
- Scope and relevant neighboring paths:
  - DEV-003 changes:
    - `src/BellTower/Core/CausalSessionReducer.cs` (`Compact`, lines 88-128)
    - `src/BellTower/IO/StateStore.cs` (`Drain`, lines 88-117)
    - `src/BellTower/IO/StateSnapshotStore.cs` (`Save`, lines 44-89)
    - `src/BellTower/IO/RefreshHealth.cs`
    - `docs/architecture.md` and `docs/hook-ingress.md`
  - Neighbors read:
    - `src/BellTower/Core/SessionReducer.cs`: `Apply`, and `ApplyUnordered` lines 17-112 (the attachment-change branch at 37-57, the ended-session gate at 59-63, and the `LastObserved` update at 78)
    - `src/BellTower/Core/EventKind.cs`
    - `StateStore.ReconcileAttachments` (ending by attachment evidence, line 220)
    - `src/BellTower/Tray/TrayApplication.cs` (`Tick` lines 242-304, `RefreshOverview`)
    - `src/BellTower/Sessions/NativeNamePump.cs`
    - `src/BellTower/IO/LocalLog.cs`
- Baseline commit: 5e95554
- Implementation identity (before): `head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/5e050d0888dd`
  - Tracked diff SHA-256: `87172e79b1189eadf5bce8fcb79b2762db7b54e4e886fe9cf83b3e4af61b6a7f`
  - Untracked:
    - `RefreshHealth.cs` e38f701f…
    - `BoundedStateTests.cs` 2d4b3147…
    - `RefreshHealthTests.cs` ffb75665…
    - `StateReporter.cs` 3ea6234c…
    - `HookStartReporter.cs` 3e8cd554…
    - `StateReporterTests.cs` b09d5ec1…
    - `HookTrayStartTests.cs` 5259cc38…
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/5e050d0888dd`. It matches the before identity and the expected summary, so there was no unexpected mutation.
- Prior reports / rechecked findings:
  - [analysis-001.md](analysis-001.md): ANALYSIS-001, ANALYSIS-002 and ANALYSIS-003
  - [analysis-002.md](analysis-002.md): ANALYSIS-100, ANALYSIS-101, ANALYSIS-102 and ANALYSIS-103
  - also read: [qa-002.md](qa-002.md) and [findings.md](findings.md)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Probe setup | The current `src/BellTower`, `BellTower.Protocol` and `BellTower.Distribution` sources (no `bin`/`obj`) were copied to the session scratch directory, outside the repository. The four DEV-003 files were confirmed byte-identical with `cmp`. A scratch console referenced the copy; it was built there and run against scratch data directories, with `BELLTOWER_DATA_DIR` also pointed at scratch. | The probe runs the real `StateStore.Drain`, `CaptureFactory`, `CaptureSpool`, `CausalSessionReducer.Compact`, `StateSnapshotStore.Save` and `RefreshHealth`, for `claude` and `codex`. | No repository build, no real data directory, no tray. Nothing in the probe is committed. |
| Fold rule: ANALYSIS-101 reorder | Real capture drains for both providers: End, then a settled drain, then each of the sequences S then R (`R.At < S.At`), R then S, and S1, S2 then R, followed by a later prompt | S then R: after S the session is ended, the journal holds 1 entry, the checkpoint is an ended copy and the floor stays at End. After R it is live with `turn-s` tracked, and the later prompt is tracked. R then S and S1, S2 then R are both live with every later turn tracked. | Fixed for both providers. |
| Fold rule: delayed earlier events | Drain probe: a permission timed before End, sent after compaction with a kept stray. Reducer probe: the same on a session ended by attachment evidence. | Ended, 0 waits, floor unchanged. A later resume then revives the session and replays the kept stray. | Holds on the causal path. The direct path is unchanged from analysis-001, which verified it: an ended session ignores anything except a newer registration. |
| Fold rule: attachment evidence and new attachment | Reducer probe: Register, Prompt and ToolStart, then `Ended = true` as `ReconcileAttachments` sets it, then `Compact`, a stray, `Compact` twice, and a resume timed before the stray. Separately, a stray and then a registration with a new attachment. | Evidence: the floor is the last applied ToolStart, the stray is kept, the second and third `Compact` return false, and the session revives with the stray's turn. New attachment: it revives on `att2` with the stray's turn. | Holds for both providers. |
| Fold rule: `At <= LastObserved` boundary | Reasoning over `ApplyUnordered` lines 39 and 59. Probes of a duplicate End at End's instant and of a prompt at End's instant. | A registration revives an ended session only when its `At` is after `LastObserved` (same attachment), or at or after it (new attachment). The fold leaves the floor at or before `LastObserved`, so every registration that could revive the session still passes the floor. Folding by time differs from folding by the full `Compare` order only for observations at exactly `LastObserved` that sort after the last applied one. For an End-ended session that is only a duplicate End, because `End` is the highest kind. The only registration that could be lost would also have to fall at exactly that instant, carry a new attachment, and sort after a floor that is itself a `Register`, which needs a session ended by attachment evidence. A full-`Compare` floor would drop it as well. In the probes, the duplicate End and the same-instant prompt were ignored and the later resume revived the session. | Correct. An equal-time stray is not mishandled. |
| Checkpoint plus kept journal | Reducer probe: the kept journal replayed through `ApplyUnordered` from a copy of `CausalCheckpoint`, compared with the stored session (ended state, times, turn, episode, waits, background, operations, approval revision, directory) | Equal for both providers. `Copy` omits only the native-signal fields, which `Apply` restores from the previous session. That predates this change. | Consistent. |
| Growth and save loop | Reducer probe: 300 strays on a compacted session, with `Compact` after each one. Drain probe: two quiet drains after a stray. | The journal stays at the 256 window and `Compact` returned false all 300 times. The quiet drains left `state.json` bytes and write time unchanged. Past 256 strays the window moves the floor onto a stray, so a resume timed before all of them is dropped. That is the existing window bound, as for a live session. One such session is about 180 K characters. | No save loop. Growth is bounded by the window per session, and across sessions by the 200 cap and the budget fallback. Many strays cost retained ended sessions, never a wedge, as design decision 1 accepts. |
| Relief compaction (ANALYSIS-103) | Drain probe: 20 settled ended sessions with full journals, a budget of the stored size plus 20 KB, and 100, 130 or 300 queued filler captures from both providers. Refusal probe: a 2,000-byte budget with 140 queued. Reading `Save` lines 44-89. | 100, 130 and 300 queued: all 20 ended sessions kept and compacted, 128 filler sessions live, and the captures beyond the batch still queued. Refusal: `InvalidDataException`, 140 of 140 captures kept and `state.json` unchanged. `relieve` is `CompactSettledSessions`, which touches only ended sessions and never removes one. Removal still covers ended sessions only, and the live-only refusal is unchanged. | Fixed. The no-live-removal and captures-kept-on-refusal guarantees hold. |
| Sanitizer (ANALYSIS-100) | `Describe` probe with synthetic profiles and names | Profile `C:\Users\O'Brien` with user `O'Brien`: the quoted path becomes `'<path>'`, the unquoted one `<path>`, a path elsewhere that contains the name becomes `'<path>'`, and the bare name `<user>`. The 2-space-profile, Unix-profile and data-file cases are unchanged. A real U+E000 in a message becomes `<user>`: it over-masks and leaks nothing. | The profile and user-name case is fixed. An apostrophe in any other path still leaks that path's tail (ANALYSIS-200). |
| Rate limit (ANALYSIS-102) | Fake clock, 250 ms ticks for 30 minutes: 8, 9 or 12 rotating reasons, a new reason on every tick, and strict alternation of failure and success | 8 reasons: 8 lines per 10 minutes. 9, 12 and unique reasons: 9 lines per 10 minutes (8 reasons and 1 notice), with 3 record writes in 30 minutes. Alternation: one line per tick (2,400 per 10 minutes) and one record write per tick. | Bounded for any stream of failures. Not bounded when successes are interleaved (ANALYSIS-201). |
| Docs | Reading the DEV-003 sentences in `docs/architecture.md` and `docs/hook-ingress.md` | The fold rule, kept strays, budget exception and nine-line bound all match the code. The hook-ingress parenthetical says the journal is "dropped" and then "kept in the journal". | Wording only, no defect. |
| Parity | Probes and code reading | Every probe ran for Claude Code and Codex, and no provider branch was added. | Parity holds. Both new findings affect both providers equally. |
| Format / build / suite | Not run in the repository | Assignment constraint: QA builds and runs the suite concurrently. | Not run. |

## Findings

### ANALYSIS-200: An apostrophe in a path other than the profile still leaks the rest of that path

- Severity: warning (content safety; low likelihood)
- Related task / requirement: 2.1; design decision 4 ("The message contains no path"); spec "a bounded, sanitized reason"
- Locations: `src/BellTower/IO/RefreshHealth.cs`
  - `:186-187`: `Quoted` ends a segment at the first apostrophe (`[^'\r\n]`)
  - `:190-191`: `PathPattern` excludes `'` from every segment
  - `:167`: `PathTail` excludes `'` in the same way
- Observation:
  - DEV-003 fixed the profile and user-name case exactly as ANALYSIS-100 proposed. The same mechanism still applies to any other path that contains an apostrophe. Probe output, with synthetic names:
    - `Access to the path 'D:\Code\Bob's app\.git\index.lock' is denied.` becomes `Access to the path '<path>'s app\.git\index.lock' is denied.`
    - `Could not find file 'D:\Clients\Smith's Project\notes\plan.md'.` becomes `Could not find file '<path>'s Project\notes\plan.md'.`
    - Unquoted, `D:\Code\Bob's app\.git\index.lock` becomes `<path>'s app\.git\index.lock`.
    - A provider home outside the profile, `'E:\codex-home\Bob's\sessions\x.jsonl'`, becomes `'<path>'s\sessions\x.jsonl'`.
  - The tail after the apostrophe reaches `bell-tower.log`, `refresh-health.json` and doctor.
  - A profile subfolder with an apostrophe is masked, because the remainder forms a new quoted segment.
  - Likelihood: the tick's caught exceptions mostly name data-directory files, which are masked whatever their characters. A leak needs an IO exception that names a repository, a custom provider home or another location outside the profile.
- Proposed improvement and benefit:
  - Treat an apostrophe as the closing quote only when it is followed by whitespace, `.`, `,`, `;`, `:`, `)` or the end of the text.
  - Let a path segment hold an apostrophe that is followed by a letter.
  - Add the four cases above to `RefreshHealthTests`.
- Behavior-preservation risks:
  - Two quoted paths on one line must still be masked separately, so use a lazy match up to the qualified closing quote.
  - The existing 13 sanitizer cases and the 4 apostrophe cases must keep their outputs.
- Evidence type: measured result (scratch probe of the DEV-003 `Describe`)
- Verification / measurement plan: the unit cases above, asserting that no backslash and no fragment after the apostrophe survives
- Relationship: residual of ANALYSIS-001, by the ANALYSIS-100 mechanism. DEV-003 did not cause it, and the analysis-002 probe already contained an unquoted `Bob's repo` input without flagging it.

### ANALYSIS-201: Failures interleaved with successes bypass the rate limit: each recovery resets it

- Severity: warning (low likelihood)
- Related task / requirement: 2.1; spec "without one entry per tick"; design decision 4
- Locations: `src/BellTower/IO/RefreshHealth.cs`
  - `:94-100`: `Succeeded` clears `logged` and `lastSuppressedLogged`, logs `tray-refresh-recovered` and writes the record
  - `:46-51` and `:71-76`: the next `Failed` starts a new window, which logs at once and writes the record again
- Observation:
  - The limiter bounds a continuous failure to 9 lines per 10 minutes, but every success that follows a failure resets it.
  - Strict alternation of failure and success on the fake clock: one line per tick (2,400 per 10 minutes, half `tray-refresh-failed: …` and half `tray-refresh-recovered`) and one `refresh-health.json` write per tick.
  - More generally, every failure that follows a success costs two log lines and two atomic record writes. Before this change the tray logged one bare line per failed tick and wrote no record.
  - The overview, title and native-name work catches its own exceptions. So a flap needs an intermittent IO failure in the per-tick drain, reminder or identity reads, such as a sharing violation on a data file.
  - `LocalLog` caps the file near 250 KB, so disk use is bounded, but the log turns over and loses earlier entries.
  - The code follows design decision 4 as written, which logs recovery on the first success and writes the record on each transition.
- Proposed improvement and benefit:
  - Keep the per-reason limiter across a recovery. Log `tray-refresh-recovered` only when the failure it ends was logged.
  - Limit record writes on a failure that starts within the repeat interval after a recovery. Still persist recovery whenever the record shows a failure, so doctor never reports a stale persistent failure.
  - This bounds the output to the same nine lines per 10 minutes for any mix of failures and successes.
- Behavior-preservation risks:
  - Doctor's persistent-failure rule (`failingSince` more than 1 minute old while the tray runs) must stay correct. The recorded window must still end on recovery.
  - A first failure after a long healthy period must still log at once.
- Evidence type: measured result (fake-clock probe)
- Verification / measurement plan: a fake-clock test that alternates failure and success (and 1 failure in 4 ticks) over 100 ticks within 10 minutes. Assert at most 9 log entries and a bounded number of record writes, and that the last record matches the final outcome.
- Relationship: residual of ANALYSIS-002 (the same requirement, bypassed by recovery instead of by changing reasons). DEV-003 did not cause it. Whether it blocks depends on whether design decision 4's per-transition logging is accepted.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-100 | `diff=87172e79b118 untracked=7/5e050d0888dd` | Probe: profile and user name `O'Brien`, in a quoted path, an unquoted path, a non-profile path that contains the name, and bare text. All are masked, with no backslash or name fragment left. The earlier cases keep their output. | verified |
| ANALYSIS-101 | same | Real capture drains for both providers: S then R, S1 and S2 then R, R then S, and a resume after a delayed earlier permission all revive the session, track the stray turns and later prompts, and keep the floor at End. The equal-time boundary, sessions ended by attachment evidence and the new-attachment resume all hold. Checkpoint plus kept journal replays to the stored session. 300 strays cause no extra saves, and quiet drains do not write. | verified |
| ANALYSIS-102 | same | 9, 12 and unique reasons over 30 minutes: 9 lines per 10 minutes (8 reasons and 1 notice). Record writes are bounded. | verified (the interleaved-success residual is ANALYSIS-201) |
| ANALYSIS-103 | same | 100, 130 and 300 queued at the budget: no ended session removed, all compacted, live sessions kept, and captures beyond the batch still queued. The live-only refusal kept 140 of 140 captures and `state.json` unchanged. | verified |
| ANALYSIS-001 (final) | same | The space, UNC, Unix, data-file, profile and user-name cases are masked. An apostrophe in a non-profile path leaks its tail. | still failing (narrow case; ANALYSIS-200) |
| ANALYSIS-002 (final) | same | Any stream of failures is bounded to 9 lines per 10 minutes. Failures interleaved with successes log one entry per tick under strict alternation. | still failing (narrow case; ANALYSIS-201) |
| ANALYSIS-003 (final) | same | (a) The backlog predicate stands, and its regression ANALYSIS-103 is fixed. (b) The floor never passes an applied End, and every revivable resume is tracked (ANALYSIS-101). | verified |

## Assessment and limitations

- **Verdicts:**
  - Verified: ANALYSIS-100, ANALYSIS-101, ANALYSIS-102, ANALYSIS-103 and ANALYSIS-003.
  - Still failing: ANALYSIS-001 and ANALYSIS-002, in the narrow residual cases ANALYSIS-200 and ANALYSIS-201.
- **Regression:**
  - The new fold rule, relief compaction, rule order, marker and eviction introduce no regression in the probes or in the code read.
  - Neither new finding comes from DEV-003; both predate it.
- **Fold rule:** correct for every invariant the assignment lists:
  - delayed earlier events
  - strays
  - a resume before or after strays
  - both reducer paths
  - sessions ended by attachment evidence
  - the equal-time boundary
  - checkpoint consistency
  - bounded growth
  - no save loop
- **Severity order:**
  - ANALYSIS-200 is a content-safety leak of a path tail. It is reachable only through an IO exception that names a path outside the data directory and the profile.
  - ANALYSIS-201 needs a refresh failure that alternates with successes. It follows design decision 4 as written. The orchestrator decides whether that design text or the spec's "without one entry per tick" governs.
- **Not performed:**
  - No repository build, test run or format verification (assignment constraint; QA-003 owns them).
  - No live tray, no real data directory and no installed check.
  - The probes used synthetic names and paths only.
- **Writes:**
  - The client refused the native report write, so this text was returned inline for the orchestrator to save.
  - Probe sources and data are in the session scratch directory, outside the repository.
  - Target identity was unchanged before and after.
- **Decision for the orchestrator:** this was the last permitted fix cycle, so decide whether ANALYSIS-200 and ANALYSIS-201 block or go to the user as follow-ups.
