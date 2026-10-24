using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class IdentityStore(DataPaths paths, Func<Palette>? candidate = null)
{
    public SessionIdentity Get(string key, string directory, string? initialName = null, string? initialChange = null, Palette? initialPalette = null)
    {
        using var gate = new DataLock(paths, "identity");
        return GetCore(key, directory, initialName, initialChange, initialPalette);
    }

    private SessionIdentity GetCore(string key, string directory, string? initialName = null, string? initialChange = null,
        Palette? initialPalette = null)
    {
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
                identity.SelectionEvidence is not null && identity.SelectionEvidence.Count <= 128 &&
                identity.Palette is { } palette && palette.Background is not null && palette.Accent is not null &&
                palette.Foreground is not null && Palette.IsHex(palette.Background) &&
                Palette.IsHex(palette.Accent) && Palette.IsHex(palette.Foreground))
            {
                // Classify an older record's Change once; the marker is persisted by the next save.
                ChangeOrigin.Classify(identity);
                return identity;
            }
        }

        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        identity = new SessionIdentity
        {
            Key = key,
            DefaultName = string.IsNullOrEmpty(name) ? "Session" : IdentityText.Clean(name),
            ManualName = initialName,
            LaunchDirectory = directory,
            Palette = initialPalette ?? AllocatePalette(key)
        };
        ChangeOrigin.Pin(identity, initialChange, ChangeOrigin.Launch);
        AtomicFile.Write(path, Json.Encode(identity));
        return identity;
    }

    // Reads an existing identity without creating one (null when absent or unreadable).
    public SessionIdentity? Find(string key)
    {
        using var gate = new DataLock(paths, "identity");
        var path = Path.Combine(paths.Identities, ContentHash.Compute(key) + ".json");
        try
        {
            return AtomicFile.Read<SessionIdentity>(path) is { } found && found.Key == key ? found : null;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    public void Save(SessionIdentity identity)
    {
        using var gate = new DataLock(paths, "identity");
        SaveCore(identity);
    }

    public SessionIdentity Update(string key, string directory, Func<SessionIdentity, bool> update)
    {
        using var gate = new DataLock(paths, "identity");
        var identity = GetCore(key, directory);
        if (update(identity))
        {
            SaveCore(identity);
        }
        return identity;
    }

    public SessionIdentity SetColor(string key, string directory, string? color, bool reset = false) => Update(key, directory, identity =>
    {
        if (reset)
        { IdentityControls.Reset(identity, () => AllocatePalette(key)); }
        if (color is not null || !reset)
        { IdentityControls.ApplyColor(identity, color, () => AllocatePalette(key)); }
        return true;
    });

    // Callers must hold the "identity" lock (Get/Update do; the launch preparer takes it explicitly) so concurrent
    // creations from either provider observe each other's accents. Live-session proxies: the most recent
    // identities plus every launch palette that was prepared in the last day (consumed or not).
    internal Palette AllocatePalette(string key)
    {
        var accents = new List<string>();
        void Collect(string directory, Func<string, string?> accent)
        {
            if (!Directory.Exists(directory))
            { return; }
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(file => file.LastWriteTimeUtc).Take(RecentLimit))
            {
                try
                {
                    if (accent(file.FullName) is { } value && Palette.IsHex(value))
                    { accents.Add(value); }
                }
                catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { }
            }
        }
        Collect(paths.Identities, file => AtomicFile.Read<SessionIdentity>(file) is { } saved && saved.Key != key ? saved.Palette?.Accent : null);
        Collect(Path.Combine(paths.Root, "launches"), file => AtomicFile.Read<LaunchPresentation>(file) is { } launch &&
            launch.AttachmentId != key && DateTimeOffset.UtcNow - launch.CreatedAt < TimeSpan.FromHours(24) ? launch.Palette?.Accent : null);
        return Palette.Allocate(accents, candidate);
    }

    private const int RecentLimit = 64;

    private void SaveCore(SessionIdentity identity)
    {
        if (!Palette.IsHex(identity.Palette.Background) || !Palette.IsHex(identity.Palette.Accent) || !Palette.IsHex(identity.Palette.Foreground))
        {
            throw new InvalidDataException("Invalid palette.");
        }

        AtomicFile.Write(Path.Combine(paths.Identities, ContentHash.Compute(identity.Key) + ".json"), Json.Encode(identity), backup: true);
    }

}
