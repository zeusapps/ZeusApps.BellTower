using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Sessions;

public sealed class SessionCatalog
{
    private readonly StateStore sessions;
    private readonly AttachmentStore attachments;
    private readonly IdentityStore identities;
    private readonly Navigator navigator;
    public SessionCatalog(DataPaths paths)
    {
        sessions = new StateStore(paths);
        attachments = new AttachmentStore(paths);
        identities = new IdentityStore(paths);
        navigator = new Navigator(paths);
    }

    public IReadOnlyList<SessionSummary> List() => sessions.Read().Sessions.Values.Where(session => !session.Ended)
        .Select(session =>
        {
            var identity = identities.Get(session.Key, session.Directory);
            return new SessionSummary(session.Key, identity, session.Activity,
                session.Reason, session.Since, navigator.Precision(attachments.Get(session.AttachmentId)))
            {
                AttachmentHealth = session.AttachmentHealth,
                NativePresentationName = session.Provider == "codex" ? SessionPresentation.CodexName(identity.Name, new GitReader().Read(session.Directory)) : null,
                NativeNameSynchronization = session.Provider == "codex" ? identity.NativeSyncStatus : null
            };
        }).ToArray();
    public SessionIdentity Rename(string key, string? name, string? change, string? role)
    {
        var session = Find(key);
        return identities.Update(key, session.Directory, identity =>
        {
            if (name is not null)
            {
                identity.ManualName = IdentityText.Optional(name);
            }

            if (change is not null)
            {
                identity.Change = IdentityText.Optional(change);
                identity.SelectionOperation = null;
                identity.SelectionRoot = null;
                identity.SelectionStore = null;
            }

            if (role is not null)
            {
                identity.Role = IdentityText.Optional(role);
            }
            return name is not null || change is not null || role is not null;
        });
    }

    public Task<FocusResult> FocusAsync(string key) => navigator.FocusAsync(attachments.Get(Find(key).AttachmentId));
    private SessionState Find(string key) => sessions.Read().Sessions.TryGetValue(key, out var session) ? session : throw new ArgumentException("Session is not registered.");
}
