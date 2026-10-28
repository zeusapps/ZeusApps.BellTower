# QA-011 native cmd evidence and regression checks

## Native evidence review

Reviewed [native investigation 009](native-investigation-009.md); its SHA-256 is `9c1d9a5617bfe191842bc0889262f53621db798b1292c0ea08c84e501593eea3`, matching the assigned evidence hash. The retained raw artifacts are under ignored `artifacts/codex-hook-fallback-009/session/` and were not changed by this review.

Independent checks of the raw files and binaries found:

- Direct Codex CLI version/hash: `0.164.0` / `6f267830b029ee25d9339864aa867cd45de9f8fd4c5ea007d2e63c13a0c66d36`.
- The capture helper SHA-256 is `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707`.
- Exactly one capture file exists. Its SHA-256 is `13df2846296c98bcd94a69aff40856d9c80ebc2df930a5b3d344a0e4b285e651`; length is 2,176 bytes.
- A scratch-only harness referencing the existing test-output `BellTower.Protocol.dll` called `CaptureProtocol.Decode` on the raw capture and exited successfully. It returned protocol 1, provider `codex`, registration source `startup`, normalized event `Register`, matching capture/observation IDs and attachment/observation directories, and one `cmd.exe` ancestor whose PID/start-time identity matches `HookShell`.
- The recorded hook-command file SHA-256 is `8a1a41ef83c2a21f447e5bae06c246849c8931e3454908f45bda55869b3f8dc4`. The hooks JSON contains the same command and hash. Re-running the retained builder harness against the current compiled Bell Tower assembly regenerated the same command hash exactly; that current assembly SHA-256 is `52a3be0e0ba1bd26bb2b5a72ed37513654768e380a3d021c56f3a83a827eb2da`.
- The special-character helper and capture-data paths retain spaces, apostrophe, Unicode, the literal environment-style `%BELLTOWER_HOOK_PROBE_EXPANSION%` component, and `! & ^ ()`. The generated command contains the expected cmd percent escaping, while the conflicting expansion value is absent from the command. The capture is present under the literal special-character data path.
- The config's selected filesystem profile grants `:root` read and scratch-root write, denies both complete PowerShell directories, and has networking enabled. The provider base URL is loopback. The endpoint metadata records one `POST /v1/responses` from a loopback peer. The recorded profile disables update checks, plugins, remote plugins, apps, tool suggestions, and analytics; the provider credential is synthetic according to the isolated-session record.
- All process identities in the capture's two-element ancestry were absent or no longer matched their recorded start times at review. No native session was replayed.

Networking was enabled to reach the isolated loopback fake provider. This review confirms the configured model URL and the one recorded request were loopback; it does not claim a system-wide external-egress block or packet audit. The capture and provider response are synthetic test traffic, not authenticated user-provider acceptance.

The separate [source review 008](analysis-008-source-checks.md), SHA-256 `67ae514e6cb0ef5eba59a49bc445313b03d7151fc7fe2484a66e81de5d33c672`, supports the narrow applicability boundary: Codex CLI 0.164.0's ordinary local Windows resolver selects PowerShell or cmd and has no Bash selection branch. Local Bash fallback is not applicable to that branch; external-host Bash remains unverified and is not declared unsupported.

## Test results

- `dotnet test tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CapabilityMatrixTests' --verbosity minimal` — passed, 6/6.
- `dotnet test -c Release --no-build --no-restore` — passed, 1005/1005, 0 skipped, default parallel execution, 2m51s. Exit code 0. Both `BELLTOWER_RUN_NATIVE_HOOK_PROBES` and `BELLTOWER_HOOK_PROBE_INGRESS` were unset; the suite run did not repeat a provider-native probe.
- The existing 1003/1004 default-parallel result and 1004/1004 serialized result in QA 007 remain historical. This final default run passed; it does not establish the precise cause of the earlier parallel timeout.

The genuine 0.1.9018 helper hash is present in the retained native artifact and packaged helper outputs. No build was run in this QA round, so no helper copy or test prerequisite repair was needed.

