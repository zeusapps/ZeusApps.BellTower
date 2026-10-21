namespace BellTower.Core;

public sealed record HostTarget(string Kind = "unknown", ProcessIdentity? Shell = null,
    ProcessIdentity? WindowProcess = null, long WindowHandle = 0, string? TerminalSession = null);
