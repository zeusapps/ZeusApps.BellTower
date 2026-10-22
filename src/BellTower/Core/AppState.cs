namespace BellTower.Core;

public sealed class AppState
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, SessionState> Sessions { get; set; } = [];
    public HashSet<string> AppliedEvents { get; set; } = [];
}
