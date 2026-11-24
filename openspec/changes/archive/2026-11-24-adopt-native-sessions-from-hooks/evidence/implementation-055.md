# Implementation evidence: DEV-055 (task 4.5)

## Changes

- `TrayProcess.SignInToken` uses the linked limited token's logon session identity for `TokenElevationTypeFull`; ordinary limited/default tokens retain their own identity. This keeps Exit suppression consistent across UAC elevation.
- An elevated hook creates the tray with `CreateProcessWithTokenW` using a duplicated primary limited token and that token's environment. If token lookup, duplication, environment creation or process creation fails, the attempt is recorded and the capture remains queued; it does not fall back to the elevated token.
- Process creation requests `CREATE_BREAKAWAY_FROM_JOB`. If access is denied and `IsProcessInJob` confirms job membership, startup records `provider-job-breakaway-refused` and does not retry inside the job. This is safe for nested jobs because it does not infer parent-job lifetime from the immediate job's flags.
- The tray Exit menu item is built by a small testable factory used by the real menu. Tests invoke that click handler, and invoke the actual CLI parser/action for `bell-tower exit` while holding the tray mutex.
- The fake-launcher test helper can run the ingress inside a synthetic kill-on-close job. Matched Claude and Codex rows assert neutral hook output, durable queued capture, a failed breakaway diagnostic and no stand-in tray.
- Compatibility, installation and hook-ingress docs describe the new safe failure behavior and state that the native Codex job remains unverified.

## API references

- [TOKEN_ELEVATION_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ne-winnt-token_elevation_type): Full is elevated, Limited is the limited token, and Default has no linked token.
- [TOKEN_LINKED_TOKEN](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_linked_token): the linked-token handle is closed after use.
- [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw): required token rights and caller privilege; explicit application path, Unicode environment, no standard-handle inheritance flags, no profile reload.
- [IsProcessInJob](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob): a null job handle checks membership in any job.
- [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects): kill-on-close terminates associated processes when the last job handle closes, including nested job descendants.

## Verification

Review target: baseline `5e955542e430397bbc82291395d00c48e5d7cba8`; `head=5e95554 baseline=5e95554 files=23 diff=f0eb110b328d untracked=7/fa1813bdefad`.

| Check | Result |
| --- | --- |
| Release build through the installed .NET 10.0.115 `MSBuild.dll` entry point from outside the repository | passed; Protocol, BellTower, ingress, launcher, fake launcher and test assembly compiled. The repository SDK pin and lock files were not changed. |
| Focused `HookTrayStartTests` first attempt | not completed; VSTest could not connect to testhost after 90 seconds. |
| Focused escalated VSTest retry | cancelled while awaiting execution approval; no result from this invocation. The orchestrator started an independent escalated focused run; its final result is pending at this handoff. |
| Real elevated Claude/Codex session and UAC token comparison | not performed; historical environment evidence does not establish a provider or platform restriction. |
| Native Codex hook job probe | not performed; do not claim Codex permits breakaway. |
| Native AOT publish | not performed in this fix cycle. |

## Limits

If Windows denies access to the linked limited token, `CreateProcessWithTokenW`, or breakaway from the hook's job, the session remains queued until a later hook can start the tray outside the job. The focused provider fixtures exercise both providers through the shared ingress; native elevation and Codex job behavior remain unverified pending user-operated acceptance.
