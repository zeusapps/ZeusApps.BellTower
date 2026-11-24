> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-050); client: Claude Code 2.1.330, Agent SDK subagent; mode: non-interactive; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-050 (light recheck of ANALYSIS-4900 after DEV-062)

## Review target

- Reviewer / role: independent code analyzer / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.330, Agent SDK subagent / non-interactive / `claude-opus-5-5`
- Date: 2026-11-23T21:27Z (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-050
- Task and requirement references: [tasks](../tasks.md) 4.7; [session-identity](../specs/session-identity/spec.md) "Provider-owned terminal titles", scenario "Setting reset or applied while a session runs"; developer evidence [implementation-062](../evidence/implementation-062.md)
- Scope and relevant neighboring paths:
  - DEV-062 diff: `src/BellTower/IO/ProviderTitleState.cs` (`ReadRecords`), `tests/BellTower.Tests/Hosts/ProviderTitleStateTests.cs` (`AMalformedRecordFileIsEmptyAndTheNextSightingRewritesIt`)
  - orchestrator spec edit: `specs/session-identity/spec.md:66-69`, compared with `docs/installation.md:80` and `docs/compatibility.md:114`
  - neighbors: `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/IO/Json.cs`
- Baseline commit: `2e7f318`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2e7f318 -Paths src tests docs vscode` printed `head=2e7f318 baseline=2e7f318 files=61 diff=6c6beec81cd8 untracked=21/f09022fb5c05` (matches the assignment)
- Before/after identity: after the analysis the same command printed `head=2e7f318 baseline=2e7f318 files=61 diff=6c6beec81cd8 untracked=21/f09022fb5c05`. The tracked diff SHA-256 was `6c6beec81cd81d5f35e56290f485f4d4c93b85e3ffd23257a597167f488dcfb8`, `ProviderTitleState.cs` hashed to `225ba201…` and `ProviderTitleStateTests.cs` to `ee671a78…`. The two runs were identical; nothing changed unexpectedly.
- Prior reports / rechecked findings: [analysis-049](analysis-049.md) (ANALYSIS-4900)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 4900: exception coverage | Source: `ProviderTitleState.cs:167-179`, `AtomicFile.cs:46-51`, `Json.cs:19` | `ReadRecords` now catches `InvalidDataException`. `AtomicFile.Read` throws it only for a file over 16 MiB. `Json.Decode` throws it only for a `null` document. These are the only two sources on this path | Both triggering inputs now read as empty |
| 4900: all entry points | Source: `DisabledFor:53-54`, `ObserveBoth:60-64`, `Report:70-74` | All three reach the record file only through `Observe` and then `ReadRecords` (`:132`). `SettingsFile` in `Report` does not touch the record | None of the three can throw for a null or oversize record |
| 4900: self-heal | Source: `Observe:145-162` | With empty records, the next present sighting (`record: true`) writes a valid file through `AtomicFile.Write`, which replaces the old file. An absent or unknown reading writes nothing, so a malformed file stays until a present sighting. Repeated reads are cheap: the oversize case checks only the length, and `null` reads 4 bytes | Heals as stated in the evidence |
| 4900: direction | Source | Empty records make the effective time the settings file's last write time. That is the same as or later than the recorded `Since`, which was taken from the same file's write time when first seen. It can only withhold a title, the same direction as the older-shape path | Conservative only |
| Swallowing review | Source | The new catch covers only data-shape errors from the state file's own reader. I/O and access errors were already caught. Settings-file reading (`Read:194-215`) and `WriteRecords` are unchanged. No error that should surface is now hidden; doctor still reports present and since from the settings file | No new inappropriate swallowing |
| Test | Source: `ProviderTitleStateTests.cs:378-407` | Four cases: Claude Code and Codex, each with a `null` document and a 17 MiB stand-in. `ObserveBoth` runs on the malformed file, then the test asserts `Report.Present`, the before and after `DisabledFor` boundary, and a small rewritten file that contains the provider key. `Report` and `DisabledFor` run only after the rewrite, so the test shows their no-throw behavior on a malformed file only through the shared `ReadRecords` path | Adequate. Execution is owned by QA-057 |
| Spec AND clause vs docs vs code | Source: `spec.md:69`, `docs/installation.md:80`, `docs/compatibility.md:114`, `Observe:146-153`, test `ARemovalAndReAddNobodySawIsTheDocumentedLimitation` | The spec says a removal and re-add made while the tray is not running is not observed, so a session started in the gap may still be titled, and the docs state this. Both docs state the same gap. In code, when the tray comes back and sees the setting present with an existing entry, it updates only `Stamp` and keeps the old `Since` (`:148-153`), so `DisabledFor` grants the gap session. The spec says "may" and the docs say "is still titled" under the premise that nothing saw the gap. These do not conflict: the title also needs the tray setting on and the session still alive, and a hook-started tray that sees the absence closes the gap | Consistent |
| Claude Code and Codex parity | Source and test | One `ReadRecords` and `Observe` rule for both providers. The new test covers both providers. No per-provider branch was added | No gap |
| Architecture / complexity | Source against `docs/development.md` | One added exception type and comment in IO. No new dependency | Conforms |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/IO/ProviderTitleState.cs tests/BellTower.Tests/Hosts/ProviderTitleStateTests.cs` | exit 0 | Passed |
| Build / tests | Not run | Owned by QA-057 | Not performed |

## Findings

**No findings.**

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4900 | `head=2e7f318 baseline=2e7f318 files=61 diff=6c6beec81cd8 untracked=21/f09022fb5c05` | `ReadRecords` treats the two `InvalidDataException` sources (null document, file over 16 MiB) as empty. `ObserveBoth`, `DisabledFor` and `Report` all read the record only through it, so none of them throws, and the tray tick's title, name and overview refresh is no longer blocked. The next present sighting rewrites a valid record. The fallback can only withhold a title. Both providers are covered by the new test, and no error that should surface is newly swallowed | verified (source; test execution owned by QA-057) |

## Assessment and limitations

Scope covered: the DEV-062 correction for ANALYSIS-4900 and its regression surface, for Claude Code and Codex, by source inspection plus a verify-only format check on the two changed files. Also checked: the orchestrator's AND clause in the session-identity scenario. It agrees with `docs/installation.md`, `docs/compatibility.md` and the code (the old `Since` survives a re-add that nobody saw, which the named limitation test covers).

- No regressions and no new findings.
- Not performed: build and tests (QA-057), a malformed-file probe on a rebuilt assembly (source evidence is enough for a one-line catch change), and live tray, title or click checks.
- The new test asserts `Report` and `DisabledFor` only after the rewrite; their no-throw behavior on a malformed file rests on the shared `ReadRecords` path (source evidence, not a separate assertion).
- The write of this report was refused, as quoted at the top; no repository file was written.
