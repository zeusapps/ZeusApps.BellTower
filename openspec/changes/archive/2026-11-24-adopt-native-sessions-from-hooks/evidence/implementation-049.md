# Implementation 049 (fix cycle 3, ANALYSIS-3301 partial-failure residual)

Change: `src/BellTower/Hosts/TerminalTitlePump.cs`, `RestoreUnpresentedAsync`. After each `writer.RestoreAsync` call, whatever it returns, the pump re-lists the restore store and forgets the receipts of sessions presented in every shell whose record of that owner is gone. Receipts for shells whose record remains stay. The 30 s retry and the `title-restore-unavailable` log on failure are unchanged. No change to shell identity or reuse checks, `TerminalTitleService` or `TitleRestoreStore`.

Test: `APartialRestoreFailureRewritesTheSurvivorInTheRestoredShellOnlyAndRetriesTheFailedShell` in `tests/BellTower.Tests/Hosts/NativePresentationTests.cs`. Two shells (the test process and a helper cmd), survivors in each, an ended attachment owning both records, restore failing only on the second shell. Matched cases: claude/codex, codex/claude, claude/claude, codex/codex (interactive-client and embedded-runtime). Asserts: the restored shell's survivor writes again and takes the restored title as prior; the failed shell's survivor is not rewritten and its record is kept; no retry inside 30 s; after 31 s the failed shell is restored and its survivor writes again.

Mutant: restoring the `done`-gated forgetting (`restored = done ? owner : []`) failed all 4 new cases (34 other NativePresentation tests passed). Reverted.

Results: Release build `--no-incremental` 0 warnings, 0 errors. Hosts/NativePresentation/MixedLaunch filter 239 passed. Full suite result reported in the return block.
