namespace BellTower.Core;

public sealed record SessionIdentity
{
    public required string Key { get; init; }
    public string DefaultName { get; init; } = "Session";
    public string? ManualName { get; set; }
    public string? Change { get; set; }
    public string? Role { get; set; }
    public required Palette Palette { get; set; }
    public string Name => ManualName ?? Change ?? DefaultName;
}
