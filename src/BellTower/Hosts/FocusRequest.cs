namespace BellTower.Hosts;

/// <summary>
/// Protocol 2 asks the owning bridge only to reveal the exact terminal (mode "reveal"); the host has
/// already activated the verified window. WindowHandle names that window only when the activation was confirmed, so the bridge can discard a wrong window record. Bridges that predate protocol 2 ignore the extra fields.
/// </summary>
public sealed record FocusRequest(string Id, string InstanceId, string AttachmentId, int ShellPid, string ShellStartedUtcTicks, DateTimeOffset ExpiresAt,
    int Protocol = 2, string Mode = "reveal", long? WindowHandle = null);
