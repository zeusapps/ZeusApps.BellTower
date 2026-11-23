namespace BellTower.Setup;

public sealed record IntegrationOwnership(Dictionary<string, List<string>> Commands, Dictionary<string, string> StatusLines)
{
    public DesktopOwnership Desktop { get; init; } = new();
    public Dictionary<string, OwnedValue> CodexFooters { get; init; } = [];
    public Dictionary<string, List<OwnedHook>> Handlers { get; init; } = [];
    public string? SelectedCodexHookShell { get; init; }
    /// <summary>Provider title settings setup added (opt-in step, design decision 11), by the settings file path.</summary>
    public Dictionary<string, OwnedProviderTitle> ProviderTitles { get; init; } = [];
    public HashSet<string> NativeBridgeProviders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
