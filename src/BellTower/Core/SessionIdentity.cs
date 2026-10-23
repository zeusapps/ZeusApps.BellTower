namespace BellTower.Core;

public sealed record SessionIdentity
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public required string Key { get; init; }
    public string DefaultName { get; init; } = "Session";
    public string? ManualName { get; set; }
    public string? Change { get; set; }
    public string? Role { get; set; }
    public string? SelectionOperation { get; set; }
    public DateTimeOffset? SelectionAt { get; set; }
    public string? SelectionEvent { get; set; }
    public List<string> SelectionReceipts { get; init; } = [];
    public List<SelectionEvidence> SelectionEvidence { get; set; } = [];
    public DateTimeOffset? SelectionFloorAt { get; set; }
    public string? SelectionRoot { get; set; }
    public string? SelectionStore { get; set; }
    public bool NativeSyncEnabled { get; set; } = true;
    public bool NativeSyncTakeOwnership { get; set; }
    public string? NativeLastName { get; set; }
    public string NativeSyncStatus { get; set; } = "unavailable";
    public required Palette Palette { get; set; }
    public string Name => ManualName ?? Change ?? DefaultName;
}
