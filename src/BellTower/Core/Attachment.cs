namespace BellTower.Core;

public sealed record Attachment(string Id, string Provider, ProcessIdentity? Process, HostTarget Host,
    string Directory, DateTimeOffset RegisteredAt, DateTimeOffset? EndedAt = null, bool ManagedTitle = false, bool ManagedBackground = false)
{
    public string Health { get; init; } = "unverified";
    public DateTimeOffset? UnverifiedSince { get; init; }
    public string Ownership { get; init; } = "unverified";
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
