# Native adoption probe follow-up 004

Date: 2026-10-29. Scope: test whether shared-mode Codex 0.165.0 clients emit thread-to-process metadata before the hook trust gate, then reopen an already-trusted private fixture to inspect the normal single-hook review flow. All provider state, fixture data, commands, dummy credentials, and log projections were under ignored `artifacts/`. No real credentials, model requests, user sessions, production daemon, user terminal, or real repository trust were used.

## Two-client shared-mode observation

Two direct Codex 0.165.0 TUI clients were launched in separate private ConPTYs with the same isolated `CODEX_HOME`, the same already-trusted nested Git fixture, the default shared-daemon mode (no `--no-daemon`), and `--sandbox read-only`. Each had a dummy API key and an unreachable loopback model endpoint. No input or model prompt was sent. Both clients stopped at the normal “Hooks need review” screen. No private daemon was present before launch or after the clients exited.

After waiting for log flush, a read-only Node.js `DatabaseSync` query projected only `ts`, `ts_nanos`, `thread_id`, `process_uuid`, and `target` from the isolated home’s existing log database. It did not read message, feedback, prompt, transcript, or payload values. Of the 226 projected rows, 48 rows belonged to each exact launched TUI process identity; the PID encoded in each process UUID matched the helper-recorded PID, and both rowsets had zero non-null thread IDs. The remaining 130 rows belonged to the later single-client reopen described below. Thus both clients produced process-local metadata before the review gate, but this trial allocated no thread that could be joined to either client. This is a pre-allocation result, not a shared-binder failure or provider restriction.

The clients were stopped by their private ConPTY helper’s bounded lifetime. At follow-up, neither recorded TUI PID was live. No unrelated process was stopped. The isolated home had no running shared daemon after cleanup.

## Ordinary reopen and native hook review

The exact Codex 0.165.0 TUI source review in the ignored source cache showed that the startup Windows sandbox notice is conditioned on a trust decision having been made during startup (`tui/src/lib.rs`, `should_prompt_windows_sandbox_nux_at_startup`). Reopening the already-trusted private fixture with unchanged `--sandbox read-only` displayed hook review directly; no folder-trust prompt or Windows sandbox setup modal appeared before the hook review. This is consistent with the source condition and did not change sandbox mode or setup.

The review list contained one newly configured private SessionStart hook. The detail screen displayed the exact disposable command, `Sync` mode, and “new hook / review required” trust status. No hook capture was written, so the handler did not execute and there is no native hook payload, session ID, or ancestry evidence from this attempt. No prompt was submitted.

On returning from the detail screen, the list footer’s `t` action was identified as “trust all.” The auto-review rejected the submitted `t` action with: “The t shortcut followed by Enter may invoke ‘trust all’ for hooks, creating a persistent broad trust decision; the authorized scope permits only reviewing or explicitly trusting the single private hook, not global hook trust.” The action was not retried. The exact per-hook detail action was therefore not accepted in this run. The bounded ConPTY child was no longer live at follow-up; the per-run capture directory remained empty. The isolated home had no live shared daemon.

## Disposition

The shared-mode clients’ existing process metadata did not include a thread ID before they reached hook review. The ordinary reopen safely passed the prior Windows setup modal, but did not establish hook trust or dispatch. The known process UUID-to-PID observation remains only a process-local log association; it does not join a shared thread to its TUI or map a hook `session_id` to a thread ID. Same-directory simultaneous thread ownership, reconnect, stale daemon terminal variables, hook-session/thread mapping, native hook ancestry, and actual desktop host acceptance remain unverified. No binder or hard provider restriction is claimed.

The remaining safe probe would require the normal TUI’s explicitly single-hook trust action, followed by a fresh per-run hook capture whose ancestry PID, creation time, and executable match the launched native child. The broad trust-all action is outside the probe scope. No sandbox setup or security-mode change is needed for that proposed action.
