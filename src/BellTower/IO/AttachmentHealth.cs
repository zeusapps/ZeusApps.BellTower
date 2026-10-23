using BellTower.Core;

namespace BellTower.IO;

public sealed class AttachmentHealth(Func<ProcessIdentity, ProcessLiveness>? inspect = null)
{
    private readonly Func<ProcessIdentity, ProcessLiveness> inspect = inspect ?? ProcessProbe.Inspect;

    public Attachment Evaluate(Attachment attachment, DateTimeOffset now, TimeSpan grace)
    {
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
