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
    // Changes only when the unresolved permission-wait identity set changes; prevents scope ABA after waits resolve/reappear.
    public long ApprovalRevision { get; set; }
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
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? NativeUserInputPending { get; set; }
    // The effective confirmation of a wait for reminders and diagnostics: provider hook evidence or fresh owning-runtime status.
    // Question waits are actionable regardless; permission waits are attention only when Confirmed.
    public HumanConfirmation EffectiveConfirmation(WaitState wait) => wait.Reason switch
    {
        AttentionReason.Permission => IsActive(wait) ? HumanConfirmation.Confirmed :
            wait.Confirmation == HumanConfirmation.Confirmed ? HumanConfirmation.Provisional : wait.Confirmation ?? HumanConfirmation.Unverified,
        AttentionReason.Question => wait.Confirmation == HumanConfirmation.Confirmed || NativeUserInputPending == true
            ? HumanConfirmation.Confirmed : wait.Confirmation ?? HumanConfirmation.Unverified,
        _ => wait.Confirmation ?? HumanConfirmation.Confirmed
    };
    // The shared human-wait grace. Elapsed provider notification delay counts toward it (see WaitState.ConfirmationCredit).
    public static readonly TimeSpan HumanWaitGrace = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan CompletionGrace = TimeSpan.FromSeconds(20);
    // Late confirmations arriving this soon after a wait resolved are treated as belonging to it (bounded receipt window).
    public static readonly TimeSpan StaleConfirmationWindow = TimeSpan.FromSeconds(30);
    public List<ApprovalReceipt> ResolvedApprovals { get; set; } = [];
    // A human-prompt notification (Claude permission_prompt) has been observed in this session, so the provider's confirmation
    // signal is demonstrably available for it. Additive snapshot data; absent in older snapshots (read as not yet observed).
    public bool PromptObserved { get; set; }
    // A request pending this long with no supported confirmation signal shows a visibly unverified chip (no sound, no reminders).
    public static readonly TimeSpan UnverifiedGrace = TimeSpan.FromSeconds(15);
    // Set by the provider adapter for the current tick: the session has no supported human-wait signal right now (a Codex session
    // without a verified owning runtime or with a failed status read, a Claude session where no permission_prompt has been
    // observed). Freshness data, never persisted.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ConfirmationSignalUnavailable { get; set; }

    // A permission wait is attention only once a supported signal confirms a human wait: a hook-confirmed wait, or fresh
    // owning-runtime status (Codex). A fresh negative runtime status suppresses it; timing alone never does.
    // Stored-state view used by reducer decisions so the causal and unordered paths agree: transient runtime flags never decide them.
    internal static bool IsConfirmed(WaitState wait) => wait.Reason != AttentionReason.Permission || wait.Confirmation == HumanConfirmation.Confirmed;
    internal AttentionReason? StoredReason => Waits.Where(IsConfirmed).Select(wait => (AttentionReason?)wait.Reason).Max();
    internal bool IsActive(WaitState wait) => wait.Reason != AttentionReason.Permission ||
        NativePermissionPending != false && (NativePermissionPending == true || wait.Confirmation == HumanConfirmation.Confirmed);
    internal List<WaitState> ActiveWaits() => Waits.Where(IsActive).ToList();
    public AttentionReason? Reason => ActiveWaits() is { Count: > 0 } active ? active.Max(w => w.Reason) : null;
    public DateTimeOffset? Since => Reason is { } reason ? ActiveWaits().Where(w => w.Reason == reason).Min(w => w.Since) : null;
    public DateTimeOffset? Due => Reason switch
    {
        null => null,
        AttentionReason.Permission => Min(ActiveWaits().Where(w => w.Reason == AttentionReason.Permission).Select(w => PermissionDue(w, w.Since))),
        AttentionReason.Done => Since + CompletionGrace,
        _ => Since
    };
    private static DateTimeOffset? Min(IEnumerable<DateTimeOffset?> values) => values.Where(v => v is not null).Min();
    private static DateTimeOffset? HookDue(WaitState wait) => wait.Confirmation == HumanConfirmation.Confirmed && wait.ConfirmedAt is { } confirmed
        ? confirmed + (HumanWaitGrace > (wait.ConfirmationCredit ?? TimeSpan.Zero) ? HumanWaitGrace - (wait.ConfirmationCredit ?? TimeSpan.Zero) : TimeSpan.Zero) : null;
    // Hook confirmation (credited provider delay) or runtime confirmation (grace starts when the runtime first reports it).
    private DateTimeOffset? PermissionDue(WaitState wait, DateTimeOffset nativeAnchor) => Min([HookDue(wait),
        NativePermissionPending == true ? (NativePermissionSince is { } confirmed && nativeAnchor < confirmed ? confirmed : nativeAnchor) + HumanWaitGrace : null]);
    public DateTimeOffset? NotificationDue
    {
        get
        {
            if (Reason is null)
            {
                return null;
            }
            if (Reason == AttentionReason.Permission)
            {
                // A newer unconfirmed or auto-approved request must not announce an older outstanding wait, so only waits of the
                // current episode count. Legacy snapshots have no per-wait episode, so use their recorded start.
                var episode = ActiveWaits().Where(wait => wait.Reason == AttentionReason.Permission &&
                    (wait.Episode == Episode || wait.Episode is null && wait.Since == EpisodeStarted)).ToList();
                if (EpisodeStarted is null)
                {
                    return Due;
                }
                // Keep announcement eligibility anchored to the original observation so fresh runtime
                // verification cannot replay a restored wait after restart.
                return Min(episode.Select(wait => Min([HookDue(wait), NativePermissionPending == true ? EpisodeStarted + HumanWaitGrace : null])));
            }
            return EpisodeStarted is { } started ? started + (Reason == AttentionReason.Done ? CompletionGrace : TimeSpan.Zero) : Due;
        }
    }

    // Start of the oldest provisional permission request when the session has no supported confirmation signal.
    public DateTimeOffset? UnverifiedSince => ConfirmationSignalUnavailable
        ? Waits.Where(wait => wait.Reason == AttentionReason.Permission && wait.Confirmation == HumanConfirmation.Provisional && !IsActive(wait))
            .Select(wait => (DateTimeOffset?)wait.Since).Min() : null;

    // The visibly unverified "possibly waiting for approval" chip: shown only when nothing is actionable, never confirmed, silent
    // (not part of RequiresAttention, so no sound, reminder or escalation uses it) and cleared with the request.
    public bool PossiblyWaiting(DateTimeOffset now) => !Ended && AttachmentHealth != "unavailable" && Reason is null && Episode is not null &&
        Episode != AcknowledgedEpisode && UnverifiedSince is { } since && since + UnverifiedGrace <= now;

    public bool RequiresAttention(DateTimeOffset now) => !Ended && AttachmentHealth != "unavailable" && Episode != AcknowledgedEpisode && Due is { } due && due <= now;
    public string Activity => Ended ? "ended" : AttachmentHealth == "unavailable" ? "unavailable" : Background.Count != 0 ? "background work" : ForegroundStopped ? "idle" : "working";
}
