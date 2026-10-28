# Design

## Context

AudioQueue serializes notifications on an STA worker and passes an optional phrase to IAudioSink. Config.Voice and Muted are reloaded, but microphone state is absent. WindowsAudioSink owns physical tone and speech. The requested behavior is tone-only during calls; this shared audio feature explicitly applies to both providers without altering either provider adapter.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** none; implement against the shipped 0.1.8 baseline.

**Primary source boundaries:** Tray/AudioQueue, WindowsAudioSink and IAudioSink boundary; Core/AppConfig and config editing; a Windows microphone probe; audio policy/tests and compatibility docs.

## Goals / Non-Goals

**Goals:** Decide speech eligibility at playback, retain tones/preferences and avoid recording audio or requiring an agent integration change.

**Non-Goals:** Do not infer that every microphone use is a specific meeting application, open the microphone, change Windows consent, or replay speech once a call ends.

## Decisions

### 1. Replaceable current-session probe

Add one microphone-activity boundary returning Active/Inactive/Unknown plus observed time. Use documented WASAPI shared-mode capture-session observations across all active capture endpoints visible to the current user/session, with bounded enumeration and explicit handling of session-enumeration freshness. Isolate native COM/device access from Core. Use a short bounded cache (one second by default); stale, failed, incomplete or remote-session observations yield Unknown. A complete successful observation with no active shared-mode capture yields Inactive; this is best-effort inactivity within the observable scope, not proof of global microphone idle. Exclusive-mode capture can be missed. Never open a microphone stream or infer a specific meeting application.

The user explicitly accepted this best-effort contract on 2026-10-28. It replaces the original CapabilityAccessManager registry plan: no primary activity-timestamp contract was established. Microsoft documents that [IAudioSessionControl](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessioncontrol) covers shared-mode streams and excludes exclusive mode; [GetState](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol-getstate) reports running streams in a session. RDP sessions yield Unknown rather than claiming redirected-device coverage. Diagnostics and capability documentation must disclose the observable scope and exclusive-mode limitation.

### 2. Tone-only is a playback decision

Add microphoneAwareVoice default true. Immediately before each single or aggregate playback, combine Voice, Muted and a fresh probe result. Only Inactive permits speech under enabled suppression; Active/Unknown yields the normal allowed tone with phrase null. An explicit opt-out restores ordinary Voice behavior. Keep preference values unchanged so a future inactive notification may speak; never queue suppressed phrases for catch-up. Extend cancellation checks so microphone becoming active while speech is queued or in progress can stop remaining speech while respecting tone and mute boundaries.

### 3. Expose capability and retain existing controls

Provide a clear persisted toggle in configuration/CLI or tray settings and show Active/Inactive/Unknown with freshness and a safe reason in doctor. Do not log app paths or registry values. Explicit Test sound follows its documented audio choices and is never invoked by diagnostics. Default tests inject the probe and use recording/silent sinks.

### 4. Compose with later reminder playback

Keep the policy at AudioQueue/sink boundary so reminders and aggregates cannot bypass it. Expose a small speech-eligibility policy rather than duplicating microphone checks inside each provider or reminder scheduler.

## Risks / Trade-offs

- [Observation is stale, incomplete, fails or occurs in RDP] -> Return Unknown with bounded diagnostics and tone-only default; retain opt-out.
- [Exclusive-mode microphone capture is not observable] -> Disclose this accepted best-effort limitation; do not claim global idle or guaranteed call detection.
- [Call begins after enqueue] -> Recheck at playback and before speech; cancel remaining speech where supported.
- [Silent tests accidentally open devices] -> Inject all native/device boundaries and make physical sink creation explicit.

## Migration Plan

Missing configuration means suppression enabled for new playback, preserving stored Voice/Muted choices and user tone selection. Persist fields through live reload and old/new JSON writers where compatible. Rollback removes the additional policy without changing user audio assets.

## Verification and fresh-context notes

Fake active/inactive/unknown/stale states, queued state transitions, aggregate alerts, mute/voice combinations, opt-out, device-access exceptions and reminder compatibility. A read-only platform probe may verify documented session observation in an isolated test profile; no microphone stream or physical sound is allowed.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 11. This shared audio feature applies equally to both providers.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
