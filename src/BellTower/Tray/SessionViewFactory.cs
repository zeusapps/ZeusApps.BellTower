using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Tray;

public sealed class SessionViewFactory(IdentityStore identities, AttachmentStore attachments, Navigator navigation)
{
    public IReadOnlyList<ChipView> Chips(IEnumerable<SessionState> sessions, DateTimeOffset now, IReadOnlyDictionary<string, GitContext> contexts) =>
        sessions.Where(session => session.RequiresAttention(now)).Select(session =>
        {
            var identity = identities.Get(session.Key, session.Directory);
            return new ChipView(session.Key, session.Episode!, identity.Name, identity.Palette,
                AlertPolicy.ReasonText(session.Reason), session.ViewedEpisode == session.Episode ? "" : AlertPolicy.Elapsed(session.Since!.Value, now),
                contexts.GetValueOrDefault(session.Key)?.ToString() ?? "", navigation.Precision(attachments.Get(session.AttachmentId)));
        }).ToArray();

    public IReadOnlyList<OverviewRow> Overview(IEnumerable<SessionState> sessions, IReadOnlyDictionary<string, GitContext> contexts) =>
        sessions.Select(session => new OverviewRow(session.Key, session, identities.Get(session.Key, session.Directory),
            contexts.GetValueOrDefault(session.Key) ?? new(), navigation.Precision(attachments.Get(session.AttachmentId)))).ToArray();
}
