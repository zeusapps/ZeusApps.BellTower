# Implementation evidence 003 (DEV-003, fix cycle 2)

Baseline 605a513, same worktree. `design.md` and `specs/session-identity/spec.md` were copied from the main checkout first. Fixes: ANALYSIS-100 and ANALYSIS-101. Both await independent verification; neither is closed here. Nothing else changed.

## ANALYSIS-100: consume the launch and write the identity as one step

- `IdentityStore.Get` takes an optional `launchPalette` provider. It is called under the identity lock, only on the branch that creates the identity, after an explicit `initialPalette` and before `AllocatePalette`.
- `HookIngestor` and `CaptureConsumer` pass `() => LaunchPresentationStore.Consume(...)` instead of consuming before `Get`. The lock order is identity, then launch (state, identity, launch under `CaptureConsumer`), the same as `LaunchPresentationStore.Prepare`.
- `Consume` no longer runs when the identity already exists (a resumed conversation).

### Effect of changing when an unused launch is marked consumed

Not entirely invisible, so recorded rather than asserted away:

- Before: every dedicated hook consumed the launch, including the first hook of a resumed conversation, which did not adopt it.
- Now: a resumed conversation leaves the launch unconsumed. It stays unconsumed until `Detach` removes it when the agent exits. Two consequences, both limited to the managed shell's lifetime:
  - While its shell is live, the unconsumed launch keeps reserving its (already painted) color in allocation, up to its 24-hour validity. This is the safe direction (a color skipped, never shared).
  - If a second, new conversation starts in the same managed shell after a resume (for example after `/clear`), its first hook now adopts the launch palette, where before it allocated a fresh color. Nothing else reads `PaletteConsumed`.
- If this behavior is not wanted, the alternative is to also mark the launch consumed (discarding the palette) when the identity exists. That is the opposite of the instruction not to run `Consume` for an existing identity, so it was not done.

### Tests (`LaunchConsumptionAllocationTests`, claude and codex)

Deterministic interleaving: the test thread holds the identity lock, the consuming step runs on another thread, the test waits up to 1.5 s for the launch to be marked consumed, then allocates for a second session from the test thread, then releases the lock.

- `ADrainedCaptureConsumesTheLaunchUnderTheIdentityLock...` (the `CaptureConsumer` path through `StateStore.Drain`): the identity gets the launch palette, the second session gets a different color, the launch ends consumed.
- `AHookConsumesTheLaunchUnderTheIdentityLock...` (the `HookIngestor` path): same assertions.
- `AnExistingIdentityLeavesTheLaunchUnconsumed...`: a resumed conversation keeps its saved palette and the launch stays unconsumed.
- Not run against the pre-fix code, so I did not observe the collision failing there; the assertions rely on the launch being marked consumed before the identity lock is requested, which is the pre-fix order.

## ANALYSIS-101: reset and random never return the own color

- `Palette.Allocate(inUse, exclude)`: entries within `SameColorDistance` of `exclude` are skipped in both the free-entry branch and the fallback.
- `AllocatePalette` passes the session's own saved accent (null for a new session, so creation is unchanged).
- Tests (`ResetAndRandomNeverReturnTheOwnColorWhenTheWholeSeriesIsInUse`, claude and codex): 12 live sessions on the full series with the session on `Series[0]`, and 13 live sessions with the session alone on `Series[1]`; both random color and reset return a different color. `AllocationNeverReturnsAnExcludedColorEvenWhenItIsTheLeastUsed` covers `Allocate` directly.

## Checks

- `dotnet build -c Release --no-restore`: 0 warnings, 0 errors.
- Palette, identity, launch, `HookIngestor` and `CaptureConsumer` classes: see the full-suite result below (the new class passed 9/9 on its own).
- `dotnet format --verify-no-changes` on the changed C# files: passed.
- `openspec validate distinct-session-colors --strict`: valid.
- Full suite (one complete run; the earlier run was interrupted by a usage limit and discarded): 1876 total, 1860 passed, 16 failed:
  - 11 `HookTrayStartTests` (known: need an ordinary start outside the test host's job).
  - 4 `CodexNameTests` (fail only inside a git worktree).
  - 1 `CodexSharedRuntimeServiceTests.AStalledSharedBackendBacksOffThePermissionPumpForTheWholeHome` (known Codex shared-runtime flake; not rerun in isolation).
  - The new `LaunchConsumptionAllocationTests` and the palette, identity, launch, `HookIngestor` and `CaptureConsumer` tests all passed.
- Not performed: task 4.3 (installed multi-session check with the user).
