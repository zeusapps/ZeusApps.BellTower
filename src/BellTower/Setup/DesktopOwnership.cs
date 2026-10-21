namespace BellTower.Setup;

public sealed class DesktopOwnership
{
    public HashSet<string> Providers { get; init; } = [];
    public Dictionary<string, HashSet<string>> Projects { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, OwnedValue> Files { get; init; } = [];
    public Dictionary<string, string> Imports { get; init; } = [];
    public List<TerminalProfileOwnership> Profiles { get; init; } = [];
    public Dictionary<string, Dictionary<string, OwnedValue>> Settings { get; init; } = [];
    public bool BridgeInstalled { get; set; }
}
