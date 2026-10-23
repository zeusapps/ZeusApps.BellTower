# Lifecycle and presentation implementation evidence

Baseline: `0a7445a506e5306cca515c251e1fadad38fd99de`.

## Prerequisite interfaces (inspected before implementation)

- `BellTower.Protocol.CaptureEnvelope` carries a normalized `Attachment`, ingress timestamp and derived `Observation`. Capture-v1 keeps causal source evidence and unique receipt IDs; `CaptureConsumer.Prepare` commits through `StateStore.Drain` before capture deletion. The legacy normalized inbox remains consumed alongside it.
- `Attachment` has provider-qualified ownership, agent PID/creation ticks, `HostTarget.Shell` PID/creation ticks, host and managed-title/background flags. `AttachmentStore` persists it with extension-data preservation. Current consumer repairs only a missing agent and replaces the entire host on that path; shell-only repair is missing.
- `AttachmentCapture.Capture` captures ancestor processes and approved environment launch metadata. `LaunchPresentationStore.Prepare` stores verified shell metadata plus palette; `Consume` and capture consumption currently remove this sole durable launch record.
- `StateStore.ReconcileAttachments` currently confirms known agent death only. Null-agent sessions remain working, with no durable unknown grace. `ProcessProbe.IsAlive` treats access-denied as alive; a three-way evidence probe is required to avoid calling uncertainty verification.
- `TerminalTitlePump` starts `sync-title` every overview refresh; cancellation ends only its wait and does not terminate the owned helper. `TerminalTitleService` reloads state/identity and creates a new Git reader in that helper. `AttachedSessionConsole` already enforces PID/start and shared-console membership and must stay isolated.
- `AttachmentLifecycle.Detach` shares the `presentation` mutex with title application. Persisting ended ownership under that lock prevents a queued writer from writing after restoration. A request writer must revalidate attachment identity under the same lock immediately before writing.

Predecessor evidence (repository-relative source): `openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/verification.md`. Its 332 tests, native benchmarks and packaged lifecycle checks are historical evidence; this implementation requires fresh targeted checks.

## Current API documentation

Context7 resolved `.NET` to `/dotnet/docs`; a first process query returned mostly tutorial dataset examples. A second docs query against the official Learn index returned the .NET 10 Process contract: [Process](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process?view=net-10.0). `WaitForExitAsync` cancels the wait; `Kill(false)` terminates only the owned process. The writer runner therefore must kill its owned helper on timeout/cancellation and wait for actual exit before releasing ownership. No new .NET 11 process API is used.

## Verification

Implementation completed for independent review on 2026-10-23. The workspace HEAD advanced externally during work to `64d3e94626b636ca38a3ab0c5b3789686c8fa15b` (repository workflow changes); the original apply baseline remains above. The exact scoped paths are in [implementation-paths.txt](implementation-paths.txt). Pre-existing workflow/planning edits were preserved.

| Command | Result |
| --- | --- |
| `dotnet build -c Release --no-restore` | Passed; zero warnings and errors |
| `dotnet test -c Release --no-build --no-restore` | Passed; 361 tests, zero failed/skipped, 28 seconds |
| `node --test vscode/service.test.js vscode/focus.test.js` | Passed; 8 tests |
| `./scripts/format.ps1 -Verify` | Passed |
| `openspec validate reconcile-session-liveness-and-title-refresh --strict` | Passed |
| `git diff --check` | Passed; Git noted normal LF-to-CRLF normalization for the ingress project |

The runtime is .NET SDK 10.0.113 on Windows. These are development builds and fixtures, not a release installation. No production setup, session mutation, microphone access or physical playback occurred.

## Task coverage and observed behavior

