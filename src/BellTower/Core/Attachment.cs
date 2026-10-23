namespace BellTower.Core;

public sealed record Attachment(string Id, string Provider, ProcessIdentity? Process, HostTarget Host,
    string Directory, DateTimeOffset RegisteredAt, DateTimeOffset? EndedAt = null, bool ManagedTitle = false, bool ManagedBackground = false)
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
