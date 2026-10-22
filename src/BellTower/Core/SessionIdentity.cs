namespace BellTower.Core;

public sealed record SessionIdentity
{
    public required string Key { get; init; }
    public string DefaultName { get; init; } = "Session";
    public string? ManualName { get; set; }
    public string? Change { get; set; }
    public string? Role { get; set; }
    public string? SelectionOperation { get; set; }
    public List<string> SelectionReceipts { get; init; } = [];
    public string? SelectionRoot { get; set; }
    public string? SelectionStore { get; set; }
    public bool NativeSyncEnabled { get; set; } = true;
    public bool NativeSyncTakeOwnership { get; set; }
    public string? NativeLastName { get; set; }
    public string NativeSyncStatus { get; set; } = "unavailable";
    public required Palette Palette { get; set; }
    public string Name => ManualName ?? Change ?? DefaultName;
}
