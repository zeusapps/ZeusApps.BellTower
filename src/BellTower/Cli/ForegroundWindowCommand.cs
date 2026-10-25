using System.CommandLine;
using System.Globalization;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Cli;

/// <summary>
/// Reports the current foreground window for the VS Code bridge. The bridge calls it only while its own
/// editor window reports focus, and the result is accepted only when two samples agree.
/// </summary>
internal sealed class ForegroundWindowCommand : Command
{
    public ForegroundWindowCommand() : base("foreground-window", "Report a stable foreground window for the terminal bridge.")
    {
        Hidden = true;
        SetAction(result =>
        {
            var windows = new NativeWindowInspector();
            var first = windows.Foreground();
            Thread.Sleep(30);
            var second = windows.Foreground();
            if (first is null || first != second || ProcessProbe.Identify(first.OwnerPid) is not { } owner)
            {
                return 1;
            }

            result.InvocationConfiguration.Output.WriteLine(Json.Encode(new
            {
                Handle = first.Handle.ToString(CultureInfo.InvariantCulture),
                first.ClassName,
                OwnerPid = owner.Pid,
                OwnerStartedUtcTicks = owner.StartedUtcTicks.ToString(CultureInfo.InvariantCulture)
            }));
            return 0;
        });
    }
}
