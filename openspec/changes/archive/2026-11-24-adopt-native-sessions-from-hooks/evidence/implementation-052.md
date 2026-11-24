# Implementation 052 (fix cycle 1 for DEV-051)

ANALYSIS-3800: `NativeAdoption.Reconcile`, nested branch. When a nested client (it inherited a managed attachment identifier, its ancestry shell) shares the shell of the live recorded managed launch, its host keeps the terminal window but drops the shell (Shell and ShellSource null). `NativePresentation.IsAdoptedDirect` needs a shell, so `GrantsPresentation` refuses: no title or background on that console, whether the launch presents or opted out, and no restore record, so nothing resets when the nested client exits. The launch record is untouched. Provider-neutral (one rule, one place). Tracking, alerts and the window click are kept; the exact Windows Terminal tab selection needs the shell, so for such a nested client navigation falls back to window only. A nested client in a different shell, and a standalone session in the launch's shell after the launch ended (the branch needs a live recorded launch), keep their grant.

ANALYSIS-3801: `NativeAdoptionReconcileTests`: new default-setting theory (no value saved) over claude/codex and launch presenting/opted out; a theory for a nested client in another shell and a standalone session after the launch ended; the explicit-off nested test remains and now expects no shell. Mutant (rule disabled) failed 6 tests (4 new, 2 nested-client); reverted.

ANALYSIS-3803: docs fixed in installation.md (config default true, saved false kept; click navigation window only with balloon when off, tab when on; nested note) and compatibility.md (on by default, switch off in tray, applies to running sessions; nested note). `grep -rn -i "off by default\|default false" docs` has no hits.

ANALYSIS-3804 and DEV-051 format section: `dotnet format ... --include AppConfig.cs` run; AppConfig.cs is CRLF. `dotnet format --verify-no-changes --include <8 changed .cs files>` exit 0. `./scripts/format.ps1 -Verify` exit 0.

Results: Release --no-incremental 0 warnings, 0 errors. Focused filters 484 passed. Full suite 1614/1615; the one failure, `CodexTransportTests.ASupersedingListenerIsSeenByTheNextOpenAndTheVerifiedConnectionKeepsItsBackend`, is load-only (see return).
