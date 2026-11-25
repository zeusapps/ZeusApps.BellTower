# Implementation evidence 001 (DEV-001)

Baseline 605a513. Tasks 1.1, 2.1, 2.2, 3.1 implemented; 4.1 build and test checks run. Tasks are not ticked by the developer.

## Decisions the artifacts left open

- **Registering sessions count as live.** A session's identity is created at hook ingest, before the monitoring state lists it, so state alone would let two sessions registering in the same drain share a color. In addition to sessions not ended in the state snapshot, an identity file written in the last hour whose key the snapshot does not list counts as in use (a pending registration). Ended sessions in the state, and older unlisted identity files, never count. The concurrency scenario and the "concurrent allocations under the lock" test depend on this.
- **"Uses" means within OKLab 0.04.** `Palette.SameColorDistance` is 0.04; adjacent series hues are about 0.057 apart. A custom hex accent therefore blocks the series color it is near, and is otherwise counted by distance in the farthest-color fallback.
- **Own color excluded by counting it.** `AllocatePalette(key)` always includes the session's own saved accent, so reset and random color never return it (while a free color exists, the farthest rule also never prefers it).
- **State read without the state lock.** Snapshots are replaced atomically; taking the state lock under the identity lock could invert the lock order.
- **Series constants.** Hue 25 + 30 k degrees, accent lightness 0.56 chroma 0.11, background lightness 0.22 chroma 0.035, order of steps 0, 6, 3, 9, 1, 7, 4, 10, 2, 8, 5, 11. Accent to foreground contrast is 4.6:1 or better; background to the terminal text (#f1f5f9) is 15.6:1 or better. Tests re-derive the hex table from the OKLCH parameters.
- `Palette.Create()` is kept for tests and returns a random series entry. The `IdentityStore` candidate-injection constructor parameter was removed (only the replaced test used it).

## Changed

Source: `src/BellTower/Core/Palette.cs`, `src/BellTower/IO/IdentityStore.cs`, comments in `src/BellTower/Core/IdentityControls.cs`. `LaunchPresentationStore` already allocates through `IdentityStore.AllocatePalette`, so managed launches, native adoption (identity creation), reset and random color share the one path with no change there. Docs: `docs/architecture.md`, `docs/session-identity.md`, `docs/installation.md`; `docs/compatibility.md` and the capability matrix text stay correct for both providers.

Tests: new `tests/BellTower.Tests/Core/PaletteSeriesTests.cs` and `tests/BellTower.Tests/IO/LiveSessionColorAllocationTests.cs`; updated `tests/BellTower.Tests/IO/PaletteAllocationTests.cs` (metric thresholds, series membership, candidate-hook test replaced by real concurrent creation).

## Checks (isolated worktree, disposable data directories)

- `dotnet restore --locked-mode`, then `dotnet build -c Release --no-restore`: 0 warnings, 0 errors.
- Focused palette, series, allocation and policy tests: passed (23 in the three palette classes).
- Full suite: 1856 total, 1840 passed, 16 failed:
  - 11 `HookTrayStartTests` (known: need an ordinary start outside the test host's job).
  - 1 `WindowsUserIdleSourceTests.PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation` (known idle-source flake).
  - 4 `CodexNameTests` (`NativeRenamePausesWritesUntilExplicitResume`, `NativeNamesFollowIdentityAndDuplicateWritesAreSuppressed`, `ResumedThreadRetainsNativeOwnershipAcrossRuntimeRestart`, `ParallelRuntimesAndThreadIdsStayIsolated`): deterministic here, caused by running inside a git worktree. The resolved name gains a ` | worktree: <name>` suffix (for example expected "First", actual "First | worktree: ..."). Unrelated to colors; not re-run on a plain checkout, so recorded as an environment effect, not as proven pre-existing.
- `dotnet format --verify-no-changes` on the changed C# files: passed.
- `openspec validate distinct-session-colors --strict`: valid.

## Not performed

Task 4.3 (installed multi-session check with the user) is outside this assignment.
