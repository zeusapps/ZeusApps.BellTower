# Native launch investigation

Recorded 2026-10-25. Installed clients: Claude Code 2.1.310 and Codex CLI 0.164.0. This is planning evidence, not implementation acceptance.

## Outcome and user intent

The requested experience is running the original `claude` or `codex` command in an ordinary terminal and having Bell Tower react to its hooks without dedicated terminal profiles. Hook delivery can discover a conversation. A hook's process context can sometimes establish a terminal reference even though the JSON payload does not carry one. It is incorrect to conclude that hooks can never identify a terminal or that a shell wrapper is necessarily required.

The difficult topology is a backend shared by multiple terminal clients. Its hook ancestry and inherited environment need not identify the client displaying a particular conversation. This investigation did not establish a complete shared-Codex conversation-to-terminal binder, but it also did not prove such a binder impossible.

A transparent shell wrapper was discussed as an alternative: a PowerShell function shadows a native command and supplies launch/exit metadata. The user did not select it. This change instead plans hook-triggered adoption with verifiable provider/host evidence; command shadowing is not an assumed requirement.

## Findings and evidence quality

| Finding | Evidence | What it does not establish |
| --- | --- | --- |
| Claude emits a native SessionStart without Bell Tower's launch wrapper | Actual native `--print` process, isolated settings, ingress capture; hook exit zero; observed agent PID 28764 | Interactive host navigation, clean SessionEnd, colors or restoration |
| Codex emits a native SessionStart without Bell Tower's wrapper | Actual TUI using a private shared daemon; normal `/hooks` review/trust; ingress capture | Exact terminal binding from that hook |
| A second local client can connect to an existing Codex shared backend | Windows Unix-domain socket, HTTP WebSocket upgrade, initialize/initialized handshake | Ownership of that backend or permission to stop/reconfigure it |
| Exact-conversation state can be read without resume | `thread/loaded/list` followed by `thread/read` with history omitted; [shared proof](native-probes/codex-shared-proof.json) | Human approval behavior or a terminal being open |
| Native name writes work through the existing shared backend | `thread/name/set` acknowledged, read-back confirmed, native TUI emitted the new title | Native-name authorship or permission to overwrite a real user's title |
| Concurrent conversations remain separately addressable | Two exact IDs retained independent A/B names and status; [parallel proof](native-probes/codex-parallel-proof.json) | A one-to-one mapping from all loaded threads to visible terminals; a third loaded thread was also observed and its purpose was not determined |
| Terminal lifetime differs from conversation lifetime | Both TUIs exited with a disconnected/work-continues message; threads remained loaded and another thread remained active; [post-exit proof](native-probes/codex-after-terminal-exit.json) | Continued work belonging specifically to either closed tab, or a complete client subscription inventory |
| Inspected hook and thread metadata do not provide a terminal/client PID or window handle | Published hook fields, generated protocol schema, observed thread keys | Impossibility of a host-assisted protocol or another provider API |

See [native probe summary](native-probes/summary.json), [sanitized real hook captures](native-probes/hook-captures.json) and [Claude process result](native-probes/claude-native-proof.json). These are retained snapshots; raw transient client homes and captures remain local at `artifacts/native-launch-investigation-8aaffaa9f051419fa3831d4ad8a5d465/` and the shorter Codex home `artifacts/nli-8aaffaa9/c/`. Planning and review must not depend on those ignored local directories continuing to exist.

## Probe procedure and limits

Native processes used separate provider configuration directories, dummy credential values and an unreachable loopback model endpoint at port 9. Inherited provider, Bell Tower and terminal metadata were removed from the probe children; the Codex launchers received distinguishable synthetic terminal markers. No real authenticated model endpoint or personal provider configuration was used. Captures were written to a private Bell Tower data directory.

For Codex, two pseudo-console TUI clients reused the same private managed daemon. Its user-level SessionStart/SessionEnd definitions pointed at the existing NativeAOT ingress. The native `/hooks` UI was used to trust the definitions; hook trust was not bypassed. A separate observer connected to that daemon's local control socket, initialized, listed loaded IDs, read metadata with history omitted and set disposable A/B names. The first native title update was visible in the TUI's terminal output. After both TUIs disconnected, another observer read the retained runtime states. No production backend was contacted.

