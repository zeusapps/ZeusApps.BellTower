using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalPresentationResolver(DataPaths paths)
{
    private readonly ConfigStore configuration = new(paths);

    public DesiredTerminalPresentation? Resolve(SessionState session, GitContext git)
    {
        var stored = new AttachmentStore(paths).Get(session.AttachmentId);
        // The one place that adds the native-presentation setting to the stored consent (title and palette background).
        var native = stored is not null && NativePresentation.GrantsPresentation(stored, configuration.Read().NativePresentation);
        var attachment = stored is null ? null : NativePresentation.Effective(stored, native, stored is not null && new ProviderTitleState(paths).DisabledFor(stored.Provider, stored.Process?.StartedUtcTicks));
        if (session.Ended || session.AttachmentHealth == "unavailable" ||
            attachment is not { EndedAt: null, Process: { } agent, Host.Shell: { } shell } || !(attachment.ManagedTitle || attachment.ManagedBackground) ||
            attachment.Health == "unavailable" || attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            attachment.Provider != session.Provider ||
            ProcessProbe.Inspect(agent) != ProcessLiveness.Live || ProcessProbe.Inspect(shell) != ProcessLiveness.Live)
        {
            return null;
        }
        var identity = new IdentityStore(paths).Get(session.Key, session.Directory);
        return new(attachment.Id, attachment.Provider, attachment.RegisteredAt, agent, shell,
            attachment.ManagedTitle ? SessionPresentation.Title(identity.Name, git) : "", attachment.ManagedBackground ? identity.Palette.Background : null);
    }
}
