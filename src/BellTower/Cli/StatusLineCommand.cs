using System.CommandLine;
using BellTower.Providers;

namespace BellTower.Cli;

internal sealed class StatusLineCommand : Command
{
    public StatusLineCommand(Option<string?> dataDirectory) : base("statusline", "Render the bundled session name chip from status-line JSON.")
    {
        var provider = CommonOptions.Provider(false, "claude");
        var noColor = new Option<bool>("--no-color")
        {
            Description = "Render plain text without ANSI colors."
        };
        Options.Add(provider);
        Options.Add(noColor);
        SetAction(result =>
        {
            var renderer = new StatusLineReader(CommonOptions.Paths(result, dataDirectory));
            result.InvocationConfiguration.Output.WriteLine(renderer.Render(Console.In, result.GetValue(provider)!, !result.GetValue(noColor)));
        });
    }
}
