using System.CommandLine;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class PrepareSessionCommand : Command
{
    public PrepareSessionCommand(Option<string?> dataDirectory) : base("prepare-session", "Prepare a managed launch palette before the native agent starts.")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Attachment ID.", true);
        var directory = CommonOptions.Text("--directory", "Agent working directory.", true);
        Options.Add(attachment);
        Options.Add(directory);
        SetAction(result =>
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BELLTOWER_SHELL_PID"), out var pid) ||
                ProcessProbe.Identify(pid) is not { } shell || !ProcessProbe.Ancestors(Environment.ProcessId).Any(parent => parent.Pid == pid))
            {
                throw new InvalidOperationException("The launching shell could not be verified.");
            }
            var paths = CommonOptions.Paths(result, dataDirectory);
            var launch = new LaunchPresentationStore(paths).Prepare(result.GetValue(attachment)!, shell);
            var location = Path.GetFullPath(result.GetValue(directory)!);
            var name = IdentityText.Optional(Environment.GetEnvironmentVariable("BELLTOWER_NAME")) ??
                IdentityText.Optional(Environment.GetEnvironmentVariable("BELLTOWER_CHANGE")) ?? new DirectoryInfo(location).Name;
            result.InvocationConfiguration.Output.WriteLine(Json.Encode(new
            {
                launch.Palette,
                Title = SessionPresentation.Title(name, new GitReader().Read(location)),
                Colors = TerminalColors.Apply(launch.Palette.Background)
            }));
        });
    }
}
