using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitleService(DataPaths paths, IConsoleTitleWriter writer)
{
    public bool Apply(DesiredTerminalPresentation desired)
    {
        if (string.IsNullOrEmpty(desired.Title) || desired.Title.Length > 512 || desired.Title.Any(char.IsControl) ||
            string.IsNullOrEmpty(desired.AttachmentId) || desired.AttachmentId.Length > 256 || desired.Agent is null || desired.Shell is null ||
            desired.Background is not null && !Core.Palette.IsHex(desired.Background))
        {
            return false;
        }
        using var gate = new DataLock(paths, "presentation");
        var attachment = new AttachmentStore(paths).Get(desired.AttachmentId);
        if (attachment is not { ManagedTitle: true, EndedAt: null } || attachment.Health == "unavailable" ||
            attachment.Provider != desired.Provider || attachment.RegisteredAt != desired.RegisteredAt ||
            attachment.Process != desired.Agent || attachment.Host.Shell != desired.Shell ||
            attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            desired.Background is not null && !attachment.ManagedBackground ||
            ProcessProbe.Inspect(desired.Agent) != ProcessLiveness.Live || ProcessProbe.Inspect(desired.Shell) != ProcessLiveness.Live)
        {
            return false;
        }
        return writer.Write(desired.Shell, desired.Agent, desired.Title, desired.Background);
    }

    // Compatibility path for older callers. The tray uses only precomputed desired values.
    public bool Apply(string key)
    {
        using var gate = new DataLock(paths, "presentation");
        if (!new StateStore(paths).Read().Sessions.TryGetValue(key, out var session) || session.Ended || session.AttachmentHealth == "unavailable")
        {
            return false;
        }
        var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
        if (attachment is not { ManagedTitle: true, EndedAt: null, Process: { } agent, Host.Shell: { } shell } ||
            attachment.Health == "unavailable" ||
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
