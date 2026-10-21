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

}
