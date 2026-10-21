namespace BellTower.Setup;

public sealed record TerminalProfileOwnership(string File, string Host, string Provider, string Key, string? Before, string After);
