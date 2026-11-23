# DEV-061: fix cycle 2 (ANALYSIS-4800, ANALYSIS-4801)

Baseline `2e7f318`, on top of the uncommitted DEV-058 to DEV-060 work. Nothing else was changed. Same constraints as DEV-060: disposable data directories and provider homes under the ignored `artifacts/` and the test workspace, no real settings, tray or installation, no scheduled task left, no commit.

## ANALYSIS-4800: the effective-time record (fixed)

`ProviderTitleState` was reworked, with the orchestrator's decision applied point by point:

- **Keyed by the resolved file.** Records in `provider-titles.json` are keyed `provider|<full path of the settings file>` and store `{Since, Stamp}` (the file stamp last observed). A process that resolves another home (a doctor run from a terminal with a different `CLAUDE_CONFIG_DIR` or `CODEX_HOME`) reads and writes a different key, so it cannot grant or erase the tray's record.
- **Doctor is read-only.** `Report` never writes or forgets a record. With no record it still answers from the file (present, since its write time) without storing it.
- **Unknown is not absent.** A reading is now present, absent or unknown. A sharing violation, a locked file, a file over the size bound or a partly written document that does not parse is unknown: it is not cached (the next look reads the file again), nothing is recorded or forgotten, and the answer is the existing record (false when there is none). A title already granted is therefore not withdrawn, and a record is not dropped, by a file the provider is rewriting.
- **The tray observes both files on its tick.** `TrayApplication` calls `ProviderTitleState.ObserveBoth()` on its existing two-second overview tick, so an absence is seen and forgotten while the tray runs; a later re-add then gets a new effective time and a session started in the gap is not titled.
- **Older shape.** A record in the earlier provider-keyed shape does not read as the new shape, is treated as empty (unknown) and the setting is sighted again at the file's write time. Tested with a 2020 time that must not be trusted.
- **Limitation, documented** in `docs/installation.md` and `docs/compatibility.md` (open evidence): a removal and re-add of the setting while the tray is stopped is not seen, and a session started in that gap is still titled. A test documents this case by name.

Tests (`ProviderTitleStateTests`, 12 to 22), each provider-parameterized for Claude Code and Codex, plus mixed cases:

- doctor resolving another home, absent there and then present there with a later time, leaves the tray's answer and the record text unchanged, and earlier sessions stay untitled;
- `Report` never creates a record, and a mixed pair keeps one record per provider and file, unchanged by doctor resolving another home for both;
- a removal and re-add seen by the tick gives no title to a session started in the gap and a title to a later one; the unobserved case is the documented limitation;
- a partly written file (a truncated JSON document, an unterminated TOML table) and a file held exclusively with a moved write time leave the record text and the granted answer unchanged, a finished write that still holds the setting keeps the original time, and one that dropped it is seen absent;
- an older provider-keyed record is re-sighted.

## ANALYSIS-4801: the unregister script (fixed)

The script now looks the task up with `-ErrorAction Stop` inside `try`/`catch`. Only an error in the `ObjectNotFound` category counts as already removed; any other failure is re-thrown, so under `ErrorActionPreference Stop` the script exits non-zero, the setup action fails and the task record and ownership are kept for a retry. The folder step and the final `exit 0` are unchanged.

Real PowerShell cannot be made to fail a lookup in a way other than not-found on demand, so the tests run the real script text with `Get-ScheduledTask` shadowed by a function (not-found, access denied and a generic failure):

- `OnlyAGenuineNotFoundCountsAsRemovedAndAnyOtherLookupFailureExitsNonZero`: exit 0 for the not-found error, non-zero for the permission-denied and generic failures;
- `ALookupFailureOtherThanNotFoundKeepsTheRecordAndARetryAfterItClears`: through `IntegrationSetup` and `SetupPlan.Apply`, the failing lookup throws the removal error and `hook-start/task.txt` stays; the retry with a not-found lookup clears it;
- the earlier missing-task test (`RemovingATaskThatIsNotThereCountsAsRemoved`, real Task Scheduler) and the real register, run and delete test still pass.

## Verification

`dotnet build -c Release --no-restore`: 0 warnings, 0 errors. `ProviderTitleStateTests` and `ProviderTitleStepTests` 52/52. `dotnet format --verify-no-changes` on the changed C# files passed. `openspec validate adopt-native-sessions-from-hooks --strict` passed. The full-suite result is in the handoff. `vscode/` is unchanged, so the node bridge tests were not run.

## Notes

- The tray tick adds two file stats (plus a read only when a settings file's write time changed) per two seconds; the answer for a title write or click is still cached by stamp.
- Other processes (`sync-title`, `focus-terminal`) record for the home they resolve, which is the profile the provider in that terminal uses; a different home has a different key.
