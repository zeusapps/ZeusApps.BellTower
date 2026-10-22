using System.CommandLine;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class CodexRuntimeHostCommand : Command
{
    public CodexRuntimeHostCommand(Option<string?> dataDirectory) : base("codex-runtime-host")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Managed attachment ID.", true);
        var executable = CommonOptions.Text("--executable", "Resolved native agent or shim.", true);
        Options.Add(attachment);
        Options.Add(executable);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            try
            {
                new CodexRuntimeHost(paths).RunAsync(result.GetValue(attachment)!, result.GetValue(executable)!, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
            {
                new LocalLog(paths).Write("codex-runtime-unavailable");
            }
        });
    }
}