| Tasks | Implementation and executed evidence |
| --- | --- |
| 1.1 | Prerequisite source inventory above; existing capture-v1 and legacy suites remain passing |
| 1.2 | `LaunchPresentationStore.Consume` marks the palette consumed while retaining shell identity; matched `AttachmentHealthTests` verify one-shot palette, durable shell lookup, restarted store and palette retention |
| 2.1 | Both legacy ingestion and capture consumption use `AttachmentStore.Adopt`; matched capture-consumer tests repair null agent/shell without replacing the wait, episode, palette or conversation |
| 2.2–2.3 | Shared `AttachmentHealth` distinguishes verified live, confirmed ended and unverified/unavailable. Tests cover shell-only live versus shell death, inaccessible evidence, actual PID/creation mismatch, quiet live processes, stale unknown across restarted stores, invalid capture rejection, valid repair, explicit resume and bounded grace |
| 2.4 | Session JSON and overview expose health; doctor reports health/ownership counts and grace; matched/mixed tests verify unavailable navigation and attention suppression while waits remain intact |
| 3.1 | A shared serialized asynchronous `CheckoutCache` serves overview, terminal title and Codex native-name pumps. Positive/negative results are cached for five seconds with a 256-directory cap and explicit invalidation. Tests verify freshness, invalidation, concurrent serialized access and native rename ownership while reusing the projected checkout |
| 3.2 | The tray resolves `DesiredTerminalPresentation`; `sync-title --desired` reads a bounded stdin request. The normal helper path does not reload session/identity state or run Git. Native tests temporarily remove the state snapshot and still successfully apply the request. The legacy `--session` entry remains for old callers |
| 3.3 | Failed destination fixture attempts occur at 0, 2, 7, 22, 52, 112 and 172 seconds during a 180-tick soak, with only three failure log entries. Separate native process tests time out/cancel real owned sleeping helpers, confirm their exit and the caller's survival, then successfully launch another owned child |
| 3.4 | Four private hidden-console cases (Claude/Codex × background enabled/disabled) queue a writer behind the presentation mutex, detach under that mutex and verify rejection after release; restored title wins and the disposable provider process remains alive |
| 4.1 | Attachment/host/session/root extension data and unknown host kinds survive; legacy snapshots without health are migrated from real ownership evidence. Atomic `attachmentHealthVersion: 1` travels with committed state; current launcher/update/rollback guards reject incapable consumers before selection changes. Tests verify unchanged state/selection, supported capable invocation, malformed/future requirements, failed commit without a declared requirement and legacy helper invocation |
| 4.2 | Each of the four native console cases executes 100 unchanged refreshes against actual child-launch counters: zero additional Git or writer launches. One identity change produces exactly one additional writer. Matched fixture soaks independently assert bounded retry/log counts |
| 4.3 | User operation, configuration, cache boundary and compatibility documented in `docs/attachment-health.md`, linked from installation/architecture and reflected in compatibility coverage. Build, formatting and full regression suites passed |
| 5.1–5.2 | Matched provider lifecycle/scheduler/capture cases plus mixed simultaneous sessions verify shared policy and independent identities. Both providers use the same grace/defaults/health/diagnostics/writer controls; native-name synchronization keeps its established ownership policy. No hard provider restriction is claimed |

## Compatibility declaration

State schema remains 1 with additive unknown-field preservation. The snapshot atomically commits `attachmentHealthVersion: 1` only when health-aware state is saved. Release metadata and the executable distribution probe advertise `attachmentHealth: 1`; probe validation checks that declared consumer capabilities agree. Current launcher and updater preflight checks inspect bounded primary/backup snapshots before permitting a monitoring consumer. A legacy tray that ignores unavailable semantics is refused through these guarded transitions. Existing normalized event publishers and capture-v1 helpers retain their contracts.

An independently run historical executable/launcher cannot be retroactively guarded. It may preserve the new fields while ignoring their meaning, so it is not a supported monitoring consumer of migrated state. This work does not claim historical-release rollback acceptance. The final integrated release change retains actual 0.1.8 package and installed field acceptance.

## Failed attempts and limits

- The first expanded native-console run passed 26 targeted cases and failed one observation read because the disposable PowerShell console was simultaneously writing `title.txt`. Polling now retries that transient IO conflict within the existing deadline; all four native cases and the full final suite subsequently passed. No production behavior changed for this fixture correction.
- A first full 361-test run passed 360 and failed the new deliberately blocked-commit fixture because Windows returned `UnauthorizedAccessException` where the assertion expected `IOException`. The assertion now accepts both valid filesystem failure categories, as the existing capture commit-failure fixture does. Final full suite passed 361/361.
- A formatting verification before the final apply found whitespace in the newly added capture-repair test. The repository formatter applied it; final verification passed.
- Optional packaging command `./scripts/package.ps1 -OutputDirectory artifacts/liveness/releases` failed during locked restore with NU1900 because sandbox networking could not reach NuGet vulnerability data. The required escalation rerun did not return from approval and was interrupted after approximately 338 seconds; no process/session ID or completed package was returned. Auditing was not disabled. Packaging/installed update acceptance is unverified here and remains in the final release change; the required checks for this change passed.
- Native console tests exercise actual Windows console APIs and the built desired-value helper in private hidden consoles. Provider labels identify matched shared-adapter scenarios, not authenticated Claude/Codex sessions. Authenticated provider, real user-terminal, VS Code and RDP acceptance remain explicitly separate integrated-release evidence. Fixture success does not stand in for those trials.

