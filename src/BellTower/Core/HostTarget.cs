namespace BellTower.Core;

public sealed record HostTarget(string Kind = "unknown", ProcessIdentity? Shell = null,
    ProcessIdentity? WindowProcess = null, long WindowHandle = 0, string? TerminalSession = null)
{
    // Where Shell came from: "ancestry" (hook process ancestry) or "launch-env" (an inherited BELLTOWER_SHELL_PID). Absent in older records.
    public string? ShellSource { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
