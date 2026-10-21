using System.Text.Json;

namespace BellTower.Providers;

public static class HookInput
{
    public static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    public static string ReadBounded(TextReader reader, int maximum = 1024 * 1024)
    {
        var builder = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (builder.Length + count > maximum)
            {
                throw new InvalidDataException("Hook input exceeds limit.");
            }

            builder.Append(buffer, 0, count);
        }

        var input = builder.ToString();
        // Native PowerShell pipelines can prefix UTF-8 JSON with a Unicode preamble.
        return input.StartsWith('\uFEFF') ? input[1..] : input;
    }
}
