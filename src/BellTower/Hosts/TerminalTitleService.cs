using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitleService(DataPaths paths, IConsoleTitleWriter writer)
{
    public bool Apply(string key)
    {
        using var gate = new DataLock(paths, "presentation");
        if (!new StateStore(paths).Read().Sessions.TryGetValue(key, out var session) || session.Ended)
        {
            return false;
        }
        var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
        if (attachment is not { ManagedTitle: true, EndedAt: null, Process: { } agent, Host.Shell: { } shell } ||
            attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            ProcessProbe.Identify(agent.Pid) != agent || ProcessProbe.Identify(shell.Pid) != shell)
        {
            return false;
        }
        var identity = new IdentityStore(paths).Get(session.Key, session.Directory);
        var title = Core.SessionPresentation.Title(identity.Name, new GitReader().Read(session.Directory));
        return writer.Write(shell, agent, title, attachment.ManagedBackground ? identity.Palette.Background : null);
    }
}
