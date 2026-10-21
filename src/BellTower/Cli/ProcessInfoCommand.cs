using System.CommandLine;
using System.Globalization;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class ProcessInfoCommand : Command
{
    public ProcessInfoCommand() : base("process-info", "Report a process identity for the terminal bridge.")
    {
        var pid = new Option<int>("--pid")
        {
            Description = "Process ID.",
            Required = true
        };
        Options.Add(pid);
        SetAction(result =>
        {
            var process = ProcessProbe.Identify(result.GetValue(pid)) ?? throw new InvalidOperationException("Process is unavailable.");
            result.InvocationConfiguration.Output.WriteLine(Json.Encode(new
            {
                process.Pid,
                StartedUtcTicks = process.StartedUtcTicks.ToString(CultureInfo.InvariantCulture)
            }));
        });
    }
}
