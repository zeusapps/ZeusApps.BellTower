namespace BellTower.Sessions;

public sealed record LaunchOptions(string Provider, string? Executable, string? Name, string? Change, string? Role, bool Silent, IReadOnlyList<string> Arguments);
