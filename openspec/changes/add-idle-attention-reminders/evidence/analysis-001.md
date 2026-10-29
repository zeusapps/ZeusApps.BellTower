# Analysis 001 evidence: idle reminders

Date: 2026-10-28. All checks used frozen target [review-target-001.txt](review-target-001.txt), baseline/head `47dfca9fd46b309017d4d785a1f91fd249d42041`.

## Bounded executable probe

Ran `dotnet run --project artifacts/idle-analysis-001/Probe.csproj -c Release` outside the sandbox. The ignored scratch project references the already-built `bell-tower.dll` and `BellTower.Protocol.dll`; it does not rebuild product projects. It uses synthetic session records, a manual clock, a disposable data directory under its output folder, and an injected idle source. No tray constructor, UI, physical audio, native input generation, authenticated provider, microphone, installation, or network send is involved.

Observed output, with decimal punctuation normalized:

```text
transient omission claude: originalOrdinal=1, recoveredOrdinal=0, identityChanged=True
transient omission codex: originalOrdinal=1, recoveredOrdinal=0, identityChanged=True
future native tick after wrap: accepted=True, elapsedDays=49.71
candidate without fresh native permission evidence: persistedRequiresAttention=False, candidates=1
candidate after config disable before tray tick: candidates=1
playback after source reports recent input: valid=True, currentSourceReads=0
playback with future idle observation: valid=True
settings form replacement preserves unknown reminder field: False
```

### Receipt recovery

For each provider, create one verified-live question session with a fixed opaque episode. Call `ReminderPolicy.Advance` at minute zero to create its receipt and minute five with five minutes idle to issue ordinal one. Call it at minute six with an empty actionable collection, then at minute seven with the unchanged session/episode. The replacement receipt has ordinal zero and a different receipt ID. Only eligibility was omitted; the durable episode never resolved. The removal is at `Core/ReminderPolicy.cs:68`; new random identity is assigned at line 87.

### Escalation validation

Persist a verified-live Codex session whose permission wait is provisional. Set `NativePermissionPending=true` and `NativePermissionSince` on the in-memory projection only. Advance at second five and minute six, establishing a candidate. Reload the persisted session: transient native confirmation is absent and `RequiresAttention` is false, but parameterless `StateStore.Escalations()` returns one candidate. Disable reminders through an isolated `ConfigStore.Update`; it still returns the candidate before another scheduling tick. This probes the exported API directly, without assuming a remote consumer exists.

### Native ticks and snapshot age

Invoke internal `WindowsUserIdleSource.Elapsed` through reflection with full uptime `4294977296` milliseconds and native last-input tick `11000`. The low uptime word is `10000`, so this is a tick 1000 milliseconds in the future. Unsigned subtraction produces `4294966296` milliseconds, accepted because it is less than full uptime. Its apparent inactivity is about 49.71 days.

For playback freshness, create an uninitialized `TrayApplication` through `RuntimeHelpers.GetUninitializedObject` to avoid UI/audio construction. Supply only the fields used by private `IsCurrentAlert`: a verified-live question in `state`, default config, an idle gate, and an injected `IUserIdleSource`. Give its cache 20 minutes idle observed 100 milliseconds ago. The injected source already returns zero idle. Invoking `IsCurrentAlert` on a reminder returns true without reading the current source. Replace the cache timestamp with one hour in the future: it again returns true. No UI method or sink is invoked.

### Settings preservation

Persist a nested `reminders.futureReminder` extension field. Run the exact replacement mapping used by `ReminderSettingsForm.Save`: `ConfigStore.Update(value => value.Reminders = new ReminderSettings { Enabled = true, InitialDelayMinutes = 5, IdleThresholdMinutes = 5, RepeatIntervalMinutes = 10, MaxPerEpisode = 3, EscalationAfterReminder = 1 })`. Reload: `AdditionalData` no longer contains the unknown field. This executes the configuration mapping, not the graphical dialog itself.

## Primary documentation

Used the find-docs skill: `npx ctx7@latest library "Microsoft Windows" ...`, then `npx ctx7@latest docs /microsoft/windows-rs ...`, both outside the sandbox. Context7 returned the official WTS declaration but did not retrieve the required idle-behavior details. Read the current Microsoft Learn reference directly for those details:

- [GetLastInputInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo): scope is the invoking session; event ticks need not increase monotonically, including synthetic input timestamps.
- [LASTINPUTINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-lastinputinfo): two 32-bit fields; caller supplies structure size; last input is expressed as a tick count.
- [WTSQuerySessionInformationW](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationw): current/local server and session selection, pointer/length outputs, and freeing the allocated result are consistent with the declarations inspected.

Native PInvoke execution and real RDP disconnect trials were not performed. These are contract checks and synthetic boundary reproductions, not claims about live desktop acceptance.

## Integrity and scope

Before and after: `head=47dfca9 baseline=47dfca9 files=26 diff=dd04597165e1 untracked=13/471dd618c9a9`. The full after output exactly matched [review-target-001.txt](review-target-001.txt) using `Compare-Object`. Only this evidence and the assigned analysis report are committable writes from this review. Scratch code/build outputs remain ignored. QA owns full regression and the comprehensive content-hygiene sweep.
