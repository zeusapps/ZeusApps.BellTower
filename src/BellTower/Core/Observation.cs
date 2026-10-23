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
    public string Key => Provider + ":" + SessionId;
}
