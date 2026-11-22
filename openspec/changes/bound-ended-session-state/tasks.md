# Tasks

## 1. Bounded state

- [x] 1.1 Compact settled ended sessions in the shared drain before save (design decision 1). Verify with unit tests for Claude Code and Codex fixtures: after the settle interval, an ended session has no completed or open operations, its journal up to its last applied observation is folded, the floor is at the latest folded observation, and only later ignored observations remain, against a checkpoint copy (otherwise the journal is empty and the checkpoint null; design decision 1 as revised 2026-11-21). Its displayed fields are unchanged. A session within the interval is not compacted. A live session is never compacted. The existing shuffled-delivery and delayed-registration tests still pass.
- [x] 1.2 Verify reducer behavior after compaction for both providers: a causally earlier event leaves the session ended and raises no attention; a later resume registration makes it live, and its following events are ordered and tracked normally, including an approval wait and its resolution.
- [x] 1.3 Cap retained ended sessions at 200, oldest `LastObserved` first, keeping the 30-day age rule (design decision 2). Verify with a test of more than 200 mixed-provider ended sessions plus live sessions: exactly the oldest extra ended sessions are removed and every live session remains.
- [x] 1.4 Add the budget fallback to the save path (design decision 3). Verify with tests using a reduced test budget: an over-budget state with ended sessions saves within the budget after removing the oldest ended sessions in a bounded number of encodes; a state whose live sessions alone exceed the budget still throws, and its captures stay queued.
- [x] 1.5 Verify recovery of existing state: a fixture shaped like the field failure (many ended sessions of both providers carrying full journals, near the budget, with queued captures for both providers) is drained in one call, saved within the budget, and all queued captures are consumed and deleted.

## 2. Visibility

- [x] 2.1 Replace the bare refresh log with a reasoned, rate-limited entry and a recovery entry, and write `refresh-health.json` on each transition (design decision 4). Verify with tests: repeated identical failures produce one entry per 10 minutes, a changed reason logs immediately, success after failure logs a recovery and updates the record, and the message contains no data-directory path.
- [x] 2.2 Add the doctor `state` section (design decision 5). Verify with doctor tests: size, budget, live and ended counts, and a persistent refresh failure while the tray is running are reported. With no state file or no health record, doctor reports this honestly and does not fail.
- [x] 2.3 Update the documentation that describes state retention and diagnostics. Verify that it matches the implemented settle interval, cap and doctor fields.

## 3. Acceptance

- [x] 3.1 Run the Release build, the full .NET test suite, format verification and `openspec validate bound-ended-session-state --strict`, and record the results.
- [x] 3.2 One QA review and one code-analyzer review of the stable implementation target. Resolve blocking findings and record suggestions as deferred.
- [ ] 3.3 Installed check on the maintainer's dev build (user-authorized install): the tray processes the queued Claude Code and Codex captures and writes presence, `state.json` drops well below the budget, doctor reports healthy state, and the log stops repeating refresh failures. Record the result, or Not performed.
