# Codex headless native SessionStart probe 004

Date: 2026-11-03. Scope: one bounded, non-elevated native Codex CLI 0.165.0 `exec` run for task 1.1 (direct native process classification, headless, no terminal claimed). Developer evidence, not reviewer acceptance.

## Execution context

- The shell that launched the probe reported no administrator role and a medium integrity level.
- The launched Codex child was checked through its process token (token elevation query on the actual child): **not elevated** (value 0).
- Mode: headless `exec`, in-process (embedded) app-server transport as logged by Codex (`rpc.transport="in-process"`, client `codex_exec`). No daemon or socket was involved; no shared-daemon claim is made.

## Fixture change (authorized)

One top-level key was added to the private fixture config, before any table header, pointing at the unmodified official tagged catalog (SHA-256 `1384625c0b6b3e67d3c60da29becea2b017f2c283fc1e2d05b8edafb4698eb10`, 12 models):

```diff
 model = "gpt-5-codex"
 model_provider = "probe"
+model_catalog_json = '<repo>/artifacts/binder-analysis-004/codex-rs__models-manager__models.json'
```

Original bytes were backed up (SHA-256 `259f15423aab1c166efda9b7cb7c98a087661458e89578c9e992d265587edf27`); a line diff shows exactly this one added line and nothing else. Hook definition (`712bcc0d...a8b5`), handler script (`176b15c9...643e`), trusted hash, enablement, sandbox and the closed loopback model endpoint were unchanged before and after the run.

## Synthetic preflight (non-elevated)

The unchanged trusted handler ran in a non-elevated PowerShell 7 child with a scrubbed environment, synthetic input and a separate capture directory. It exited 0 with empty stderr and wrote exactly one parseable capture with a matching event and session, a fresh timestamp and its own PID in the ancestry. The handler's CIM ancestry query therefore works without elevation. The native capture directory stayed empty during preflight.

## Native attempt

One launch of the version-matched `codex.exe`: `exec --json --sandbox read-only` with a literal no-tools prompt, private CODEX_HOME, a dummy credential, and a closed loopback model endpoint (no stub server). Codex tracing was enabled at debug level for core, hooks, exec, model manager and app-server targets and kept in an ignored file with per-line elapsed times. Bound: 120 s.

Stage timeline (elapsed from launch):

| Elapsed | Stage |
| --- | --- |
| 0.2 s | config load, cloud config (none), in-process app-server initialized |
| 0.35 s | `thread/start` completed; `thread.started` event |
| 0.38 s | turn input submitted; `Model metadata for gpt-5-codex not found` (static catalog in use, no catalog network access; fallback metadata for the unlisted slug) |
| 0.45 s | `turn.started` |
| 0.9-2.1 s | asynchronous plugin/context preparation |
| about 2.08 s | **SessionStart hook executed; one capture written** |
| 2.18 s | user-prompt turn event |
| 4.2 s | first `/responses` request failed after 2.0 s (closed endpoint); reconnect loop with backoff until the bound |

The SessionStart stage time above is the handler's own capture timestamp compared with the traced turn and request stages; the trace contains no explicit hook-dispatch line, so hook-before-model-request ordering is inferred.

## Network observation

This run is not claimed to be loopback-only. During startup, Codex's plugin manager attempted one unauthenticated featured-plugin metadata request to a remote ChatGPT backend endpoint, which returned 401 Unauthorized. No private auth file existed and only a dummy key was present, so no account, quota or real credential was used. The model endpoint itself was loopback-only and closed. No socket-level network capture was taken, so the absence of other remote requests is not proven beyond the trace. Future isolated probes must first disable or isolate remote plugin warm-up through supported configuration.

The turn then only retried the closed endpoint; no further SessionStart capture appeared. At the 120 s bound the run was still reconnecting (timed out by design, helper exit status 1 after kill). Cleanup: the process was identity-checked by PID plus creation FILETIME before a forced tree kill, and no codex process remained.

## Capture validation

- Exactly one new capture file (none before the launch); its time was about 2.1 s after launch.
- Event `SessionStart`; run nonce matches the trusted hook's nonce; the session ID is non-synthetic and equals the thread/session ID Codex logged.
- Payload shape contained the provider fields `source`, `permission_mode`, `transcript_path`, `model`, `session_id`, `cwd`, `hook_event_name`.
- Ancestry order: hook shell (pwsh) -> hook shell (pwsh) -> **launched Codex** -> outer helper shell -> outer timeout wrappers. The launched Codex PID, creation FILETIME and image path **all matched** the launch record exactly. Outer ancestors are not used as evidence.
- Only `CODEX_HOME` matched the environment-name filter in the capture; no terminal, VS Code or Bell Tower variables were present, consistent with a direct headless process.

## Disposition

Native Codex 0.165.0 headless `exec` SessionStart dispatch, with the exact launched non-elevated Codex process in the hook ancestry, is proven for this fixture under normal hook trust, read-only sandbox and the supported offline catalog. The earlier QA-007 no-capture timeout is not reproduced; the difference includes non-elevation, the static catalog, and tracing, and this run does not isolate which factor mattered, so it should not be attributed to any single cause. The custom handler (not the product ingress) produced the capture, so product-ingress parsing of a native Codex envelope remains to be exercised.

Not established: terminal binding, shared-daemon or shared-client behavior, TUI mode, Claude/Codex mixed simultaneous sessions, freshness or reconnect behavior. No Codex provider restriction is claimed or implied.
