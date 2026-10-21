namespace BellTower.Core;

public sealed class SessionState
{
    public required string Key { get; init; }
    public required string Provider { get; init; }
    public required string SessionId { get; init; }
    public required string AttachmentId { get; set; }
    public string Directory { get; set; } = "";
    public string? TurnId { get; set; }
    public DateTimeOffset TurnStarted { get; set; }
    public DateTimeOffset LastObserved { get; set; }
    public bool Ended { get; set; }
    public bool ForegroundStopped { get; set; }
    public bool CompletionPending { get; set; }
    public List<BackgroundTask> Background { get; set; } = [];
    public List<WaitState> Waits { get; set; } = [];
    public HashSet<string> CompletedOperations { get; set; } = [];
    public Dictionary<string, InFlightOperation> Operations { get; set; } = [];
    public string? Episode { get; set; }
    public DateTimeOffset? EpisodeStarted { get; set; }
    public string? AcknowledgedEpisode { get; set; }
    public string? ViewedEpisode { get; set; }
    public AttentionReason? Reason => Waits.Count == 0 ? null : Waits.Max(w => w.Reason);
    public DateTimeOffset? Since => Reason is { } reason ? Waits.Where(w => w.Reason == reason).Min(w => w.Since) : null;
    public DateTimeOffset? Due => Since is { } since ? since + (Reason == AttentionReason.Done ? TimeSpan.FromSeconds(20) : TimeSpan.Zero) : null;
    public DateTimeOffset? NotificationDue => EpisodeStarted is { } started ?
        started + (Reason == AttentionReason.Done ? TimeSpan.FromSeconds(20) : TimeSpan.Zero) : Due;

    public bool RequiresAttention(DateTimeOffset now) => !Ended && Episode != AcknowledgedEpisode && Due is { } due && due <= now;
    public string Activity => Ended ? "ended" : Background.Count != 0 ? "background work" : ForegroundStopped ? "idle" : "working";
}
