namespace BellTower.Core;

public sealed record HostTarget(string Kind = "unknown", ProcessIdentity? Shell = null,
    ProcessIdentity? WindowProcess = null, long WindowHandle = 0, string? TerminalSession = null)
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
