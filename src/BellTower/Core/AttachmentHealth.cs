using BellTower.IO;

namespace BellTower.Core;

/// <summary>
/// The single evaluator of attachment health. A directly attached process (and any non-shared record) keeps the original exit,
/// grace and stickiness rules unchanged. A shared-backend conversation, which has no process of its own, is reduced by
/// <see cref="ConversationLifecycle"/> from the stored evidence and an optional read-only facts snapshot. Pure policy: process
/// liveness is injected, nothing here reads the disk or the machine.
/// </summary>
public sealed class AttachmentHealth(Func<ProcessIdentity, ProcessLiveness> inspect)
{
    public Attachment Evaluate(Attachment attachment, DateTimeOffset now, TimeSpan grace, ConversationFacts? facts = null)
    {
        if (attachment.EndedAt is null && ConversationLifecycle.IsSharedConversation(attachment))
        {
            return ConversationLifecycle.Reduce(attachment, facts, now, grace, inspect);
        }
        var agent = attachment.Process is { } process ? inspect(process) : ProcessLiveness.Unverified;
        var shell = attachment.Host.Shell is { } owner ? inspect(owner) : ProcessLiveness.Unverified;
        if (attachment.EndedAt is not null || agent == ProcessLiveness.Ended || shell == ProcessLiveness.Ended)
        {
            return attachment with { Health = "confirmed-ended", EndedAt = attachment.EndedAt ?? now };
        }
        if (agent == ProcessLiveness.Live)
        {
            return attachment with { Health = "verified-live", UnverifiedSince = null };
        }
        var since = attachment.UnverifiedSince ?? (attachment.Health == "verified-live" && attachment.Process is not null ? now : attachment.RegisteredAt);
        return attachment with
        {
            Health = attachment.Health == "unavailable" || now - since >= grace ? "unavailable" : "unverified",
            UnverifiedSince = since
        };
    }
}
