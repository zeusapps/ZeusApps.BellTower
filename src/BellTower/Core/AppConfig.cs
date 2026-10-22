namespace BellTower.Core;

public sealed class AppConfig
{
    public UpdateSettings Updates { get; set; } = new();
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public bool Voice { get; set; } = true;
    public bool Muted { get; set; }
    public string? ToneFile { get; set; }
    public string? ToneName { get; set; }
}
