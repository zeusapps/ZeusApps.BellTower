using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalPresentationResolver(DataPaths paths)
{
    public DesiredTerminalPresentation? Resolve(SessionState session, GitContext git)
    {
        var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
        if (session.Ended || session.AttachmentHealth == "unavailable" ||
            attachment is not { ManagedTitle: true, EndedAt: null, Process: { } agent, Host.Shell: { } shell } ||
            attachment.Health == "unavailable" || attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            attachment.Provider != session.Provider ||
            ProcessProbe.Inspect(agent) != ProcessLiveness.Live || ProcessProbe.Inspect(shell) != ProcessLiveness.Live)
        {
            return null;
        }
        var identity = new IdentityStore(paths).Get(session.Key, session.Directory);
        return new(attachment.Id, attachment.Provider, attachment.RegisteredAt, agent, shell,
            SessionPresentation.Title(identity.Name, git), attachment.ManagedBackground ? identity.Palette.Background : null);
    }
}
