using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class WindowsTerminalFocusService(DataPaths paths, TerminalTabSelector tabs, WindowActivator activation,
    Func<ProcessIdentity, ProcessIdentity, ISessionConsole?> openConsole, Func<HostTarget, bool> windowOwned)
{
    public FocusResult Focus(string attachmentId)
    {
        using var gate = new DataLock(paths, "presentation");
        var attachment = new AttachmentStore(paths).Get(attachmentId);
        if (attachment is not { EndedAt: null, Process: { } agent, Host.Kind: "windows-terminal", Host.Shell: { } shell })
        {
            return new(false, "unavailable", "The Windows Terminal session is no longer registered.");
        }
        bool StillOwned() => ProcessProbe.Identify(agent.Pid) == agent && ProcessProbe.Identify(shell.Pid) == shell && windowOwned(attachment.Host);
        if (!StillOwned())
        {
            return new(false, "unavailable", "The Windows Terminal session or window cannot be verified.");
        }
        var selected = false;
        if (attachment.ManagedTitle)
        {
            using var console = openConsole(shell, agent);
            if (console is not null)
            {
                var previous = console.ReadTitle();
                var marker = "BellTower-" + Guid.NewGuid().ToString("N");
                try
                {
                    if (console.SetTitle(marker))
                    {
                        selected = tabs.Select(attachment.Host.WindowHandle, marker, StillOwned);
                    }
                }
                finally
                {
                    console.SetTitle(previous);
                }
            }
        }
        if (!StillOwned())
        {
            return new(false, "unavailable", "The Windows Terminal destination changed before activation.");
        }
        var precision = selected ? "exact tab" : "window only";
        return activation.Activate(new IntPtr(attachment.Host.WindowHandle))
            ? new(true, precision, selected ? "The session's Windows Terminal tab was selected; split-pane focus is unchanged." : "Windows Terminal focused; its tab could not be identified. Select the tab or pane manually.")
            : new(false, precision, "Windows declined foreground activation.");
    }
}
