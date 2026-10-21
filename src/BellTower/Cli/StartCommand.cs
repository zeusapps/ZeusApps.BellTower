using System.CommandLine;
using BellTower.Tray;

namespace BellTower.Cli;

internal sealed class StartCommand : Command
{
    public StartCommand(Option<string?> dataDirectory) : base("start", "Start a detached tray.")
    {
        var silent = CommonOptions.Silent();
        Options.Add(silent);
        SetAction(result => new TrayController(CommonOptions.Paths(result, dataDirectory)).Start(result.GetValue(silent)));
    }
}
