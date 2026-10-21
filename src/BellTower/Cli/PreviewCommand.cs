using System.CommandLine;
using BellTower.Tray;

namespace BellTower.Cli;

internal sealed class PreviewCommand : Command
{
    public PreviewCommand() : base("preview", "Save a sample attention strip as a PNG.")
    {
        var output = CommonOptions.Text("--output", "Output PNG path.", true);
        Options.Add(output);
        SetAction(result => StripPreview.Save(result.GetValue(output)!));
    }
}
