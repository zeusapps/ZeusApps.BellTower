namespace BellTower.Core;

public sealed record Observation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string Provider { get; init; }
    public required string SessionId { get; init; }
    public required string AttachmentId { get; init; }
    public required EventKind Kind { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? IngressAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? HookShellStartedAt { get; init; }
    public string? OrderConfidence { get; init; }
    public string? RegistrationSource { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public string Directory { get; init; } = "";
    public string? TurnId { get; init; }
    public string? OperationId { get; init; }
    public string? OperationFingerprint { get; init; }
    public string? BackgroundId { get; init; }
    public string? BackgroundType { get; init; }
    public List<BackgroundTask>? BackgroundSnapshot { get; init; }
    public bool IsMainThread { get; init; } = true;
    // Provider adapters set these; the shared reducer owns the policy.
    // HumanPrompt: the provider says a person is being asked (Claude permission_prompt notification).
    public bool HumanPrompt { get; init; }
    // QuestionTool: the observation concerns a question tool (AskUserQuestion, request_user_input[_async]).
    public bool QuestionTool { get; init; }
    // ProviderDelay: documented/observed delay the provider already applied before emitting a human-prompt signal.
    public TimeSpan? ProviderDelay { get; init; }
    // The provider's permission mode when the hook reports one (diagnostics only; never policy input).
    public string? PermissionMode { get; init; }
    // The producer's observation shape. Null means a producer older than the human-wait fields (a cached ingress image
    // that predates this change), so the consumer re-derives the legacy permission_prompt shape instead of trusting absent flags.
    public int? AdapterRevision { get; init; }
    // The owning runtime (not a hook) reported a pending human approval; confirms provisional permission waits of the session.
    public bool RuntimeApproval { get; init; }
    // With RuntimeApproval: the oldest start of a request that predates the tray run. Waits starting at or before it are confirmed
    // back-dated (silent restore) while the observation keeps its own time, so the causal journal order is unchanged.
    public DateTimeOffset? RestoredSince { get; init; }
    // Runtime approval is not operation-attributed by Codex. This opaque snapshot scope limits it to the exact unresolved
    // permission waits inspected when the runtime query began. Missing scope is not confirmation evidence.
    public string[]? RuntimeApprovalScope { get; init; }
    // Generation of the permission-wait set inspected by the owning runtime. Missing values are legacy and cannot confirm.
    public long? RuntimeApprovalRevision { get; init; }
    public const int HumanWaitRevision = 1;
    public string Key => Provider + ":" + SessionId;
}
