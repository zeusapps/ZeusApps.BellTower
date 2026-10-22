namespace BellTower.Core;

public sealed class UpdateSettings
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public bool Enabled { get; set; } = true;
}
