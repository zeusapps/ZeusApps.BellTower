# Implementation 007: confirm-claude-human-permission-waits (candidate-scope correction)

Developer handoff against baseline `754aed5`. Nothing is committed. This corrects canonical finding `ANALYSIS-002`; it remains open for independent review. All regression coverage uses fake runtime clients and isolated test data. No live provider session, installed app, audio, user setting or default data directory was touched.

## Correction

`WaitState.ConfirmationScopeId` now combines the stable opaque wait episode identity with a hash of its current candidate membership. Candidate IDs are sorted before hashing, so order changes preserve scope while A+B narrowing to B changes it. Legacy waits without episodes use an opaque hash of normalized wait identity. Raw operation IDs, fingerprints and candidate values are not included in the emitted scope string.

The existing `ApprovalRevision` comparison now observes this candidate-inclusive scope. A partial resume invalidates both persisted runtime observations and transient cached status even though the wait episode remains. The pump captures the already-opaque candidate-inclusive scope IDs at query start and emits those exact captured values after the asynchronous status read; later changes to the caller's wait state cannot broaden the result.

Added a unit test for candidate-order canonicalization, narrowed-set distinction and metadata privacy. Added a real `CodexPermissionPump` + `StateStore` delayed-client regression on causal and unordered paths: start a no-ID wait for candidates A+B, delay a positive response, resume A so the wait narrows to B, reject old transient and persisted evidence, then confirm B from a fresh query. Existing named-wait P1/P2, A→A+B→A, fresh-query recovery and idempotent replay tests remain in the focused run.

## Changed paths owned by this correction

- `src/BellTower/Core/WaitState.cs`
- `src/BellTower/Sessions/CodexPermissionPump.cs`
- `tests/BellTower.Tests/Core/HumanWaitCorrectionTests.cs`
- `tests/BellTower.Tests/Providers/CodexPermissionTests.cs`
- `docs/architecture.md`
- `docs/hook-ingress.md`

## Checks

- `dotnet build -c Release --no-restore` — passed, zero warnings and errors.
- Targeted `CodexPermissionTests`, `HumanWaitCorrectionTests`, `CaptureTests` — passed, 98/98.
- Scoped `dotnet format ZeusApps.BellTower.slnx --no-restore --include ...` — passed for the changed C# files.
- `git diff --check` — passed; Git printed its existing LF-to-CRLF notice for `docs/follow-ups.md`.
- Full suite — not run by developer; QA owns full-suite execution.
- Native/live approval flow — not run.

## Review identity

`./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`

`head=754aed5 baseline=754aed5 files=62 diff=518bc5fdeb23 untracked=15/c0940d0a9277`

The identity covers concurrent in-flight changes as well as this correction. The orchestrator should freeze and circulate the final target before independent review.
