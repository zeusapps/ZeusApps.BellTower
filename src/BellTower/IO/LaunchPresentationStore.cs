using BellTower.Core;

namespace BellTower.IO;

public sealed class LaunchPresentationStore(DataPaths paths)
{
    private string FilePath(string id) => Path.Combine(paths.Root, "launches", ContentHash.Compute(id) + ".json");

    public LaunchPresentation Prepare(string id, ProcessIdentity shell)
    {
        using var gate = new DataLock(paths, "launch");
        var presentation = new LaunchPresentation(id, shell, Palette.Create(), DateTimeOffset.UtcNow);
        AtomicFile.Write(FilePath(id), Json.Encode(presentation));
        return presentation;
    }

    public Palette? Consume(string id, ProcessIdentity? shell)
    {
        var palette = Read(id, shell);
        Remove(id);
        return palette;
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
        return presentation.AttachmentId == id && presentation.Shell == shell && shell is not null &&
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
