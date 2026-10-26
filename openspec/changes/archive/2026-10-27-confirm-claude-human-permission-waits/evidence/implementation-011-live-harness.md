# Live auto-versus-human trial harness 011

Prepared 2026-10-26 for tasks 3.4 and 4.1. This record describes a harness for the user to operate; it contains no live-trial result. Every observation below under "Harness smoke" came from non-interactive checks against a loopback fake model with no approval answered. No human or automatic approval trial has been performed on the installed candidate, and nothing here closes 3.4 or 4.1.

Harness root (ignored, local only): `artifacts/permission-live-011/`. All paths below are relative to the repository root. Run every command from the repository root.

## Targets verified at preparation

| Item | Observed |
| --- | --- |
| Bell Tower selected and running | 0.1.9012 (launcher, selected version file and payload agree); source `754aed5` plus the reviewed dirty-tree identity |
| Claude Code | The default client had auto-updated to 2.1.311 during preparation. The earlier 2.1.310 release is still on disk, so the harness runs a byte-identical copy of it (hash verified against the on-disk release). The 2.1.310 copy reports `2.1.310` and sends `cc_version=2.1.310` to the fake endpoint. The user's default client and settings were not touched |
| Codex CLI | 0.164.0 (installed native executable) |
| Bell Tower ingress used by the isolated hooks | The installed 0.1.9012 `bell-tower-ingress.exe`, executed in place, read-only. The user's own hook entries reference two other ingress images (neither hash equals the 0.1.9012 helper), so the user's real client configuration is not yet on the 0.1.9012 helper; the harness is unaffected because it names the installed 0.1.9012 helper directly |

## Isolation design

- **Data directory.** Every run gets `work/<run>/data` and passes it explicitly: `--data-dir` on the hook command line, on the tray and on every CLI call. Hook events therefore never reach the production data directory (verified after the smoke runs: no production file references the harness folder, and the production tray processes kept their identities).
- **Second tray, safe alongside the user's tray.** Bell Tower's single-instance mutex and every lock name are derived from the data directory root, so a tray on a different `--data-dir` is an independent instance. The harness starts `tray --silent --duration-ms 2400000` from a byte-identical copy of the installed payload (located outside any `versions` folder, so update discovery and installation logic never engage). Its strip is positioned at a distinct top-left location (x 30, y 90) through the isolated `position.json`, away from the user's strip. The user's tray, its codex runtime hosts and its data were not touched in any smoke run. Not verified: a screenshot of the strip on a real display; the displayed chip text is read through UI Automation from the isolated tray's own windows instead.
- **Provider isolation.** Private `CLAUDE_CONFIG_DIR` / `CODEX_HOME`, `--setting-sources ""` for Claude, a disposable git project (branch `trial`) per provider, a loopback fake model endpoint per run, synthetic keys, provider and Bell Tower environment variables stripped before launch, telemetry and auto-update disabled. Claude and Codex are launched through the installed `Invoke-BellTowerSession` function (extracted from the installed shell module with the executable and data directory pinned per run), so attachment, managed-runtime and detach behavior match a managed session. Codex plugin, apps and tool-suggestion features are disabled in the isolated config to avoid a repository sync; the Codex TUI still fetches a startup tip (no inference).
- **Audio: log only, no sound.** The tray runs with `--silent`, which selects `SilentAudioSink`; no physical audio or speech can play. No sound test, voice toggle or tray exit is invoked.

## How Bell Tower state and announcements are observed

Bell Tower 0.1.9012 has no announcement log line: `bell-tower.log` records failure codes only, and the silent sink drops audio items without recording them. A direct "announcement queued" log therefore does not exist, and the harness does not claim one. It records, in `work/<run>/obs/timeline.txt` and `timeline.jsonl` (changes only, about 0.7 s resolution):

- the persisted session state from the isolated `state.json` (attention reason, waits with confirmation provenance Provisional/Confirmed, notification due time);
- `announce=True`: derived with the product's own `AlertPolicy.Collect` rule (attention reason set, notification due time reached, episode not acknowledged, session live). This is the condition under which the tray would queue an announcement. It is derived evidence, not a log of the audio queue;
- `unverifiedChip=True`: a non-confirmed Permission wait at least 15 s old, with no attention reason (the silent "possibly waiting for approval" rule);
- `CHIPS:`: the chip text actually rendered by the isolated tray, read with Windows UI Automation (direct display evidence), for example `... needs permission, 00:00:06` or `... possibly waiting for approval, 00:00:15`;
- a record-only provider hook timeline (`claude/hooks.jsonl`, `codex/hooks.jsonl`: event names, tool, notification type, no decisions) and the fake endpoint request metadata (`*-server.jsonl`);
- `bell-tower list` and `bell-tower doctor` against the isolated data directory (`-Action Status`), and `work/<run>/data/bell-tower.log` for failures.

After every session ends, Bell Tower raises a normal `Done` ("finished") chip with `announce=True` about 20 s after Stop. That is the completion behavior, not a permission alert: judge scenarios by the `reason` column (`Permission` versus `Done`).

