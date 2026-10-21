using System.CommandLine;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class FocusCommand : Command
{
    public FocusCommand(Option<string?> dataDirectory) : base("focus", "Focus the verified terminal destination of a session.")
    {
        var session = CommonOptions.Text("--session", "Provider-qualified session ID.", true);
        Options.Add(session);
        SetAction(result =>
        {
            var focus = new SessionCatalog(CommonOptions.Paths(result, dataDirectory)).FocusAsync(result.GetValue(session)!).GetAwaiter().GetResult();
            result.InvocationConfiguration.Output.WriteLine(focus.Message);
            return focus.Success ? 0 : 1;
        });
    }
}
