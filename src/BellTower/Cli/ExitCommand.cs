using System.CommandLine;
using BellTower.Tray;

namespace BellTower.Cli;

internal sealed class ExitCommand : Command
{
    public ExitCommand(Option<string?> dataDirectory) : base("exit", "Stop Bell Tower for this data directory.")
    {
        SetAction(result => new TrayController(CommonOptions.Paths(result, dataDirectory)).RequestExit());
    }
}