## Commands

Open a PowerShell 7 prompt at the repository root. Each scenario starts its own windows (a "BELLTOWER TRIAL ..." window per client), a silent second tray, the fake endpoint and the observer, and first stops any harness-owned processes from a previous run. Do not type into a window except where the instruction line in it says to.

| # | Command (copy-pasteable) |
| --- | --- |
| 1 Claude human approval | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 1` |
| 2 Claude slow automatic approval | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 2` |
| 3 Codex human approval, managed owning runtime | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 3` |
| 4 Codex slow automatic approval, managed runtime | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 4` |
| 5 Codex `--no-daemon` human approval | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 5` |
| 6 Mixed Claude and Codex, both approved | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 6` |
| Optional 2b (different mechanism, see below) | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 2 -HookDelay` |
| Optional 4b (auto review without owning runtime) | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Run -Scenario 4 -NoDaemon` |
| Inspect current run (timeline, list, doctor, hook and endpoint timeline) | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Status` |
| Copy run summary to `work/<run>/collected` | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Collect` |
| Cleanup (harness-owned processes only) | `pwsh -NoProfile -File .\artifacts\permission-live-011\live.ps1 -Action Cleanup` |

Cleanup stops only processes recorded in the run's `pids.json` whose start time still matches, plus processes of a fixed set of image names (`node`, `pwsh`, `claude`, `codex`, `bell-tower`, `bell-tower-ingress`, `cmd`, `conhost`) whose command line contains the harness `work` folder, and never an ancestor of the cleanup process. It does not touch the installed tray, the production data directory or any other session. After a run, run Cleanup before closing windows; the trial files stay under `work/` for evidence.

## Scenarios and what to check

Recorded times are offsets from the first `PermissionRequest` in the smoke runs; treat them as expectations, not requirements.

### 1. Claude human approval (Claude 2.1.310, default permission mode)
Fake model asks for `echo live-trial > live-trial-marker.txt` in the disposable project. When the Claude permission dialog appears, wait about 20 s and choose option 1 (Yes).
- Expect: `PermissionRequest` immediately, `Notification permission_prompt` about 6 s later; timeline goes Provisional (no chip, no announcement) then Confirmed with `reason=Permission announce=True` and chip `needs permission` at about 6 s; the chip clears and the wait disappears after your approval (`PostToolUse`), followed by a normal `Done` chip after the turn.
- Fail signals: chip or `announce=True` before the notification; chip still present after approval; no confirmation after 6 s while the dialog is displayed.

### 2. Claude slow automatic approval (real auto mode, classifier held 20 s)
Auto mode is selected. The fake endpoint receives Claude's real classifier call (`claude-sonnet-5`, system prompt "security monitor") and holds it 20 s before answering with a low severity. Type nothing.
- Expect: no chip, no announcement of any kind during the 20 s hold; the command runs after the hold; then the normal `Done` chip.
- Important observed behavior: in this mode Claude 2.1.310 emitted no `PermissionRequest` and no `Notification` while the classifier was pending (hook timeline shows only `PreToolUse` then `PostToolUse`), so Bell Tower sees no approval flow at all. The scenario proves absence of a false alert for the real automatic path; it does not exercise the provisional-then-timeout path. That path needs variant 2b.

### 2b. Variant, different mechanism, not the provider's automatic reviewer
Default mode with a PermissionRequest hook that allows after 20 s. No human is involved, but this is a hook decision, not Claude's classifier.
- Observed in smoke: `PermissionRequest` at 0.1 s, and Claude still emitted `Notification permission_prompt` at about 6 s while the hook was pending. Bell Tower therefore showed a Confirmed Permission chip with `announce=True` at 6 s although no person was asked, and cleared when the hook allowed at about 22 s. A hook-held decision is indistinguishable from a human wait by the notification. Treat 2b as characterization of this mechanism only; do not read it as a restriction or as the product's automatic-review result.

### 3. Codex human approval (managed owning runtime)
Launched by the installed session function, which starts Bell Tower's dedicated loopback runtime and runs the TUI with `--remote` (a managed, owning runtime; recorded for this trial). Fake model asks for `Set-Content ... live-trial-marker.txt` in a read-only on-request session. When the approval prompt appears, wait about 20 s and choose "1. Yes, proceed".
- Expect: Provisional immediately; the runtime `waitingOnApproval` status confirms within about 1 s; `announce=True` and chip `needs permission` about 5 s after the first positive status (shared five-second grace); clears on approval.
- Fail signals: no confirmation although the prompt is on screen; announcement before the grace; chip persists after approval.

