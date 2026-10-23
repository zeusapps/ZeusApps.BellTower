using System.Text.Json;

namespace BellTower.Distribution;

public static class AttachmentHealthCompatibility
{
    public static void Ensure(string dataDirectory, int consumer)
    {
        foreach (var name in new[] { "state.json", "state.json.bak" })
        {
            var path = Path.Combine(dataDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }
            JsonElement document;
            try
            {
                document = DistributionJson.Read<JsonElement>(path, 16 * 1024 * 1024);
            }
            catch (JsonException) when (consumer == 1)
            {
                continue; // The capable snapshot reader may recover its last-good backup.
            }
            if (document.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Attachment health metadata is invalid; selection was preserved.");
            }
            if (document.TryGetProperty("attachmentHealthVersion", out var version) &&
                (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var value) || value is not (0 or 1) || value > consumer))
            {
                throw new InvalidDataException("Attachment health requires a health-v1 consumer; selection and sessions were preserved. Keep a compatible tray selected.");
            }
        }
    }
}
