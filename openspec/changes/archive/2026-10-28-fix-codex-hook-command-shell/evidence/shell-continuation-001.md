# Codex hook shell selection continuation

## Implementation

Packaged Codex setup now selects a command form during planning by invoking the installed Codex CLI with an isolated `CODEX_HOME`, temporary project/data, and loopback fake Responses endpoint. It tries PowerShell, cmd, then Bash. A candidate is selected only after Codex's native SessionStart hook creates a durable capture. The selected shell is included in the preview and recorded in integration ownership. Apply repeats the probe with the exact retained helper target and the selected form before writing provider settings; failure or shell drift aborts before those writes. Doctor performs the same isolated selection probe and reports `matches`, `drift`, or `unverified` without changing provider settings or calling a real model endpoint.

The probe path includes spaces, apostrophe, Unicode, a literal `%BELLTOWER_HOOK_PROBE_EXPANSION%` token, `!`, `&`, `^`, and parentheses. The variable named by that token is set in the Codex child environment to a different value, so a successful capture also checks that percent-delimited path text stayed literal. The helper path is separately rechecked at Apply after it has been staged at its final immutable target.

## Direct shell controls

These controls exercised the generated command quoting/argument boundary directly; they are not Codex-native shell-selection evidence.

- **cmd.exe:** a disposable argv recorder and its `--data-dir` argument were both placed beneath paths containing the characters listed above. With `BELLTOWER_HOOK_PROBE_EXPANSION` set to `wrong`, the generated command ran through the pinned runner's `/C` form (no `/d`, `/s` or `/v` switches), exited 0, and reported the exact original helper/data arguments, including the literal `%...%` text. The first form that placed `^%` inside one quoted segment failed; the accepted direct-shell form closes and reopens quotes around escaped percent characters.
- **Git for Windows Bash 5.2.37:** a disposable argv recorder at the same class of special path ran through the existing single-quote form. It exited 0 and received the expected slash-normalized Windows helper/data paths exactly; `%...%`, `!`, `&`, `^`, parentheses, spaces, apostrophe, and Unicode remained literal.
- A PowerShell builder regression continues to verify argument boundaries for spaces, apostrophe, Unicode and shell metacharacters.

The direct controls establish command parsing in these exact local shells only. They do not establish that Codex selected cmd or Bash for a turn. The current native Codex probes previously observed PowerShell 7; this continuation did not force Codex's internal cmd fallback or Bash branch. Those branches remain unverified until the native candidate itself produces a durable capture. Setup fails closed when none of the candidates passes. The installed Codex command-array form remains unsupported by the tested 0.164.0 parser.

## Checks

- `dotnet build tests/BellTower.Tests/BellTower.Tests.csproj -c Release --no-restore -m:1` -> passed after shell-selection and doctor changes.
- Focused setup/doctor tests run outside the sandbox after the testhost IPC hang: `FastHookSetupTests`, `HookDeliveryReporterTests`, and `CodexWindowsCommandsAreSpecificToTheShellAndKeepPercentTokensLiteral` -> 20 passed, 0 failed.
- Direct cmd `/C` argv recorder -> exit 0; exact helper and data-root argument comparison passed with the parent environment's percent token variable set to a conflicting value. The command used `/C` only, matching the pinned Codex runner's cmd branch.
- Direct Git for Windows Bash 5.2.37 argv recorder -> exit 0; exact slash-normalized helper/data-root argument comparison passed.
- `./scripts/package.ps1 -Dev -Offline` -> packaged candidate 0.1.9016. Package SHA-256 `7c8d5e7a07e5dc39a8de12652c2aac5b3d94959d7ff31eb148ab4688b4372480`; standalone ingress SHA-256 `ade83e42b4327117ecc5d8339bcff79dd7360f7dc7272ec7a3d8747f5462f5a9`. The package command first failed a locked restore in the sandbox; the documented offline command passed outside the sandbox.
- Review identity before package: `head=aadbfb3 baseline=aadbfb3 files=21 diff=ff9951a31d79 untracked=4/d2da9a7f3128`. Recomputed after packaging with the same summary, so the package is bound to this source snapshot.
- Isolated native setup using package 0.1.9016, a temporary Codex home/data root with the special characters above, and the loopback fake model endpoint -> exit 0. Planning selected PowerShell from a durable SessionStart capture; apply reverified the exact retained target and wrote only the isolated settings/ownership files. This proves the current native PowerShell path and the full selected data-root quoting with literal percent text.
- Isolated doctor rerun with both provider homes and data redirected to the same temporary tree -> expected `PowerShell`, current `PowerShell`, status `matches`; exit 0. An earlier doctor invocation isolated Codex but did not override the Claude settings root; it emitted no settings content and changed nothing. The reported doctor result above comes from the fully isolated rerun.
- A native Codex cmd/Bash turn and user-operated post-migration live trial remain pending. The direct cmd/Bash controls do not establish the provider's shell selection branches. No real provider settings or installation were changed by these checks.
