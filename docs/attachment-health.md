# Attachment health and terminal refresh

Claude Code and Codex share attachment health, evidence grace, terminal scheduling and diagnostics. A conversation retains its identity, palette, waits, viewed state and acknowledgment when ownership becomes uncertain.

| Health | Meaning | Presentation |
| --- | --- | --- |
| `verified-live` | The registered agent PID and creation time match a live process | Existing waits and supported navigation remain available |
| `confirmed-ended` | An explicit end, agent exit, owning-shell exit or PID reuse confirms that attachment ended | Removed from running overview and attention |
| `unverified` | Agent evidence is missing or inaccessible; a live shell alone is insufficient | Grace remains anchored to persisted evidence |
| `unavailable` | Unknown ownership exceeded the evidence grace | Visible as unavailable; no actionable chip, queued alert or terminal destination |

Set `attachmentEvidenceGraceMinutes` in the selected data directory's `config.json` to configure the shared grace. The default is `10`; values are bounded to `1` through `1440` minutes. Configuration reloads without changing voice, mute or unrelated settings. A known-live idle agent never expires because hooks are quiet. Restart preserves the unknown origin and unavailable state. A later creation-time-verified capture can repair missing agent or shell ownership on the same attachment; stale, conflicting and post-detach captures cannot transfer ownership. A fresh explicit resume registration can replace ended ownership.

`bell-tower sessions` and the Sessions window expose attachment health. `bell-tower doctor` reports counts by health and ownership mode and the configured grace. `managed` launches, directly `observed` local attachments, `shared-runtime` observations and legacy `unverified` ownership remain distinct. Unknown host kinds survive persistence and remain unavailable for navigation. Missing ownership is uncertainty, not proof that a provider is unsupported.

Launch records retain the owning shell independently of their one-time palette adoption. Detach removes that record. The title writer and detach share the presentation mutex; each request rechecks attachment generation, PID/creation identities, opt-in controls and console membership before writing. Console attachment runs only in the disposable helper.

An accepted same-ID ownership replacement uses the new generation's host and destination together; absent destination fields clear former values. Durable title/background opt-ins and unknown extension data remain intact. Ordinary same-generation repair retains established owners and modes. Before any write attempt, the scheduler invalidates its previous confirmation because a failed or timed-out write may already have changed part of the visible title/palette. Returning to a former desired value therefore restores it before successful-write deduplication resumes.

The tray computes desired titles and uses one asynchronous checkout cache for overview and titles. Discovery is serialized, bounded to 256 cached directories and fresh for five seconds, including negative results. The cache exposes explicit invalidation for a known checkout/branch change. Unchanged fresh-cache cycles launch zero title or Git children. After freshness expires, read-only discovery can refresh Git; the helper launches only when title, palette or destination changed. Failed writes retry after 2, 5, 15, 30 and then 60 seconds, with transition diagnostics and at most one summary per minute for the same failure. Changing the desired presentation resets the retry schedule. Timeout/cancellation terminates and joins only the helper started by Bell Tower; an unreaped helper prevents the next launch.

The future identity resolver supplies a separate working checkout to this cache boundary. Launch directory, planning-store path and process ownership are not interchangeable. This change continues using the session directory until that resolver is implemented.

## Shared conversations

A shared-backend Codex conversation has no agent process of its own, so it is never `verified-live` and never gets reminders, escalation or host actions. `AttachmentHealth` is the single evaluator: directly attached processes (Claude Code, embedded Codex, owned runtimes) keep the rules above unchanged, and a shared conversation is reduced by the shared lifecycle policy in `Core` from its stored evidence plus a read-only snapshot of terminal and work facts that the caller gathers before the state and presentation locks are taken. A missing observation never changes stored state.

- The terminal facet in `evidence.terminal` is `connected`, `disconnected`, `demoted` or `unverified`, with a reason. A disconnect or demotion is never completion. A stored `connected` ages to `unverified` after five minutes without a new observation. The owner client's death (PID plus creation time) and the recorded terminal shell's exit (from `evidence.terminal`, never `Host.Shell`) are detected without any new facts.
- The conversation evidence time only moves forward: it is the latest of the last hook, the stored anchor, registration and fresh active runtime work, so hooks and runtime-confirmed work get the same configured grace. Idle or merely loaded threads extend nothing.
- An exact-conversation hook revives an `unavailable` shared conversation to `unverified` (never `verified-live`, never a host action). Direct records and other unknown-owner records keep the sticky `unavailable` rule. Rolling back to an older tray restores its permanent `unavailable` for shared conversations; it never fabricates liveness.
- Evidence with a revision this build does not understand is left byte-for-byte as stored; only the legacy `Health` and `UnverifiedSince` fields are updated.

## Data compatibility

State schema remains `1`; new attachment, launch and session fields are additive, with extension-data preservation for unknown root/session/attachment/host fields. Legacy records without health fields are evaluated from actual process evidence. Palette consumption now marks the launch record consumed rather than deleting its shell metadata. Legacy normalized event helpers and capture-v1 ingress remain readable; this is not a promise that old trays implement new health semantics.

Health-aware snapshots atomically commit `attachmentHealthVersion: 1` alongside session health. New packages and `--distribution-info` advertise `attachmentHealth: 1`. The current launcher and update coordinator inspect bounded primary/backup snapshots and reject activation or rollback to a consumer lacking that capability, preserving selection and sessions. Legacy extension-data writers retain the requirement. A compatible successor must declare and implement the capability. Rollback to a legacy tray needs an explicit maintenance/migration plan. Independently running a historical binary or historical launcher cannot be retroactively guarded by new code. Such a binary preserves unknown JSON but does not honor `unavailable`, so it is not a supported monitoring consumer for this state. Existing legacy hook publishers may continue because they do not decide monitoring health.

## Evidence boundary

Automated acceptance uses matched provider fixtures, mixed simultaneous conversations, fake/silent boundaries, real owned helper termination and private hidden Windows console probes. Provider names in those native console probes identify the common adapters under test; no authenticated Claude/Codex session or user terminal is touched. Authenticated provider, VS Code and RDP field checks remain part of the integrated release acceptance change. No provider-specific restriction is claimed for attachment health or scheduling.
