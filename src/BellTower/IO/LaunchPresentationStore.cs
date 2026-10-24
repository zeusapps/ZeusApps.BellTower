using BellTower.Core;

namespace BellTower.IO;

public sealed class LaunchPresentationStore(DataPaths paths)
{
    private string FilePath(string id) => Path.Combine(paths.Root, "launches", ContentHash.Compute(id) + ".json");

    public LaunchPresentation Prepare(string id, ProcessIdentity shell)
    {
        // Identity lock first (same order everywhere), so the allocation and the reservation are one atomic step
        // with respect to identity creation by either provider.
        using var identityGate = new DataLock(paths, "identity");
        using var gate = new DataLock(paths, "launch");
        var presentation = new LaunchPresentation(id, shell, new IdentityStore(paths).AllocatePalette(id), DateTimeOffset.UtcNow);
        AtomicFile.Write(FilePath(id), Json.Encode(presentation));
        return presentation;
    }

    public Palette? Consume(string id, ProcessIdentity? shell, DateTimeOffset? observedAt = null, bool requireLive = true)
    {
        using var gate = new DataLock(paths, "launch");
        var palette = Read(id, shell, observedAt, requireLive);
        var presentation = AtomicFile.Read<LaunchPresentation>(FilePath(id));
        if (palette is not null && presentation is not null)
        {
            AtomicFile.Write(FilePath(id), Json.Encode(presentation with { PaletteConsumed = true }));
        }
        return palette;
    }

    public ProcessIdentity? Shell(string id)
    {
        using var gate = new DataLock(paths, "launch");
        var presentation = AtomicFile.Read<LaunchPresentation>(FilePath(id));
        return presentation?.AttachmentId == id ? presentation.Shell : null;
    }

    public Palette? Read(string id, ProcessIdentity? shell, DateTimeOffset? observedAt = null, bool requireLive = true)
    {
        using var gate = new DataLock(paths, "launch");
        var path = FilePath(id);
        var presentation = AtomicFile.Read<LaunchPresentation>(path);
        if (presentation is null)
        {
            return null;
        }
        return !presentation.PaletteConsumed && presentation.AttachmentId == id && presentation.Shell == shell && shell is not null &&
            (!requireLive || ProcessProbe.Identify(shell.Pid) == shell) &&
            (observedAt ?? DateTimeOffset.UtcNow) >= presentation.CreatedAt &&
            (observedAt ?? DateTimeOffset.UtcNow) - presentation.CreatedAt < TimeSpan.FromHours(24) &&
            Palette.IsHex(presentation.Palette.Background) && Palette.IsHex(presentation.Palette.Accent) && Palette.IsHex(presentation.Palette.Foreground)
            ? presentation.Palette : null;
    }

    public void Remove(string id)
    {
        using var gate = new DataLock(paths, "launch");
        var path = FilePath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
