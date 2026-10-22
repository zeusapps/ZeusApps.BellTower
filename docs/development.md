# Development conventions

Bell Tower follows WorkLog's small-helper structure: application folders by responsibility, one top-level type per file, and a command class per CLI verb. A small stable launcher and shared distribution-contract library support immutable versioned payloads; xUnit and packaged acceptance projects verify their behavior. WorkLog is a style reference, with no project or runtime dependency.

## CLI commands

Use `System.CommandLine` 2.0.13, matching WorkLog. `CliApplication` registers commands; command classes declare typed options, validation, and their action. `CommonOptions` owns shared option definitions. The library generates help and handles invalid commands, required options, and typed values. See Microsoft's [parsing and invocation documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke).

Commands delegate to application services. Do not add another parser or move service logic into `Program`. `Program` owns UTF-8 console setup, the STA entry point, and top-level error reporting. Keep tray invocation synchronous so WinForms runs on that STA thread.

For `launch`, only Bell Tower's prefix is parsed. Strings after `--` pass directly to `ProcessStartInfo.ArgumentList`, including spaces, option-like values, and literal `@` arguments. Response-file expansion is disabled. Hook invocations have a dedicated failure boundary: malformed input or options produce a sanitized log code and a successful empty JSON response, without provider decisions or raw payload logging.

## Responsibilities and dependencies

- `Core`: session data, the observation reducer, attention policy, identity text, and palette rules. Keep provider JSON, disk access, native windows, and UI controls out of this layer.
- `IO`: local persistence, atomic writes, locking, process inspection, hashes, and read-only Git queries. `StateStore` coordinates replay and state transactions; `StateSnapshotStore` reads/writes snapshots; `AttachmentStore` owns attachment files.
- `Providers`: bounded hook input, normalization, ingestion, and status-line rendering. Adapter output is the provider-neutral `Observation` model.
- `Sessions`: agent launch/lifecycle and session queries/metadata actions, shared by the command layer.
- `Hosts`: attachment capture and terminal navigation. `Navigator` validates process identity and delegates to `ITerminalNavigator`; each host implementation owns its protocol or native window calls.
- `Tray`: UI lifetime, controls, view projection, and audio. `SessionViewFactory` creates presentation records. `AudioQueue` depends on `IAudioSink`; physical playback and silent/test substitutes remain separate.
- `Setup`: plan construction, owned settings edits, encoded hook commands, application of file changes, and explicit legacy import. Keep these separate from ordinary startup.
- `Integration/BellTower.psm1`: embedded PowerShell support for native session launches. Setup supplies stable executable/data paths and exports the selected provider functions plus legacy aliases. Directory selection, optional background ownership and restoration belong here. Do not replace the direct native invocation with a call to the executable launcher.
- `Diagnostics`: read-only reports of observations and capability limitations.

Add interfaces where there are actual alternate implementations or a replaceable boundary, as with terminal hosts and audio sinks. Do not add base classes or interfaces just to wrap individual concrete services. Small functions and data records do not need inheritance hierarchies.

Managed console-title writes run only in the isolated helper process, with process identity and console-membership checks. Keep that console attachment code out of hooks and the tray process itself. Detachment shares the presentation lock so shell restoration happens after outstanding writes. Desktop setup must preserve unrelated settings and track adopted profiles/settings for conditional restoration; removal ownership is committed after extension actions succeed so failed removals can be retried.

Tab selection also runs in a bounded helper. Resolve a fresh random console marker through UI Automation, preserve the previous title in a finally block, and fall back when the target is not unique. Keep UI Automation separate from selection policy through the replaceable tab source. Never derive tab identity from a saved position or friendly name. Detached tray/helper processes use the executable directory rather than retaining a project as their working directory.

## Formatting and checks

`.editorconfig` defines the whitespace and C# style. C# uses four spaces, file-scoped namespaces, braces for control flow, readable statement blocks, and normal single-line auto-properties. XML project/solution files and JSON use two spaces. Keep statements on separate lines, break up long expressions, and group tests under the area they exercise.

After restoring dependencies:

```powershell
./scripts/format.ps1
./scripts/format.ps1 -Verify
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore
node --test vscode/service.test.js vscode/focus.test.js
```

The formatting script runs the SDK formatter for C#. Keep XML/JSON readable using their `.editorconfig` indentation settings. Tests use isolated data/configuration and silent audio sinks. Fake agent subprocesses exercise launching without authenticated Claude or Codex sessions. Live hook coverage and desktop focus acceptance remain separate work in the compatibility matrix.
