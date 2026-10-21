namespace BellTower.Setup;

public sealed record FileChange(string Path, string? Before, string? After, bool Utf8Bom = false);
