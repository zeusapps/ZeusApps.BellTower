using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Tray;

public sealed class SessionViewFactory(IdentityStore identities, AttachmentStore attachments, Navigator navigation)
{
    public IReadOnlyList<ChipView> Chips(IEnumerable<SessionState> sessions, DateTimeOffset now, IReadOnlyDictionary<string, GitContext> contexts) =>
        sessions.Where(session => session.RequiresAttention(now) || session.PossiblyWaiting(now)).Select(session =>
        {
            var identity = identities.Get(session.Key, session.Directory);
            // A confirmed or question wait always wins; the unverified chip is only for a request nothing confirms.
            var unverified = !session.RequiresAttention(now);
            return new ChipView(session.Key, session.Episode!, identity.Name, identity.Palette,
                unverified ? AlertPolicy.UnverifiedText : AlertPolicy.ReasonText(session.Reason),
                session.ViewedEpisode == session.Episode ? "" : AlertPolicy.Elapsed(unverified ? session.UnverifiedSince!.Value : session.Since!.Value, now),
                contexts.GetValueOrDefault(session.Key)?.ToString() ?? "", navigation.Precision(attachments.Get(session.AttachmentId)), unverified);
        }).ToArray();

    public IReadOnlyList<OverviewRow> Overview(IEnumerable<SessionState> sessions, IReadOnlyDictionary<string, GitContext> contexts) =>
        sessions.Select(session => new OverviewRow(session.Key, session, identities.Get(session.Key, session.Directory),
            contexts.GetValueOrDefault(session.Key) ?? new(), navigation.Precision(attachments.Get(session.AttachmentId)))).ToArray();
}
