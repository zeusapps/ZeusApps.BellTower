using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

public sealed class AttachmentLifecycle(DataPaths paths)
{
    public void Detach(string id)
    {
        // Synchronize with the title helper before the shell restores its original title.
        using var gate = new DataLock(paths, "presentation");
        new LaunchPresentationStore(paths).Remove(id);
        var attachments = new AttachmentStore(paths);
        var attachment = attachments.Get(id);
        if (attachment is null)
        {
            return;
        }
        attachments.Save(attachment with { EndedAt = DateTimeOffset.UtcNow });
        var events = new EventStore(paths);
        foreach (var session in new StateStore(paths).Read().Sessions.Values.Where(session => session.AttachmentId == id && !session.Ended))
        {
            events.Publish(new Observation { Provider = session.Provider, SessionId = session.SessionId, AttachmentId = id, Kind = EventKind.End });
        }
    }
}
