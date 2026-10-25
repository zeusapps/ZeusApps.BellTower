# Design

## Context

vscode/focus.js calls terminal.show(false), workbench.action.focusWindow and show(false) again, then tests window.state.focused plus activeTerminal. The user confirms correct terminal selection but the editor stays behind another application or minimized. VS Code documents activeTerminal as current or most-recent focus, so it is insufficient proof of keyboard input focus. VsCodeNavigator currently has no native foreground fallback. service.js atomic() lacks cleanup around renameSync, and a registration failure aborts the tick before focus requests. HostRegistration identifies the extension-host process, not an independently verified top-level window.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `reconcile-session-liveness-and-title-refresh`.

**Primary source boundaries:** vscode/extension.js, focus.js, service.js and Node tests; Hosts/VsCodeNavigator, HostRegistration, WindowProbe and activation interfaces; packaged bridge acceptance.

## Goals / Non-Goals

**Goals:** Bring forward the exact owning editor after a chip click and make bridge writes recoverable without weakening destination verification.

**Non-Goals:** Do not use window titles, Code process name alone, saved terminal indices, global terminal.focus routing, keyboard injection, or arbitrary foreground stealing.

## Decisions

### 1. Prove the owning window before native activation

Extend registration/handshake with a verifiable editor-instance window mapping. Use supported VS Code/Electron window activation where reliable and a native helper only after establishing ownership from an instance-bound capability/handshake plus process/window evidence. The extension-host PID is not itself the main-window PID; process ancestry alone can share several windows. If the installed API cannot prove an HWND mapping, return unavailable rather than picking one. Prototype this mapping in an isolated two-window fixture before committing a fallback implementation. Preserve creation-time and request expiry checks.

### 2. A bounded user-initiated activation protocol

From the tray's click path grant/perform only the verified activation needed for that request; restore an iconic/minimized owner only and verify the actual foreground HWND afterward. Then the owning bridge reveals the exact terminal, including editor-area terminals. Keep window activation and terminal reveal acknowledgments distinct internally. A positive result requires proven foreground plus exact reveal; activeTerminal alone does not prove keyboard input focus. Record that stronger input-focus limitation if the public API cannot expose it. Preserve maximized/fullscreen state.

### 3. Recoverable atomic files

Wrap creation/write/rename in cleanup that removes only the uniquely owned temporary path on failure. Do not delete the previous valid registration/result to force a rename. Retry on a later bounded tick with rate-limited error status. Separate registration publication from focus request iteration sufficiently that a transient write failure cannot indefinitely starve still-verifiable requests; never consume a request against expired ownership. Apply size, instance, PID/start and expiry validation before taking action.

### 4. Compatibility and receipt semantics

Version any new registration/request fields, negotiate capability and retain old bridge behavior as an explicit partial/unavailable path where necessary. Do not change provider transport. The bridge feature applies to both Claude and Codex hosts. Hide a chip timer only on the current episode's successful navigation; window-only or denied activation retains the wait and appropriate honest precision.

## Risks / Trade-offs

- [Wrong Code window activated] -> Require an instance-bound mapping and duplicate-window fixture before allowing native calls.
- [Windows foreground policy rejects the request] -> Return a failed receipt and retain the chip instead of claiming success.
- [Tests only simulate focus] -> Separate Node protocol tests, native disposable-window tests and required manual background/minimized acceptance.

## Migration Plan

Package the matching VSIX, preserve extension ownership and unrelated VS Code settings, and report editor reload needed for activation without forcing it or restarting agents. Keep registrations from older bridges readable until capability expiry; native fallback is disabled when ownership cannot be proven.

## Verification and fresh-context notes

Node tests must inject write/rename failure and assert zero leaked .tmp files and next-tick recovery. Native/process fixtures must cover expiry, PID reuse, denied foreground, two duplicate-name windows, panel/editor terminals and minimized versus visible maximized/fullscreen state. Live acceptance must reproduce a window behind another application and minimized under Windows 11/RDP. Automation targets disposable hidden/controlled windows only and never types into a production terminal. Reference checked docs: https://code.visualstudio.com/api/references/vscode-api.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Original user foreground-activation report; field report 5. Host behavior applies to both providers; provider APIs stay unchanged.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.
