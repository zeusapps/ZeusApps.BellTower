# Implementation evidence DEV-047 (fix cycle 1: ANALYSIS-3300 code part, ANALYSIS-3301)

Baseline `1c80a7d`, on the uncommitted DEV-045 and DEV-046 target. Awaiting independent verification.

## ANALYSIS-3300 (code and docs wording)
`NativePresentation.GrantsTitle` is renamed `GrantsPresentation` in all callers and tests, because it grants the title and the palette background. The `AppConfig.NativePresentation` summary, the `GrantsPresentation` and `Effective` summaries, the test class summary and the test comment that said the background was never granted now say title and background. No other title-only wording for this setting was found in code or docs (the docs already describe title and color).

## ANALYSIS-3301 (shell-keyed record)
- `TitleRestoreStore` keeps one record per shell identity (process identifier plus creation time; file name from that pair) with `Previous`, `Written`, `Colored` and the owning attachment (the last writer). `Get(shell)`, `ForAttachment(id)`, `List`, `Save`, `Remove(shell)`.
- `WriteNative` reuses the shell's record `Previous` or reads the title once, keeps `Colored` true across title-only rewrites, and makes the writing attachment the owner.
- `Restore(attachmentId)` finds the record that attachment owns; no record means nothing to restore. A gone or reused shell discards the record without a write; a failed title restore or color reset keeps it for retry. The inheritance scan and the scan over every attachment are removed.
- The pump restores a record when its owner is not presented. After a restore it forgets the receipts of any presented session in the same shell, so a surviving session writes again (prior title read afresh from the restored console).
- `sync-title --restore` still takes an attachment ID.
- Earlier attachment-keyed dev-build records lived in the `title-restore` folder. The new store uses `shell-title-restore` and never reads, migrates or deletes the old folder (unreleased records, left to be removed by hand; they cannot cause a write).
- Docs: `docs/attachment-health.md` updated for the per-shell record.

Tests: `SwitchingOffTwoLiveSessionsInOneShellRestoresThePriorTitleOnceWhicheverWroteLast` (both write orders, claude and codex --no-daemon: one title restore, one color reset, prior title back, record removed), `AShellRecordIsKeyedByShellIdentityAndNeverByAttachment`, and the updated second-session test. Mutant: making `WriteNative` ignore the existing shell record failed 5 tests; reverted.
