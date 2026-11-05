# Implementation 041: task 3.4 capability matrix reconciliation and observer content discard (DEV-041)

Awaiting independent verification. Baseline 0b11026. No native check (no provider launch).

## Reconciliation result

The repository already kept one capability source: `CapabilityMatrix.Rows` (printed by `bell-tower doctor` as `capabilityMatrix`, mirrored in `docs/compatibility.md`, guarded by `CapabilityMatrixTests`). It was written before the shared-backend observer and still said native names were reachable for "managed runtimes only". This task keeps that source and extends it instead of adding a second matrix.

- New status `not applicable` (for a mode the provider does not have, with the reason), next to supported, restricted and unverified. It is required by the change's own task 5.3 wording for the Claude cross-check and keeps Claude from being labelled unverified for a mode it cannot have.
- Six new rows (per-mode): Codex owned runtime (supported), Codex shared backend observer (supported), direct clients (Codex unverified: no access path, not a proven restriction; Claude unverified because its native-title write is unfinished and owned by `sync-native-session-titles`), shared conversations not yet reached (unverified, task 4.2: adopted after the launcher exited, other Codex home), refused shared threads and backends (unverified: fork and delegated threads refused by the conservative policy pending the task 6.1 probe; elevated backend refused by the non-elevated scope decision), observer read content handling (supported).
- Stale text corrected: native-title history recovery, native chat-name write and the human-wait-without-runtime row no longer say shared backends are unreachable; doctor `codex.nameSynchronization` (was "existing/remote sessions ... remain unavailable") now reports `CapabilityMatrix.CodexNameModes`, which interpolates the `CodexAccessStatus` labels, so doctor, matrix and the status shown to the user share one source. `codex.attachment` mentions that a shared backend is observed without ownership and carries no terminal binding until 4.2.
- Claude native-title synchronization stays `unverified (unfinished)`, owned by `sync-native-session-titles`, with "not a provider restriction" in the matrix, doctor (`claude.nameSynchronization`, unchanged), `NativeNamePolicy.ClaudeWriteStatus` and docs. That change has no proposal and no evidence of a hard restriction (its README), so nothing is called restricted or complete. No file of that change was touched.
- No product policy was added or duplicated: only descriptive rows, one constant and docs. Selection, pause, refusal and discard behavior is the existing shared code (`CodexRuntimeOpener`, `CodexNameService`, `CodexPermissionPump`, `CodexAccessStatus`, `CodexThreadIdentity`).

Docs updated: `docs/compatibility.md` (legend, per-mode rows, rows renamed to match the source, closing-evidence entry for the shared-backend gaps), `docs/session-identity.md` (native chat names, Codex rollout line), `docs/session-titles.md` (existing-sessions paragraph). `docs/architecture.md` already described the shared observer path accurately (evidence 034 to 040) and is unchanged.

## Tests added (all in the existing files)

- `CapabilityMatrixTests`: `PerModeNativeNameAndStatusStatesAreHonestForBothProviders` (statuses, task references, Claude unfinished and never restricted, not-applicable cases, doctor text constants); `DoctorReportsTheSamePerModeCodexNameText`; `EveryAccessRefusalLabelNamesAnUnavailableReasonAndNeverClaimsRestrictionOrCompletion` (every `CodexTransportFailure`, distinct labels, no "restrict", whole-word "supported" or "complete"); `SupportedModeRowsNameBackingTestsThatExist` (each supported mode row cites test classes and methods that exist by reflection); `CompatibilityDocumentStatesMatchTheMatrixSource` (every row present in `docs/compatibility.md` with the same first status word for both providers). The existing restriction-wording test now allows only the negated phrases.
- `CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens` (owned and shared): stand-in thread responses carry a preview, turn items, messages, an access token and an authorization header with canary values, for success, native-title pause, fork refusal and id mismatch, through the name service and the permission pump. Asserts the outcomes (synchronized, paused, `fork or delegated thread mapping unverified` on shared only, mismatch), no resume or turn method, then that no canary appears in any file under the data directory (local log, identity, attachment, event and runtime stores), in the serialized identities and sessions, or in the status text. It also asserts the log really recorded the failure code, so the scan is not vacuous.

## Mutants (each killed, then restored byte-for-byte)

1. Name read returns the whole thread JSON when it has no name: `ObserverReadsDiscardPreviewsTurnsAndTokens` (both cases) and three name-service tests failed.
2. `docs/compatibility.md` shared-backend Codex cell changed to unverified: `CompatibilityDocumentStatesMatchTheMatrixSource` failed.
3. Doctor Codex name text reverted to a literal: `DoctorReportsTheSamePerModeCodexNameText` failed.
4. `ElevatedPeer` label rewritten as a restriction: the refusal-label test failed.
5. Codex direct row changed to `restricted` without proof: four matrix tests failed (including the per-mode, doc and proof/fallback/regression checks).
6. A cited backing method renamed: `SupportedModeRowsNameBackingTestsThatExist` failed (a first attempt survived because the citation had no class prefix; fixed by citing `Class.Method`).

## Checks

See the hand-back for command results and counts.

## Limitations and proposals for the orchestrator

- The Claude column of the new content-handling row is `not applicable` (no observer runtime read). Claude transcript history recovery is not covered by the discard test; if a content-discard claim for Claude history reads is wanted, it needs its own test and row (not claimed here).
- The doc agreement test compares the first status word of a cell, not the prose; the prose can still drift in detail.
- Proposal (no change made): task 5.2 could cite `CapabilityMatrix.CodexNameModes` as the per-provider native-name diagnostic so the doctor surface is not rebuilt; the per-mode rows already provide a place for the 5.2 version-table and navigation-precision states.
- Task 3.4's checkbox is left to the orchestrator.
