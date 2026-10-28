# Analysis 008 source and evidence checks

Reviewed on 2026-10-28 UTC. This was a read-only source/evidence review; no native provider, sandbox child, endpoint, build, test, installation, or migration was launched.

## Evidence identity

Independent `Get-FileHash -Algorithm SHA256` results matched the assignment:

| Record | SHA-256 |
| --- | --- |
| [Investigation 005](native-investigation-005.md) | `C08BB8435C1FB2CFBE84FE6236101C13BC39971491331389E6320FBE3E628DFB` |
| [Investigation 006](native-investigation-006.md) | `ADA8D6EB3ABD6C73A16D16283912D2ED89FB9167323373D2737A5051428A7B7B` |
| [Investigation 007](native-investigation-007.md) | `42D2C1D00D40EBE6213CDF3A8C7AD273D48461860AA93EC80EC8D720829FD215` |

The suggested ignored source-cache directories ending in `fallback-005` and `fallback-006` were absent. Official version-pinned source was reopened directly instead. No source cache or prior evidence was edited. This is code review, rather than a question about current CLI usage or API syntax; no additional Context7 documentation command was needed to assess the pinned implementation.

## Independently checked pinned source

| Trace | Result |
| --- | --- |
| [Thread request](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/thread_manager.rs#L339-L373), [test helpers](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/thread_manager.rs#L1308-L1348) | The normal constructor leaves the private override unset. The cited setters are internal helpers expressly named for tests. This does not establish a public override. |
| [Session initialization](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/session/session.rs#L1393-L1457) | Session startup selects override-or-default and passes that shell into `ThreadEnvironments`. |
| [Local versus remote resolution](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/environment_selection.rs#L907-L943) | Nonremote resolution retains `local_shell`; remote resolution can consume executor metadata. |
| [Windows default selection](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L303-L353), [PowerShell discovery](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/shell-command/src/shell_detect.rs#L189-L272) | The Windows default branch selects PowerShell, otherwise ultimate cmd. Bash is absent from that branch. Discovery checks executable paths rather than testing their language; substituting a renamed executable would not prove cmd selection. |
| [ThreadStartParams](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/app-server-protocol/src/protocol/v2/thread.rs#L59-L156), [TurnEnvironmentParams](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/app-server-protocol/src/protocol/v2/turn.rs#L90-L96) | Neither inspected request shape supplies a shell input. An environment ID selects an environment rather than supplying its shell metadata. Absence here is not proof of a universal provider restriction. |
| [Environment shell conversion](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/core/src/shell.rs#L57-L71) | Executor metadata explicitly admits Bash and cmd, among other types. External-host coverage therefore cannot be dismissed from the local-default trace. |
| [Debug sandbox launch](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/cli/src/debug_sandbox.rs#L459-L503) | The Windows branch transforms the supplied child argv through the MXC manager and launches the returned wrapper. This supports the proposed unchanged-child route, not runtime availability. |
| [MXC policy](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/mxc-sandbox/src/policy.rs#L266-L312), [native launch](https://github.com/openai/codex/blob/rust-v0.164.0/codex-rs/mxc-sandbox/src/native.rs#L15-L36) | The request carries denied paths and disables DACL mutation. A nonempty deny list requires native denied-path support before spawning the workload. Runtime support remains separate evidence. |

## Investigation 007 static control check

Read-only inspection of its retained scratch config confirmed the named filesystem profile, root/System32 read grants, scratch write grant, both PowerShell directory denies, and network disabled. The actual system-directory spelling uses uppercase `WINDOWS`; this case difference does not contradict the recorded Windows paths. The expected marker was absent when checked. No raw profile or environment dump is retained here.

The recorded command uses a direct resolved executable and an argument vector, then a cmd visibility test. The bounded plan records process identity before waiting and forbids broad process termination or host ACL changes. No concrete profile, argv, or lifecycle defect was found that establishes a cause for `CreateProcessW` error 5. In particular, a cmd language-parsing failure would require cmd to have started; the recorded failure is at workload creation. That observation does not identify which access check failed.

The two reported outcomes were not rerun by this analyzer: 95 ms/backend unavailable inside the tool sandbox, and 216 ms/CreateProcessW error 5 outside it, both without a marker. Marker absence alone proves no visibility result was reported, not the exact child lifetime or error cause. Neither outcome demonstrates shell selection or native hook delivery.

## Late QA008 evidence review

The orchestrator requested review of the newly returned [QA008](../reports/qa-008.md) and [baseline evidence](qa-008-mxc-baseline.md). Their SHA-256 values at inspection were respectively `3EC5AE05FDFF11BD7E5805B208E89A8070E8D70E008FE0F70431F8F5B04B4ED8` and `4DF12A24F23C2023AC7F1155EFC28408BC1CACB89D78E87E2C6A061F09EDCC57`.

QA records exit 1, no marker, and filename/directory/volume-label syntax stderr from its no-deny marker command. That establishes failure of the whole baseline, not whether cmd was created. The report's line40 inference of an executor child-startup problem is stronger than the evidence. Cmd redirection/command parsing after launch remains an alternative explanation; no quoting defect is proved because the exact command is not retained in that evidence. Investigation007's explicit CreateProcessW error5 is a different result and must not be merged with it. A quote-free stdout control can distinguish successful child launch from the marker-command failure. This analyzer did not run it; any later result requires its own evidence.

## Acceptance interpretation

For unchanged Codex CLI 0.164.0, ordinary local Windows operation without an internal override or external environment has no default Bash-selection branch. A native Bash-selection test of precisely that branch can be classified **not applicable from source**, with that version/mode boundary. Generic Bash execution through an external host remains unverified; this is not a provider exception or native Bash pass.

Native cmd remains a real Windows fallback branch. Task 2.1 still needs actual native cmd selection plus valid durable hook capture and the required path controls; direct cmd argument tests and safe setup refusal do not meet that criterion. The failed MXC controls do not waive it. Investigation 006 explicitly corrects investigation 005's WSL registration count; neither count proves an alternate runtime route.

The [live acceptance record](user-live-acceptance-001.md) supports task 5.2 for the migrated setup: the user's both-client observation, Codex screenshot, and fresh receipts are distinct evidence kinds. It supplies no fallback attribution or separate trust/permission-wait result. Existing benchmark and QA results retain their recorded scope.
