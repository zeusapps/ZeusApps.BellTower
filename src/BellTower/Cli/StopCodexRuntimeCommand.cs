using System.CommandLine;
using BellTower.IO;

namespace BellTower.Cli;

// Invoked by the owning shell only after its interactive Codex child exits.
internal sealed class StopCodexRuntimeCommand : Command
{
    public StopCodexRuntimeCommand(Option<string?> dataDirectory) : base("stop-codex-runtime")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Owned runtime attachment ID.", true);
        Options.Add(attachment);
        SetAction(result => new CodexRuntimeStore(CommonOptions.Paths(result, dataDirectory)).StopAndWait(result.GetValue(attachment)!));
    }
}
