namespace BellTower.Core;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, SessionState> Sessions { get; set; } = [];
    public HashSet<string> AppliedEvents { get; set; } = [];
}
