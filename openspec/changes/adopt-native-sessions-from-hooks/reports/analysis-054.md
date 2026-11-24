> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-054); client: Claude Code 2.1.331, subagent; mode: auto mode; model: claude-opus-5-5.
> Refusal (as reported by the reviewer): the client refused the write ("subagents return findings as text").
> Isolated probe (not committable, session scratch only): scratchpad\encprobe\Program.cs.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-054

Bounded read-only investigation of the startup `IOException` crashes of the versioned `bell-tower.exe`. This is not a code review.

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331, subagent (auto mode), claude-opus-5-5
- Date: 2026-11-24 (analysis done between about 18:40Z and 18:50Z)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-054 (ledger entries of 2026-11-24 in [findings.md](findings.md))
- Task and requirement references: none. This crash is not part of any task in this change (see Assessment).
- Scope and relevant neighboring paths: `src/BellTower/Program.cs`, `src/BellTower.Launcher/Program.cs`, `src/BellTower.Ingress/Program.cs`, `src/BellTower/Setup/HookCommandBuilder.cs`, `src/BellTower/Setup/ProviderSettingsEditor.cs:168-174`, `src/BellTower/Cli/StatusLineCommand.cs`, `src/BellTower.Protocol/HookTrayStart.cs`, `src/BellTower.Protocol/TrayProcess.cs`, `src/BellTower.Protocol/HookStartTask.cs`, `src/BellTower/Hosts/WindowsTerminalNavigator.cs`, `src/BellTower/Hosts/IsolatedTerminalPresentationWriter.cs`, `src/BellTower/IO/OwnedProcessRunner.cs`, `src/BellTower/Tray/TrayApplication.cs:305-321`, `src/BellTower/Tray/TrayController.cs`, `src/BellTower/Updates/ReleasePackage.cs`, `src/BellTower/Updates/UpdateTransaction.cs`, `src/BellTower/Setup/{SetupAction,IngressProvision,HookStartTaskSetup,ProviderHookProbe}.cs`, `src/BellTower/Integration/BellTower.psm1`, `vscode/extension.js`, `tests/BellTower.Tests` (for tests that run an installed binary)
- Baseline commit: 76beb52
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 76beb52 -Paths src tests scripts` -> `Summary: head=76beb52 baseline=76beb52 files=6 diff=e9baf4fdabd0 untracked=0/e3b0c44298fc`
- Before/after identity: after the analysis, the same command printed the same summary line. `git status --short` showed the same 23 entries as at the start. There were no unexpected mutations.
- Prior reports / rechecked findings: none. The ledger entry "Unexplained, not yet triaged" of 2026-11-24 is the origin.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Crash record | `Get-WinEvent` Application log, providers `.NET Runtime` 1026, `Application Error` 1000, `Windows Error Reporting` 1001, filtered on `bell-tower` | 2026-11-24: three crashes, not two. Their start times, from the 1000 events' start filetimes, are 16:37:37.168Z, 16:48:02.630Z and 18:38:59.002Z. The runtime event follows about 90 ms after each start. All three are on 0.1.9029 with the same stack: `HandleSetConsoleEncodingError` <- `set_OutputEncoding` <- `Program.Main`. WER bucket 1359319577758459996, exception 0xe0434352 | The third crash, at 18:38:59Z, was not in the assignment. The log gives no parent PID or command line. Security 4688 has no events and Sysmon is not installed |
| History (is it new?) | Same query over 60 days, grouped by day, signature and faulting path | There are 281 `.NET Runtime` 1026 events for `bell-tower.exe` from 2026-10-23 to 2026-11-24 on 22 of those days, 3 to 34 per day. 278 are `set_OutputEncoding` and 3 are `set_InputEncoding` (all on 2026-11-13). Faulting paths are only `%LOCALAPPDATA%\Programs\ZeusApps.BellTower\versions\<v>\bell-tower.exe`, across 13 installed versions (0.1.9, 0.1.10, 0.1.9010 to 0.1.9029). None came from a `src/**/bin` or test output path | The bug is old and exists in every installed version. `git log -L12,13:src/BellTower/Program.cs` shows the two setter lines came in with `6591ad0` (initial setup) and never changed |
| Local log and data timestamps (read only) | `%LOCALAPPDATA%\ZeusApps.BellTower\bell-tower.log`, `hook-start\*` timestamps | The log has no entry at 16:37 or 16:48. It has `navigation-ok-exact-tab` at 16:39:03, 18:38:30 and 18:38:33 (user activity). The last hook tray start attempt was at 18:24:54Z (`Started started-via-shell`). Earlier tray starts were at 16:36:21Z (ledger) and 18:24:55Z | The local log doesn't record the crashing process (it dies before any product code runs). No tray start attempt matches a crash time |
| Caller inventory | `Grep` for `ProcessStartInfo`, `Process.Start`, `CreateProcess*`, `Console.*Encoding =` in `src`; read each spawning site | Classification in the "Caller inventory" table under ANALYSIS-5400 | `Console.OutputEncoding/InputEncoding` are set in only one place, `src/BellTower/Program.cs:12-13`. The launcher and the ingress set neither |
| Provider configuration shape (read only, not quoted) | Parsed `~/.claude/settings.json` and `~/.codex/config.toml` / `hooks.json` for route shape only | Claude: all 12 hook entries call `bell-tower-ingress` (none call `bell-tower.exe`). `statusLine` is `type=command`, `refreshInterval=5`, and its command is the Bell Tower PowerShell `-EncodedCommand` route into the stable `bell-tower.exe statusline` (as written by `ProviderSettingsEditor.cs:168-174`). Codex: hooks call `bell-tower-ingress`. `status_line` lists only built-in items, with no command | This is the only frequent route that starts the versioned payload under a console owned by a client-killed process tree |
| Provider documentation | `ctx7` `/websites/code_claude` (status line page) | "If a new update triggers while your script is still running, Claude Code cancels the in-flight script." Updates are debounced at 300 ms, and `refreshInterval` re-runs the command every N seconds in addition to event updates | So Claude Code routinely cancels the status-line process. How it kills the process (single process or tree) is not documented. See ANALYSIS-5400 inference |
| Isolated mechanism probe | A small .NET 10 console probe built and run only in the session scratch directory (outside the repository). Each scenario runs in an inner process with its own windowless console (`CREATE_NO_WINDOW`), which starts a target that sets `Console.OutputEncoding` then `InputEncoding` in "current" form (as Program.cs) and in "guarded" form (catch `IOException`). Unhandled-exception crashes were avoided by design (no WER or event-log writes; checked afterwards). The only process stopped was the probe's own conhost | Results in the "Probe results" table under ANALYSIS-5400 | The crash is reproduced exactly, with the same HRESULT and message. The closed-pipe hypothesis is refuted |
| Startup cost (for ANALYSIS-5401) | `Measure-Command` x5 | Bare `powershell.exe -NoProfile -NonInteractive -Command "exit 0"`: 486 to 578 ms. A small .NET 10 exe start: 54 to 90 ms | Measured on this machine only. The full status-line route was not timed (it would touch the default data directory) |
| Tests that run an installed binary | `Grep` in `tests/` for `LocalApplicationData`, `Programs`, `%LOCALAPPDATA%` | None. Tests use `C:/Programs/Bell Tower/bell-tower.exe` as a plain string in setup plans | `dotnet test` (16:48) and the developer build don't start the installed payload. They only coincide in time |
| Build / test / format | not run | Read-only investigation. No product edits to check | Not applicable |

## Findings

### ANALYSIS-5400: Program.Main crashes when the inherited console host goes away during process startup (Claude Code status-line cancellation)

- Severity: warning (no data loss found, but every occurrence is a real unhandled crash with a WER report, and it hides real crashes in the event log)
- Related task / requirement: none in this change. It existed before this change (see Assessment).
- Locations: `src/BellTower/Program.cs:12-13` (at 76beb52); conduit `src/BellTower.Launcher/Program.cs:23` (the payload inherits the launcher's console by design); trigger route `src/BellTower/Setup/HookCommandBuilder.cs:27-31` with `src/BellTower/Setup/ProviderSettingsEditor.cs:168-174` (`refreshInterval` 5)
- Observation:
  - **Mechanism (measured).** On Windows, `Console.OutputEncoding`/`InputEncoding` call `SetConsoleOutputCP`/`SetConsoleCP` on the attached console. .NET ignores only `ERROR_INVALID_HANDLE`/`ERROR_INVALID_ACCESS` (no console) and throws for any other error. If the console server (conhost) disconnects after the process has connected to it at load time but before `Main` sets the encoding, the call fails with Win32 233 `ERROR_PIPE_NOT_CONNECTED`. That surfaces as `IOException 0x800700E9 "No process is on the other end of the pipe."`. Program.cs sets the encodings before its `try`, so the exception is unhandled. The window is the whole .NET runtime startup, about 50 to 90 ms (the crashed processes lived about 90 ms). The three `set_InputEncoding` crashes on 2026-11-13 show the same race landing between the two setter calls.
  - **Probe results.** From the isolated probe:

    | Scenario | "current" target (as Program.cs) | "guarded" target |
    | --- | --- | --- |
    | Console host killed after the child connected, before the encoding calls | `OutputEncoding` throws `IOException 0x800700E9 'No process is on the other end of the pipe.'` (the exact production signature) | Both setters tolerated (0x800700E9). A stdout write to the dead console is silently ignored. A stdin read throws `IOException` (the existing `catch` in Main would turn it into exit 1) |
    | Console host killed before the suspended child initialized | Child fails at load with 0xC0000142 (never reaches Main) | Same |
    | stdout is a pipe whose read end was closed before start | Both setters ok, write ok, exit 0 | Same |
    | stdin is a pipe whose write end was closed before start | Both setters ok, read returns EOF, exit 0 | Same |
    | No console (`DETACHED_PROCESS`) | Both setters ok (ignored by .NET), exit 0 | Same |

  - **The closed-pipe hypothesis is refuted (measured).** A redirected or already-closed stdout or stdin pipe does not make the setters throw. The setters act on the console, not on the standard handles. So the callers that redirect pipes (`TrayApplication.cs:312`, `WindowsTerminalNavigator.cs:25-38`, `OwnedProcessRunner.cs:16-22`, `ReleasePackage.cs:59`, `IngressProvision.cs:91`, `SetupAction.cs:20`) are not at risk because of their pipes.
  - **Caller inventory.** This lists who can start the versioned payload under a console that can disappear:

    | Caller | How the payload's console is obtained | Risk |
    | --- | --- | --- |
    | Claude Code status line: `powershell.exe -EncodedCommand` -> stable launcher -> payload `statusline` | Inherited from the PowerShell process Claude Code starts (and cancels) every 5 s and on events | **Trigger (inferred, high confidence).** Claude only |
    | Claude and Codex event hooks | `bell-tower-ingress.exe`, which never starts the payload and sets no encoding | None |
    | Hook tray start (`HookTrayStart` -> `TrayProcess.Start`) | Parent reassigned to explorer.exe, `CREATE_NO_WINDOW` gives it its own console, outside the client's process tree | None. No tray start attempts at the crash times |
    | Hook tray start, scheduled-task fallback (`HookStartTask.cs:79`) | `CREATE_NO_WINDOW`, own console | None |
    | Tray helpers: `focus-terminal`, `sync-title` (`WindowsTerminalNavigator.cs:25,78`, `IsolatedTerminalPresentationWriter` via `OwnedProcessRunner`), update via launcher (`TrayApplication.cs:311`), update coordinator and restart (`UpdateTransaction.cs:74,300`), tray start (`TrayController.cs:28`) | `CREATE_NO_WINDOW` or ShellExecute-hidden: a private console that lives as long as the child. The tray reaps with `Kill(entireProcessTree: false)`, which leaves conhost alive | None in practice |
    | Setup/probe/update paths (`ReleasePackage.Probe`, `IngressProvision`, `SetupAction`, `HookStartTaskSetup`, `ProviderHookProbe`) | `CREATE_NO_WINDOW`, own console (or they start other executables) | None |
    | Shell wrapper `BellTower.psm1` (both providers: `prepare-session`, `prepare-codex-runtime`, `detach`, `stop-codex-runtime`, tray start) | The user's interactive terminal console | Only if the terminal closes during the call, which tears the session down anyway. Theoretical, not observed |
    | Orchestrator CLI calls (`bell-tower.exe doctor` from the PowerShell tool) | The tool's console, alive for the whole command | Not the 16:37 crash: that command succeeded and is a different process |
    | VS Code extension (`vscode/extension.js` `execFile`) | Extension-host child | Not shown to be affected. Timeout behavior not inspected in depth |
    | Tests (`dotnet test`, InstallDevScriptTests, HookMaintenanceTests) | They don't run the installed binary. The event log has no crash from build or test output paths | None. 16:48 only coincides in time |

  - **Attribution (inference, not measured).** Several facts point to the Claude status line:
    - Only installed payloads crash, and the status-line command targets the installed stable launcher.
    - It is the only configured route where a client-owned short-lived process tree starts the payload and inherits its console. Hooks go through the ingress, and Codex has no command status line.
    - Claude Code is documented to cancel in-flight status-line commands, and runs them every 5 s plus on events, so cancellations are frequent while sessions are active. Each run is slow: about 0.5 s of PowerShell startup before the launcher even runs.
    - The crash counts per day (3 to 34) track interactive Claude Code days since 2026-10-23, the first day of installed use.
    - All three crashes on 2026-11-24 fall in periods of active Claude Code sessions: the orchestrator at 16:37, subagents at 16:48, user clicks at 18:38.

    The crash itself requires that the cancellation also takes down the console host while a payload created after the kill snapshot is still starting. That fits a tree kill (for example `taskkill /T`) that cannot catch a grandchild created after its snapshot. The client's exact kill method is not documented, so that last step is inferred.
- Proposed improvement and benefit: tolerate a console that has already gone away when setting the encodings, instead of crashing, and keep the two setters independent:

  ```csharp
  // Program.Main, replacing lines 12-13
  UseUtf8Console();
  ...
  /// <summary>A console whose host has already disconnected (for example a cancelled status-line shell) has nothing to
  /// configure; .NET already ignores a missing console, but throws for a disconnected one.</summary>
  internal static void UseUtf8Console(Action<Encoding>? output = null, Action<Encoding>? input = null)
  {
      var utf8 = new UTF8Encoding(false);
      try { (output ?? (e => Console.OutputEncoding = e))(utf8); } catch (IOException) { }
      try { (input ?? (e => Console.InputEncoding = e))(utf8); } catch (IOException) { }
  }
  ```

  Catch only `IOException`, not all exceptions. Don't just move the lines into the existing `try`: its `catch` returns 1 with a stderr message, which would still drop a legitimate orphaned command (for example a wrapper `detach`). With the guard the payload goes on, writes to a dead console or pipe are already ignored by .NET, and a read of a dead stdin ends in the existing `catch` (exit 1, no crash). This removes the WER reports and crash dumps, the 0xe0434352 entries in Reliability Monitor, and the event-log noise (281 events in 33 days) that hides real crashes. The fix is in shared startup code and covers both clients. No provider adapter changes.
- Behavior-preservation risks: On a healthy console nothing changes. The setters run in the same order, before `CaptureCompatibility.EnsureTrayInvocation`. On a disconnected console, a command that used to crash now runs and may finish without visible output. For `statusline` that is a render already cancelled by the client: it reads the identity and presentation stores and writes to a dead pipe, so it is harmless. No lease effect: the crash happened before `Installation.Lease`, and with the fix the lease is taken and released normally by `using`. The launcher and ingress are unchanged (they set no encoding).
- Impact (assignment goal 2):
  - Hook captures: not lost. Both providers capture through `bell-tower-ingress.exe`, which never runs this code.
  - Tray helper results: not lost in practice, since helpers own their console. If a helper died this way anyway, `WindowsTerminalNavigator.FocusAsync` falls back to `FocusWindow` (empty output gives `JsonException`) and `OwnedProcessRunner` returns false. Both are already handled.
  - CLI commands: the crashing invocations are cancelled status-line renders. Claude Code had already dropped their output, and the next render comes within 5 s. No state is written before the crash.
  - Effect on this change: none. This is a long-standing bug, not a regression.
- Evidence type: measured result (event-log history and signatures, the probe reproducing the exact HRESULT and message, refutation of the closed-pipe case, the guarded variant surviving); source observation (caller inventory, routes); inference (attribution to the status-line cancellation and the client's kill method)
- Verification / measurement plan:
  - Unit tests (`tests/BellTower.Tests`, e.g. `ProgramConsoleTests`) for `UseUtf8Console` with fake setters:
    - the output setter throws `new IOException("No process is on the other end of the pipe.", unchecked((int)0x800700E9))`: no throw, and the input setter is still called;
    - the input setter throws `IOException`: no throw;
    - both succeed: both are called with a BOM-less UTF-8 encoding;
    - a non-IO exception (for example `InvalidOperationException`) still propagates.
  - A deterministic process-level regression is not feasible without a product test hook: the race needs the console to die after load but before `Main`. Don't add a timing hook to product code. The probe above, recorded here, is the mechanism evidence.
  - After a dev install of the fix, count Application-log `.NET Runtime` 1026 events for `bell-tower.exe` over a few days of normal Claude Code use. Expected: no new `set_OutputEncoding`/`set_InputEncoding` events.
  - Parity scenarios: verify a Claude Code session (status line active) and a Codex session (wrapper path) side by side for a day with no new events. Run the existing full suite for regression.
  - Optional, needs a user decision (admin): enable process-creation auditing or Sysmon briefly to confirm the parent chain (Claude Code -> powershell.exe -> stable launcher -> crashing payload) and close the inferred part of the attribution.
- Relationship: canonical for the ledger entry "Unexplained, not yet triaged" of 2026-11-24.

### ANALYSIS-5401: the Claude status-line route pays a PowerShell host start on every render, which widens the cancellation window

- Severity: suggestion (follow-up, performance hypothesis)
- Related task / requirement: none in this change. This is the existing Claude status-line integration.
- Locations: `src/BellTower/Setup/HookCommandBuilder.cs:27-31` (every verb other than `event`, or `event` without ingress, goes through `powershell.exe -NoProfile -NonInteractive -EncodedCommand ... | & <stable launcher> statusline`); `src/BellTower/Setup/ProviderSettingsEditor.cs:168-174` (`refreshInterval` 5)
- Observation: Each status-line render runs three processes: Windows PowerShell 5.1 (measured 486 to 578 ms bare start here), then the stable launcher, then the payload. This happens every 5 s and on every debounced event, so each render stays in flight for roughly 0.6 s or more. Claude Code cancels an in-flight render when a new update arrives, and each cancellation risks the ANALYSIS-5400 crash. It also costs CPU in every active Claude session. The PowerShell wrapper exists only to force UTF-8 stdin before handing over to the launcher. The event hooks already moved to a direct, quoted executable command for the same client (`HookCommandBuilder.cs:21-22`).
- Proposed improvement and benefit: In a follow-up, consider a direct status-line command, written like the Claude event hook (a quoted path to the stable launcher with `statusline --provider claude --data-dir ...`). The payload already sets `Console.InputEncoding` to UTF-8 and the status-line reader takes `Console.In`. This would cut about 0.5 s per render and make cancellations rare. It is not a fix for ANALYSIS-5400; the guard is still needed.
- Behavior-preservation risks: Claude Code's shell and quoting on Windows (the event hook already relies on the Bash-literal form), and that stdin bytes are UTF-8 without the PowerShell re-encoding. Existing users' settings would need migration through the ownership record (`ownership.StatusLines`), because a changed command would read as "unowned" at `ProviderSettingsEditor.cs:169-171` unless handled. Codex is not affected (no command status line), so this is a Claude-only adapter change and no parity question.
- Evidence type: performance hypothesis (bare PowerShell start measured; full route not measured to avoid the default data directory)
- Verification / measurement plan: Time the old and new status-line commands end to end with a disposable `--data-dir` and a fixture JSON on stdin (10 runs each). Add setup tests for the written command shape and for ownership migration. Do a live Claude Code check that the chip still renders, including non-ASCII session names.
- Relationship: contributes to the trigger rate of ANALYSIS-5400. It is independent of it.

## Re-verification

None (not a recheck assignment).

## Assessment and limitations

- **Scope covered:** every place in `src` that starts a process, the two console entry points, the hook and status-line command builders, the configured provider routes (shape only), the tests that might run an installed binary, the Application log history, and the local log and timestamps. All reads were non-mutating. Nothing in the installation, configuration or data directory was changed. No installed process was started or stopped.
- **Answers to the assignment goals:**
  1. **Who can start the payload this way.** In practice it is the Claude status line, through PowerShell and the stable launcher (inferred, high confidence). The redirected or closed pipe callers are not at risk: the probe refutes that hypothesis. The tray helpers, hook ingress, hook tray start (shell and task), setup, probe and update paths, and tests are not affected (see the caller inventory).
  2. **Impact.** No hook capture, tray helper result or meaningful CLI result is lost. The crashing runs are status-line renders the client had already cancelled. The cost is WER and crash-dump work and event-log noise. The bug existed before this change: since `6591ad0`, and in all 13 installed versions since 2026-10-23.
  3. **Fix.** Guard both encoding setters separately against `IOException` (ANALYSIS-5400). Add the unit tests above.
- **Placement recommendation:** a small follow-up (a bugfix change or a standalone task), not this change. It is outside the change's requirements and existed before it. Folding it in would widen a change that is waiting on the task 4.11 live check. It is a low-risk edit to two lines plus one test class, so the orchestrator or the user may choose to fold it in instead. ANALYSIS-5401 belongs in a separate Claude status-line follow-up.
- **Parity:** the fix is in shared startup code and covers both clients. Codex has no frequent trigger today: hooks go through the ingress and its status line is built in. The Codex-relevant path is the shell wrapper, which benefits the same way. These are not proven-restricted gaps.
- **Limitations:**
  - No parent-process evidence exists for the crashes: no audit or Sysmon, and the WER temp dumps were not opened. So attributing them to the status line, and to a tree kill of the console host, is inference.
  - The 18:38:59Z crash is on 0.1.9029; the switch to 0.1.9030 came later, per the ledger.
  - The full status-line route was not timed (it would use the default data directory).
  - The probe stopped only the console host of its own probe process, wrote only under the session scratch directory, and produced no event-log entries (checked).
  - No build, test or format check was run (read-only investigation, no product edits).
- **Unexpected writes:** none. The write of this report was refused by the client, and the text is returned inline.
- **Decisions needed (orchestrator or user):**
  - Follow-up or fold-in for ANALYSIS-5400.
  - Whether to record ANALYSIS-5401 as a follow-up.
  - Optional: authorize temporary process-creation auditing to confirm the attribution.
