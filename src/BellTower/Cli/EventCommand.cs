using System.CommandLine;
using BellTower.Providers;

namespace BellTower.Cli;

internal sealed class EventCommand : Command
{
    public EventCommand(Option<string?> dataDirectory) : base("event", "Read and normalize provider hook JSON from standard input.")
    {
        var provider = CommonOptions.Provider();
        Options.Add(provider);
        SetAction(result =>
        {
            new HookIngestor(CommonOptions.Paths(result, dataDirectory)).Ingest(result.GetValue(provider)!, Console.In);
            result.InvocationConfiguration.Output.WriteLine("{}");
        });
    }
}
