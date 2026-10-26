# Codex Plan-mode async question availability check

Captured 2026-10-26 with Codex CLI 0.164.0. This was an isolated **app-server** probe, not the interactive terminal UI. The app-server used a private `CODEX_HOME`, disposable project, loopback fixture Responses endpoint and a synthetic API key; no authenticated model request, real user dialog, production settings or live session was used.

## Method

The copied probe helper is `artifacts/permission-async-plan-005/codex-appserver-probe.js`; ignored run data are under that directory. The probe starts a hidden app-server process by its resolved Node entry point, connects to an ephemeral loopback websocket, initializes experimental API support and starts a root thread turn with `collaborationMode.mode = plan`. It records the function tools sent to the fixture model and the app-server tool request/status events. The configured Plan feature was `experimental_request_user_input = true`.

Before escalation, the sandbox run could not establish the loopback websocket (`no-connection`). The same isolated probe succeeded when run outside the sandbox, as required for a sandbox network failure. The probe-owned app-server child exited after each bounded run. A separate conhost process was visible after one run; its parent identity could not be queried in this environment, so it was left untouched and is not claimed as probe-owned cleanup.

## Results

| Plan-mode fixture call | Function tools offered to fixture model | App-server result |
| --- | --- | --- |
| `request_user_input_async` | Included `request_user_input`; did not include `request_user_input_async` (`hasTool=false`) | No input-request server method, turn went idle |
| `request_user_input` positive control | Included `request_user_input` (`hasTool=true`) | `item/tool/requestUserInput` was sent, and thread status exposed `waitingOnUserInput` while unanswered |

The first result means this Codex 0.164.0 app-server / Plan / fixture-model configuration did not expose the async function. The positive control confirms that this Plan turn reached and registered the blocking question tool. Full sanitized event output is retained only in the ignored run directories.

## Contract boundary and limitations

Current OpenAI app-server documentation was checked at [Codex App Server](https://learn.chatgpt.com/docs/app-server). The official [async tool calling guide](https://developers.openai.com/api/docs/guides/async-tool-calling) describes `request_user_input_async` as an application-defined function tool rather than a built-in Responses tool. Neither page proves that every Codex model catalog or UI surface should expose this function. In particular, the captured app-server tool list does not prove the interactive TUI's Plan-mode tool list.

Therefore this is narrow version/mode/configuration evidence, not a provider-wide hard restriction. Task 2A.5 remains partial: a Plan-mode app-server probe was performed with an async-unavailable outcome; interactive TUI Plan mode and other model/provider configurations remain unverified. The separate live auto-versus-human permission acceptance remains unperformed.
