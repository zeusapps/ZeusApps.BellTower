using System.CommandLine;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class ListCommand : Command
{
    public ListCommand(Option<string?> dataDirectory) : base("list", "List live sessions and their navigation precision as JSON.")
    {
        SetAction(result => result.InvocationConfiguration.Output.WriteLine(Json.Encode(new SessionCatalog(CommonOptions.Paths(result, dataDirectory)).List())));
    }
}
