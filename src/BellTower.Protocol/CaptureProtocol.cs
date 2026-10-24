using System.Text;
using System.Text.Json;

namespace BellTower.Protocol;

public static class CaptureProtocol
{
    public const int Version = 1;
    public const int MaximumInputBytes = 1024 * 1024;
    public const int MaximumEnvelopeBytes = 128 * 1024;
    public const string InboxName = "captures-v1";

    public static byte[] ReadBounded(Stream stream, int maximum = MaximumInputBytes)
    {
        using var result = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = stream.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)result.Length))) > 0)
        {
            result.Write(buffer, 0, count);
            if (result.Length > maximum)
            {
                throw new InvalidDataException("input-limit");
            }
        }
        return result.ToArray();
    }

    public static JsonDocument ReadInput(Stream stream)
    {
        var bytes = ReadBounded(stream);
        var start = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        // Reject invalid UTF-8 rather than silently changing identifiers.
        _ = new UTF8Encoding(false, true).GetCharCount(bytes, start, bytes.Length - start);
        var document = JsonDocument.Parse(bytes.AsMemory(start), new() { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidDataException("input-shape");
        }
        return document;
    }

    public static byte[] Encode(CaptureEnvelope envelope)
    {
        Validate(envelope);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, CaptureJson.Default.CaptureEnvelope);
        if (bytes.Length > MaximumEnvelopeBytes)
        {
            throw new InvalidDataException("envelope-limit");
        }
        return bytes;
    }

    public static CaptureEnvelope Decode(Stream stream)
    {
        var bytes = ReadBounded(stream, MaximumEnvelopeBytes);
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("protocol", out var protocol) || protocol.ValueKind != JsonValueKind.Number ||
            !protocol.TryGetInt32(out var version) || version != Version)
        {
            throw new InvalidDataException("protocol-version");
        }
        var envelope = JsonSerializer.Deserialize(bytes, CaptureJson.Default.CaptureEnvelope)
            ?? throw new InvalidDataException("envelope-empty");
        Validate(envelope);
        return envelope;
    }

    public static void Validate(CaptureEnvelope envelope)
    {
        var observation = envelope.Observation;
        var attachment = envelope.Attachment;
        if (envelope.Protocol != Version || !Guid.TryParseExact(envelope.Id, "N", out var parsedId) || parsedId.ToString("N") != envelope.Id ||
            observation is null || attachment is null || attachment.Host is null || observation.Id != envelope.Id ||
            observation.Provider is not ("claude" or "codex") || observation.Provider != attachment.Provider ||
            observation.AttachmentId != attachment.Id || !Text(observation.SessionId, 200) || !Text(attachment.Id, 200) ||
            observation.IngressAt != envelope.IngressAt || observation.At > envelope.IngressAt || observation.At == default ||
            !Text(observation.Directory, 32768) || observation.Directory != attachment.Directory || !Enum.IsDefined(observation.Kind) ||
            envelope.IngressAt > DateTimeOffset.UtcNow.AddMinutes(5) || envelope.IngressAt == default ||
            envelope.Ancestors is null || envelope.Ancestors.Count > 32 ||
            envelope.Ancestors.Any(p => p is null || p.Identity is null || p.Identity.Pid <= 0 || p.Identity.StartedUtcTicks <= 0 || !Text(p.Name, 260)) ||
            envelope.Name?.Length > 200 || envelope.Change?.Length > 200 ||
            envelope.TranscriptPath?.Length > 32768 || observation.OperationId?.Length > 256 ||
            observation.OperationFingerprint?.Length > 256 || observation.TurnId?.Length > 256 ||
            observation.BackgroundId?.Length > 256 || observation.BackgroundSnapshot?.Count > 128 ||
            observation.BackgroundSnapshot?.Any(t => t is null || !Text(t.Id, 256) || !Text(t.Type, 100)) == true ||
            envelope.Selection is { } selection && (!Text(selection.Change, 200) || !Text(selection.Operation, 64) ||
                selection.Store?.Length > 128 || selection.WorkRoot?.Length > 32768 || selection.Directory?.Length > 32768 || selection.Verb is not ("new" or "status" or "instructions" or "edit" or "skill")) ||
            envelope.SelectionRoot?.Length > 32768)
        {
            throw new InvalidDataException("envelope-invalid");
        }
    }

    private static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
}
