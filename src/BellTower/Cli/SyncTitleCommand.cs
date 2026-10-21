using System.CommandLine;
using BellTower.Hosts;

namespace BellTower.Cli;

internal sealed class SyncTitleCommand : Command
{
    public SyncTitleCommand(Option<string?> dataDirectory) : base("sync-title", "Apply a managed title to a verified shell console.")
    {
        Hidden = true;
        var session = CommonOptions.Text("--session", "Provider-qualified session ID.", true);
        Options.Add(session);
        SetAction(result => new TerminalTitleService(CommonOptions.Paths(result, dataDirectory), new AttachedConsoleTitleWriter())
            .Apply(result.GetValue(session)!) ? 0 : 1);
    }
}