For Claude, the native headless CLI used explicit isolated hook settings, no tools and no user/project setting sources. The SessionStart handler exited zero and captured the actual native Claude process identity and ancestry. The CLI was terminated after the bounded model connection retry interval, so its `exitCode: -1` / `endedWithinBudget: false` describe probe cleanup, not hook failure or a proven clean session exit. Earlier empty-input runs did not establish hook delivery.

Initial sandbox runs could not create or inspect the private Codex daemon and caused the Claude handler to exit 66. Retrying outside the sandbox established the successful native results; these sandbox failures are not client capability restrictions. An overlong private Codex home exceeded the Windows Unix-socket path limit, so a shorter isolated home was used. A quoted initial Codex probe command exited one; the private unquoted-path command delivered the accepted capture. This is not evidence that production setup quoting is broken or validated; generated installed commands and quoted/Unicode paths need their own acceptance.

All probe TUIs and Claude processes exited or were stopped by the probe. The private backend required cleanup after its graceful shutdown request did not complete promptly; its creation time and executable path under the private home were checked before terminating it and its children. Bell Tower must never apply that ownership cleanup to a backend it merely observes in normal operation.

No successful model turn, real human approval prompt, actual VS Code/Windows Terminal focus operation, terminal color/restoration trial, full standalone Codex attachment trial, or full provider/host matrix was completed. Those remain unverified. Native Codex's first hook was observed after initial input rather than as a pre-launch decoration opportunity.

## Repository implications

- `AttachmentCapture` already obtains process ancestry and creation identities without requiring `BELLTOWER_ATTACHMENT`; the existing VS Code bridge publishes terminal shell identities. Direct native auto-binding is therefore a concrete avenue, not a new product flow invented from scratch.
- Both ingress and legacy ingestion deliberately clear Codex host/process evidence unless the wrapper supplied the dedicated flag. This guards against shared-daemon metadata leakage but also means a plain `codex --no-daemon` launch is not already automatically classified as a verified standalone attachment.
- The inspected native TUI metadata reported `originator: codex-tui` with `source: vscode`. Do not route a terminal from a source label.
- Existing runtime stores and `CodexConnection` assume Bell Tower owned loopback runtimes. Add shared access behind their transport/lookup boundary and keep lifecycle ownership separate.
- Existing attachment health can expire an unowned processless observation even when runtime evidence could prove the conversation live. Conversation evidence and terminal health need distinct interpretation; do not weaken destination verification.
- The current managed wrapper prepares startup colors and restores the shell in `finally`. Hook-only adoption does not automatically reproduce pre-start colors or safe restoration. These require surface-specific proof and explicit presentation policy.
- Global hooks work across projects once installed/trusted. Monitoring must still run independently: current ingress deliberately persists captures without starting the tray per event.
- PowerShell's current module import uses `-NoClobber`. Adding functions named `claude` and `codex` would intentionally affect command precedence; this proposal does not select that alternative.

## Primary documentation consulted

- [Claude Code hooks](https://code.claude.com/docs/en/hooks): user-level scope, common input, SessionStart and environment-file behavior. `CLAUDE_ENV_FILE` supplies later tool subprocesses; it is not a general parent-shell mutation channel.
- [OpenAI hooks](https://learn.chatgpt.com/docs/hooks): user-level hooks, trust, common fields and lifecycle events.
- [OpenAI app-server](https://learn.chatgpt.com/docs/app-server): local transports, initialization, non-resuming thread reads, runtime status, native names and loaded-thread lifetime.
- [OpenAI CLI reference](https://learn.chatgpt.com/docs/cli/reference): native launch, standalone and remote modes. Installed `--help` also exposed `app-server daemon` and `app-server proxy` on 0.164.0.
- [Upstream daemon documentation](https://github.com/openai/codex/blob/main/codex-rs/app-server-daemon/README.md), retrieved through Context7: shared clients use the daemon's inherited environment rather than per-client environment. The indexed main-branch documentation is supporting context, not proof of every installed-version behavior.
- [PowerShell command precedence](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_command_precedence): aliases/functions can shadow native commands.

## Next bounded evidence gate

Verify direct native ancestry and bridge matching for Claude and standalone Codex in both local hosts. Separately establish whether shared Codex exposes a trustworthy conversation/client binding or whether the host bridge can provide one without input injection, guessed names or changed launch commands. Keep full native terminal management unverified until that proof exists; seek a user scope decision if the required behavior cannot be met. Do not convert missing investigation into a hard provider restriction or silently introduce wrappers.
