using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Setup;

public static class LegacyImport
{
    public static SessionIdentity ImportBadge(DataPaths paths, string file, string provider, string session, string directory)
    {
        if (provider is not ("claude" or "codex") || string.IsNullOrWhiteSpace(session))
        {
            throw new ArgumentException("An explicit provider and session are required.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var data = document.RootElement;
        string Field(string name) => data.GetProperty(name).GetString() ?? throw new InvalidDataException("Legacy badge is incomplete.");
        var background = Field("background");
        var accent = Field("accent");
        if (!Palette.IsHex(background) || !Palette.IsHex(accent))
        {
            throw new InvalidDataException("Legacy badge color is invalid.");
        }

        var foreground = Palette.Contrast(accent, "#ffffff") >= Palette.Contrast(accent, "#000000") ? "#ffffff" : "#000000";
        var store = new IdentityStore(paths);
        var identity = store.Get(provider + ":" + session, directory) with
        {
            ManualName = IdentityText.Clean(Field("title")),
            Palette = new(background, accent, foreground)
        };
        store.Save(identity);
        return identity;
    }

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
