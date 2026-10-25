namespace BellTower.IO;

public enum TrayInvocationKind
{
    NotTray,
    ThisDataDirectory,
    OtherDataDirectory
}

/// <summary>Classifies a Bell Tower process without invoking its command.</summary>
public static class TrayInvocationCommand
{
    private static readonly HashSet<string> NonTrayCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "__apply-update", "--distribution-info", "--help", "-h", "--version", "-v", "help", "version",
        "start", "exit", "update", "test-sound", "toggle-voice", "event", "statusline", "list", "rename",
        "native-sync", "focus", "setup", "import", "process-info", "foreground-window", "preview", "doctor", "launch", "detach",
        "sync-title", "focus-terminal", "prepare-session", "prepare-codex-runtime", "codex-runtime-host",
        "stop-codex-runtime", "tone"
    };

    public static TrayInvocationKind Classify(string commandLine, string expectedExecutable, string expectedDataDirectory)
    {
        var arguments = WindowsCommandLine.Parse(commandLine);
        if (arguments.Length == 0 || !SamePath(arguments[0], expectedExecutable))
        { throw new InvalidDataException("Process command line does not match its executable image."); }

        var index = 1;
        string? command = null;
        var dataDirectories = new List<string>();
        while (index < arguments.Length)
        {
            var argument = arguments[index++];
            if (TryReadOption(argument, "--data-dir", arguments, ref index, out var dataDirectory))
            {
                dataDirectories.Add(dataDirectory);
                continue;
            }
            if (argument.Equals("--silent", StringComparison.OrdinalIgnoreCase))
            { continue; }
            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                if (argument is "--help" or "-h" or "--version" or "-v")
                { return TrayInvocationKind.NotTray; }
                throw new InvalidDataException("Process command line has an unknown global option.");
            }

            command = argument;
            break;
        }

        // The root action runs the tray when no command is supplied.
        if (command is null)
        { command = "tray"; }
        if (!command.Equals("tray", StringComparison.OrdinalIgnoreCase))
        {
            if (NonTrayCommands.Contains(command))
            { return TrayInvocationKind.NotTray; }
            throw new InvalidDataException("Process command line has an unknown Bell Tower command.");
        }

        while (index < arguments.Length)
        {
            var argument = arguments[index++];
            if (TryReadOption(argument, "--data-dir", arguments, ref index, out var dataDirectory))
            {
                dataDirectories.Add(dataDirectory);
                continue;
            }
            if (argument.Equals("--silent", StringComparison.OrdinalIgnoreCase) || argument.Equals("--help", StringComparison.OrdinalIgnoreCase) || argument.Equals("-h", StringComparison.OrdinalIgnoreCase))
            { continue; }
            if (TryReadOption(argument, "--duration-ms", arguments, ref index, out var duration) && int.TryParse(duration, out _))
            { continue; }
            throw new InvalidDataException("Tray command line contains an unknown or malformed argument.");
        }

        if (dataDirectories.Count != 1 || string.IsNullOrWhiteSpace(dataDirectories[0]))
        { throw new InvalidDataException("Tray command line has no single explicit data directory."); }
        if (!Path.IsPathFullyQualified(dataDirectories[0]))
        { throw new InvalidDataException("Tray command line has an unresolved data directory."); }
        return SamePath(dataDirectories[0], expectedDataDirectory) ? TrayInvocationKind.ThisDataDirectory : TrayInvocationKind.OtherDataDirectory;
    }

    private static bool TryReadOption(string argument, string name, string[] arguments, ref int index, out string value)
    {
        if (argument.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (index >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index]))
            { throw new InvalidDataException("Tray command line has an option without a value."); }
            value = arguments[index++];
            return true;
        }

        var prefix = name + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            if (string.IsNullOrWhiteSpace(value))
            { throw new InvalidDataException("Tray command line has an empty option value."); }
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool SamePath(string left, string right)
    {
        try
        { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
    }
}
