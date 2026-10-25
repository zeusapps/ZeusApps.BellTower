using BellTower.Core;

namespace BellTower.IO;

public sealed class AttachmentStore(DataPaths paths)
{
    public Attachment? Get(string id) => AtomicFile.Read<Attachment>(FilePath(id));
    /// <summary>Read-only listing of every readable attachment, in a stable order. Unreadable files are skipped.</summary>
    public IReadOnlyList<Attachment> List()
    {
        var result = new List<Attachment>();
        if (!Directory.Exists(paths.Attachments))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(paths.Attachments, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (AtomicFile.Read<Attachment>(file) is { } attachment)
                {
                    result.Add(attachment);
                }
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                // A partial file is not a session.
            }
        }

        return result;
    }

    public void Save(Attachment attachment) => AtomicFile.Write(FilePath(attachment.Id), Json.Encode(attachment));
    public Attachment Adopt(Attachment captured, DateTimeOffset observedAt, bool resume = false)
    {
        using var presentation = new DataLock(paths, "presentation");
        using var gate = new DataLock(paths, "attachments");
        var previous = Get(captured.Id);
        if (previous is null)
        {
            var launchShell = new LaunchPresentationStore(paths).Shell(captured.Id);
            var first = launchShell is not null
                ? captured with { Host = captured.Host with { Shell = launchShell } } : captured;
            Save(first with { Health = "unverified", UnverifiedSince = first.RegisteredAt });
            return Get(first.Id)!;
        }
        if (previous.Provider != captured.Provider || observedAt < previous.RegisteredAt ||
            previous.EndedAt is not null && (!resume || observedAt <= previous.EndedAt))
        {
            return previous;
        }
        var freshAgent = captured.Process is { } agent && ProcessProbe.Inspect(agent) == ProcessLiveness.Live;
        var replacement = resume && (previous.EndedAt is not null || freshAgent && previous.Process != captured.Process);
        var freshShell = captured.Host.Shell ?? (replacement ? null : new LaunchPresentationStore(paths).Shell(captured.Id));
        if (freshShell is not null && ProcessProbe.Inspect(freshShell) != ProcessLiveness.Live)
        {
            freshShell = null;
        }
        // A conflicting established owner belongs to a different attachment. Only explicit
        // resume may replace it; late hooks cannot transfer ownership between processes.
        if (!resume && (previous.Process is not null && captured.Process is not null && previous.Process != captured.Process ||
            previous.Host.Shell is not null && freshShell is not null && previous.Host.Shell != freshShell))
        {
            return previous;
        }
        var host = replacement ? captured.Host with
        {
            Shell = freshShell,
            AdditionalData = MergeHostExtensions(previous.Host, captured.Host)
        } : previous.Host with
        {
            Shell = previous.Host.Shell ?? freshShell,
            WindowProcess = previous.Host.WindowProcess ?? captured.Host.WindowProcess,
            WindowHandle = previous.Host.WindowHandle != 0 ? previous.Host.WindowHandle : captured.Host.WindowHandle,
            TerminalSession = previous.Host.TerminalSession ?? captured.Host.TerminalSession
        };
        // Preserve unknown host modes and extension data rather than treating them as a known host.
        if (host.Kind == "unknown" && captured.Host.Kind is "vscode" or "windows-terminal")
        {
            host = host with { Kind = captured.Host.Kind };
        }
        var updated = previous with
        {
            Process = replacement ? (freshAgent ? captured.Process : null) : previous.Process ?? (freshAgent ? captured.Process : null),
            Host = host,
            EndedAt = replacement ? null : previous.EndedAt,
            RegisteredAt = replacement ? observedAt : previous.RegisteredAt,
            UnverifiedSince = replacement ? observedAt : previous.UnverifiedSince,
            Health = replacement ? "unverified" : previous.Health
        };
        Save(updated);
        return updated;
    }

    private static Dictionary<string, System.Text.Json.JsonElement> MergeHostExtensions(HostTarget previous, HostTarget captured)
    {
        var extensions = previous.AdditionalData is null ? new Dictionary<string, System.Text.Json.JsonElement>() : new(previous.AdditionalData);
        foreach (var field in captured.AdditionalData ?? [])
        {
            extensions[field.Key] = field.Value;
        }
        return extensions;
    }

    private string FilePath(string id) => Path.Combine(paths.Attachments, ContentHash.Compute(id) + ".json");
}
