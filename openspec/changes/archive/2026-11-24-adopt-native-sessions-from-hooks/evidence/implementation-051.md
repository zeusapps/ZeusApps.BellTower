# Implementation 051 (native presentation on by default)

AppConfig.NativePresentation defaults to true; the JSON writer keeps an explicit false, so a config without the value reads on, a saved false stays off, and saving an unrelated setting keeps the state. ConfigStore unchanged. The tray checkbox shows config.NativePresentation (effective) and a click saves an explicit value. Texts updated: tray tooltip, AppConfig doc, DiagnosticsReporter, CapabilityMatrix row, docs/installation.md, docs/compatibility.md, docs/attachment-health.md; plain default codex on a shared backend stays excluded.

Tests (NativePresentationTests): default-on and explicit round trip with unrelated save; config file without the value; per-provider (claude interactive-client, codex embedded) default presentation, saved false untouched, shared backend nothing. Existing off-assuming tests (NativePresentationTests off/pump, TerminalTabSelectionTests, NativeAdoptionReconcileTests nested client) now set the setting off explicitly; assertions unchanged. Mutant (default false) failed 5 new tests, reverted.

Results: Release --no-incremental 0 warnings 0 errors; focused filters pass; full suite 1607/1609, the two failures (AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor, CodexSharedAccessTests.FailuresAfterTheHandshake...) are load-only and pass alone. format.ps1 -Verify checked below.
