namespace BellTower.Sessions;

public sealed record LaunchOptions(string Provider, string? Executable, string? Name, string? Change, bool Silent, IReadOnlyList<string> Arguments);
