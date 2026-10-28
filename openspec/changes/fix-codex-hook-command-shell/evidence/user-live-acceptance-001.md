# User-operated post-migration acceptance

## Setup and evidence kinds

On 2026-10-27, after the authorized installation of Bell Tower development build 0.1.9018 and migration of both providers' hooks, the user ran the requested fresh-conversation check and reported **"both worked"** with a screenshot. This records task 5.2's live result separately from fixtures and the unsuccessful isolated mixed harness.

- Installed and selected Bell Tower version: 0.1.9018; rollback version remains 0.1.9.
- Screenshot: a Windows terminal running Codex CLI 0.164.0, successful `echo 1` execution with output `1`, and two Bell Tower chips showing `finished`. The screenshot is supplied in the conversation; no repository image file was created.
- User observation: both Claude and Codex worked in the requested post-migration check. The two chips are visible together; their provider attribution comes from the user's confirmation, not a guessed color mapping.
- Version probe: the currently resolved installed Claude CLI reports 2.1.311 with exit 0. The screenshot does not separately show the Claude process version; this probe is installation evidence, not a screenshot-based process-version claim.

## Read-only delivery corroboration

The orchestrator read each provider's existing delivery receipt without running hooks or changing settings. Both timestamps are newer than the saved migration apply record:

| Provider | Receipt observed in UTC | After migration |
| --- | --- | --- |
| Claude | 2026-10-27T19:56:05.4703583Z | yes |
| Codex | 2026-10-27T19:53:13.5700027Z | yes |

These receipts establish fresh provider delivery. They do not contain command-specific attribution in this report; the `echo 1` result and visible chips come from the screenshot and user observation.

## Result and limits

Task 5.2's user-operated post-migration Claude/Codex mixed-session and Codex-chip acceptance passes for this setup. Native hooks are operational under the migrated configuration. No separate hook-trust dialog interaction, permission-wait scenario, physical audio result, or chip-click activation is inferred from this simple command trial.

Task 2.1's native Codex cmd/Bash fallback coverage remains unverified. The default parallel-suite timing failure also remains recorded alongside the successful isolated and full serial-collection runs. This live result does not waive either gap or declare the whole change ready to archive.
