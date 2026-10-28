# Tasks

## 1. Probe and policy contract

- [x] 1.1 Read AudioQueue/WindowsAudioSink/config boundaries and this design; verify the implementation plan applies at shared playback without changing either provider.
- [x] 1.2 Verify documented Windows shared-mode capture-session observation across active capture endpoints and its exclusive-mode/session-enumeration/RDP limitations against official documentation and fixtures; preserve the user-approved best-effort active/inactive/unknown scope without reading or recording audio.
- [x] 1.3 Add a bounded replaceable current-session probe and safe failure state; verify fake endpoint/session/probe exceptions, count/time bounds, freshness and stale or incomplete observations cannot become false inactive evidence. RDP yields unknown; disclose that exclusive capture may be missed.

## 2. Playback integration

- [x] 2.1 Add persisted microphone-aware suppression default enabled and an explicit opt-out/status control; verify live reload leaves stored Voice/Mute values and unrelated settings unchanged.
- [x] 2.2 Evaluate speech eligibility immediately before single/aggregate playback and suppress speech for active/unknown state while retaining allowed tones; verify all Mute/Voice/opt-out combinations with a recording sink.
- [x] 2.3 Recheck/cancel queued or remaining speech when microphone activity changes and discard suppressed phrases permanently; verify call-start races and no catch-up speech when the call ends.
- [x] 2.4 Keep the policy reusable by reminder AudioItems; verify normal and reminder-like aggregate items follow the same eligibility boundary.

## 3. Acceptance

- [x] 3.1 Run audio worker/queue tests using only fake probes and silent or recording sinks; verify no test constructs a live microphone stream or triggers physical sound.
- [x] 3.2 Document capability limitations, default tone-only unknown behavior and opt-out; verify formatting, Release build and audio/config regression tests for both providers.


## 4. Provider parity acceptance

- [x] 4.1 Implement and verify this feature for both Claude Code and Codex using shared policy and provider adapters; run matched provider fixtures plus a mixed-session case and retain version/mode-specific evidence.
- [x] 4.2 Audit setup, defaults, configuration, diagnostics and controls for equal feature availability; document any hard provider restriction with its proof, honest shared fallback and regression check, and treat missing implementation or unverified capability as incomplete work.
