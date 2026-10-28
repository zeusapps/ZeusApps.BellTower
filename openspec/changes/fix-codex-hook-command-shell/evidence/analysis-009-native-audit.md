# Analysis 009 native evidence audit

Date: 2026-10-28 UTC. Independent read-only inspection of retained evidence; no native provider, sandbox, endpoint, product build, or test suite was launched. Only pure in-process builder/protocol reflection and filesystem/process reads were used.

## Input identity

| Input | SHA-256 |
| --- | --- |
| [Native investigation009](native-investigation-009.md) | `9C1D9A5617BFE191842BC0889262F53621DB798B1292C0EA08C84E501593EEA3` |
| [QA009 report](../reports/qa-009.md) | `BA7110E34CF0D47A35D95C90D68ECDC8FB5CA3CEA8A78D471D355B068528ACC2` |
| [QA010 observer evidence](qa-010-mxc-denied-file-observer.md) | `5C1CEF235B18EE099972C2BF3BA8F7167D46AD93EF999F98F47C38E194DA5D6E` |
| [Completion010](completion-docs-010.md) | `81243744991C6D53B559CF1BD9C493DCBC9E7BD28805CDB0128CE0ADA40B488D` |
| [Completion011](completion-docs-011.md) | `D52C91188BE7DF1ED1469BD48B58471131578EBC87398BC525D635738D2C065C` |

The native record's committable loopback placeholder preserves the claim without retaining a literal address. Raw data below stays in ignored `artifacts/codex-hook-fallback-009/session/`; no raw personal paths, environment, request bodies, or provider configuration are copied here.

## Independent raw checks

| Check | Observed result |
| --- | --- |
| Native image provenance | Invoked the current compiled `ProviderHookProbe.ResolveCodexExecutable()` through reflection without starting its result. It resolved a direct `.exe`; file hash `6F267830B029EE25D9339864AA867CD45DE9F8FD4C5EA007D2E63C13A0C66D36` matches the recorded0.164.0 target. No shim or provider profile read was performed. |
| Helper provenance | Special-path helper hash `82CC115629B60F2D34ACE16F91D72009E6EAB1E8B28D80EB0CE796D150AD5707` matches the retained0.1.9018 ingress in [package binding](shell-continuation-002.md). |
| Current command reproduction | Constructed `DataPaths` with the retained isolated data root and `HookCommandBuilder` with the retained helper and `CodexHookShell.Cmd`, then invoked `Build("event", "codex")`. Current compiled builder output exactly equals `hook-command.txt` and the sole registered SessionStart command. Command hash `8A1A41EF83C2A21F447E5BAE06C246849C8931E3454908F45BDA55869B3F8DC4`. |
| Protocol decode | Opened the sole spool file as a stream and invoked current `BellTower.Protocol.CaptureProtocol.Decode`. Successful decode; protocol1; provider `codex`; registration source `startup`; observation and attachment directories equal the isolated project. Capture hash `13DF2846296C98BCD94A69AFF40856D9C80EBC2DF930A5B3D344A0E4B285E651`. |
| Shell identity | Ancestor names are exactly `cmd.exe -> codex.exe`; no PowerShell/Bash ancestor. `HookShell` PID and UTC creation ticks both equal the sole cmd ancestor, whose creation precedes ingress. This is process identity evidence rather than inference from the configured command. |
| Source provenance of identity | `src/BellTower.Ingress/Program.cs:46` obtains ancestors with `ProcessProbe.Ancestors(Environment.ProcessId)`, identifies creation times, and chooses the shell preceding the provider executable. `src/BellTower/IO/ProcessProbe.cs:61` uses the Windows process snapshot and `Identify` uses process start time. Hook text or provider stdin does not supply this ancestry. |
| Exact special paths | Reproduced builder arguments and sole capture location both retain spaces, apostrophe, Unicode, the literal percent-delimited expansion name, and `! & ^ (x)` in helper/data roots. No expanded alternate root was substituted. The conflicting child variable is documented by the launching investigator; the exited process environment is not independently reconstructable from these raw files. |
| Configuration boundary | Retained private TOML has root read, two full PowerShell-directory denies and one loopback model-provider URL. The retained hooks JSON has one SessionStart command hook. No actual config or URL is published here. |
| Endpoint | `endpoint-requests.jsonl` has one `POST /v1/responses`; its peer is loopback. The retained server binds loopback, emits the synthetic completion, and logs metadata/byte counts rather than request bodies. This checks the retained endpoint transcript, not all system network traffic. |
| Native completion | Retained stdout contains thread/turn start, synthetic assistant completion and `turn.completed`; stderr contains the stdin hint. Investigator records outer exit0/816ms; raw text alone does not independently establish that exit code or duration. |
| Cleanup | A read-only PID-plus-start-time check of the retained cmd/codex ancestor identities found neither still running. Endpoint cleanup remains the launching investigator's recorded check; no process was stopped by this analyzer. |

The current compiled desktop DLL hash is `52A3BE0E0BA1BD26BB2B5A72ED37513654768E380A3D021C56F3A83A827EB2DA`, reflecting the subsequent capability evidence update. It is distinct from the investigator's pre-update builder DLL `90bab965de1eebd925273f819fccb88804a8f5d178df49cae92ac3cf653fa6d5`. Exact command reproduction above bridges those builds for the tested command; it is not a claim that installed0.1.9018 contains later capability wording. No package was rebuilt or installed here.

An initial PowerShell reflection attempt used an unsuitable `Activator.CreateInstance` overload and could not construct the builder; its resulting command comparison was invalid. The corrected explicit constructor invocation produced the exact equality above. No native process ran in either reflection attempt.

Additional raw hashes: `hooks.json` `4DC9D24F17B24C0641942D80CE9B3F530D6C45D2D7B517A9A36537C57F0C8BC7`; config `E4195CA0124CD34FD76CF98D3ADD94F07CE4BFC481CDAE6E833370B4735568DA`; endpoint metadata `49FF21F5DD79304909A1245BB460DBDB97CF32E5754EB1E7A2FF5B9794FE13FB`.

## Earlier evidence correction and enforcement

Read retained QA009 raw stdout/stderr: both no-deny and deny echo controls equal `MXC_BASELINE_OK`, with empty stderr. QA009 expressly corrects the unsupported QA008 inference while preserving its history and leaving the error's origin unknown. This verifies the correction requested by ANALYSIS-350, without pretending to explain investigation007's distinct CreateProcessW error5.

Read retained QA010 observer JSON: host can stat/read both fixed PowerShell executables; inside the directory-deny profile both metadata and read checks fail with `EPERM`. This is independent observer enforcement evidence, separate from the later native Codex capture. Together with the pinned local Windows resolver trace in [Analysis008](analysis-008-source-checks.md) and the process identity above, native009 establishes the genuine cmd fallback path in the tested0.164.0 Windows MXC mode.

## Documentation recheck

Completion011 corrects Completion010's false open benchmark/trust statements in current follow-ups/compatibility text. Current compatibility distinguishes installed Claude2.1.311 from the screenshot's unidentified Claude process version. The four docs consistently keep external-host Bash and other versions/path priorities unverified and do not turn those gaps into provider restrictions.

ORCH-001 remains partly uncorrected: `docs/hook-ingress.md:64` still says the trial002 result awaits independent review. QA005 and Analysis005 already reviewed that exact literal-echo trial, as current follow-ups correctly records. Authentication scope is a separate valid limitation and does not make the review pending. This is a current status contradiction, not a native/runtime defect. No new finding ID is needed for the existing canonical ORCH-001.
