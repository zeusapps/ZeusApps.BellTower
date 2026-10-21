using System.CommandLine;
using BellTower.Tray;

namespace BellTower.Cli;

internal sealed class TrayCommand : Command
{
    public TrayCommand(Option<string?> dataDirectory) : base("tray", "Run the tray in this process.")
    {
        var silent = CommonOptions.Silent();
        var duration = new Option<int>("--duration-ms")
        {
            Description = "Exit after this many milliseconds (verification only).",
            Hidden = true
        };
        Options.Add(silent);
        Options.Add(duration);
        SetAction(result => new TrayHost(CommonOptions.Paths(result, dataDirectory)).Run(result.GetValue(silent), result.GetValue(duration)));
    }
}
