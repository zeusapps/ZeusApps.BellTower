namespace BellTower.Core;

public sealed class SessionState
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public required string Key { get; init; }
    public required string Provider { get; init; }
    public required string SessionId { get; init; }
    public required string AttachmentId { get; set; }
    public string Directory { get; set; } = "";
    public string? TurnId { get; set; }
    public DateTimeOffset TurnStarted { get; set; }
    public DateTimeOffset LastObserved { get; set; }
    public List<Observation> CausalEvents { get; set; } = [];
    public SessionState? CausalCheckpoint { get; set; }
    public Observation? CausalFloor { get; set; }
    public bool Ended { get; set; }
    public string AttachmentHealth { get; set; } = "unverified";
    public DateTimeOffset? AttachmentUnverifiedSince { get; set; }
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
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? NativePermissionPending { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset? NativePermissionSince { get; set; }
    public AttentionReason? Reason => Waits.Count == 0 ? null : Waits.Max(w => w.Reason);
    public DateTimeOffset? Since => Reason is { } reason ? Waits.Where(w => w.Reason == reason).Min(w => w.Since) : null;
    private TimeSpan Grace => Reason switch
    {
        AttentionReason.Done => TimeSpan.FromSeconds(20),
        AttentionReason.Permission => TimeSpan.FromSeconds(NativePermissionPending == true ? 5 : 15),
        _ => TimeSpan.Zero
    };
    public DateTimeOffset? Due => PermissionStart(Since) is { } since ? since + Grace : null;
    private DateTimeOffset? PermissionStart(DateTimeOffset? started) => Reason != AttentionReason.Permission ? started :
        NativePermissionPending == false ? null : NativePermissionSince is { } confirmed && started < confirmed ? confirmed : started;
    public DateTimeOffset? NotificationDue
    {
        get
        {
            // A newer auto-approved request must not announce an older outstanding wait.
            // Legacy snapshots have no per-wait episode, so use their recorded start.
            if (Reason == AttentionReason.Permission && EpisodeStarted is { } permissionStarted &&
                !Waits.Any(wait => wait.Reason == AttentionReason.Permission &&
                    (wait.Episode == Episode || wait.Episode is null && wait.Since == permissionStarted)))
            {
                return null;
            }
            // Keep announcement eligibility anchored to the original observation so
            // fresh runtime verification cannot replay a restored wait after restart.
            return EpisodeStarted is { } started && (Reason != AttentionReason.Permission || NativePermissionPending != false)
                ? started + Grace : Due;
        }
    }

    public bool RequiresAttention(DateTimeOffset now) => !Ended && AttachmentHealth != "unavailable" && Episode != AcknowledgedEpisode && Due is { } due && due <= now;
    public string Activity => Ended ? "ended" : AttachmentHealth == "unavailable" ? "unavailable" : Background.Count != 0 ? "background work" : ForegroundStopped ? "idle" : "working";
}
