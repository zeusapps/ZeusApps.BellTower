# Implementation evidence 062 (DEV-062, ANALYSIS-4900)

- Fix: `ProviderTitleState.ReadRecords` now also treats `InvalidDataException` (oversize file from `AtomicFile`, `null` document from `Json`) as an empty record. `ObserveBoth`, `DisabledFor` and `Report` no longer throw for it; the next present sighting rewrites a valid record. Conservative: an unreadable record can only withhold a title.
- Tests: `AMalformedRecordFileIsEmptyAndTheNextSightingRewritesIt` for claude and codex, with a `null` document and a 17 MiB oversize stand-in.
- Correction to implementation-061.md: the helpers `sync-title` and `focus-terminal` do not use "the profile the provider in that terminal uses"; they inherit the tray's environment. That earlier evidence is left unedited.
