namespace BellTower.Setup;

public sealed record IntegrationOwnership(Dictionary<string, List<string>> Commands, Dictionary<string, string> StatusLines)
{
    public DesktopOwnership Desktop { get; init; } = new();
    public Dictionary<string, OwnedValue> CodexFooters { get; init; } = [];
}
