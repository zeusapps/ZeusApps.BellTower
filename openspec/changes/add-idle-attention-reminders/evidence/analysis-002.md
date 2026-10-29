# Analysis 002 evidence: assigned reminder corrections

Date: 2026-10-28. Recheck of ANALYSIS-001–005 on frozen [target 002](review-target-002.txt). Baseline/head `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Executed independent probes

Command: `dotnet run --project artifacts/idle-analysis-002/Probe.csproj -c Release`, outside the sandbox. The ignored scratch project references the existing Release application/protocol assemblies. It does not build product projects or overlap QA's broad suite. Data directories are unique disposable folders below the scratch output; no default/production data is accessed.

Observed output:

```text
capped restart claude: ordinal=3, idPreserved=True, futureDeadline=True
settings mapping claude: unknownPreserved=True
capped restart codex: ordinal=3, idPreserved=True, futureDeadline=True
settings mapping codex: unknownPreserved=True
candidate current confirmation permitted=True
candidate missing confirmation withheld=True
candidate negative confirmation withheld=True
candidate muted permitted=True
candidate disabled withheld=True
future tick rejected=True
short wrap preserved=True
25h idle permitted=True
future observation rejected=True
actual source after input100ms: cachedIdleMinutes=20, lowLevelReads=1
actual wrapper playback after input100ms: valid=True, lowLevelReads=1
```

### ANALYSIS-001

For Claude and Codex separately, persist a synthetic verified-live question session and fixed episode. Advance at minutes zero, five, fifteen and twenty-five to reach cap three. Omit the current projection at minute twenty-six while leaving the persisted lifecycle unchanged. Construct a fresh `StateStore` and advance after a 90-day absence with a new run ID. The persisted ordinal stays three, the opaque ID remains equal, and the next deadline is future. Product retention now comes from the persisted session set, independent of immediate scheduling eligibility.

### ANALYSIS-002

Persist a Codex permission wait with provisional confirmation. Supply a separate current projection with `NativePermissionPending=true`/`NativePermissionSince` to earn the candidate. The new `Escalations(currentSessions, now)` API returns it only with current confirmation; the unconfirmed projection and a fresh negative projection return none. Muting through isolated config retains candidate availability. Disabling reminders through config with no intervening scheduler call immediately withholds it. Enumeration intentionally receives no new idle threshold: candidate earning already required idle, and the agreed contract does not restart that gate on each query.

### ANALYSIS-003

Reflection invocation of `WindowsUserIdleSource.Elapsed(4294977296UL, 11000U)` now returns null. A legitimate wrap sample returns 21 milliseconds. `UserIdleSnapshot.IsFresh` accepts 25 hours idle at a current timestamp and rejects a future timestamp by one tick. Source inspection confirms half-uint elapsed bounds, nonnegative observation ages and negative/out-of-range idle rejection. Native declarations and current-session mapping retain the prior reviewed scope; no real input or native desktop test was performed.

### ANALYSIS-004: still failing through the actual source wrapper

Use the actual `WindowsUserIdleSource` with its internal low-level `Func<UserIdleSnapshot>` seam and manual clock. First observation returns 20 minutes idle at time T. Start and finish its worker, then harvest the result. Set the low-level observer's next result to zero idle (representing input) and advance the manual clock to T+100 ms. Calling the real source returns the previous 20-minute sample and leaves the low-level read counter at one because its retry throttle is 250 ms.

Inject that actual source into an uninitialized `TrayApplication` fixture, with only fields used by private `IsCurrentAlert` populated. Invoke the predicate for a current verified-live question reminder. It returns true and still performs no new low-level observation. Thus the tray now calls the source, but the wrapper can authorize playback from the same pre-input cache.

This is separate from unavoidable input arriving after a fresh native sample has been taken. The reproduced case contains no fresh sample attempt at the playback boundary at all: the source deliberately reuses a previously completed sample. The test uses no physical input and constructs no UI or audio sink.

Smallest bounded correction direction: keep the cached source method for periodic status if useful, but provide a playback fresh-read boundary that never grants eligibility from a sample preceding that request. Request/join the single-flight worker with a small fixed completion budget, or use a similarly bounded fresh local-input read while keeping potentially blocking WTS queries on the worker. If no qualifying current sample completes within the budget, return unknown and drop that reminder. Do not add an unbounded WTS call or wait to the audio callback. Verify the real source wrapper with the above idle-to-input transition, blocked worker/deadline, and single-flight cases, plus synchronized before-playback/during-tone silent-sink cases.

### ANALYSIS-005

For both disposable configurations, persist `reminders.futureReminder` extension data. Invoke the actual internal static `ReminderSettingsForm.ApplySettings` through reflection inside `ConfigStore.Update`, then reload. The unknown nested field survives. Source inspection confirms the real form's Save handler invokes that mapping and that Update validates a clone before replacing last-good state.

## Scope and integrity

Before and after, full `review-identity.ps1` output exactly matches [target 002](review-target-002.txt): `head=47dfca9 baseline=47dfca9 files=29 diff=2cabb1effe53 untracked=16/08007d8b2afe`. Only reserved report/evidence and ignored scratch outputs were written. No implementation, task, ledger or historical-report mutation occurred. QA owns full regression, build, format and the broad hygiene scan; these probes are independent bounded evidence only.
