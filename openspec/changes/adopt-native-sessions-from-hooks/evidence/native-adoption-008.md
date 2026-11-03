# Native adoption probe follow-up 008

Date: 2026-10-29. Scope: diagnose the unchanged isolated SessionStart handler in the actual PowerShell 7 hook shell, then restore its one private test-state enablement flag. No Codex or Claude provider was launched in this step.

## Direct handler preflight

The handler was invoked directly with synthetic `SessionStart` input and a synthetic session ID. It ran in the same elevated PowerShell execution context needed for its read-only process ancestry query, with a new capture directory separate from native-run captures. The wrapper removed inherited provider, terminal, Bell Tower, and credential environment keys before starting the hook shell. No provider process, model request, network call, or terminal input was involved.

The first diagnostic run showed that the handler exited successfully and wrote a capture, but my result projection marked the capture unparseable because it reparsed a timestamp that PowerShell had already converted to a `DateTime`. That was a wrapper validation defect, not a handler error. I fixed the projection and repeated the direct preflight into a fresh capture directory. The corrected run confirmed:

- handler exit code `0` and exactly one capture;
- synthetic session and `SessionStart` event matched, with a fresh capture nonce;
- capture time was after the invocation began;
- ancestry was present and the handler process PID plus creation FILETIME matched the ancestry record;
- the input contained only `hook_event_name` and `session_id` fields;
- stderr error type, fully qualified error ID, script line, and native error code were all absent because the handler succeeded.

The raw capture and sanitized result remain only under ignored private artifacts. No raw ancestry PID, executable path, session ID, or environment values are included here.

## Private test-state repair

Inspection found that the sole isolated `SessionStart` hook state had its existing trusted hash but `enabled = false`. The authorized correction changed only that boolean to `true`. The original config bytes were backed up under ignored private artifacts before the edit. Post-edit assertions passed: only the enabled boolean changed; the existing trusted hash and hook definition file were unchanged; model/provider endpoint config was unchanged; and sandbox-related config was unchanged. No trust record was created, copied, or altered, and no hook definition or reviewed command was modified.

## Disposition

The direct preflight now proves the private handler can produce one fresh ancestry capture in its required execution context, and the isolated test state's intended enablement is restored without changing trust or sandbox policy. This does not establish native provider dispatch or terminal ownership. No Codex retry was launched; the next native attempt remains with the orchestrator to assign.