### 4. Codex slow automatic approval (real auto_review reviewer, held 20 s)
`approvals_reviewer = "auto_review"` is set. The fake endpoint receives Codex's real reviewer request (model `codex-auto-review`, JSON-schema output with `risk_level`, `user_authorization`, `outcome`, `rationale`), holds it 20 s, then answers allow. Type nothing. Managed runtime.
- Expect: a Provisional Permission wait for about 25 s (`PermissionRequest` then `PostToolUse` about 25 s later) with no chip, no unverified chip and no announcement; then the normal `Done` chip.
- Variant 4b (`-NoDaemon`): the same without an owning runtime; expect the silent `possibly waiting for approval` chip at 15 s and no announcement, clearing when the review ends.

### 5. Codex `--no-daemon` human approval
No owning runtime. Same prompt as scenario 3; wait about 20 s and approve.
- Expect: Provisional; at 15 s a silent `possibly waiting for approval` chip (`unverifiedChip=True`); never `announce=True` for the Permission wait and no reminder; the chip clears on approval. `doctor` reports Codex confirmation as unavailable without a verified runtime.
- Fail signals: any `announce=True` with reason Permission; wording that calls the wait confirmed.

### 6. Mixed Claude and Codex
Both windows open together (Claude default mode, Codex managed). Approve each after about 20 s, in any order.
- Expect: two independent chips (one per session) each becoming `needs permission` on its own signal (Claude about 6 s, Codex about 5 s after status); approving one clears only that session's chip.

## Mechanism determinations

- **Claude auto mode routes its classifier to the configured endpoint.** Verified with the real 2.1.310 client in a non-interactive run and in the TUI: two non-main requests with model `claude-sonnet-5` reached the fake endpoint after `PreToolUse`, were held 20 s and, on a low severity answer (`<severity>3</severity>`; the classifier requires a severity number with a stop sequence), the command ran. An unrecognized answer fails closed and denies. The real mechanism is therefore usable for scenario 2 with no substitution, but it produces no approval hook (above).
- **First-use notice.** In the TUI a classifier call through a non-first-party gateway first shows an "Enter to continue" notice (auto-mode classifier billing change). The harness pre-seeds the isolated config's acknowledgement so no keypress is needed; without it scenario 2 would stall until Enter.
- **Codex auto review routes to the configured provider.** Verified in the TUI with 0.164.0: the reviewer request went to the fake endpoint, was held 20 s, the command then ran and the marker file was created. No substitution needed.
- **Codex hook shell.** Codex 0.164.0 on this machine runs hook commands through PowerShell 7 (probe: `%COMSPEC%` stayed unexpanded and `$PSHOME` expanded). The hook string Bell Tower generates for Codex on Windows, `"<ingress>" --provider codex --data-dir "<dir>"`, is a PowerShell parse error in that shell (reproduced), and in two isolated Codex TUI runs it produced no capture while a record-only hook in the same group ran; the PowerShell call form `& '<ingress>' --provider codex --data-dir '<dir>'` delivered the events. The harness therefore uses the call form with the same installed helper (`-GeneratedCodexHook` reproduces the product string). Whether the user's real Codex configuration is affected is unverified: production data was not read. This is a candidate defect for the orchestrator, not a harness choice that hides it.
- **Codex hook ordering.** With the installed async registration, `PreToolUse` and `PermissionRequest` ran within about 20 ms and reached the ingress out of order once (the request before the tool start with ingress-only ordering), which the reducer treated as a resolved request and cleared the wait. Synchronous registration (the harness default) keeps the observed order. `-AsyncCodexHooks` reproduces the installed async shape. Treat as a second candidate defect to confirm; it does not depend on the human-versus-automatic question.
- **Mixed trials need no substitution.**

## Harness smoke (non-interactive, no approval answered)

Details are in `implementation-011-live-harness.json`. Summary, fake model only, hidden consoles, unanswered prompts stopped by Cleanup:

| Scenario | Observed |
| --- | --- |
| 1 | Provisional at 0.1 s; `permission_prompt` at 6.0 s; Confirmed, `announce=True`, chip `needs permission` at 6 s; dialog options displayed (Yes first) |
| 2 | Classifier held 20 s; no permission hook events; no wait, no chip; `Done` chip 20 s after Stop |
| 2b | Provisional, Confirmed with `announce=True` at 6 s (prompt notification during a hook hold), cleared at about 22 s |
| 3 | Provisional then Confirmed within the same second; `announce=True` and chip about 5 s later |
| 4 | Provisional about 25 s, no chip and no announcement; reviewer held 20 s |
| 4b | Silent unverified chip at 15 s, cleared at about 21 s, no announcement |
| 5 | Provisional; silent unverified chip at 15 s |
| 6 | Claude Confirmed at 6 s, Codex Confirmed at about 5 s after status; two independent chips |
| Production check | Production tray processes unchanged; no production data file references the harness |

Not performed: the visible-window launch (smoke used hidden consoles through the same start path), a screenshot of the strip, any human or real-model session, any sound.

## Hygiene

Committable text uses repository-relative paths only. Machine-specific paths, per-run configuration, hook and request logs and the copied clients live under the ignored `artifacts/permission-live-011/`. No credentials are recorded; the endpoints use synthetic keys and loopback only.
