using System.CommandLine;
using BellTower.Diagnostics;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class DoctorCommand : Command
{
    public DoctorCommand(Option<string?> dataDirectory) : base("doctor", "Report setup observations and known capability limits as JSON.")
    {
        SetAction(result => result.InvocationConfiguration.Output.WriteLine(Json.Encode(new DiagnosticsReporter(CommonOptions.Paths(result, dataDirectory)).Report())));
    }
}
