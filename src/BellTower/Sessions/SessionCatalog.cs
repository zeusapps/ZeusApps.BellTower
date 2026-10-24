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
    private readonly DataPaths paths;
    public SessionCatalog(DataPaths paths)
    {
        this.paths = paths;
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
                // Both clients expose the same native-name status; only the displayed text differs by adapter.
                NativePresentationName = session.Provider == "codex" ? SessionPresentation.CodexName(identity.Name, new GitReader().Read(identity.Checkout(session.Directory))) : identity.Name,
                NativeNameSynchronization = NativeNamePolicy.StatusFor(session.Provider, identity),
                Checkout = identity.Checkout(session.Directory),
                CheckoutSource = identity.CheckoutPin is null ? identity.CheckoutSource : CheckoutPolicy.User
            };
        }).ToArray();
    public SessionIdentity Rename(string key, string? name, string? change)
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
                ChangeOrigin.Pin(identity, IdentityText.Optional(change));
                identity.SelectionOperation = null;
                identity.SelectionRoot = null;
                identity.SelectionStore = null;
            }

            return name is not null || change is not null;
        });
    }

    public Task<FocusResult> FocusAsync(string key) => navigator.FocusAsync(attachments.Get(Find(key).AttachmentId));
    public SessionIdentity SetNativeNames(string key, bool enabled)
    {
        var session = Find(key);
        NativeNameControl.SetEnabled(paths, session, enabled);
        return identities.Get(key, session.Directory);
    }

    // Shared by both clients (CLI and tray): return from a native user title to the automatic or Bell Tower name.
    public SessionIdentity UseBellTowerName(string key)
    {
        var session = Find(key);
        NativeNameControl.UseBellTowerName(paths, session);
        return identities.Get(key, session.Directory);
    }

    // Attach (path) or clear (empty) the working checkout, set a named/hex/random colour, or reset the Bell Tower pins.
    // Attention state is stored elsewhere and is never touched here.
    public SessionIdentity Configure(string key, string? checkout, string? color, bool reset)
    {
        var session = Find(key);
        string? pin = null;
        if (checkout is { Length: > 0 })
        {
            if (!Path.IsPathFullyQualified(checkout) || !Directory.Exists(checkout))
            { throw new ArgumentException("Checkout must be an existing absolute directory."); }
            pin = new GitReader().Toplevel(checkout) is { } top ? Path.GetFullPath(top) : throw new ArgumentException("Checkout must be inside a Git working tree.");
        }
        if (color is not null && !string.Equals(color, "random", StringComparison.OrdinalIgnoreCase))
        { _ = Palette.FromColor(color.Trim()); }
        if (checkout is not null)
        {
            identities.Update(key, session.Directory, value => { value.CheckoutPin = pin; return true; });
        }
        return color is not null || reset ? identities.SetColor(key, session.Directory, color, reset) : identities.Get(key, session.Directory);
    }
    private SessionState Find(string key) => sessions.Read().Sessions.TryGetValue(key, out var session) ? session : throw new ArgumentException("Session is not registered.");
}
