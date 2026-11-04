# Implementation 027: fix cycle 2 for DEV-025/026 (tasks 2.3 and 2.4)

Developer assignment DEV-027. Baseline `683ad12`, uncommitted working tree. Fixes awaiting independent verification; nothing here closes a finding.

## Per-finding fix and test

| Finding | Fix (`src/BellTower/Core/ConversationLifecycle.cs`, `ReduceTerminal`) | Test (`ConversationLifecycleTests.cs`) |
| --- | --- | --- |
| ANALYSIS-1300 | Non-late facts are the current binding observation. With facts, the owner is `facts.OwnerClient` with no fallback to the stored client (no owner clears it). The shell is replaced together with the owner (same non-null owner keeps the stored shell unless the facts name one). Only a confirmed `Ended` owner overrides the outcome (`owner-exited`). Otherwise the reported outcome and reason are stored; a reported `connected` whose owner is unreadable or missing is capped to `unverified` / `owner-unverifiable`. With no facts (or late facts) the stored-owner death check, aging and evidence-shell check are unchanged. | P1 `AMissingOriginOutcomeWithoutAnOwnerIsStoredForANeverBoundConversation`; P2 `FreshFactsWithoutAnOwnerReplaceAStaleOwnerExitedClaim`; P3 `AnUnreadableOwnerDoesNotBlockAFreshDisconnectOutcome`; P4 `ANewUnreadableOwnerTakesItsShellAndIsCappedToUnverified`. Existing owner, shell, late-fact, restart and reused-PID tests still pass. |
| ANALYSIS-1301 | Test only. `StaleRuntimeEvidenceIsNotWork` now evaluates a future active observation (1 h) -> `unavailable`, evidence time unchanged, and a within-skew observation (+10 s) -> `unverified` with `UnverifiedSince == now` (clamped). | `StaleRuntimeEvidenceIsNotWork` |
| QA-2100 | Test only. A shared record with `EndedAt` set (older tray) given fresh disconnect facts and active work stays `confirmed-ended`, keeps `EndedAt` and its evidence. | `ASharedRecordWithEndedAtStaysEndedAndIsNotRevivedByFactsOrHooks` |

## Mutation checks (one change, run `ConversationLifecycleTests`, restored afterwards)

| Mutation | Result |
| --- | --- |
| restore `?? stored.Client` owner fallback | killed by `FreshFactsWithoutAnOwnerReplaceAStaleOwnerExitedClaim` |
| future rejection removed (`active.ObservedAt <= now + ClockSkew`) | killed by `StaleRuntimeEvidenceIsNotWork` |
| clamp to now removed | killed by `StaleRuntimeEvidenceIsNotWork` |
| `EndedAt is null` gate removed in `Core/AttachmentHealth.cs` | killed by `ASharedRecordWithEndedAtStays...` |

All four failed exactly one test; originals restored (full build and suite run afterwards). `Core/AttachmentHealth.cs` was touched only for the temporary EndedAt mutation and is unchanged.

## Commands and results

- `dotnet build -c Release --no-restore -nodeReuse:false` -> 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore` (nothing else of mine running) -> 1264 passed, 0 failed, 2 m 53 s (32 in `ConversationLifecycleTests`, up from 27).
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` -> exit 0.
- `dotnet build-server shutdown` run; no `dotnet` or `testhost` process of this session remains. No real `claude` or `codex` and no model service was contacted.

## Review identity

The identity includes this file, so it cannot contain its own hash. The summary line is given in the developer handback, computed over the 13 assigned paths after this text was final.
