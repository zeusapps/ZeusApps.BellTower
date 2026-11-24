# Codex headless native hook probe 001

Date: 2026-10-29. Scope: one bounded current-version Codex CLI `exec` probe for task 1.1. This is an installed native headless process attempt, not a fixture or terminal-binding test. The launched child used the version-matched Codex executable (0.165.0), `exec --json --sandbox read-only`, a private existing Codex home with one normally trusted `SessionStart` handler, a dummy auth value, and a closed loopback model endpoint. The prompt explicitly prohibited tools, file access, and changes. No external model service, real credential, user terminal, or provider setting edit was used.

The exact launched process identity (PID, creation time, executable image) was recorded by the existing private ConPTY helper. The helper ran for its 25-second bound and returned timeout status 124; the exact process was no longer present after cleanup. Codex emitted JSON startup/error events, but no successful model response was expected or observed. The private config and hook definition hashes were unchanged.

The authorized capture directory was empty immediately before launch and remained empty after launch. Therefore this run does not establish that native Codex dispatched the hook, nor does it provide a captured ancestor chain to compare with the exact launched PID, creation time, and image. The separate synthetic handler-through-shell preflight was not performed before this launch. No second native run was attempted.

## Results

| Check | Result | Evidence boundary |
| --- | --- | --- |
| Installed Codex version and native executable | Passed: 0.165.0 | Version checked under the private Codex home. |
| Private endpoint and credentials | Passed: selected endpoint was loopback-only and closed; child received only a dummy auth value | No real provider request or credential use. |
| Normal `exec` launch | Passed as an installed native process launch | `exec --json --sandbox read-only`; no trust bypass, approval override, or sandbox mode change. |
| Bounded cleanup | Passed | Helper timed out at 25 seconds (exit 124); exact launched process was absent afterward. |
| Native SessionStart capture | Failed to establish | No fresh capture was created; the existing capture directory remained empty. |
| Exact provider ancestry | Not performed | Without a capture there was no provider-recorded PID, creation time, or image to compare. |
| Synthetic handler preflight through its declared shell | Not performed | The required preflight was missed before the native launch. No second launch was used to compensate. |
| Terminal binding | Not applicable | This was `exec` headless/embedded mode; no terminal association is claimed. |
| Mixed clients or concurrent sessions | Not performed | Outside this one-case probe. |

The selected isolated configuration named `gpt-5-codex`; the endpoint was deliberately closed, so no successful model resolution or response was established. This attempt does not prove native hook trust/dispatch, exact ancestry, shared-session behavior, or any provider restriction. Task 1.1 remains incomplete.
