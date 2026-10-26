using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;

namespace BellTower.Tests.Testing;

// Replays a redacted provider hook capture (Fixtures/hooks/*.json: relative offsets plus payloads) through the real
// HookAdapter and reducer. These are fixtures, not live provider sessions.
public sealed class HookSequence : IDisposable
{
    private readonly JsonDocument document;
    public string Provider { get; }
    public IReadOnlyList<(int OffsetMs, JsonElement Payload)> Events { get; }

    private HookSequence(string provider, JsonDocument document)
    {
        Provider = provider;
        this.document = document;
        Events = document.RootElement.GetProperty("events").EnumerateArray()
            .Select(item => (item.GetProperty("offsetMs").GetInt32(), item.GetProperty("payload"))).ToList();
    }

    public static HookSequence Load(string name)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hooks", name)));
        return new(document.RootElement.GetProperty("provider").GetString()!, document);
    }

    public Attachment Attachment => new("attachment-" + Provider, Provider, null, new(), "D:/example", DateTimeOffset.UtcNow);

    public Observation Parse(int index, DateTimeOffset start, string? attachment = null, string? session = null)
    {
        var (offset, payload) = Events[index];
        var parsed = HookAdapter.Parse(Provider, payload, Attachment, start.AddMilliseconds(offset))!;
        return attachment is null && session is null ? parsed
            : parsed with { AttachmentId = attachment ?? parsed.AttachmentId, SessionId = session ?? parsed.SessionId };
    }

    public void Dispose() => document.Dispose();
}
