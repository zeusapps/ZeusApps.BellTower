using System.CommandLine;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class DetachCommand : Command
{
    public DetachCommand(Option<string?> dataDirectory) : base("detach", "End shell attachment metadata without signaling any process.")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Shell attachment ID.", true);
        Options.Add(attachment);
        SetAction(result => new AttachmentLifecycle(CommonOptions.Paths(result, dataDirectory)).Detach(result.GetValue(attachment)!));
    }
}
