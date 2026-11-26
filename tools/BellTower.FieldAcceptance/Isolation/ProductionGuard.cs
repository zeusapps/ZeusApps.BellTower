namespace BellTower.FieldAcceptance.Isolation;

public sealed class IsolationException(IReadOnlyList<string> violations)
    : InvalidOperationException("Production isolation violated: " + string.Join("; ", violations))
{
    public IReadOnlyList<string> Violations { get; } = violations;
}

/// <summary>
/// Pure refusal rules. The harness calls these before it creates a sandbox, before it builds a child environment and before every
/// process it starts, so a mistaken path fails the run instead of reaching the user's installation, data, tray or provider settings.
/// </summary>
public static class ProductionGuard
{
    /// <summary>Every sandbox path must be inside <paramref name="allowedBase"/> and must neither lie within nor contain a production root.</summary>
    public static IReadOnlyList<string> CheckLayout(InstanceLayout layout, ProductionLocations production, string allowedBase)
    {
        var violations = new List<string>();
        foreach (var (label, path) in layout.Paths())
        {
            if (!PathRelation.IsUnderOrEqual(path, allowedBase))
            {
                violations.Add($"{label} path is outside the harness artifacts base");
            }
            foreach (var root in production.Roots.Where(root => PathRelation.Overlaps(path, root)))
            {
                violations.Add($"{label} path overlaps a production location ({Path.GetFileName(root)})");
            }
        }
        return violations;
    }

    /// <summary>Environment values (PATH split into entries) must not name a production location.</summary>
    public static IReadOnlyList<string> CheckEnvironment(IReadOnlyDictionary<string, string> environment, ProductionLocations production)
    {
        var violations = new List<string>();
        foreach (var (name, value) in environment)
        {
            var values = name.Equals("PATH", StringComparison.OrdinalIgnoreCase)
                ? value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) : [value];
            foreach (var root in production.Roots)
            {
                if (values.Any(entry => PathRelation.Mentions(entry, root)))
                {
                    violations.Add($"environment variable {name} names a production location ({Path.GetFileName(root)})");
                }
            }
            if (values.Any(entry => IsHome(entry, production)))
            {
                violations.Add($"environment variable {name} is the user's own profile or application-data folder");
            }
        }
        return violations;
    }

    /// <summary>
    /// Builds the child environment from nothing but a short allow-list of the harness's own variables plus the sandbox values.
    /// PATH entries inside production locations are dropped (so a bare command name can never resolve to the user's real
    /// installation or provider binaries) and the fake provider directory comes first.
    /// </summary>
    public static (Dictionary<string, string> Environment, IReadOnlyList<string> RemovedPathEntries) BuildEnvironment(
        IReadOnlyDictionary<string, string> parent, InstanceLayout layout, ProductionLocations production)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Inherited.Where(name => parent.ContainsKey(name)))
        {
            environment[name] = parent[name];
        }
        var removed = new List<string>();
        var kept = new List<string> { layout.FakeBin };
        if (parent.TryGetValue("PATH", out var path))
        {
            foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (production.Roots.Any(root => PathRelation.Mentions(entry, root)))
                {
                    removed.Add(entry);
                }
                else
                {
                    kept.Add(entry);
                }
            }
        }
        environment["PATH"] = string.Join(Path.PathSeparator, kept);
        environment["USERPROFILE"] = layout.Home;
        environment["HOME"] = layout.Home;
        environment["APPDATA"] = layout.RoamingAppData;
        environment["LOCALAPPDATA"] = layout.LocalAppData;
        environment["TEMP"] = layout.Temp;
        environment["TMP"] = layout.Temp;
        environment["CLAUDE_CONFIG_DIR"] = layout.ClaudeDirectory;
        environment["CODEX_HOME"] = layout.CodexDirectory;
        environment["BELLTOWER_DATA_DIR"] = layout.Data;
        return (environment, removed);
    }

    // Only variables that cannot name the user's data: the Windows system locations, the .NET host and the Git Bash override the product probes read.
    private static readonly string[] Inherited =
    [
        "SystemRoot", "SystemDrive", "windir", "ComSpec", "PATHEXT", "OS", "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS",
        "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432",
        "ProgramData", "ALLUSERSPROFILE", "PUBLIC", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_MULTILEVEL_LOOKUP", "CLAUDE_CODE_GIT_BASH_PATH"
    ];

    /// <summary>
    /// Refuses a command that would leave the sandbox. <paramref name="systemTools"/> are the absolute paths of the few
    /// non-sandbox executables the harness may run (Git Bash, Windows PowerShell, schtasks).
    /// </summary>
    public static IReadOnlyList<string> CheckCommand(
        string executable, IReadOnlyList<string> arguments, InstanceLayout layout, ProductionLocations production, IReadOnlyCollection<string> systemTools)
    {
        var violations = new List<string>();
        var full = Path.GetFullPath(executable);
        var insideSandbox = PathRelation.IsUnderOrEqual(full, layout.Root);
        if (!insideSandbox && !systemTools.Any(tool => tool.Equals(full, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add("executable is neither inside the sandbox nor an allowed system tool");
        }
        if (production.Roots.Any(root => PathRelation.IsUnderOrEqual(full, root)))
        {
            violations.Add("executable is inside a production location");
        }
        foreach (var argument in arguments)
        {
            foreach (var root in production.Roots.Where(root => PathRelation.Mentions(argument, root)))
            {
                violations.Add($"an argument names a production location ({Path.GetFileName(root)})");
            }
            if (IsHome(argument, production))
            {
                violations.Add("an argument is the user's own profile or application-data folder");
            }
        }
        if (insideSandbox && Path.GetFileName(full).Equals("bell-tower.exe", StringComparison.OrdinalIgnoreCase) && !IsInformational(arguments))
        {
            var index = arguments.ToList().FindIndex(argument => argument == "--data-dir");
            if (index < 0 || index + 1 >= arguments.Count)
            {
                violations.Add("a Bell Tower command has no explicit --data-dir");
            }
            else if (!PathRelation.IsUnderOrEqual(arguments[index + 1], layout.Data))
            {
                violations.Add("a Bell Tower command targets a data directory outside the sandbox");
            }
        }
        return violations;
    }

    private static bool IsHome(string value, ProductionLocations production) =>
        value.Length > 2 && Path.IsPathRooted(value) && value.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        production.Homes.Any(home => Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)).Equals(Path.TrimEndingDirectorySeparator(home), StringComparison.OrdinalIgnoreCase));

    private static bool IsInformational(IReadOnlyList<string> arguments) => arguments is [("--version" or "--help" or "-h" or "-?")];

    public static void Require(IReadOnlyList<string> violations)
    {
        if (violations.Count > 0)
        {
            throw new IsolationException(violations);
        }
    }
}
