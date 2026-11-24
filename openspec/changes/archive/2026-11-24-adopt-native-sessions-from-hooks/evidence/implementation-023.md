# Implementation 023: SDK pin and QA-1304 retained-evidence replay

Developer assignment DEV-023 for task 2.2, on top of [implementation-022](implementation-022.md). Baseline `b6c0b45`. Developer evidence, awaiting independent verification of QA-1304; nothing here closes a finding. No product code changed and no replay exposed a defect.

## SDK pin

`global.json` now selects the installed SDK 10.0.114 with `rollForward: disable` unchanged, and the SDK row of `docs/compatibility.md` states 10.0.114. `docs/validation.md` and `docs/distribution-validation.md` keep 10.0.113 because they record what was validated then. The only other consumer is `.github/workflows/validate.yml`, which reads `global.json` through `global-json-file`; no other file pins the version. No temporary `global.json` edit was needed for build or test.

## Retained-evidence replay (QA-1304, task 2.2)

The retained probe files hold metadata, not raw hook payloads. `native-probes/hook-captures.json` records, for one real native Claude Code 2.1.310 and one real native Codex 0.164.0 SessionStart, the opaque session ID, provider, ancestor image names, captured agent identity and the dedicated classification. `claude-native-001` and `codex-headless-004` record the payload field shapes (`session_id`, `cwd`, `hook_event_name`, `source`, plus `permission_mode`, `transcript_path`, `model` for Codex). The fixtures under `tests/BellTower.Tests/Fixtures/native/` therefore are:

- `retained-hook-captures.json`: byte copy of the retained capture record (already sanitized: no paths, opaque IDs only).
- `claude-` and `codex-` `sessionstart.json` and `userpromptsubmit.json`: payloads rebuilt from the recorded shapes, with the retained session IDs, placeholder `D:/example` paths and a redacted prompt. These are reconstructions of the recorded shape, not verbatim captured payloads (the verbatim ones were never retained).

`Providers/NativeReplayTests.cs` (both providers, 9 cases) replays them through the real ingestion paths:

- `HookIngestor` (legacy event path) and `CaptureFactory`/`CaptureSpool`/`StateStore` (capture path) each establish a native conversation from the retained SessionStart, with the retained classification: Claude dedicated to the agent process, Codex failed closed to the shared runtime without a process or shell.
- A later `UserPromptSubmit` alone, with no prior SessionStart, recovers the conversation through both paths with no wait or attention invented.
- The retained Claude agent process no longer exists and state reconciliation correctly ends a session whose agent is gone, so a dedicated capture is replayed with a live stand-in process. Classification and policy are unchanged.

## Real-process Resolve

`Hosts/NativeAgentResolveProcessTests.cs` copies `cmd.exe` to `claude.exe` or `codex.exe` in a disposable folder and runs `bell-tower.exe event --provider <p> --data-dir <temp>` as its child, so `AttachmentCapture.Resolve` reads a real, live ancestor command line. Rows: Claude interactive, `-p`, `--print`; Codex embedded runtime and `app-server` shared backend. Each asserts the stored attachment mode, lifecycle owner and hook-observed health; headless claims no host; the shared backend never claims the process or terminal. There was no existing real-process Resolve test for Codex, so both providers are new here.

## Checks

See the handoff for commands and counts. All new rows passed (12 tests). Notes:

- The full suite once failed `AudioQueueTests.NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor` while a formatter run executed in parallel; it passed alone and in its class (44 of 44) and is unrelated to this change.
- The format check initially flagged CRLF endings and one unused using in the new files; fixed with the formatter limited to the two new files, then verified clean.

## Limitations

- Codex headless `exec` cannot be shaped through a `cmd.exe` image because `/c` must be its first argument and the classifier reads the first positional token; that mode stays covered by the classifier theory and by `codex-headless-004` as evidence, not a real-process row.
- Payloads are reconstructions of recorded shapes. A verbatim native Codex envelope through product ingress remains unproven, as already stated in `codex-headless-004`.
- Real-process rows depend on the built `bell-tower.exe` in the Release output.
