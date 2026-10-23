using System.CommandLine;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class SyncTitleCommand : Command
{
    public SyncTitleCommand(Option<string?> dataDirectory) : base("sync-title", "Apply a managed title to a verified shell console.")
    {
        Hidden = true;
        var session = CommonOptions.Text("--session", "Provider-qualified session ID (legacy helper mode).");
        var desired = new Option<bool>("--desired") { Description = "Read a bounded precomputed title request from stdin." };
        Options.Add(session);
        Options.Add(desired);
        SetAction(result =>
        {
            var service = new TerminalTitleService(CommonOptions.Paths(result, dataDirectory), new AttachedConsoleTitleWriter());
            if (!result.GetValue(desired))
            {
                return result.GetValue(session) is { } key && service.Apply(key) ? 0 : 1;
            }
            var buffer = new char[4097];
            var count = Console.In.ReadBlock(buffer, 0, buffer.Length);
            if (count == 0 || count > 4096)
            {
                return 1;
            }
            try
            {
                return service.Apply(Json.Decode<DesiredTerminalPresentation>(new string(buffer, 0, count))) ? 0 : 1;
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or ArgumentException)
            {
                return 1;
            }
        });
    }
}
