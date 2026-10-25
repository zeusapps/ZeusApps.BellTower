using System.Text.Json;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Diagnostics;

/// <summary>
/// Reports the VS Code bridge verification state per registered window and per live session. Sessions of both
/// providers go through the same <see cref="VsCodeNavigator"/> policy, so they are reported identically.
/// The shape is stable: Windows and Sessions are always arrays and a read failure sets Error instead.
/// </summary>
public sealed class VsCodeBridgeReporter(DataPaths paths, VsCodeNavigator navigator)
{
    public const string Summary = "requires bundled focus bridge";
    public const string States = "verified: window activation is available; partial: older bridge, terminal reveal only; unverified: window not yet observed or not provable (for example more than one window observed: run Developer: Reload Window), terminal reveal only";
    public const string EndedReason = "session process has ended";

    public VsCodeBridgeReporter(DataPaths paths) : this(paths, new VsCodeNavigator(paths))
    {
    }

    public object Report()
    {
        var windows = new List<object>();
        var sessions = new List<object>();
        string? error = null;
        try
        {
            windows.AddRange(navigator.Windows().Select(window => new { window.Instance, window.Terminals, window.State, window.Reason }));
            sessions.AddRange(Sessions());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = "registrations or attachments could not be read";
        }

        return new { Bridge = Summary, States, Windows = windows, Sessions = sessions, Error = error };
    }

    private IEnumerable<object> Sessions()
    {
        foreach (var attachment in new AttachmentStore(paths).List())
        {
            if (attachment is not { EndedAt: null, Host: { Kind: "vscode", Shell: { } shell } })
            {
                continue;
            }

            // A crashed or abandoned attachment is not a session: say so instead of blaming the registration.
            var ended = ProcessProbe.Inspect(shell) == ProcessLiveness.Ended ||
                attachment.Process is { } agent && ProcessProbe.Inspect(agent) == ProcessLiveness.Ended;
            if (ended)
            {
                yield return new { attachment.Provider, Navigation = "unavailable", Reason = EndedReason };
                continue;
            }

            var (precision, reason) = navigator.Explain(attachment);
            yield return new { attachment.Provider, Navigation = precision, Reason = reason };
        }
    }
}
