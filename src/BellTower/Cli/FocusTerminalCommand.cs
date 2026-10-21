using System.CommandLine;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class FocusTerminalCommand : Command
{
    public FocusTerminalCommand(Option<string?> dataDirectory) : base("focus-terminal", "Resolve and select a verified Windows Terminal tab.")
    {
        Hidden = true;
        var attachment = CommonOptions.Text("--attachment", "Attachment ID.", true);
        Options.Add(attachment);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            var focus = Task.Run(() => new WindowsTerminalFocusService(paths, new(new AutomationTerminalTabSource()),
                new(new NativeWindowActivationApi()), AttachedSessionConsole.Open, WindowsTerminalNavigator.IsAvailable)
                .Focus(result.GetValue(attachment)!)).GetAwaiter().GetResult();
            result.InvocationConfiguration.Output.WriteLine(Json.Encode(focus));
            return focus.Success ? 0 : 1;
        });
    }
}
