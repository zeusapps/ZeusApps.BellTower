using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class IdentityStore(DataPaths paths)
{
    public SessionIdentity Get(string key, string directory, string? initialName = null, string? initialChange = null, string? initialRole = null, Palette? initialPalette = null)
    {
        using var gate = new DataLock(paths, "identity");
        var path = Path.Combine(paths.Identities, ContentHash.Compute(key) + ".json");
        SessionIdentity? identity = null;
        foreach (var candidate in new[]
        {
            path,
            path + ".bak"
        })
        {
            try
            {
                identity = AtomicFile.Read<SessionIdentity>(candidate);
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
            {
                new LocalLog(paths).Write("identity-invalid");
            }

            if (identity is not null && identity.Key == key && identity.DefaultName is not null &&
                identity.Palette is { } palette && palette.Background is not null && palette.Accent is not null &&
                palette.Foreground is not null && Palette.IsHex(palette.Background) &&
                Palette.IsHex(palette.Accent) && Palette.IsHex(palette.Foreground))
            {
                return identity;
            }
        }

        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        identity = new SessionIdentity
        {
            Key = key,
            DefaultName = string.IsNullOrEmpty(name) ? "Session" : IdentityText.Clean(name),
            ManualName = initialName,
            Change = initialChange,
            Role = initialRole,
            Palette = initialPalette ?? Palette.Create()
        };
        AtomicFile.Write(path, Json.Encode(identity));
        return identity;
    }

    public void Save(SessionIdentity identity)
    {
        using var gate = new DataLock(paths, "identity");
        if (!Palette.IsHex(identity.Palette.Background) || !Palette.IsHex(identity.Palette.Accent) || !Palette.IsHex(identity.Palette.Foreground))
        {
            throw new InvalidDataException("Invalid palette.");
        }

        AtomicFile.Write(Path.Combine(paths.Identities, ContentHash.Compute(identity.Key) + ".json"), Json.Encode(identity), backup: true);
    }

}
