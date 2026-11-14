namespace BellTower.Core;

public sealed record HostTarget(string Kind = "unknown", ProcessIdentity? Shell = null,
    ProcessIdentity? WindowProcess = null, long WindowHandle = 0, string? TerminalSession = null)
{
    /// <summary>ShellSource of a nested client that runs in the shell of a live managed launch: navigation uses the shell, native presentation never does.</summary>
    public const string LaunchShared = "launch-shared";

    // Where Shell came from: "ancestry" (hook process ancestry), "launch-env" (an inherited BELLTOWER_SHELL_PID), "launch-record" (an owned Codex runtime record) or "launch-shared" (a nested client in a live launch's shell). Absent in older records.
    public string? ShellSource { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
