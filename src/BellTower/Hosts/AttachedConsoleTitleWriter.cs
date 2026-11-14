using BellTower.Core;

namespace BellTower.Hosts;

// Use only inside the short-lived sync-title helper, never inside the tray or a hook invocation.
public sealed class AttachedConsoleTitleWriter : IConsoleTitleWriter
{
    public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null)
    {
        using var console = AttachedSessionConsole.Open(shell, agent);
        if (console is null)
        {
            return false;
        }
        var changed = console.SetTitle(title);
        return background is null ? changed : console.Write(TerminalColors.Apply(background)) && changed;
    }

    public string? ReadTitle(ProcessIdentity shell, ProcessIdentity agent)
    {
        using var console = AttachedSessionConsole.Open(shell, agent);
        return console?.ReadTitle();
    }

    public bool ResetColors(ProcessIdentity shell)
    {
        using var console = AttachedSessionConsole.OpenShell(shell);
        return console is not null && console.Write(TerminalColors.Reset);
    }

    public bool RestoreTitle(ProcessIdentity shell, string expected, string previous)
    {
        using var console = AttachedSessionConsole.OpenShell(shell);
        if (console is null)
        {
            return false;
        }
        return console.ReadTitle() != expected || console.SetTitle(previous);
    }
}
