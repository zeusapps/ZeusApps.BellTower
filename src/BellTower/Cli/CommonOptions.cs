using System.CommandLine;
using BellTower.IO;

namespace BellTower.Cli;

internal static class CommonOptions
{
    public static Option<string?> DataDirectory() => new("--data-dir")
    {
        Description = "Local data directory. Defaults to BELLTOWER_DATA_DIR or per-user application data.",
        Arity = ArgumentArity.ExactlyOne,
        Recursive = true
    };
    public static Option<string?> Text(string name, string description, bool required = false) => new(name)
    {
        Description = description,
        Required = required,
        Arity = ArgumentArity.ExactlyOne
    };
    public static Option<string?> Provider(bool required = true, string? defaultValue = null)
    {
        var option = Text("--provider", "Agent provider: claude or codex.", required);
        if (defaultValue is not null)
        {
            option.DefaultValueFactory = _ => defaultValue;
        }

        option.AcceptOnlyFromAmong("claude", "codex");
        return option;
    }

    public static Option<bool> Silent() => new("--silent")
    {
        Description = "Use a silent audio sink."
    };
    public static DataPaths Paths(ParseResult result, Option<string?> dataDirectory)
    {
        var paths = new DataPaths(result.GetValue(dataDirectory));
        paths.Ensure();
        return paths;
    }
}
