using System.CommandLine;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class PrepareCodexRuntimeCommand : Command
{
    public PrepareCodexRuntimeCommand(Option<string?> dataDirectory) : base("prepare-codex-runtime")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Managed attachment ID.", true);
        var executable = CommonOptions.Text("--executable", "Resolved native agent or shim.", true);
        Options.Add(attachment);
        Options.Add(executable);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            var service = new CodexRuntimeHost(paths);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var runtime = service.StartAsync(result.GetValue(attachment)!, result.GetValue(executable)!, timeout.Token).GetAwaiter().GetResult();
            result.InvocationConfiguration.Output.WriteLine(Json.Encode(new
            {
                runtime.Endpoint,
                TokenPath = new CodexRuntimeStore(paths).TokenPath(runtime.Attachment)
            }));
        });
    }
}
