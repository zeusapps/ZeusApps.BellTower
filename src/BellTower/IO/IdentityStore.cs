using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class IdentityStore(DataPaths paths)
{
    public SessionIdentity Get(string key, string directory, string? initialName = null, string? initialChange = null, Palette? initialPalette = null, Func<Palette?>? launchPalette = null)
    {
        using var gate = new DataLock(paths, "identity");
        return GetCore(key, directory, initialName, initialChange, initialPalette, launchPalette);
    }

    private SessionIdentity GetCore(string key, string directory, string? initialName = null, string? initialChange = null,
        Palette? initialPalette = null, Func<Palette?>? launchPalette = null)
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
            Palette = initialPalette ?? launchPalette?.Invoke() ?? AllocatePalette(key)
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
    // creations from either provider observe each other's accents. Each session or pending launch counts once. A color is in use by:
    // a session not ended in the monitoring state (an unavailable one only while observed in the last hour); an identity written
    // in the last hour whose session the state does not list yet (it registered but was not drained); a launch palette that is
    // unconsumed, within the launch reader's validity and whose recorded shell is still live; and the session's own saved color,
    // so reset and random color always move to another color. Ended sessions and older identity files do not count. The state is
    // read without its lock: snapshots are replaced atomically, and taking it here could invert the lock order.
    internal Palette AllocatePalette(string key)
    {
        var inUse = new Dictionary<string, string>(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var state = new StateSnapshotStore(paths).Read();
        void Add(string owner, string? accent)
        {
            if (accent is not null && Palette.IsHex(accent))
            { inUse[owner] = accent; }
        }
        string? Accent(string identityKey) => Read(() => AtomicFile.Read<SessionIdentity>(IdentityPath(identityKey)) is { } saved && saved.Key == identityKey ? saved.Palette?.Accent : null);
        foreach (var session in state.Sessions.Values.Where(session => CountsAsLive(session, now)))
        { Add(session.Key, Accent(session.Key)); }
        var own = Accent(key);
        Add(key, own);
        if (Directory.Exists(paths.Identities))
        {
            // Files of sessions the state lists were handled above (live) or must not count (ended), so only unlisted ones are read.
            var listed = state.Sessions.Keys.Select(listedKey => ContentHash.Compute(listedKey) + ".json").ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in new DirectoryInfo(paths.Identities).EnumerateFiles("*.json").Where(file => now - file.LastWriteTimeUtc < PendingWindow && !listed.Contains(file.Name)))
            {
                var saved = Read(() => AtomicFile.Read<SessionIdentity>(file.FullName) is { } identity && !state.Sessions.ContainsKey(identity.Key) ? identity : null);
                if (saved is not null)
                { Add(saved.Key, saved.Palette?.Accent); }
            }
        }
        var launches = Path.Combine(paths.Root, "launches");
        if (Directory.Exists(launches))
        {
            foreach (var file in new DirectoryInfo(launches).EnumerateFiles("*.json"))
            {
                var launch = Read(() => AtomicFile.Read<LaunchPresentation>(file.FullName) is { PaletteConsumed: false } pending && pending.AttachmentId != key &&
                    now - pending.CreatedAt < LaunchPresentationStore.Validity && ProcessProbe.Identify(pending.Shell.Pid) == pending.Shell ? pending : null);
                if (launch is not null)
                { Add("launch:" + launch.AttachmentId, launch.Palette?.Accent); }
            }
        }
        return Palette.Allocate(inUse.Values.ToArray(), own);
    }

    // Verified and unverified sessions count; an unavailable one only while it was observed recently, so stale conversations
    // that never sent an end event do not hold colors indefinitely.
    private static bool CountsAsLive(SessionState session, DateTimeOffset now) =>
        !session.Ended && (session.AttachmentHealth != "unavailable" || now - session.LastObserved < PendingWindow);

    private string IdentityPath(string key) => Path.Combine(paths.Identities, ContentHash.Compute(key) + ".json");

    private static T? Read<T>(Func<T?> read) where T : class
    {
        try
        { return read(); }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        { return null; }
    }

    // How long a registering identity reserves its color, and how recently an unavailable session must have been observed to keep one.
    private static readonly TimeSpan PendingWindow = TimeSpan.FromHours(1);

    private void SaveCore(SessionIdentity identity)
    {
        if (!Palette.IsHex(identity.Palette.Background) || !Palette.IsHex(identity.Palette.Accent) || !Palette.IsHex(identity.Palette.Foreground))
        {
            throw new InvalidDataException("Invalid palette.");
        }

        AtomicFile.Write(Path.Combine(paths.Identities, ContentHash.Compute(identity.Key) + ".json"), Json.Encode(identity), backup: true);
    }

}
