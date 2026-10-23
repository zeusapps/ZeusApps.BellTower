namespace BellTower.Setup;

public sealed record IntegrationOwnership(Dictionary<string, List<string>> Commands, Dictionary<string, string> StatusLines)
{
    public DesktopOwnership Desktop { get; init; } = new();
    public Dictionary<string, OwnedValue> CodexFooters { get; init; } = [];
    public Dictionary<string, List<OwnedHook>> Handlers { get; init; } = [];
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
