using System.CommandLine;
using BellTower.Diagnostics;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class DoctorCommand : Command
{
    public DoctorCommand(Option<string?> dataDirectory) : base("doctor", "Report setup observations and known capability limits as JSON.")
    {
        var transcript = CommonOptions.Text("--transcript", "Optionally inspect a bounded read-only transcript tail for verified timeout records.");
        var provider = new Option<string>("--transcript-provider") { DefaultValueFactory = _ => "claude" };
        provider.AcceptOnlyFromAmong("claude", "codex");
        Options.Add(transcript);
        Options.Add(provider);
        SetAction(result => result.InvocationConfiguration.Output.WriteLine(Json.Encode(new DiagnosticsReporter(CommonOptions.Paths(result, dataDirectory))
            .Report(result.GetValue(transcript), result.GetValue(provider)!))));
    }
}
