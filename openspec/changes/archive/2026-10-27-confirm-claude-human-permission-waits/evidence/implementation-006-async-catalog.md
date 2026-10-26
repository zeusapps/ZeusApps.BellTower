# Local Codex app-server async-tool catalog check

Captured 2026-10-26 using installed Codex CLI 0.164.0. This is an offline metadata and fake-endpoint probe. It did not make a model request, use credentials, or interact with a real user dialog.

## Method and results

An isolated hidden app-server was initialized with `experimentalApi=true`. A `model/list` request against the local bundled catalog returned eight model records: `gpt-6.1-sol`, `gpt-6-astra`, `gpt-6-sol`, `gpt-6-luna`, `gpt-5.6-sol`, `gpt-5.6-terra`, `gpt-5.6-luna`, and `gpt-5.5`. Each returned an empty `experimentalSupportedTools` list. The catalog records names/capabilities only and are not an entitlement check.

The follow-up turn used the same private CODEX_HOME, disposable project and fake loopback Responses endpoint as evidence 005, in Plan mode with `experimental_request_user_input` enabled. The fixture model was not offered `request_user_input_async`; the ordinary `request_user_input` positive control was offered and raised `item/tool/requestUserInput`, leaving status `waitingOnUserInput` while unanswered. No local catalog entry advertises async support that could be selected as a positive advertised-model control. App-server child processes exited after each run; no external service or production process was used.

The probe helper and raw run data are ignored under `artifacts/permission-async-catalog-006/`. Its `model/list` inspection logs only IDs, display names, and `experimentalSupportedTools` metadata.

## Evidence boundary

This narrows the earlier fixture-model result: it is consistent with the installed CLI 0.164.0 bundled catalog advertising no experimental async tools. It establishes no provider-wide or TUI restriction. It does not cover dynamic catalogs, other CLI versions, or the interactive TUI's Plan-mode tool list. Task 2A.5 remains partial, with this exact local catalog evidence and the TUI limitation recorded.
