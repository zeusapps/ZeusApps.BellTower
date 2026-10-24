using System.Text.Json;
using System.Text.RegularExpressions;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Setup;

public static partial class LegacyImport
{
    private const int MaxBadgeBytes = 64 * 1024;

    public static SessionIdentity ImportBadge(DataPaths paths, string file, string provider, string session, string directory)
    {
        if (provider is not ("claude" or "codex") || string.IsNullOrWhiteSpace(session))
        {
            throw new ArgumentException("An explicit provider and session are required.");
        }

        var source = new FileInfo(file);
        if (!source.Exists || source.Length > MaxBadgeBytes)
        { throw new InvalidDataException("Legacy badge file is missing or too large."); }
        // Read-only: the source file is never rewritten, and nothing here emits an escape sequence anywhere.
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var data = document.RootElement;
        string Field(string name) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
            value.GetString() is { Length: > 0 } text ? text : throw new InvalidDataException("Legacy badge is incomplete.");
        var background = NormalizeBackground(Field("background"));
        var accent = Field("accent").Trim().ToLowerInvariant();
        if (!Palette.IsHex(background) || !Palette.IsHex(accent))
        {
            throw new InvalidDataException("Legacy badge color is invalid.");
        }

        // The better of white and black on the accent always reaches at least 4.5:1.
        var foreground = Palette.Contrast(accent, "#ffffff") >= Palette.Contrast(accent, "#000000") ? "#ffffff" : "#000000";
        var store = new IdentityStore(paths);
        var title = IdentityText.Clean(Field("title"));
        return store.Update(provider + ":" + session, directory, identity =>
        {
            identity.ManualName = title;
            identity.ColorPin = accent;
            identity.Palette = new(background, accent, foreground);
            return true;
        });
    }

    public static string NormalizeBackground(string value)
    {
        if (Palette.IsHex(value))
        { return value.ToLowerInvariant(); }
        var text = value;
        if (text.StartsWith("\u001b]11;", StringComparison.Ordinal))
        {
            text = text[5..];
            if (text.EndsWith('\a'))
            { text = text[..^1]; }
            else if (text.EndsWith("\u001b\\", StringComparison.Ordinal))
            { text = text[..^2]; }
            else
            { throw new InvalidDataException("Legacy OSC color is incomplete."); }
        }
        var match = Rgb().Match(text);
        if (!match.Success)
        { throw new InvalidDataException("Legacy badge color is invalid."); }
        return "#" + string.Concat(Enumerable.Range(1, 3).Select(index =>
        {
            var component = match.Groups[index].Value;
            var normalized = (int)Math.Round(Convert.ToInt32(component, 16) * 255d / (Math.Pow(16, component.Length) - 1));
            return normalized.ToString("x2");
        }));
    }

    [GeneratedRegex(@"\Argb:([0-9a-fA-F]{1,4})/([0-9a-fA-F]{1,4})/([0-9a-fA-F]{1,4})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Rgb();

    public static void ImportPreferences(DataPaths paths, string? config, string? position)
    {
        if (config is not null)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(config));
            if (json.RootElement.TryGetProperty("voice", out var voice) && voice.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                new ConfigStore(paths).Update(c => c.Voice = voice.GetBoolean());
            }
        }

        if (position is not null)
        {
            var saved = AtomicFile.Read<Tray.StripPosition>(position) ?? throw new InvalidDataException("Position is missing.");
            AtomicFile.Write(paths.Position, Json.Encode(saved));
        }
    }
}
