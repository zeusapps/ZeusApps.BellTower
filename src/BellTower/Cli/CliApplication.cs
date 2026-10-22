using System.CommandLine;
using BellTower.IO;
using BellTower.Tray;

namespace BellTower.Cli;

internal static class CliApplication
{
    public static int Run(string[] arguments)
    {
        var delimiter = Array.IndexOf(arguments, "--");
        var agentArguments = delimiter < 0 ? [] : arguments[(delimiter + 1)..];
        var dataDirectory = CommonOptions.DataDirectory();
        var silent = CommonOptions.Silent();
        var root = new RootCommand("Bell Tower - local Claude/Codex session attention.");
        root.Options.Add(dataDirectory);
        root.Options.Add(silent);
        root.Subcommands.Add(new TrayCommand(dataDirectory));
        root.Subcommands.Add(new StartCommand(dataDirectory));
        root.Subcommands.Add(new ExitCommand(dataDirectory));
        root.Subcommands.Add(new UpdateCommand(dataDirectory));
        root.Subcommands.Add(new TestSoundCommand(dataDirectory));
        root.Subcommands.Add(new ToggleVoiceCommand(dataDirectory));
        root.Subcommands.Add(new EventCommand(dataDirectory));
        root.Subcommands.Add(new StatusLineCommand(dataDirectory));
        root.Subcommands.Add(new ListCommand(dataDirectory));
        root.Subcommands.Add(new RenameCommand(dataDirectory));
        root.Subcommands.Add(new NativeSyncCommand(dataDirectory));
        root.Subcommands.Add(new FocusCommand(dataDirectory));
        root.Subcommands.Add(new SetupCommand(dataDirectory));
        root.Subcommands.Add(new ImportCommand(dataDirectory));
        root.Subcommands.Add(new ProcessInfoCommand());
        root.Subcommands.Add(new PreviewCommand());
        root.Subcommands.Add(new DoctorCommand(dataDirectory));
        root.Subcommands.Add(new LaunchCommand(dataDirectory, agentArguments));
        root.Subcommands.Add(new DetachCommand(dataDirectory));
        root.Subcommands.Add(new SyncTitleCommand(dataDirectory));
        root.Subcommands.Add(new FocusTerminalCommand(dataDirectory));
        root.Subcommands.Add(new PrepareSessionCommand(dataDirectory));
        root.Subcommands.Add(new PrepareCodexRuntimeCommand(dataDirectory));
        root.Subcommands.Add(new CodexRuntimeHostCommand(dataDirectory));
        root.Subcommands.Add(new StopCodexRuntimeCommand(dataDirectory));
        root.Subcommands.Add(new ToneCommand(dataDirectory));
        root.Options.OfType<VersionOption>().Single().Action = new ApplicationVersionAction();
        // Keep invocation synchronous so the tray runs on Program's STA thread.
        root.SetAction(result => new TrayHost(CommonOptions.Paths(result, dataDirectory)).Run(result.GetValue(silent)));
        var normalized = arguments switch
        {
            ["help"] => ["--help"],
            ["version"] => ["--version"],
            _ => arguments
        };
        // Forward exact agent strings without interpreting their options or expanding response files.
        var parser = new ParserConfiguration { ResponseFileTokenReplacer = null };
        var parsed = root.Parse(delimiter < 0 ? normalized : normalized[..delimiter], parser);
        if (delimiter >= 0 && parsed.CommandResult.Command is not LaunchCommand)
        {
            parsed = root.Parse(normalized, parser);
        }
        var invocation = new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false
        };
        if (parsed.CommandResult.Command is not EventCommand)
        {
            return parsed.Invoke(invocation);
        }

        // Parser failures in an installed hook must not steer approvals or expose payloads.
        try
        {
            if (parsed.Errors.Count != 0)
            {
                throw new ArgumentException("Invalid hook command.");
            }

            return parsed.Invoke(invocation);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            try
            {
                new LocalLog(new DataPaths(parsed.GetValue(dataDirectory))).Write("hook-rejected");
            }
            catch (Exception logError) when (logError is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Hook success must not depend on a writable diagnostic directory.
            }

            Console.WriteLine("{}");
            return 0;
        }
    }
}
