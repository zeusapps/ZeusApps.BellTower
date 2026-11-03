namespace BellTower.Setup;

public sealed record IntegrationOwnership(Dictionary<string, List<string>> Commands, Dictionary<string, string> StatusLines)
{
    public DesktopOwnership Desktop { get; init; } = new();
    public Dictionary<string, OwnedValue> CodexFooters { get; init; } = [];
    public Dictionary<string, List<OwnedHook>> Handlers { get; init; } = [];
    public string? SelectedCodexHookShell { get; init; }
    public HashSet<string> NativeBridgeProviders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
