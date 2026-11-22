# Manual native-session acceptance checklist

Evidence template for tasks 4.1, 4.3, 4.4, 4.5, 5.1 and 6.1, including QA-5100. This does not mark any task complete or replace tasks.md.

## Before testing

- Install a development build containing the final DEV-056 changes. The changes from the 2026-11-15 Codex session were not installed during that session.
- Refresh integration with `setup --host-bridge --apply` using that installed build. Accept changed Codex hooks through its normal trust flow if requested, and reload VS Code windows after updating the bridge.
- Record the Bell Tower build, Claude and Codex versions, terminal host and whether the terminal is elevated.
- Confirm `bell-tower doctor` reports hook-start available. An unavailable launcher record is a setup problem to resolve before the startup trial.
- Use test conversations and a test directory. Save work before the restart/sign-in checks.

## Coverage matrix

Run the applicable tests in both Windows Terminal and the VS Code integrated terminal for each command below. Record the actual mode reported by doctor; a command alone does not prove its runtime mode.

| Command / actual mode | Expected conversation behavior | Expected terminal behavior |
| --- | --- | --- |
| Plain `claude`, direct | Tracking and completion/real permission-wait alerts | Title, color and navigation when presentation is on |
| `codex --no-daemon`, direct | Tracking and completion/real permission-wait alerts | Title, color and navigation when presentation is on |
| Plain `codex`, verified shared backend | Tracking, alerts, supported name sync and permission status | No title or color; click explains the navigation limitation |

This is six host/command combinations. An untested combination stays **Not performed**.

## Hook-start tests: priority

### H1. First hook after sign-in starts a stopped tray

1. Restart Windows or sign out and back in.
2. Confirm Bell Tower is not running before launching the client. If another startup mechanism already started it, record this cold-start check as not performed.
3. Start the client in the selected host and submit a harmless prompt if startup itself emits no hook.
4. Observe Bell Tower and the client through completion of the prompt.

Expected: exactly one tray appears, the session appears after queued captures are consumed, and the client continues normally without a console flash or waiting for Bell Tower to close. The tray remains running after the hook returns and after the client exits.

Use a fresh sign-in for the cold-start check. Choosing Exit sets suppression and belongs to H3.

### H2. Further hooks reuse the running tray

1. With Bell Tower running, submit several harmless prompts.
2. Start another client session and trigger hooks there too.

Expected: no duplicate tray/process, no repeated startup windows, responsive clients, and separate session entries.

### H3. Tray Exit suppresses automatic restart

1. Choose **Exit** from the tray menu.
2. Trigger another hook from each provider during the same Windows sign-in.

Expected: Bell Tower stays stopped and both clients continue normally. A later manual start consumes queued captures. Doctor identifies the current-sign-in user stop.

### H4. Command-line Exit also suppresses restart

1. Start Bell Tower manually.
2. Run the installed `bell-tower exit` command.
3. Trigger another hook from each provider.

Expected: the same suppression as H3. This specifically checks the second user-stop entry point.

### H5. Manual start and a new sign-in clear suppression

1. After H3 or H4, start Bell Tower manually and confirm queued sessions appear.
2. Check that doctor no longer reports a current user stop.
3. Choose Exit again, then sign out/in or restart Windows.
4. Repeat H1 with another provider/host combination.

Expected: the previous sign-in's stop does not prevent the first hook from starting the tray.

### H6. Concurrent first hooks produce one tray

1. In a fresh sign-in with no tray running, prepare Claude and Codex sessions in separate terminals.
2. Trigger their first hooks close together.

Expected: exactly one tray, both session entries, and neither client's hook hangs. Record which hosts and Codex mode were used.

### H7. Elevation preserves user intent and normal tray privileges

1. Start the tray normally, choose Exit, then trigger hooks from an elevated terminal using Claude and Codex.
2. Confirm neither elevated hook restarts the tray in the same sign-in.
3. In a fresh sign-in with no tray, trigger a hook from an elevated terminal.
4. Check the new tray process's elevation in Task Manager and record doctor's latest hook-start result.

Expected: Exit suppression also holds across elevation. Any automatically started tray is non-elevated. Record a refused launch as **safe refusal / successful startup unverified**, not a successful H1 result. Testing this does not require changing Windows UAC policy.

## Session and presentation tests

### S1. Discovery, completion and real permission waits

For each matrix row, verify the session appears; complete a harmless prompt; then use a harmless operation that actually requires human permission under the client's current policy.

Expected: completion and real human-wait alerts occur with the correct session identity. An automatically approved operation is not evidence of the human-wait case.

### S2. Title/color on and off, with visible click results

For direct Claude and Codex sessions:

1. With **Title and color native sessions** on, trigger a hook and check title and color.
2. Click the session from Bell Tower while another tab/window is selected.
3. Switch the setting off while the session remains open; trigger another hook if needed.
4. Click again, then switch the setting back on.

Expected: presentation begins after the first accepted hook. In Windows Terminal, on enables managed-tab selection; off permits window-only focus with a visible explanation. VS Code reveals the registered terminal. A click always produces focus or a stated reason. Switching off restores the prior title and resets a Bell Tower-written background to the terminal default. Switching on applies to the live session or explicitly explains a restart requirement.

For verified shared-backend Codex, the setting changes neither title nor color, and clicking explains its navigation limitation.

### S3. Restoration when the session ends

1. Start a direct session with presentation on and note its prior shell title.
2. Exit the client normally while keeping the shell/tab open.

Expected: the prior title returns and the background returns to the terminal default. Other tabs and sessions retain their own appearance. Existing presentation opt-outs remain effective.

### S4. Same-directory and mixed managed/native sessions

1. Run two direct sessions in the same directory, including sessions of the same provider.
2. Add a session started through an existing Bell Tower managed profile or launch function.
3. Trigger work/waits in different sessions and click each entry.
4. Exit one session while the others remain open.
5. Include multiple host windows and matching titles where practical.

Expected: correct session destinations or honest navigation limitations; independent identities, colors and waits; no stale metadata inherited by native/nested sessions; no duplicate/replayed alerts. The managed session retains its normal coloring, controls and exit restoration.

### S5. Defaults and saved opt-outs

Check an isolated configuration with no saved native-presentation value, then one with an explicitly saved off value.

Expected: the missing value defaults to on; the saved off value stays off across restart. Per-session opt-outs continue to win. Record this as not performed if only the existing configuration was tested.

## Setup acceptance: separate disposable setup

### I1. Preview, apply, repeat and remove

Using disposable provider settings/profiles, preview native hook/bridge setup, apply it, repeat the preview/apply, then remove one provider's integration and finally the remaining integration.

Expected: the preview describes actual changes; a repeat is a no-op; unrelated settings, profiles and aliases survive; removing one provider preserves the other's integration and shared bridge settings; final removal restores unchanged owned settings. Record each provider and hook-trust result separately. Use the installed build's supported removal options; this checklist does not prescribe guessed flags.

## Recording results

Use **Passed**, **Failed**, or **Not performed** for each applicable host/command/test combination. A safe startup refusal is a useful diagnostic, but does not pass successful-start acceptance.

| Test | Host | Command / actual mode | Elevated? | Build/provider versions | Result | Observed behavior / hookStart result |
| --- | --- | --- | --- | --- | --- | --- |
| H1 | Windows Terminal | plain claude / direct | no | fill in | Not performed | fill in |

For a startup failure, record doctor's `hookStart` availability, latest attempt result/detail and suppression state. Keep private paths, prompts, tokens and unrelated configuration out of retained results.
