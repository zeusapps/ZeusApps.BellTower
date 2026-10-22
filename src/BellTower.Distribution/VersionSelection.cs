namespace BellTower.Distribution;

public sealed record VersionSelection(string Version, string? Previous = null, string? Activation = null);
