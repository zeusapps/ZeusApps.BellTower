namespace BellTower.Core;

public sealed class AlertPolicy(DateTimeOffset startedAt)
{
    private readonly HashSet<string> announced = [];
    public List<SessionState> Collect(IEnumerable<SessionState> sessions, DateTimeOffset now, bool muted)
    {
        var result = new List<SessionState>();
        foreach (var s in sessions)
        {
            if (!s.RequiresAttention(now) || s.Episode is null || s.NotificationDue is not { } due || due > now)
            {
                continue;
            }

            var key = s.Key + ":" + s.Episode;
            if (!announced.Add(key))
            {
                continue;
            }

            if (!muted && due > startedAt)
            {
                result.Add(s);
            }
        }

        return result;
    }

    public static string ReasonText(AttentionReason? reason) => reason switch
    {
        AttentionReason.Permission => "needs permission",
        AttentionReason.Question => "has a question",
        AttentionReason.Error => "needs help",
        AttentionReason.Done => "finished",
        _ => "working"
    };
    public static string Elapsed(DateTimeOffset since, DateTimeOffset now)
    {
        var elapsed = now > since ? now - since : TimeSpan.Zero;
        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    public static string Phrase(SessionState session, SessionIdentity identity)
    {
        var subject = string.Join(" ", identity.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4));
        return string.Join(", ", new[] { identity.Role, subject, ReasonText(session.Reason) }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