Independent QA and code analysis have not yet been applied to this target. No task checkbox or reviewer finding was changed by the developer.

## Required corrections after independent review round 001

The original target above is historical. [QA round 001](../reports/qa-001.md) and [analysis round 001](../reports/analysis-001.md) found two required corrections, assigned by the orchestrator as ANALYSIS-001 and ANALYSIS-002. Both are implemented below and await independent round-002 verification; the developer has not closed findings.

- **ANALYSIS-001:** invalidate `Destination.Successful` immediately before an attempted write, since false/exception/timeout may follow a partial mutation. Four matched provider tests cover success A → B visibly mutates then returns false or throws IO → desired A restores → unchanged successful A deduplicates. The test also checks failure backoff, an independent other-provider destination's receipt, wait episode and palette preservation.
- **ANALYSIS-002:** accepted explicit same-ID replacement now adopts host kind, window process, handle and terminal session from the captured generation together, clearing absent values. A missing new shell cannot borrow the old generation's launch record. Unknown old/new host extension fields are merged, with incoming values winning collisions; durable attachment extensions and title/background opt-ins remain. Four matched provider tests use a real newly owned hidden process and synthetic host destinations to verify cross-host replacement, missing destination clearing, extension/opt-in retention and stale/conflicting non-resume rejection. They do not perform focus or broaden ordinary resume behavior.

Changed paths in this correction batch (the existing 58-path manifest is unchanged):

```text
docs/attachment-health.md
openspec/changes/reconcile-session-liveness-and-title-refresh/evidence/implementation.md
src/BellTower/Hosts/TerminalTitlePump.cs
src/BellTower/IO/AttachmentStore.cs
tests/BellTower.Tests/Hosts/PresentationSchedulerTests.cs
tests/BellTower.Tests/IO/AttachmentHealthTests.cs
```

Required build initially surfaced NU1900 in assets left by the earlier optional package's failed audited restore. `dotnet build -c Release` outside the sandbox refreshed those assets with normal auditing and network access; auditing was not disabled and packaging was not retried. A new assertion initially triggered xUnit2031 and was corrected to use its predicate overload. The subsequent Release build passed with zero warnings/errors.

Focused affected regressions passed: `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~AttachmentHealthTests|FullyQualifiedName~PresentationSchedulerTests|FullyQualifiedName~ConsoleTitleProcessTests|FullyQualifiedName~AttachmentHealthCompatibilityTests|FullyQualifiedName~CodexNameTests'` — 47 passed, zero failed/skipped, 15 seconds. This includes the eight new cases, existing lifecycle/scheduler/native-console/native-cleanup checks, compatibility and native-name regressions. The earlier full 361-test pass remains historical coverage of unaffected behavior; no new full-suite pass is claimed for this bounded correction batch. Final formatter/build/diff verification results follow below.

Final correction verification: post-format `dotnet build -c Release --no-restore` passed with zero warnings/errors; `./scripts/format.ps1 -Verify` and `git diff --check` passed. Both fixes are ready for independent verification, and implementation writes are paused.

## Archive relocation preparation

The maintained operational helper now resolves only its known active or dated-archive layout. Mirrored private recovery-tool copies retain their own nearby API root; no upward repository/DLL search is performed. The already frozen live recovery helper and its DLLs were not read or changed. PowerShell syntax and isolated resolver fixtures passed for active, dated-archive and mirrored-private layouts, including distinct API-location markers; an unsupported layout was rejected. Only the resolver fragment ran, with its automatic script-location input mocked: no DLL loading or operational function executed. `git diff --check` passed for the two assigned evidence files. The predecessor reference above is repository-relative plain text so moving this change does not create an `archive/archive` link.
