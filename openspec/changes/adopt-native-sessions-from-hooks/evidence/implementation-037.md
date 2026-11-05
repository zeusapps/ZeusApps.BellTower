# Implementation 037: fix cycle 1 for DEV-036 (task 3.2)

Developer DEV-037. implementation-036 stays as written history; this file corrects it where noted. Fixes await independent verification.

## ANALYSIS-2400: thread identity proof and the fork/agent refusal

Orchestrator decision applied: forks and delegated threads stay refused on unowned (shared) runtimes as an explicitly conservative policy until the task 6.1 fork hook probe shows which session id a fork's or delegated thread's hook carries.

What is actually proven on a shared backend: the returned thread has exactly the requested id, and the backend was verified as the socket's peer process (live, `codex` image, shared-backend command line, not elevated). That shows reads and writes go to the thread Bell Tower named. It does not prove that the hook's session id names the conversation the hook came from for a fork or delegated thread; that mapping is unobserved.

Corrections to implementation-036 wording: its statement that the thread read "must prove" the hook's own root conversation, and the rationale that a wrong mapping "would write another conversation's name", overstate the proof. The refusal does not detect a wrong mapping (a fork whose hook carried the parent's id would return the parent and pass). The refusal is a policy.

Changes:
- New failure kind `ThreadUnverified` (fork, parent, agent role/nickname, or other-session fields) distinct from `ThreadUnmapped` (no thread, no id, or id differs: a true mismatch).
- Status texts: `unavailable: fork or delegated thread mapping unverified` and `unavailable: runtime thread does not match the conversation`. Nothing is written in either case.
- `sessionId == id` check kept, now reported as unverified: evidence 011 shows forks also report `sessionId == id`, so it cannot distinguish a fork from a root, but it is a cheap conservative refusal of a thread that reports another session tree. It proves nothing and is documented as such.
- Code comments in `CodexThreadIdentity` and `docs/architecture.md` (shared-backend paragraph) state the real proof and the policy.
- Tests: `AThreadThatIsNotTheRequestedRootIsRefusedOnASharedBackendWithTheRightKind` (theory, six threads, kind per case), `ForkedOrDelegatedThreadIsRefusedByPolicyWithAnUnverifiedStatusAndNothingIsWritten`, `ADifferentThreadIdIsAMismatchWithItsOwnStatusAndNothingIsWritten` (exact status texts, no `thread/name/set`).

## ANALYSIS-2401: concurrent failed opens multiplied the backoff

`CodexSharedAccess.OpenAsync` records the home's failure count when an attempt starts and, on failure, calls `Failed` under the gate only if the count is unchanged. Overlapping attempts failing in one backoff window therefore count once; a success resets as before. Single-flight was not chosen, so each caller keeps its own connection.

Test `ConcurrentFailedOpensCountOneBackoffFailureAndALaterSuccessResets`: four `Task.Run` opens through a stand-in that never answers the handshake, frozen clock; failure count 1; next open inside the window is `BackedOff` with count still 1; after advancing 3 s with the backend answering, the open succeeds and the count is 0.

## Other corrections

- ANALYSIS-2403: implementation-036's statement that a conversation of another Codex home gets `HomeMismatch` is wrong. Such a conversation is read through the tray-home backend (the opener uses the resolved Codex home of the tray, not the session's) and gets `Rejected` or "not running". The label itself is folded into a later task.
- ANALYSIS-2404 (gap recorded, not implemented): a shared conversation whose first hook arrives after the launching client exited is stored with unknown mode and gets no shared access until task 4.2 moves the selection gate off the stored mode.
- QA-3100: the origin of DEV-036's single denied provider CONNECT is undetermined; QA-031 did not reproduce it in seven samples.
- Not done (folded into later tasks): ANALYSIS-2402, 2403 label changes, 2404 gate move, 2405 negative cache, 2406 test hygiene.

## Parity

Unchanged from DEV-036: Codex-only shared path (design decision 4); Claude behavior untouched.
